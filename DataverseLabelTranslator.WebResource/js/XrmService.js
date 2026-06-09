(function (XrmService, undefined) {
    "use strict";

    var actionName = "Other";
    var baseLanguage = null;

    function executeOther(operation, payload) {
        var input = Object.assign({ operation: operation }, payload || {});

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Other, input).then(
            function (result) {
                return Helper.GetCustomActionObject(result);
            }
        );
    }

    XrmService.GetSolutions = function () {
        return executeOther("GetSolutions").then(function (output) {
            return (output && output.solutions) || [];
        });
    };

    XrmService.GetEntities = function (solutionId) {
        return executeOther("GetEntities", { solutionId: solutionId || "all" }).then(function (output) {
            return (output && output.entities) || [];
        });
    };

    XrmService.GetBaseLanguage = function () {
        if (baseLanguage) {
            return Promise.resolve(baseLanguage);
        }

        return executeOther("GetBaseLanguage").then(function (output) {
            baseLanguage = output && output.languageCode;
            return baseLanguage;
        });
    };

    XrmService.GetAllNoneBaseLanguageCodes = function () {
        return executeOther("GetAllNoneBaseLanguageCodes").then(function (output) {
            return {
                LocaleIds: (output && output.LocaleIds) || []
            };
        });
    };
})((window.XrmService = window.XrmService || {}));
