(function (RibbonHandler, undefined) {
    "use strict";

    var idSeparator = "|";
    var translatableAttributes = ["LabelText", "ToolTipTitle", "ToolTipDescription"];
    var propertyDisplayNames = {
        LabelText: "Text",
        ToolTipTitle: "Title",
        ToolTipDescription: "Description"
    };
    var ribbonState = null;
    var ribbonLoadReleaseTimer = null;
    var ribbonLoadStatusSuppressed = false;

    function getSelectedSolution() {
        var solutionId = XrmTranslator.GetSolution();

        if (!solutionId || solutionId === "all") {
            return Promise.reject(new Error("Please select a solution before loading ribbon labels."));
        }

        return WebApiClient.Retrieve({
            entityName: "solution",
            entityId: solutionId,
            queryParams: "?$select=solutionid,uniquename,friendlyname,version"
        })
        .then(function (solution) {
            if (solution) {
                return solution;
            }

            throw new Error("Selected solution was not found.");
        });
    }

    function getSelectedEntityInfo() {
        var selected = XrmTranslator.GetEntity();
        var entities = XrmTranslator.allEntities || [];

        for (var i = 0; i < entities.length; i++) {
            if (entities[i].SchemaName === selected || entities[i].LogicalName === selected) {
                return {
                    schemaName: entities[i].SchemaName,
                    logicalName: entities[i].LogicalName,
                    metadataId: entities[i].MetadataId
                };
            }
        }

        return {
            schemaName: selected,
            logicalName: String(selected || "").toLowerCase(),
            metadataId: XrmTranslator.GetEntityId()
        };
    }

    function showRibbonLoadStatus(message, tone, autoHideMs) {
        if (ribbonLoadStatusSuppressed) {
            return;
        }

        XrmTranslator.ShowStatusBanner({
            tone: tone || "info",
            message: message,
            autoHideMs: autoHideMs || 0
        });
    }

    function setRibbonLoadButtonsDisabled(disabled, saveDisabled) {
        XrmTranslator.SetLoadButtonDisabled(!!disabled);
        XrmTranslator.SetSaveButtonDisabled(typeof saveDisabled === "boolean" ? saveDisabled : !!disabled);
    }

    function scheduleRibbonLoadButtonsRelease(saveDisabled, delayMs) {
        if (ribbonLoadReleaseTimer) {
            clearTimeout(ribbonLoadReleaseTimer);
            ribbonLoadReleaseTimer = null;
        }

        ribbonLoadReleaseTimer = setTimeout(function() {
            ribbonLoadReleaseTimer = null;
            setRibbonLoadButtonsDisabled(false, !!saveDisabled);
        }, delayMs || 0);
    }

    function exportSelectedSolution(solution) {
        showRibbonLoadStatus("Exporting selected solution " + solution.uniquename + ".");

        return WebApiClient.Execute(WebApiClient.Requests.ExportSolutionRequest.with({
            payload: {
                SolutionName: solution.uniquename,
                Managed: false
            }
        }));
    }

    function parseXml(xmlText) {
        var doc = new DOMParser().parseFromString(xmlText, "text/xml");

        if (doc.getElementsByTagName("parsererror").length > 0) {
            throw new Error("Invalid XML returned from solution export.");
        }

        return doc;
    }

    function getDirectChild(parent, tagName) {
        if (!parent) {
            return null;
        }

        for (var i = 0; i < parent.childNodes.length; i++) {
            var child = parent.childNodes[i];
            if (child.nodeType === 1 && child.nodeName === tagName) {
                return child;
            }
        }

        return null;
    }

    function getElementText(parent, tagName) {
        var child = getDirectChild(parent, tagName);
        return child ? String(child.textContent || "") : "";
    }

    function findEntityNode(customizationsDoc, logicalName) {
        var entities = customizationsDoc.getElementsByTagName("Entity");
        var expected = String(logicalName || "").toLowerCase();

        for (var i = 0; i < entities.length; i++) {
            if (getElementText(entities[i], "Name").toLowerCase() === expected) {
                return entities[i];
            }
        }

        return null;
    }

    function getAttributeInsensitive(node, name) {
        return node.getAttribute(name) || node.getAttribute(name.toLowerCase()) || node.getAttribute(name.toUpperCase());
    }

    function buildLocLabelMap(ribbonDiffXml) {
        var map = {};
        var labels = ribbonDiffXml ? ribbonDiffXml.getElementsByTagName("LocLabel") : [];

        for (var i = 0; i < labels.length; i++) {
            var locLabel = labels[i];
            var id = locLabel.getAttribute("Id");
            if (!id) {
                continue;
            }

            map[id] = map[id] || {};
            var titles = locLabel.getElementsByTagName("Title");
            for (var t = 0; t < titles.length; t++) {
                var language = getAttributeInsensitive(titles[t], "languagecode");
                if (language) {
                    map[id][String(language)] = titles[t].getAttribute("description") || "";
                }
            }
        }

        return map;
    }

    function findAncestor(node, tagName) {
        var current = node ? node.parentNode : null;

        while (current && current.nodeType === 1) {
            if (current.nodeName === tagName) {
                return current;
            }
            current = current.parentNode;
        }

        return null;
    }

    function detectSurface(controlNode) {
        var customAction = findAncestor(controlNode, "CustomAction");
        var location = customAction ? customAction.getAttribute("Location") || "" : "";

        if (location.indexOf("HomepageGrid") !== -1) {
            return "main_grid";
        }
        if (location.indexOf("SubGrid") !== -1) {
            return "sub_grid";
        }
        if (location.indexOf("Form") !== -1) {
            return "form";
        }

        return "";
    }

    function getSurfaceLabel(surface) {
        if (surface === "main_grid") {
            return "Homepage Grid";
        }
        if (surface === "sub_grid") {
            return "Subgrid";
        }
        if (surface === "form") {
            return "Form";
        }

        return "Ribbon";
    }

    function isControlNode(node) {
        return node && (node.nodeName === "Button" || node.nodeName === "SplitButton" || node.nodeName === "FlyoutAnchor");
    }

    function getLocLabelId(controlNode, property) {
        var value = controlNode.getAttribute(property);

        if (!value || value.indexOf("$LocLabels:") !== 0) {
            return null;
        }

        return value.substring("$LocLabels:".length);
    }

    function getDefaultLocLabelId(controlId, property) {
        return controlId ? controlId + "." + property : property;
    }

    function getPreferredLanguageIds() {
        var languages = [];

        if (XrmTranslator.baseLanguage) {
            languages.push(XrmTranslator.baseLanguage.toString());
        }

        if (XrmTranslator.userSettings && XrmTranslator.userSettings.uilanguageid) {
            languages.push(XrmTranslator.userSettings.uilanguageid.toString());
        }

        for (var i = 0; i < XrmTranslator.installedLanguages.LocaleIds.length; i++) {
            languages.push(XrmTranslator.installedLanguages.LocaleIds[i].toString());
        }

        return languages.filter(function (language, index) {
            return language && languages.indexOf(language) === index;
        });
    }

    function getFirstLabel(labels) {
        labels = labels || {};

        var preferredLanguages = getPreferredLanguageIds();
        for (var i = 0; i < preferredLanguages.length; i++) {
            if (labels[preferredLanguages[i]]) {
                return labels[preferredLanguages[i]];
            }
        }

        var keys = Object.keys(labels);
        return keys.length > 0 ? labels[keys[0]] : "";
    }

    function getControlPreviewLabel(controlNode, locLabels) {
        var labelLocId = getLocLabelId(controlNode, "LabelText");
        var previewLabel = labelLocId ? getFirstLabel(locLabels[labelLocId]) : "";

        if (previewLabel) {
            return previewLabel;
        }

        var controlId = controlNode.getAttribute("Id") || "";
        var parts = controlId.split(".");
        return parts.length > 0 ? parts[parts.length - 1] : controlId;
    }

    function getControlNodeText(surfaceLabel, controlNode, previewLabel) {
        var controlType = controlNode.nodeName;

        if (previewLabel) {
            return surfaceLabel + " / " + controlType + ": " + previewLabel;
        }

        return surfaceLabel + " / " + controlType;
    }

    function appendControlRecords(records, seen, controlNode, entityInfo, locLabels, controlIndex) {
        var controlId = controlNode.getAttribute("Id") || "";
        var commandId = controlNode.getAttribute("Command") || "";
        var surface = detectSurface(controlNode);
        var surfaceLabel = getSurfaceLabel(surface);
        var children = [];

        for (var i = 0; i < translatableAttributes.length; i++) {
            var property = translatableAttributes[i];
            var existingLocLabelId = getLocLabelId(controlNode, property);
            var locLabelId = existingLocLabelId || getDefaultLocLabelId(controlId, property);

            var dedupeKey = [surface, controlId, property, locLabelId].join(idSeparator);
            if (seen[dedupeKey]) {
                continue;
            }
            seen[dedupeKey] = true;

            var row = {
                recid: "ribbon-control-" + controlIndex + "-label-" + children.length,
                schemaName: propertyDisplayNames[property] || property,
                entityLogicalName: entityInfo.logicalName,
                component: controlNode.nodeName,
                surface: surface,
                controlId: controlId,
                commandId: commandId,
                property: property,
                locLabelId: locLabelId,
                locLabelExists: !!existingLocLabelId,
                _isRibbonLabelRow: true
            };

            var labels = locLabels[locLabelId] || {};
            for (var l = 0; l < XrmTranslator.installedLanguages.LocaleIds.length; l++) {
                var language = XrmTranslator.installedLanguages.LocaleIds[l].toString();
                row[language] = Object.prototype.hasOwnProperty.call(labels, language) ? labels[language] : null;
            }

            children.push(row);
        }

        if (children.length > 0) {
            records.push({
                recid: "ribbon-control-" + controlIndex,
                schemaName: getControlNodeText(surfaceLabel, controlNode, getControlPreviewLabel(controlNode, locLabels)),
                entityLogicalName: entityInfo.logicalName,
                component: controlNode.nodeName,
                surface: surface,
                controlId: controlId,
                commandId: commandId,
                _isRibbonControlNode: true,
                w2ui: {
                    editable: false,
                    expanded: false,
                    children: children
                }
            });
        }
    }

    function buildRecordsFromRibbon(ribbonDiffXml, entityInfo) {
        var records = [];
        var seen = {};
        var locLabels = buildLocLabelMap(ribbonDiffXml);
        var allNodes = ribbonDiffXml ? ribbonDiffXml.getElementsByTagName("*") : [];
        var controlIndex = 0;

        for (var i = 0; i < allNodes.length; i++) {
            if (isControlNode(allNodes[i])) {
                appendControlRecords(records, seen, allNodes[i], entityInfo, locLabels, controlIndex);
                controlIndex++;
            }
        }

        records.sort(function (a, b) {
            return (a.schemaName + a.locLabelId).localeCompare(b.schemaName + b.locLabelId);
        });

        return records;
    }

    function readCustomizationsFromExport(exportResponse) {
        if (!window.JSZip) {
            throw new Error("JSZip is not loaded. Ensure js/lib/jszip.min.js is included before RibbonHandler.js.");
        }

        if (!exportResponse || !exportResponse.ExportSolutionFile) {
            throw new Error("ExportSolution did not return ExportSolutionFile.");
        }

        showRibbonLoadStatus("Reading exported ribbon solution.");

        return JSZip.loadAsync(exportResponse.ExportSolutionFile, { base64: true })
        .then(function (zip) {
            var entry = zip.file("customizations.xml");
            if (!entry) {
                throw new Error("Exported solution does not contain customizations.xml.");
            }

            return entry.async("string")
            .then(function (customizationsXml) {
                return {
                    zip: zip,
                    customizationsXml: customizationsXml,
                    solutionZipBase64: exportResponse.ExportSolutionFile
                };
            });
        });
    }

    function normalizeSavedLabelValue(value) {
        return value == null ? "" : String(value);
    }

    function createGuid() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function(c) {
            var r = Math.random() * 16 | 0;
            var v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function collectRibbonChanges() {
        var records = XrmTranslator.GetAllRecords();
        var languageIds = XrmTranslator.installedLanguages.LocaleIds.map(function(language) {
            return language.toString();
        });
        var changes = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record || !record.property || !record.controlId || !record.w2ui || !record.w2ui.changes) {
                continue;
            }

            for (var l = 0; l < languageIds.length; l++) {
                var language = languageIds[l];
                if (!Object.prototype.hasOwnProperty.call(record.w2ui.changes, language)) {
                    continue;
                }

                changes.push({
                    record: record,
                    entityLogicalName: record.entityLogicalName,
                    component: record.component,
                    surface: record.surface,
                    controlId: record.controlId,
                    property: record.property,
                    locLabelId: record.locLabelId || getDefaultLocLabelId(record.controlId, record.property),
                    language: language,
                    value: normalizeSavedLabelValue(record.w2ui.changes[language])
                });
            }
        }

        return changes;
    }

    function findControlNodeByChange(ribbonDiffXml, change) {
        var nodes = ribbonDiffXml ? ribbonDiffXml.getElementsByTagName(change.component || "*") : [];

        for (var i = 0; i < nodes.length; i++) {
            if (!isControlNode(nodes[i])) {
                continue;
            }

            if ((nodes[i].getAttribute("Id") || "") !== change.controlId) {
                continue;
            }

            if (change.surface && detectSurface(nodes[i]) !== change.surface) {
                continue;
            }

            return nodes[i];
        }

        var allNodes = ribbonDiffXml ? ribbonDiffXml.getElementsByTagName("*") : [];
        for (var a = 0; a < allNodes.length; a++) {
            if (isControlNode(allNodes[a]) && (allNodes[a].getAttribute("Id") || "") === change.controlId) {
                return allNodes[a];
            }
        }

        return null;
    }

    function getOrCreateDirectChild(doc, parent, tagName) {
        var child = getDirectChild(parent, tagName);
        if (child) {
            return child;
        }

        child = doc.createElement(tagName);
        parent.appendChild(child);
        return child;
    }

    function getOrCreateLocLabelsNode(doc, ribbonDiffXml) {
        return getOrCreateDirectChild(doc, ribbonDiffXml, "LocLabels");
    }

    function getOrCreateLocLabel(doc, locLabelsNode, locLabelId) {
        var labels = locLabelsNode.getElementsByTagName("LocLabel");

        for (var i = 0; i < labels.length; i++) {
            if (labels[i].getAttribute("Id") === locLabelId) {
                return labels[i];
            }
        }

        var locLabel = doc.createElement("LocLabel");
        locLabel.setAttribute("Id", locLabelId);
        locLabelsNode.appendChild(locLabel);
        return locLabel;
    }

    function getOrCreateTitle(doc, titlesNode, language) {
        var titles = titlesNode.getElementsByTagName("Title");

        for (var i = 0; i < titles.length; i++) {
            if (String(getAttributeInsensitive(titles[i], "languagecode")) === String(language)) {
                return titles[i];
            }
        }

        var title = doc.createElement("Title");
        title.setAttribute("languagecode", language);
        titlesNode.appendChild(title);
        return title;
    }

    function applyRibbonXmlChanges(customizationsXml, changes) {
        var customizationsDoc = parseXml(customizationsXml);
        var entityLogicalName = ribbonState.entityInfo.logicalName;
        var entityNode = findEntityNode(customizationsDoc, entityLogicalName);
        if (!entityNode) {
            throw new Error("Entity " + entityLogicalName + " was not found in customizations.xml.");
        }

        var ribbonDiffXml = getDirectChild(entityNode, "RibbonDiffXml");
        if (!ribbonDiffXml) {
            throw new Error("RibbonDiffXml was not found for entity " + entityLogicalName + ".");
        }

        var locLabelsNode = getOrCreateLocLabelsNode(customizationsDoc, ribbonDiffXml);

        for (var i = 0; i < changes.length; i++) {
            var change = changes[i];
            var controlNode = findControlNodeByChange(ribbonDiffXml, change);
            if (!controlNode) {
                throw new Error("Ribbon control " + change.controlId + " was not found in customizations.xml.");
            }

            if (getLocLabelId(controlNode, change.property) !== change.locLabelId) {
                controlNode.setAttribute(change.property, "$LocLabels:" + change.locLabelId);
            }

            var locLabel = getOrCreateLocLabel(customizationsDoc, locLabelsNode, change.locLabelId);
            var titlesNode = getOrCreateDirectChild(customizationsDoc, locLabel, "Titles");
            var title = getOrCreateTitle(customizationsDoc, titlesNode, change.language);
            title.setAttribute("description", change.value);
        }

        return new XMLSerializer().serializeToString(customizationsDoc);
    }

    function getBackupFileName(entityLogicalName) {
        var stamp = new Date().toISOString()
            .replace(/[-:]/g, "")
            .replace(/\..+$/, "")
            .replace("T", "-");

        return "ribbon-backup-" + entityLogicalName + "-" + stamp + ".zip";
    }

    function prepareBackupArtifact(changes) {
        var backupZip = new JSZip();
        var fileName = getBackupFileName(ribbonState.entityInfo.logicalName);
        var changedLocLabels = {};

        for (var i = 0; i < changes.length; i++) {
            changedLocLabels[changes[i].locLabelId] = true;
        }

        backupZip.file(ribbonState.solutionUniqueName + "-original.zip", ribbonState.solutionZipBase64, { base64: true });
        backupZip.file("customizations.xml", ribbonState.customizationsXml);
        backupZip.file("backup-info.json", JSON.stringify({
            entityLogicalName: ribbonState.entityInfo.logicalName,
            entitySchemaName: ribbonState.entityInfo.schemaName,
            solutionUniqueName: ribbonState.solutionUniqueName,
            timestamp: new Date().toISOString(),
            changedCellCount: changes.length,
            locLabelIds: Object.keys(changedLocLabels)
        }, null, 2));

        return backupZip.generateAsync({ type: "blob" })
        .then(function(blob) {
            return {
                fileName: fileName,
                blob: blob
            };
        });
    }

    function downloadBackupArtifact(backupArtifact) {
        if (!backupArtifact || !backupArtifact.blob || !backupArtifact.fileName || !window.URL || !URL.createObjectURL) {
            throw new Error("Ribbon backup download could not be prepared.");
        }

        var url = URL.createObjectURL(backupArtifact.blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = backupArtifact.fileName;
        link.style.display = "none";

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        setTimeout(function() {
            URL.revokeObjectURL(url);
        }, 0);
    }

    function askRibbonBackupDecision(backupArtifact) {
        return DialogHelper.question(
            "Before saving ribbon labels, choose how to continue.\n\n" +
            "- Download backup and save: download the current entity ribbon backup first, then continue Save.\n" +
            "- Save without backup: no backup file will be downloaded. Use at your own risk.\n" +
            "- Cancel: stop Save and keep current grid changes.\n\n" +
            "Backup file prepared: " + backupArtifact.fileName,
            [
                { text: "Download backup and save", value: "download" },
                { text: "Save without backup", value: "skip" },
                { text: "Cancel", value: "cancel" }
            ],
            {
                title: "Ribbon backup before save",
                width: 760,
                height: 380
            }
        );
    }

    function createImportSolutionBase64(updatedCustomizationsXml) {
        return JSZip.loadAsync(ribbonState.solutionZipBase64, { base64: true })
        .then(function(zip) {
            zip.file("customizations.xml", updatedCustomizationsXml);
            return zip.generateAsync({ type: "base64" })
            .then(function(importZipBase64) {
                return {
                    zip: zip,
                    base64: importZipBase64
                };
            });
        });
    }

    function importRibbonSolution(importZipBase64) {
        var importJobId = createGuid();
        var toolbarType = XrmTranslator.GetTypeStateLabel("ribbons");

        XrmTranslator.StartOperationStatus({
            phase: "importing",
            tone: "info",
            message: "Publishing " + toolbarType,
            type: "ribbons",
            toolbarType: toolbarType,
            entityLogicalName: ribbonState.entityInfo.logicalName,
            importJobId: importJobId,
            operationId: importJobId,
            blockSave: true,
            blockLoad: true
        });

        return XrmTranslator.RunAsBaseLanguage(function () {
            return WebApiClient.SendRequest("POST", WebApiClient.GetApiUrl({ apiVersion: "9.2" }) + "ImportSolution()", {
                CustomizationFile: importZipBase64,
                ImportJobId: importJobId,
                OverwriteUnmanagedCustomizations: true,
                PublishWorkflows: false
            });
        })
        .then(function(response) {
            XrmTranslator.UpdateOperationStatus({
                phase: "startingPublish",
                tone: "info",
                message: "Publishing " + toolbarType,
                type: "ribbons",
                toolbarType: toolbarType,
                entityLogicalName: ribbonState.entityInfo.logicalName,
                importJobId: importJobId,
                operationId: importJobId,
                blockSave: true,
                blockLoad: true
            });

            return response;
        });
    }

    function extractPublishJobId(response) {
        if (!response) {
            return null;
        }

        if (typeof response === "string") {
            var stringMatch = response.match(/[0-9a-fA-F-]{36}/);
            return stringMatch ? stringMatch[0] : null;
        }

        var candidates = [
            response.AsyncOperationId,
            response.asyncoperationid,
            response.AsyncOperationID
        ];

        for (var i = 0; i < candidates.length; i++) {
            if (candidates[i]) {
                return String(candidates[i]).replace(/[{}]/g, "");
            }
        }

        var text = JSON.stringify(response);
        var match = text.match(/[0-9a-fA-F-]{36}/);
        return match ? match[0] : null;
    }

    function publishAllXmlAsync() {
        var toolbarType = XrmTranslator.GetTypeStateLabel("ribbons");
        XrmTranslator.UpdateOperationStatus({
            phase: "startingPublish",
            tone: "info",
            message: "Publishing " + toolbarType,
            type: "ribbons",
            toolbarType: toolbarType,
            entityLogicalName: ribbonState.entityInfo.logicalName,
            blockSave: true,
            blockLoad: true
        });

        return XrmTranslator.RunAsBaseLanguage(function () {
            return WebApiClient.SendRequest("POST", WebApiClient.GetApiUrl({ apiVersion: "9.2" }) + "PublishAllXmlAsync()", null);
        })
        .then(function(response) {
            var jobId = extractPublishJobId(response);
            if (!jobId) {
                throw new Error("PublishAllXmlAsync did not return AsyncOperationId.");
            }

            return XrmTranslator.StorePublishXmlJob({
                jobId: jobId,
                operation: "PublishAllXmlAsync",
                type: "ribbons",
                toolbarType: toolbarType,
                entityLogicalName: ribbonState.entityInfo.logicalName
            });
        });
    }

    function commitRibbonGridChanges(changes) {
        var refreshed = {};

        for (var i = 0; i < changes.length; i++) {
            var change = changes[i];
            change.record[change.language] = change.value;

            if (change.record.w2ui && change.record.w2ui.changes) {
                delete change.record.w2ui.changes[change.language];
                if (Object.keys(change.record.w2ui.changes).length === 0) {
                    delete change.record.w2ui.changes;
                }
            }

            refreshed[change.record.recid] = true;
        }

        var grid = XrmTranslator.GetGrid();
        Object.keys(refreshed).forEach(function(recid) {
            grid.refreshRow(recid);
        });
        XrmTranslator.SetSaveButtonDisabled(true);
    }

    function showPublishStartedMessage(job) {
        XrmTranslator.UnlockGrid();
        XrmTranslator.ShowPublishXmlStatus(job);
        return Promise.resolve();
    }

    function fillTable(records) {
        var grid = XrmTranslator.GetGrid();
        grid.clear();
        XrmTranslator.AddSummary(records);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    RibbonHandler.Load = function () {
        var entityInfo = getSelectedEntityInfo();

        if (!entityInfo.metadataId) {
            return Promise.reject(new Error("Selected entity metadata id was not found."));
        }

        var exported = null;
        var errorBannerMs = 8000;

        ribbonLoadStatusSuppressed = true;
        setRibbonLoadButtonsDisabled(true, true);

        return getSelectedSolution()
        .then(function (solution) {
            return exportSelectedSolution(solution)
            .then(function (exportResponse) {
                return {
                    solution: solution,
                    exportResponse: exportResponse
                };
            });
        })
        .then(function (context) {
            return readCustomizationsFromExport(context.exportResponse)
            .then(function (exportData) {
                exportData.solution = context.solution;
                return exportData;
            });
        })
        .then(function (exportData) {
            exported = exportData;
            showRibbonLoadStatus("Parsing ribbon labels.");

            var customizationsDoc = parseXml(exportData.customizationsXml);
            var entityNode = findEntityNode(customizationsDoc, entityInfo.logicalName);
            if (!entityNode) {
                throw new Error("Entity " + entityInfo.logicalName + " was not found in exported customizations.xml.");
            }

            var ribbonDiffXml = getDirectChild(entityNode, "RibbonDiffXml");
            var records = buildRecordsFromRibbon(ribbonDiffXml, entityInfo);

            ribbonState = {
                entityInfo: entityInfo,
                solutionUniqueName: exported.solution.uniquename,
                zip: exported.zip,
                customizationsXml: exported.customizationsXml,
                solutionZipBase64: exported.solutionZipBase64,
                recordCount: records.length
            };
            XrmTranslator.metadata = records;
            fillTable(records);
            ribbonLoadStatusSuppressed = false;
            XrmTranslator.HideStatusBanner();
            XrmTranslator.EnableLoadAndSave();
        })
        .catch(function (error) {
            ribbonLoadStatusSuppressed = false;
            XrmTranslator.ShowStatusBanner({
                tone: "error",
                message: "Ribbon labels failed to load.",
                autoHideMs: errorBannerMs
            });
            XrmTranslator.EnableLoadAndSave();
            XrmTranslator.errorHandler(error);
            XrmTranslator.GetGrid().unlock();
        });
    };

    RibbonHandler.Save = function () {
        if (!ribbonState || !ribbonState.customizationsXml || !ribbonState.solutionZipBase64) {
            return DialogHelper.alert("Please load ribbon labels before saving.", {
                title: "Ribbons"
            });
        }

        var changes = collectRibbonChanges();
        if (changes.length === 0) {
            XrmTranslator.EnableLoadAndSave();
            return DialogHelper.alert("There are no ribbon changes to save.", {
                title: "Ribbons"
            });
        }

        var backupArtifact = null;
        var updatedCustomizationsXml = null;
        var importData = null;

        var toolbarType = XrmTranslator.GetTypeStateLabel("ribbons");
        XrmTranslator.LockGrid("Saving " + toolbarType);

        return prepareBackupArtifact(changes)
        .then(function(preparedBackup) {
            backupArtifact = preparedBackup;
            XrmTranslator.UnlockGrid();
            return askRibbonBackupDecision(backupArtifact);
        })
        .then(function(backupDecision) {
            if (backupDecision === "cancel" || !backupDecision) {
                XrmTranslator.HideStatusBanner();
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                return false;
            }

            if (backupDecision === "download") {
                downloadBackupArtifact(backupArtifact);
            }

            XrmTranslator.StartOperationStatus({
                phase: "publishing",
                tone: "info",
                message: "Publishing " + toolbarType,
                type: "ribbons",
                toolbarType: toolbarType,
                entityLogicalName: ribbonState.entityInfo.logicalName,
                blockSave: true,
                blockLoad: true
            });
            XrmTranslator.LockGrid("Publishing " + toolbarType);
            updatedCustomizationsXml = applyRibbonXmlChanges(ribbonState.customizationsXml, changes);
            return createImportSolutionBase64(updatedCustomizationsXml);
        })
        .then(function(createdImportData) {
            if (!createdImportData) {
                XrmTranslator.ClearOperationStatus();
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                return null;
            }

            importData = createdImportData;
            return importRibbonSolution(importData.base64);
        })
        .then(function() {
            if (!importData) {
                return null;
            }

            return publishAllXmlAsync();
        })
        .then(function(job) {
            if (!job) {
                return null;
            }

            ribbonState.customizationsXml = updatedCustomizationsXml;
            ribbonState.solutionZipBase64 = importData.base64;
            ribbonState.zip = importData.zip;

            commitRibbonGridChanges(changes);
            return showPublishStartedMessage(job);
        })
        .catch(function(error) {
            XrmTranslator.ClearOperationStatus({
                status: {
                    tone: "error",
                    message: "Ribbon save failed. Save and Load are available.",
                    autoHideMs: 8000
                }
            });
            XrmTranslator.UnlockGrid();
            XrmTranslator.EnableLoadAndSave();
            XrmTranslator.errorHandler(error);
        });
    };

    RibbonHandler.GetState = function () {
        return ribbonState;
    };

}(window.RibbonHandler = window.RibbonHandler || {}));
