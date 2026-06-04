(function (EntityHandler, undefined) {
    "use strict";

    function GetMetadataComponent() {
        var component = XrmTranslator.GetComponent();
        return component === "DisplayText" ? "DisplayName" : component;
    }

    function ApplyChanges(changes, labels) {
        for (var change in changes) {
            if (!changes.hasOwnProperty(change)) {
                continue;
            }

            // Skip empty labels
            if (!changes[change]) {
                continue;
            }

            for (var i = 0; i < labels.length; i++) {
                var label = labels[i];

                if (label.LanguageCode == change) {
                    label.Label = changes[change];
                    label.HasChanged = true;

                    break;
                }

                // Did not find label for this language
                if (i === labels.length - 1) {
                    labels.push({ LanguageCode: change, Label: changes[change] });
                }
            }
        }
    }

    function GetUpdates() {
        var records = XrmTranslator.GetGrid().records;

        var update = XrmTranslator.metadata;

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (record.w2ui && record.w2ui.changes) {
                var labels = null;

                if (record.schemaName === "Display Text") {
                    labels = update[GetMetadataComponent()].LocalizedLabels;
                } else if (record.schemaName === "Collection Name") {
                    labels = update.DisplayCollectionName.LocalizedLabels;
                }

                var changes = record.w2ui.changes;

                ApplyChanges(changes, labels);
            }
        }

        return update;
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var records = [];

        var entity = XrmTranslator.metadata;

        var displayNames = entity[GetMetadataComponent()].LocalizedLabels;
        var collectionNames = entity.DisplayCollectionName.LocalizedLabels;

        if (!displayNames && !collectionNames) {
            return;
        }

        var singular = {
            recid: XrmTranslator.metadata.MetadataId + "|1",
            schemaName: "Display Text"
        };

        var plural = {
            recid: XrmTranslator.metadata.MetadataId + "|2",
            schemaName: "Collection Name"
        };

        for (var i = 0; i < displayNames.length; i++) {
            var displayName = displayNames[i];

            singular[displayName.LanguageCode.toString()] = displayName.Label;
        }

        for (var j = 0; j < collectionNames.length; j++) {
            var collectionName = collectionNames[j];

            plural[collectionName.LanguageCode.toString()] = collectionName.Label;
        }

        records.push(singular);
        records.push(plural);

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    EntityHandler.Load = function () {
        var entityName = XrmTranslator.GetEntity();
        var entityMetadataId = XrmTranslator.entityMetadata[entityName];

        var request = {
            entityName: "EntityDefinition",
            entityId: entityMetadataId
        };

        return WebApiClient.Retrieve(request)
            .then(function (response) {
                XrmTranslator.metadata = response;

                FillTable();
            })
            .catch(XrmTranslator.errorHandler);
    };

    EntityHandler.SaveOnly = function () {
        var updates = GetUpdates();
        var entityUrl = WebApiClient.GetApiUrl() + "EntityDefinitions(" + XrmTranslator.GetEntityId() + ")";

        XrmTranslator.LockGridProgress("Saving 6. Entity Metadata", 1, 1);

        return WebApiClient.SendRequest("PUT", entityUrl, updates, [{ key: "MSCRM.MergeLabels", value: "true" }]);
    };

    EntityHandler.Save = function () {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                return EntityHandler.SaveOnly();
            },
            publishAction: function () {
                return XrmTranslator.Publish();
            },
            reloadAction: function () {
                return EntityHandler.Load();
            }
        });
    };
})((window.EntityHandler = window.EntityHandler || {}));
