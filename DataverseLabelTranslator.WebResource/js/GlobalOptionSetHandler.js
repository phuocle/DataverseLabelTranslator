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

    function BuildOptionSetDescriptionUpdate(optionSet, labels) {
        return {
            optionSetName: optionSet.Name,
            labels: labels
        };
    }

    function GetComponent(app) {
        return app.IsDescriptionComponent() ? Helper.ComponentTypes.Description : Helper.ComponentTypes.DisplayText;
    }

    function AddOptionChild(parent, optionSet, option, component, editablePlaceholder, baseEditablePlaceholder, app) {
        var labels = Helper.GetComponentLocalizedLabels(option, component);
        var child = {
            recid: optionSet.MetadataId + idSeparator + option.Value,
            schemaName: option.Value.toString()
        };

        Helper.ApplyPlaceholder(child, editablePlaceholder, baseEditablePlaceholder, app);
        Helper.AddLocalizedLabelsToRecord(child, labels);
        parent.w2ui.children.push(child);
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var optionValueUpdates = [];
        var optionSetDescriptionUpdates = [];
        var isDescription = app.IsDescriptionComponent();
        var isDisplayText = app.IsDisplayTextComponent();

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var parts = record.recid.split(idSeparator);
            var optionSetId = parts[0];
            var optionSet = app.GetAttributeById(optionSetId);

            if (!optionSet) {
                continue;
            }

            var changes = record.w2ui.changes;
            var labels;
            Helper.ValidateBaseLanguageNotEmpty(record, changes, {
                app: app,
                getRowPath: function (changedRecord) {
                    return GetDisplayTextRowPath(changedRecord, optionSet);
                }
            });

            if (parts.length === 1) {
                if (!isDescription) {
                    continue;
                }

                labels = Helper.GetChangedLabels(changes, true);
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

            labels = Helper.GetChangedLabels(changes, isDescription || isDisplayText);
            if (labels.length < 1) {
                continue;
            }

            optionValueUpdates.push({
                optionSetName: optionSet.Name,
                value: optionValue,
                labels: labels
            });
        }

        return {
            optionValueUpdates: optionValueUpdates,
            optionSetDescriptionUpdates: optionSetDescriptionUpdates
        };
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        grid.clear();
        var records = [];
        var isDescription = app.IsDescriptionComponent();
        var isDisplayText = app.IsDisplayTextComponent();
        var component = GetComponent(app);
        var editablePlaceholder = isDescription
            ? Helper.GetPlaceholderDescription()
            : isDisplayText
              ? Helper.GetPlaceholderDisplayText()
              : null;
        var baseEditablePlaceholder = isDisplayText ? Helper.GetPlaceholderDisplayTextBase() : null;
        var metadata = app.GetMetadata();

        for (var i = 0; i < metadata.length; i++) {
            var optionSet = metadata[i];

            var parent = {
                recid: optionSet.MetadataId,
                schemaName: optionSet.Name,
                w2ui: { editable: isDescription, children: [] }
            };

            if (isDescription) {
                Helper.ApplyPlaceholder(parent, editablePlaceholder, baseEditablePlaceholder, app);
                if (optionSet.Description) {
                    Helper.AddLocalizedLabelsToRecord(
                        parent,
                        Helper.GetComponentLocalizedLabels(optionSet, Helper.ComponentTypes.Description)
                    );
                }
            } else {
                parent._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();
            }

            var options;
            if (optionSet.TrueOption) {
                options = [optionSet.TrueOption, optionSet.FalseOption];
            } else {
                options = optionSet.Options;
                if (!options || options.length === 0) {
                    if (!isDescription) {
                        continue;
                    }
                }
            }

            for (var j = 0; options && j < options.length; j++) {
                AddOptionChild(
                    parent,
                    optionSet,
                    options[j],
                    component,
                    editablePlaceholder,
                    baseEditablePlaceholder,
                    app
                );
            }

            records.push(parent);
        }

        Helper.FinalizeGrid(records, app);
    }

    GlobalOptionSetHandler.Load = function () {
        var app = Helper.GetTranslator();
        app.LockGrid(Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: function () {
                return {
                    solutionId: app.GetSolution()
                };
            },
            onLoaded: function (output) {
                var optionSets = output.optionSets || [];
                optionSets = optionSets.filter(function (os) {
                    return os && os.IsCustomizable && os.IsCustomizable.Value && os.IsGlobal;
                });

                optionSets.sort(function (a, b) {
                    return (a.Name || "").localeCompare(b.Name || "");
                });

                app.SetMetadata(optionSets);
                FillTable();
            }
        });
    };

    function GetOptionSetNames(result) {
        return result && result.optionSetNames ? result.optionSetNames : [];
    }

    function GetOptionSetNamePayload(output) {
        var optionSetNames = GetOptionSetNames(output);
        return optionSetNames.length > 0 ? { optionSetNames: optionSetNames } : null;
    }

    GlobalOptionSetHandler.Save = function () {
        var app = Helper.GetTranslator();

        return Helper.RunServerSaveFlow({
            app: app,
            actionName: actionName,
            getSavePayload: function () {
                var updates = GetUpdates();
                return {
                    component: GetComponent(app),
                    optionValueUpdates: updates.optionValueUpdates,
                    optionSetDescriptionUpdates: updates.optionSetDescriptionUpdates
                };
            },
            getPublishPayload: GetOptionSetNamePayload,
            getPublishedPayload: GetOptionSetNamePayload,
            shouldReload: function (output) {
                return GetOptionSetNames(output).length > 0;
            },
            reloadAction: function () {
                return GlobalOptionSetHandler.Load();
            }
        });
    };
})((window.GlobalOptionSetHandler = window.GlobalOptionSetHandler || {}));
