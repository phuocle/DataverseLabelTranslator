(function (ViewHandler, undefined) {
    "use strict";
    var idSeparator = "|";

    function ApplyChanges(changes, labels) {
        for (var change in changes) {
            if (!changes.hasOwnProperty(change)) {
                continue;
            }

            // Skip empty labels
            if (!changes[change]) {
                continue;
            }

            var found = false;
            for (var i = 0; i < labels.length; i++) {
                var label = labels[i];

                if (label.LanguageCode == change) {
                    label.Label = changes[change];
                    label.HasChanged = true;
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

    function GetUpdates() {
        var records = XrmTranslator.GetGrid().records;
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (record.w2ui && record.w2ui.changes) {
                var parts = String(record.recid || "").split(idSeparator);
                var viewId = parts[0];
                var attributeName = parts[1] || "name";
                var view = XrmTranslator.GetAttributeByProperty("recid", viewId);

                if (!view || !view.labels || !view.labels[attributeName]) {
                    continue;
                }

                if (!view.labels[attributeName].Label) {
                    view.labels[attributeName].Label = { LocalizedLabels: [] };
                }
                if (!view.labels[attributeName].Label.LocalizedLabels) {
                    view.labels[attributeName].Label.LocalizedLabels = [];
                }

                var labels = view.labels[attributeName].Label.LocalizedLabels;

                var changes = record.w2ui.changes;

                ApplyChanges(changes, labels);
                updates.push({
                    recid: viewId,
                    attributeName: attributeName,
                    labels: view.labels[attributeName]
                });
            }
        }

        return updates;
    }

    var viewTypeMap = {
        0: "Public",
        1: "Advanced Find",
        2: "Associated",
        4: "Quick Find",
        16: "Address Book",
        32: "Sub Grid",
        64: "Lookup",
        128: "Offline Filters",
        256: "Offline Template",
        1024: "Saved Filters",
        2048: "Multi-entity Lookup",
        4096: "Custom Defined",
        8192: "Outlook",
        32768: "Service Appointment Book",
        131072: "Power BI",
        262144: "Modern Search",
        1048576: "Copilot"
    };

    function AddLabelRecord(records, view, attributeName, labelText) {
        var labels = view.labels && view.labels[attributeName] && view.labels[attributeName].Label
            ? view.labels[attributeName].Label.LocalizedLabels
            : [];

        var record = {
           recid: view.recid + idSeparator + attributeName,
           schemaName: (viewTypeMap[view.querytype] || ("Type " + view.querytype)) + " / " + labelText
        };

        for (var i = 0; i < labels.length; i++) {
            record[labels[i].LanguageCode.toString()] = labels[i].Label;
        }

        records.push(record);
    }

    function FillTable () {
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var records = [];

        for (var i = 0; i < XrmTranslator.metadata.length; i++) {
            var view = XrmTranslator.metadata[i];

            AddLabelRecord(records, view, "name", "Name");
            AddLabelRecord(records, view, "description", "Description");
        }

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    ViewHandler.Load = function() {
        var entityName = XrmTranslator.GetEntity();

        var entityMetadataId = XrmTranslator.entityMetadata[entityName];

        var queryRequest = {
            entityName: "savedquery",
            queryParams: "?$filter=returnedtypecode eq '" + entityName.toLowerCase() + "' and iscustomizable/Value eq true&$orderby=savedqueryid asc"
        };

        var languages = XrmTranslator.installedLanguages.LocaleIds;
        var initialLanguage = XrmTranslator.userSettings.uilanguageid;

        return WebApiClient.Retrieve(queryRequest)
            .then(function(response) {
                var views = response.value;
                var requests = [];

                for (var i = 0; i < views.length; i++) {
                    var view = views[i];

                    var retrieveNameLabelsRequest = WebApiClient.Requests.RetrieveLocLabelsRequest
                        .with({
                            urlParams: {
                                EntityMoniker: "{'@odata.id':'savedqueries(" + view.savedqueryid + ")'}",
                                AttributeName: "'name'",
                                IncludeUnpublished: true
                            }
                        })

                    var retrieveDescriptionLabelsRequest = WebApiClient.Requests.RetrieveLocLabelsRequest
                        .with({
                            urlParams: {
                                EntityMoniker: "{'@odata.id':'savedqueries(" + view.savedqueryid + ")'}",
                                AttributeName: "'description'",
                                IncludeUnpublished: true
                            }
                        })

                    var prop = WebApiClient.Promise.props({
                        recid: view.savedqueryid,
                        querytype: view.querytype,
                        labels: WebApiClient.Promise.props({
                            name: WebApiClient.Execute(retrieveNameLabelsRequest),
                            description: WebApiClient.Execute(retrieveDescriptionLabelsRequest)
                        })
                    });

                    requests.push(prop);
                }

                return WebApiClient.Promise.all(requests);
            })
            .then(function(responses) {
                    var views = responses;
                    XrmTranslator.metadata = views;

                    FillTable();
            })
            .catch(XrmTranslator.errorHandler);
    }

    ViewHandler.SaveOnly = function() {
        var updates = GetUpdates();
        return XrmTranslator.ExecuteChangeSetBatches(updates, {
            progressLabel: "Saving 4. Views",
            batchNamePrefix: "batch_setviewlabels",
            changeSetNamePrefix: "changeset_setviewlabels",
            buildRequest: function(update) {
                return new WebApiClient.BatchRequest({
                    method: "POST",
                    url: WebApiClient.GetApiUrl() + "SetLocLabels",
                    payload: {
                        Labels: update.labels.Label.LocalizedLabels,
                        EntityMoniker: {
                            "@odata.type": "Microsoft.Dynamics.CRM.savedquery",
                            savedqueryid: update.recid
                        },
                        AttributeName: update.attributeName
                    }
                });
            }
        })
            .then(function () {
                var viewIds = [];
                for (var i = 0; i < updates.length; i++) {
                    if (viewIds.indexOf(updates[i].recid) === -1) {
                        viewIds.push(updates[i].recid);
                    }
                }

                return XrmTranslator.AddToSolution(viewIds, XrmTranslator.ComponentType.SavedQuery);
            });
    }

    ViewHandler.Save = function() {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                return ViewHandler.SaveOnly();
            },
            publishAction: function () {
                return XrmTranslator.Publish();
            },
            reloadAction: function () {
                return ViewHandler.Load();
            }
        });
    }
} (window.ViewHandler = window.ViewHandler || {}));
