(function (DataverseLabelTranslatorHandler, undefined) {
    "use strict";

    var actionName = "EasyTranslator";

    function GetApp() {
        return Helper.GetTranslator();
    }

    function IsLanguageField(field) {
        return /^\d+$/.test(String(field || ""));
    }

    function GetEditablePlaceholder(app) {
        if (app.IsDescriptionComponent()) {
            return Helper.GetPlaceholderDescription();
        }

        if (app.IsDisplayTextComponent()) {
            return Helper.GetPlaceholderDisplayText();
        }

        return null;
    }

    function GetBaseEditablePlaceholder(app) {
        return app.IsDisplayTextComponent() ? Helper.GetPlaceholderDisplayTextBase() : null;
    }

    function IsSummaryRecord(record) {
        return !!(record && record.w2ui && record.w2ui.summary);
    }

    function IsReadonlyRecord(record) {
        return !!(record && (record.isEditable === false || (record.w2ui && record.w2ui.editable === false)));
    }

    function NormalizeSaveValue(value) {
        if (value == null) {
            return "";
        }

        var text = String(value);
        var placeholders = [
            Helper.GetPlaceholderDisplayText(),
            Helper.GetPlaceholderDisplayTextBase(),
            Helper.GetPlaceholderDescription(),
            Helper.GetPlaceholderReadonly()
        ];

        for (var i = 0; i < placeholders.length; i++) {
            if (text === placeholders[i]) {
                return "";
            }
        }

        return text;
    }

    function GetPublishTargets(output) {
        return output && Array.isArray(output.publishTargets) ? output.publishTargets : [];
    }

    function HasPublishTargets(output) {
        return GetPublishTargets(output).length > 0;
    }

    function GetPublishPayload(output) {
        var publishTargets = GetPublishTargets(output);
        if (publishTargets.length === 0) {
            return null;
        }

        return {
            translatorType: GetApp().GetType(),
            publishTargets: publishTargets
        };
    }

    function BuildOperationPayload() {
        var app = GetApp();

        return {
            translatorType: app.GetType(),
            solutionId: app.GetSolution(),
            entityName: app.GetEntity(),
            entityId: app.GetEntityId(),
            component: app.GetComponent()
        };
    }

    function FindFormRecord(record) {
        var current = record;
        var app = GetApp();

        while (current) {
            if (current.gridKey && /^forms\|/i.test(current.gridKey)) {
                return current;
            }

            if (!current.w2ui || !current.w2ui.parent_recid) {
                return null;
            }

            current = app.GetByRecId(app.GetAllRecords(), current.w2ui.parent_recid);
        }

        return null;
    }

    function GetSelectedFormId() {
        var app = GetApp();
        var grid = app.GetGrid();
        var selection = typeof grid.getSelection === "function" ? grid.getSelection() : [];

        if (!selection || selection.length === 0) {
            return null;
        }

        var record = app.GetByRecId(app.GetAllRecords(), selection[0]);
        var formRecord = FindFormRecord(record);
        if (!formRecord || !formRecord.gridKey) {
            return null;
        }

        var parts = formRecord.gridKey.split("|");
        return parts.length > 1 ? decodeURIComponent(parts[1]) : null;
    }

    function CopyServerRowFields(source, target) {
        for (var key in source) {
            if (!Object.prototype.hasOwnProperty.call(source, key) || key === "children") {
                continue;
            }

            target[key] = source[key];
        }
    }

    function BuildGridRow(serverRow, app) {
        var record = {};
        var editable = serverRow && serverRow.isEditable !== false;
        var children = serverRow && Array.isArray(serverRow.children) ? serverRow.children : [];

        CopyServerRowFields(serverRow || {}, record);

        record.recid = record.recid || record.gridKey || record.schemaName;
        record.schemaName = record.schemaName || record.recid;
        record.w2ui = record.w2ui || {};
        record.w2ui.editable = editable;
        record._isGroupNode = record.isTranslatable === false || record.ai?.include === false;

        if (editable) {
            Helper.ApplyPlaceholder(record, GetEditablePlaceholder(app), GetBaseEditablePlaceholder(app), app);
        } else {
            record._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();
        }

        if (children.length > 0) {
            record.w2ui.children = [];
            for (var i = 0; i < children.length; i++) {
                var child = BuildGridRow(children[i], app);
                child.w2ui.parent_recid = record.recid;
                record.w2ui.children.push(child);
            }
        }

        return record;
    }

    function CollectLanguageColumnFields(rows, fields) {
        rows = Array.isArray(rows) ? rows : [];

        for (var i = 0; i < rows.length; i++) {
            var row = rows[i] || {};
            for (var key in row) {
                if (Object.prototype.hasOwnProperty.call(row, key) && IsLanguageField(key)) {
                    fields[String(key)] = true;
                }
            }

            CollectLanguageColumnFields(row.children, fields);
        }
    }

    function GetLanguageColumns(gridOutput) {
        var columns = gridOutput && Array.isArray(gridOutput.languageColumns) ? gridOutput.languageColumns : [];
        if (columns.length > 0) {
            return columns;
        }

        var fields = {};
        var inferredColumns = [];
        CollectLanguageColumnFields(gridOutput && gridOutput.rows, fields);

        Object.keys(fields)
            .sort(function (a, b) {
                return parseInt(a, 10) - parseInt(b, 10);
            })
            .forEach(function (field) {
                inferredColumns.push({
                    field: field,
                    text: field
                });
            });

        return inferredColumns;
    }

    function ApplyLanguageColumns(gridOutput, app) {
        var columns = GetLanguageColumns(gridOutput);
        if (columns.length === 0) {
            return;
        }

        app.ApplyLanguageColumns(columns);
    }

    function FillTable(gridOutput) {
        var app = GetApp();
        var grid = app.GetGrid();
        var records = [];
        var rows = gridOutput && Array.isArray(gridOutput.rows) ? gridOutput.rows : [];

        grid.clear();
        ApplyLanguageColumns(gridOutput, app);

        for (var i = 0; i < rows.length; i++) {
            records.push(BuildGridRow(rows[i], app));
        }

        Helper.FinalizeGrid(records, app);
    }

    function GetRowPath(record) {
        return record && record.schemaName != null ? String(record.schemaName) : "(unknown)";
    }

    function BuildChangedRows() {
        var app = GetApp();
        var records = app.GetAllRecords();
        var changedRows = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (
                !record ||
                IsSummaryRecord(record) ||
                IsReadonlyRecord(record) ||
                !record.gridKey ||
                !record.w2ui ||
                !record.w2ui.changes
            ) {
                continue;
            }

            Helper.ValidateBaseLanguageNotEmpty(record, record.w2ui.changes, {
                app: app,
                getRowPath: GetRowPath
            });

            var changes = {};
            for (var field in record.w2ui.changes) {
                if (!Object.prototype.hasOwnProperty.call(record.w2ui.changes, field) || !IsLanguageField(field)) {
                    continue;
                }

                changes[String(field)] = NormalizeSaveValue(record.w2ui.changes[field]);
            }

            if (Object.keys(changes).length === 0) {
                continue;
            }

            changedRows.push({
                gridKey: record.gridKey,
                recid: record.recid,
                rowType: record.rowType,
                changes: changes
            });
        }

        return changedRows;
    }

    DataverseLabelTranslatorHandler.IsUnifiedType = function (type) {
        return (
            [
                "sitemap",
                "dashboards",
                "webresources",
                "globalOptionSet",
                "attributes",
                "options",
                "forms",
                "entityMeta",
                "views",
                "formMeta",
                "relationships",
                "charts",
                "ribbons",
                "bpf",
                "entityMessages",
                "commands",
                "businessRules",
                "content"
            ].indexOf(type) !== -1
        );
    };

    DataverseLabelTranslatorHandler.Load = function (lockText) {
        var app = GetApp();

        app.LockGrid(lockText || Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: BuildOperationPayload,
            onLoaded: function (output) {
                if (output.baseLanguage) {
                    app.SetBaseLanguage(output.baseLanguage);
                }

                app.SetMetadata(output.grid || {});
                FillTable(output.grid || {});
            }
        });
    };

    DataverseLabelTranslatorHandler.Save = function () {
        var app = GetApp();
        var payload = BuildOperationPayload();
        payload.baseLanguage = app.GetBaseLanguage();
        payload.changedRows = BuildChangedRows();

        if (payload.changedRows.length === 0) {
            return DialogHelper.alert("There are no " + app.GetCurrentToolbarTypeText() + " changes to save.", {
                title: app.GetCurrentToolbarTypeText()
            });
        }

        return Helper.RunServerSaveFlow({
            app: app,
            actionName: actionName,
            getSavePayload: function () {
                return payload;
            },
            getPublishPayload: GetPublishPayload,
            getPublishedPayload: GetPublishPayload,
            afterSave: function (output) {
                if (!output || app.GetType() !== "ribbons" || !output.import || !output.import.importJobId) {
                    return false;
                }

                var toolbarType = app.GetTypeStateLabel("ribbons");
                app.StartOperationStatus({
                    phase: "importing",
                    tone: "info",
                    icon: "...",
                    message: "Importing " + toolbarType,
                    type: "ribbons",
                    toolbarType: toolbarType,
                    entityLogicalName: app.GetEntity(),
                    importJobId: output.import.importJobId,
                    operationId: output.import.operationId || output.import.importJobId,
                    blockSave: true,
                    blockLoad: true
                });
                app.ApplyStoredOperationStatus();
                app.LockGrid("Importing " + toolbarType);

                return true;
            },
            shouldReload: function (output) {
                return HasPublishTargets(output) || (output && output.changed === true);
            },
            reloadAction: function () {
                return DataverseLabelTranslatorHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };

    DataverseLabelTranslatorHandler.RemoveOverriddenCellLabels = function () {
        var app = GetApp();
        var formId = GetSelectedFormId();

        if (!formId) {
            return DialogHelper.alert("Select a form row before removing overridden labels.", {
                title: "Remove Overridden Labels"
            });
        }

        return DialogHelper.confirm(
            "This will remove ALL overridden attribute labels on the selected form and reset them to the default attribute labels. This action cannot be undone.\n\nDo you want to continue?",
            {
                title: "Remove Overridden Labels",
                width: 620,
                height: 270,
                popupClass: "xqt-remove-overridden-confirm"
            }
        ).then(function (confirmed) {
            if (!confirmed) {
                return null;
            }

            var payload = BuildOperationPayload();
            payload.operation = "RemoveOverriddenCellLabels";
            payload.formId = formId;

            return Helper.RunServerSaveFlow({
                app: app,
                actionName: actionName,
                getSavePayload: function () {
                    return payload;
                },
                getPublishPayload: GetPublishPayload,
                getPublishedPayload: GetPublishPayload,
                shouldReload: function (output) {
                    return HasPublishTargets(output) || (output && output.changed === true);
                },
                reloadAction: function () {
                    return DataverseLabelTranslatorHandler.Load(Helper.GetOperationReLoading());
                }
            });
        });
    };
})((window.DataverseLabelTranslatorHandler = window.DataverseLabelTranslatorHandler || {}));

(function (DataverseLabelTranslator, undefined) {
    "use strict";

    DataverseLabelTranslator.metadata = DataverseLabelTranslator.metadata || [];
    DataverseLabelTranslator.baseLanguage = DataverseLabelTranslator.baseLanguage || null;

    DataverseLabelTranslator.GetSelectedRecordIds = function () {
        var grid = DataverseLabelTranslator.GetGrid();

        return grid && typeof grid.getSelection === "function" ? grid.getSelection() : [];
    };

    DataverseLabelTranslator.RefreshGridRow = function (recid) {
        var grid = DataverseLabelTranslator.GetGrid();
        if (grid && typeof grid.refreshRow === "function") {
            grid.refreshRow(recid);
        }
    };

    DataverseLabelTranslator.RefreshGrid = function () {
        var grid = DataverseLabelTranslator.GetGrid();
        if (grid && typeof grid.refresh === "function") {
            grid.refresh();
        }
    };

    function ShowAiTranslateUnavailable() {
        if (window.DialogHelper && typeof DialogHelper.alert === "function") {
            return DialogHelper.alert("AI Translate is not available.", { title: "AI Translate" });
        }

        return w2alert("AI Translate is not available.", "AI Translate");
    }

    function GetColumnText(column) {
        return column ? column.text || column.caption || column.label || String(column.field || "") : "";
    }

    function GetLanguageColumns() {
        var grid = DataverseLabelTranslator.GetGrid();
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
        var grid = DataverseLabelTranslator.GetGrid();
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
        var mainRecords = DataverseLabelTranslator.GetAllRecords();
        var changed = false;
        var refreshed = {};

        for (var i = 0; i < changes.length; i++) {
            var change = changes[i];
            var mainRecord = DataverseLabelTranslator.GetByRecId(mainRecords, change.targetRecid);
            if (!mainRecord) {
                continue;
            }

            if (DataverseLabelTranslator.ApplyGridChangeValue(mainRecord, change.field, change.value)) {
                changed = true;
                refreshed[mainRecord.recid] = true;
            }
        }

        for (var recid in refreshed) {
            if (Object.prototype.hasOwnProperty.call(refreshed, recid)) {
                DataverseLabelTranslator.RefreshGridRow(recid);
            }
        }

        if (changed) {
            DataverseLabelTranslator.SetSaveButtonDisabled(!DataverseLabelTranslator.HasPendingChanges());
            DataverseLabelTranslator.RefreshGrid();
        }

        return changed;
    }

    DataverseLabelTranslator.BuildAiTranslateDataSource = function () {
        var typeName = DataverseLabelTranslator.GetCurrentToolbarTypeText();
        var languages = GetLanguageColumns();
        var baseLcid = DataverseLabelTranslator.GetBaseLanguage()
            ? String(DataverseLabelTranslator.GetBaseLanguage())
            : "";

        return {
            typeName: typeName,
            componentText: DataverseLabelTranslator.GetCurrentComponentText(),
            languages: languages,
            baseLcid: baseLcid || (languages.length ? languages[0].lcid : ""),
            rows: BuildAiTranslateRows(languages),
            applyChanges: ApplyAiTranslateChanges
        };
    };

    DataverseLabelTranslator.ShowAITranslate = function () {
        if (!window.AIService || typeof AIService.OpenWorkspace !== "function") {
            return ShowAiTranslateUnavailable();
        }

        var dataSource = DataverseLabelTranslator.BuildAiTranslateDataSource();
        if (!HasTranslatableRows(dataSource.rows)) {
            return ShowAiTranslateUnavailable();
        }

        return AIService.OpenWorkspace(dataSource);
    };

    var currentHandler = null;
    var entityMetadata = {};
    var allEntities = [];
    var ENTITY_DEPENDENT_TYPE_ITEMS = [
        "type:attributes",
        "type:options",
        "type:forms",
        "type:content",
        "type:bpf",
        "type:businessRules",
        "type:ribbons",
        "type:commands",
        "type:entityMessages"
    ];
    var GLOBAL_TYPE_ITEMS = [
        "type:sitemap",
        "type:dashboards",
        "type:webresources",
        "type:globalOptionSet",
        "type:entityMeta",
        "type:views",
        "type:formMeta",
        "type:relationships",
        "type:charts"
    ];

    DataverseLabelTranslator.defaultSchemaNameSize = "20%";
    DataverseLabelTranslator.entityMetadata = entityMetadata;
    DataverseLabelTranslator.allEntities = allEntities;
    DataverseLabelTranslator.columnRestoreNeeded = false;

    function HasOwnProperty(obj, prop) {
        return Object.prototype.hasOwnProperty.call(obj || {}, prop);
    }

    function GetToolbar() {
        return w2ui && w2ui.grid_toolbar ? w2ui.grid_toolbar : null;
    }

    function RefreshToolbar() {
        var toolbar = GetToolbar();
        if (toolbar && typeof toolbar.refresh === "function") {
            toolbar.refresh();
        }
    }

    function NormalizeGridSearchUiSoon() {
        NormalizeGridSearchUi();
        setTimeout(NormalizeGridSearchUi, 0);
        setTimeout(NormalizeGridSearchUi, 50);
        setTimeout(NormalizeGridSearchUi, 150);
        setTimeout(NormalizeGridSearchUi, 300);
    }

    function NormalizeGridSearchUi() {
        var grid = w2ui && w2ui.grid ? w2ui.grid : null;
        var gridBox = grid && grid.box ? grid.box : null;
        if (!grid || !gridBox || !grid.name) {
            return;
        }

        ConfigureSimpleGridSearch(grid);
        RemoveGridSearchPanel(gridBox);
        EnsureSimpleGridSearchStyle(grid);

        if (grid.searchSelected) {
            grid.searchSelected = null;
            if (typeof grid.refreshSearch === "function") {
                grid.refreshSearch();
            }
        }

        if (grid.last) {
            grid.last.field = "all";
            grid.last.label = "All Fields";
        }

        var searchName = gridBox.querySelector("#grid_" + grid.name + "_search_name");
        var searchInput = gridBox.querySelector("#grid_" + grid.name + "_search_all");
        var nameText = searchName ? searchName.querySelector(".name-text") : null;

        if (searchName) {
            searchName.style.display = "none";
        }

        if (nameText) {
            nameText.textContent = "";
        }

        if (searchInput) {
            searchInput.placeholder = "";
            searchInput.removeAttribute("placeholder");

            var searchValueText = String(searchInput.value || "")
                .trim()
                .toLowerCase();
            if (searchValueText === "null" || searchValueText === "undefined") {
                searchInput.value = "";
            }
        }
    }

    function EnsureSimpleGridSearchStyle(grid) {
        var styleId = "easy-translator-simple-grid-search-style";
        if (!grid || document.getElementById(styleId)) {
            return;
        }

        var style = document.createElement("style");
        style.id = styleId;
        style.textContent =
            "#grid_" +
            grid.name +
            "_search_name{display:none!important;}" +
            "#grid_" +
            grid.name +
            "_search_all::placeholder{color:transparent!important;}";
        document.head.appendChild(style);
    }

    function RemoveGridSearchPanel(gridBox) {
        var searchPanels = gridBox.querySelectorAll(".w2ui-grid-searches");
        for (var i = 0; i < searchPanels.length; i++) {
            searchPanels[i].remove();
        }
    }

    function ConfigureSimpleGridSearch(grid) {
        if (!grid || grid._easyTranslatorSimpleSearchConfigured) {
            return;
        }

        if (grid.defaultOperator) {
            grid.defaultOperator.text = "contains";
        }
        if (grid.show) {
            grid.show.searchLogic = false;
            grid.show.searchSave = false;
        }

        grid.searchOpen = function () {};
        grid.searchShowFields = function () {};
        grid.searchSuggest = function () {};
        grid._easyTranslatorSimpleSearchConfigured = true;
    }

    function SetToolbarItemsVisible(ids, visible) {
        var toolbar = GetToolbar();
        if (!toolbar) {
            return;
        }

        for (var i = 0; i < ids.length; i++) {
            if (toolbar.get(ids[i])) {
                if (visible) {
                    toolbar.show(ids[i]);
                } else {
                    toolbar.hide(ids[i]);
                }
            }
        }
    }

    function SetToolbarItemsEnabled(ids, enabled) {
        var toolbar = GetToolbar();
        if (!toolbar) {
            return;
        }

        for (var i = 0; i < ids.length; i++) {
            if (!toolbar.get(ids[i])) {
                continue;
            }

            if (enabled) {
                toolbar.enable(ids[i]);
            } else {
                toolbar.disable(ids[i]);
            }
        }
    }

    function SetSolutionRequiredState(enabled) {
        SetToolbarItemsEnabled(["entitySelect", "type", "component", "load"], enabled);

        if (!enabled) {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, false);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, false);
            GetToolbar().get("entitySelect").selected = "none";
            GetToolbar().get("type").selected = "none";
            GetToolbar().get("component").selected = "DisplayText";
            SetToolbarItemsEnabled(["component", "load"], false);
        }

        DataverseLabelTranslator.SetSaveButtonDisabled(true);
        RefreshToolbar();
    }

    function ApplyTypeVisibilityForEntity(entityTarget) {
        var toolbar = GetToolbar();
        var typeItem = toolbar.get("type");
        var componentItem = toolbar.get("component");

        if (entityTarget === "entitySelect:none" || entityTarget === "none") {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, false);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, true);
        } else {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, true);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, false);
            SetToolbarItemsVisible(
                ["type:content"],
                String(entityTarget || "")
                    .toLowerCase()
                    .indexOf("adx_contentsnippet") !== -1
            );
        }

        typeItem.selected = "none";
        if (componentItem) {
            componentItem.selected = "DisplayText";
        }
        SetToolbarItemsEnabled(["component", "load"], false);
        DataverseLabelTranslator.SetSaveButtonDisabled(true);
        UpdateComponentDropdown(typeItem.selected);
        SetHandler();
        RefreshToolbar();
    }

    function CompactToolbarText(text, maxLength, stripOrderPrefix) {
        text = String(text || "").trim();
        if (stripOrderPrefix) {
            text = text.replace(/^\d+\.\s*/, "");
        }

        maxLength = maxLength || 28;
        return text.length <= maxLength ? text : text.substring(0, maxLength - 1) + "...";
    }

    function GetToolbarDisplayName(text) {
        return String(text || "")
            .replace(/\s+\([^)]+\)\s*$/, "")
            .trim();
    }

    function GetEntityToolbarText(item, toolbar) {
        var el = toolbar.get("entitySelect:" + item.selected);
        if (!el) {
            return "Entity";
        }

        if (item.selected === "none") {
            return "None";
        }

        return CompactToolbarText(GetToolbarDisplayName(el.text), 26);
    }

    function GetTypeStateLabel(type) {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("type:" + type) : null;
        return item ? item.text : type || "";
    }

    function SetHandler() {
        if (w2ui.grid) {
            w2ui.grid.show.selectColumn = false;
        }

        currentHandler = null;
        if (
            window.DataverseLabelTranslatorHandler &&
            DataverseLabelTranslatorHandler.IsUnifiedType(DataverseLabelTranslator.GetType())
        ) {
            currentHandler = DataverseLabelTranslatorHandler;
        }

        var toolbar = GetToolbar();
        if (toolbar && toolbar.get("removeOverriddenAttributeLabels")) {
            if (DataverseLabelTranslator.GetType() === "forms") {
                toolbar.show("removeOverriddenAttributeLabels");
            } else {
                toolbar.hide("removeOverriddenAttributeLabels");
            }
        }
    }

    function IsGlobalType(type) {
        return (
            [
                "sitemap",
                "dashboards",
                "webresources",
                "globalOptionSet",
                "entityMeta",
                "views",
                "formMeta",
                "relationships",
                "charts"
            ].indexOf(type) !== -1
        );
    }

    function UpdateComponentDropdown(selectedType) {
        if (!selectedType || selectedType === "none") {
            SetToolbarItemsEnabled(["component"], false);
            RefreshToolbar();
            return;
        }

        var hasDescription =
            [
                "attributes",
                "options",
                "views",
                "formMeta",
                "entityMeta",
                "globalOptionSet",
                "sitemap",
                "webresources"
            ].indexOf(selectedType) !== -1;
        var toolbar = GetToolbar();
        if (!toolbar) {
            return;
        }

        var componentItem = toolbar.get("component");
        if (componentItem && !hasDescription) {
            componentItem.selected = "DisplayText";
            toolbar.disable("component");
        } else if (componentItem) {
            toolbar.enable("component");
        }

        RefreshToolbar();
    }

    function LoadHandler() {
        if (DataverseLabelTranslator.GetType() === "none") {
            DataverseLabelTranslator.SetLoadButtonDisabled(true);
            DataverseLabelTranslator.SetSaveButtonDisabled(true);
            return;
        }

        SetHandler();

        if (!currentHandler || typeof currentHandler.Load !== "function") {
            DialogHelper.alert("No load handler is available for the selected type.", { title: "Load" });
            return;
        }

        if (!IsGlobalType(DataverseLabelTranslator.GetType()) && DataverseLabelTranslator.GetEntity() === "none") {
            DialogHelper.alert("Select an entity before loading this type.", { title: "Load" });
            return;
        }

        DataverseLabelTranslator.SetSaveButtonDisabled(true);
        DataverseLabelTranslator.GetGrid().sort();
        currentHandler
            .Load()
            .then(function () {
                DataverseLabelTranslator.SetSaveButtonDisabled(!DataverseLabelTranslator.HasLoadedRecords());
            })
            .catch(DataverseLabelTranslator.errorHandler);
    }

    function SaveHandler(event) {
        if (event && typeof event.preventDefault === "function") {
            event.preventDefault();
        }

        SetHandler();
        DataverseLabelTranslator.NormalizeGridChanges();
        DataverseLabelTranslator.SetSaveButtonDisabled(true);

        if (!currentHandler || typeof currentHandler.Save !== "function") {
            DataverseLabelTranslator.SetSaveButtonDisabled(false);
            DialogHelper.alert("No save handler is available for the selected type.", { title: "Save" });
            return;
        }

        currentHandler.Save().catch(function (error) {
            DataverseLabelTranslator.SetSaveButtonDisabled(false);
            DataverseLabelTranslator.errorHandler(error);
        });
    }

    function TriggerUnavailable(name) {
        return function () {
            DialogHelper.alert(name + " is not available in the DataverseLabelTranslator shell yet.", { title: name });
        };
    }

    function GetRootGridRecords(grid) {
        return (grid.records || []).filter(function (record) {
            return !record.w2ui || !record.w2ui.parent_recid;
        });
    }

    function HasChildGridRecords(record) {
        return record && record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0;
    }

    function AppendRecordTree(flatRecords, record, expand) {
        flatRecords.push(record);

        if (!HasChildGridRecords(record)) {
            return;
        }

        record.w2ui.expanded = !!expand;

        for (var i = 0; i < record.w2ui.children.length; i++) {
            var child = record.w2ui.children[i];
            child.w2ui = child.w2ui || {};
            child.w2ui.parent_recid = record.recid;

            if (!Array.isArray(child.w2ui.children)) {
                child.w2ui.children = [];
            }

            if (expand) {
                AppendRecordTree(flatRecords, child, true);
            }
        }
    }

    function ToggleExpandCollapse(expand) {
        var grid = DataverseLabelTranslator.GetGrid();
        var rootRecords = GetRootGridRecords(grid);
        var flatRecords = [];

        for (var i = 0; i < rootRecords.length; i++) {
            AppendRecordTree(flatRecords, rootRecords[i], expand);
        }

        grid.records = flatRecords;
        grid.total = flatRecords.length;
        if (grid.last) {
            grid.last.idCache = {};
        }
        if (grid.searchData && grid.searchData.length > 0) {
            grid.localSearch(true);
        }
        grid.refresh();
    }

    function EscapeRegex(text) {
        return String(text || "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    }

    function OpenFindAndReplaceDialog() {
        DialogHelper.ShowFindAndReplaceDialog(DataverseLabelTranslator.GetGrid().columns, function (values) {
            DataverseLabelTranslator.FindRecords(
                undefined,
                values.find,
                values.replace,
                values.regex,
                values.ignoreCase,
                values.columnId,
                values.columnText
            );
        });
    }

    function FillEntitySelector(entities) {
        entities = (entities || []).slice().sort(function (left, right) {
            var leftText = (left.DisplayName || left.LogicalName || left.SchemaName || "").toLowerCase();
            var rightText = (right.DisplayName || right.LogicalName || right.SchemaName || "").toLowerCase();

            if (leftText < rightText) return -1;
            if (leftText > rightText) return 1;
            return 0;
        });

        var toolbar = GetToolbar();
        var entitySelect = toolbar.get("entitySelect").items;

        for (var i = 0; i < entities.length; i++) {
            var entity = entities[i];
            var label = entity.DisplayName || "";
            entitySelect.push({
                id: entity.SchemaName,
                text: label ? label + " (" + entity.LogicalName + ")" : entity.LogicalName,
                icon: "icon-entity"
            });
            entityMetadata[entity.SchemaName] = entity.MetadataId;
        }

        DataverseLabelTranslator.allEntities = entities;
        DataverseLabelTranslator.entityMetadata = entityMetadata;
        return entities;
    }

    function FillSolutionSelector(solutions) {
        var toolbar = GetToolbar();
        var solutionSelect = toolbar.get("solutionSelect");
        solutionSelect.items = [];

        for (var i = 0; i < solutions.length; i++) {
            var solution = solutions[i];
            solutionSelect.items.push({
                id: solution.solutionid,
                text: solution.friendlyname + " (" + solution.uniquename + ")",
                icon: "icon-solution"
            });
        }

        RefreshToolbar();
        return solutions;
    }

    function RepopulateEntitySelector(solutionId) {
        var toolbar = GetToolbar();
        var entitySelectItem = toolbar.get("entitySelect");
        entitySelectItem.selected = "none";
        entitySelectItem.items = [{ id: "none", text: "None", icon: "icon-empty" }, { text: "--" }];
        entityMetadata = {};
        DataverseLabelTranslator.entityMetadata = entityMetadata;

        if (!solutionId) {
            RefreshToolbar();
            return Promise.resolve();
        }

        DataverseLabelTranslator.LockGrid(Helper.GetOperationLoading());

        return Helper.GetEntities(solutionId)
            .then(function (solutionEntities) {
                FillEntitySelector(solutionEntities);
                DataverseLabelTranslator.UnlockGrid();
                SetToolbarItemsEnabled(["entitySelect", "type", "load"], true);
                ApplyTypeVisibilityForEntity("entitySelect:none");
                RefreshToolbar();
            })
            .catch(function (error) {
                DataverseLabelTranslator.UnlockGrid();
                DataverseLabelTranslator.errorHandler(error);
            });
    }

    function HandleToolbarClick(event) {
        var target = String(event.target || "");
        var toolbar = GetToolbar();

        if (target === "about") {
            DialogHelper.ShowAbout();
            return;
        }

        if (target === "help") {
            DialogHelper.ShowHelp();
            return;
        }

        if (target === "autoTranslate") {
            DataverseLabelTranslator.ShowAITranslate();
            return;
        }

        if (target === "aiSettings") {
            if (window.AppService && typeof AppService.ShowAppSettings === "function") {
                AppService.ShowAppSettings();
                return;
            }

            TriggerUnavailable("App Settings")();
            return;
        }

        if (target === "applyDictionary") {
            TriggerUnavailable("Apply Dictionary")();
            return;
        }

        if (target === "dictionary") {
            if (window.DictionaryService && DictionaryService.ShowDictionaryPrompt) {
                DictionaryService.ShowDictionaryPrompt();
                return;
            }

            TriggerUnavailable("Dictionary")();
            return;
        }

        if (target === "addSelectedDictionary") {
            TriggerUnavailable("Add selected translation to dictionary")();
            return;
        }

        if (target.indexOf("expandAll") !== -1) {
            ToggleExpandCollapse(true);
            return;
        }

        if (target.indexOf("collapseAll") !== -1) {
            ToggleExpandCollapse(false);
            return;
        }

        if (target.startsWith("solutionSelect:")) {
            toolbar.get("solutionSelect").selected = target.replace("solutionSelect:", "");
            RepopulateEntitySelector(DataverseLabelTranslator.GetSolution());
            return;
        }

        if (target.startsWith("entitySelect:")) {
            toolbar.get("entitySelect").selected = target.replace("entitySelect:", "");
            ApplyTypeVisibilityForEntity(target);
            RefreshToolbar();
            return;
        }

        if (target.startsWith("type:")) {
            toolbar.get("type").selected = target.replace("type:", "");
            UpdateComponentDropdown(DataverseLabelTranslator.GetType());
            SetHandler();
            SetToolbarItemsEnabled(["load"], DataverseLabelTranslator.GetType() !== "none");
            DataverseLabelTranslator.SetSaveButtonDisabled(true);
            RefreshToolbar();
            return;
        }

        if (target.startsWith("component:")) {
            toolbar.get("component").selected = target.replace("component:", "");
            RefreshToolbar();
        }
    }

    function InitializeGrid() {
        if (w2ui.grid) {
            w2ui.grid.destroy();
        }

        var toolbarItems = [
            {
                type: "menu-radio",
                id: "solutionSelect",
                icon: "icon-solution",
                tooltip: "Solution",
                text: function (item) {
                    var el = this.get("solutionSelect:" + item.selected);
                    return el ? CompactToolbarText(GetToolbarDisplayName(el.text), 24) : "Solution";
                },
                selected: null,
                items: []
            },
            {
                type: "menu-radio",
                id: "entitySelect",
                icon: "icon-entity",
                tooltip: "Entity",
                text: function (item) {
                    return GetEntityToolbarText(item, this);
                },
                selected: "none",
                items: [{ id: "none", text: "None", icon: "icon-empty" }, { text: "--" }]
            },
            {
                type: "menu-radio",
                id: "type",
                icon: "icon-type",
                tooltip: "Translation type",
                text: function (item) {
                    if (item.selected === "none") {
                        return "None";
                    }

                    var el = this.get("type:" + item.selected);
                    return el ? CompactToolbarText(el.text, 18, true) : "Type";
                },
                selected: "none",
                items: [
                    { id: "none", text: "None", icon: "icon-empty" },
                    { text: "--" },
                    { id: "attributes", text: "Attributes", icon: "icon-attribute" },
                    { id: "options", text: "Option Sets", icon: "icon-options" },
                    { id: "forms", text: "Forms", icon: "icon-form" },
                    { id: "views", text: "Views", icon: "icon-view" },
                    { id: "formMeta", text: "Form Metadata", icon: "icon-layout" },
                    { id: "entityMeta", text: "Entity Metadata", icon: "icon-entity" },
                    { id: "relationships", text: "Relationships", icon: "icon-link" },
                    { id: "charts", text: "Charts", icon: "icon-chart" },
                    { id: "bpf", text: "Business Process Flows", icon: "icon-flow" },
                    { id: "businessRules", text: "Business Rules", icon: "icon-flow" },
                    { id: "ribbons", text: "Ribbons", icon: "icon-grid" },
                    { id: "commands", text: "Commands", icon: "icon-component" },
                    { id: "entityMessages", text: "Entity Messages", icon: "icon-description" },
                    { id: "content", text: "Content Snippets", icon: "icon-code" },
                    { id: "sitemap", text: "Sitemap", icon: "icon-sitemap" },
                    { id: "dashboards", text: "Dashboards", icon: "icon-dashboard" },
                    { id: "webresources", text: "Web Resources", icon: "icon-file-code" },
                    { id: "globalOptionSet", text: "Global Option Set", icon: "icon-global-options" }
                ]
            },
            {
                type: "menu-radio",
                id: "component",
                icon: "icon-component",
                tooltip: "Component",
                text: function (item) {
                    var el = this.get("component:" + item.selected);
                    return el ? CompactToolbarText(el.text, 18) : "Component";
                },
                selected: "DisplayText",
                items: [
                    { id: "DisplayText", text: "Display Text", icon: "icon-label" },
                    { id: "Description", text: "Description", icon: "icon-description" }
                ]
            },
            { type: "break" },
            {
                type: "button",
                id: "load",
                text: "Load",
                tooltip: "Load selected data",
                icon: "icon-load",
                onClick: LoadHandler
            },
            { type: "break", id: "break-context" },
            {
                type: "button",
                hidden: true,
                id: "removeOverriddenAttributeLabels",
                text: "",
                tooltip: "Remove overridden attribute labels",
                icon: "icon-eraser",
                onClick: function () {
                    DataverseLabelTranslatorHandler.RemoveOverriddenCellLabels();
                }
            },
            { type: "button", id: "autoTranslate", text: "", tooltip: "Auto Translate", icon: "icon-translate" },
            { type: "button", id: "aiSettings", text: "", tooltip: "App Settings", icon: "w2ui-icon-settings" },
            { type: "break" },
            { type: "button", id: "applyDictionary", text: "", tooltip: "Apply dictionary", icon: "icon-book-check" },
            {
                type: "button",
                id: "addSelectedDictionary",
                text: "",
                tooltip: "Add selected translation to dictionary",
                icon: "icon-book-plus"
            },
            { type: "button", id: "dictionary", text: "", tooltip: "Manage dictionary", icon: "icon-book" },
            { type: "spacer" },
            { type: "button", id: "about", text: "", tooltip: "About", icon: "icon-about" },
            { type: "button", id: "help", text: "", tooltip: "Help", icon: "icon-help" }
        ];

        new w2grid({
            name: "grid",
            box: "#grid",
            show: {
                toolbar: true,
                footer: true,
                toolbarSave: true,
                toolbarSearch: true,
                toolbarReload: false,
                statusRecordID: false
            },
            multiSearch: false,
            searches: [{ field: "schemaName", text: "Schema Name", type: "text", operator: "contains" }],
            columns: [
                {
                    field: "schemaName",
                    text: "Schema Name",
                    size: DataverseLabelTranslator.defaultSchemaNameSize,
                    sortable: true,
                    resizable: true,
                    frozen: true
                }
            ],
            onSave: SaveHandler,
            onChange: function (event) {
                event.onComplete = function () {
                    DataverseLabelTranslator.NormalizeGridChanges();
                    DataverseLabelTranslator.SetSaveButtonDisabled(!DataverseLabelTranslator.HasPendingChanges());
                    DataverseLabelTranslator.UpdateChangedCellFooter(event);
                };
            },
            onClick: function (event) {
                event.onComplete = function () {
                    DataverseLabelTranslator.UpdateChangedCellFooter(event);
                };
            },
            onDblClick: function (event) {
                event.onComplete = function () {
                    DataverseLabelTranslator.UpdateChangedCellFooter(event);
                };
            },
            onEditField: function () {
                DataverseLabelTranslator.SetSaveButtonDisabled(false);
            },
            onSearch: function (event) {
                event.onComplete = NormalizeGridSearchUiSoon;
            },
            toolbar: {
                items: toolbarItems,
                onClick: HandleToolbarClick
            }
        }).render();

        var gridToolbar = GetToolbar();
        gridToolbar.insert("w2ui-search-advanced", {
            type: "menu",
            id: "toggle",
            text: "",
            tooltip: "Expand/collapse rows",
            icon: "icon-tree",
            items: [
                { type: "button", text: "Expand all records", id: "expandAll", icon: "w2ui-icon-expand" },
                { type: "button", text: "Collapse all records", id: "collapseAll", icon: "w2ui-icon-collapse" }
            ]
        });
        gridToolbar.insert("w2ui-search-advanced", { type: "break", id: "break-toggle" });
        gridToolbar.insert("w2ui-search-advanced", {
            type: "button",
            text: "",
            tooltip: "Find and replace",
            icon: "icon-find-replace",
            id: "findReplace",
            onClick: function () {
                OpenFindAndReplaceDialog();
            }
        });

        var saveBtn = gridToolbar.get("w2ui-save");
        if (saveBtn) {
            saveBtn.text = "Save";
            saveBtn.tooltip = "Save changes";
            gridToolbar.remove("w2ui-save");
            gridToolbar.add(saveBtn);
        }

        DataverseLabelTranslator.SetSaveButtonDisabled(true);
        SetSolutionRequiredState(false);
        NormalizeGridSearchUiSoon();
    }

    function GetOriginalRecordValue(record, field) {
        if (!record || !HasOwnProperty(record, field)) {
            return "";
        }

        return record[field] == null ? "" : record[field];
    }

    function NormalizeComparableGridValue(value) {
        return value == null ? "" : String(value);
    }

    function GridValuesEqual(left, right) {
        return NormalizeComparableGridValue(left) === NormalizeComparableGridValue(right);
    }

    function EscapeHtml(text) {
        return String(text || "").replace(/[&<>"]/g, function (m) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[m];
        });
    }

    function GetRecordFieldValue(record, field) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && HasOwnProperty(record.w2ui.changes, field)) {
            return record.w2ui.changes[field];
        }

        if (HasOwnProperty(record, field)) {
            return record[field];
        }

        return "";
    }

    function FlattenRecords(records) {
        return (records || []).reduce(function (all, record) {
            var children = FlattenRecords(record.w2ui && record.w2ui.children ? record.w2ui.children : []);
            return all.concat(children).concat([record]);
        }, []);
    }

    function SetChangedCellFooter(html) {
        var grid = DataverseLabelTranslator.GetGrid();
        var footer =
            grid && grid.box ? grid.box.querySelector("#grid_" + grid.name + "_footer .w2ui-footer-center") : null;

        if (footer) {
            footer.innerHTML = html || "";
        }
    }

    DataverseLabelTranslator.GetGrid = function () {
        return w2ui.grid;
    };

    DataverseLabelTranslator.GetSolution = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("solutionSelect") : null;
        return item ? item.selected : null;
    };

    DataverseLabelTranslator.GetEntity = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("entitySelect") : null;
        return item ? item.selected : "none";
    };

    DataverseLabelTranslator.GetEntityId = function () {
        return entityMetadata[DataverseLabelTranslator.GetEntity()] || null;
    };

    DataverseLabelTranslator.GetType = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("type") : null;
        return item ? item.selected : "sitemap";
    };

    DataverseLabelTranslator.GetComponent = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("component") : null;
        return item ? item.selected : "DisplayText";
    };

    DataverseLabelTranslator.IsDescriptionComponent = function () {
        return DataverseLabelTranslator.GetComponent() === "Description";
    };

    DataverseLabelTranslator.IsDisplayTextComponent = function () {
        return DataverseLabelTranslator.GetComponent() === "DisplayText";
    };

    DataverseLabelTranslator.GetCurrentComponentText = function () {
        var component = DataverseLabelTranslator.GetComponent();

        if (component === "DisplayText") {
            return "Display Text";
        }

        if (component === "Description") {
            return "Description";
        }

        return component || "";
    };

    DataverseLabelTranslator.GetCurrentToolbarTypeText = function () {
        return GetTypeStateLabel(DataverseLabelTranslator.GetType());
    };

    DataverseLabelTranslator.GetTypeStateLabel = GetTypeStateLabel;

    DataverseLabelTranslator.SetMetadata = function (metadata) {
        DataverseLabelTranslator.metadata = metadata || [];
        return DataverseLabelTranslator.metadata;
    };

    DataverseLabelTranslator.GetMetadata = function () {
        return DataverseLabelTranslator.metadata || [];
    };

    DataverseLabelTranslator.GetBaseLanguage = function () {
        return DataverseLabelTranslator.baseLanguage || null;
    };

    DataverseLabelTranslator.SetBaseLanguage = function (languageCode) {
        DataverseLabelTranslator.baseLanguage = languageCode;
        return DataverseLabelTranslator.baseLanguage;
    };

    DataverseLabelTranslator.GetAllRecords = function () {
        return Array.from(new Set(FlattenRecords(DataverseLabelTranslator.GetGrid().records || [])));
    };

    DataverseLabelTranslator.GetColumns = function (includeSchemaName) {
        var columns = DataverseLabelTranslator.GetGrid().columns.map(function (column) {
            return column.field;
        });

        return includeSchemaName
            ? columns
            : columns.filter(function (field) {
                  return field !== "schemaName";
              });
    };

    DataverseLabelTranslator.GetByRecId = function (records, recid) {
        records = records || DataverseLabelTranslator.GetAllRecords();
        for (var i = 0; i < records.length; i++) {
            if (records[i].recid === recid) {
                return records[i];
            }
        }

        return null;
    };

    DataverseLabelTranslator.GetAttributeById = function (id) {
        var metadata = DataverseLabelTranslator.GetMetadata();
        for (var i = 0; i < metadata.length; i++) {
            if (metadata[i].MetadataId === id) {
                return metadata[i];
            }
        }

        return null;
    };

    DataverseLabelTranslator.ClearColumns = function () {
        var grid = DataverseLabelTranslator.GetGrid();
        var columns = DataverseLabelTranslator.GetColumns(false);
        for (var i = 0; i < columns.length; i++) {
            grid.removeColumn(columns[i]);
        }

        grid.searches = (grid.searches || []).filter(function (search) {
            return search && search.field === "schemaName";
        });
    };

    DataverseLabelTranslator.ApplyLanguageColumns = function (columns) {
        var grid = DataverseLabelTranslator.GetGrid();
        var normalizedColumns = [];
        var seen = {};

        columns = columns || [];
        for (var i = 0; i < columns.length; i++) {
            var field = String((columns[i] && columns[i].field) || "");
            if (!field || seen[field]) {
                continue;
            }

            seen[field] = true;
            normalizedColumns.push({
                field: field,
                text:
                    /^\d+$/.test(field) && window.Helper && Helper.FormatLanguageColumnHeader
                        ? Helper.FormatLanguageColumnHeader(field)
                        : (columns[i] && columns[i].text) || field
            });
        }

        DataverseLabelTranslator.columnRestoreNeeded = normalizedColumns.length > 0;
        DataverseLabelTranslator.ClearColumns();

        if (normalizedColumns.length === 0) {
            grid.refresh();
            return;
        }

        var schemaSize = DataverseLabelTranslator.defaultSchemaNameSize || "30%";
        var schemaSizeNumber = parseInt(schemaSize.replace("%"), 10);
        var columnWidth = (100 - (isNaN(schemaSizeNumber) ? 30 : schemaSizeNumber)) / normalizedColumns.length;

        for (var j = 0; j < normalizedColumns.length; j++) {
            grid.addColumn({
                field: normalizedColumns[j].field,
                text: normalizedColumns[j].text,
                size: columnWidth + "%",
                sortable: true,
                editable: { type: "text" },
                render: DataverseLabelTranslator.CreateTranslationCellRenderer(normalizedColumns[j].field)
            });
            grid.addSearch({ field: normalizedColumns[j].field, text: normalizedColumns[j].text, type: "text" });
        }

        grid.refresh();
    };

    DataverseLabelTranslator.RenderTranslationCell = function (record, field) {
        var value = GetRecordFieldValue(record, field);

        if (value !== null && typeof value !== "undefined" && String(value).length > 0) {
            return EscapeHtml(value);
        }

        if (record && record._emptyEditablePlaceholders && HasOwnProperty(record._emptyEditablePlaceholders, field)) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-editable" title="Click to edit">' +
                EscapeHtml(record._emptyEditablePlaceholders[field]) +
                "</span>"
            );
        }

        if (record && record._emptyEditablePlaceholder) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-editable" title="Click to edit">' +
                EscapeHtml(record._emptyEditablePlaceholder) +
                "</span>"
            );
        }

        if (record && record._emptyReadonlyPlaceholder) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-readonly" title="Read only">' +
                EscapeHtml(record._emptyReadonlyPlaceholder) +
                "</span>"
            );
        }

        return "";
    };

    DataverseLabelTranslator.CreateTranslationCellRenderer = function (field) {
        return function (record) {
            return DataverseLabelTranslator.RenderTranslationCell(record, field);
        };
    };

    DataverseLabelTranslator.AddSummary = function (records) {
        var count = (records || []).filter(function (record) {
            return !(record.w2ui && record.w2ui.summary);
        }).length;
        var summary = {
            w2ui: { summary: true },
            recid: "Summary-1",
            schemaName: '<span style="float: right;">Of ' + count + " labels in total</span>"
        };
        var languages = DataverseLabelTranslator.GetColumns(false);

        for (var i = 0; i < languages.length; i++) {
            var language = String(languages[i]);
            var translatedRecords = 0;
            var flatRecords = FlattenRecords(records);

            for (var j = 0; j < flatRecords.length; j++) {
                if (flatRecords[j][language]) {
                    translatedRecords++;
                }
            }

            summary[language] =
                translatedRecords + " translated (" + (flatRecords.length - translatedRecords) + " untranslated)";
        }

        records.push(summary);
    };

    DataverseLabelTranslator.NormalizeRecordChanges = function (record) {
        if (!record || !record.w2ui || !record.w2ui.changes) {
            return false;
        }

        var changed = false;
        var changes = record.w2ui.changes;

        for (var field in changes) {
            if (!HasOwnProperty(changes, field)) {
                continue;
            }

            if (GridValuesEqual(GetOriginalRecordValue(record, field), changes[field])) {
                delete changes[field];
                changed = true;
            }
        }

        if (Object.keys(changes).length === 0) {
            delete record.w2ui.changes;
            changed = true;
        }

        return changed;
    };

    DataverseLabelTranslator.NormalizeGridChanges = function (records) {
        records = records || DataverseLabelTranslator.GetAllRecords();
        var normalizedRecords = [];

        for (var i = 0; i < records.length; i++) {
            if (DataverseLabelTranslator.NormalizeRecordChanges(records[i])) {
                normalizedRecords.push(records[i]);
            }
        }

        return normalizedRecords;
    };

    DataverseLabelTranslator.HasPendingChanges = function (records) {
        records = records || DataverseLabelTranslator.GetAllRecords();

        for (var i = 0; i < records.length; i++) {
            DataverseLabelTranslator.NormalizeRecordChanges(records[i]);

            if (records[i].w2ui && records[i].w2ui.changes && Object.keys(records[i].w2ui.changes).length > 0) {
                return true;
            }
        }

        return false;
    };

    DataverseLabelTranslator.HasLoadedRecords = function () {
        var records = DataverseLabelTranslator.GetAllRecords();

        for (var i = 0; i < records.length; i++) {
            if (!(records[i].w2ui && records[i].w2ui.summary)) {
                return true;
            }
        }

        return false;
    };

    DataverseLabelTranslator.ApplyGridChangeValue = function (record, field, value) {
        if (!record) {
            return false;
        }

        if (GridValuesEqual(GetOriginalRecordValue(record, field), value)) {
            if (record.w2ui && record.w2ui.changes && HasOwnProperty(record.w2ui.changes, field)) {
                delete record.w2ui.changes[field];
                DataverseLabelTranslator.NormalizeRecordChanges(record);
                return true;
            }

            return false;
        }

        record.w2ui = record.w2ui || {};
        record.w2ui.changes = record.w2ui.changes || {};

        if (HasOwnProperty(record.w2ui.changes, field) && GridValuesEqual(record.w2ui.changes[field], value)) {
            return false;
        }

        record.w2ui.changes[field] = value;
        return true;
    };

    DataverseLabelTranslator.ApplyFindAndReplace = function (selected, results) {
        var grid = DataverseLabelTranslator.GetGrid();
        var savable = false;

        for (var i = 0; i < selected.length; i++) {
            var result = DataverseLabelTranslator.GetByRecId(results, selected[i]);
            var record = result
                ? DataverseLabelTranslator.GetByRecId(DataverseLabelTranslator.GetAllRecords(), result.recid)
                : null;

            if (!record) {
                continue;
            }

            if (
                DataverseLabelTranslator.ApplyGridChangeValue(
                    record,
                    result.column,
                    result.w2ui && result.w2ui.changes ? result.w2ui.changes.replaced : result.replaced
                )
            ) {
                savable = true;
                grid.refreshRow(record.recid);
            }
        }

        if (savable) {
            DataverseLabelTranslator.SetSaveButtonDisabled(false);
        }
    };

    DataverseLabelTranslator.FindRecords = function (records, find, replace, useRegex, ignoreCase, column, columnName) {
        records = records || DataverseLabelTranslator.GetAllRecords();
        var findings = [];
        var flags = ignoreCase ? "i" : "";
        var regex = new RegExp(useRegex ? find : EscapeRegex(find), flags);

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.w2ui && record.w2ui.summary) {
                continue;
            }

            var value = GetRecordFieldValue(record, column);
            if (value === null || typeof value === "undefined") {
                continue;
            }

            var replaced = String(value).replace(regex, replace);
            if (String(value) === replaced) {
                continue;
            }

            findings.push({
                recid: record.recid,
                schemaName: record.schemaName,
                column: column,
                columnName: columnName,
                current: value,
                replaced: replaced
            });
        }

        DialogHelper.ShowFindAndReplaceResults(findings, DataverseLabelTranslator.ApplyFindAndReplace);
    };

    DataverseLabelTranslator.LockGrid = function (message) {
        var grid = DataverseLabelTranslator.GetGrid();
        if (grid && typeof grid.lock === "function") {
            grid.lock(message || "Loading...", true);
        }
    };

    DataverseLabelTranslator.UnlockGrid = function () {
        var grid = DataverseLabelTranslator.GetGrid();
        if (grid && typeof grid.unlock === "function") {
            grid.unlock();
        }
    };

    DataverseLabelTranslator.SetSaveButtonDisabled = function (disabled) {
        var toolbar = GetToolbar();
        var saveButton = toolbar ? toolbar.get("w2ui-save") : null;
        if (saveButton) {
            saveButton.disabled = !!disabled;
            RefreshToolbar();
        }
    };

    DataverseLabelTranslator.SetLoadButtonDisabled = function (disabled) {
        var toolbar = GetToolbar();
        var loadButton = toolbar ? toolbar.get("load") : null;
        if (loadButton) {
            loadButton.disabled = !!disabled;
            RefreshToolbar();
        }
    };

    DataverseLabelTranslator.UpdateChangedCellFooter = function (event) {
        var grid = DataverseLabelTranslator.GetGrid();
        var recid = event && typeof event.recid !== "undefined" ? event.recid : null;
        var columnIndex = event && typeof event.column !== "undefined" ? event.column : null;
        var column = grid && columnIndex !== null ? grid.columns[columnIndex] : null;
        var record =
            recid !== null
                ? DataverseLabelTranslator.GetByRecId(DataverseLabelTranslator.GetAllRecords(), recid)
                : null;

        if (
            !record ||
            !column ||
            !record.w2ui ||
            !record.w2ui.changes ||
            !HasOwnProperty(record.w2ui.changes, column.field)
        ) {
            SetChangedCellFooter("");
            return;
        }

        SetChangedCellFooter(
            '<span style="display:flex;align-items:center;box-sizing:border-box;height:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left;padding:0 8px;transform:translateY(3px);">' +
                "<b>Column:&nbsp;</b>" +
                EscapeHtml(column.text || column.field) +
                " | <b>Old Value:&nbsp;</b>" +
                EscapeHtml(GetOriginalRecordValue(record, column.field) || "(empty)") +
                " | <b>New Value:&nbsp;</b>" +
                EscapeHtml(record.w2ui.changes[column.field] || "(empty)") +
                "</span>"
        );
    };

    DataverseLabelTranslator.ShowStatusBanner = function (options) {
        options = options || {};
        if (options.message) {
            DialogHelper.alert(options.message, { title: options.title || "Status" });
        }
    };

    DataverseLabelTranslator.StartOperationStatus = function () {};
    DataverseLabelTranslator.ApplyStoredOperationStatus = function () {};

    DataverseLabelTranslator.errorHandler = function (error) {
        DataverseLabelTranslator.UnlockGrid();
        return Helper.ShowError(error, { title: "Dataverse Label Translator" });
    };

    DataverseLabelTranslator.Initialize = function () {
        Helper.GetBaseLanguage()
            .then(function (baseLanguage) {
                DataverseLabelTranslator.SetBaseLanguage(baseLanguage);
                InitializeGrid();
                return Helper.GetAllNoneBaseLanguageCodes().then(function (installedLanguages) {
                    var languages = [baseLanguage].concat((installedLanguages && installedLanguages.LocaleIds) || []);
                    return Helper.BuildLanguageColumns(languages);
                });
            })
            .then(function (columns) {
                DataverseLabelTranslator.ApplyLanguageColumns(columns);
                return Helper.GetSolutions();
            })
            .then(function (solutions) {
                FillSolutionSelector(solutions || []);
                SetHandler();
            })
            .catch(DataverseLabelTranslator.errorHandler);
    };

    DataverseLabelTranslator.AiTranslateDataSource = {
        HasTranslatableRows: HasTranslatableRows
    };
})((window.DataverseLabelTranslator = window.DataverseLabelTranslator || {}));
