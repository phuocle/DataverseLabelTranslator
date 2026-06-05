(function (DashboardHandler, undefined) {
    "use strict";

    var userText = {
        noChangesToSave: "There are no dashboard changes to save.",
        title: "Dashboards",
        unknownRow: "(unknown)"
    };
    var actionName = "Dashboard";

    function GetMetadata(app) {
        if (typeof app.GetMetadata === "function") {
            return app.GetMetadata();
        }

        return app.metadata || [];
    }

    function GetBaseLanguage(app) {
        if (app.baseLanguage) {
            return app.baseLanguage;
        }

        if (typeof app.GetBaseLanguage === "function") {
            return app.GetBaseLanguage();
        }

        return null;
    }

    function SetBaseLanguage(app, baseLanguage) {
        if (!baseLanguage) {
            return;
        }

        if (typeof app.SetBaseLanguage === "function") {
            app.SetBaseLanguage(baseLanguage);
            return;
        }

        app.baseLanguage = baseLanguage;
    }

    function GetDashboardLocalizedLabels(dashboard) {
        return Helper.GetComponentLocalizedLabels(dashboard, Helper.ComponentTypes.DisplayText);
    }

    function GetDashboardLabel(dashboard, languageCode) {
        var labels = GetDashboardLocalizedLabels(dashboard);
        var stringLanguageCode = String(languageCode || "");

        for (var i = 0; i < labels.length; i++) {
            if (String(labels[i].LanguageCode) === stringLanguageCode) {
                return labels[i].Label || "";
            }
        }

        return "";
    }

    function GetDashboardBaseLabel(dashboard, app) {
        return (
            GetDashboardLabel(dashboard, GetBaseLanguage(app)) ||
            dashboard.name ||
            dashboard.formid ||
            userText.unknownRow
        );
    }

    function GetRowPath(record) {
        return (record && (record.schemaName || record.recid)) || userText.unknownRow;
    }

    function HasNoChanges(updates) {
        return !updates || !updates.dashboardUpdates || updates.dashboardUpdates.length === 0;
    }

    function GetDashboardIds(result) {
        return result && result.dashboardIds ? result.dashboardIds : [];
    }

    function GetDashboardIdPayload(output) {
        var dashboardIds = GetDashboardIds(output);
        return dashboardIds.length > 0 ? { dashboardIds: dashboardIds } : null;
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var dashboardUpdates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes || record.w2ui.summary) {
                continue;
            }

            var changes = record.w2ui.changes;
            Helper.ValidateBaseLanguageNotEmpty(record, changes, {
                app: app,
                getRowPath: GetRowPath
            });

            var labels = Helper.GetChangedLabels(changes, true);
            if (labels.length < 1) {
                continue;
            }

            dashboardUpdates.push({
                dashboardId: record.recid,
                labels: labels
            });
        }

        return { dashboardUpdates: dashboardUpdates };
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        var records = [];
        var dashboards = GetMetadata(app).slice();
        var editablePlaceholder = Helper.GetPlaceholderDisplayText();
        var baseEditablePlaceholder = Helper.GetPlaceholderDisplayTextBase();

        grid.clear();

        dashboards.sort(function (a, b) {
            return GetDashboardBaseLabel(a, app).localeCompare(GetDashboardBaseLabel(b, app));
        });

        for (var i = 0; i < dashboards.length; i++) {
            var dashboard = dashboards[i];
            var record = {
                recid: dashboard.formid,
                schemaName: GetDashboardBaseLabel(dashboard, app)
            };

            Helper.ApplyPlaceholder(record, editablePlaceholder, baseEditablePlaceholder, app);
            Helper.AddLocalizedLabelsToRecord(record, GetDashboardLocalizedLabels(dashboard));
            records.push(record);
        }

        Helper.FinalizeGrid(records, app);
    }

    DashboardHandler.Load = function (lockText) {
        var app = Helper.GetTranslator();

        app.LockGrid(lockText || Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: function () {
                return {
                    solutionId: app.GetSolution()
                };
            },
            onLoaded: function (output) {
                SetBaseLanguage(app, output.baseLanguage);
                app.SetMetadata(output.dashboards || []);
                FillTable();
            }
        });
    };

    DashboardHandler.Save = function () {
        var updates = GetUpdates();

        if (HasNoChanges(updates)) {
            return DialogHelper.alert(userText.noChangesToSave, {
                title: userText.title
            });
        }

        return Helper.RunServerSaveFlow({
            app: Helper.GetTranslator(),
            actionName: actionName,
            getSavePayload: function () {
                return updates;
            },
            getPublishPayload: GetDashboardIdPayload,
            getPublishedPayload: GetDashboardIdPayload,
            shouldReload: function (output) {
                return GetDashboardIds(output).length > 0;
            },
            reloadAction: function () {
                return DashboardHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };
})((window.DashboardHandler = Object(window.DashboardHandler)));
