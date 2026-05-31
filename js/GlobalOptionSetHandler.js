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

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        var updates = [];

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

            var optionValue = parseInt(record.schemaName);
            if (isNaN(optionValue)) {
                continue;
            }

            var changes = record.w2ui.changes;
            var labels = [];

            for (var change in changes) {
                if (!changes.hasOwnProperty(change) || !changes[change]) {
                    continue;
                }
                labels.push({ LanguageCode: change, Label: changes[change] });
            }

            if (labels.length < 1) {
                continue;
            }

            updates.push({
                Value: optionValue,
                [GetComponent()]: { LocalizedLabels: labels },
                MergeLabels: true,
                OptionSetName: optionSet.Name
            });
        }

        return updates;
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        grid.clear();
        var records = [];

        for (var i = 0; i < XrmTranslator.metadata.length; i++) {
            var optionSet = XrmTranslator.metadata[i];

            var parent = {
                recid: optionSet.MetadataId,
                schemaName: optionSet.Name,
                w2ui: { editable: false, children: [] }
            };

            if (!!optionSet.TrueOption) {
                var options = [optionSet.TrueOption, optionSet.FalseOption];
                for (var j = 0; j < options.length; j++) {
                    var option = options[j];
                    var labels = option[GetComponent()].LocalizedLabels;
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    for (var k = 0; k < labels.length; k++) {
                        child[labels[k].LanguageCode.toString()] = labels[k].Label;
                    }
                    parent.w2ui.children.push(child);
                }
            }
            else {
                var options = optionSet.Options;
                if (!options || options.length === 0) {
                    continue;
                }
                for (var j = 0; j < options.length; j++) {
                    var option = options[j];
                    var labels = option[GetComponent()].LocalizedLabels;
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    for (var k = 0; k < labels.length; k++) {
                        child[labels[k].LanguageCode.toString()] = labels[k].Label;
                    }
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

                if (!updates || updates.length === 0) {
                    return {
                        optionSetNames: [],
                        optionSetIds: []
                    };
                }

                var optionSetNames = [];
                updates.forEach(function (u) {
                    if (optionSetNames.indexOf(u.OptionSetName) === -1) {
                        optionSetNames.push(u.OptionSetName);
                    }
                });

                var optionSetIds = [];
                for (var i = 0; i < XrmTranslator.metadata.length; i++) {
                    var os = XrmTranslator.metadata[i];
                    if (optionSetNames.indexOf(os.Name) !== -1 && optionSetIds.indexOf(os.MetadataId) === -1) {
                        optionSetIds.push(os.MetadataId);
                    }
                }

                return XrmTranslator.ExecuteChangeSetBatches(updates, {
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
                })
                .then(function () {
                    return {
                        optionSetNames: optionSetNames,
                        optionSetIds: optionSetIds
                    };
                });
            },
            publishAction: function (result) {
                var optionSetNames = result && result.optionSetNames ? result.optionSetNames : [];
                var optionSetIds = result && result.optionSetIds ? result.optionSetIds : [];

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
                })
                .then(function () {
                    return XrmTranslator.AddToSolution(optionSetIds, XrmTranslator.ComponentType.OptionSet, true, true);
                });
            },
            reloadAction: function () {
                return GlobalOptionSetHandler.Load();
            }
        });
    };

}(window.GlobalOptionSetHandler = window.GlobalOptionSetHandler || {}));
