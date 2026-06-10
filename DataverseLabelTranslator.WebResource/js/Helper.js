(function (Helper, undefined) {
    "use strict";

    var otherActionName = "Other";
    var baseLanguage = null;

    function GetXrm() {
        if (typeof Xrm !== "undefined") {
            return Xrm;
        }

        if (window.parent && window.parent.Xrm) {
            return window.parent.Xrm;
        }

        throw new Error("Xrm is not available in the current context.");
    }

    Helper.CustomActionTypes = {
        Loading: "Loading",
        Saving: "Saving",
        Publishing: "Publishing",
        Published: "Published",
        Other: "Other"
    };

    Helper.ComponentTypes = {
        DisplayText: "DisplayText",
        Description: "Description"
    };

    Helper.UiText = {
        Placeholders: {
            DisplayText: "Add-display-text",
            DisplayTextBase: "Add-display-text(*)",
            Description: "Add-description",
            Readonly: "-"
        },
        Operations: {
            Loading: "Loading ....",
            Saving: "Saving ...",
            Publishing: "Publishing ...",
            Published: "Published",
            ReLoading: "Re-Loading ..."
        }
    };

    Helper.IsEmptyLabelValue = function (value) {
        return value == null || String(value).trim().length === 0;
    };

    Helper.GetPlaceholderDisplayText = function () {
        return Helper.UiText.Placeholders.DisplayText;
    };

    Helper.GetPlaceholderDisplayTextBase = function () {
        return Helper.UiText.Placeholders.DisplayTextBase;
    };

    Helper.GetPlaceholderDescription = function () {
        return Helper.UiText.Placeholders.Description;
    };

    Helper.GetPlaceholderReadonly = function () {
        return Helper.UiText.Placeholders.Readonly;
    };

    Helper.GetOperationLoading = function () {
        return Helper.UiText.Operations.Loading;
    };

    Helper.GetOperationSaving = function () {
        return Helper.UiText.Operations.Saving;
    };

    Helper.GetOperationPublishing = function () {
        return Helper.UiText.Operations.Publishing;
    };

    Helper.GetOperationPublished = function () {
        return Helper.UiText.Operations.Published;
    };

    Helper.GetOperationReLoading = function () {
        return Helper.UiText.Operations.ReLoading;
    };

    function ExecuteOther(operation, payload) {
        var input = Object.assign({ operation: operation }, payload || {});

        return Helper.ExecuteTypedCustomAction(otherActionName, Helper.CustomActionTypes.Other, input).then(
            function (result) {
                return Helper.GetCustomActionObject(result);
            }
        );
    }

    Helper.GetSolutions = function () {
        return ExecuteOther("GetSolutions").then(function (output) {
            return (output && output.solutions) || [];
        });
    };

    Helper.GetEntities = function (solutionId) {
        return ExecuteOther("GetEntities", { solutionId: solutionId || "all" }).then(function (output) {
            return (output && output.entities) || [];
        });
    };

    Helper.GetBaseLanguage = function () {
        if (baseLanguage) {
            return Promise.resolve(baseLanguage);
        }

        return ExecuteOther("GetBaseLanguage").then(function (output) {
            baseLanguage = output && output.languageCode;
            return baseLanguage;
        });
    };

    Helper.GetAllNoneBaseLanguageCodes = function () {
        return ExecuteOther("GetAllNoneBaseLanguageCodes").then(function (output) {
            return {
                LocaleIds: (output && output.LocaleIds) || []
            };
        });
    };

    Helper.GetTranslator = function () {
        if (window.DataverseLabelTranslator) {
            return window.DataverseLabelTranslator;
        }

        throw new Error("Translator is not available.");
    };

    function GetBaseLanguage(app) {
        app = app || Helper.GetTranslator();

        if (app && app.baseLanguage) {
            return app.baseLanguage;
        }

        if (typeof app.GetBaseLanguage === "function") {
            var baseLanguage = app.GetBaseLanguage();
            return baseLanguage && typeof baseLanguage.then !== "function" ? baseLanguage : null;
        }

        return null;
    }

    Helper.GetLanguageColumnText = function (languageCode, grid) {
        grid = grid || Helper.GetTranslator().GetGrid();
        var columns = grid.columns || [];
        var field = String(languageCode);

        for (var i = 0; i < columns.length; i++) {
            if (String(columns[i].field) === field) {
                return columns[i].text || columns[i].caption || columns[i].label || field;
            }
        }

        return field;
    };

    Helper.ApplyPlaceholder = function (record, editablePlaceholder, basePlaceholder, app) {
        if (!editablePlaceholder) {
            return;
        }

        record._emptyEditablePlaceholder = editablePlaceholder;

        var baseLanguage = GetBaseLanguage(app);
        if (basePlaceholder && baseLanguage) {
            record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
            record._emptyEditablePlaceholders[String(baseLanguage)] = basePlaceholder;
        }
    };

    Helper.AddLocalizedLabelsToRecord = function (record, localizedLabels) {
        localizedLabels = localizedLabels || [];

        for (var i = 0; i < localizedLabels.length; i++) {
            record[localizedLabels[i].LanguageCode.toString()] = localizedLabels[i].Label;
        }
    };

    Helper.GetComponentLocalizedLabels = function (metadata, component) {
        var metadataProperty = component === Helper.ComponentTypes.Description ? "Description" : "Label";
        var localizedLabelContainer = metadata && metadata[metadataProperty] ? metadata[metadataProperty] : {};

        return localizedLabelContainer.LocalizedLabels || [];
    };

    function GetRecordValue(record, field) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, field)) {
            return record.w2ui.changes[field];
        }

        if (Object.prototype.hasOwnProperty.call(record, field)) {
            return record[field];
        }

        var stringField = String(field);
        if (
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, stringField)
        ) {
            return record.w2ui.changes[stringField];
        }

        return Object.prototype.hasOwnProperty.call(record, stringField) ? record[stringField] : "";
    }

    function AddBaseDisplayTextLabel(labels, options) {
        options = options || {};

        if (!options.includeBaseDisplayText || labels.length === 0) {
            return labels;
        }

        var app = options.app || Helper.GetTranslator();
        if (!app.IsDisplayTextComponent || !app.IsDisplayTextComponent()) {
            return labels;
        }

        var baseLanguage = GetBaseLanguage(app);
        if (!baseLanguage) {
            return labels;
        }

        var baseField = String(baseLanguage);
        for (var i = 0; i < labels.length; i++) {
            if (String(labels[i].LanguageCode) === baseField) {
                return labels;
            }
        }

        var baseValue = GetRecordValue(options.record, baseField);
        if (Helper.IsEmptyLabelValue(baseValue)) {
            return labels;
        }

        labels.push({ LanguageCode: baseField, Label: baseValue });
        return labels;
    }

    Helper.GetChangedLabels = function (changes, allowEmpty, options) {
        var labels = [];

        for (var change in changes) {
            if (!Object.prototype.hasOwnProperty.call(changes, change)) {
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

        return AddBaseDisplayTextLabel(labels, options);
    };

    Helper.ValidateBaseLanguageNotEmpty = function (record, changes, options) {
        options = options || {};
        var app = options.app || Helper.GetTranslator();

        if (!app.IsDisplayTextComponent || !app.IsDisplayTextComponent()) {
            return;
        }

        var baseLanguage = GetBaseLanguage(app);
        if (!baseLanguage) {
            return;
        }

        baseLanguage = String(baseLanguage);
        if (!Object.prototype.hasOwnProperty.call(changes, baseLanguage)) {
            return;
        }

        if (!Helper.IsEmptyLabelValue(changes[baseLanguage])) {
            return;
        }

        var rowPath =
            typeof options.getRowPath === "function" ? options.getRowPath(record) : record && record.schemaName;

        throw new Error(
            "Display Text in the base language (" +
                Helper.GetLanguageColumnText(baseLanguage) +
                ") cannot be empty.\n" +
                "Row: " +
                (rowPath || "(unknown)")
        );
    };

    Helper.FinalizeGrid = function (records, app) {
        app = app || Helper.GetTranslator();
        var grid = app.GetGrid();

        app.AddSummary(records);
        grid.add(records);

        if (typeof app.UnlockGrid === "function") {
            app.UnlockGrid();
        } else {
            grid.unlock();
        }
    };

    Helper.GetCustomActionObject = function (result) {
        return result && result.object ? result.object : {};
    };

    Helper.ShowError = function (message, options) {
        options = options || {};
        var text = message && message.message ? message.message : String(message || "");
        var title = options.title || "Error";

        if (window.DialogHelper && typeof DialogHelper.alert === "function") {
            return DialogHelper.alert(text, { title: title });
        }

        if (window.w2alert) {
            return w2alert(text, title);
        }

        window.alert(text);
        return null;
    };

    Helper.ExecuteCustomAction = function (functionName, input) {
        var request = {
            f: functionName,
            input: input == null ? null : JSON.stringify(input),
            getMetadata: function () {
                return {
                    boundParameter: null,
                    parameterTypes: {
                        f: { typeName: "Edm.String", structuralProperty: 1 },
                        input: { typeName: "Edm.String", structuralProperty: 1 }
                    },
                    operationType: 0,
                    operationName: "pl_DataverseLabelTranslatorCustomAction"
                };
            }
        };

        return GetXrm()
            .WebApi.online.execute(request)
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Custom action failed: " + functionName);
                }

                return response.json();
            })
            .then(function (json) {
                var output = json && json.output ? JSON.parse(json.output) : null;
                if (!output) {
                    throw new Error("Custom action returned empty output: " + functionName);
                }

                if (output.ok === false) {
                    throw new Error(output.message || "Custom action failed: " + functionName);
                }

                return output;
            });
    };

    Helper.ExecuteTypedCustomAction = function (functionName, type, input) {
        var payload = Object.assign({}, input || {});
        payload.type = type;

        return Helper.ExecuteCustomAction(functionName, payload).then(function (result) {
            if (!result || result.type !== type) {
                throw new Error(
                    functionName + " returned unexpected type: " + (result && result.type ? result.type : "(empty)")
                );
            }

            return result;
        });
    };

    Helper.RunServerLoad = function (options) {
        options = options || {};
        var app = options.app || Helper.GetTranslator();
        var payload = typeof options.getPayload === "function" ? options.getPayload() : options.payload || {};

        return Helper.ExecuteTypedCustomAction(options.actionName, Helper.CustomActionTypes.Loading, payload)
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);

                if (typeof options.onLoaded === "function") {
                    options.onLoaded(output, result);
                }

                return result;
            })
            .catch(function (error) {
                if (typeof app.UnlockGrid === "function") {
                    app.UnlockGrid();
                }

                if (options.handleError !== false && typeof app.errorHandler === "function") {
                    app.errorHandler(error);
                    return null;
                }

                throw error;
            });
    };

    Helper.RunServerSaveFlow = function (options) {
        options = options || {};
        var app = options.app || Helper.GetTranslator();
        var savePayload =
            typeof options.getSavePayload === "function" ? options.getSavePayload() : options.payload || {};
        var savingMessage = options.savingMessage || Helper.GetOperationSaving();
        var publishingMessage = options.publishingMessage || Helper.GetOperationPublishing();
        var publishedMessage = options.publishedMessage || Helper.GetOperationPublished();
        var reloadingMessage = options.reloadingMessage || Helper.GetOperationReLoading();

        app.LockGrid(savingMessage);

        return Helper.ExecuteTypedCustomAction(options.actionName, Helper.CustomActionTypes.Saving, savePayload)
            .then(function (saveResult) {
                var saveOutput = Helper.GetCustomActionObject(saveResult);
                if (typeof options.afterSave === "function" && options.afterSave(saveOutput, saveResult) === true) {
                    return {
                        skipPostSaveFlow: true,
                        result: saveResult
                    };
                }

                var publishPayload =
                    typeof options.getPublishPayload === "function"
                        ? options.getPublishPayload(saveOutput, saveResult)
                        : null;

                if (!publishPayload) {
                    app.UnlockGrid();
                    return saveResult;
                }

                app.LockGrid(publishingMessage);
                return Helper.ExecuteTypedCustomAction(
                    options.actionName,
                    Helper.CustomActionTypes.Publishing,
                    publishPayload
                ).then(function (publishingResult) {
                    var publishingOutput = Helper.GetCustomActionObject(publishingResult);
                    var publishedPayload =
                        typeof options.getPublishedPayload === "function"
                            ? options.getPublishedPayload(publishingOutput, publishingResult, saveOutput)
                            : publishPayload;

                    if (!publishedPayload) {
                        app.UnlockGrid();
                        return publishingResult;
                    }

                    app.LockGrid(publishedMessage);
                    return Helper.ExecuteTypedCustomAction(
                        options.actionName,
                        Helper.CustomActionTypes.Published,
                        publishedPayload
                    );
                });
            })
            .then(function (result) {
                if (result && result.skipPostSaveFlow) {
                    return result.result;
                }

                var output = Helper.GetCustomActionObject(result);
                var shouldReload =
                    typeof options.shouldReload === "function" ? options.shouldReload(output, result) : false;

                if (!shouldReload) {
                    app.UnlockGrid();
                    return result;
                }

                app.LockGrid(reloadingMessage);

                if (typeof options.reloadAction !== "function") {
                    app.UnlockGrid();
                    return result;
                }

                return options.reloadAction(result, output).then(function () {
                    app.UnlockGrid();
                    return result;
                });
            })
            .catch(function (error) {
                if (typeof app.UnlockGrid === "function") {
                    app.UnlockGrid();
                }

                throw error;
            });
    };
})((window.Helper = window.Helper || {}));
