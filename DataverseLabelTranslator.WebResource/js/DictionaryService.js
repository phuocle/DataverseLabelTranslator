(function (DictionaryService, undefined) {
    "use strict";

    var actionName = "Other";
    var dictionaryGridContext = null;
    var dictionaryBaselineSignature = null;
    var dictionaryAllowCloseWithoutPrompt = false;

    function getDefaultDictionaryXml() {
        return [
            '<?xml version="1.0" encoding="utf-8"?>',
            '<dictionary version="2.0" sourceLcid="">',
            "  <entries>",
            "  </entries>",
            "</dictionary>"
        ].join("\n");
    }

    function executeOther(operation, payload) {
        var input = Object.assign({ operation: operation }, payload || {});

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Other, input).then(
            function (result) {
                return Helper.GetCustomActionObject(result);
            }
        );
    }

    function runEnsureInitialized(forceRefresh) {
        return executeOther("ReadDictionary");
    }

    function ensureInitialized(forceRefresh) {
        return runEnsureInitialized(forceRefresh);
    }

    function readTagValue(entryNode, tagName) {
        var tags = entryNode.getElementsByTagName(tagName);
        if (!tags || !tags.length) {
            return "";
        }

        return tags[0].textContent || "";
    }

    function parseBoolean(value, defaultValue) {
        if (value == null || value === "") {
            return defaultValue;
        }

        var normalized = String(value).toLowerCase().trim();
        return !(normalized === "false" || normalized === "0" || normalized === "no");
    }

    function extractLanguageDisplayName(caption) {
        var text = String(caption || "").trim();
        var index = text.lastIndexOf(" (");

        if (index > 0) {
            return text.substring(0, index);
        }

        return text;
    }

    function getLanguageNameByLcid(lcid) {
        var lookup = String(lcid || "");
        var app = window.DataverseLabelTranslator;

        if (app && app.GetGrid) {
            var columns = app.GetGrid().columns || [];

            for (var i = 0; i < columns.length; i++) {
                var column = columns[i];
                if (String(column.field) === lookup) {
                    return extractLanguageDisplayName(column.text || column.caption || column.label) || lookup;
                }
            }
        }

        return lookup;
    }

    function buildTargetFieldName(lcid) {
        return "target_" + String(lcid);
    }

    function buildDictionaryGridContext() {
        return Helper.GetBaseLanguage().then(function (baseLanguage) {
            var baseLcid = String(baseLanguage);
            var localeIds = [];
            var gridColumns =
                window.DataverseLabelTranslator && DataverseLabelTranslator.GetColumns
                    ? DataverseLabelTranslator.GetColumns(false)
                    : [];

            localeIds = gridColumns
                .filter(function (field) {
                    return /^\d+$/.test(String(field));
                })
                .map(function (field) {
                    return String(field);
                });

            if (localeIds.indexOf(baseLcid) === -1) {
                localeIds.unshift(baseLcid);
            }

            var uniqueLcids = [];
            for (var i = 0; i < localeIds.length; i++) {
                if (uniqueLcids.indexOf(localeIds[i]) === -1) {
                    uniqueLcids.push(localeIds[i]);
                }
            }

            var targetLanguages = uniqueLcids
                .filter(function (lcid) {
                    return lcid !== baseLcid;
                })
                .map(function (lcid) {
                    return {
                        lcid: lcid,
                        name: getLanguageNameByLcid(lcid),
                        field: buildTargetFieldName(lcid)
                    };
                });

            return {
                baseLcid: baseLcid,
                baseName: getLanguageNameByLcid(baseLcid),
                targetLanguages: targetLanguages
            };
        });
    }

    function parseDictionaryXml(xmlContent, context) {
        var model = {
            sourceLcid: String((context && context.baseLcid) || ""),
            entries: []
        };

        if (!xmlContent) {
            return model;
        }

        try {
            var parser = new DOMParser();
            var xml = parser.parseFromString(xmlContent, "application/xml");

            if (xml.getElementsByTagName("parsererror").length > 0) {
                throw new Error("Invalid dictionary XML format.");
            }

            var dictionaryNode = xml.getElementsByTagName("dictionary")[0];
            if (dictionaryNode && dictionaryNode.getAttribute("sourceLcid")) {
                model.sourceLcid = String(dictionaryNode.getAttribute("sourceLcid"));
            }

            var entriesBySource = {};
            var entryOrder = [];
            var nodes = xml.getElementsByTagName("entry");

            function getOrCreateEntry(sourceText, isActive) {
                var key = String(sourceText);

                if (!entriesBySource[key]) {
                    entriesBySource[key] = {
                        sourceText: sourceText,
                        isActive: isActive,
                        targets: {}
                    };
                    entryOrder.push(key);
                } else {
                    entriesBySource[key].isActive = entriesBySource[key].isActive || isActive;
                }

                return entriesBySource[key];
            }

            for (var i = 0; i < nodes.length; i++) {
                var node = nodes[i];
                var sourceText = readTagValue(node, "sourceText").trim();

                if (!sourceText) {
                    continue;
                }

                var isActive = parseBoolean(
                    node.getAttribute("active"),
                    parseBoolean(readTagValue(node, "isActive"), true)
                );
                var entry = getOrCreateEntry(sourceText, isActive);

                var targetNodes = node.getElementsByTagName("target");

                if (targetNodes && targetNodes.length > 0) {
                    for (var t = 0; t < targetNodes.length; t++) {
                        var targetNode = targetNodes[t];
                        var targetLcid = String(targetNode.getAttribute("lcid") || "").trim();
                        var targetText = targetNode.textContent || "";

                        if (targetLcid) {
                            entry.targets[targetLcid] = targetText;
                        }
                    }
                } else {
                    // Backward compatibility with v1 schema (source/target LCID per row).
                    var legacySourceLcid = String(readTagValue(node, "sourceLcid") || "").trim();
                    var legacyTargetLcid = String(readTagValue(node, "targetLcid") || "").trim();
                    var legacyTargetText = readTagValue(node, "targetText");

                    if (!model.sourceLcid && legacySourceLcid) {
                        model.sourceLcid = legacySourceLcid;
                    }

                    if (legacyTargetLcid) {
                        entry.targets[legacyTargetLcid] = legacyTargetText;
                    }
                }
            }

            model.entries = entryOrder.map(function (key) {
                return entriesBySource[key];
            });

            return model;
        } catch {
            return model;
        }
    }

    function xmlEscape(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/\"/g, "&quot;")
            .replace(/'/g, "&apos;");
    }

    function serializeDictionaryXml(model) {
        var sourceLcid = String((model && model.sourceLcid) || "");
        var entries = (model && model.entries) || [];

        var xml = [
            '<?xml version="1.0" encoding="utf-8"?>',
            '<dictionary version="2.0" sourceLcid="' + xmlEscape(sourceLcid) + '">',
            "  <entries>"
        ];

        for (var i = 0; i < entries.length; i++) {
            var entry = entries[i];
            var targets = entry.targets || {};
            var targetLcids = Object.keys(targets);

            xml.push('    <entry active="' + (entry.isActive ? "true" : "false") + '">');
            xml.push("      <sourceText>" + xmlEscape(entry.sourceText) + "</sourceText>");
            xml.push("      <targets>");

            for (var j = 0; j < targetLcids.length; j++) {
                var targetLcid = targetLcids[j];
                var targetText = targets[targetLcid];
                xml.push('        <target lcid="' + xmlEscape(targetLcid) + '">' + xmlEscape(targetText) + "</target>");
            }

            xml.push("      </targets>");
            xml.push("    </entry>");
        }

        xml.push("  </entries>");
        xml.push("</dictionary>");

        return xml.join("\n");
    }

    function sortDictionaryEntries(entries) {
        return (entries || []).sort(function (left, right) {
            return normalizeLookupText(left && left.sourceText).localeCompare(
                normalizeLookupText(right && right.sourceText),
                undefined,
                { sensitivity: "base" }
            );
        });
    }

    function sanitizeDictionaryModel(records, context) {
        var entries = [];
        var targets = (context && context.targetLanguages) || [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            var sourceText = String(getDictionaryRecordFieldValue(record, "sourceText") || "").trim();

            if (!sourceText) {
                continue;
            }

            var entry = {
                sourceText: sourceText,
                isActive: getDictionaryRecordFieldValue(record, "isActive") !== false,
                targets: {}
            };

            for (var t = 0; t < targets.length; t++) {
                var target = targets[t];
                var targetValue = String(getDictionaryRecordFieldValue(record, target.field) || "").trim();

                if (targetValue) {
                    entry.targets[target.lcid] = targetValue;
                }
            }

            entries.push(entry);
        }

        return {
            sourceLcid: String((context && context.baseLcid) || ""),
            entries: sortDictionaryEntries(entries)
        };
    }

    function getDictionaryRecordFieldValue(record, fieldName) {
        if (
            record &&
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, fieldName)
        ) {
            return record.w2ui.changes[fieldName];
        }

        return record ? record[fieldName] : null;
    }

    function createDictionaryInputRow(context) {
        var record = {
            recid: "new_" + Date.now() + "_" + Math.floor(Math.random() * 10000),
            sourceText: "",
            isActive: true
        };

        var targets = (context && context.targetLanguages) || [];
        for (var i = 0; i < targets.length; i++) {
            record[targets[i].field] = "";
        }

        return record;
    }

    function isDictionaryInputRowEmpty(record, context) {
        if (!record) {
            return false;
        }

        var sourceText = String(getDictionaryRecordFieldValue(record, "sourceText") || "").trim();
        if (sourceText) {
            return false;
        }

        var targets = (context && context.targetLanguages) || [];
        for (var i = 0; i < targets.length; i++) {
            var value = String(getDictionaryRecordFieldValue(record, targets[i].field) || "").trim();
            if (value) {
                return false;
            }
        }

        return true;
    }

    function ensureDictionaryInputRow(grid, context) {
        if (!grid) {
            return;
        }

        var records = grid.records || [];
        var emptyRows = [];

        for (var i = 0; i < records.length; i++) {
            if (isDictionaryInputRowEmpty(records[i], context)) {
                emptyRows.push(records[i].recid);
            }
        }

        if (!emptyRows.length) {
            grid.add(createDictionaryInputRow(context));
            return;
        }

        if (emptyRows.length > 1) {
            var extraRows = emptyRows.slice(1);
            grid.remove.apply(grid, extraRows);
        }
    }

    function createDictionarySignature(model) {
        var normalizedEntries = ((model && model.entries) || [])
            .map(function (entry) {
                var targetKeys = Object.keys(entry.targets || {}).sort();
                var normalizedTargets = {};

                for (var i = 0; i < targetKeys.length; i++) {
                    var key = targetKeys[i];
                    normalizedTargets[key] = entry.targets[key];
                }

                return {
                    sourceText: String(entry.sourceText || "").trim(),
                    isActive: entry.isActive !== false,
                    targets: normalizedTargets
                };
            })
            .filter(function (entry) {
                return !!entry.sourceText;
            })
            .sort(function (a, b) {
                return a.sourceText.localeCompare(b.sourceText);
            });

        return JSON.stringify({
            sourceLcid: String((model && model.sourceLcid) || ""),
            entries: normalizedEntries
        });
    }

    function getCurrentDictionaryGridModel() {
        if (!w2ui.translationDictionaryGrid || !dictionaryGridContext) {
            return {
                sourceLcid: String((dictionaryGridContext && dictionaryGridContext.baseLcid) || ""),
                entries: []
            };
        }

        return sanitizeDictionaryModel(w2ui.translationDictionaryGrid.records, dictionaryGridContext);
    }

    function hasDictionaryUnsavedChanges() {
        if (!dictionaryBaselineSignature || !dictionaryGridContext || !w2ui.translationDictionaryGrid) {
            return false;
        }

        var currentModel = getCurrentDictionaryGridModel();
        var currentSignature = createDictionarySignature(currentModel);

        return currentSignature !== dictionaryBaselineSignature;
    }

    function cleanupDictionaryPromptState() {
        dictionaryGridContext = null;
        dictionaryBaselineSignature = null;
        dictionaryAllowCloseWithoutPrompt = false;
    }

    function refreshDictionaryFromWebResource(context) {
        return loadDictionaryModel(context || {})
            .then(function (latestModel) {
                return latestModel;
            })
            .catch(function () {
                return null;
            });
    }

    function promptDictionaryCloseWithUnsavedChanges() {
        function askConfirm(message) {
            return new Promise(function (resolve) {
                if (window.w2confirm) {
                    w2confirm(message, function (answer) {
                        resolve(answer === "Yes");
                    });
                    return;
                }

                resolve(window.confirm(message));
            });
        }

        return askConfirm("You have unsaved dictionary changes. Save before closing?").then(function (saveBeforeClose) {
            if (saveBeforeClose) {
                return DictionaryService.SaveFromGrid();
            }

            return askConfirm("Discard unsaved dictionary changes and close?").then(function (discardChanges) {
                if (discardChanges) {
                    dictionaryAllowCloseWithoutPrompt = true;
                    w2popup.close();
                }

                return null;
            });
        });
    }

    function loadDictionaryModel(context) {
        function tryParseWithFallbacks(content, parseContext) {
            var parsed = parseDictionaryXml(content, parseContext);

            if (parsed.entries && parsed.entries.length > 0) {
                return parsed;
            }

            var unescaped = String(content || "")
                .replace(/&lt;/g, "<")
                .replace(/&gt;/g, ">")
                .replace(/&quot;/g, '"')
                .replace(/&apos;/g, "'")
                .replace(/&amp;/g, "&");

            if (unescaped !== content) {
                var reparsed = parseDictionaryXml(unescaped, parseContext);
                if (reparsed.entries && reparsed.entries.length > 0) {
                    return reparsed;
                }
            }

            return parsed;
        }

        return ensureInitialized(false)
            .then(function (content) {
                content = content && content.content;
                content = content || getDefaultDictionaryXml();

                var primaryModel = tryParseWithFallbacks(content, context || {});
                return primaryModel;
            })
            .then(function (finalModel) {
                return finalModel;
            });
    }

    function saveDictionaryModel(records, context) {
        var model = sanitizeDictionaryModel(records, context || {});
        var xml = serializeDictionaryXml(model);

        return executeOther("WriteDictionary", { content: xml }).then(function () {
            return model;
        });
    }

    function flushActiveDictionaryCellEdit() {
        try {
            var grid = w2ui.translationDictionaryGrid;
            if (!grid || !grid.box) {
                return;
            }

            var active = document.activeElement;
            if (active && typeof active.blur === "function" && grid.box.contains(active)) {
                active.blur();
            }
        } catch {}
    }

    function removeDictionarySearchPanel(gridBox) {
        var searchPanels = gridBox.querySelectorAll(".w2ui-grid-searches");
        for (var i = 0; i < searchPanels.length; i++) {
            searchPanels[i].remove();
        }
    }

    function configureSimpleDictionarySearch(grid) {
        if (!grid || grid._xqtSimpleSearchConfigured) {
            return;
        }

        if (grid.defaultOperator) {
            grid.defaultOperator.text = "contains";
        }
        if (grid.show) {
            grid.show.searchLogic = false;
            grid.show.searchSave = false;
            grid.show.toolbarReload = false;
        }

        grid.searchOpen = function () {};
        grid.searchShowFields = function () {};
        grid.searchSuggest = function () {};
        grid._xqtSimpleSearchConfigured = true;
    }

    function normalizeDictionarySearchUi() {
        var grid = w2ui && w2ui.translationDictionaryGrid ? w2ui.translationDictionaryGrid : null;
        var gridBox = grid && grid.box ? grid.box : null;
        if (!grid || !gridBox || !grid.name) {
            return;
        }

        configureSimpleDictionarySearch(grid);
        removeDictionarySearchPanel(gridBox);

        if (grid.toolbar) {
            if (grid.toolbar.get("w2ui-reload")) {
                grid.toolbar.remove("w2ui-reload");
            }
            if (grid.toolbar.get("w2ui-search-advanced")) {
                grid.toolbar.remove("w2ui-search-advanced");
            }
        }

        var searchName = gridBox.querySelector("#grid_" + grid.name + "_search_name");
        var searchInput = gridBox.querySelector("#grid_" + grid.name + "_search_all");
        var nameText = searchName ? searchName.querySelector(".name-text") : null;

        if (searchName) {
            searchName.style.display = "none";
        }
        if (nameText) {
            nameText.textContent = "";
        }

        grid.searchSelected = null;

        if (searchInput) {
            searchInput.readOnly = false;
            var searchValueText = String(searchInput.value || "")
                .trim()
                .toLowerCase();
            if (searchValueText === "null" || searchValueText === "undefined" || searchInput.value === " ") {
                searchInput.value = "";
            }
            searchInput.placeholder = "";
            searchInput.removeAttribute("placeholder");
        }

        if (grid.last) {
            grid.last.field = "all";
            grid.last.label = "All Fields";
        }
    }

    function normalizeDictionarySearchUiSoon() {
        normalizeDictionarySearchUi();
        setTimeout(normalizeDictionarySearchUi, 0);
        setTimeout(normalizeDictionarySearchUi, 50);
    }

    function ensureDictionaryGrid(context) {
        if (w2ui.translationDictionaryGrid) {
            w2ui.translationDictionaryGrid.destroy();
        }

        var targetCount = context.targetLanguages.length;
        var sourceSize = targetCount > 0 ? 35 : 80;
        var activeSize = 10;
        var targetSize = targetCount > 0 ? (100 - sourceSize - activeSize) / targetCount : 0;

        var columns = [
            {
                field: "sourceText",
                text: "Source " + context.baseName,
                size: sourceSize + "%",
                sortable: true,
                searchable: true,
                editable: { type: "text" }
            }
        ];

        for (var i = 0; i < context.targetLanguages.length; i++) {
            var target = context.targetLanguages[i];
            columns.push({
                field: target.field,
                text: "Target " + target.name,
                size: targetSize.toFixed(2) + "%",
                sortable: true,
                searchable: true,
                editable: { type: "text" }
            });
        }

        columns.push({
            field: "isActive",
            text: "Active",
            size: activeSize + "%",
            sortable: true,
            searchable: true,
            editable: { type: "checkbox" }
        });

        new w2grid({
            name: "translationDictionaryGrid",
            show: {
                toolbar: true,
                footer: true,
                selectColumn: true,
                toolbarSearch: true,
                toolbarReload: false,
                searchLogic: false,
                searchSave: false
            },
            multiSearch: false,
            multiSelect: true,
            searches: columns.map(function (column) {
                return { field: column.field, text: column.text, type: "text", operator: "contains" };
            }),
            toolbar: {
                items: [
                    { id: "delete", type: "button", text: "Delete", icon: "w2ui-icon-cross" },
                    { type: "spacer" },
                    { id: "save", type: "button", text: "Save", icon: "w2ui-icon-check" },
                    { id: "close", type: "button", text: "Close", icon: "w2ui-icon-cross" }
                ],
                onClick: function (event) {
                    if (event.target === "delete") {
                        var selected = w2ui.translationDictionaryGrid.getSelection();
                        if (selected && selected.length > 0) {
                            w2ui.translationDictionaryGrid.remove.apply(w2ui.translationDictionaryGrid, selected);
                            ensureDictionaryInputRow(w2ui.translationDictionaryGrid, dictionaryGridContext);
                        }
                    }

                    if (event.target === "save") {
                        DictionaryService.SaveFromGrid();
                    }

                    if (event.target === "close") {
                        w2popup.close();
                    }
                }
            },
            onChange: function (event) {
                event.onComplete = function () {
                    ensureDictionaryInputRow(w2ui.translationDictionaryGrid, dictionaryGridContext);
                };
            },
            onSearch: function (event) {
                event.onComplete = normalizeDictionarySearchUiSoon;
            },
            columns: columns,
            records: []
        });

        configureSimpleDictionarySearch(w2ui.translationDictionaryGrid);

        return w2ui.translationDictionaryGrid;
    }

    function toGridRecords(model, context) {
        var entries = (model && model.entries) || [];

        return sortDictionaryEntries(entries).map(function (entry, index) {
            var record = {
                recid: index + 1,
                sourceText: entry.sourceText,
                isActive: entry.isActive !== false
            };

            for (var i = 0; i < context.targetLanguages.length; i++) {
                var target = context.targetLanguages[i];
                record[target.field] = (entry.targets && entry.targets[target.lcid]) || "";
            }

            return record;
        });
    }

    function normalizeLookupText(value) {
        return String(value || "").trim();
    }

    function getRecordValue(record, lcid) {
        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, lcid)) {
            return record.w2ui.changes[lcid];
        }

        return record[lcid] || record[String(lcid)] || "";
    }

    function buildLookupKey(sourceText) {
        return normalizeLookupText(sourceText);
    }

    DictionaryService.EnsureInitialized = function (forceRefresh) {
        return ensureInitialized(!!forceRefresh);
    };

    DictionaryService.GetStorageInfo = function () {
        return null;
    };

    DictionaryService.UpsertEntries = function (entries) {
        entries = entries || [];

        return buildDictionaryGridContext().then(function (context) {
            var activeContext = dictionaryGridContext || context;
            var modelPromise =
                w2ui.translationDictionaryGrid && dictionaryGridContext
                    ? Promise.resolve(getCurrentDictionaryGridModel())
                    : loadDictionaryModel(activeContext);

            return modelPromise.then(function (model) {
                model = model || {};
                model.sourceLcid = String(activeContext.baseLcid || model.sourceLcid || "");
                model.entries = model.entries || [];

                var existingBySource = {};
                for (var i = 0; i < model.entries.length; i++) {
                    var existingEntry = model.entries[i];
                    if (!existingEntry || !existingEntry.sourceText) {
                        continue;
                    }

                    existingEntry.targets = existingEntry.targets || {};
                    existingBySource[buildLookupKey(existingEntry.sourceText)] = existingEntry;
                }

                var addedEntries = 0;
                var updatedEntries = 0;
                var targetCount = 0;

                for (var j = 0; j < entries.length; j++) {
                    var entry = entries[j] || {};
                    var sourceText = String(entry.sourceText || "").trim();
                    var targets = entry.targets || {};
                    var targetLcids = Object.keys(targets);

                    if (!sourceText || !targetLcids.length) {
                        continue;
                    }

                    var sourceKey = buildLookupKey(sourceText);
                    var modelEntry = existingBySource[sourceKey];
                    var isNewEntry = false;

                    if (!modelEntry) {
                        modelEntry = {
                            sourceText: sourceText,
                            isActive: true,
                            targets: {}
                        };
                        model.entries.push(modelEntry);
                        existingBySource[sourceKey] = modelEntry;
                        addedEntries++;
                        isNewEntry = true;
                    } else {
                        modelEntry.isActive = true;
                        modelEntry.targets = modelEntry.targets || {};
                    }

                    var entryTargetCount = 0;
                    for (var t = 0; t < targetLcids.length; t++) {
                        var targetLcid = String(targetLcids[t]);
                        var targetText = String(targets[targetLcid] || "").trim();

                        if (!targetLcid || !targetText) {
                            continue;
                        }

                        modelEntry.targets[targetLcid] = targetText;
                        targetCount++;
                        entryTargetCount++;
                    }

                    if (!isNewEntry && entryTargetCount > 0) {
                        updatedEntries++;
                    }
                }

                if (targetCount === 0) {
                    throw new Error("No dictionary target translations to save.");
                }

                return saveDictionaryModel(toGridRecords(model, activeContext), activeContext).then(
                    function (savedModel) {
                        if (w2ui.translationDictionaryGrid && dictionaryGridContext) {
                            var grid = w2ui.translationDictionaryGrid;
                            grid.clear();
                            grid.add(toGridRecords(savedModel, dictionaryGridContext));
                            ensureDictionaryInputRow(grid, dictionaryGridContext);
                            dictionaryBaselineSignature = createDictionarySignature(savedModel);
                            grid.refresh();
                        }

                        return {
                            addedEntries: addedEntries,
                            updatedEntries: updatedEntries,
                            targetCount: targetCount,
                            model: savedModel
                        };
                    }
                );
            });
        });
    };

    DictionaryService.ShowDictionaryPrompt = function () {
        DataverseLabelTranslator.LockGrid("Loading dictionary ...");

        return buildDictionaryGridContext()
            .then(function (context) {
                return loadDictionaryModel(context).then(function (model) {
                    var modelSourceLcid = String(model.sourceLcid || context.baseLcid);
                    if (modelSourceLcid !== context.baseLcid) {
                        context.baseLcid = modelSourceLcid;
                        context.baseName = getLanguageNameByLcid(modelSourceLcid);
                        context.targetLanguages = context.targetLanguages.filter(function (target) {
                            return target.lcid !== modelSourceLcid;
                        });
                    }

                    dictionaryGridContext = context;
                    dictionaryBaselineSignature = createDictionarySignature(model);
                    dictionaryAllowCloseWithoutPrompt = false;

                    var grid = ensureDictionaryGrid(context);
                    grid.clear();
                    var gridRecords = toGridRecords(model, context);

                    grid.add(gridRecords);
                    ensureDictionaryInputRow(grid, context);
                    DataverseLabelTranslator.UnlockGrid();

                    w2popup.open({
                        title: "Dictionary",
                        buttons: "",
                        width: 1000,
                        height: 640,
                        showClose: false,
                        showMax: false,
                        modal: true,
                        keyboard: false,
                        body: '<div id="dictionary-main" style="position: absolute; left: 5px; top: 5px; right: 5px; bottom: 5px;"></div>',
                        onOpen: function (event) {
                            event.onComplete = function () {
                                w2ui.translationDictionaryGrid.render("#w2ui-popup #dictionary-main");
                                normalizeDictionarySearchUiSoon();
                                setTimeout(function () {
                                    w2popup.max();
                                }, 100);
                            };
                        },
                        onToggle: function (event) {
                            w2ui.translationDictionaryGrid.box.style.display = "none";
                            event.onComplete = function () {
                                w2ui.translationDictionaryGrid.box.style.display = "";
                                w2ui.translationDictionaryGrid.resize();
                            };
                        },
                        onClose: function (event) {
                            if (dictionaryAllowCloseWithoutPrompt) {
                                var finalContext = dictionaryGridContext
                                    ? {
                                          baseLcid: dictionaryGridContext.baseLcid,
                                          baseName: dictionaryGridContext.baseName,
                                          targetLanguages: dictionaryGridContext.targetLanguages
                                      }
                                    : {};

                                cleanupDictionaryPromptState();
                                refreshDictionaryFromWebResource(finalContext);
                                return;
                            }

                            if (!hasDictionaryUnsavedChanges()) {
                                var cleanContext = dictionaryGridContext
                                    ? {
                                          baseLcid: dictionaryGridContext.baseLcid,
                                          baseName: dictionaryGridContext.baseName,
                                          targetLanguages: dictionaryGridContext.targetLanguages
                                      }
                                    : {};

                                dictionaryAllowCloseWithoutPrompt = true;
                                cleanupDictionaryPromptState();
                                refreshDictionaryFromWebResource(cleanContext);
                                return;
                            }

                            event.preventDefault();
                            promptDictionaryCloseWithUnsavedChanges();
                        }
                    });
                });
            })
            .catch(function (error) {
                DataverseLabelTranslator.UnlockGrid();
                DataverseLabelTranslator.errorHandler(error);
            });
    };

    DictionaryService.SaveFromGrid = function () {
        if (!w2ui.translationDictionaryGrid || !dictionaryGridContext) {
            return;
        }

        flushActiveDictionaryCellEdit();

        return Promise.resolve()
            .then(function () {
                // Let blur/change handlers flush active cell value into grid changes.
                return new Promise(function (resolve) {
                    setTimeout(resolve, 0);
                });
            })
            .then(function () {
                w2popup.lock("Saving ......", true);

                return saveDictionaryModel(w2ui.translationDictionaryGrid.records, dictionaryGridContext);
            })
            .then(function (savedModel) {
                dictionaryBaselineSignature = createDictionarySignature(savedModel);
                w2popup.unlock();
                return null;
            })
            .catch(function (error) {
                w2popup.unlock();

                var errorMessage = error && error.message ? error.message : String(error);
                if (window.DialogHelper && DialogHelper.alert) {
                    return DialogHelper.alert(errorMessage, { title: "Dictionary" });
                }

                w2alert(errorMessage);
                return null;
            });
    };

    DictionaryService.SplitRecordsByDictionary = function (fromLcid, targetLcid, records) {
        return buildDictionaryGridContext()
            .then(function (context) {
                return loadDictionaryModel(context).then(function (model) {
                    var sourceLcid = String(model.sourceLcid || context.baseLcid || "");
                    if (String(fromLcid) !== sourceLcid) {
                        return {
                            matchedResults: [],
                            unmatchedRecords: records
                        };
                    }

                    var lookup = {};
                    var entries = model.entries || [];

                    for (var i = 0; i < entries.length; i++) {
                        var entry = entries[i];

                        if (!entry || !entry.isActive) {
                            continue;
                        }

                        var targetText = entry.targets ? entry.targets[String(targetLcid)] : null;
                        if (!targetText) {
                            continue;
                        }

                        var sourceKey = buildLookupKey(entry.sourceText);
                        if (!lookup[sourceKey]) {
                            lookup[sourceKey] = targetText;
                        }
                    }

                    var matchedResults = [];
                    var unmatchedRecords = [];

                    for (var j = 0; j < records.length; j++) {
                        var record = records[j];
                        var source = w2utils.decodeTags(getRecordValue(record, fromLcid));
                        var sourceLookupKey = buildLookupKey(source);
                        var matchedTarget = lookup[sourceLookupKey];

                        if (matchedTarget) {
                            matchedResults.push({
                                recid: record.recid,
                                targetRecid: record.recid,
                                location: record.location,
                                schemaName: record.schemaName,
                                column: targetLcid,
                                source: source,
                                translation: w2utils.encodeTags(matchedTarget),
                                fromDictionary: true
                            });
                        } else {
                            unmatchedRecords.push(record);
                        }
                    }

                    return {
                        matchedResults: matchedResults,
                        unmatchedRecords: unmatchedRecords
                    };
                });
            })
            .catch(function () {
                return {
                    matchedResults: [],
                    unmatchedRecords: records
                };
            });
    };
})((window.DictionaryService = window.DictionaryService || {}));
