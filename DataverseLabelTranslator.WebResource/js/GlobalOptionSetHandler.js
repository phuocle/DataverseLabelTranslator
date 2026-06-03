window.GlobalOptionSetHandler = Object(window.GlobalOptionSetHandler);

(function (GlobalOptionSetHandler, undefined) {
    "use strict";

    var idSeparator = "|";
    var actionName = "GlobalOptionSet";

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

        if (!Helper.IsEmptyLabelValue(changes[baseLanguage])) {
            return;
        }

        throw new Error(
            "Display Text in the base language (" +
                Helper.GetLanguageColumnText(baseLanguage) +
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

    GlobalOptionSetHandler.Load = function () {
        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, {
            solutionId: XrmTranslator.GetSolution()
        })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);
                var optionSets = output.optionSets || [];
                optionSets = optionSets.filter(function (os) {
                    return os && os.IsCustomizable && os.IsCustomizable.Value && os.IsGlobal;
                });

                optionSets.sort(function (a, b) {
                    return (a.Name || "").localeCompare(b.Name || "");
                });

                XrmTranslator.metadata = optionSets;
                FillTable();
                return result;
            })
            .catch(function (error) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.errorHandler(error);
            });
    };

    function GetOptionSetNames(result) {
        return result && result.optionSetNames ? result.optionSetNames : [];
    }

    GlobalOptionSetHandler.Save = function () {
        var updates = GetUpdates();
        var optionValueUpdates = updates.optionValueUpdates;
        var optionSetDescriptionUpdates = updates.optionSetDescriptionUpdates;
        var component = XrmTranslator.IsDisplayTextComponent() ? "Label" : XrmTranslator.GetComponent();

        XrmTranslator.LockGrid("Saving ...");

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, {
            component: component,
            optionValueUpdates: optionValueUpdates,
            optionSetDescriptionUpdates: optionSetDescriptionUpdates
        })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);
                var optionSetNames = GetOptionSetNames(output);

                if (optionSetNames.length === 0) {
                    XrmTranslator.UnlockGrid();
                    XrmTranslator.EnableLoadAndSave();
                    return result;
                }

                XrmTranslator.LockGrid("Publishing ...");
                return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, {
                    optionSetNames: optionSetNames
                }).then(function (publishingResult) {
                    var publishingOutput = Helper.GetCustomActionObject(publishingResult);
                    XrmTranslator.LockGrid("Published");
                    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, {
                        optionSetNames: GetOptionSetNames(publishingOutput)
                    });
                });
            })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);
                if (GetOptionSetNames(output).length === 0) {
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
