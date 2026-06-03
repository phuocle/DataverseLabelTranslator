(function (EasyTranslator, undefined) {
    "use strict";

    EasyTranslator.metadata = EasyTranslator.metadata || [];
    EasyTranslator.baseLanguage = EasyTranslator.baseLanguage || null;

    function GetLegacyTranslator() {
        if (window.XrmTranslator) {
            return window.XrmTranslator;
        }

        throw new Error("XrmTranslator is not available.");
    }

    function CallLegacy(methodName, args) {
        var translator = GetLegacyTranslator();

        if (typeof translator[methodName] !== "function") {
            throw new Error("XrmTranslator." + methodName + " is not available.");
        }

        return translator[methodName].apply(translator, args || []);
    }

    EasyTranslator.GetGrid = function () {
        return CallLegacy("GetGrid");
    };

    EasyTranslator.GetSolution = function () {
        return CallLegacy("GetSolution");
    };

    EasyTranslator.GetEntity = function () {
        return CallLegacy("GetEntity");
    };

    EasyTranslator.GetEntityId = function () {
        return CallLegacy("GetEntityId");
    };

    EasyTranslator.GetType = function () {
        return CallLegacy("GetType");
    };

    EasyTranslator.GetComponent = function () {
        return CallLegacy("GetComponent");
    };

    EasyTranslator.IsDescriptionComponent = function () {
        return EasyTranslator.GetComponent() === "Description";
    };

    EasyTranslator.IsDisplayTextComponent = function () {
        return EasyTranslator.GetComponent() === "DisplayText";
    };

    EasyTranslator.GetAllRecords = function () {
        return CallLegacy("GetAllRecords");
    };

    EasyTranslator.GetAttributeById = function (id) {
        return CallLegacy("GetAttributeById", [id]);
    };

    EasyTranslator.AddSummary = function (records) {
        return CallLegacy("AddSummary", [records]);
    };

    EasyTranslator.LockGrid = function (message) {
        return CallLegacy("LockGrid", [message]);
    };

    EasyTranslator.UnlockGrid = function () {
        return CallLegacy("UnlockGrid");
    };

    EasyTranslator.errorHandler = function (error) {
        return CallLegacy("errorHandler", [error]);
    };

    EasyTranslator.SetMetadata = function (metadata) {
        EasyTranslator.metadata = metadata || [];

        if (window.XrmTranslator) {
            window.XrmTranslator.metadata = EasyTranslator.metadata;
        }

        return EasyTranslator.metadata;
    };

    EasyTranslator.GetMetadata = function () {
        if (window.XrmTranslator && window.XrmTranslator.metadata) {
            EasyTranslator.metadata = window.XrmTranslator.metadata;
        }

        return EasyTranslator.metadata || [];
    };

    EasyTranslator.GetBaseLanguage = function () {
        if (EasyTranslator.baseLanguage) {
            return EasyTranslator.baseLanguage;
        }

        if (window.XrmTranslator && window.XrmTranslator.baseLanguage) {
            EasyTranslator.baseLanguage = window.XrmTranslator.baseLanguage;
        }

        return EasyTranslator.baseLanguage || null;
    };

    EasyTranslator.SetBaseLanguage = function (languageCode) {
        EasyTranslator.baseLanguage = languageCode;

        if (window.XrmTranslator) {
            window.XrmTranslator.baseLanguage = languageCode;
        }

        return EasyTranslator.baseLanguage;
    };
})((window.EasyTranslator = window.EasyTranslator || {}));
