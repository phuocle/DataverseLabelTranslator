(function (WebResourceHandler, undefined) {
    "use strict";

    var userText = {
        noChangesToSave: "There are no web resource changes to save.",
        rowPathSeparator: " > ",
        unknownRow: "(unknown)",
        title: "Web Resources"
    };
    var idSeparator = "|";
    var actionName = "WebResource";

    function GetMetadata(app) {
        return typeof app.GetMetadata === "function" ? app.GetMetadata() : app.metadata || {};
    }

    function SetMetadata(app, metadata) {
        if (typeof app.SetMetadata === "function") {
            app.SetMetadata(metadata);
            return;
        }

        app.metadata = metadata;
    }

    function GetGroupKey(id) {
        var separatorIndex = id.indexOf(idSeparator);

        if (separatorIndex === -1) {
            return id;
        }

        return id.substring(0, separatorIndex);
    }

    function GetDisplayTextRowPath(record, group) {
        var groupName = group && group.__displayName ? group.__displayName : GetGroupKey(record.recid);
        var rowName = record && record.schemaName != null ? String(record.schemaName) : "";

        if (groupName && rowName && groupName !== rowName) {
            return groupName + userText.rowPathSeparator + rowName;
        }

        return rowName || groupName || userText.unknownRow;
    }

    function HasNoChanges(updates) {
        return !updates || !updates.resourceChanges || updates.resourceChanges.length === 0;
    }

    function GetWebResourceIds(result) {
        return result && result.webresourceIds ? result.webresourceIds : [];
    }

    function GetWebResourceIdPayload(output) {
        var webresourceIds = GetWebResourceIds(output);
        return webresourceIds.length > 0 ? { webresourceIds: webresourceIds } : null;
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var metadata = GetMetadata(app);
        var resourceChangesMap = {};

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            var groupKey = GetGroupKey(record.recid);

            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var group = metadata[groupKey];
            var changes = record.w2ui.changes;
            Helper.ValidateBaseLanguageNotEmpty(record, changes, {
                app: app,
                getRowPath: function (changedRecord) {
                    return GetDisplayTextRowPath(changedRecord, group);
                }
            });

            var labels = Helper.GetChangedLabels(changes, true);

            for (var l = 0; l < labels.length; l++) {
                var lcid = String(labels[l].LanguageCode);
                var existingResource = null;
                if (group) {
                    for (var j = 0; j < group.length; j++) {
                        if (String(group[j].__lcid) === String(lcid)) {
                            existingResource = group[j];
                            break;
                        }
                    }
                }

                var changeKey;
                if (existingResource && existingResource.webresourceid) {
                    changeKey = existingResource.webresourceid;
                    if (!resourceChangesMap[changeKey]) {
                        resourceChangesMap[changeKey] = {
                            webresourceid: existingResource.webresourceid,
                            contentChanges: []
                        };
                    }
                } else {
                    changeKey = "__new__" + groupKey + "__" + lcid;
                    if (!resourceChangesMap[changeKey]) {
                        var baseResource = null;
                        if (group) {
                            for (var k = 0; k < group.length; k++) {
                                if (String(group[k].__lcid) == String(app.baseLanguage)) {
                                    baseResource = group[k];
                                    break;
                                }
                            }
                        }
                        resourceChangesMap[changeKey] = {
                            webresourceid: null,
                            lcid: String(lcid),
                            baseWebresourceid: baseResource ? baseResource.webresourceid : null,
                            contentChanges: []
                        };
                    }
                }

                resourceChangesMap[changeKey].contentChanges.push({
                    key: record.schemaName,
                    value: w2utils.decodeTags(labels[l].Label)
                });
            }
        }

        var result = [];
        for (var key in resourceChangesMap) {
            if (resourceChangesMap.hasOwnProperty(key)) {
                result.push(resourceChangesMap[key]);
            }
        }

        return { resourceChanges: result };
    }

    function FillKey(record, property, group, app) {
        var keyRecord = {
            recid: record.recid + idSeparator + property,
            schemaName: property
        };

        for (var i = 0; i < group.length; i++) {
            var resource = group[i];

            var value = resource.content[property];

            if (!resource.__lcid) {
                continue;
            }

            keyRecord[resource.__lcid] =
                value === null || typeof value === "undefined" ? "" : w2utils.encodeTags(value);
        }

        Helper.ApplyPlaceholder(
            keyRecord,
            Helper.GetPlaceholderDisplayText(),
            Helper.GetPlaceholderDisplayTextBase(),
            app
        );
        record.w2ui.children.push(keyRecord);
    }

    function BuildMetadata(output) {
        var metadata = {};
        var groups = output.groups || [];

        for (var i = 0; i < groups.length; i++) {
            var groupData = groups[i];
            var resources = groupData.resources || [];

            for (var j = 0; j < resources.length; j++) {
                resources[j].__lcid = resources[j].lcid;
            }

            resources.__displayName = groupData.displayName;
            metadata[groupData.key] = resources;
        }

        return metadata;
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        grid.clear();

        var records = [];

        var metadata = GetMetadata(app);
        var groups = Object.keys(metadata);

        for (var i = 0; i < groups.length; i++) {
            var key = groups[i];
            var group = metadata[key];

            var record = {
                recid: key,
                schemaName: group.__displayName || key,
                w2ui: {
                    editable: false,
                    children: []
                }
            };
            record._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();

            var properties = Array.from(
                new Set(
                    group
                        .map(function (g) {
                            return Object.keys(g.content);
                        })
                        .reduce(function (all, cur) {
                            return all.concat(cur);
                        }, [])
                )
            );

            for (var j = 0; j < properties.length; j++) {
                var property = properties[j];

                FillKey(record, property, group, app);
            }

            records.push(record);
        }

        Helper.FinalizeGrid(records, app);
    }

    WebResourceHandler.Load = function (lockText) {
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
                if (output.baseLanguage) {
                    app.baseLanguage = output.baseLanguage;
                }

                SetMetadata(app, BuildMetadata(output));
                FillTable();
            }
        });
    };

    WebResourceHandler.Save = function () {
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
            getPublishPayload: GetWebResourceIdPayload,
            getPublishedPayload: GetWebResourceIdPayload,
            shouldReload: function (output) {
                return GetWebResourceIds(output).length > 0;
            },
            reloadAction: function () {
                return WebResourceHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };
})((window.WebResourceHandler = window.WebResourceHandler || {}));
