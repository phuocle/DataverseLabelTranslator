(function (Helper, undefined) {
    "use strict";

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

    Helper.IsEmptyLabelValue = function (value) {
        return value == null || String(value).trim().length === 0;
    };

    Helper.GetLanguageColumnText = function (languageCode, grid) {
        grid = grid || XrmTranslator.GetGrid();
        var columns = grid.columns || [];
        var field = String(languageCode);

        for (var i = 0; i < columns.length; i++) {
            if (String(columns[i].field) === field) {
                return columns[i].text || columns[i].caption || columns[i].label || field;
            }
        }

        return field;
    };

    Helper.GetCustomActionObject = function (result) {
        return result && result.object ? result.object : {};
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
})((window.Helper = window.Helper || {}));
