(function (EasyTranslator, undefined) {
    "use strict";

    var actionName = "AiTranslate";
    var workspaceGridName = "easyAiTranslateGrid";
    var state = null;

    function executeOther(operation, payload) {
        var input = Object.assign({ operation: operation }, payload || {});

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Other, input).then(
            Helper.GetCustomActionObject
        );
    }

    function encodeValue(value) {
        return window.w2utils && typeof w2utils.encodeTags === "function" ? w2utils.encodeTags(value) : value;
    }

    function decodeValue(value) {
        return window.w2utils && typeof w2utils.decodeTags === "function" ? w2utils.decodeTags(value) : value;
    }

    function normalizeText(value) {
        return String(value || "")
            .replace(/&nbsp;/gi, " ")
            .replace(/\u00a0/g, " ")
            .replace(/<[^>]*>/g, "")
            .trim();
    }

    function hasText(value) {
        return normalizeText(value).length > 0;
    }

    function isLanguageField(field) {
        return /^\d+$/.test(String(field || ""));
    }

    function getWorkspaceValue(record, field) {
        if (
            record &&
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, field)
        ) {
            return record.w2ui.changes[field];
        }

        return record ? record[field] : "";
    }

    function getLanguageItemText(lcid) {
        if (!state) {
            return lcid;
        }

        for (var i = 0; i < state.languages.length; i++) {
            if (state.languages[i].lcid === String(lcid)) {
                return state.languages[i].text || lcid;
            }
        }

        return lcid;
    }

    function getToolbarSelection(id) {
        var grid = w2ui[workspaceGridName];
        var item = grid && grid.toolbar ? grid.toolbar.get(id) : null;

        return item ? String(item.selected || "") : "";
    }

    function setToolbarApplyEnabled(enabled) {
        var grid = w2ui[workspaceGridName];
        var toolbar = grid && grid.toolbar ? grid.toolbar : null;
        var item = toolbar ? toolbar.get("apply") : null;

        if (!item) {
            return;
        }

        item.disabled = !enabled;
        toolbar.refresh();
    }

    function setToolbarTranslateEnabled(enabled) {
        var grid = w2ui[workspaceGridName];
        var toolbar = grid && grid.toolbar ? grid.toolbar : null;
        var item = toolbar ? toolbar.get("translate") : null;

        if (!item) {
            return;
        }

        item.disabled = !enabled;
        toolbar.refresh();
    }

    function visitRows(rows, visitor) {
        rows = rows || [];
        for (var i = 0; i < rows.length; i++) {
            visitor(rows[i]);

            if (rows[i].w2ui && Array.isArray(rows[i].w2ui.children)) {
                visitRows(rows[i].w2ui.children, visitor);
            }
        }
    }

    function getRecordByRecid(records, recid) {
        var found = null;

        visitRows(records, function (record) {
            if (!found && record.recid === recid) {
                found = record;
            }
        });

        return found;
    }

    function getTranslatableRows(rows) {
        var result = [];

        visitRows(rows, function (record) {
            if (record.targetRecid) {
                result.push(record);
            }
        });

        return result;
    }

    function hasWorkspaceLanguageChanges() {
        var records = state ? state.allRows || [] : [];
        var hasChanges = false;

        visitRows(records, function (record) {
            var changes = record.w2ui && record.w2ui.changes ? record.w2ui.changes : {};
            for (var field in changes) {
                if (Object.prototype.hasOwnProperty.call(changes, field) && isLanguageField(field)) {
                    hasChanges = true;
                    return;
                }
            }
        });

        return hasChanges;
    }

    function updateApplyButtonState() {
        setToolbarApplyEnabled(hasWorkspaceLanguageChanges());
    }

    function canTranslate() {
        return !!(state && state.targetLcid && state.providerId);
    }

    function updateTranslateButtonState() {
        setToolbarTranslateEnabled(canTranslate());
    }

    function createLanguageToolbarText(label) {
        return function (item) {
            return item.selected ? label + ": " + getLanguageItemText(item.selected) : label;
        };
    }

    function createProviderToolbarText() {
        return function (item) {
            var selected = String(item.selected || "");

            for (var i = 0; state && i < state.providers.length; i++) {
                if (state.providers[i].id === selected) {
                    return "Provider: " + state.providers[i].text;
                }
            }

            return "Provider";
        };
    }

    function createColumns() {
        var columns = [
            { field: "schemaName", text: "Schema Name", size: "25%", sortable: true, searchable: true, frozen: true }
        ];
        var orderedLanguages = [state.baseLcid].concat(
            state.languages
                .map(function (language) {
                    return language.lcid;
                })
                .filter(function (lcid) {
                    return lcid !== state.baseLcid;
                })
        );

        for (var i = 0; i < orderedLanguages.length; i++) {
            var lcid = orderedLanguages[i];
            columns.push({
                field: lcid,
                text: getLanguageItemText(lcid),
                size: "18%",
                sortable: true,
                searchable: true,
                editable: { type: "text" }
            });
        }

        columns.push({
            field: "usedDictionary",
            text: "Use Dictionary",
            size: "110px",
            sortable: true,
            searchable: true,
            render: function (record) {
                return '<input type="checkbox" disabled ' + (record.usedDictionary === true ? "checked" : "") + " />";
            }
        });

        return columns;
    }

    function createToolbarItems() {
        var languageItems = state.languages.map(function (language) {
            return {
                id: language.lcid,
                text: language.text
            };
        });
        var sourceLanguageItems = languageItems.filter(function (language) {
            return language.id === state.baseLcid;
        });
        var targetLanguageItems = languageItems.filter(function (language) {
            return language.id !== state.baseLcid;
        });
        var providerItems = state.providers.map(function (provider) {
            return {
                id: provider.id,
                text: provider.text
            };
        });

        return [
            {
                type: "menu",
                id: "toggle",
                text: "",
                tooltip: "Expand/collapse rows",
                icon: "icon-tree",
                items: [
                    { type: "button", text: "Expand all records", id: "expandAll", icon: "w2ui-icon-expand" },
                    { type: "button", text: "Collapse all records", id: "collapseAll", icon: "w2ui-icon-collapse" }
                ]
            },
            { type: "break", id: "break-toggle" },
            {
                type: "menu-radio",
                id: "source",
                text: createLanguageToolbarText("Source"),
                selected: state.baseLcid,
                items: sourceLanguageItems,
                disabled: true
            },
            {
                type: "menu-radio",
                id: "target",
                text: createLanguageToolbarText("Target"),
                selected: state.targetLcid,
                items: targetLanguageItems
            },
            {
                type: "menu-radio",
                id: "provider",
                text: createProviderToolbarText(),
                selected: state.providerId,
                items: providerItems
            },
            { type: "check", id: "allMissing", text: "All Missing", checked: false },
            { type: "check", id: "useDictionary", text: "Use Dictionary", checked: true },
            { type: "spacer" },
            { type: "button", id: "translate", text: "Translate", icon: "icon-translate", disabled: !canTranslate() },
            { type: "button", id: "apply", text: "Apply and Close", icon: "w2ui-icon-check", disabled: true },
            { type: "button", id: "close", text: "Close", icon: "w2ui-icon-cross" }
        ];
    }

    function isRowEligible(row, source, target, allMissing) {
        if (!row.targetRecid) {
            return false;
        }

        if (!hasText(getWorkspaceValue(row, source))) {
            return false;
        }

        if (allMissing && hasText(getWorkspaceValue(row, target))) {
            return false;
        }

        return true;
    }

    function filterRows(rows) {
        var source = state.sourceLcid;
        var target = state.targetLcid;
        var allMissing = !!state.allMissing;
        var filteredRows = [];

        rows = rows || [];
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            var clone = JSON.parse(JSON.stringify(row));
            var children = row.w2ui && Array.isArray(row.w2ui.children) ? row.w2ui.children : [];
            var filteredChildren = filterRows(children);

            if (filteredChildren.length > 0) {
                clone.w2ui = clone.w2ui || {};
                clone.w2ui.children = filteredChildren;
                filteredRows.push(clone);
                continue;
            }

            if (isRowEligible(row, source, target, allMissing)) {
                if (clone.w2ui && clone.w2ui.children) {
                    delete clone.w2ui.children;
                }
                filteredRows.push(clone);
            }
        }

        return filteredRows;
    }

    function cloneRows(rows) {
        return JSON.parse(JSON.stringify(rows || []));
    }

    function syncGridRowsToState() {
        var grid = w2ui[workspaceGridName];
        var records = grid ? grid.records || [] : [];
        if (!state) {
            return;
        }

        visitRows(records, function (visibleRecord) {
            var stateRecord = getRecordByRecid(state.allRows || [], visibleRecord.recid);
            if (!stateRecord) {
                return;
            }

            stateRecord.usedDictionary = visibleRecord.usedDictionary === true;

            if (!visibleRecord.w2ui || !visibleRecord.w2ui.changes) {
                return;
            }

            stateRecord.w2ui = stateRecord.w2ui || {};
            stateRecord.w2ui.changes = stateRecord.w2ui.changes || {};

            for (var field in visibleRecord.w2ui.changes) {
                if (Object.prototype.hasOwnProperty.call(visibleRecord.w2ui.changes, field)) {
                    stateRecord.w2ui.changes[field] = visibleRecord.w2ui.changes[field];
                }
            }
        });
    }

    function rebuildGridRows() {
        var grid = w2ui[workspaceGridName];
        if (!grid) {
            return;
        }

        syncGridRowsToState();
        grid.clear();
        grid.add(filterRows(state.allRows));
        updateApplyButtonState();
        normalizeSearchUiSoon();
    }

    function removeSearchPanel(gridBox) {
        var searchPanels = gridBox.querySelectorAll(".w2ui-grid-searches");
        for (var i = 0; i < searchPanels.length; i++) {
            searchPanels[i].remove();
        }
    }

    function configureSimpleSearch(grid) {
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

    function normalizeSearchUi() {
        var grid = w2ui && w2ui[workspaceGridName] ? w2ui[workspaceGridName] : null;
        var gridBox = grid && grid.box ? grid.box : null;
        if (!grid || !gridBox || !grid.name) {
            return;
        }

        configureSimpleSearch(grid);
        removeSearchPanel(gridBox);

        if (grid.toolbar) {
            if (grid.toolbar.get("w2ui-reload")) {
                grid.toolbar.remove("w2ui-reload");
            }
            if (grid.toolbar.get("w2ui-search-advanced")) {
                grid.toolbar.remove("w2ui-search-advanced");
            }
        }

        if (grid.searchSelected) {
            grid.searchSelected = null;
            if (typeof grid.refreshSearch === "function") {
                grid.refreshSearch();
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
            if (
                searchValueText === "null" ||
                searchValueText === "undefined" ||
                searchValueText === "search all fields" ||
                searchInput.value === " "
            ) {
                searchInput.value = "";
            }
            searchInput.placeholder = "";
            searchInput.setAttribute("placeholder", "");
            searchInput.removeAttribute("placeholder");
        }

        if (grid.last) {
            grid.last.field = "all";
            grid.last.label = "All Fields";
        }

        resetGridBodyOffset(grid, gridBox);
    }

    function toggleAllRows(expand) {
        var grid = w2ui[workspaceGridName];

        if (!grid || !state) {
            return;
        }

        syncGridRowsToState();
        toggleRenderedRows(grid, expand);
        syncGridRowsToState();
        normalizeSearchUiSoon();
    }

    function toggleRenderedRows(grid, expand) {
        var changed = true;
        var guard = 0;

        while (changed && guard < 20) {
            changed = false;
            guard++;

            for (var i = 0; i < grid.records.length; i++) {
                var record = grid.records[i];
                if (
                    !record ||
                    !record.w2ui ||
                    !Array.isArray(record.w2ui.children) ||
                    record.w2ui.children.length === 0
                ) {
                    continue;
                }

                if (expand && record.w2ui.expanded !== true && typeof grid.expand === "function") {
                    changed = grid.expand(record.recid) !== false || changed;
                    continue;
                }

                if (!expand && record.w2ui.expanded === true && typeof grid.collapse === "function") {
                    changed = grid.collapse(record.recid) !== false || changed;
                }
            }
        }

        if (typeof grid.refresh === "function") {
            grid.refresh();
        }
    }

    function resetGridBodyOffset(grid, gridBox) {
        var toolbar = gridBox.querySelector(".w2ui-grid-toolbar");
        var body = gridBox.querySelector(".w2ui-grid-body");
        var toolbarHeight = getVisibleToolbarContentHeight(toolbar);

        if (!body || !toolbarHeight) {
            return;
        }

        if (grid && grid.last) {
            grid.last.toolbar_height = toolbarHeight;
        }

        body.style.setProperty("top", toolbarHeight + "px", "important");
    }

    function getVisibleToolbarContentHeight(toolbar) {
        if (!toolbar) {
            return 0;
        }

        var toolbarRect = toolbar.getBoundingClientRect();
        var items = toolbar.querySelectorAll(
            ".w2ui-grid-search-input, .w2ui-tb-button, .w2ui-tb-break, .w2ui-tb-spacer"
        );
        var bottom = 0;

        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var style = window.getComputedStyle(item);
            if (style.display === "none" || style.visibility === "hidden") {
                continue;
            }

            var rect = item.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) {
                continue;
            }

            bottom = Math.max(bottom, rect.bottom - toolbarRect.top);
        }

        return bottom ? Math.ceil(bottom) + 4 : toolbar.offsetHeight;
    }

    function normalizeSearchUiSoon() {
        normalizeSearchUi();
        setTimeout(normalizeSearchUi, 0);
        setTimeout(normalizeSearchUi, 50);
        setTimeout(normalizeSearchUi, 150);
        setTimeout(normalizeSearchUi, 300);
    }

    function canEditWorkspaceCell(event) {
        var grid = w2ui[workspaceGridName];
        var detail = event.detail || {};
        var column = grid && grid.columns ? grid.columns[detail.column] : null;
        var field = column ? column.field : "";
        var record = grid && typeof grid.get === "function" ? grid.get(detail.recid) : null;

        if (!isLanguageField(field)) {
            return true;
        }

        return !!(record && record.targetRecid);
    }

    function handleToolbarClick(event) {
        var target = String(event.target || "");

        if (target.indexOf("target:") === 0) {
            state.targetLcid = target.replace("target:", "");
            updateTranslateButtonState();
            return;
        }

        if (target.indexOf("provider:") === 0) {
            state.providerId = target.replace("provider:", "");
            updateTranslateButtonState();
            return;
        }

        if (target === "allMissing") {
            event.onComplete = function () {
                var item = w2ui[workspaceGridName].toolbar.get("allMissing");
                state.allMissing = !!(item && item.checked);
                rebuildGridRows();
                normalizeSearchUiSoon();
            };
            return;
        }

        if (target === "useDictionary") {
            event.onComplete = function () {
                var item = w2ui[workspaceGridName].toolbar.get("useDictionary");
                state.useDictionary = !!(item && item.checked);
            };
            return;
        }

        if (target.indexOf("expandAll") !== -1) {
            toggleAllRows(true);
            return;
        }

        if (target.indexOf("collapseAll") !== -1) {
            toggleAllRows(false);
            return;
        }

        if (target === "translate") {
            translateRows();
            return;
        }

        if (target === "apply") {
            applyWorkspaceChanges();
            return;
        }

        if (target === "close") {
            w2popup.close();
        }
    }

    function renderWorkspace() {
        var container = document.querySelector("#w2ui-popup #easy-ai-translate-main");
        if (!container) {
            return;
        }

        container.innerHTML = "";

        if (w2ui[workspaceGridName]) {
            w2ui[workspaceGridName].destroy();
        }

        new w2grid({
            name: workspaceGridName,
            show: {
                toolbar: true,
                footer: true,
                toolbarSearch: true,
                toolbarReload: false,
                searchSave: false
            },
            multiSelect: false,
            columns: createColumns(),
            records: [],
            searches: createColumns()
                .filter(function (column) {
                    return column.searchable === true && isLanguageField(column.field) === false;
                })
                .map(function (column) {
                    return { field: column.field, text: column.text, type: "text", operator: "contains" };
                }),
            toolbar: {
                items: createToolbarItems(),
                onClick: handleToolbarClick
            },
            onChange: function (event) {
                event.onComplete = function () {
                    syncGridRowsToState();
                    updateApplyButtonState();
                };
            },
            onEditField: function (event) {
                if (!canEditWorkspaceCell(event)) {
                    event.preventDefault();
                }
            },
            onSearch: function (event) {
                event.onComplete = normalizeSearchUiSoon;
            }
        });

        w2ui[workspaceGridName].render(container);
        rebuildGridRows();
        updateTranslateButtonState();
        normalizeSearchUiSoon();
        w2ui[workspaceGridName].resize();
    }

    function renderLoadError(error) {
        var container = document.querySelector("#w2ui-popup #easy-ai-translate-main");
        if (!container) {
            return;
        }

        container.innerHTML =
            '<div class="easy-ai-load-error">' +
            '<div class="easy-ai-load-error-title">AI Translate is not ready</div>' +
            '<div class="easy-ai-load-error-message">' +
            encodeValue(getErrorMessage(error)) +
            "</div>" +
            '<button type="button" class="w2ui-btn easy-ai-load-error-close">Close</button>' +
            "</div>";

        var closeButton = container.querySelector(".easy-ai-load-error-close");
        if (closeButton) {
            closeButton.onclick = function () {
                w2popup.close();
            };
        }
    }

    function getErrorMessage(error) {
        if (!error) {
            return "Could not load AI Translate settings.";
        }

        if (typeof error === "string") {
            return error;
        }

        return error.message || error.Message || error.error || "Could not load AI Translate settings.";
    }

    function getInitialTarget(languages, baseLcid) {
        for (var i = 0; i < languages.length; i++) {
            if (languages[i].lcid !== baseLcid) {
                return languages[i].lcid;
            }
        }

        return "";
    }

    function normalizeDataSource(dataSource) {
        dataSource = dataSource || {};

        var languages = dataSource.languages || [];
        var baseLcid = String(dataSource.baseLcid || (languages.length ? languages[0].lcid : ""));

        return {
            languages: languages,
            baseLcid: baseLcid,
            sourceLcid: baseLcid,
            targetLcid: getInitialTarget(languages, baseLcid),
            rows: cloneRows(dataSource.rows || []),
            title: getWorkspaceTitle(dataSource),
            applyChanges: typeof dataSource.applyChanges === "function" ? dataSource.applyChanges : null
        };
    }

    function getWorkspaceTitle(dataSource) {
        var componentText = normalizeText(dataSource && dataSource.componentText);

        return componentText ? "AI Translate - " + componentText : "AI Translate";
    }

    function preloadWorkspace(dataSource) {
        var normalizedDataSource = normalizeDataSource(dataSource);

        return executeOther("Providers").then(function (providerOutput) {
            var providers = providerOutput.providers || [];
            var providerId = providerOutput.selectedProvider || (providers.length ? providers[0].id : "");

            state = {
                languages: normalizedDataSource.languages,
                baseLcid: normalizedDataSource.baseLcid,
                sourceLcid: normalizedDataSource.sourceLcid,
                targetLcid: normalizedDataSource.targetLcid,
                providerId: providerId,
                providers: providers,
                allMissing: false,
                useDictionary: true,
                allRows: normalizedDataSource.rows,
                title: normalizedDataSource.title,
                applyChanges: normalizedDataSource.applyChanges
            };

            return state;
        });
    }

    function getIso(lcid) {
        var text = getLanguageItemText(lcid);
        var match = text.match(/\(([a-z]{2})(?:[-_][a-z]{2})?\)\s*\(\d+\)\s*$/i);

        if (match) {
            return match[1].toLowerCase();
        }

        match = text.match(/\b([a-z]{2})(?:[-_][a-z]{2})?\b/i);
        return match ? match[1].toLowerCase() : null;
    }

    function validateTranslate() {
        var grid = w2ui[workspaceGridName];
        var source = state.baseLcid;
        var target = getToolbarSelection("target") || state.targetLcid;
        var provider = getToolbarSelection("provider") || state.providerId;

        state.sourceLcid = source;
        state.targetLcid = target;
        state.providerId = provider;

        if (!grid) return "AI Translate grid is not loaded.";
        if (!state.languages.length) return "Language columns are required.";
        if (!source) return "Source language is required.";
        if (!target) return "Target language is required.";
        if (source === target) return "Source and target languages must be different.";
        if (!provider) return "AI provider is required.";
        if (!isLanguageField(target)) return "Target language column is invalid.";
        if (!getIso(source) || !getIso(target)) return "Could not resolve source or target language ISO code.";
        syncGridRowsToState();
        if (!getTranslatableRows(filterRows(grid.records || [])).length) {
            return "No records to translate for the selected options.";
        }

        return "";
    }

    function applyWorkspaceCellChange(record, field, value) {
        if (!record.w2ui) {
            record.w2ui = {};
        }

        if (!record.w2ui.changes) {
            record.w2ui.changes = {};
        }

        record.w2ui.changes[field] = value;
    }

    function applyTranslationResults(results, targetLcid) {
        var grid = w2ui[workspaceGridName];

        for (var i = 0; i < results.length; i++) {
            var result = results[i];
            var record = getRecordByRecid(grid.records || [], result.recid);
            var stateRecord = getRecordByRecid(state.allRows || [], result.recid);

            if (record) {
                applyWorkspaceCellChange(record, targetLcid, result.translation);
                record.usedDictionary = result.usedDictionary === true;
                grid.refreshRow(record.recid);
            }

            if (stateRecord) {
                applyWorkspaceCellChange(stateRecord, targetLcid, result.translation);
                stateRecord.usedDictionary = result.usedDictionary === true;
            }
        }

        updateApplyButtonState();
    }

    function translateRows() {
        var validationError = validateTranslate();
        if (validationError) {
            updateTranslateButtonState();
            return;
        }

        var grid = w2ui[workspaceGridName];
        var sourceLcid = state.sourceLcid;
        var targetLcid = state.targetLcid;
        syncGridRowsToState();
        var eligibleRows = getTranslatableRows(filterRows(grid.records || []));

        w2popup.lock("Translating...", true);
        executeOther("Translate", {
            provider: state.providerId,
            fromLanguage: getIso(sourceLcid),
            toLanguage: getIso(targetLcid),
            fromLcid: sourceLcid,
            toLcid: targetLcid,
            useDictionary: state.useDictionary !== false,
            items: eligibleRows.map(function (record) {
                return {
                    text: decodeValue(getWorkspaceValue(record, sourceLcid))
                };
            })
        })
            .then(function (output) {
                var serverResults = output.results || [];
                var translations = output.translations || [];
                var results = [];

                for (var i = 0; i < eligibleRows.length; i++) {
                    var serverResult = serverResults[i] || {};
                    var translation = Object.prototype.hasOwnProperty.call(serverResult, "translation")
                        ? serverResult.translation
                        : translations[i];

                    if (!translation) {
                        continue;
                    }

                    results.push({
                        recid: eligibleRows[i].recid,
                        translation: encodeValue(translation),
                        usedDictionary: serverResult.usedDictionary === true
                    });
                }

                applyTranslationResults(results, targetLcid);
            })
            .then(function () {
                w2popup.unlock();
            })
            .catch(function (error) {
                w2popup.unlock();
                if (window.console && typeof window.console.error === "function") {
                    window.console.error("AI Translate failed.", error);
                }
            });
    }

    function applyWorkspaceChanges() {
        var grid = w2ui[workspaceGridName];
        syncGridRowsToState();
        var records = getTranslatableRows(state.allRows || []);
        var changesToApply = [];

        for (var i = 0; i < records.length; i++) {
            var workspaceRecord = records[i];
            var changes = workspaceRecord.w2ui && workspaceRecord.w2ui.changes ? workspaceRecord.w2ui.changes : null;
            if (!changes) {
                continue;
            }

            for (var field in changes) {
                if (!Object.prototype.hasOwnProperty.call(changes, field) || !isLanguageField(field)) {
                    continue;
                }

                changesToApply.push({
                    targetRecid: workspaceRecord.targetRecid,
                    field: field,
                    value: changes[field]
                });
            }
        }

        if (!changesToApply.length) {
            updateApplyButtonState();
            w2popup.close();
            return;
        }

        if (typeof state.applyChanges !== "function") {
            Helper.ShowError("AI Translate apply callback is not configured.", { title: "AI Translate" });
            return;
        }

        state.applyChanges(changesToApply);

        for (var r = 0; r < records.length; r++) {
            var record = records[r];
            var recordChanges = record.w2ui && record.w2ui.changes ? record.w2ui.changes : null;
            if (!recordChanges) {
                continue;
            }

            for (var changedField in recordChanges) {
                if (
                    !Object.prototype.hasOwnProperty.call(recordChanges, changedField) ||
                    !isLanguageField(changedField)
                ) {
                    continue;
                }

                record[changedField] = recordChanges[changedField];
                delete recordChanges[changedField];
            }

            if (Object.keys(recordChanges).length === 0) {
                delete record.w2ui.changes;
            }
        }

        if (grid) {
            grid.refresh();
        }
        updateApplyButtonState();
        w2popup.close();
    }

    function openWorkspace(dataSource) {
        var title = getWorkspaceTitle(dataSource);

        w2popup.open({
            title: title,
            name: "easyAiTranslatePopup",
            body:
                '<div id="easy-ai-translate-main" style="position:absolute;left:5px;top:5px;right:5px;bottom:5px;">' +
                '<div style="height:100%;display:flex;align-items:center;justify-content:center;font-size:16px;">Loading ...</div>' +
                "</div>",
            buttons: "",
            width: 1000,
            height: 650,
            showClose: false,
            showMax: false,
            modal: true,
            onOpen: function (event) {
                event.onComplete = function () {
                    setTimeout(function () {
                        w2popup.max();
                    }, 0);

                    preloadWorkspace(dataSource)
                        .then(renderWorkspace)
                        .catch(function (error) {
                            renderLoadError(error);
                        });
                };
            },
            onToggle: function (event) {
                if (w2ui[workspaceGridName] && w2ui[workspaceGridName].box) {
                    w2ui[workspaceGridName].box.style.display = "none";
                }
                event.onComplete = function () {
                    if (w2ui[workspaceGridName] && w2ui[workspaceGridName].box) {
                        w2ui[workspaceGridName].box.style.display = "";
                        w2ui[workspaceGridName].resize();
                    }
                };
            },
            onClose: function () {
                state = null;
                if (w2ui[workspaceGridName]) {
                    w2ui[workspaceGridName].destroy();
                }
            }
        });
    }

    EasyTranslator.OpenAiTranslateWorkspace = function (dataSource) {
        return openWorkspace(dataSource);
    };
})((window.EasyTranslator = window.EasyTranslator || {}));
