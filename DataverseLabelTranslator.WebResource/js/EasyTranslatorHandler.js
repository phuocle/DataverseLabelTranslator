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

    function FillTable(gridOutput) {
        var app = GetApp();
        var grid = app.GetGrid();
        var records = [];
        var rows = gridOutput && Array.isArray(gridOutput.rows) ? gridOutput.rows : [];

        grid.clear();

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
                "entityMeta",
                "views",
                "formMeta",
                "relationships",
                "charts",
                "entityMessages"
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
            shouldReload: HasPublishTargets,
            reloadAction: function () {
                return EasyTranslatorHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };
})((window.EasyTranslatorHandler = window.EasyTranslatorHandler || {}));
