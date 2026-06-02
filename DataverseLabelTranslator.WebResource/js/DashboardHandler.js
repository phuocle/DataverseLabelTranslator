window.DashboardHandler = Object(window.DashboardHandler);

(function (DashboardHandler, undefined) {
    "use strict";

    DashboardHandler.dashboards = null;

    function IsEmptyLabelValue(value) {
        return value == null || String(value).trim().length === 0;
    }

    function GetLanguageColumnText(languageCode) {
        var columns = XrmTranslator.GetGrid().columns || [];
        var field = String(languageCode);

        for (var i = 0; i < columns.length; i++) {
            if (String(columns[i].field) === field) {
                return columns[i].text || columns[i].caption || columns[i].label || field;
            }
        }

        return field;
    }

    function GetSelectedSolutionDashboardIds() {
        var solutionId = XrmTranslator.GetSolution();

        if (!solutionId || solutionId === "all") {
            return WebApiClient.Promise.resolve(null);
        }

        return WebApiClient.Retrieve({
            entityName: "solutioncomponent",
            queryParams:
                "?$select=objectid&$filter=_solutionid_value eq " +
                solutionId +
                " and componenttype eq " +
                XrmTranslator.ComponentType.SystemForm
        }).then(function (response) {
            return (response.value || []).map(function (component) {
                return component.objectid;
            });
        });
    }

    function GetDashboardQueryForLoad() {
        return GetSelectedSolutionDashboardIds().then(function (dashboardIds) {
            var query =
                "?$select=formid,name,type,objecttypecode&$filter=formactivationstate eq 1 and iscustomizable/Value eq true and (type eq 0 or type eq 10)";

            if (dashboardIds === null) {
                return query;
            }

            if (dashboardIds.length === 0) {
                return null;
            }

            var idFilter = dashboardIds
                .map(function (id) {
                    return "formid eq " + id;
                })
                .join(" or ");

            return query + " and (" + idFilter + ")";
        });
    }

    function GetLocalizedLabels(dashboard) {
        return dashboard && dashboard.labels && dashboard.labels.Label && dashboard.labels.Label.LocalizedLabels
            ? dashboard.labels.Label.LocalizedLabels
            : [];
    }

    function GetDashboardLabel(dashboard, languageCode) {
        var labels = GetLocalizedLabels(dashboard);
        var stringLanguageCode = String(languageCode || "");

        for (var i = 0; i < labels.length; i++) {
            if (String(labels[i].LanguageCode) === stringLanguageCode) {
                return labels[i].Label || "";
            }
        }

        return "";
    }

    function GetDashboardBaseLabel(dashboard) {
        return GetDashboardLabel(dashboard, XrmTranslator.baseLanguage) || dashboard.name || dashboard.formid;
    }

    function AddDashboardLanguageValues(record, dashboard) {
        var labels = GetLocalizedLabels(dashboard);

        for (var i = 0; i < labels.length; i++) {
            record[String(labels[i].LanguageCode)] = labels[i].Label || "";
        }
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        var records = [];
        var dashboards = DashboardHandler.dashboards || [];

        grid.clear();

        dashboards.sort(function (a, b) {
            return GetDashboardBaseLabel(a).localeCompare(GetDashboardBaseLabel(b));
        });

        for (var i = 0; i < dashboards.length; i++) {
            var dashboard = dashboards[i];
            var record = {
                recid: dashboard.formid,
                schemaName: GetDashboardBaseLabel(dashboard),
                _emptyEditablePlaceholder: "Add display text"
            };

            if (XrmTranslator.baseLanguage) {
                record._emptyEditablePlaceholders = {};
                record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = "Add display text (*)";
            }

            AddDashboardLanguageValues(record, dashboard);
            records.push(record);
        }

        XrmTranslator.metadata = dashboards;
        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    function ValidateBaseLanguageChange(record, changes) {
        if (!XrmTranslator.baseLanguage) {
            return;
        }

        var baseLanguage = String(XrmTranslator.baseLanguage);
        if (!Object.prototype.hasOwnProperty.call(changes, baseLanguage)) {
            return;
        }

        if (!IsEmptyLabelValue(changes[baseLanguage])) {
            return;
        }

        throw new Error(
            "Dashboard display text in the base language (" +
                GetLanguageColumnText(baseLanguage) +
                ") cannot be empty.\n" +
                "Row: " +
                (record.schemaName || record.recid || "(unknown)")
        );
    }

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (!record.w2ui || !record.w2ui.changes || record.w2ui.summary) {
                continue;
            }

            var changes = record.w2ui.changes;
            ValidateBaseLanguageChange(record, changes);

            for (var languageCode in changes) {
                if (!changes.hasOwnProperty(languageCode)) {
                    continue;
                }

                var text = changes[languageCode];
                if (text == null) {
                    text = "";
                }

                updates.push({
                    dashboardId: record.recid,
                    languageCode: String(languageCode),
                    text: text
                });
            }
        }

        return updates;
    }

    function GetDashboardById(dashboardId) {
        var dashboards = DashboardHandler.dashboards || [];

        for (var i = 0; i < dashboards.length; i++) {
            if (dashboards[i].formid === dashboardId) {
                return dashboards[i];
            }
        }

        return null;
    }

    function CloneDashboardLabels(dashboard) {
        return GetLocalizedLabels(dashboard).map(function (label) {
            return {
                Label: label.Label || "",
                LanguageCode: parseInt(label.LanguageCode, 10),
                IsManaged: label.IsManaged || null,
                MetadataId: label.MetadataId || null,
                HasChanged: label.HasChanged || null
            };
        });
    }

    function ApplyDashboardLabelChanges(labels, updates) {
        for (var i = 0; i < updates.length; i++) {
            var update = updates[i];
            var languageCode = parseInt(update.languageCode, 10);
            var text = update.text == null ? "" : update.text;
            var found = false;

            for (var j = 0; j < labels.length; j++) {
                if (parseInt(labels[j].LanguageCode, 10) === languageCode) {
                    labels[j].Label = text;
                    labels[j].HasChanged = true;
                    found = true;
                    break;
                }
            }

            if (!found) {
                labels.push({
                    Label: text,
                    LanguageCode: languageCode,
                    HasChanged: true
                });
            }
        }

        return labels;
    }

    function GetDashboardUpdatesById(updates) {
        var groups = {};
        var result = [];

        for (var i = 0; i < updates.length; i++) {
            var update = updates[i];
            var dashboardId = update.dashboardId;

            if (!groups[dashboardId]) {
                groups[dashboardId] = {
                    dashboardId: dashboardId,
                    updates: []
                };
                result.push(groups[dashboardId]);
            }

            groups[dashboardId].updates.push(update);
        }

        return result;
    }

    function GetChangedDashboardIds(updates) {
        var seen = {};
        var dashboardIds = [];

        for (var i = 0; i < updates.length; i++) {
            var dashboardId = updates[i].dashboardId;

            if (seen[dashboardId]) {
                continue;
            }

            seen[dashboardId] = true;
            dashboardIds.push({
                recid: dashboardId
            });
        }

        return dashboardIds;
    }

    function SaveDashboardGroup(group, index, total) {
        var dashboard = GetDashboardById(group.dashboardId);

        if (!dashboard) {
            return Promise.resolve();
        }

        XrmTranslator.LockGridProgress("Saving " + XrmTranslator.GetCurrentToolbarTypeText(), index + 1, total);

        return WebApiClient.SendRequest("POST", WebApiClient.GetApiUrl() + "SetLocLabels", {
            Labels: ApplyDashboardLabelChanges(CloneDashboardLabels(dashboard), group.updates),
            EntityMoniker: {
                "@odata.type": "Microsoft.Dynamics.CRM.systemform",
                formid: group.dashboardId
            },
            AttributeName: "name"
        });
    }

    function RetrieveDashboardLabels(dashboard) {
        var retrieveLabelsRequest = WebApiClient.Requests.RetrieveLocLabelsRequest.with({
            urlParams: {
                EntityMoniker: "{'@odata.id':'systemforms(" + dashboard.formid + ")'}",
                AttributeName: "'name'",
                IncludeUnpublished: true
            }
        });

        return WebApiClient.Promise.props({
            formid: dashboard.formid,
            name: dashboard.name,
            type: dashboard.type,
            objecttypecode: dashboard.objecttypecode,
            labels: WebApiClient.Execute(retrieveLabelsRequest)
        });
    }

    DashboardHandler.Load = function () {
        return GetDashboardQueryForLoad()
            .then(function (query) {
                if (!query) {
                    return [];
                }

                return XrmTranslator.RunAsBaseLanguage(function () {
                    return WebApiClient.Retrieve({
                        entityName: "systemform",
                        queryParams: query
                    });
                });
            })
            .then(function (response) {
                var dashboards = response && response.value ? response.value : [];

                return WebApiClient.Promise.all(dashboards.map(RetrieveDashboardLabels));
            })
            .then(function (dashboards) {
                DashboardHandler.dashboards = dashboards || [];
                FillTable();
            })
            .catch(XrmTranslator.errorHandler);
    };

    DashboardHandler.Save = function () {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                var updates = GetUpdates();

                if (updates.length === 0) {
                    return false;
                }

                var groupedUpdates = GetDashboardUpdatesById(updates);
                var dashboardIds = GetChangedDashboardIds(updates);

                return WebApiClient.Promise.each(groupedUpdates, function (group, index) {
                    return SaveDashboardGroup(group, index, groupedUpdates.length);
                }).then(function () {
                    return {
                        dashboardIds: dashboardIds
                    };
                });
            },
            shouldPublish: function (result) {
                return !!(result && result.dashboardIds && result.dashboardIds.length > 0);
            },
            publishAction: function (result) {
                return XrmTranslator.PublishDashboard(result.dashboardIds);
            },
            reloadAction: function () {
                return DashboardHandler.Load();
            }
        });
    };
})(window.DashboardHandler);
