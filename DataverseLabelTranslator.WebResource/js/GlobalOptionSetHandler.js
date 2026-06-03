window.GlobalOptionSetHandler = Object(window.GlobalOptionSetHandler);

(function (GlobalOptionSetHandler, undefined) {
    "use strict";

    var idSeparator = "|";

    function IsEmptyLabelValue(value) {
        return value == null || String(value).trim().length === 0;
    }

    function GetLanguageColumnText(languageCode) {
        var columns = XrmTranslator.GetGrid().columns || [];
        var field = String(languageCode);

        for (var i = 0; i < columns.length; i++) {
            if (String(columns[i].field) === field) {
                return columns[i].text || columns[i].caption || columns[i].label || field;
            }
        }

        return field;
    }

    function GetDisplayTextRowPath(record, optionSet) {
        var optionSetName = optionSet && optionSet.Name ? optionSet.Name : "";
        var rowName = record && record.schemaName != null ? String(record.schemaName) : "";

        if (optionSetName && rowName && optionSetName !== rowName) {
            return optionSetName + " > " + rowName;
        }

        return rowName || optionSetName || "(unknown)";
    }

    function ValidateDisplayTextBaseLanguageChange(record, optionSet, changes) {
        if (!XrmTranslator.IsDisplayTextComponent() || !XrmTranslator.baseLanguage) {
            return;
        }

        var baseLanguage = String(XrmTranslator.baseLanguage);
        if (!Object.prototype.hasOwnProperty.call(changes, baseLanguage)) {
            return;
        }

        if (!IsEmptyLabelValue(changes[baseLanguage])) {
            return;
        }

        throw new Error(
            "Display Text in the base language (" +
                GetLanguageColumnText(baseLanguage) +
                ") cannot be empty.\n" +
                "Row: " +
                GetDisplayTextRowPath(record, optionSet)
        );
    }

    function ApplyEmptyEditablePlaceholder(record, editablePlaceholder, basePlaceholder) {
        if (!editablePlaceholder) {
            return;
        }

        record._emptyEditablePlaceholder = editablePlaceholder;

        if (basePlaceholder && XrmTranslator.baseLanguage) {
            record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
            record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = basePlaceholder;
        }
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

            var label = changes[change];
            if (label == null) {
                if (!allowEmpty) {
                    continue;
                }
                label = "";
            }

            if (!allowEmpty && !label) {
                continue;
            }

            labels.push({ LanguageCode: change, Label: label });
        }

        return labels;
    }

    function BuildOptionSetDescriptionUpdate(optionSet, labels) {
        return {
            optionSetName: optionSet.Name,
            labels: labels
        };
    }

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        var optionValueUpdates = [];
        var optionSetDescriptionUpdates = [];
        var isDescription = XrmTranslator.IsDescriptionComponent();
        var isDisplayText = XrmTranslator.IsDisplayTextComponent();
        var component = isDisplayText ? "Label" : XrmTranslator.GetComponent();

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
            ValidateDisplayTextBaseLanguageChange(record, optionSet, changes);

            if (parts.length === 1) {
                if (!isDescription) {
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

            labels = GetChangedLabels(changes, isDescription || isDisplayText);
            if (labels.length < 1) {
                continue;
            }

            optionValueUpdates.push({
                optionSetName: optionSet.Name,
                value: optionValue,
                component: component,
                labels: labels
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
        var isDescription = XrmTranslator.IsDescriptionComponent();
        var isDisplayText = XrmTranslator.IsDisplayTextComponent();
        var componentName = isDisplayText ? "Label" : XrmTranslator.GetComponent();
        var editablePlaceholder = isDescription ? "Add description" : isDisplayText ? "Add display text" : null;
        var baseEditablePlaceholder = isDisplayText ? "Add display text (*)" : null;

        for (var i = 0; i < XrmTranslator.metadata.length; i++) {
            var optionSet = XrmTranslator.metadata[i];

            var parent = {
                recid: optionSet.MetadataId,
                schemaName: optionSet.Name,
                w2ui: { editable: isDescription, children: [] }
            };

            if (isDescription) {
                ApplyEmptyEditablePlaceholder(parent, editablePlaceholder, baseEditablePlaceholder);
                if (optionSet.Description) {
                    AddLocalizedLabelsToRecord(parent, optionSet.Description.LocalizedLabels);
                }
            } else if (!isDescription) {
                parent._emptyReadonlyPlaceholder = "-";
            }

            if (!!optionSet.TrueOption) {
                var options = [optionSet.TrueOption, optionSet.FalseOption];
                for (var j = 0; j < options.length; j++) {
                    var option = options[j];
                    var component = option[componentName] || {};
                    var labels = component.LocalizedLabels || [];
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    ApplyEmptyEditablePlaceholder(child, editablePlaceholder, baseEditablePlaceholder);
                    AddLocalizedLabelsToRecord(child, labels);
                    parent.w2ui.children.push(child);
                }
            } else {
                var options = optionSet.Options;
                if (!options || options.length === 0) {
                    if (!isDescription) {
                        continue;
                    }
                }
                for (var j = 0; options && j < options.length; j++) {
                    var option = options[j];
                    var component = option[componentName] || {};
                    var labels = component.LocalizedLabels || [];
                    var child = {
                        recid: optionSet.MetadataId + idSeparator + option.Value,
                        schemaName: option.Value.toString()
                    };
                    ApplyEmptyEditablePlaceholder(child, editablePlaceholder, baseEditablePlaceholder);
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
            queryParams:
                "?$select=objectid&$filter=_solutionid_value eq " +
                solutionId +
                " and componenttype eq " +
                XrmTranslator.ComponentType.OptionSet
        }).then(function (response) {
            return (response.value || []).map(function (component) {
                return component.objectid;
            });
        });
    }

    function RetrieveGlobalOptionSet(optionSetId) {
        var url = WebApiClient.GetApiUrl() + "GlobalOptionSetDefinitions(" + optionSetId + ")";
        return WebApiClient.SendRequest("GET", url).then(function (response) {
            return typeof response === "string" ? JSON.parse(response) : response;
        });
    }

    GlobalOptionSetHandler.Load = function () {
        return GetSelectedSolutionOptionSetIds()
            .then(function (optionSetIds) {
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
            .catch(function (error) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.errorHandler(error);
            });
    };

    function GetOptionSetNames(result) {
        return result && result.optionSetNames ? result.optionSetNames : [];
    }

    function PublishOptionSets(optionSetNames) {
        var optionSetXml = optionSetNames
            .map(function (n) {
                return "<optionset>" + n + "</optionset>";
            })
            .join("");
        var xml = "<importexportxml><optionsets>" + optionSetXml + "</optionsets></importexportxml>";

        var request = WebApiClient.Requests.PublishXmlRequest.with({
            payload: { ParameterXml: xml }
        });
        return WebApiClient.Execute(request);
    }

    GlobalOptionSetHandler.Save = function () {
        var updates = GetUpdates();
        var optionValueUpdates = updates.optionValueUpdates;
        var optionSetDescriptionUpdates = updates.optionSetDescriptionUpdates;
        var component = XrmTranslator.IsDisplayTextComponent() ? "Label" : XrmTranslator.GetComponent();

        if (optionValueUpdates.length === 0 && optionSetDescriptionUpdates.length === 0) {
            XrmTranslator.UnlockGrid();
            XrmTranslator.EnableLoadAndSave();
            return Promise.resolve({ optionSetNames: [] });
        }

        XrmTranslator.LockGrid("Saving ...");

        return Helper.ExecuteCustomAction("GlobalOptionSet", {
            component: component,
            optionValueUpdates: optionValueUpdates,
            optionSetDescriptionUpdates: optionSetDescriptionUpdates
        })
            .then(function (result) {
                var optionSetNames = GetOptionSetNames(result);

                if (optionSetNames.length === 0) {
                    XrmTranslator.UnlockGrid();
                    XrmTranslator.EnableLoadAndSave();
                    return result;
                }

                XrmTranslator.LockGrid("Publishing ...");
                return XrmTranslator.RunAsBaseLanguage(function () {
                    return PublishOptionSets(optionSetNames);
                }).then(function () {
                    XrmTranslator.LockGrid("Published");
                    return result;
                });
            })
            .then(function (result) {
                if (GetOptionSetNames(result).length === 0) {
                    return result;
                }

                XrmTranslator.LockGrid("Re-Loading ...");
                return GlobalOptionSetHandler.Load().then(function () {
                    XrmTranslator.EnableLoadAndSave();
                    return result;
                });
            })
            .catch(function (error) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                throw error;
            });
    };
})(window.GlobalOptionSetHandler);
