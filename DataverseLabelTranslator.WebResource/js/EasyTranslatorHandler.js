(function (EasyTranslatorHandler, undefined) {
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
        var translator = window.XrmTranslator || app;
        var grid = app.GetGrid();

        if (columns.length === 0) {
            return;
        }

        translator.columnRestoreNeeded = true;
        translator.ClearColumns();

        var schemaSize = translator.defaultSchemaNameSize || "30%";
        var schemaSizeNumber = parseInt(schemaSize.replace("%"), 10);
        var columnWidth = (100 - (isNaN(schemaSizeNumber) ? 30 : schemaSizeNumber)) / columns.length;

        for (var i = 0; i < columns.length; i++) {
            var field = String(columns[i].field || "");
            if (!field) {
                continue;
            }

            grid.addColumn({
                field: field,
                text: columns[i].text || field,
                size: columnWidth + "%",
                sortable: true,
                editable: { type: "text" },
                render: translator.CreateTranslationCellRenderer(field)
            });
            grid.addSearch({ field: field, text: columns[i].text || field, type: "text" });
        }
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

    EasyTranslatorHandler.IsUnifiedType = function (type) {
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

    EasyTranslatorHandler.Load = function (lockText) {
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

    EasyTranslatorHandler.Save = function () {
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
                return EasyTranslatorHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };

    EasyTranslatorHandler.RemoveOverriddenCellLabels = function () {
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
                    return EasyTranslatorHandler.Load(Helper.GetOperationReLoading());
                }
            });
        });
    };
})((window.EasyTranslatorHandler = window.EasyTranslatorHandler || {}));
