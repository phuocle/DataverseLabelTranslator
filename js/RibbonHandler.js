(function (RibbonHandler, undefined) {
    "use strict";

    var BASE_SOLUTION_UNIQUE_NAME = "DataverseLabelTranslator";
    var DATA_SOLUTION_UNIQUE_NAME = "DataverseLabelTranslatorData";
    var HELPER_SOLUTION_DISPLAY_NAME = "translate-ribbon";
    var HELPER_SOLUTION_UNIQUE_NAME = "translate_ribbon";
    var idSeparator = "|";
    var translatableAttributes = ["LabelText", "ToolTipTitle", "ToolTipDescription"];
    var propertyDisplayNames = {
        LabelText: "Text",
        ToolTipTitle: "Title",
        ToolTipDescription: "Description"
    };
    var ribbonState = null;

    function escapeODataString(value) {
        return String(value || "").replace(/'/g, "''");
    }

    function parseGuidFromCreateResponse(createResponse) {
        if (!createResponse) {
            return null;
        }

        if (typeof createResponse === "string") {
            var match = createResponse.match(/[0-9a-fA-F-]{36}/);
            return match ? match[0] : null;
        }

        return createResponse.id || null;
    }

    function normalizePublisherPrefix(prefix) {
        var sanitized = String(prefix || "new")
            .replace(/[^A-Za-z0-9]/g, "")
            .toLowerCase();

        if (!sanitized) {
            return "new";
        }

        return sanitized.substring(0, 8);
    }

    function buildPublisherPrefixCandidates(basePrefix) {
        var primary = normalizePublisherPrefix(basePrefix);
        var secondary = normalizePublisherPrefix(primary + "d");

        if (primary === secondary) {
            return [primary];
        }

        return [primary, secondary];
    }

    function findSolution(uniqueName) {
        return WebApiClient.Retrieve({
            entityName: "solution",
            queryParams: "?$select=solutionid,uniquename,friendlyname,_publisherid_value,version&$filter=uniquename eq '" + escapeODataString(uniqueName) + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function findBaseSolution() {
        return findSolution(BASE_SOLUTION_UNIQUE_NAME)
        .then(function (solution) {
            if (!solution) {
                throw new Error("Base solution " + BASE_SOLUTION_UNIQUE_NAME + " not found.");
            }

            return solution;
        });
    }

    function getPublisherById(publisherId) {
        return WebApiClient.Retrieve({
            entityName: "publisher",
            entityId: publisherId,
            queryParams: "?$select=publisherid,uniquename,friendlyname,customizationprefix"
        });
    }

    function findPublisherByUniqueName(uniqueName) {
        return WebApiClient.Retrieve({
            entityName: "publisher",
            queryParams: "?$select=publisherid,uniquename,friendlyname,customizationprefix&$filter=uniquename eq '" + escapeODataString(uniqueName) + "'"
        })
        .then(function (response) {
            return response && response.value && response.value.length > 0 ? response.value[0] : null;
        });
    }

    function tryCreateDataPublisher(uniqueName, friendlyName, prefixCandidates, index) {
        if (index >= prefixCandidates.length) {
            throw new Error("Failed to create data publisher for ribbon helper solution.");
        }

        return WebApiClient.Create({
            entityName: "publisher",
            entity: {
                uniquename: uniqueName,
                friendlyname: friendlyName,
                customizationprefix: prefixCandidates[index]
            }
        })
        .then(function (createResponse) {
            var publisherId = parseGuidFromCreateResponse(createResponse);
            return publisherId ? getPublisherById(publisherId) : findPublisherByUniqueName(uniqueName);
        })
        .catch(function () {
            return tryCreateDataPublisher(uniqueName, friendlyName, prefixCandidates, index + 1);
        });
    }

    function ensureDataPublisher(basePublisher) {
        var dataPublisherUniqueName = (basePublisher.uniquename || BASE_SOLUTION_UNIQUE_NAME) + "Data";
        var dataPublisherFriendlyName = (basePublisher.friendlyname || BASE_SOLUTION_UNIQUE_NAME) + " Data";

        return findPublisherByUniqueName(dataPublisherUniqueName)
        .then(function (existingPublisher) {
            if (existingPublisher) {
                return existingPublisher;
            }

            return tryCreateDataPublisher(
                dataPublisherUniqueName,
                dataPublisherFriendlyName,
                buildPublisherPrefixCandidates(basePublisher.customizationprefix),
                0);
        });
    }

    function resolveRuntimePublisher() {
        return findSolution(DATA_SOLUTION_UNIQUE_NAME)
        .then(function (dataSolution) {
            if (dataSolution && dataSolution._publisherid_value) {
                return getPublisherById(dataSolution._publisherid_value);
            }

            return findBaseSolution()
            .then(function (baseSolution) {
                return getPublisherById(baseSolution._publisherid_value);
            })
            .then(ensureDataPublisher);
        });
    }

    function ensureHelperSolution() {
        return findSolution(HELPER_SOLUTION_UNIQUE_NAME)
        .then(function (solution) {
            if (solution) {
                return solution;
            }

            return resolveRuntimePublisher()
            .then(function (publisher) {
                return WebApiClient.Create({
                    entityName: "solution",
                    entity: {
                        friendlyname: HELPER_SOLUTION_DISPLAY_NAME,
                        uniquename: HELPER_SOLUTION_UNIQUE_NAME,
                        version: "1.0.0.0",
                        "publisherid@odata.bind": "/publishers(" + publisher.publisherid + ")"
                    }
                });
            })
            .then(function () {
                return findSolution(HELPER_SOLUTION_UNIQUE_NAME);
            });
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

    function getSolutionComponents(solutionId) {
        return WebApiClient.Retrieve({
            entityName: "solutioncomponent",
            queryParams: "?$select=objectid,componenttype&$filter=_solutionid_value eq " + solutionId
        })
        .then(function (response) {
            return response && response.value ? response.value : [];
        });
    }

    function removeSolutionComponent(component) {
        if (!component.objectid || component.componenttype == null) {
            return Promise.resolve(null);
        }

        return WebApiClient.Execute(WebApiClient.Requests.RemoveSolutionComponentRequest.with({
            payload: {
                SolutionComponent: {
                    solutioncomponentid: component.objectid
                },
                ComponentType: component.componenttype,
                SolutionUniqueName: HELPER_SOLUTION_UNIQUE_NAME
            }
        }));
    }

    function resetHelperSolutionToEntity(solution, entityInfo) {
        XrmTranslator.LockGrid("Preparing ribbon helper solution");

        return getSolutionComponents(solution.solutionid)
        .then(function (components) {
            var chain = Promise.resolve(null);

            for (var i = 0; i < components.length; i++) {
                (function (component) {
                    chain = chain.then(function () {
                        return removeSolutionComponent(component);
                    });
                }(components[i]));
            }

            return chain;
        })
        .then(function () {
            return WebApiClient.Execute(WebApiClient.Requests.AddSolutionComponentRequest.with({
                payload: {
                    ComponentId: entityInfo.metadataId,
                    ComponentType: XrmTranslator.ComponentType.Entity,
                    SolutionUniqueName: HELPER_SOLUTION_UNIQUE_NAME,
                    AddRequiredComponents: false,
                    IncludedComponentSettingsValues: [],
                    DoNotIncludeSubcomponents: true
                }
            }));
        });
    }

    function exportHelperSolution() {
        XrmTranslator.LockGrid("Exporting ribbon helper solution");

        return WebApiClient.Execute(WebApiClient.Requests.ExportSolutionRequest.with({
            payload: {
                SolutionName: HELPER_SOLUTION_UNIQUE_NAME,
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
                locLabelExists: !!existingLocLabelId
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
                    customizationsXml: customizationsXml
                };
            });
        });
    }

    function fillTable(records) {
        var grid = XrmTranslator.GetGrid();
        grid.clear();
        XrmTranslator.AddSummary(records);
        grid.add(records);
        XrmTranslator.SetSaveButtonDisabled(true);
        grid.unlock();
    }

    RibbonHandler.Load = function () {
        var entityInfo = getSelectedEntityInfo();

        if (!entityInfo.metadataId) {
            return Promise.reject(new Error("Selected entity metadata id was not found."));
        }

        var exported = null;

        return ensureHelperSolution()
        .then(function (solution) {
            return resetHelperSolutionToEntity(solution, entityInfo);
        })
        .then(exportHelperSolution)
        .then(function (exportResponse) {
            return readCustomizationsFromExport(exportResponse);
        })
        .then(function (exportData) {
            exported = exportData;
            XrmTranslator.LockGrid("Parsing ribbon labels");

            var customizationsDoc = parseXml(exportData.customizationsXml);
            var entityNode = findEntityNode(customizationsDoc, entityInfo.logicalName);
            if (!entityNode) {
                throw new Error("Entity " + entityInfo.logicalName + " was not found in exported customizations.xml.");
            }

            var ribbonDiffXml = getDirectChild(entityNode, "RibbonDiffXml");
            var records = buildRecordsFromRibbon(ribbonDiffXml, entityInfo);

            ribbonState = {
                entityInfo: entityInfo,
                zip: exported.zip,
                customizationsXml: exported.customizationsXml,
                recordCount: records.length
            };
            XrmTranslator.metadata = records;
            fillTable(records);
        })
        .catch(function (error) {
            XrmTranslator.errorHandler(error);
            XrmTranslator.GetGrid().unlock();
        });
    };

    RibbonHandler.Save = function () {
        return DialogHelper.alert("Ribbon save is not implemented in phase 1. This phase is read-only.", {
            title: "Ribbons"
        });
    };

    RibbonHandler.GetState = function () {
        return ribbonState;
    };

}(window.RibbonHandler = window.RibbonHandler || {}));
