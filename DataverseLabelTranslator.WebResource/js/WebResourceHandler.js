(function (WebResourceHandler, undefined) {
    "use strict";

    var idSeparator = "|";
    var actionName = "WebResource";

    function GetGroupKey(id) {
        var separatorIndex = id.indexOf(idSeparator);

        if (separatorIndex === -1) {
            return id;
        }

        return id.substring(0, separatorIndex);
    }

    function ApplyDisplayTextPlaceholder(record) {
        record._emptyEditablePlaceholder = "Add display text";

        if (XrmTranslator.baseLanguage) {
            record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
            record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = "Add display text (*)";
        }
    }

    function ValidateDisplayTextBaseLanguageChange(record, group, changes) {
        if (!XrmTranslator.baseLanguage) {
            return;
        }

        var baseLanguage = String(XrmTranslator.baseLanguage);
        if (!Object.prototype.hasOwnProperty.call(changes, baseLanguage)) {
            return;
        }

        if (!Helper.IsEmptyLabelValue(changes[baseLanguage])) {
            return;
        }

        var groupName = group && group.__displayName ? group.__displayName : GetGroupKey(record.recid);
        var rowName = record && record.schemaName != null ? String(record.schemaName) : "";
        var path =
            groupName && rowName && groupName !== rowName
                ? groupName + " > " + rowName
                : rowName || groupName || "(unknown)";

        throw new Error(
            "Display Text in the base language (" +
                Helper.GetLanguageColumnText(baseLanguage) +
                ") cannot be empty.\n" +
                "Row: " +
                path
        );
    }

    function GetUpdates(records) {
        var resourceChangesMap = {};

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            var groupKey = GetGroupKey(record.recid);

            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var group = XrmTranslator.metadata[groupKey];
            var changes = record.w2ui.changes;
            ValidateDisplayTextBaseLanguageChange(record, group, changes);

            for (var lcid in changes) {
                if (!changes.hasOwnProperty(lcid)) {
                    continue;
                }

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
                                if (String(group[k].__lcid) == String(XrmTranslator.baseLanguage)) {
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
                    value: w2utils.decodeTags(changes[lcid])
                });
            }
        }

        var result = [];
        for (var key in resourceChangesMap) {
            if (resourceChangesMap.hasOwnProperty(key)) {
                result.push(resourceChangesMap[key]);
            }
        }

        return result;
    }

    function FillKey(record, property, group) {
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

        ApplyDisplayTextPlaceholder(keyRecord);
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
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var records = [];

        var groups = Object.keys(XrmTranslator.metadata);

        for (var i = 0; i < groups.length; i++) {
            var key = groups[i];
            var group = XrmTranslator.metadata[key];

            var record = {
                recid: key,
                schemaName: group.__displayName || key,
                w2ui: {
                    editable: false,
                    children: []
                }
            };
            record._emptyReadonlyPlaceholder = "-";

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

                FillKey(record, property, group);
            }

            records.push(record);
        }

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    WebResourceHandler.Load = function () {
        XrmTranslator.metadata = {};

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, {
            solutionId: XrmTranslator.GetSolution()
        })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);

                if (output.baseLanguage) {
                    XrmTranslator.baseLanguage = output.baseLanguage;
                }

                XrmTranslator.metadata = BuildMetadata(output);
                FillTable();
                return result;
            })
            .catch(function (error) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.errorHandler(error);
            });
    };

    function GetWebResourceIds(result) {
        return result && result.webresourceIds ? result.webresourceIds : [];
    }

    WebResourceHandler.Save = function () {
        var records = XrmTranslator.GetAllRecords();
        var resourceChanges = GetUpdates(records);

        XrmTranslator.LockGrid("Saving ...");

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, {
            resourceChanges: resourceChanges
        })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);
                var webresourceIds = GetWebResourceIds(output);

                if (webresourceIds.length === 0) {
                    XrmTranslator.UnlockGrid();
                    XrmTranslator.EnableLoadAndSave();
                    return result;
                }

                XrmTranslator.LockGrid("Publishing ...");
                return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, {
                    webresourceIds: webresourceIds
                }).then(function (publishingResult) {
                    var publishingOutput = Helper.GetCustomActionObject(publishingResult);
                    XrmTranslator.LockGrid("Published");
                    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, {
                        webresourceIds: GetWebResourceIds(publishingOutput)
                    });
                });
            })
            .then(function (result) {
                var output = Helper.GetCustomActionObject(result);
                if (GetWebResourceIds(output).length === 0) {
                    return result;
                }

                XrmTranslator.LockGrid("Re-Loading ...");
                return WebResourceHandler.Load().then(function () {
                    XrmTranslator.EnableLoadAndSave();
                    return result;
                });
            })
            .catch(function (error) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                throw error;
            });
    };
})((window.WebResourceHandler = window.WebResourceHandler || {}));
