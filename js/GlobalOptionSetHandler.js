(function (GlobalOptionSetHandler, undefined) {
    "use strict";

    var idSeparator = "|";

    function GetComponent() {
        var component = XrmTranslator.GetComponent();
        if (component === "DisplayName") {
            return "Label";
        }
        return component;
    }

    function IsDescriptionComponent() {
        return XrmTranslator.GetComponent() === "Description";
    }

    function AddLocalizedLabelsToRecord(record, localizedLabels) {
        localizedLabels = localizedLabels || [];

        for (var i = 0; i < localizedLabels.length; i++) {
            record[localizedLabels[i].LanguageCode.toString()] = localizedLabels[i].Label;
        }
    }

    function GetChangedLabels(changes, allowEmpty) {
        var labels = [];

        for (var change in changes) {
            if (!changes.hasOwnProperty(change)) {
                continue;
            }

            if (changes[change] == null || (!allowEmpty && !changes[change])) {
                continue;
            }

            labels.push({ LanguageCode: change, Label: changes[change] });
        }

        return labels;
    }

    function BuildOptionSetDescriptionUpdate(optionSet, labels) {
        var update = JSON.parse(JSON.stringify(optionSet));
        delete update["@odata.context"];
        delete update["@odata.etag"];

        update.Description = update.Description || {};
        update.Description.LocalizedLabels = labels;

        return {
            metadataId: optionSet.MetadataId,
            optionSetName: optionSet.Name,
            payload: update
        };
    }

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        var optionValueUpdates = [];
        var optionSetDescriptionUpdates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var parts = record.recid.split(idSeparator);
            var optionSetId = parts[0];
            var optionSet = XrmTranslator.GetAttributeById(optionSetId);

            if (!optionSet) {
                continue;
            }

            var changes = record.w2ui.changes;
            var labels;

            if (parts.length === 1) {
                if (!IsDescriptionComponent()) {
                    continue;
                }

                labels = GetChangedLabels(changes, true);
                if (labels.length < 1) {
                    continue;
                }

                optionSetDescriptionUpdates.push(BuildOptionSetDescriptionUpdate(optionSet, labels));
                continue;
            }

            var optionValue = parseInt(record.schemaName);
            if (isNaN(optionValue)) {
                continue;
            }

            labels = GetChangedLabels(changes, false);
            if (labels.length < 1) {
                continue;
            }

            optionValueUpdates.push({
                Value: optionValue,
                [GetComponent()]: { LocalizedLabels: labels },
                MergeLabels: true,
                OptionSetName: optionSet.Name
            });
        }

        return {
            optionValueUpdates: optionValueUpdates,
            optionSetDescriptionUpdates: optionSetDescriptionUpdates
        };
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        grid.clear();
        var records = [];
        var isDescription = IsDescriptionComponent();

        for (var i = 0; i < XrmTranslator.metadata.length; i++) {
            var optionSet = XrmTranslator.metadata[i];

            var parent = {
                recid: optionSet.MetadataId,
                schemaName: optionSet.Name,
                w2ui: { editable: isDescription, children: [] }
            };

            if (isDescription && optionSet.Description) {
                AddLocalizedLabelsToRecord(parent, optionSet.Description.LocalizedLabels);
            }

            if (!!optionSet.TrueOption) {
                var options = [optionSet.TrueOption, optionSet.FalseOption];
                for (var j = 0; j < options.length; j++) {
                    var option = options[j];
                    var component = option[GetComponent()] || {};
                    var labels = component.LocalizedLabels || [];
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    AddLocalizedLabelsToRecord(child, labels);
                    parent.w2ui.children.push(child);
                }
            }
            else {
                var options = optionSet.Options;
                if (!options || options.length === 0) {
                    if (!isDescription) {
                        continue;
                    }
                }
                for (var j = 0; options && j < options.length; j++) {
                    var option = options[j];
                    var component = option[GetComponent()] || {};
                    var labels = component.LocalizedLabels || [];
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    AddLocalizedLabelsToRecord(child, labels);
                    parent.w2ui.children.push(child);
                }
            }

            records.push(parent);
        }

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    function GetSelectedSolutionOptionSetIds() {
        var solutionId = XrmTranslator.GetSolution();

        if (!solutionId || solutionId === "all") {
            return WebApiClient.Promise.resolve([]);
        }

        return WebApiClient.Retrieve({
            entityName: "solutioncomponent",
            queryParams: "?$select=objectid&$filter=_solutionid_value eq " + solutionId + " and componenttype eq " + XrmTranslator.ComponentType.OptionSet
        })
        .then(function(response) {
            return (response.value || []).map(function(component) {
                return component.objectid;
            });
        });
    }

    function RetrieveGlobalOptionSet(optionSetId) {
        var url = WebApiClient.GetApiUrl() + "GlobalOptionSetDefinitions(" + optionSetId + ")";
        return WebApiClient.SendRequest("GET", url)
        .then(function(response) {
            return typeof response === "string" ? JSON.parse(response) : response;
        });
    }

    GlobalOptionSetHandler.Load = function () {
        return GetSelectedSolutionOptionSetIds()
            .then(function(optionSetIds) {
                if (optionSetIds.length === 0) {
                    return [];
                }

                return WebApiClient.Promise.all(optionSetIds.map(RetrieveGlobalOptionSet));
            })
            .then(function (optionSets) {
                optionSets = optionSets.filter(function (os) {
                    return os && os.IsCustomizable && os.IsCustomizable.Value && os.IsGlobal;
                });

                optionSets.sort(function (a, b) {
                    return (a.Name || "").localeCompare(b.Name || "");
                });

                XrmTranslator.metadata = optionSets;
                FillTable();
            })
            .catch(XrmTranslator.errorHandler);
    };

    GlobalOptionSetHandler.Save = function () {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                var updates = GetUpdates();
                var optionValueUpdates = updates.optionValueUpdates || [];
                var optionSetDescriptionUpdates = updates.optionSetDescriptionUpdates || [];

                if (optionValueUpdates.length === 0 && optionSetDescriptionUpdates.length === 0) {
                    return {
                        optionSetNames: []
                    };
                }

                var optionSetNames = [];
                optionValueUpdates.forEach(function (u) {
                    if (optionSetNames.indexOf(u.OptionSetName) === -1) {
                        optionSetNames.push(u.OptionSetName);
                    }
                });
                optionSetDescriptionUpdates.forEach(function (u) {
                    if (optionSetNames.indexOf(u.optionSetName) === -1) {
                        optionSetNames.push(u.optionSetName);
                    }
                });

                var saveChain = Promise.resolve();

                if (optionSetDescriptionUpdates.length > 0) {
                    saveChain = saveChain.then(function () {
                        return XrmTranslator.ExecuteChangeSetBatches(optionSetDescriptionUpdates, {
                            progressLabel: "Saving " + XrmTranslator.GetCurrentToolbarTypeText(),
                            batchNamePrefix: "batch_updateglobaloptionset",
                            changeSetNamePrefix: "changeset_updateglobaloptionset",
                            buildRequest: function(update) {
                                return new WebApiClient.BatchRequest({
                                    method: "PUT",
                                    url: WebApiClient.GetApiUrl() + "GlobalOptionSetDefinitions(" + update.metadataId + ")",
                                    payload: update.payload,
                                    headers: [{ key: "MSCRM.MergeLabels", value: "true" }]
                                });
                            }
                        });
                    });
                }

                if (optionValueUpdates.length > 0) {
                    saveChain = saveChain.then(function () {
                        return XrmTranslator.ExecuteChangeSetBatches(optionValueUpdates, {
                            progressLabel: "Saving " + XrmTranslator.GetCurrentToolbarTypeText(),
                            batchNamePrefix: "batch_updateglobaloptionvalue",
                            changeSetNamePrefix: "changeset_updateglobaloptionvalue",
                            buildRequest: function(payload) {
                                return new WebApiClient.BatchRequest({
                                    method: "POST",
                                    url: WebApiClient.GetApiUrl() + "UpdateOptionValue",
                                    payload: payload
                                });
                            }
                        });
                    });
                }

                return saveChain
                .then(function () {
                    return {
                        optionSetNames: optionSetNames
                    };
                });
            },
            shouldPublish: function (result) {
                return !!(result && result.optionSetNames && result.optionSetNames.length > 0);
            },
            publishAction: function (result) {
                var optionSetNames = result && result.optionSetNames ? result.optionSetNames : [];

                if (optionSetNames.length === 0) {
                    return Promise.resolve();
                }

                return XrmTranslator.RunAsBaseLanguage(function () {
                    var optionSetXml = optionSetNames.map(function (n) {
                        return "<optionset>" + n + "</optionset>";
                    }).join("");
                    var xml = "<importexportxml><optionsets>" + optionSetXml + "</optionsets></importexportxml>";

                    var request = WebApiClient.Requests.PublishXmlRequest.with({
                        payload: { ParameterXml: xml }
                    });
                    return WebApiClient.Execute(request);
                });
            },
            reloadAction: function () {
                return GlobalOptionSetHandler.Load();
            }
        });
    };

}(window.GlobalOptionSetHandler = window.GlobalOptionSetHandler || {}));
