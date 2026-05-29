(function (ModernCommandHandler, undefined) {
    "use strict";

    var APP_ACTION_COMPONENT_TYPE = 10298;
    var idSeparator = "|";
    var commandData = [];

    var translatableAttributes = [
        { propertyName: "buttonlabeltext", displayName: "Text" },
        { propertyName: "buttontooltiptitle", displayName: "Title" },
        { propertyName: "buttontooltipdescription", displayName: "Description" },
        { propertyName: "buttonaccessibilitytext", displayName: "Accessibility Text" },
        { propertyName: "grouptitle", displayName: "Group Title" }
    ];

    var locationDisplayNames = {
        0: "Form",
        1: "Main Grid",
        2: "Sub Grid",
        3: "Associated Grid",
        4: "Quick Form",
        5: "Global Header",
        6: "Dashboard"
    };

    var commandTypeDisplayNames = {
        0: "Button",
        1: "Dropdown",
        2: "Split Button",
        3: "Group"
    };

    function escapeODataString(value) {
        return String(value || "").replace(/'/g, "''");
    }

    function getApiUrl() {
        return WebApiClient.GetApiUrl({ apiVersion: "9.2" });
    }

    function retrieveAll(url) {
        var items = [];

        function next(nextUrl) {
            return WebApiClient.SendRequest("GET", nextUrl)
            .then(function (response) {
                if (response && response.value) {
                    items = items.concat(response.value);
                }

                var nextLink = response && response["@odata.nextLink"];
                if (nextLink) {
                    return next(nextLink);
                }

                return items;
            });
        }

        return next(url);
    }

    function normalizeGuid(value) {
        return String(value || "").replace(/[{}]/g, "").toLowerCase();
    }

    function getLocalizedLabel(labelCollection) {
        var labels = labelCollection && labelCollection.Label && labelCollection.Label.LocalizedLabels
            ? labelCollection.Label.LocalizedLabels
            : [];

        var preferredLcids = [
            XrmTranslator.baseLanguage,
            XrmTranslator.userSettings && XrmTranslator.userSettings.uilanguageid
        ];

        for (var p = 0; p < preferredLcids.length; p++) {
            var preferred = preferredLcids[p];

            if (!preferred) {
                continue;
            }

            for (var i = 0; i < labels.length; i++) {
                if (labels[i].LanguageCode == preferred && labels[i].Label) {
                    return labels[i].Label;
                }
            }
        }

        for (var j = 0; j < labels.length; j++) {
            if (labels[j].Label) {
                return labels[j].Label;
            }
        }

        return "";
    }

    function getPropertyDefinition(propertyName) {
        for (var i = 0; i < translatableAttributes.length; i++) {
            if (translatableAttributes[i].propertyName === propertyName) {
                return translatableAttributes[i];
            }
        }

        return null;
    }

    function getPrimaryCommandText(command) {
        return command.buttonlabeltext ||
            command.grouptitle ||
            command.buttontooltiptitle ||
            command.name ||
            command.uniquename ||
            command.appactionid;
    }

    function getCommandTypeText(command) {
        return commandTypeDisplayNames[command.type] || ("Type " + command.type);
    }

    function getLocationText(command) {
        return locationDisplayNames[command.location] || ("Location " + command.location);
    }

    function getCommandNodeText(command, commandById) {
        var parts = [];
        var current = command;
        var guard = {};

        while (current) {
            var currentId = normalizeGuid(current.appactionid);

            if (guard[currentId]) {
                break;
            }

            guard[currentId] = true;
            parts.unshift(getCommandTypeText(current) + ": " + getPrimaryCommandText(current));

            var parentId = normalizeGuid(current._parentappactionid_value);
            current = parentId ? commandById[parentId] : null;
        }

        return getLocationText(command) + " / " + parts.join(" / ");
    }

    function getCommandSortPath(command, commandById) {
        var parts = [];
        var current = command;
        var guard = {};

        while (current) {
            var currentId = normalizeGuid(current.appactionid);

            if (guard[currentId]) {
                break;
            }

            guard[currentId] = true;
            parts.unshift([
                String(current.location == null ? 99 : current.location).padStart(3, "0"),
                String(Math.round(Number(current.sequence || 0) * 1000)).padStart(14, "0"),
                String(current.type == null ? 99 : current.type).padStart(3, "0"),
                getPrimaryCommandText(current).toLowerCase(),
                currentId
            ].join("~"));

            var parentId = normalizeGuid(current._parentappactionid_value);
            current = parentId ? commandById[parentId] : null;
        }

        return parts.join(">");
    }

    function cloneLabelCollection(labelCollection) {
        var labels = labelCollection && labelCollection.Label && labelCollection.Label.LocalizedLabels
            ? labelCollection.Label.LocalizedLabels
            : [];
        var cloned = [];

        for (var i = 0; i < labels.length; i++) {
            cloned.push({
                Label: labels[i].Label || "",
                LanguageCode: labels[i].LanguageCode,
                IsManaged: labels[i].IsManaged == null ? null : labels[i].IsManaged,
                MetadataId: labels[i].MetadataId == null ? null : labels[i].MetadataId,
                HasChanged: labels[i].HasChanged == null ? null : labels[i].HasChanged
            });
        }

        return {
            Label: {
                LocalizedLabels: cloned,
                UserLocalizedLabel: null
            }
        };
    }

    function applyChanges(changes, labels) {
        for (var change in changes) {
            if (!changes.hasOwnProperty(change)) {
                continue;
            }

            if (changes[change] === null || typeof changes[change] === "undefined") {
                continue;
            }

            var found = false;
            for (var i = 0; i < labels.length; i++) {
                if (labels[i].LanguageCode == change) {
                    labels[i].Label = changes[change];
                    labels[i].HasChanged = true;
                    found = true;
                    break;
                }
            }

            if (!found) {
                labels.push({
                    LanguageCode: parseInt(change, 10),
                    Label: changes[change],
                    HasChanged: true
                });
            }
        }
    }

    function retrieveLocLabels(commandId, propertyName) {
        var entityMoniker = encodeURIComponent("{'@odata.id':'appactions(" + commandId + ")'}");
        var url = getApiUrl() +
            "RetrieveLocLabels(EntityMoniker=@p1,AttributeName='" + propertyName + "',IncludeUnpublished=true)" +
            "?@p1=" + entityMoniker;

        return WebApiClient.SendRequest("GET", url);
    }

    function retrieveCommandLabels(command) {
        var requests = [];

        for (var i = 0; i < translatableAttributes.length; i++) {
            (function (propertyName) {
                requests.push(
                    retrieveLocLabels(command.appactionid, propertyName)
                    .then(function (labels) {
                        command.labels = command.labels || {};
                        command.labels[propertyName] = labels || { Label: { LocalizedLabels: [] } };
                    })
                );
            })(translatableAttributes[i].propertyName);
        }

        return WebApiClient.Promise.all(requests)
        .then(function () {
            return command;
        });
    }

    function getSolutionAppActionIds() {
        var solutionId = XrmTranslator.GetSolution();

        return retrieveAll(
            getApiUrl() +
            "solutioncomponents?$select=objectid,componenttype&$filter=_solutionid_value eq " + solutionId +
            " and componenttype eq " + APP_ACTION_COMPONENT_TYPE
        )
        .then(function (components) {
            var ids = {};

            for (var i = 0; i < components.length; i++) {
                ids[normalizeGuid(components[i].objectid)] = true;
            }

            return ids;
        });
    }

    function retrieveEntityCommands(solutionCommandIds) {
        var entityName = String(XrmTranslator.GetEntity() || "").toLowerCase();

        if (!entityName || entityName === "none") {
            return WebApiClient.Promise.resolve([]);
        }

        return retrieveAll(
            getApiUrl() +
            "appactions?$select=appactionid,name,uniquename,location,context,contextvalue,type," +
            "buttonlabeltext,buttontooltiptitle,buttontooltipdescription,buttonaccessibilitytext,grouptitle," +
            "_parentappactionid_value,sequence,componentstate,ismanaged,isdisabled,hidden,origin,statecode" +
            "&$filter=contextvalue eq '" + escapeODataString(entityName) + "' and statecode eq 0" +
            "&$orderby=location asc,sequence asc,name asc"
        )
        .then(function (commands) {
            var filtered = [];

            for (var i = 0; i < commands.length; i++) {
                var commandId = normalizeGuid(commands[i].appactionid);

                if (solutionCommandIds[commandId]) {
                    filtered.push(commands[i]);
                }
            }

            return filtered;
        });
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var commandById = {};
        for (var i = 0; i < commandData.length; i++) {
            commandById[normalizeGuid(commandData[i].appactionid)] = commandData[i];
        }

        commandData.sort(function (a, b) {
            return getCommandSortPath(a, commandById).localeCompare(getCommandSortPath(b, commandById));
        });

        var records = [];

        for (var c = 0; c < commandData.length; c++) {
            var command = commandData[c];
            var parent = {
                recid: command.appactionid,
                schemaName: getCommandNodeText(command, commandById),
                w2ui: {
                    editable: false,
                    children: []
                }
            };

            for (var p = 0; p < translatableAttributes.length; p++) {
                var property = translatableAttributes[p];
                var labelCollection = command.labels && command.labels[property.propertyName]
                    ? command.labels[property.propertyName]
                    : { Label: { LocalizedLabels: [] } };
                var child = {
                    recid: command.appactionid + idSeparator + property.propertyName,
                    schemaName: property.displayName,
                    _isModernCommandLabelRow: true,
                    _appActionId: command.appactionid,
                    _appActionUniqueName: command.uniquename,
                    _propertyName: property.propertyName,
                    _propertyDisplayName: property.displayName,
                    _labelCollection: cloneLabelCollection(labelCollection)
                };

                var labels = labelCollection.Label && labelCollection.Label.LocalizedLabels
                    ? labelCollection.Label.LocalizedLabels
                    : [];

                for (var l = 0; l < labels.length; l++) {
                    child[String(labels[l].LanguageCode)] = labels[l].Label || "";
                }

                parent.w2ui.children.push(child);
            }

            records.push(parent);
        }

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
    }

    function GetUpdates(records) {
        records = records || XrmTranslator.GetAllRecords();
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (!record._isModernCommandLabelRow || !record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var property = getPropertyDefinition(record._propertyName);
            if (!property) {
                continue;
            }

            var labelCollection = cloneLabelCollection(record._labelCollection);
            applyChanges(record.w2ui.changes, labelCollection.Label.LocalizedLabels);

            updates.push({
                recid: record._appActionId,
                uniqueName: record._appActionUniqueName,
                propertyName: record._propertyName,
                propertyDisplayName: record._propertyDisplayName,
                labels: labelCollection
            });
        }

        return updates;
    }

    ModernCommandHandler.Load = function () {
        commandData = [];
        XrmTranslator.metadata = [];

        return getSolutionAppActionIds()
        .then(retrieveEntityCommands)
        .then(function (commands) {
            XrmTranslator.LockGridProgress("Loading command labels", 0, commands.length);

            var requests = [];
            for (var i = 0; i < commands.length; i++) {
                requests.push(retrieveCommandLabels(commands[i]));
            }

            return WebApiClient.Promise.all(requests);
        })
        .then(function (commands) {
            commandData = commands || [];
            XrmTranslator.metadata = commandData;
            FillTable();
        })
        .catch(XrmTranslator.errorHandler);
    };

    ModernCommandHandler.SaveOnly = function (records) {
        var updates = GetUpdates(records);

        if (updates.length === 0) {
            return WebApiClient.Promise.resolve();
        }

        return XrmTranslator.ExecuteChangeSetBatches(updates, {
            progressLabel: "Saving command labels",
            batchNamePrefix: "batch_setcommandlabels",
            changeSetNamePrefix: "changeset_setcommandlabels",
            buildRequest: function (update) {
                return new WebApiClient.BatchRequest({
                    method: "POST",
                    url: getApiUrl() + "SetLocLabels",
                    payload: {
                        Labels: update.labels.Label.LocalizedLabels,
                        EntityMoniker: {
                            "@odata.type": "Microsoft.Dynamics.CRM.appaction",
                            appactionid: update.recid
                        },
                        AttributeName: update.propertyName
                    }
                });
            }
        });
    };

    ModernCommandHandler.Save = function () {
        XrmTranslator.LockGrid("Saving");

        return ModernCommandHandler.SaveOnly()
            .then(function () {
                XrmTranslator.LockGrid("Publishing");
                return XrmTranslator.Publish();
            })
            .then(function () {
                XrmTranslator.LockGrid("Reloading");
                return ModernCommandHandler.Load();
            })
            .catch(XrmTranslator.errorHandler);
    };

}(window.ModernCommandHandler = window.ModernCommandHandler || {}));
