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
})((window.Helper = window.Helper || {}));
