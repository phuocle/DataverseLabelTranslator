(function (BusinessRuleHandler, undefined) {
    "use strict";

    var businessRuleData = [];
    var attributeDisplayNames = {};
    var idSeparator = "|";

    function escapeODataString(value) {
        return String(value || "").replace(/'/g, "''");
    }

    function escapeRegex(value) {
        return String(value || "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    }

    function decodeXmlAttribute(value) {
        var text = String(value || "");

        for (var i = 0; i < 3; i++) {
            var decoded = text
                .replace(/&#x([0-9a-f]+);/gi, function(match, code) {
                    return String.fromCharCode(parseInt(code, 16));
                })
                .replace(/&#(\d+);/g, function(match, code) {
                    return String.fromCharCode(parseInt(code, 10));
                })
                .replace(/&quot;/g, "\"")
                .replace(/&apos;/g, "'")
                .replace(/&lt;/g, "<")
                .replace(/&gt;/g, ">")
                .replace(/&amp;/g, "&");

            if (decoded === text) {
                break;
            }

            text = decoded;
        }

        return text;
    }

    function encodeXmlAttribute(value) {
        var text = value === null || typeof value === "undefined" ? "" : String(value);

        if (typeof w2utils !== "undefined" && w2utils.decodeTags) {
            text = w2utils.decodeTags(text);
        }

        return text
            .replace(/&/g, "&amp;")
            .replace(/"/g, "&quot;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    function parseAttributes(tag) {
        var attributes = {};
        var regex = /([\w:]+)\s*=\s*(["'])(.*?)\2/g;
        var match;

        while ((match = regex.exec(tag)) !== null) {
            attributes[match[1]] = decodeXmlAttribute(match[3]);
        }

        return attributes;
    }

    function getActiveSetMessageTag(xaml, index) {
        var before = xaml.substring(0, index);
        var setMessageRegex = /<mcwc:SetMessage(?!\.)\b[^>]*>/g;
        var match;
        var lastMatch = null;

        while ((match = setMessageRegex.exec(before)) !== null) {
            lastMatch = {
                index: match.index,
                tag: match[0]
            };
        }

        var openIdx = lastMatch ? lastMatch.index : -1;
        var closeIdx = before.lastIndexOf("</mcwc:SetMessage>");

        if (openIdx === -1 || openIdx < closeIdx) {
            return null;
        }

        return lastMatch.tag;
    }

    function isInsideElement(xaml, index, openTag, closeTag) {
        var before = xaml.substring(0, index);
        var openIdx = before.lastIndexOf(openTag);
        var closeIdx = before.lastIndexOf(closeTag);

        return openIdx !== -1 && openIdx > closeIdx;
    }

    function getLabelContext(xaml, index) {
        var setMessageTag = getActiveSetMessageTag(xaml, index);
        var setMessageAttributes = setMessageTag ? parseAttributes(setMessageTag) : {};
        var controlId = setMessageAttributes.ControlId || "";
        var level = String(setMessageAttributes.Level || "").toUpperCase();

        if (isInsideElement(xaml, index, "<mcwc:SetMessage.StepLabels>", "</mcwc:SetMessage.StepLabels>")) {
            return {
                kind: level === "RECOMMENDATION" ? "Recommendation title" : "Error message",
                sourceField: controlId
            };
        }

        if (isInsideElement(xaml, index, 'x:Key="StepLabels"', "</sco:Collection>")) {
            return {
                kind: "Recommendation detail",
                sourceField: controlId
            };
        }

        return {
            kind: "Business rule label",
            sourceField: controlId
        };
    }

    function getLabelRowName(label) {
        var location = getFieldDisplayName(label.sourceField);
        var kind = label.kind || "Business rule label";

        if (location) {
            return location + " / " + kind;
        }

        return kind;
    }

    function getKindSortOrder(kind) {
        var normalized = String(kind || "").toLowerCase();

        if (normalized === "error message") {
            return 1;
        }

        if (normalized === "recommendation title") {
            return 2;
        }

        if (normalized === "recommendation detail") {
            return 3;
        }

        return 99;
    }

    function getFieldDisplayName(logicalName) {
        var key = String(logicalName || "").toLowerCase();

        if (!key) {
            return "";
        }

        return attributeDisplayNames[key] || logicalName;
    }

    function parseStepLabels(workflow) {
        var xaml = workflow.xaml || "";
        var labelsById = {};
        var orderedLabels = [];
        var regex = /<mcwo:StepLabel\b[^>]*\/>/g;
        var match;

        while ((match = regex.exec(xaml)) !== null) {
            var tag = match[0];
            var attributes = parseAttributes(tag);
            var labelId = attributes.LabelId;
            var languageCode = attributes.LanguageCode;

            if (!labelId || !languageCode) {
                continue;
            }

            if (!labelsById[labelId]) {
                var context = getLabelContext(xaml, match.index);

                labelsById[labelId] = {
                    labelId: labelId,
                    kind: context.kind,
                    sourceField: context.sourceField,
                    order: match.index,
                    labels: {}
                };

                orderedLabels.push(labelsById[labelId]);
            }

            labelsById[labelId].labels[String(languageCode)] = attributes.Description || "";
        }

        return orderedLabels.sort(function(a, b) {
            var aLocation = getFieldDisplayName(a.sourceField).toLowerCase();
            var bLocation = getFieldDisplayName(b.sourceField).toLowerCase();

            if (aLocation !== bLocation) {
                return aLocation.localeCompare(bLocation);
            }

            var aKind = getKindSortOrder(a.kind);
            var bKind = getKindSortOrder(b.kind);

            if (aKind !== bKind) {
                return aKind - bKind;
            }

            return a.order - b.order;
        });
    }

    function getLocalizedLabel(labelCollection) {
        var labels = labelCollection && labelCollection.LocalizedLabels ? labelCollection.LocalizedLabels : [];
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

    function loadAttributeDisplayNames() {
        attributeDisplayNames = {};

        return WebApiClient.Retrieve({
            entityName: "EntityDefinition",
            entityId: XrmTranslator.GetEntityId(),
            queryParams: "/Attributes?$select=LogicalName,SchemaName,DisplayName"
        })
        .then(function(response) {
            var attributes = response && response.value ? response.value : [];

            for (var i = 0; i < attributes.length; i++) {
                var attribute = attributes[i];
                var logicalName = String(attribute.LogicalName || "").toLowerCase();

                if (!logicalName) {
                    continue;
                }

                attributeDisplayNames[logicalName] =
                    getLocalizedLabel(attribute.DisplayName) ||
                    attribute.SchemaName ||
                    attribute.LogicalName;
            }

            return attributeDisplayNames;
        })
        .catch(function(error) {
            if (window.console && console.warn) {
                console.warn("BusinessRuleHandler: failed to load attribute display names.", error);
            }

            attributeDisplayNames = {};
            return attributeDisplayNames;
        });
    }

    function getStateText(workflow) {
        return workflow.statecode === 1 ? "Active" : "Draft";
    }

    function FillTable() {
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var records = [];

        for (var i = 0; i < businessRuleData.length; i++) {
            var workflow = businessRuleData[i];
            var parent = {
                recid: workflow.workflowid,
                schemaName: "Business Rule: " + workflow.name + " (" + getStateText(workflow) + ")",
                w2ui: {
                    editable: false,
                    children: []
                }
            };

            for (var j = 0; j < workflow.stepLabels.length; j++) {
                var label = workflow.stepLabels[j];
                var child = {
                    recid: workflow.workflowid + idSeparator + label.labelId,
                    schemaName: getLabelRowName(label),
                    _isBusinessRuleLabelRow: true,
                    _businessRuleId: workflow.workflowid,
                    _businessRuleName: workflow.name,
                    _labelId: label.labelId,
                    _labelKind: label.kind,
                    _sourceField: label.sourceField
                };

                var languages = Object.keys(label.labels);
                for (var l = 0; l < languages.length; l++) {
                    child[languages[l]] = label.labels[languages[l]];
                }

                parent.w2ui.children.push(child);
            }

            records.push(parent);
        }

        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    function GetUpdates(records) {
        var updatedWorkflows = {};
        records = records || XrmTranslator.GetAllRecords();

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (!record._isBusinessRuleLabelRow || !record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var changes = record.w2ui.changes;
            var workflowId = record._businessRuleId;

            if (!updatedWorkflows[workflowId]) {
                updatedWorkflows[workflowId] = {
                    workflowId: workflowId,
                    workflowName: record._businessRuleName,
                    labels: {}
                };
            }

            if (!updatedWorkflows[workflowId].labels[record._labelId]) {
                updatedWorkflows[workflowId].labels[record._labelId] = {
                    labelId: record._labelId,
                    labels: []
                };
            }

            for (var lang in changes) {
                if (!changes.hasOwnProperty(lang)) {
                    continue;
                }

                if (changes[lang] === null || typeof changes[lang] === "undefined") {
                    continue;
                }

                updatedWorkflows[workflowId].labels[record._labelId].labels.push({
                    languageCode: String(lang),
                    description: changes[lang]
                });
            }
        }

        return updatedWorkflows;
    }

    function findStepLabelTag(xaml, predicate) {
        var regex = /<mcwo:StepLabel\b[^>]*\/>/g;
        var match;

        while ((match = regex.exec(xaml)) !== null) {
            var attributes = parseAttributes(match[0]);

            if (predicate(attributes)) {
                return {
                    tag: match[0],
                    start: match.index,
                    end: match.index + match[0].length,
                    attributes: attributes
                };
            }
        }

        return null;
    }

    function updateDescriptionAttribute(tag, encodedDescription) {
        if (/Description\s*=\s*(["']).*?\1/.test(tag)) {
            return tag.replace(/Description\s*=\s*(["']).*?\1/, 'Description="' + encodedDescription + '"');
        }

        return tag.replace(/\/>$/, ' Description="' + encodedDescription + '" />');
    }

    function findLabelCollectionEnd(xaml, anchorStart) {
        var setMessageEnd = xaml.indexOf("</mcwc:SetMessage.StepLabels>", anchorStart);
        var collectionEnd = xaml.indexOf("</sco:Collection>", anchorStart);

        if (setMessageEnd === -1) {
            return collectionEnd;
        }

        if (collectionEnd === -1) {
            return setMessageEnd;
        }

        return Math.min(setMessageEnd, collectionEnd);
    }

    function upsertStepLabel(xaml, labelId, languageCode, description) {
        var encodedDescription = encodeXmlAttribute(description);
        var existing = findStepLabelTag(xaml, function(attributes) {
            return String(attributes.LabelId).toLowerCase() === String(labelId).toLowerCase()
                && String(attributes.LanguageCode) === String(languageCode);
        });

        if (existing) {
            var updatedTag = updateDescriptionAttribute(existing.tag, encodedDescription);
            return xaml.substring(0, existing.start) + updatedTag + xaml.substring(existing.end);
        }

        var anchor = findStepLabelTag(xaml, function(attributes) {
            return String(attributes.LabelId).toLowerCase() === String(labelId).toLowerCase();
        });

        if (!anchor) {
            throw new Error("Business rule label " + labelId + " was not found in workflow XAML.");
        }

        var closingIdx = findLabelCollectionEnd(xaml, anchor.start);

        if (closingIdx === -1) {
            throw new Error("Business rule label collection for " + labelId + " was not found in workflow XAML.");
        }

        var newTag = '<mcwo:StepLabel Description="' + encodedDescription + '" LabelId="' + labelId + '" LanguageCode="' + languageCode + '" />';
        return xaml.substring(0, closingIdx) + newTag + xaml.substring(closingIdx);
    }

    function ApplyXamlUpdates(xaml, labelUpdates) {
        var updatedXaml = xaml;

        for (var i = 0; i < labelUpdates.length; i++) {
            var update = labelUpdates[i];

            for (var j = 0; j < update.labels.length; j++) {
                var label = update.labels[j];
                updatedXaml = upsertStepLabel(updatedXaml, update.labelId, label.languageCode, label.description);
            }
        }

        return updatedXaml;
    }

    function DeactivateWorkflow(workflowId) {
        return WebApiClient.Update({
            entityName: "workflow",
            entityId: workflowId,
            entity: {
                statecode: 0,
                statuscode: 1
            }
        });
    }

    function ActivateWorkflow(workflowId) {
        return WebApiClient.Update({
            entityName: "workflow",
            entityId: workflowId,
            entity: {
                statecode: 1,
                statuscode: 2
            }
        });
    }

    function restoreWorkflow(workflowId, originalXaml, wasActive) {
        var rollback = WebApiClient.Promise.resolve();

        if (originalXaml) {
            rollback = rollback.then(function () {
                return WebApiClient.Update({
                    entityName: "workflow",
                    entityId: workflowId,
                    entity: {
                        xaml: originalXaml
                    }
                });
            });
        }

        if (wasActive) {
            rollback = rollback.then(function () {
                return ActivateWorkflow(workflowId);
            });
        }

        return rollback;
    }

    function getLabelUpdatesForWorkflow(workflowUpdate) {
        var result = [];
        var keys = Object.keys(workflowUpdate.labels);

        for (var i = 0; i < keys.length; i++) {
            result.push(workflowUpdate.labels[keys[i]]);
        }

        return result;
    }

    BusinessRuleHandler.Load = function () {
        var entityName = XrmTranslator.GetEntity();

        businessRuleData = [];
        XrmTranslator.metadata = [];

        return loadAttributeDisplayNames()
        .then(function () {
            return WebApiClient.Retrieve({
                entityName: "workflow",
                queryParams: "?$select=workflowid,name,primaryentity,category,statecode,statuscode,scope,clientdata,xaml" +
                    "&$filter=category eq 2 and primaryentity eq '" + escapeODataString(String(entityName || "").toLowerCase()) + "'" +
                    "&$orderby=name asc",
                returnAllPages: true
            });
        })
        .then(function (response) {
            var workflows = response && response.value ? response.value : [];

            for (var i = 0; i < workflows.length; i++) {
                var workflow = workflows[i];
                workflow.stepLabels = parseStepLabels(workflow);
                businessRuleData.push(workflow);
            }

            XrmTranslator.metadata = businessRuleData;
            FillTable();
        })
        .catch(XrmTranslator.errorHandler);
    };

    BusinessRuleHandler.SaveOnly = function (records) {
        var updatedWorkflows = GetUpdates(records);
        var workflowIds = Object.keys(updatedWorkflows);

        if (workflowIds.length === 0) {
            return WebApiClient.Promise.resolve();
        }

        var saveIndex = 0;

        return WebApiClient.Promise.resolve(workflowIds)
            .each(function (workflowId) {
                var workflowUpdate = updatedWorkflows[workflowId];
                XrmTranslator.LockGridProgress("Saving 10. Business Rules", ++saveIndex, workflowIds.length);

                var originalXaml = null;
                var wasActive = false;
                var deactivated = false;
                var latestWorkflow = null;

                return WebApiClient.Retrieve({
                    entityName: "workflow",
                    entityId: workflowId,
                    queryParams: "?$select=workflowid,name,statecode,statuscode,xaml"
                })
                .then(function (workflow) {
                    latestWorkflow = workflow;
                    originalXaml = workflow.xaml;
                    wasActive = workflow.statecode === 1;

                    if (!originalXaml) {
                        throw new Error("Business rule " + (workflow.name || workflowId) + " does not contain workflow XAML.");
                    }

                    if (wasActive) {
                        return DeactivateWorkflow(workflowId)
                        .then(function () {
                            deactivated = true;
                        });
                    }

                    return null;
                })
                .then(function () {
                    var updatedXaml = ApplyXamlUpdates(originalXaml, getLabelUpdatesForWorkflow(workflowUpdate));

                    if (updatedXaml === originalXaml) {
                        return null;
                    }

                    return WebApiClient.Update({
                        entityName: "workflow",
                        entityId: workflowId,
                        entity: {
                            xaml: updatedXaml
                        }
                    });
                })
                .then(function () {
                    if (wasActive) {
                        return ActivateWorkflow(workflowId);
                    }

                    return null;
                })
                .catch(function (error) {
                    return restoreWorkflow(workflowId, deactivated ? originalXaml : null, wasActive && deactivated)
                    .then(function () {
                        throw new Error(
                            "Failed to save Business Rule translations for " +
                            ((latestWorkflow && latestWorkflow.name) || workflowUpdate.workflowName || workflowId) +
                            ". The rule was restored to its original state.\n\nOriginal error: " +
                            (error.message || error)
                        );
                    })
                    .catch(function (rollbackError) {
                        if (rollbackError.message && rollbackError.message.indexOf("Failed to save Business Rule translations") === 0) {
                            throw rollbackError;
                        }

                        throw new Error(
                            "Failed to save Business Rule translations and failed to restore " +
                            ((latestWorkflow && latestWorkflow.name) || workflowUpdate.workflowName || workflowId) +
                            ". The rule may be left in Draft state.\n\nOriginal error: " +
                            (error.message || error) +
                            "\nRollback error: " +
                            (rollbackError.message || rollbackError)
                        );
                    });
                });
            })
            .then(function () {
                return XrmTranslator.AddToSolution(workflowIds, XrmTranslator.ComponentType.Workflow);
            });
    };

    BusinessRuleHandler.Save = function () {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                return BusinessRuleHandler.SaveOnly();
            },
            reloadAction: function () {
                return BusinessRuleHandler.Load();
            }
        });
    };

}(window.BusinessRuleHandler = window.BusinessRuleHandler || {}));
