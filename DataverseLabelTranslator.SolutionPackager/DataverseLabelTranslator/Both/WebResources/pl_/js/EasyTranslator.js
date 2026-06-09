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

    EasyTranslator.GetCurrentToolbarTypeText = function () {
        return CallLegacy("GetCurrentToolbarTypeText");
    };

    EasyTranslator.IsDescriptionComponent = function () {
        return EasyTranslator.GetComponent() === "Description";
    };

    EasyTranslator.IsDisplayTextComponent = function () {
        return EasyTranslator.GetComponent() === "DisplayText";
    };

    EasyTranslator.GetCurrentComponentText = function () {
        var component = EasyTranslator.GetComponent();

        if (component === "DisplayText") {
            return "Display Text";
        }

        if (component === "Description") {
            return "Description";
        }

        return component || "";
    };

    EasyTranslator.GetAllRecords = function () {
        return CallLegacy("GetAllRecords");
    };

    EasyTranslator.GetColumns = function (includeSchemaName) {
        return CallLegacy("GetColumns", [includeSchemaName]);
    };

    EasyTranslator.GetSelectedRecordIds = function () {
        var grid = EasyTranslator.GetGrid();

        return grid && typeof grid.getSelection === "function" ? grid.getSelection() : [];
    };

    EasyTranslator.GetAttributeById = function (id) {
        return CallLegacy("GetAttributeById", [id]);
    };

    EasyTranslator.GetByRecId = function (records, recid) {
        return CallLegacy("GetByRecId", [records, recid]);
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

    EasyTranslator.ApplyGridChangeValue = function (record, field, value) {
        return CallLegacy("ApplyGridChangeValue", [record, field, value]);
    };

    EasyTranslator.RefreshGridRow = function (recid) {
        var grid = EasyTranslator.GetGrid();
        if (grid && typeof grid.refreshRow === "function") {
            grid.refreshRow(recid);
        }
    };

    EasyTranslator.RefreshGrid = function () {
        var grid = EasyTranslator.GetGrid();
        if (grid && typeof grid.refresh === "function") {
            grid.refresh();
        }
    };

    EasyTranslator.SetSaveButtonDisabled = function (disabled) {
        return CallLegacy("SetSaveButtonDisabled", [disabled]);
    };

    EasyTranslator.HasPendingChanges = function () {
        return CallLegacy("HasPendingChanges");
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

    function ShowLegacyAiTranslate() {
        if (window.TranslationHandler && typeof TranslationHandler.ShowTranslationPrompt === "function") {
            return TranslationHandler.ShowTranslationPrompt();
        }

        if (window.DialogHelper && typeof DialogHelper.alert === "function") {
            return DialogHelper.alert("AI Translate is not available.", { title: "AI Translate" });
        }

        return w2alert("AI Translate is not available.", "AI Translate");
    }

    function GetColumnText(column) {
        return column ? column.text || column.caption || column.label || String(column.field || "") : "";
    }

    function GetLanguageColumns() {
        var grid = EasyTranslator.GetGrid();
        var columns = grid && grid.columns ? grid.columns : [];
        var languages = [];

        for (var i = 0; i < columns.length; i++) {
            var field = String(columns[i].field || "");
            if (/^\d+$/.test(field)) {
                languages.push({
                    lcid: field,
                    text: GetColumnText(columns[i])
                });
            }
        }

        return languages;
    }

    function GetCurrentCellValue(record, lcid) {
        var field = String(lcid);

        if (
            record &&
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, field)
        ) {
            return record.w2ui.changes[field];
        }

        if (record && Object.prototype.hasOwnProperty.call(record, field)) {
            return record[field];
        }

        return "";
    }

    function IsSummaryRecord(record) {
        return !!(record && record.w2ui && record.w2ui.summary);
    }

    function HasChildRecords(record) {
        return !!(record && record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0);
    }

    function GetRootGridRecords() {
        var grid = EasyTranslator.GetGrid();
        var records = grid && grid.records ? grid.records : [];

        return records.filter(function (record) {
            return !IsSummaryRecord(record) && (!record.w2ui || !record.w2ui.parent_recid);
        });
    }

    function IsGenericAiTranslatableRecord(record) {
        if (!record || IsSummaryRecord(record)) {
            return false;
        }

        if (!record.gridKey || record.isEditable !== true || record.isTranslatable !== true) {
            return false;
        }

        if (record.w2ui && record.w2ui.editable === false) {
            return false;
        }

        if (record.ai && record.ai.include === false) {
            return false;
        }

        return true;
    }

    function CreateAiTranslateRow(record, languages, translatable) {
        if (!record || IsSummaryRecord(record)) {
            return null;
        }

        var row = {
            recid: "easy_ai_" + record.recid,
            targetRecid: translatable === false ? null : record.recid,
            schemaName: String(record.schemaName || "").trim(),
            w2ui: {}
        };

        for (var i = 0; i < languages.length; i++) {
            row[languages[i].lcid] = GetCurrentCellValue(record, languages[i].lcid);
        }

        if (record.ai && record.ai.location) {
            row.location = record.ai.location;
        }

        return row;
    }

    function BuildGenericAiTranslateTree(record, languages) {
        var childRows = [];
        var children = HasChildRecords(record) ? record.w2ui.children : [];

        for (var i = 0; i < children.length; i++) {
            var child = BuildGenericAiTranslateTree(children[i], languages);
            if (child) {
                childRows.push(child);
            }
        }

        var translatable = IsGenericAiTranslatableRecord(record);
        if (!translatable && childRows.length === 0) {
            return null;
        }

        var row = CreateAiTranslateRow(record, languages, translatable);
        if (!row) {
            return null;
        }

        if (childRows.length > 0) {
            row.w2ui.children = [];
            for (var c = 0; c < childRows.length; c++) {
                childRows[c].w2ui.parent_recid = row.recid;
                row.w2ui.children.push(childRows[c]);
            }
        }

        return row;
    }

    function BuildAiTranslateRows(languages) {
        var rootRecords = GetRootGridRecords();
        var rows = [];

        for (var i = 0; i < rootRecords.length; i++) {
            var row = BuildGenericAiTranslateTree(rootRecords[i], languages);
            if (row) {
                rows.push(row);
            }
        }

        return rows;
    }

    function HasTranslatableRows(rows) {
        rows = rows || [];

        for (var i = 0; i < rows.length; i++) {
            if (rows[i].targetRecid) {
                return true;
            }

            if (rows[i].w2ui && Array.isArray(rows[i].w2ui.children) && HasTranslatableRows(rows[i].w2ui.children)) {
                return true;
            }
        }

        return false;
    }

    function ApplyAiTranslateChanges(changes) {
        var mainRecords = EasyTranslator.GetAllRecords();
        var changed = false;
        var refreshed = {};

        for (var i = 0; i < changes.length; i++) {
            var change = changes[i];
            var mainRecord = EasyTranslator.GetByRecId(mainRecords, change.targetRecid);
            if (!mainRecord) {
                continue;
            }

            if (EasyTranslator.ApplyGridChangeValue(mainRecord, change.field, change.value)) {
                changed = true;
                refreshed[mainRecord.recid] = true;
            }
        }

        for (var recid in refreshed) {
            if (Object.prototype.hasOwnProperty.call(refreshed, recid)) {
                EasyTranslator.RefreshGridRow(recid);
            }
        }

        if (changed) {
            EasyTranslator.SetSaveButtonDisabled(!EasyTranslator.HasPendingChanges());
            EasyTranslator.RefreshGrid();
        }

        return changed;
    }

    EasyTranslator.BuildAiTranslateDataSource = function () {
        var typeName = EasyTranslator.GetCurrentToolbarTypeText();
        var languages = GetLanguageColumns();
        var baseLcid = EasyTranslator.GetBaseLanguage() ? String(EasyTranslator.GetBaseLanguage()) : "";

        return {
            typeName: typeName,
            componentText: EasyTranslator.GetCurrentComponentText(),
            languages: languages,
            baseLcid: baseLcid || (languages.length ? languages[0].lcid : ""),
            rows: BuildAiTranslateRows(languages),
            applyChanges: ApplyAiTranslateChanges
        };
    };

    EasyTranslator.ShowAITranslate = function () {
        if (typeof EasyTranslator.OpenAiTranslateWorkspace !== "function") {
            return ShowLegacyAiTranslate();
        }

        var dataSource = EasyTranslator.BuildAiTranslateDataSource();
        if (!HasTranslatableRows(dataSource.rows)) {
            return ShowLegacyAiTranslate();
        }

        return EasyTranslator.OpenAiTranslateWorkspace(dataSource);
    };

    EasyTranslator.AiTranslateDataSource = {
        HasTranslatableRows: HasTranslatableRows
    };
})((window.EasyTranslator = window.EasyTranslator || {}));
