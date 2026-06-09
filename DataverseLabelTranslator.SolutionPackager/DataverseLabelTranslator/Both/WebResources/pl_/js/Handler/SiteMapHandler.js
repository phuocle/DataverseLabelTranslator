(function (SiteMapHandler, undefined) {
    "use strict";

    var userText = {
        noChangesToSave: "There are no sitemap changes to save.",
        title: "Sitemap"
    };
    var idSeparator = "|";
    var actionName = "SiteMap";

    function GetComponent(app) {
        return app.IsDescriptionComponent() ? Helper.ComponentTypes.Description : Helper.ComponentTypes.DisplayText;
    }

    function GetMetadata(app) {
        return typeof app.GetMetadata === "function" ? app.GetMetadata() : app.metadata;
    }

    function SetMetadata(app, metadata) {
        if (typeof app.SetMetadata === "function") {
            app.SetMetadata(metadata);
            return;
        }

        app.metadata = metadata;
    }

    function GetDisplayTextRowPath(record) {
        return record && record.schemaName != null ? String(record.schemaName) : "(unknown)";
    }

    function HasNoChanges(updates) {
        return !updates || !updates.sitemapUpdates || updates.sitemapUpdates.length === 0;
    }

    function AddLabelsToRecord(record, labels) {
        for (var i = 0; labels && i < labels.length; i++) {
            var languageCode = labels[i].lcid || labels[i].LanguageCode;
            var label = labels[i].text || labels[i].Label;
            if (languageCode != null && label != null) {
                record[languageCode.toString()] = label;
            }
        }
    }

    function FindEntityLabels(sitemap, entityName) {
        var entityLabels = sitemap.entityLabels || [];
        for (var i = 0; i < entityLabels.length; i++) {
            if (entityLabels[i] && entityLabels[i].entityName === entityName) {
                return entityLabels[i].labels || [];
            }
        }

        return [];
    }

    function AddDefaultTitleToRecord(record, title, app) {
        if (!title) {
            return;
        }

        var columns = app.GetGrid().columns || [];
        for (var i = 0; i < columns.length; i++) {
            var field = columns[i].field;
            if (field && field !== "schemaName") {
                record[field.toString()] = title;
            }
        }
    }

    function BuildNodeRecord(sitemap, node, app) {
        var component = GetComponent(app);
        var record = {
            recid: sitemap.sitemapid + idSeparator + node.compositeId,
            schemaName: "[" + node.type + "] " + node.id + (node.entity ? " (" + node.entity + ")" : ""),
            _siteMapId: sitemap.sitemapid,
            _siteMapCompositeId: node.compositeId,
            _siteMapNodeType: node.nodeType,
            w2ui: { editable: true, children: [] }
        };

        var labels = component === Helper.ComponentTypes.Description ? node.descriptions : node.titles;

        if (labels && labels.length > 0) {
            AddLabelsToRecord(record, labels);
        } else if (component === Helper.ComponentTypes.DisplayText && node.entity) {
            AddLabelsToRecord(record, FindEntityLabels(sitemap, node.entity));
        } else if (component === Helper.ComponentTypes.DisplayText) {
            AddDefaultTitleToRecord(record, node.defaultTitle, app);
        }

        Helper.ApplyPlaceholder(
            record,
            app.IsDescriptionComponent() ? Helper.GetPlaceholderDescription() : Helper.GetPlaceholderDisplayText(),
            app.IsDisplayTextComponent() ? Helper.GetPlaceholderDisplayTextBase() : null,
            app
        );

        return record;
    }

    function ParseSiteMapXml(xmlString) {
        var parser = new DOMParser();
        var doc = parser.parseFromString(xmlString, "text/xml");
        var results = [];

        function getDirectChildren(parent, tagName) {
            var children = [];
            for (var i = 0; i < parent.childNodes.length; i++) {
                var child = parent.childNodes[i];
                if (child.nodeType === 1 && child.nodeName === tagName) {
                    children.push(child);
                }
            }
            return children;
        }

        function extractLabels(node, containerTag, itemTag, attrName) {
            var labels = [];
            var containers = getDirectChildren(node, containerTag);
            if (containers.length > 0) {
                var items = containers[0].getElementsByTagName(itemTag);
                for (var i = 0; i < items.length; i++) {
                    labels.push({
                        lcid: items[i].getAttribute("LCID"),
                        text: items[i].getAttribute(attrName)
                    });
                }
            }
            return labels;
        }

        var areas = doc.getElementsByTagName("Area");
        for (var a = 0; a < areas.length; a++) {
            var area = areas[a];
            var areaId = area.getAttribute("Id") || "";
            results.push({
                id: areaId,
                compositeId: areaId,
                nodeType: "Area",
                type: "Area",
                defaultTitle: area.getAttribute("Title") || "",
                titles: extractLabels(area, "Titles", "Title", "Title"),
                descriptions: extractLabels(area, "Descriptions", "Description", "Description")
            });

            var groups = getDirectChildren(area, "Group");
            for (var g = 0; g < groups.length; g++) {
                var group = groups[g];
                var groupId = group.getAttribute("Id") || "";
                results.push({
                    id: groupId,
                    compositeId: areaId + idSeparator + groupId,
                    nodeType: "Group",
                    type: "Group",
                    defaultTitle: group.getAttribute("Title") || "",
                    titles: extractLabels(group, "Titles", "Title", "Title"),
                    descriptions: extractLabels(group, "Descriptions", "Description", "Description")
                });

                var subAreas = getDirectChildren(group, "SubArea");
                for (var s = 0; s < subAreas.length; s++) {
                    var subArea = subAreas[s];
                    var subAreaId = subArea.getAttribute("Id") || "";
                    results.push({
                        id: subAreaId,
                        compositeId: areaId + idSeparator + groupId + idSeparator + subAreaId,
                        nodeType: "SubArea",
                        type: "SubArea",
                        defaultTitle: subArea.getAttribute("Title") || "",
                        entity: subArea.getAttribute("Entity") || "",
                        titles: extractLabels(subArea, "Titles", "Title", "Title"),
                        descriptions: extractLabels(subArea, "Descriptions", "Description", "Description")
                    });
                }
            }
        }

        return results;
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var sitemapUpdates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record || !record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var sitemapId = record._siteMapId;
            var compositeId = record._siteMapCompositeId;
            var parts = String(record.recid || "").split(idSeparator);

            if (!sitemapId && parts.length > 1) {
                sitemapId = parts[0];
                compositeId = parts.slice(1).join(idSeparator);
            }

            if (!sitemapId) {
                sitemapId = record.recid || "";
            }

            if (compositeId == null) {
                compositeId = record.recid || "";
            }

            if (!sitemapId || compositeId == null) {
                continue;
            }

            var changes = record.w2ui.changes;
            Helper.ValidateBaseLanguageNotEmpty(record, changes, {
                app: app,
                getRowPath: GetDisplayTextRowPath
            });

            var labels = Helper.GetChangedLabels(
                changes,
                app.IsDescriptionComponent() || app.IsDisplayTextComponent(),
                {
                    app: app,
                    includeBaseDisplayText: true,
                    record: record
                }
            );
            if (labels.length === 0) {
                continue;
            }

            sitemapUpdates.push({
                sitemapId: sitemapId,
                compositeId: compositeId,
                nodeType: record._siteMapNodeType || "SubArea",
                component: GetComponent(app),
                labels: labels
            });
        }

        return { sitemapUpdates: sitemapUpdates };
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        var metadata = GetMetadata(app);
        var records = [];

        grid.clear();

        for (var i = 0; i < metadata.length; i++) {
            var sitemap = metadata[i];
            if (!sitemap || !sitemap.sitemapxml) {
                continue;
            }

            var parent = {
                recid: sitemap.sitemapid,
                schemaName: sitemap.sitemapname || sitemap.sitemapid,
                w2ui: { editable: false, children: [] }
            };
            parent._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();

            try {
                var nodes = ParseSiteMapXml(sitemap.sitemapxml);
                for (var n = 0; n < nodes.length; n++) {
                    parent.w2ui.children.push(BuildNodeRecord(sitemap, nodes[n], app));
                }
            } catch (error) {
                app.errorHandler(error);
            }

            records.push(parent);
        }

        Helper.FinalizeGrid(records, app);
    }

    function GetSitemapIds(output) {
        return output && output.sitemapIds ? output.sitemapIds : [];
    }

    function GetPublishPayload(output) {
        var sitemapIds = GetSitemapIds(output);
        return sitemapIds.length > 0 ? { sitemapIds: sitemapIds } : null;
    }

    SiteMapHandler.Load = function (lockText) {
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
                SetMetadata(app, output.sitemaps || []);
                FillTable();
            }
        });
    };

    SiteMapHandler.Save = function () {
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
            getPublishPayload: GetPublishPayload,
            getPublishedPayload: GetPublishPayload,
            shouldReload: function (output) {
                return GetSitemapIds(output).length > 0;
            },
            reloadAction: function () {
                return SiteMapHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };
})((window.SiteMapHandler = window.SiteMapHandler || {}));
