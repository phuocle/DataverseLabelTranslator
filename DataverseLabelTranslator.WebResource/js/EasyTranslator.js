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
        if (!window.AIService || typeof AIService.OpenWorkspace !== "function") {
            return ShowLegacyAiTranslate();
        }

        var dataSource = EasyTranslator.BuildAiTranslateDataSource();
        if (!HasTranslatableRows(dataSource.rows)) {
            return ShowLegacyAiTranslate();
        }

        return AIService.OpenWorkspace(dataSource);
    };

    var currentHandler = null;
    var entityMetadata = {};
    var allEntities = [];

    EasyTranslator.defaultSchemaNameSize = "20%";
    EasyTranslator.entityMetadata = entityMetadata;
    EasyTranslator.allEntities = allEntities;
    EasyTranslator.columnRestoreNeeded = false;

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
        if (window.EasyTranslatorHandler && EasyTranslatorHandler.IsUnifiedType(EasyTranslator.GetType())) {
            currentHandler = EasyTranslatorHandler;
        }

        var toolbar = GetToolbar();
        if (toolbar && toolbar.get("removeOverriddenAttributeLabels")) {
            if (EasyTranslator.GetType() === "forms") {
                toolbar.show("removeOverriddenAttributeLabels");
            } else {
                toolbar.hide("removeOverriddenAttributeLabels");
            }
        }
    }

    function IsGlobalType(type) {
        return ["sitemap", "dashboards", "webresources", "globalOptionSet", "content"].indexOf(type) !== -1;
    }

    function UpdateComponentDropdown(selectedType) {
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
        SetHandler();

        if (!currentHandler || typeof currentHandler.Load !== "function") {
            DialogHelper.alert("No load handler is available for the selected type.", { title: "Load" });
            return;
        }

        if (!IsGlobalType(EasyTranslator.GetType()) && EasyTranslator.GetEntity() === "none") {
            DialogHelper.alert("Select an entity before loading this type.", { title: "Load" });
            return;
        }

        EasyTranslator.GetGrid().sort();
        currentHandler.Load().catch(EasyTranslator.errorHandler);
    }

    function SaveHandler(event) {
        if (event && typeof event.preventDefault === "function") {
            event.preventDefault();
        }

        SetHandler();
        EasyTranslator.NormalizeGridChanges();
        EasyTranslator.SetSaveButtonDisabled(true);

        if (!currentHandler || typeof currentHandler.Save !== "function") {
            EasyTranslator.SetSaveButtonDisabled(false);
            DialogHelper.alert("No save handler is available for the selected type.", { title: "Save" });
            return;
        }

        currentHandler.Save().catch(function (error) {
            EasyTranslator.SetSaveButtonDisabled(false);
            EasyTranslator.errorHandler(error);
        });
    }

    function TriggerUnavailable(name) {
        return function () {
            DialogHelper.alert(name + " is not available in the EasyTranslator shell yet.", { title: name });
        };
    }

    function ToggleExpandCollapse(expand) {
        var grid = EasyTranslator.GetGrid();
        var records = grid.records || [];

        function setExpanded(record) {
            if (record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0) {
                record.w2ui.expanded = !!expand;
                for (var i = 0; i < record.w2ui.children.length; i++) {
                    setExpanded(record.w2ui.children[i]);
                }
            }
        }

        for (var i = 0; i < records.length; i++) {
            setExpanded(records[i]);
        }

        grid.refresh();
    }

    function EscapeRegex(text) {
        return String(text || "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    }

    function OpenFindAndReplaceDialog() {
        DialogHelper.ShowFindAndReplaceDialog(EasyTranslator.GetGrid().columns, function (values) {
            EasyTranslator.FindRecords(
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

        EasyTranslator.allEntities = entities;
        EasyTranslator.entityMetadata = entityMetadata;
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
        EasyTranslator.entityMetadata = entityMetadata;

        if (!solutionId) {
            RefreshToolbar();
            return Promise.resolve();
        }

        EasyTranslator.LockGrid("Loading solution entities...");

        return XrmService.GetEntities(solutionId)
            .then(function (solutionEntities) {
                FillEntitySelector(solutionEntities);
                EasyTranslator.UnlockGrid();
                RefreshToolbar();
            })
            .catch(function (error) {
                EasyTranslator.UnlockGrid();
                EasyTranslator.errorHandler(error);
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
            EasyTranslator.ShowAITranslate();
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
            RepopulateEntitySelector(EasyTranslator.GetSolution());
            return;
        }

        if (target.startsWith("entitySelect:")) {
            toolbar.get("entitySelect").selected = target.replace("entitySelect:", "");
            RefreshToolbar();
            return;
        }

        if (target.startsWith("type:")) {
            toolbar.get("type").selected = target.replace("type:", "");
            UpdateComponentDropdown(EasyTranslator.GetType());
            SetHandler();
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
                    var el = this.get("type:" + item.selected);
                    return el ? CompactToolbarText(el.text, 18, true) : "Type";
                },
                selected: "sitemap",
                items: [
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
                    EasyTranslatorHandler.RemoveOverriddenCellLabels();
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
                    size: EasyTranslator.defaultSchemaNameSize,
                    sortable: true,
                    resizable: true,
                    frozen: true
                }
            ],
            onSave: SaveHandler,
            onChange: function (event) {
                event.onComplete = function () {
                    EasyTranslator.NormalizeGridChanges();
                    EasyTranslator.SetSaveButtonDisabled(!EasyTranslator.HasPendingChanges());
                    EasyTranslator.UpdateChangedCellFooter(event);
                };
            },
            onClick: function (event) {
                event.onComplete = function () {
                    EasyTranslator.UpdateChangedCellFooter(event);
                };
            },
            onDblClick: function (event) {
                event.onComplete = function () {
                    EasyTranslator.UpdateChangedCellFooter(event);
                };
            },
            onEditField: function () {
                EasyTranslator.SetSaveButtonDisabled(false);
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

        EasyTranslator.SetSaveButtonDisabled(true);
        UpdateComponentDropdown("sitemap");
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
        var grid = EasyTranslator.GetGrid();
        var footer =
            grid && grid.box ? grid.box.querySelector("#grid_" + grid.name + "_footer .w2ui-footer-center") : null;

        if (footer) {
            footer.innerHTML = html || "";
        }
    }

    EasyTranslator.GetGrid = function () {
        return w2ui.grid;
    };

    EasyTranslator.GetSolution = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("solutionSelect") : null;
        return item ? item.selected : null;
    };

    EasyTranslator.GetEntity = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("entitySelect") : null;
        return item ? item.selected : "none";
    };

    EasyTranslator.GetEntityId = function () {
        return entityMetadata[EasyTranslator.GetEntity()] || null;
    };

    EasyTranslator.GetType = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("type") : null;
        return item ? item.selected : "sitemap";
    };

    EasyTranslator.GetComponent = function () {
        var toolbar = GetToolbar();
        var item = toolbar ? toolbar.get("component") : null;
        return item ? item.selected : "DisplayText";
    };

    EasyTranslator.GetCurrentToolbarTypeText = function () {
        return GetTypeStateLabel(EasyTranslator.GetType());
    };

    EasyTranslator.GetTypeStateLabel = GetTypeStateLabel;

    EasyTranslator.GetAllRecords = function () {
        return Array.from(new Set(FlattenRecords(EasyTranslator.GetGrid().records || [])));
    };

    EasyTranslator.GetColumns = function (includeSchemaName) {
        var columns = EasyTranslator.GetGrid().columns.map(function (column) {
            return column.field;
        });

        return includeSchemaName
            ? columns
            : columns.filter(function (field) {
                  return field !== "schemaName";
              });
    };

    EasyTranslator.GetByRecId = function (records, recid) {
        records = records || EasyTranslator.GetAllRecords();
        for (var i = 0; i < records.length; i++) {
            if (records[i].recid === recid) {
                return records[i];
            }
        }

        return null;
    };

    EasyTranslator.GetAttributeById = function (id) {
        var metadata = EasyTranslator.GetMetadata();
        for (var i = 0; i < metadata.length; i++) {
            if (metadata[i].MetadataId === id) {
                return metadata[i];
            }
        }

        return null;
    };

    EasyTranslator.ClearColumns = function () {
        var columns = EasyTranslator.GetColumns(false);
        for (var i = 0; i < columns.length; i++) {
            EasyTranslator.GetGrid().removeColumn(columns[i]);
        }
    };

    EasyTranslator.RenderTranslationCell = function (record, field) {
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

    EasyTranslator.CreateTranslationCellRenderer = function (field) {
        return function (record) {
            return EasyTranslator.RenderTranslationCell(record, field);
        };
    };

    EasyTranslator.AddSummary = function (records) {
        var count = (records || []).filter(function (record) {
            return !(record.w2ui && record.w2ui.summary);
        }).length;
        var summary = {
            w2ui: { summary: true },
            recid: "Summary-1",
            schemaName: '<span style="float: right;">Of ' + count + " labels in total</span>"
        };
        var languages = EasyTranslator.GetColumns(false);

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

    EasyTranslator.NormalizeRecordChanges = function (record) {
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

    EasyTranslator.NormalizeGridChanges = function (records) {
        records = records || EasyTranslator.GetAllRecords();
        var normalizedRecords = [];

        for (var i = 0; i < records.length; i++) {
            if (EasyTranslator.NormalizeRecordChanges(records[i])) {
                normalizedRecords.push(records[i]);
            }
        }

        return normalizedRecords;
    };

    EasyTranslator.HasPendingChanges = function (records) {
        records = records || EasyTranslator.GetAllRecords();

        for (var i = 0; i < records.length; i++) {
            EasyTranslator.NormalizeRecordChanges(records[i]);

            if (records[i].w2ui && records[i].w2ui.changes && Object.keys(records[i].w2ui.changes).length > 0) {
                return true;
            }
        }

        return false;
    };

    EasyTranslator.ApplyGridChangeValue = function (record, field, value) {
        if (!record) {
            return false;
        }

        if (GridValuesEqual(GetOriginalRecordValue(record, field), value)) {
            if (record.w2ui && record.w2ui.changes && HasOwnProperty(record.w2ui.changes, field)) {
                delete record.w2ui.changes[field];
                EasyTranslator.NormalizeRecordChanges(record);
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

    EasyTranslator.ApplyFindAndReplace = function (selected, results) {
        var grid = EasyTranslator.GetGrid();
        var savable = false;

        for (var i = 0; i < selected.length; i++) {
            var result = EasyTranslator.GetByRecId(results, selected[i]);
            var record = result ? EasyTranslator.GetByRecId(EasyTranslator.GetAllRecords(), result.recid) : null;

            if (!record) {
                continue;
            }

            if (
                EasyTranslator.ApplyGridChangeValue(
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
            EasyTranslator.SetSaveButtonDisabled(false);
        }
    };

    EasyTranslator.FindRecords = function (records, find, replace, useRegex, ignoreCase, column, columnName) {
        records = records || EasyTranslator.GetAllRecords();
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

        DialogHelper.ShowFindAndReplaceResults(findings, EasyTranslator.ApplyFindAndReplace);
    };

    EasyTranslator.LockGrid = function (message) {
        var grid = EasyTranslator.GetGrid();
        if (grid && typeof grid.lock === "function") {
            grid.lock(message || "Loading...", true);
        }
    };

    EasyTranslator.UnlockGrid = function () {
        var grid = EasyTranslator.GetGrid();
        if (grid && typeof grid.unlock === "function") {
            grid.unlock();
        }
    };

    EasyTranslator.SetSaveButtonDisabled = function (disabled) {
        var toolbar = GetToolbar();
        var saveButton = toolbar ? toolbar.get("w2ui-save") : null;
        if (saveButton) {
            saveButton.disabled = !!disabled;
            RefreshToolbar();
        }
    };

    EasyTranslator.SetLoadButtonDisabled = function (disabled) {
        var toolbar = GetToolbar();
        var loadButton = toolbar ? toolbar.get("load") : null;
        if (loadButton) {
            loadButton.disabled = !!disabled;
            RefreshToolbar();
        }
    };

    EasyTranslator.UpdateChangedCellFooter = function (event) {
        var grid = EasyTranslator.GetGrid();
        var recid = event && typeof event.recid !== "undefined" ? event.recid : null;
        var columnIndex = event && typeof event.column !== "undefined" ? event.column : null;
        var column = grid && columnIndex !== null ? grid.columns[columnIndex] : null;
        var record = recid !== null ? EasyTranslator.GetByRecId(EasyTranslator.GetAllRecords(), recid) : null;

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

    EasyTranslator.ShowStatusBanner = function (options) {
        options = options || {};
        if (options.message) {
            DialogHelper.alert(options.message, { title: options.title || "Status" });
        }
    };

    EasyTranslator.StartOperationStatus = function () {};
    EasyTranslator.ApplyStoredOperationStatus = function () {};

    EasyTranslator.errorHandler = function (error) {
        EasyTranslator.UnlockGrid();
        return Helper.ShowError(error, { title: "Dataverse Label Translator" });
    };

    EasyTranslator.Initialize = function () {
        XrmService.GetBaseLanguage()
            .then(function (baseLanguage) {
                EasyTranslator.SetBaseLanguage(baseLanguage);
                InitializeGrid();
                return XrmService.GetSolutions();
            })
            .then(function (solutions) {
                FillSolutionSelector(solutions || []);
                SetHandler();
            })
            .catch(EasyTranslator.errorHandler);
    };

    EasyTranslator.AiTranslateDataSource = {
        HasTranslatableRows: HasTranslatableRows
    };
})((window.EasyTranslator = window.EasyTranslator || {}));
