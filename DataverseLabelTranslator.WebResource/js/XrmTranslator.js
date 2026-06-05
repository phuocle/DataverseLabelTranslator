(function (XrmTranslator, undefined) {
    "use strict";

    XrmTranslator.entityMetadata = {};
    XrmTranslator.metadata = [];

    XrmTranslator.entity = null;
    XrmTranslator.type = null;

    // We need those for the FormHandleer, uilanguageid is current user language, formXml only contains labels for this locale by default
    XrmTranslator.userId = null;
    XrmTranslator.userSettings = null;
    XrmTranslator.installedLanguages = null;
    XrmTranslator.baseLanguage = null;
    XrmTranslator.hasAllowedRole = null;

    XrmTranslator.columnRestoreNeeded = false;

    XrmTranslator.defaultSchemaNameSize = "20%";

    XrmTranslator.showAllInOneType = true;

    XrmTranslator.LockGridProgress = function (label, current, total) {
        total = total || 0;
        current = Math.min(current || 0, total);

        if (total <= 0) {
            XrmTranslator.LockGrid(label);
            return;
        }

        XrmTranslator.LockGrid(label + " " + current + "/" + total);
    };

    XrmTranslator.allEntities = [];

    var currentHandler = null;
    var baseLanguageScopeDepth = 0;
    var baseLanguageRestoreLcid = null;
    var unfilteredRecords = null;
    var recordSelectorContext = null;
    var PUBLISH_XML_JOB_MAX_BLOCKED_ATTEMPTS = 10;
    var PUBLISH_XML_JOB_POLL_INTERVAL_MS = 30000;
    var IMPORT_JOB_POLL_INTERVAL_MS = 5000;
    var OPERATION_STATE_MAX_AGE_MS = 10 * 60 * 1000;
    var statusBannerAutoHideTimer = null;
    var publishXmlJobPollTimer = null;
    var publishXmlJobPollInFlight = false;
    var publishXmlJobState = null;
    var importJobPollTimer = null;
    var importJobPollInFlight = false;
    var operationState = null;
    var appLoadingActive = false;
    var appLoadingToolbarState = null;
    var ENTITY_DEPENDENT_TYPE_ITEMS = [
        "type:allInOne",
        "type:entitySeparator",
        "type:attributes",
        "type:options",
        "type:forms",
        "type:views",
        "type:formMeta",
        "type:entityMeta",
        "type:relationships",
        "type:charts",
        "type:content",
        "type:bpf",
        "type:businessRules",
        "type:ribbons",
        "type:commands",
        "type:entityMessages"
    ];
    var GLOBAL_TYPE_ITEMS = ["type:sitemap", "type:dashboards", "type:webresources", "type:globalOptionSet"];
    var TYPE_STATE_LABELS = {
        allInOne: "All-In-One",
        attributes: "Attributes",
        options: "Option Sets",
        forms: "Forms",
        views: "Views",
        formMeta: "Form Metadata",
        entityMeta: "Entity Metadata",
        relationships: "Relationships",
        charts: "Charts",
        bpf: "Business Process Flows",
        businessRules: "Business Rules",
        ribbons: "Ribbons",
        commands: "Commands",
        entityMessages: "Entity Messages",
        content: "Content Snippets",
        sitemap: "Sitemap",
        dashboards: "Dashboards",
        webresources: "Web Resources",
        globalOptionSet: "Global Option Set"
    };
    var ALLOWED_ROLE_NAMES = {
        "system administrator": true,
        "system customizer": true
    };
    RegExp.escape = function (s) {
        return s.replace(/[-\/\\^$*+?.()|[\]{}]/g, "\\$&");
    };

    function getTypeStateLabel(type) {
        return TYPE_STATE_LABELS[type] || String(type || "");
    }

    function GetRootGridRecords(grid) {
        return grid.records.filter(function (record) {
            return !record.w2ui || !record.w2ui.parent_recid;
        });
    }

    function HasChildGridRecords(record) {
        return record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0;
    }

    function AppendRecordTree(flatRecords, record, expand) {
        flatRecords.push(record);

        if (!HasChildGridRecords(record)) {
            return;
        }

        record.w2ui.expanded = !!expand;

        for (var i = 0; i < record.w2ui.children.length; i++) {
            var child = record.w2ui.children[i];
            child.w2ui = child.w2ui || {};
            child.w2ui.parent_recid = record.recid;

            if (!Array.isArray(child.w2ui.children)) {
                child.w2ui.children = [];
            }

            if (expand) {
                AppendRecordTree(flatRecords, child, true);
            }
        }
    }

    function ToggleExpandCollapse(expand) {
        var grid = XrmTranslator.GetGrid();
        var rootRecords = GetRootGridRecords(grid);
        var flatRecords = [];

        for (var i = 0; i < rootRecords.length; i++) {
            AppendRecordTree(flatRecords, rootRecords[i], expand);
        }

        grid.records = flatRecords;
        grid.total = flatRecords.length;
        if (grid.last) {
            grid.last.idCache = {};
        }
        if (grid.searchData && grid.searchData.length > 0) {
            grid.localSearch(true);
        }
        grid.refresh();
        NormalizeGridSearchUiSoon();
    }

    function GetToolbar() {
        return w2ui && w2ui.grid_toolbar ? w2ui.grid_toolbar : null;
    }

    function RefreshToolbar() {
        var toolbar = GetToolbar();
        if (toolbar) {
            toolbar.refresh();
            NormalizeGridSearchUiSoon();
        }
    }

    function EnforceToolbarOperationButtons(toolbar) {
        return false;
    }

    function EnforceToolbarOperationButtonsSoon() {
        function enforce() {
            var toolbar = GetToolbar();
            if (EnforceToolbarOperationButtons(toolbar) && toolbar && typeof toolbar.refresh === "function") {
                toolbar.refresh();
            }
        }

        enforce();
        setTimeout(enforce, 0);
        setTimeout(enforce, 50);
        setTimeout(enforce, 150);
    }

    function PatchToolbarOperationGuard() {
        var toolbar = GetToolbar();
        if (!toolbar || toolbar._xqtOperationGuardPatched || typeof toolbar.refresh !== "function") {
            return;
        }

        var originalRefresh = toolbar.refresh;
        toolbar.refresh = function () {
            EnforceToolbarOperationButtons(this);
            return originalRefresh.apply(this, arguments);
        };

        toolbar._xqtOperationGuardPatched = true;
    }

    function getStoredPublishXmlJob() {
        if (!publishXmlJobState) {
            return null;
        }

        if (publishXmlJobState.source !== "DataverseLabelTranslator" || !publishXmlJobState.jobId) {
            publishXmlJobState = null;
            return null;
        }

        return Object.assign({}, publishXmlJobState);
    }

    function isNotFoundError(error) {
        var message = String((error && (error.message || error.statusText || error)) || "");
        return /not\s+found|does\s+not\s+exist|0x80040217/i.test(message);
    }

    function retrievePublishJob(jobId) {
        return WebApiClient.Retrieve({
            apiVersion: "9.2",
            entityName: "asyncoperation",
            entityId: jobId,
            queryParams: "?$select=asyncoperationid,name,statuscode,statecode,operationtype,createdon"
        });
    }

    function retrieveImportJob(importJobId) {
        return WebApiClient.Retrieve({
            apiVersion: "9.2",
            entityName: "importjob",
            entityId: importJobId,
            queryParams: "?$select=importjobid,name,progress,completedon"
        });
    }

    function persistPublishXmlJob(job) {
        publishXmlJobState = job ? Object.assign({}, job) : null;
    }

    function extractAsyncOperationId(response) {
        if (!response) {
            return null;
        }

        if (typeof response === "string") {
            var stringMatch = response.match(/[0-9a-fA-F-]{36}/);
            return stringMatch ? stringMatch[0] : null;
        }

        var candidates = [response.AsyncOperationId, response.asyncoperationid, response.AsyncOperationID];

        for (var i = 0; i < candidates.length; i++) {
            if (candidates[i]) {
                return String(candidates[i]).replace(/[{}]/g, "");
            }
        }

        var text = JSON.stringify(response);
        var match = text.match(/[0-9a-fA-F-]{36}/);
        return match ? match[0] : null;
    }

    function getStoredOperationState() {
        if (!operationState) {
            return null;
        }

        if (operationState.source !== "DataverseLabelTranslator" || !operationState.phase) {
            operationState = null;
            return null;
        }

        if (isOperationStateStale(operationState)) {
            operationState = null;
            return null;
        }

        return Object.assign({}, operationState);
    }

    function persistOperationState(state) {
        operationState = state ? Object.assign({}, state) : null;
    }

    function clearStoredOperationState() {
        operationState = null;
    }

    function isOperationStateStale(state) {
        if (!state || state.phase === "publishing") {
            return false;
        }

        var timestamp = Date.parse(state.updatedOn || state.createdOn);
        if (isNaN(timestamp)) {
            return false;
        }

        return Date.now() - timestamp > OPERATION_STATE_MAX_AGE_MS;
    }

    function normalizeStatusTone(tone) {
        tone = String(tone || "info").toLowerCase();
        return tone === "warning" || tone === "success" || tone === "error" ? tone : "info";
    }

    function getStatusToneIcon(tone) {
        switch (normalizeStatusTone(tone)) {
            case "warning":
                return "!";
            case "success":
                return "OK";
            case "error":
                return "x";
            default:
                return "i";
        }
    }

    function ensureStatusBanner() {
        if (!document || !document.body) {
            return null;
        }

        var banner = document.getElementById("xqt-status-banner");
        if (banner) {
            return banner;
        }

        banner = document.createElement("div");
        banner.id = "xqt-status-banner";
        banner.className = "xqt-status-banner";

        var icon = document.createElement("span");
        icon.className = "xqt-status-banner-icon";
        banner.appendChild(icon);

        var text = document.createElement("span");
        text.className = "xqt-status-banner-text";
        banner.appendChild(text);

        document.body.insertBefore(banner, document.body.firstChild);

        return banner;
    }

    function showStatusBanner(options) {
        options = options || {};
        var banner = ensureStatusBanner();
        if (!banner) {
            return;
        }

        var tone = normalizeStatusTone(options.tone);
        banner.className = "xqt-status-banner xqt-status-banner-" + tone;

        var icon = banner.querySelector(".xqt-status-banner-icon");
        if (icon) {
            icon.textContent = options.icon || getStatusToneIcon(tone);
        }

        var text = banner.querySelector(".xqt-status-banner-text");
        if (text) {
            text.textContent = options.message || "";

            if (options.code) {
                var code = document.createElement("span");
                code.className = "xqt-status-banner-code";
                code.textContent = String(options.code);
                text.appendChild(code);
            }
        }

        document.body.classList.add("xqt-status-banner-visible");

        if (statusBannerAutoHideTimer) {
            clearTimeout(statusBannerAutoHideTimer);
            statusBannerAutoHideTimer = null;
        }

        var autoHideMs = parseInt(options.autoHideMs, 10);
        if (!isNaN(autoHideMs) && autoHideMs > 0) {
            statusBannerAutoHideTimer = setTimeout(function () {
                hideStatusBanner();
            }, autoHideMs);
        }
    }

    function hideStatusBanner() {
        if (statusBannerAutoHideTimer) {
            clearTimeout(statusBannerAutoHideTimer);
            statusBannerAutoHideTimer = null;
        }

        if (document && document.body) {
            document.body.classList.remove("xqt-status-banner-visible");
        }
    }

    function buildOperationState(options, existingState) {
        options = options || {};
        existingState = existingState || {};

        return {
            source: "DataverseLabelTranslator",
            phase: options.phase || existingState.phase || "running",
            type: options.type || existingState.type || null,
            toolbarType: options.toolbarType || existingState.toolbarType || null,
            entityLogicalName: options.entityLogicalName || existingState.entityLogicalName || null,
            operationId: options.operationId || existingState.operationId || null,
            importJobId: options.importJobId || existingState.importJobId || null,
            jobId: options.jobId || existingState.jobId || null,
            message: options.message || existingState.message || "",
            tone: normalizeStatusTone(options.tone || existingState.tone),
            icon: typeof options.icon !== "undefined" ? options.icon : existingState.icon || null,
            code: typeof options.code !== "undefined" ? options.code : existingState.code || null,
            blockSave: typeof options.blockSave === "boolean" ? options.blockSave : existingState.blockSave !== false,
            blockLoad: typeof options.blockLoad === "boolean" ? options.blockLoad : !!existingState.blockLoad,
            createdOn: existingState.createdOn || options.createdOn || new Date().toISOString(),
            updatedOn: new Date().toISOString()
        };
    }

    function operationBlocksSave() {
        var state = getStoredOperationState();
        return !!state && state.blockSave !== false;
    }

    function operationBlocksLoad() {
        var state = getStoredOperationState();
        return !!state && state.blockLoad === true;
    }

    function applyOperationButtons() {
        XrmTranslator.SetSaveButtonDisabled(false);
        XrmTranslator.SetLoadButtonDisabled(false);
    }

    function safeHasPendingChanges() {
        try {
            return !!(w2ui && w2ui.grid && XrmTranslator.HasPendingChanges());
        } catch (e) {
            return false;
        }
    }

    function enableSaveButtonSoon() {
        function enable() {
            XrmTranslator.SetSaveButtonDisabled(false);
        }

        enable();
        setTimeout(enable, 0);
        setTimeout(enable, 50);
        setTimeout(enable, 150);
        setTimeout(enable, 300);
        setTimeout(enable, 600);
    }

    function showOperationState(state) {
        if (!state) {
            return;
        }

        showStatusBanner({
            tone: state.tone,
            message: state.message,
            icon: state.icon,
            code: state.code
        });
        applyOperationButtons();
        EnforceToolbarOperationButtonsSoon();
    }

    function getOperationToolbarType(stateOrJob) {
        if (stateOrJob && stateOrJob.toolbarType) {
            return stateOrJob.toolbarType;
        }

        return getTypeStateLabel(stateOrJob && stateOrJob.type);
    }

    function clearPublishXmlJobPoll() {
        if (publishXmlJobPollTimer) {
            clearInterval(publishXmlJobPollTimer);
            publishXmlJobPollTimer = null;
        }
        publishXmlJobPollInFlight = false;
    }

    function pollPublishXmlJobOnce() {
        var currentJob = getStoredPublishXmlJob();
        if (!currentJob || publishXmlJobPollInFlight) {
            return;
        }

        publishXmlJobPollInFlight = true;
        XrmTranslator.RefreshPublishXmlJobState({
            silent: true,
            countAttempt: true,
            notifyForcedClear: false,
            skipInitialBannerUpdate: true
        })
            .then(function (state) {
                publishXmlJobPollInFlight = false;
                if (!state || !state.hasRunningJob) {
                    clearPublishXmlJobPoll();
                }
            })
            .catch(function (error) {
                publishXmlJobPollInFlight = false;
                if (window.console && console.warn) {
                    console.warn("Publish XML polling failed.", error);
                }
            });
    }

    function schedulePublishXmlJobPoll(job) {
        if (!job || !job.jobId || publishXmlJobPollTimer) {
            return;
        }

        publishXmlJobPollTimer = setInterval(pollPublishXmlJobOnce, PUBLISH_XML_JOB_POLL_INTERVAL_MS);
    }

    function clearImportJobPoll() {
        if (importJobPollTimer) {
            clearTimeout(importJobPollTimer);
            importJobPollTimer = null;
        }
        importJobPollInFlight = false;
    }

    function isImportJobCompleted(importJob) {
        if (!importJob) {
            return false;
        }

        var progress = parseFloat(importJob.progress);
        return !!importJob.completedon || (!isNaN(progress) && progress >= 100);
    }

    function startPublishAllXmlFromOperationState(state) {
        state = state || getStoredOperationState();
        if (!state) {
            return Promise.resolve(null);
        }

        var toolbarType = getOperationToolbarType(state) || "Ribbons";
        XrmTranslator.UpdateOperationStatus({
            phase: "startingPublish",
            tone: "success",
            icon: "...",
            message: "Publishing " + toolbarType,
            type: state.type || "ribbons",
            toolbarType: toolbarType,
            entityLogicalName: state.entityLogicalName || null,
            importJobId: state.importJobId || null,
            operationId: state.operationId || state.importJobId || null,
            blockSave: true,
            blockLoad: true
        });

        return XrmTranslator.RunAsBaseLanguage(function () {
            return WebApiClient.SendRequest(
                "POST",
                WebApiClient.GetApiUrl({ apiVersion: "9.2" }) + "PublishAllXmlAsync()",
                null
            );
        }).then(function (response) {
            var jobId = extractAsyncOperationId(response);
            if (!jobId) {
                throw new Error("PublishAllXmlAsync did not return AsyncOperationId.");
            }

            return XrmTranslator.StorePublishXmlJob({
                jobId: jobId,
                operation: "PublishAllXmlAsync",
                type: state.type || "ribbons",
                toolbarType: toolbarType,
                entityLogicalName: state.entityLogicalName || null
            });
        });
    }

    function scheduleImportJobPoll(state) {
        state = state || getStoredOperationState();
        if (!state || !state.importJobId || importJobPollTimer || importJobPollInFlight) {
            return;
        }

        importJobPollTimer = setTimeout(function () {
            importJobPollTimer = null;

            var currentState = getStoredOperationState();
            if (!currentState || currentState.phase !== "importing" || !currentState.importJobId) {
                return;
            }

            importJobPollInFlight = true;
            retrieveImportJob(currentState.importJobId)
                .then(function (importJob) {
                    importJobPollInFlight = false;

                    if (!isImportJobCompleted(importJob)) {
                        scheduleImportJobPoll(currentState);
                        return null;
                    }

                    return startPublishAllXmlFromOperationState(currentState);
                })
                .catch(function (error) {
                    importJobPollInFlight = false;
                    if (window.console && console.warn) {
                        console.warn("Import job polling failed.", error);
                    }
                    scheduleImportJobPoll(currentState);
                });
        }, IMPORT_JOB_POLL_INTERVAL_MS);
    }

    function resumeStoredOperationState() {
        var state = getStoredOperationState();
        if (!state) {
            clearImportJobPoll();
            return;
        }

        showOperationState(state);

        if (state.phase === "importing" && state.importJobId) {
            scheduleImportJobPoll(state);
        } else if (state.phase === "startingPublish") {
            startPublishAllXmlFromOperationState(state).catch(XrmTranslator.errorHandler);
        } else if (state.phase === "publishing") {
            schedulePublishXmlJobPoll(getStoredPublishXmlJob());
        }
    }

    function getPublishXmlJobAttempts(job) {
        var attempts = parseInt(job && job.blockedAttempts, 10);
        if (isNaN(attempts) || attempts < 0) {
            return 0;
        }

        return attempts;
    }

    function getPublishXmlJobAttemptsRemaining(job) {
        return Math.max(PUBLISH_XML_JOB_MAX_BLOCKED_ATTEMPTS - getPublishXmlJobAttempts(job), 0);
    }

    function incrementPublishXmlJobAttempt(job) {
        job.blockedAttempts = Math.min(getPublishXmlJobAttempts(job) + 1, PUBLISH_XML_JOB_MAX_BLOCKED_ATTEMPTS);
        job.lastCheckedOn = new Date().toISOString();
        persistPublishXmlJob(job);

        return job;
    }

    function parseAsyncOperationCode(value) {
        var number = parseInt(value, 10);
        return isNaN(number) ? null : number;
    }

    function isPublishXmlJobTerminal(asyncOperation) {
        var stateCode = parseAsyncOperationCode(asyncOperation && asyncOperation.statecode);
        var statusCode = parseAsyncOperationCode(asyncOperation && asyncOperation.statuscode);

        return stateCode === 3 || statusCode === 30 || statusCode === 31 || statusCode === 32;
    }

    function updatePublishStatusBanner(job) {
        if (!job) {
            return;
        }

        var attemptsRemaining = getPublishXmlJobAttemptsRemaining(job);
        var toolbarType = getOperationToolbarType(job) || "Publish XML";
        var state = buildOperationState(
            {
                phase: "publishing",
                tone: "success",
                icon: "...",
                type: job.type || "ribbons",
                toolbarType: toolbarType,
                entityLogicalName: job.entityLogicalName || null,
                jobId: job.jobId,
                code: job.jobId,
                blockSave: true,
                blockLoad: true,
                message:
                    "Publishing " +
                    toolbarType +
                    ". Save and Load are temporarily disabled until this server job finishes. The page will unlock automatically. Recovery checks left: " +
                    attemptsRemaining +
                    ". Job ID: "
            },
            getStoredOperationState()
        );

        clearImportJobPoll();
        persistOperationState(state);
        showOperationState(state);
        schedulePublishXmlJobPoll(job);
    }

    function NormalizeGridSearchUiSoon() {
        NormalizeGridSearchUi();
        setTimeout(NormalizeGridSearchUi, 0);
        setTimeout(NormalizeGridSearchUi, 50);
        setTimeout(NormalizeGridSearchUi, 150);
        setTimeout(NormalizeGridSearchUi, 300);
    }

    function NormalizeGridSearchUi() {
        var grid = w2ui && w2ui.grid ? w2ui.grid : null;
        var gridBox = grid && grid.box ? grid.box : null;
        if (!grid || !gridBox || !grid.name) {
            return;
        }

        ConfigureSimpleGridSearch(grid);
        RemoveGridSearchPanel(gridBox);
        EnsureSimpleGridSearchStyle(grid);

        if (grid.searchSelected) {
            grid.searchSelected = null;
            if (typeof grid.refreshSearch === "function") {
                grid.refreshSearch();
            }
        }

        if (grid.last) {
            grid.last.field = "all";
            grid.last.label = "All Fields";
        }

        var searchName = gridBox.querySelector("#grid_" + grid.name + "_search_name");
        var searchInput = gridBox.querySelector("#grid_" + grid.name + "_search_all");
        var nameText = searchName ? searchName.querySelector(".name-text") : null;
        var label = nameText ? String(nameText.textContent || "").trim() : "";
        var hasValidSearchName = label && label.toLowerCase() !== "null" && label.toLowerCase() !== "undefined";

        if (searchName) {
            searchName.style.display = "none";
        }
        if (nameText) {
            nameText.textContent = "";
        }

        if (!hasValidSearchName) {
            grid.searchSelected = null;
        }

        if (searchInput) {
            if (!hasValidSearchName) {
                searchInput.readOnly = false;
            }
            var searchValueText = String(searchInput.value || "")
                .trim()
                .toLowerCase();
            if (
                searchValueText === "null" ||
                searchValueText === "undefined" ||
                (!hasValidSearchName && searchInput.value === " ")
            ) {
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

    function EnsureSimpleGridSearchStyle(grid) {
        var styleId = "xrm-translator-simple-grid-search-style";
        if (!grid || document.getElementById(styleId)) {
            return;
        }

        var style = document.createElement("style");
        style.id = styleId;
        style.textContent =
            "#grid_" +
            grid.name +
            "_search_name{display:none!important;}" +
            "#grid_" +
            grid.name +
            "_search_all::placeholder{color:transparent!important;}";
        document.head.appendChild(style);
    }

    function RemoveGridSearchPanel(gridBox) {
        var searchPanels = gridBox.querySelectorAll(".w2ui-grid-searches");
        for (var i = 0; i < searchPanels.length; i++) {
            searchPanels[i].remove();
        }
    }

    function ConfigureSimpleGridSearch(grid) {
        if (!grid || grid._xqtSimpleSearchConfigured) {
            return;
        }

        if (grid.defaultOperator) {
            grid.defaultOperator.text = "contains";
        }
        if (grid.show) {
            grid.show.searchLogic = false;
            grid.show.searchSave = false;
        }

        grid.searchOpen = function () {};
        grid.searchShowFields = function () {};
        grid.searchSuggest = function () {};
        grid._xqtSimpleSearchConfigured = true;
    }

    function SetToolbarItemsVisible(ids, visible) {
        var toolbar = GetToolbar();
        if (!toolbar) {
            return;
        }

        for (var i = 0; i < ids.length; i++) {
            if (toolbar.get(ids[i])) {
                if (visible) {
                    toolbar.show(ids[i]);
                } else {
                    toolbar.hide(ids[i]);
                }
            }
        }
    }

    function HasSelectedSolution() {
        var solutionId = XrmTranslator.GetSolution();
        return !!solutionId && solutionId !== "all";
    }

    function SetToolbarItemsEnabled(ids, enabled) {
        var toolbar = GetToolbar();
        if (!toolbar) {
            return;
        }

        for (var i = 0; i < ids.length; i++) {
            if (!toolbar.get(ids[i])) {
                continue;
            }

            if (enabled) {
                toolbar.enable(ids[i]);
            } else {
                toolbar.disable(ids[i]);
            }
        }
    }

    function captureToolbarDisabledState() {
        var toolbar = GetToolbar();
        var state = {};

        if (!toolbar || !toolbar.items) {
            return state;
        }

        for (var i = 0; i < toolbar.items.length; i++) {
            var item = toolbar.items[i];
            if (item && item.id) {
                state[item.id] = !!item.disabled;
            }
        }

        return state;
    }

    function restoreToolbarDisabledState(state) {
        var toolbar = GetToolbar();

        if (!toolbar || !toolbar.items || !state) {
            return;
        }

        for (var i = 0; i < toolbar.items.length; i++) {
            var item = toolbar.items[i];
            if (!item || !item.id || !Object.prototype.hasOwnProperty.call(state, item.id)) {
                continue;
            }

            if (state[item.id]) {
                toolbar.disable(item.id);
            } else {
                toolbar.enable(item.id);
            }
        }

        RefreshToolbar();
    }

    function DisableAllToolbarItems() {
        var toolbar = GetToolbar();
        if (!toolbar || !toolbar.items) {
            return;
        }

        for (var i = 0; i < toolbar.items.length; i++) {
            var item = toolbar.items[i];
            if (item && item.id) {
                toolbar.disable(item.id);
            }
        }

        toolbar.refresh();
    }

    function GetRoleItems(roles) {
        var items = [];

        if (!roles) {
            return items;
        }

        if (typeof roles.getAll === "function") {
            return roles.getAll() || [];
        }

        if (Array.isArray(roles)) {
            return roles;
        }

        if (typeof roles.forEach === "function") {
            roles.forEach(function (role) {
                items.push(role);
            });
            return items;
        }

        if (typeof roles.getLength === "function" && typeof roles.get === "function") {
            for (var i = 0; i < roles.getLength(); i++) {
                items.push(roles.get(i));
            }
            return items;
        }

        if (typeof roles.get === "function") {
            var allRoles = roles.get();
            if (Array.isArray(allRoles)) {
                return allRoles;
            }
        }

        return items;
    }

    function GetRoleName(role) {
        if (!role) {
            return "";
        }

        return String(role.name || role.Name || "")
            .trim()
            .toLowerCase();
    }

    XrmTranslator.UserHasAllowedRole = function () {
        try {
            var context = typeof GetGlobalContext === "function" ? GetGlobalContext() : null;
            var userSettings = context && context.userSettings ? context.userSettings : null;
            var roles = GetRoleItems(userSettings && userSettings.roles);

            for (var i = 0; i < roles.length; i++) {
                if (ALLOWED_ROLE_NAMES[GetRoleName(roles[i])]) {
                    return true;
                }
            }
        } catch (e) {}

        return false;
    };

    function ApplyTypeVisibilityForEntity(entityTarget) {
        if (entityTarget === "entitySelect:none" || entityTarget === "none") {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, false);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, true);

            if (
                [
                    "allInOne",
                    "attributes",
                    "options",
                    "forms",
                    "views",
                    "formMeta",
                    "entityMeta",
                    "relationships",
                    "charts",
                    "bpf",
                    "content",
                    "businessRules",
                    "ribbons",
                    "commands",
                    "entityMessages"
                ].indexOf(GetToolbar().get("type").selected) !== -1
            ) {
                GetToolbar().get("type").selected = "sitemap";
                UpdateComponentDropdown("sitemap");
            }
        } else {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, true);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, false);
            SetToolbarItemsVisible(["type:content"], false);

            if (entityTarget === "entitySelect:Adx_contentsnippet" || entityTarget === "Adx_contentsnippet") {
                SetToolbarItemsVisible(["type:content"], true);
            }

            if (
                ["content", "webresources", "dashboards", "sitemap", "globalOptionSet"].indexOf(
                    GetToolbar().get("type").selected
                ) !== -1
            ) {
                GetToolbar().get("type").selected = "attributes";
                UpdateComponentDropdown("attributes");
            }
        }
    }

    function SetSolutionRequiredState(enabled) {
        SetToolbarItemsEnabled(["entitySelect", "type", "load"], enabled);

        if (!enabled) {
            SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, false);
            SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, false);
            SetToolbarItemsEnabled(["component"], false);
        }

        RefreshToolbar();
    }

    function SetToolbarLocked(locked) {
        var toolbar = GetToolbar();
        var toolbarBox = toolbar && toolbar.box ? toolbar.box : null;

        if (typeof document !== "undefined" && document.body && document.body.classList) {
            document.body.classList.toggle("xqt-grid-locked", !!locked);
        }

        if (toolbarBox && toolbarBox.classList) {
            toolbarBox.classList.toggle("xqt-toolbar-locked", !!locked);
        }
    }

    function RemoveGridLockDom(grid) {
        var gridBox = grid && grid.box ? grid.box : null;
        if (!gridBox || !gridBox.querySelectorAll) {
            return;
        }

        var lockNodes = gridBox.querySelectorAll(".w2ui-lock, .w2ui-lock-msg");
        for (var i = 0; i < lockNodes.length; i++) {
            if (lockNodes[i] && lockNodes[i].parentNode) {
                lockNodes[i].parentNode.removeChild(lockNodes[i]);
            }
        }
    }

    function PatchGridToolbarLock() {
        var grid = w2ui && w2ui.grid ? w2ui.grid : null;
        if (!grid || grid._xqtToolbarLockPatched) {
            return;
        }

        var originalLock = grid.lock.bind(grid);
        var originalUnlock = grid.unlock.bind(grid);

        grid.lock = function (message, showSpinner) {
            originalLock(message, showSpinner);
            SetToolbarLocked(true);
        };

        grid.unlock = function () {
            originalUnlock();
            RemoveGridLockDom(grid);
            SetToolbarLocked(false);
            NormalizeGridSearchUiSoon();
        };

        grid._xqtToolbarLockPatched = true;
    }

    function StripOrderPrefix(text) {
        return String(text || "").replace(/^\d+\.\s*/, "");
    }

    function CompactToolbarText(text, maxLength, keepOrderPrefix) {
        if (!keepOrderPrefix) {
            text = StripOrderPrefix(text);
        }

        maxLength = maxLength || 28;

        if (text.length <= maxLength) {
            return text;
        }

        return text.substring(0, maxLength - 1) + "...";
    }

    function GetToolbarDisplayName(text) {
        return String(text || "")
            .replace(/\s+\([^)]+\)\s*$/, "")
            .trim();
    }

    function GetEntityToolbarText(item, toolbar) {
        var el = toolbar.get("entitySelect:" + item.selected);
        if (!el) {
            return "Entity";
        }

        if (item.selected === "none") {
            return "None";
        }

        return CompactToolbarText(GetToolbarDisplayName(el.text), 26);
    }

    XrmTranslator.ComponentType = {
        Entity: 1,
        Attribute: 2,
        Relationship: 3,
        AttributePicklistValue: 4,
        AttributeLookupValue: 5,
        ViewAttribute: 6,
        LocalizedLabel: 7,
        RelationshipExtraCondition: 8,
        OptionSet: 9,
        EntityRelationship: 10,
        EntityRelationshipRole: 11,
        EntityRelationshipRelationships: 12,
        ManagedProperty: 13,
        EntityKey: 14,
        Role: 20,
        RolePrivilege: 21,
        DisplayString: 22,
        DisplayStringMap: 23,
        Form: 24,
        Organization: 25,
        SavedQuery: 26,
        Workflow: 29,
        Report: 31,
        ReportEntity: 32,
        ReportCategory: 33,
        ReportVisibility: 34,
        Attachment: 35,
        EmailTemplate: 36,
        ContractTemplate: 37,
        KBArticleTemplate: 38,
        MailMergeTemplate: 39,
        DuplicateRule: 44,
        DuplicateRuleCondition: 45,
        EntityMap: 46,
        AttributeMap: 47,
        RibbonCommand: 48,
        RibbonContextGroup: 49,
        RibbonCustomization: 50,
        RibbonRule: 52,
        RibbonTabToCommandMap: 53,
        RibbonDiff: 55,
        SavedQueryVisualization: 59,
        SystemForm: 60,
        WebResource: 61,
        SiteMap: 62,
        ConnectionRole: 63,
        FieldSecurityProfile: 70,
        FieldPermission: 71,
        PluginType: 90,
        PluginAssembly: 91,
        SDKMessageProcessingStep: 92,
        SDKMessageProcessingStepImage: 93,
        ServiceEndpoint: 95,
        RoutingRule: 150,
        RoutingRuleItem: 151,
        SLA: 152,
        SLAItem: 153,
        ConvertRule: 154,
        ConvertRuleItem: 155,
        HierarchyRule: 65,
        MobileOfflineProfile: 161,
        MobileOfflineProfileItem: 162,
        SimilarityRule: 165,
        CustomControl: 66,
        CustomControlDefaultConfig: 68,
        AppAction: 10298
    };

    XrmTranslator.GetSolution = function () {
        return GetToolbar().get("solutionSelect").selected;
    };

    XrmTranslator.GetEntity = function () {
        return GetToolbar().get("entitySelect").selected;
    };

    XrmTranslator.GetEntityId = function () {
        return XrmTranslator.entityMetadata[XrmTranslator.GetEntity()];
    };

    XrmTranslator.GetType = function () {
        return GetToolbar().get("type").selected;
    };

    XrmTranslator.GetComponent = function () {
        return GetToolbar().get("component").selected;
    };

    XrmTranslator.IsDescriptionComponent = function () {
        return XrmTranslator.GetComponent() === "Description";
    };

    XrmTranslator.IsDisplayTextComponent = function () {
        return XrmTranslator.GetComponent() === "DisplayText";
    };

    function SetHandler() {
        // Deactivate selectColumn on each change, only ContentSnippetHandler supports this right now
        w2ui.grid.show.selectColumn = false;

        w2ui["grid_toolbar"].hide("removeOverriddenAttributeLabels");
        currentHandler = null;

        if (XrmTranslator.GetType() === "allInOne") {
            currentHandler = AllInOneHandler;
        } else if (XrmTranslator.GetType() === "attributes") {
            currentHandler = AttributeHandler;
        } else if (XrmTranslator.GetType() === "options") {
            currentHandler = OptionSetHandler;
        } else if (XrmTranslator.GetType() === "forms") {
            w2ui["grid_toolbar"].show("removeOverriddenAttributeLabels");
            currentHandler = FormHandler;
        } else if (XrmTranslator.GetType() === "dashboards") {
            currentHandler = DashboardHandler;
        } else if (XrmTranslator.GetType() === "views") {
            currentHandler = ViewHandler;
        } else if (XrmTranslator.GetType() === "formMeta") {
            currentHandler = FormMetaHandler;
        } else if (XrmTranslator.GetType() === "entityMeta") {
            currentHandler = EntityHandler;
        } else if (XrmTranslator.GetType() === "relationships") {
            currentHandler = RelationshipHandler;
        } else if (XrmTranslator.GetType() === "sitemap") {
            currentHandler = SiteMapHandler;
        } else if (XrmTranslator.GetType() === "charts") {
            currentHandler = ChartHandler;
        } else if (XrmTranslator.GetType() === "bpf") {
            currentHandler = BpfHandler;
        } else if (XrmTranslator.GetType() === "businessRules") {
            currentHandler = BusinessRuleHandler;
        } else if (XrmTranslator.GetType() === "ribbons") {
            currentHandler = RibbonHandler;
        } else if (XrmTranslator.GetType() === "commands") {
            currentHandler = ModernCommandHandler;
        } else if (XrmTranslator.GetType() === "entityMessages") {
            currentHandler = EntityMessageHandler;
        } else if (XrmTranslator.GetType() === "content") {
            w2ui.grid.show.selectColumn = true;
            currentHandler = ContentSnippetHandler;
        } else if (XrmTranslator.GetType() === "webresources") {
            currentHandler = WebResourceHandler;
        } else if (XrmTranslator.GetType() === "globalOptionSet") {
            currentHandler = GlobalOptionSetHandler;
        }

        w2ui.grid.refresh();
        RefreshToolbar();
    }

    function CreatePlannedTypeHandler(typeName) {
        return {
            Load: function () {
                XrmTranslator.GetGrid().clear();
                XrmTranslator.UnlockGrid();
                w2alert(typeName + " is planned but not implemented yet.");
                return Promise.resolve();
            },
            Save: function () {
                w2alert(typeName + " is planned but not implemented yet.");
                return Promise.resolve();
            }
        };
    }

    XrmTranslator.errorHandler = function (error) {
        var message = "Unexpected error.";

        if (error) {
            if (error.statusText) {
                message = error.statusText;
            } else if (error.message) {
                message = error.message;
            } else if (typeof error === "string") {
                message = error;
            } else {
                try {
                    message = JSON.stringify(error);
                } catch (e) {
                    message = String(error);
                }
            }
        }

        if (window.console && console.error) {
            console.error(error);
        }

        w2alert(message);

        XrmTranslator.UnlockGrid();
    };

    XrmTranslator.SchemaNameComparer = function (e1, e2) {
        if (e1.SchemaName < e2.SchemaName) {
            return -1;
        }

        if (e1.SchemaName > e2.SchemaName) {
            return 1;
        }

        return 0;
    };

    XrmTranslator.EntityComparer = function (e1, e2) {
        var e1localizedLabel = e1.DisplayName.UserLocalizedLabel || {};
        var e2localizedLabel = e2.DisplayName.UserLocalizedLabel || {};

        var e1compareValue = (e1localizedLabel.Label || e1.SchemaName).toLowerCase();
        var e2compareValue = (e2localizedLabel.Label || e2.SchemaName).toLowerCase();

        if (e1compareValue < e2compareValue) {
            return -1;
        }

        if (e1compareValue > e2compareValue) {
            return 1;
        }

        return 0;
    };

    XrmTranslator.GetGrid = function () {
        return w2ui.grid;
    };

    XrmTranslator.LockGrid = function (message) {
        var grid = w2ui && w2ui.grid ? w2ui.grid : null;
        if (appLoadingActive) {
            message = "App Loading";
        }
        if (grid) {
            grid.lock(message, true);
        }
        SetToolbarLocked(true);
    };

    XrmTranslator.UnlockGrid = function () {
        var grid = w2ui && w2ui.grid ? w2ui.grid : null;
        if (grid) {
            grid.unlock();
            RemoveGridLockDom(grid);
        }
        SetToolbarLocked(false);
    };

    XrmTranslator.StartAppLoading = function () {
        appLoadingActive = true;
        appLoadingToolbarState = captureToolbarDisabledState();
        XrmTranslator.LockGrid("App Loading");
        DisableAllToolbarItems();
        XrmTranslator.SetLoadButtonDisabled(true);
        XrmTranslator.SetSaveButtonDisabled(true);
    };

    XrmTranslator.ClearAppLoading = function () {
        var toolbarState = appLoadingToolbarState;
        appLoadingActive = false;
        appLoadingToolbarState = null;
        XrmTranslator.UnlockGrid();
        restoreToolbarDisabledState(toolbarState);
    };

    XrmTranslator.GetCurrentToolbarTypeText = function () {
        var toolbar = GetToolbar();
        var typeItem = toolbar ? toolbar.get("type") : null;
        var selected = typeItem ? typeItem.selected : null;
        var items = typeItem && typeItem.items ? typeItem.items : [];

        for (var i = 0; i < items.length; i++) {
            if (items[i].id === selected) {
                return items[i].text || selected || "";
            }
        }

        return selected || getTypeStateLabel(XrmTranslator.GetType()) || "";
    };

    XrmTranslator.GetTypeStateLabel = function (type) {
        return getTypeStateLabel(type);
    };

    XrmTranslator.Delay = function (ms) {
        return new Promise(function (resolve) {
            setTimeout(resolve, ms);
        });
    };

    XrmTranslator.EnableLoadAndSave = function () {
        XrmTranslator.SetLoadButtonDisabled(false);
        XrmTranslator.SetSaveButtonDisabled(false);
    };

    XrmTranslator.RunTypeSaveFlow = function (options) {
        options = options || {};

        var toolbarType = options.toolbarType || XrmTranslator.GetCurrentToolbarTypeText();
        var publishMinimumMs = parseInt(options.publishMinimumMs, 10);
        var publishedMessageMs = parseInt(options.publishedMessageMs, 10);
        publishMinimumMs = isNaN(publishMinimumMs) ? 5000 : Math.max(publishMinimumMs, 5000);
        publishedMessageMs = isNaN(publishedMessageMs) ? 5000 : Math.max(publishedMessageMs, 5000);
        var saveAction =
            options.saveAction ||
            function () {
                return Promise.resolve();
            };
        var publishAction =
            options.publishAction ||
            function () {
                return Promise.resolve();
            };
        var reloadAction =
            options.reloadAction ||
            function () {
                return Promise.resolve();
            };
        var shouldPublish =
            typeof options.shouldPublish === "function"
                ? options.shouldPublish
                : function () {
                      return true;
                  };

        XrmTranslator.LockGrid("Saving " + toolbarType);

        return Promise.resolve()
            .then(function () {
                return saveAction();
            })
            .then(function (saveResult) {
                XrmTranslator.UnlockGrid();

                if (shouldPublish(saveResult) === false) {
                    XrmTranslator.ShowStatusBanner({
                        tone: "success",
                        icon: "0",
                        message: "No changes to save.",
                        autoHideMs: 3000
                    });
                    XrmTranslator.EnableLoadAndSave();
                    return {
                        skipRemainingFlow: true,
                        result: saveResult
                    };
                }

                XrmTranslator.StartOperationStatus({
                    phase: "publishing",
                    type: XrmTranslator.GetType(),
                    toolbarType: toolbarType,
                    blockSave: true,
                    blockLoad: true,
                    tone: "success",
                    icon: "...",
                    message: "Publishing " + toolbarType
                });

                return Promise.all([publishAction(saveResult), XrmTranslator.Delay(publishMinimumMs)]).then(
                    function () {
                        return {
                            skipRemainingFlow: false,
                            result: saveResult
                        };
                    }
                );
            })
            .then(function (flowState) {
                if (flowState && flowState.skipRemainingFlow) {
                    return flowState;
                }

                var saveResult = flowState ? flowState.result : flowState;
                XrmTranslator.UpdateOperationStatus({
                    phase: "published",
                    type: XrmTranslator.GetType(),
                    toolbarType: toolbarType,
                    blockSave: true,
                    blockLoad: true,
                    tone: "success",
                    icon: "OK",
                    message: "Published " + toolbarType
                });

                return XrmTranslator.Delay(publishedMessageMs).then(function () {
                    return {
                        skipRemainingFlow: false,
                        result: saveResult
                    };
                });
            })
            .then(function (flowState) {
                if (flowState && flowState.skipRemainingFlow) {
                    return flowState;
                }

                var saveResult = flowState ? flowState.result : flowState;
                XrmTranslator.UpdateOperationStatus({
                    phase: "reloading",
                    type: XrmTranslator.GetType(),
                    toolbarType: toolbarType,
                    blockSave: true,
                    blockLoad: true,
                    tone: "info",
                    message: "Reloading " + toolbarType
                });
                XrmTranslator.HideStatusBanner();
                XrmTranslator.LockGrid("Reloading " + toolbarType);
                return reloadAction(saveResult);
            })
            .then(function (reloadResult) {
                if (reloadResult && reloadResult.skipRemainingFlow) {
                    return reloadResult.result;
                }

                XrmTranslator.ClearOperationStatus({ hideBanner: true });
                return reloadResult;
            })
            .catch(function (error) {
                XrmTranslator.ClearOperationStatus({ hideBanner: true });
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                XrmTranslator.errorHandler(error);
            });
    };

    XrmTranslator.SetUserLanguage = function (userId, language) {
        return WebApiClient.Update({
            overriddenSetName: "usersettingscollection",
            entityId: userId,
            entity: {
                uilanguageid: language,
                helplanguageid: language
            }
        });
    };

    XrmTranslator.GetBaseLanguage = function () {
        if (XrmTranslator.baseLanguage) {
            return Promise.resolve(XrmTranslator.baseLanguage);
        }

        return WebApiClient.Retrieve({ entityName: "organization" }).then(function (orgs) {
            // Org exists always
            var org = orgs.value[0];

            XrmTranslator.baseLanguage = org.languagecode;

            return org.languagecode;
        });
    };

    XrmTranslator.SetBaseLanguage = function (userId) {
        return XrmTranslator.GetBaseLanguage().then(function (baseLanguage) {
            return XrmTranslator.SetUserLanguage(userId, baseLanguage);
        });
    };

    XrmTranslator.RestoreUserLanguage = function () {
        var initialLanguage = XrmTranslator.userSettings.uilanguageid;

        return XrmTranslator.SetUserLanguage(XrmTranslator.userId, initialLanguage);
    };

    XrmTranslator.RunAsBaseLanguage = function (action) {
        if (typeof action !== "function") {
            return Promise.resolve(null);
        }

        if (baseLanguageScopeDepth > 0) {
            baseLanguageScopeDepth++;

            return Promise.resolve()
                .then(function () {
                    return action();
                })
                .then(
                    function (result) {
                        baseLanguageScopeDepth--;
                        return result;
                    },
                    function (error) {
                        baseLanguageScopeDepth--;
                        throw error;
                    }
                );
        }

        baseLanguageScopeDepth = 1;
        baseLanguageRestoreLcid = XrmTranslator.userSettings && XrmTranslator.userSettings.uilanguageid;

        function resetScope() {
            var restoreLcid = baseLanguageRestoreLcid;
            baseLanguageRestoreLcid = null;
            baseLanguageScopeDepth = 0;

            return restoreLcid;
        }

        return XrmTranslator.SetBaseLanguage(XrmTranslator.userId)
            .then(function () {
                return action();
            })
            .then(
                function (result) {
                    var restoreLcid = resetScope();

                    if (restoreLcid == null) {
                        return result;
                    }

                    return XrmTranslator.SetUserLanguage(XrmTranslator.userId, restoreLcid).then(function () {
                        return result;
                    });
                },
                function (error) {
                    var restoreLcid = resetScope();

                    if (restoreLcid == null) {
                        throw error;
                    }

                    return XrmTranslator.SetUserLanguage(XrmTranslator.userId, restoreLcid).then(
                        function () {
                            throw error;
                        },
                        function () {
                            throw error;
                        }
                    );
                }
            );
    };

    XrmTranslator.Publish = function (globalOptionSetNames) {
        return XrmTranslator.RunAsBaseLanguage(function () {
            var options = globalOptionSetNames || [];
            var optionSetString =
                "<optionsets>" +
                options
                    .map(function (o) {
                        return "<optionset>" + o + "</optionset>";
                    })
                    .join("") +
                "</optionsets>";

            var xml =
                "<importexportxml><entities><entity>" +
                XrmTranslator.GetEntity().toLowerCase() +
                "</entity></entities>" +
                (options.length ? optionSetString : "") +
                "</importexportxml>";

            var request = WebApiClient.Requests.PublishXmlRequest.with({
                payload: {
                    ParameterXml: xml
                }
            });
            return WebApiClient.Execute(request);
        }).catch(XrmTranslator.errorHandler);
    };

    XrmTranslator.PublishDashboard = function (dashboardIds) {
        return XrmTranslator.RunAsBaseLanguage(function () {
            var xml = "<importexportxml><dashboards>";
            for (var i = 0; i < dashboardIds.length; i++) {
                xml += `<dashboard>{${dashboardIds[i].recid}}</dashboard>`;
            }
            xml += "</dashboards></importexportxml>";

            var request = WebApiClient.Requests.PublishXmlRequest.with({
                payload: {
                    ParameterXml: xml
                }
            });
            return WebApiClient.Execute(request);
        }).catch(XrmTranslator.errorHandler);
    };

    XrmTranslator.PublishWebResources = function (webresourceIds) {
        return XrmTranslator.RunAsBaseLanguage(function () {
            var xml = "<importexportxml><webresources>";
            for (var i = 0; i < webresourceIds.length; i++) {
                xml += "<webresource>" + webresourceIds[i] + "</webresource>";
            }
            xml += "</webresources></importexportxml>";

            var request = WebApiClient.Requests.PublishXmlRequest.with({
                payload: {
                    ParameterXml: xml
                }
            });
            return WebApiClient.Execute(request);
        }).catch(XrmTranslator.errorHandler);
    };

    XrmTranslator.BatchSaveSize = 25;

    XrmTranslator.CreateBatchName = function (prefix) {
        return prefix + "_" + Date.now() + "_" + Math.floor(Math.random() * 1000000);
    };

    XrmTranslator.ChunkArray = function (items, chunkSize) {
        var chunks = [];

        for (var i = 0; i < items.length; i += chunkSize) {
            chunks.push(items.slice(i, i + chunkSize));
        }

        return chunks;
    };

    XrmTranslator.ExecuteChangeSetBatches = function (items, options) {
        options = options || {};

        var batchSize = options.batchSize || XrmTranslator.BatchSaveSize;
        var batches = XrmTranslator.ChunkArray(items || [], batchSize);
        var progressLabel = options.progressLabel || "Saving batches";
        var batchNamePrefix = options.batchNamePrefix || "batch";
        var changeSetNamePrefix = options.changeSetNamePrefix || "changeset";
        var buildRequest = options.buildRequest;
        var saveIndex = 0;
        var responses = [];

        if (!buildRequest) {
            throw new Error("XrmTranslator.ExecuteChangeSetBatches requires buildRequest.");
        }

        return WebApiClient.Promise.resolve(batches)
            .each(function (batchItems, batchIndex) {
                XrmTranslator.LockGridProgress(progressLabel, ++saveIndex, batches.length);

                var requests = batchItems.map(function (item, index) {
                    var request = buildRequest(item, {
                        batchIndex: batchIndex,
                        index: index,
                        contentId: batchIndex * batchSize + index + 1
                    });

                    if (request && !request.contentId) {
                        request.contentId = batchIndex * batchSize + index + 1;
                    }

                    return request;
                });

                var changeSet = new WebApiClient.ChangeSet({
                    name: XrmTranslator.CreateBatchName(changeSetNamePrefix),
                    requests: requests
                });

                var batch = new WebApiClient.Batch({
                    name: XrmTranslator.CreateBatchName(batchNamePrefix),
                    changeSets: [changeSet]
                });

                return WebApiClient.SendBatch(batch).then(function (response) {
                    if (response && response.isFaulted) {
                        var errorMessage =
                            response.errors && response.errors.length > 0
                                ? response.errors
                                      .map(function (error) {
                                          return error.message || error.code || error;
                                      })
                                      .join("\n")
                                : progressLabel + " failed.";

                        throw new Error(errorMessage);
                    }

                    if (response && response.changeSetResponses) {
                        for (var i = 0; i < response.changeSetResponses.length; i++) {
                            responses = responses.concat(response.changeSetResponses[i].responses || []);
                        }
                    }

                    if (response && response.batchResponses) {
                        responses = responses.concat(response.batchResponses);
                    }
                });
            })
            .then(function () {
                return responses;
            });
    };

    XrmTranslator.GetRecord = function (records, selector) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (selector(record)) {
                return record;
            }
        }

        return null;
    };

    XrmTranslator.SetSaveButtonDisabled = function (disabled) {
        var toolbar = w2ui && w2ui.grid_toolbar ? w2ui.grid_toolbar : null;
        if (!toolbar) {
            return;
        }

        var saveButton = toolbar.get("w2ui-save");
        if (!saveButton) {
            return;
        }

        saveButton.disabled = false;
        toolbar.refresh();
    };

    XrmTranslator.SetLoadButtonDisabled = function (disabled) {
        var toolbar = w2ui && w2ui.grid_toolbar ? w2ui.grid_toolbar : null;
        if (!toolbar) {
            return;
        }

        var loadButton = toolbar.get("load");
        if (!loadButton) {
            return;
        }

        loadButton.disabled = false;
        toolbar.refresh();
    };

    XrmTranslator.ShowStatusBanner = function (options) {
        showStatusBanner(options);
    };

    XrmTranslator.HideStatusBanner = function () {
        hideStatusBanner();
    };

    XrmTranslator.StartOperationStatus = function (options) {
        var state = buildOperationState(options, null);
        persistOperationState(state);
        showOperationState(state);
        return state;
    };

    XrmTranslator.UpdateOperationStatus = function (options) {
        var state = buildOperationState(options, getStoredOperationState());
        persistOperationState(state);
        showOperationState(state);
        return state;
    };

    XrmTranslator.ClearOperationStatus = function (options) {
        options = options || {};
        clearImportJobPoll();
        clearPublishXmlJobPoll();
        clearStoredOperationState();

        if (options.status) {
            showStatusBanner(options.status);
        } else if (options.hideBanner !== false) {
            hideStatusBanner();
        }

        XrmTranslator.SetLoadButtonDisabled(false);
        XrmTranslator.SetSaveButtonDisabled(false);
    };

    XrmTranslator.ApplyStoredOperationStatus = function () {
        var state = getStoredOperationState();
        resumeStoredOperationState();
        return state;
    };

    XrmTranslator.CheckActiveOperationBeforeAction = function (actionName) {
        var state = getStoredOperationState();
        if (!state) {
            return Promise.resolve(true);
        }

        var action = String(actionName || "").toLowerCase();
        var isBlocked =
            (action === "save" && state.blockSave !== false) || (action === "load" && state.blockLoad === true);

        if (!isBlocked) {
            return Promise.resolve(true);
        }

        showOperationState(state);
        return Promise.resolve(false);
    };

    XrmTranslator.ShowPublishXmlStatus = function (job) {
        updatePublishStatusBanner(job || getStoredPublishXmlJob());
    };

    XrmTranslator.HidePublishXmlStatus = function () {
        hideStatusBanner();
    };

    XrmTranslator.StorePublishXmlJob = function (job) {
        if (!job || !job.jobId) {
            throw new Error("Publish XML did not return a system job id.");
        }

        var storedJob = {
            jobId: job.jobId,
            source: "DataverseLabelTranslator",
            operation: job.operation || "PublishAllXmlAsync",
            type: job.type || null,
            toolbarType: job.toolbarType || getTypeStateLabel(job.type),
            entityLogicalName: job.entityLogicalName || null,
            createdOn: job.createdOn || new Date().toISOString(),
            blockedAttempts: 0,
            lastCheckedOn: null
        };

        persistPublishXmlJob(storedJob);
        XrmTranslator.ShowPublishXmlStatus(storedJob);

        return storedJob;
    };

    XrmTranslator.ClearPublishXmlJob = function (options) {
        options = options || {};
        var job = getStoredPublishXmlJob();
        var toolbarType = getOperationToolbarType(job);
        clearPublishXmlJobPoll();
        persistPublishXmlJob(null);

        var operationState = getStoredOperationState();
        if (operationState && operationState.phase === "publishing") {
            clearStoredOperationState();
        }

        if (options.showCompleted) {
            showStatusBanner({
                tone: "success",
                message: toolbarType
                    ? "Published " + toolbarType
                    : "Publish XML completed. Save and Load are available.",
                autoHideMs: options.autoHideMs || 5000
            });
        } else if (options.hideBanner !== false) {
            hideStatusBanner();
        }

        XrmTranslator.SetLoadButtonDisabled(false);
        if (options.enableSaveAfterClear) {
            enableSaveButtonSoon();
        } else {
            XrmTranslator.SetSaveButtonDisabled(false);
        }
    };

    XrmTranslator.RefreshPublishXmlJobState = function (options) {
        options = options || {};
        var job = getStoredPublishXmlJob();

        if (!job) {
            var operationState = getStoredOperationState();
            if (operationState && operationState.phase === "publishing") {
                XrmTranslator.ClearOperationStatus();
            }
            return Promise.resolve({ hasRunningJob: false, job: null });
        }

        if (!options.skipInitialBannerUpdate) {
            XrmTranslator.ShowPublishXmlStatus(job);
        }

        return retrievePublishJob(job.jobId)
            .then(function (asyncOperation) {
                if (isPublishXmlJobTerminal(asyncOperation)) {
                    XrmTranslator.ClearPublishXmlJob({ showCompleted: true, enableSaveAfterClear: true });
                    XrmTranslator.UnlockGrid();
                    return {
                        hasRunningJob: false,
                        job: job,
                        cleared: true,
                        completed: true,
                        asyncOperation: asyncOperation
                    };
                }

                if (options.countAttempt) {
                    job = incrementPublishXmlJobAttempt(job);

                    if (getPublishXmlJobAttempts(job) >= PUBLISH_XML_JOB_MAX_BLOCKED_ATTEMPTS) {
                        XrmTranslator.ClearPublishXmlJob();
                        XrmTranslator.UnlockGrid();

                        var forcedState = {
                            hasRunningJob: false,
                            job: job,
                            cleared: true,
                            forcedCleared: true,
                            asyncOperation: asyncOperation
                        };

                        if (options.notifyForcedClear) {
                            return DialogHelper.alert(
                                "The stored Publish XML guard was cleared after " +
                                    PUBLISH_XML_JOB_MAX_BLOCKED_ATTEMPTS +
                                    " checks.\n\n" +
                                    "Job ID: " +
                                    job.jobId +
                                    "\n\n" +
                                    "Save is available again. If Dataverse is still publishing, wait a few minutes before saving ribbon changes.",
                                { title: "Publish XML guard cleared", width: 560, height: 280 }
                            ).then(function () {
                                return forcedState;
                            });
                        }

                        return forcedState;
                    }
                }

                XrmTranslator.ShowPublishXmlStatus(job);

                if (!options.silent) {
                    return DialogHelper.alert(
                        "Publish XML is still running for a previous Dataverse Label Translator ribbon save.\n\n" +
                            "Job ID: " +
                            job.jobId +
                            "\n\n" +
                            "Recovery checks left: " +
                            getPublishXmlJobAttemptsRemaining(job) +
                            "\n\n" +
                            "Please wait a few minutes before saving or loading ribbon customizations again. The page will unlock automatically when the publish job finishes.",
                        { title: "Publish XML running", width: 560, height: 260 }
                    ).then(function () {
                        return { hasRunningJob: true, job: job, asyncOperation: asyncOperation };
                    });
                }

                return { hasRunningJob: true, job: job, asyncOperation: asyncOperation };
            })
            .catch(function (error) {
                if (isNotFoundError(error)) {
                    XrmTranslator.ClearPublishXmlJob({ showCompleted: true, enableSaveAfterClear: true });
                    XrmTranslator.UnlockGrid();
                    return { hasRunningJob: false, job: job, cleared: true, completed: true };
                }

                throw error;
            });
    };

    XrmTranslator.CheckPendingPublishJobBeforeSave = function () {
        return XrmTranslator.RefreshPublishXmlJobState({
            silent: true,
            countAttempt: true,
            notifyForcedClear: true
        }).then(function (state) {
            if (!state.hasRunningJob) {
                return true;
            }

            return DialogHelper.alert(
                "Publish XML is still running for a previous Dataverse Label Translator ribbon save.\n\n" +
                    "Job ID: " +
                    state.job.jobId +
                    "\n\n" +
                    "Recovery checks left: " +
                    getPublishXmlJobAttemptsRemaining(state.job) +
                    "\n\n" +
                    "Please wait a few minutes before saving again. The page will unlock automatically when the publish job finishes.",
                { title: "Publish XML running", width: 560, height: 260 }
            ).then(function () {
                return false;
            });
        });
    };

    function HasOwnProperty(obj, property) {
        return !!obj && Object.prototype.hasOwnProperty.call(obj, property);
    }

    function GetOriginalRecordValue(record, field) {
        if (!record) {
            return undefined;
        }

        if (HasOwnProperty(record, field)) {
            return record[field];
        }

        var stringField = String(field);
        if (HasOwnProperty(record, stringField)) {
            return record[stringField];
        }

        return undefined;
    }

    function NormalizeComparableGridValue(value) {
        if (value === null || typeof value === "undefined") {
            return "";
        }

        var text = String(value);
        return typeof w2utils !== "undefined" && w2utils.decodeTags ? w2utils.decodeTags(text) : text;
    }

    function GridValuesEqual(left, right) {
        return NormalizeComparableGridValue(left) === NormalizeComparableGridValue(right);
    }

    function EncodeHtml(value) {
        if (value === null || typeof value === "undefined") {
            return "";
        }

        var text = String(value);
        return typeof w2utils !== "undefined" && w2utils.encodeTags
            ? w2utils.encodeTags(text)
            : text.replace(/&/g, "&amp;").replace(/>/g, "&gt;").replace(/</g, "&lt;").replace(/"/g, "&quot;");
    }

    function FormatChangedCellFooterValue(value) {
        if (value === null || typeof value === "undefined" || value === "") {
            return "<i>(empty)</i>";
        }

        return EncodeHtml(value);
    }

    function GetGridEventValue(event, property) {
        if (!event) {
            return undefined;
        }

        if (typeof event[property] !== "undefined") {
            return event[property];
        }

        return event.detail ? event.detail[property] : undefined;
    }

    function SetChangedCellFooter(html) {
        var grid = XrmTranslator.GetGrid();
        var footer =
            grid && grid.box ? grid.box.querySelector("#grid_" + grid.name + "_footer .w2ui-footer-center") : null;

        if (footer) {
            footer.innerHTML = html || "";
        }
    }

    function GetColumnFooterText(column) {
        var text = column ? column.text || column.field || "" : "";
        return typeof w2utils !== "undefined" && w2utils.stripTags
            ? w2utils.stripTags(text)
            : String(text).replace(/<[^>]*>/g, "");
    }

    XrmTranslator.UpdateChangedCellFooter = function (event) {
        var grid = XrmTranslator.GetGrid();
        var recid = GetGridEventValue(event, "recid");
        var columnIndex = GetGridEventValue(event, "column");
        var column =
            grid && typeof columnIndex !== "undefined" && columnIndex !== null ? grid.columns[columnIndex] : null;
        var record =
            typeof recid !== "undefined" && recid !== null
                ? XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), recid)
                : null;

        if (
            !record ||
            !column ||
            !record.w2ui ||
            !record.w2ui.changes ||
            !HasOwnProperty(record.w2ui.changes, column.field)
        ) {
            SetChangedCellFooter("");
            return;
        }

        SetChangedCellFooter(
            '<span style="display:flex;align-items:center;box-sizing:border-box;height:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left;padding:0 8px;transform:translateY(3px);">' +
                "<b>Column:&nbsp;</b>" +
                EncodeHtml(GetColumnFooterText(column)) +
                " | <b>Old Value:&nbsp;</b>" +
                FormatChangedCellFooterValue(GetOriginalRecordValue(record, column.field)) +
                " | <b>New Value:&nbsp;</b>" +
                FormatChangedCellFooterValue(record.w2ui.changes[column.field]) +
                "</span>"
        );
    };

    function ShouldKeepRibbonBlankChange(record, field, value) {
        return (
            record &&
            record._isRibbonLabelRow === true &&
            (record[field] === null || typeof record[field] === "undefined") &&
            value === ""
        );
    }

    XrmTranslator.NormalizeRecordChanges = function (record) {
        if (!record || !record.w2ui || !record.w2ui.changes) {
            return false;
        }

        var changed = false;
        var changes = record.w2ui.changes;

        for (var field in changes) {
            if (!HasOwnProperty(changes, field)) {
                continue;
            }

            if (ShouldKeepRibbonBlankChange(record, field, changes[field])) {
                continue;
            }

            if (GridValuesEqual(GetOriginalRecordValue(record, field), changes[field])) {
                delete changes[field];
                changed = true;
            }
        }

        if (Object.keys(changes).length === 0) {
            delete record.w2ui.changes;
            changed = true;
        }

        return changed;
    };

    XrmTranslator.NormalizeGridChanges = function (records) {
        records = records || XrmTranslator.GetAllRecords();
        var normalizedRecords = [];

        for (var i = 0; i < records.length; i++) {
            if (XrmTranslator.NormalizeRecordChanges(records[i])) {
                normalizedRecords.push(records[i]);
            }
        }

        return normalizedRecords;
    };

    XrmTranslator.HasPendingChanges = function (records) {
        records = records || XrmTranslator.GetAllRecords();

        for (var i = 0; i < records.length; i++) {
            XrmTranslator.NormalizeRecordChanges(records[i]);

            if (records[i].w2ui && records[i].w2ui.changes && Object.keys(records[i].w2ui.changes).length > 0) {
                return true;
            }
        }

        return false;
    };

    XrmTranslator.ApplyGridChangeValue = function (record, field, value) {
        if (!record) {
            return false;
        }

        if (GridValuesEqual(GetOriginalRecordValue(record, field), value)) {
            if (record.w2ui && record.w2ui.changes && HasOwnProperty(record.w2ui.changes, field)) {
                delete record.w2ui.changes[field];
                XrmTranslator.NormalizeRecordChanges(record);
                return true;
            }

            return false;
        }

        if (!record.w2ui) {
            record.w2ui = {};
        }

        if (!record.w2ui.changes) {
            record.w2ui.changes = {};
        }

        if (HasOwnProperty(record.w2ui.changes, field) && GridValuesEqual(record.w2ui.changes[field], value)) {
            return false;
        }

        record.w2ui.changes[field] = value;
        return true;
    };

    XrmTranslator.GetAttributeById = function (id) {
        return XrmTranslator.GetAttributeByProperty("MetadataId", id);
    };

    XrmTranslator.GetByRecId = function (records, recid) {
        function selector(rec) {
            if (rec.recid === recid) {
                return true;
            }
            return false;
        }

        return XrmTranslator.GetRecord(records, selector);
    };

    function FlattenRecords(recs) {
        return recs.reduce(function (all, cur) {
            const children = FlattenRecords(cur.w2ui && cur.w2ui.children ? cur.w2ui.children : []);

            if (children && children.length) {
                all = all.concat(children);
            }

            return all.concat([cur]);
        }, []);
    }

    XrmTranslator.GetManyByRecId = function (records, recids) {
        function buildSelector(recid) {
            return function selector(rec) {
                if (rec.recid === recid) {
                    return true;
                }
                return false;
            };
        }

        var searchRecords = records || XrmTranslator.GetAllRecords();

        return recids.reduce(function (all, cur) {
            var record = XrmTranslator.GetRecord(searchRecords, buildSelector(cur));

            if (!record) {
                return all;
            }

            // Leave out parent nodes that are not editable
            if (!record.w2ui || record.w2ui.editable == null || record.w2ui.editable) {
                all.push(record);
            }

            return all;
        }, []);
    };

    XrmTranslator.GetAttributeByProperty = function (property, value) {
        for (var i = 0; i < XrmTranslator.metadata.length; i++) {
            var attribute = XrmTranslator.metadata[i];

            if (attribute[property] === value) {
                return attribute;
            }
        }

        return null;
    };

    XrmTranslator.ApplyFindAndReplace = function (selected, results) {
        var grid = XrmTranslator.GetGrid();
        var savable = false;

        for (var i = 0; i < selected.length; i++) {
            var select = selected[i];

            var result = XrmTranslator.GetByRecId(results, select);
            var record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), result.recid);

            if (!record) {
                continue;
            }

            if (
                XrmTranslator.ApplyGridChangeValue(
                    record,
                    result.column,
                    result.w2ui && result.w2ui.changes ? result.w2ui.changes.replaced : result.replaced
                )
            ) {
                savable = true;
                grid.refreshRow(record.recid);
            }
        }

        if (savable) {
            XrmTranslator.SetSaveButtonDisabled(false);
        }
    };

    function ShowFindAndReplaceResults(results) {
        if (!w2ui.findAndReplaceGrid) {
            new w2grid({
                name: "findAndReplaceGrid",
                show: { selectColumn: true },
                multiSelect: true,
                columns: [
                    { field: "schemaName", text: "Schema Name", size: "25%", sortable: true, searchable: true },
                    { field: "column", text: "Column LCID", sortable: true, searchable: true, hidden: true },
                    { field: "columnName", text: "Column", size: "25%", sortable: true, searchable: true },
                    { field: "current", text: "Current Text", size: "25%", sortable: true, searchable: true },
                    {
                        field: "replaced",
                        text: "Replaced Text",
                        size: "25%",
                        sortable: true,
                        searchable: true,
                        editable: { type: "text" }
                    }
                ],
                records: []
            });
        }

        w2ui.findAndReplaceGrid.clear();
        w2ui.findAndReplaceGrid.add(results);

        w2popup.open({
            title: "Apply Find and Replace",
            buttons:
                '<button class="w2ui-btn" onclick="w2popup.close();">Cancel</button> ' +
                '<button class="w2ui-btn" onclick="XrmTranslator.ApplyFindAndReplace(w2ui.findAndReplaceGrid.getSelection(), w2ui.findAndReplaceGrid.records); w2popup.close();">Apply</button>',
            width: 900,
            height: 600,
            showMax: true,
            body: '<div id="main" style="position: absolute; left: 5px; top: 5px; right: 5px; bottom: 5px;"></div>',
            onOpen: function (event) {
                event.onComplete = function () {
                    w2ui.findAndReplaceGrid.render("#w2ui-popup #main");
                    w2ui.findAndReplaceGrid.selectAll();
                };
            },
            onToggle: function (event) {
                w2ui.findAndReplaceGrid.box.style.display = "none";
                event.onComplete = function () {
                    w2ui.findAndReplaceGrid.box.style.display = "";
                    w2ui.findAndReplaceGrid.resize();
                };
            }
        });
    }

    XrmTranslator.FindRecords = function (
        records,
        find,
        replace,
        useRegex,
        ignoreCase,
        column,
        columnName,
        selectRecords
    ) {
        if (!records && selectRecords) {
            XrmTranslator.ShowRecordSelector(
                "XrmTranslator.FindRecords",
                [find, replace, useRegex, ignoreCase, column, columnName, selectRecords],
                XrmTranslator.GetGrid().getSelection() || []
            );
            return;
        } else if (!records) {
            records = XrmTranslator.GetAllRecords();
        }

        var findings = [];

        var regex = null;

        if (useRegex) {
            if (ignoreCase) {
                regex = new RegExp(find, "i");
            } else {
                regex = new RegExp(find);
            }
        } else {
            if (ignoreCase) {
                regex = new RegExp(RegExp.escape(find), "i");
            } else {
                regex = new RegExp(RegExp.escape(find));
            }
        }

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            var value = record[column];

            if (record.w2ui && record.w2ui.changes && record.w2ui.changes[column]) {
                value = record.w2ui.changes[column];
            }

            if (value === null || typeof value === "undefined") {
                continue;
            }

            var replaced = null;

            replaced = value.replace(regex, replace);

            // No hit for search and replace
            if (value === replaced) {
                continue;
            }

            findings.push({
                recid: record.recid,
                schemaName: record.schemaName,
                column: column,
                columnName: columnName,
                current: value,
                replaced: replaced
            });
        }

        ShowFindAndReplaceResults(findings);
    };

    function removeHideCheckBoxFlag(r) {
        if (r.w2ui && r.w2ui.hideCheckBox) {
            r.w2ui.hideCheckBox = false;
        }

        if (r.w2ui && r.w2ui.children) {
            r.w2ui.children = r.w2ui.children.map(removeHideCheckBoxFlag);
        }

        return r;
    }

    function hasRecordSelectorText(value) {
        if (value === null || typeof value === "undefined") {
            return false;
        }

        return (
            String(value)
                .replace(/&nbsp;/gi, " ")
                .replace(/\u00a0/g, " ")
                .replace(/<[^>]*>/g, "")
                .trim().length > 0
        );
    }

    function getRecordSelectorValue(record, lang) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, lang)) {
            return record.w2ui.changes[lang];
        }

        if (
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, String(lang))
        ) {
            return record.w2ui.changes[String(lang)];
        }

        if (Object.prototype.hasOwnProperty.call(record, lang)) {
            return record[lang];
        }

        if (Object.prototype.hasOwnProperty.call(record, String(lang))) {
            return record[String(lang)];
        }

        return "";
    }

    function ResolveRecordSelectorCallback(callbackName) {
        var parts = String(callbackName || "").split(".");
        var context = window;

        for (var i = 0; i < parts.length; i++) {
            context = context[parts[i]];
            if (!context) {
                return null;
            }
        }

        return typeof context === "function" ? context : null;
    }

    XrmTranslator.ApplyRecordSelectorSelection = function () {
        var context = recordSelectorContext;
        recordSelectorContext = null;

        if (!context) {
            w2popup.close();
            return;
        }

        var callback = ResolveRecordSelectorCallback(context.callbackName);
        var selectedRecords = context.selectAllOnly
            ? context.records || []
            : XrmTranslator.GetManyByRecId(null, w2ui.recordSelectorGrid.getSelection());
        var callbackParameters = context.callbackParameters || [];

        w2popup.close();

        if (callback) {
            callback.apply(null, [selectedRecords].concat(callbackParameters));
        }
    };

    function getRecordSelectorRootRecords() {
        var selectorSourceRecords = unfilteredRecords || XrmTranslator.GetGrid().records;

        return selectorSourceRecords.filter(function (r) {
            if (r.w2ui && r.w2ui.summary) {
                return false;
            }

            // Expanded w2ui tree rows are also inserted into grid.records with
            // parent_recid. Use root rows only, then traverse w2ui.children.
            return !r.w2ui || !r.w2ui.parent_recid;
        });
    }

    function getRecordSelectorLocationLabel(record) {
        if (!record || (record.w2ui && record.w2ui.summary)) {
            return "";
        }

        return String(record.schemaName || "").trim();
    }

    function shouldIncludeRecordSelectorLocation(record) {
        if (!record) {
            return false;
        }

        var label = getRecordSelectorLocationLabel(record);
        if (!label) {
            return false;
        }

        // Skip top-level All-In-One buckets such as "Forms"; keep actual form names.
        return !record._isGroupNode || !/^\d+\.\s/.test(label);
    }

    function getRecordSelectorChildLocation(location, record) {
        if (!shouldIncludeRecordSelectorLocation(record)) {
            return location;
        }

        var label = getRecordSelectorLocationLabel(record);
        return location ? location + " > " + label : label;
    }

    function getRecordSelectorLeafRecords(records, location) {
        var result = [];

        records.forEach(function (record) {
            if (record.w2ui && record.w2ui.summary) {
                return;
            }

            var childLocation = getRecordSelectorChildLocation(location || "", record);

            if (record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0) {
                result = result.concat(getRecordSelectorLeafRecords(record.w2ui.children, childLocation));
                return;
            }

            if (!record._isGroupNode) {
                record.location = location || "";
                result.push(record);
            }
        });

        return result;
    }

    function getRecordSelectorTranslationRecords(records, includeBranchRecords, location) {
        var result = [];

        records.forEach(function (record) {
            if (record.w2ui && record.w2ui.summary) {
                return;
            }

            var hasChildren = record.w2ui && Array.isArray(record.w2ui.children) && record.w2ui.children.length > 0;
            var hasSource = hasRecordSelectorText(record.sourceText);
            var currentLocation = location || "";
            var childLocation = getRecordSelectorChildLocation(currentLocation, record);

            if (includeBranchRecords && hasChildren && !record._isGroupNode && hasSource) {
                record.location = currentLocation;
                result.push(record);
            }

            if (hasChildren) {
                result = result.concat(
                    getRecordSelectorTranslationRecords(record.w2ui.children, includeBranchRecords, childLocation)
                );
                return;
            }

            if (!record._isGroupNode) {
                record.location = currentLocation;
                result.push(record);
            }
        });

        return result;
    }

    function trimRecordSelectorDisplayValues(record) {
        ["schemaName", "sourceText", "location"].forEach(function (field) {
            if (record[field] !== null && typeof record[field] !== "undefined") {
                record[field] = String(record[field]).trim();
            }
        });

        return record;
    }

    function prepareRecordSelectorLeafRecord(record) {
        trimRecordSelectorDisplayValues(record);

        if (record.w2ui) {
            delete record.w2ui.children;
            delete record.w2ui.parent_recid;
            delete record.w2ui.expanded;
            delete record.w2ui.hideCheckBox;
            delete record.w2ui.summary;
        }

        return record;
    }

    XrmTranslator.ShowRecordSelector = function (
        callbackName,
        callbackParameters,
        preselectedRecords,
        recordFilter,
        options
    ) {
        options = options || {};
        var selectAllOnly = !!options.selectAllOnly;

        if (w2ui.recordSelectorGrid && w2ui.recordSelectorGrid._xqtSelectAllOnly !== selectAllOnly) {
            w2ui.recordSelectorGrid.destroy();
        }

        if (!w2ui.recordSelectorGrid) {
            new w2grid({
                name: "recordSelectorGrid",
                show: { selectColumn: !selectAllOnly },
                multiSelect: !selectAllOnly,
                _xqtSelectAllOnly: selectAllOnly,
                columns: [
                    { field: "location", text: "Location", size: "40%", sortable: true, searchable: true },
                    { field: "schemaName", text: "Schema Name", size: "25%", sortable: true, searchable: true },
                    { field: "sourceText", text: "Source Text", size: "35%", sortable: true, searchable: true }
                ],
                records: [],
                onSelect: function (event) {
                    const record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), event.recid);

                    if (record && record.w2ui && record.w2ui.children) {
                        w2ui.recordSelectorGrid.expand(event.recid);
                        record.w2ui.children
                            .map(function (c) {
                                return c.recid;
                            })
                            .forEach(function (id) {
                                w2ui.recordSelectorGrid.select(id);
                            });
                    }
                },
                onUnselect: function (event) {
                    const record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), event.recid);

                    if (record && record.w2ui && record.w2ui.children) {
                        w2ui.recordSelectorGrid.expand(event.recid);
                        record.w2ui.children
                            .map(function (c) {
                                return c.recid;
                            })
                            .forEach(function (id) {
                                w2ui.recordSelectorGrid.unselect(id);
                            });
                    }
                },
                onExpand: function (event) {
                    event.onComplete = function () {
                        const record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), event.recid);

                        if (record && record.w2ui && record.w2ui.children) {
                            record.w2ui.children
                                .map(function (c) {
                                    return c.recid;
                                })
                                .forEach(function (id) {
                                    w2ui.recordSelectorGrid.expand(id);
                                });
                        }
                    };
                },
                onCollapse: function (event) {
                    event.preventDefault();
                }
            });
            w2ui.recordSelectorGrid._xqtSelectAllOnly = selectAllOnly;
        }

        w2ui.recordSelectorGrid.reset(true);
        w2ui.recordSelectorGrid.clear();
        var allRecords = JSON.parse(JSON.stringify(getRecordSelectorRootRecords())).map(removeHideCheckBoxFlag);

        var sourceLang = options.sourceLcid
            ? String(options.sourceLcid)
            : XrmTranslator.baseLanguage
              ? XrmTranslator.baseLanguage.toString()
              : null;
        if (sourceLang) {
            var setSourceTextRecursive = function (record, lang) {
                record.sourceText = getRecordSelectorValue(record, lang);

                if (record.w2ui && Array.isArray(record.w2ui.children)) {
                    record.w2ui.children.forEach(function (child) {
                        setSourceTextRecursive(child, lang);
                    });
                }
            };
            allRecords.forEach(function (r) {
                setSourceTextRecursive(r, sourceLang);
            });
        }

        var filteredRecords;
        if (options.leafOnly) {
            var selectorRecords = options.includeBranchRecords
                ? getRecordSelectorTranslationRecords(allRecords, true)
                : getRecordSelectorLeafRecords(allRecords);

            filteredRecords = selectorRecords
                .filter(function (r) {
                    if (options.excludeEmptySource && !hasRecordSelectorText(r.sourceText)) {
                        return false;
                    }

                    return recordFilter ? recordFilter(r) : true;
                })
                .map(prepareRecordSelectorLeafRecord);
        } else if (recordFilter) {
            var filterRecursive = function (records) {
                return records.filter(function (r) {
                    var hasChildren = r.w2ui && Array.isArray(r.w2ui.children);

                    if (r.w2ui && Array.isArray(r.w2ui.children)) {
                        r.w2ui.children = filterRecursive(r.w2ui.children);
                        if (r.w2ui.children.length > 0) return true;
                    }

                    if (options.excludeEmptySource && !hasRecordSelectorText(r.sourceText)) {
                        return false;
                    }

                    if (hasChildren) {
                        return false;
                    }

                    return recordFilter(r);
                });
            };
            filteredRecords = filterRecursive(allRecords);
        } else {
            filteredRecords = allRecords;
        }

        filteredRecords.forEach(function (record) {
            if (!record.w2ui || !Array.isArray(record.w2ui.children)) {
                trimRecordSelectorDisplayValues(record);
            }
        });

        if ((recordFilter || options.excludeEmptySource) && filteredRecords.length === 0) {
            w2alert(
                options.emptyMessage ||
                    "No matching records found. All records already have translations for the target language."
            );
            return;
        }

        w2ui.recordSelectorGrid.add(filteredRecords);
        w2ui.recordSelectorGrid.refresh();

        recordSelectorContext = {
            callbackName: callbackName,
            callbackParameters: callbackParameters || [],
            records: filteredRecords,
            selectAllOnly: selectAllOnly
        };

        w2popup.open({
            title: options.title || (selectAllOnly ? "Records to Translate" : "Select Records"),
            buttons:
                '<button class="w2ui-btn" onclick="w2popup.close();">Cancel</button> ' +
                '<button class="w2ui-btn" onclick="XrmTranslator.ApplyRecordSelectorSelection();">OK</button>',
            width: 900,
            height: 600,
            showMax: false,
            body: '<div id="main" style="position: absolute; left: 5px; top: 5px; right: 5px; bottom: 5px;"></div>',
            onOpen: function (event) {
                event.onComplete = function () {
                    w2ui.recordSelectorGrid.render("#w2ui-popup #main");
                    w2ui.recordSelectorGrid.records.slice().forEach(function (r) {
                        w2ui.recordSelectorGrid.expand(r.recid);
                    });
                    setTimeout(function () {
                        w2popup.max();
                        w2ui.recordSelectorGrid.resize();
                    }, 100);

                    if (selectAllOnly) {
                        return;
                    }

                    if (preselectedRecords && preselectedRecords.length > 0) {
                        for (let i = 0; i < preselectedRecords.length; i++) {
                            const id = preselectedRecords[i];

                            w2ui.recordSelectorGrid.select(id);
                        }
                    } else {
                        w2ui.recordSelectorGrid.selectAll();
                    }
                };
            },
            onClose: function () {
                recordSelectorContext = null;
            },
            onToggle: function (event) {
                w2ui.recordSelectorGrid.box.style.display = "none";
                event.onComplete = function () {
                    w2ui.recordSelectorGrid.box.style.display = "";
                    w2ui.recordSelectorGrid.resize();
                };
            }
        });
    };

    var typesWithDescription = [
        "attributes",
        "options",
        "views",
        "formMeta",
        "entityMeta",
        "globalOptionSet",
        "sitemap"
    ];

    function UpdateComponentDropdown(selectedType) {
        var hasDescription = typesWithDescription.indexOf(selectedType) !== -1;
        var toolbar = GetToolbar();
        var componentItem = toolbar.get("component");
        var displayTextItem = toolbar.get("component:DisplayText");

        if (displayTextItem) {
            displayTextItem.text = "Display Text";
        }

        if (hasDescription) {
            toolbar.enable("component");
        } else {
            if (componentItem) {
                componentItem.selected = "DisplayText";
            }
            toolbar.disable("component");
        }
        RefreshToolbar();
    }

    function InitializeFindAndReplaceDialog() {
        var languageItems = [];
        var availableLanguages = XrmTranslator.GetGrid().columns;

        for (var i = 0; i < availableLanguages.length; i++) {
            if (availableLanguages[i].field === "schemaName") {
                continue;
            }

            languageItems.push({ id: availableLanguages[i].field, text: availableLanguages[i].text });
        }

        if (!w2ui.findAndReplace) {
            new w2form({
                name: "findAndReplace",
                style: "border: 0px; background-color: transparent;",
                formHTML:
                    '<div class="w2ui-page page-0 xqt-find-replace-form">' +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Replace in Column <span class="xqt-required">*</span></label>' +
                    '        <input name="column" type="list"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Find <span class="xqt-required">*</span></label>' +
                    '        <input name="find" type="text"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Replace <span class="xqt-required">*</span></label>' +
                    '        <input name="replace" type="text"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-options">' +
                    '        <label><input name="regex" type="checkbox"/> Use Regex</label>' +
                    '        <label><input name="ignoreCase" type="checkbox"/> Ignore Case</label>' +
                    '        <label><input name="selectRecords" type="checkbox"/> Select Records</label>' +
                    "    </div>" +
                    "</div>" +
                    '<div class="w2ui-buttons xqt-find-replace-buttons">' +
                    '    <button class="w2ui-btn" name="cancel">Cancel</button>' +
                    '    <button class="w2ui-btn" name="ok">Ok</button>' +
                    "</div>",
                fields: [
                    { field: "find", type: "text", required: true },
                    { field: "replace", type: "text", required: true },
                    { field: "regex", type: "checkbox", required: true },
                    { field: "ignoreCase", type: "checkbox", required: true },
                    { field: "selectRecords", type: "checkbox", required: false },
                    { field: "column", type: "list", required: true, options: { items: languageItems } }
                ],
                actions: {
                    ok: function () {
                        var errors = this.validate();
                        if (errors.length > 0 || !this.record.column) {
                            return;
                        }

                        w2popup.close();
                        XrmTranslator.FindRecords(
                            undefined,
                            this.record.find,
                            this.record.replace,
                            this.record.regex,
                            this.record.ignoreCase,
                            this.record.column.id,
                            this.record.column.text,
                            this.record.selectRecords
                        );
                    },
                    cancel: function () {
                        w2popup.close();
                    }
                }
            });
        } else {
            // Columns will be different when user switches to portal content snippet or back from it, we need to make sure columns always match current grid columns
            var columnField = w2ui.findAndReplace.fields.find(function (field) {
                return field.field === "column";
            });

            if (columnField) {
                columnField.options.items = languageItems;
            }

            w2ui.findAndReplace.refresh();
        }

        return Promise.resolve({});
    }

    function OpenFindAndReplaceDialog() {
        InitializeFindAndReplaceDialog().then(function () {
            w2popup.open({
                title: "Find and Replace",
                name: "findAndReplacePopup",
                body: '<div id="form" class="xqt-find-replace-popup-form"></div>',
                style: "padding: 0",
                width: 720,
                height: 305,
                showMax: false,
                onToggle: function (event) {
                    w2ui.findAndReplace.box.style.display = "none";
                    event.onComplete = function () {
                        w2ui.findAndReplace.box.style.display = "";
                        w2ui.findAndReplace.resize();
                    };
                },
                onOpen: function (event) {
                    event.onComplete = function () {
                        var popup = document.querySelector("#w2ui-popup");
                        if (popup) {
                            popup.classList.add("xqt-find-replace-popup");
                        }
                        w2ui.findAndReplace.render("#w2ui-popup #form");
                    };
                },
                onClose: function () {
                    var popup = document.querySelector("#w2ui-popup");
                    if (popup) {
                        popup.classList.remove("xqt-find-replace-popup");
                    }
                }
            });
        });
    }

    function isRecordUntranslated(record, targetColumns) {
        for (var i = 0; i < targetColumns.length; i++) {
            var col = targetColumns[i];
            var val;

            if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, col)) {
                val = record.w2ui.changes[col];
            } else {
                val = record[col];
            }

            if (!val) {
                return true;
            }
        }

        return false;
    }

    function filterRecordsRecursive(records, targetColumns) {
        var result = [];

        for (var i = 0; i < records.length; i++) {
            var rec = records[i];

            if (rec.w2ui && rec.w2ui.summary) {
                continue;
            }

            if (rec.w2ui && rec.w2ui.children && rec.w2ui.children.length > 0) {
                // Node with children: recurse, keep only if any leaf descendant is untranslated
                var filteredChildren = filterRecordsRecursive(rec.w2ui.children, targetColumns);

                if (filteredChildren.length > 0) {
                    var clone = JSON.parse(JSON.stringify(rec));
                    clone.w2ui.children = filteredChildren;
                    result.push(clone);
                }
            } else if (!rec._isGroupNode) {
                // Leaf node: check if untranslated (skip structural group nodes)
                if (isRecordUntranslated(rec, targetColumns)) {
                    result.push(rec);
                }
            }
        }

        return result;
    }

    function ToggleUntranslatedFilter() {
        var grid = XrmTranslator.GetGrid();

        if (!unfilteredRecords) {
            // Activating: store originals and filter
            var baseLcid = XrmTranslator.baseLanguage ? XrmTranslator.baseLanguage.toString() : null;
            var targetColumns = XrmTranslator.GetColumns(false).map(function (c) {
                return String(c);
            });

            if (baseLcid) {
                targetColumns = targetColumns.filter(function (c) {
                    return c !== baseLcid;
                });
            }

            if (targetColumns.length === 0) {
                w2alert("No target language columns to filter on.");
                return;
            }

            unfilteredRecords = JSON.parse(JSON.stringify(grid.records));

            // Use only root-level records for filtering. When w2ui expands
            // tree nodes it splices children into grid.records as flat entries
            // (with parent_recid set). Filtering via w2ui.children already
            // reaches those children, so we skip the flat duplicates here.
            var rootRecords = grid.records.filter(function (r) {
                return !r.w2ui || !r.w2ui.parent_recid;
            });

            var filtered = filterRecordsRecursive(rootRecords, targetColumns);

            grid.clear();
            grid.add(filtered);
            XrmTranslator.AddSummary(filtered);
            grid.refresh();
        } else {
            // Deactivating: restore originals
            if (unfilteredRecords) {
                grid.clear();
                grid.add(unfilteredRecords);
                unfilteredRecords = null;
                grid.refresh();
            }
        }
    }

    function LoadHandler() {
        if (XrmTranslator.hasAllowedRole === false) {
            return;
        }

        var entity = XrmTranslator.GetEntity();

        if (!HasSelectedSolution()) {
            return DialogHelper.alert("Please select a solution before loading.");
        }

        if (!entity || !XrmTranslator.GetType()) {
            return;
        }

        return XrmTranslator.CheckActiveOperationBeforeAction("Load").then(function (canLoad) {
            if (canLoad) {
                TriggerLoading(entity);
            }
        });
    }

    function GetColumnDisplayName(field) {
        var columns = XrmTranslator.GetGrid().columns || [];
        var fieldText = String(field);

        for (var i = 0; i < columns.length; i++) {
            if (String(columns[i].field) === fieldText) {
                return columns[i].text || columns[i].caption || columns[i].label || fieldText;
            }
        }

        return fieldText;
    }

    function DecodeDictionaryText(value) {
        if (value === null || typeof value === "undefined") {
            return "";
        }

        var text = String(value);
        return typeof w2utils !== "undefined" && w2utils.decodeTags ? w2utils.decodeTags(text) : text;
    }

    function HasDictionaryText(value) {
        return (
            DecodeDictionaryText(value)
                .replace(/&nbsp;/gi, " ")
                .replace(/\u00a0/g, " ")
                .replace(/<[^>]*>/g, "")
                .trim().length > 0
        );
    }

    function GetDictionaryGridValue(record, field) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, field)) {
            return record.w2ui.changes[field];
        }

        if (Object.prototype.hasOwnProperty.call(record, field)) {
            return record[field];
        }

        var stringField = String(field);
        if (
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, stringField)
        ) {
            return record.w2ui.changes[stringField];
        }

        if (Object.prototype.hasOwnProperty.call(record, stringField)) {
            return record[stringField];
        }

        return "";
    }

    function EscapeHtml(text) {
        return String(text || "").replace(/[&<>"]/g, function (m) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[m];
        });
    }

    function GetRecordFieldValue(record, field) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, field)) {
            return record.w2ui.changes[field];
        }

        if (Object.prototype.hasOwnProperty.call(record, field)) {
            return record[field];
        }

        var stringField = String(field);
        if (
            record.w2ui &&
            record.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(record.w2ui.changes, stringField)
        ) {
            return record.w2ui.changes[stringField];
        }

        if (Object.prototype.hasOwnProperty.call(record, stringField)) {
            return record[stringField];
        }

        return "";
    }

    XrmTranslator.RenderTranslationCell = function (record, field) {
        var value = GetRecordFieldValue(record, field);

        if (value !== null && typeof value !== "undefined" && String(value).length > 0) {
            return EscapeHtml(value);
        }

        var stringField = String(field);
        if (
            record &&
            record._emptyEditablePlaceholders &&
            Object.prototype.hasOwnProperty.call(record._emptyEditablePlaceholders, stringField)
        ) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-editable" title="Click to edit">' +
                EscapeHtml(record._emptyEditablePlaceholders[stringField]) +
                "</span>"
            );
        }

        if (record && record._emptyEditablePlaceholder) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-editable" title="Click to edit">' +
                EscapeHtml(record._emptyEditablePlaceholder) +
                "</span>"
            );
        }

        if (record && record._emptyReadonlyPlaceholder) {
            return (
                '<span class="xqt-empty-cell-hint xqt-empty-cell-hint-readonly" title="Read only">' +
                EscapeHtml(record._emptyReadonlyPlaceholder) +
                "</span>"
            );
        }

        return "";
    };

    XrmTranslator.CreateTranslationCellRenderer = function (field) {
        return function (record) {
            return XrmTranslator.RenderTranslationCell(record, field);
        };
    };

    function BuildDictionaryValueBox(label, value) {
        return (
            '<div style="margin: 0 0 14px 0;">' +
            '<div style="font-weight: 600; color: #444; margin-bottom: 5px;">' +
            EscapeHtml(label) +
            "</div>" +
            '<div style="border: 1px solid #d0d7de; background: #f8f9fb; border-radius: 4px; padding: 9px 11px; font-weight: 600; color: #111; white-space: pre-wrap;">' +
            EscapeHtml(value) +
            "</div>" +
            "</div>"
        );
    }

    function ConfirmAddSelectedTranslationToDictionary(sourceLabel, sourceText, targetItems) {
        var body =
            '<div style="padding: 22px 28px 18px 28px; font-size: 15px; line-height: 1.45;">' +
            '<div style="font-size: 18px; font-weight: 600; margin-bottom: 18px;">Add this translation to dictionary?</div>' +
            BuildDictionaryValueBox("Source (" + sourceLabel + ")", sourceText);

        for (var i = 0; i < targetItems.length; i++) {
            body += BuildDictionaryValueBox(targetItems[i].label, targetItems[i].value);
        }

        body += "</div>";

        return new Promise(function (resolve) {
            var result = false;

            w2popup.open({
                title: "Add to Dictionary",
                body: body,
                buttons:
                    '<button class="w2ui-btn" onclick="w2popup._xqtAddDictionaryResult=false; w2popup.close();">No</button> ' +
                    '<button class="w2ui-btn" onclick="w2popup._xqtAddDictionaryResult=true; w2popup.close();">Add</button>',
                width: 720,
                height: 560,
                modal: true,
                showClose: true,
                showMax: false,
                onOpen: function () {
                    w2popup._xqtAddDictionaryResult = false;
                },
                onClose: function () {
                    result = !!w2popup._xqtAddDictionaryResult;
                    w2popup._xqtAddDictionaryResult = null;
                    resolve(result);
                }
            });
        });
    }

    function ShowAddSelectedTranslationToDictionary() {
        if (!window.TranslationDictionaryService || !TranslationDictionaryService.UpsertEntries) {
            return DialogHelper.alert("Dictionary service is not available.", { title: "Dictionary" });
        }

        var grid = XrmTranslator.GetGrid();
        var selected = grid.getSelection ? grid.getSelection() || [] : [];

        if (selected.length !== 1) {
            return DialogHelper.alert("Please select exactly one translatable row.", { title: "Dictionary" });
        }

        var selectedId = selected[0] && selected[0].recid ? selected[0].recid : selected[0];
        var record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), selectedId);

        if (!record) {
            return DialogHelper.alert("Selected row was not found.", { title: "Dictionary" });
        }

        if (
            (record.w2ui && record.w2ui.summary) ||
            record._isGroupNode ||
            (record.w2ui && record.w2ui.editable === false)
        ) {
            return DialogHelper.alert(
                "Selected row is a group row and cannot be added to dictionary. Please select a translatable label row.",
                { title: "Dictionary" }
            );
        }

        return XrmTranslator.GetBaseLanguage()
            .then(function (baseLanguage) {
                var baseLcid = String(baseLanguage);
                var sourceText = DecodeDictionaryText(GetDictionaryGridValue(record, baseLcid)).trim();

                if (!HasDictionaryText(sourceText)) {
                    return DialogHelper.alert(
                        "Selected row does not have source text in " + GetColumnDisplayName(baseLcid) + ".",
                        { title: "Dictionary" }
                    );
                }

                var targetColumns = XrmTranslator.GetColumns(false)
                    .map(function (field) {
                        return String(field);
                    })
                    .filter(function (field) {
                        return field !== baseLcid && /^\d+$/.test(field);
                    });

                if (!targetColumns.length) {
                    return DialogHelper.alert("No target language columns found.", { title: "Dictionary" });
                }

                var targets = {};
                var targetItems = [];
                var targetNames = [];

                for (var i = 0; i < targetColumns.length; i++) {
                    var targetLcid = targetColumns[i];
                    var targetName = GetColumnDisplayName(targetLcid);
                    var targetText = DecodeDictionaryText(GetDictionaryGridValue(record, targetLcid)).trim();

                    targetNames.push(targetName);

                    if (!HasDictionaryText(targetText)) {
                        continue;
                    }

                    targets[targetLcid] = targetText;
                    targetItems.push({
                        label: targetName,
                        value: targetText
                    });
                }

                if (!targetItems.length) {
                    return DialogHelper.alert(
                        "No translated value found for target languages: " + targetNames.join(", ") + ".",
                        { title: "Dictionary" }
                    );
                }

                return ConfirmAddSelectedTranslationToDictionary(
                    GetColumnDisplayName(baseLcid),
                    sourceText,
                    targetItems
                ).then(function (confirmed) {
                    if (!confirmed) {
                        return null;
                    }

                    XrmTranslator.LockGrid("Updating dictionary...");

                    return TranslationDictionaryService.UpsertEntries([
                        {
                            sourceText: sourceText,
                            targets: targets
                        }
                    ])
                        .then(function (result) {
                            XrmTranslator.UnlockGrid();
                            return DialogHelper.alert(
                                "Dictionary updated.\n\nSource: " +
                                    sourceText +
                                    "\nTargets saved: " +
                                    result.targetCount,
                                { title: "Dictionary" }
                            );
                        })
                        .catch(function (error) {
                            XrmTranslator.UnlockGrid();
                            var message = error && error.message ? error.message : String(error);
                            return DialogHelper.alert(message, { title: "Dictionary" });
                        });
                });
            })
            .catch(function (error) {
                var message = error && error.message ? error.message : String(error);
                return DialogHelper.alert(message, { title: "Dictionary" });
            });
    }

    function ShowAbout() {
        var html =
            '<div style="padding: 25px 30px; font-size: 16px; line-height: 1.6; text-align: center;">' +
            '<h2 style="margin: 0 0 10px 0; font-size: 26px; font-weight: 600;">Dataverse Label Translator</h2>' +
            '<p style="margin: 0 0 10px 0; color: #777; font-size: 15px;">Version: 1.0.0.0</p>' +
            '<p style="margin: 0 0 15px 0; color: #777; font-size: 15px;">Complete Translation Management UI for Dynamics 365 / Dataverse</p>' +
            '<hr style="border: none; border-top: 1px solid #eaeaea; margin: 20px 0;">' +
            '<p style="text-align: justify; text-align-last: center; font-size: 15px; margin: 0; color: #444;">Developed by ' +
            '<a href="https://github.com/phuocle" target="_blank" rel="noopener noreferrer" style="font-weight: 500; text-decoration: none;">Phuoc Le</a>, ' +
            "featuring AI-powered translation, intelligent dictionary management, All-In-One bulk translation mode, " +
            "and a beautifully optimized workflow.</p>" +
            "</div>";

        w2popup.open({
            title: "About",
            body: html,
            width: 580,
            height: 310,
            modal: true,
            showClose: true,
            showMax: false,
            buttons: '<button class="w2ui-btn" onclick="w2popup.close();">Close</button>',
            onOpen: function (event) {
                event.onComplete = function () {
                    setTimeout(function () {
                        w2popup.max();
                    }, 100);
                };
            }
        });
    }

    function ShowHelp() {
        var html =
            '<div style="padding: 15px 20px; font-size: 13px; line-height: 1.8;">' +
            "<b>Solution filter:</b> Select a Solution first. The Entity list and solution-level types are scoped to that solution." +
            '<hr style="margin: 8px 0; border: none; border-top: 1px solid #ddd;">' +
            "<b>Entity-based types</b> (select an Entity first):" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>All-In-One</b> — Loads the current bulk-supported entity types into one grid for translation, including Business Rules.</li>" +
            "<li><b>Attributes</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Attributes &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Options</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Options &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Forms</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Forms &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Views</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Views &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Form Metadata</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Form Metadata &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Entity Metadata</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Entity Metadata &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Relationships</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Relationships &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Charts</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Charts &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Business Process Flows</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Business Process Flows &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Business Rules</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Business Rules &rarr; Load &rarr; Translate &rarr; Save. Save temporarily deactivates each changed rule, patches workflow XAML, then reactivates it.</li>" +
            "<li><b>Ribbons</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Ribbons &rarr; Load &rarr; Translate &rarr; Save. Save downloads a backup first, then starts Publish XML asynchronously.</li>" +
            "<li><b>Commands</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Commands &rarr; Load &rarr; Translate &rarr; Save. Loads modern command designer appaction labels and publishes the selected entity.</li>" +
            "<li><b>Entity Messages</b> — Solution &rarr; Entity &rarr; <i>[entity]</i> &rarr; Type &rarr; Entity Messages &rarr; Load &rarr; Translate &rarr; Save. Loads table messages/display strings from the selected solution translation package, imports changed translations, then publishes the selected entity.</li>" +
            "<li><b>Content Snippets</b> — Solution &rarr; Entity &rarr; Adx_contentsnippet &rarr; Type &rarr; Content Snippets &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "</ul>" +
            "<b>Entity-independent types</b> (set Entity to None):" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Sitemap</b> — Solution &rarr; Entity &rarr; None &rarr; Type &rarr; Sitemap &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Dashboards</b> — Solution &rarr; Entity &rarr; None &rarr; Type &rarr; Dashboards &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Web Resources</b> — Solution &rarr; Entity &rarr; None &rarr; Type &rarr; Web Resources &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "<li><b>Global Option Set</b> — Solution &rarr; Entity &rarr; None &rarr; Type &rarr; Global Option Set &rarr; Load &rarr; Translate &rarr; Save</li>" +
            "</ul>" +
            '<hr style="margin: 8px 0; border: none; border-top: 1px solid #ddd;">' +
            "<b>AI Translate:</b>" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Auto Translate</b> — Uses the selected enabled provider to translate labels from a source language to a target language. " +
            "Configure provider credentials via <i>App Settings</i>.</li>" +
            "<li><b>App Settings</b> — Configure URLs, API keys, model names, and custom prompts for enabled providers. " +
            "Settings are stored in the Dataverse app settings web resource.</li>" +
            "</ul>" +
            "<b>Dictionary:</b>" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Dictionary</b> — Open and manage translation dictionary entries (source &rarr; target term pairs). " +
            "When <i>Use Dictionary as First Priority</i> is enabled in Auto Translate, dictionary matches are applied before calling the AI provider.</li>" +
            "<li><b>Apply Dictionary</b> — Batch-apply existing dictionary entries to all matching records in the current grid without calling AI. " +
            "Supports two modes: <i>All Overwrite</i> and <i>All Missing</i>.</li>" +
            "<li><b>Storage:</b> Dictionary data is saved as a web resource (<code>pl_/DataverseLabelTranslator/data/TranslationDictionary.xml</code>) " +
            "inside an unmanaged solution named <b>Dataverse Label Translator Data</b> (unique name: <code>DataverseLabelTranslatorData</code>). " +
            "This solution is auto-created on first use.</li>" +
            "</ul>" +
            "</div>";

        w2popup.open({
            title: "Translation Guide",
            body: html,
            width: 700,
            height: 520,
            modal: true,
            showClose: true,
            showMax: false,
            onOpen: function (event) {
                event.onComplete = function () {
                    setTimeout(function () {
                        w2popup.max();
                    }, 100);
                };
            }
        });
    }

    function TriggerLoading(entity) {
        unfilteredRecords = null;
        var filterBtn = w2ui.grid_toolbar ? w2ui.grid_toolbar.get("filterUntranslated") : null;
        if (filterBtn) {
            filterBtn.checked = false;
            w2ui.grid_toolbar.refresh();
        }

        let promise = undefined;

        if (XrmTranslator.columnRestoreNeeded) {
            XrmTranslator.ClearColumns();
            promise = TranslationHandler.FillLanguageCodes(
                XrmTranslator.installedLanguages.LocaleIds,
                XrmTranslator.userSettings
            );
        } else {
            promise = Promise.resolve(null);
        }

        function loadSelectedHandler() {
            XrmTranslator.columnRestoreNeeded = false;
            XrmTranslator.entity = entity;
            SetHandler();

            XrmTranslator.LockGrid(
                XrmTranslator.GetType() === "globalOptionSet"
                    ? "Loading ..."
                    : "Loading " + XrmTranslator.GetCurrentToolbarTypeText()
            );

            // Reset column sorting
            XrmTranslator.GetGrid().sort();
            return currentHandler.Load();
        }

        if (XrmTranslator.GetType() === "globalOptionSet") {
            promise.then(loadSelectedHandler).catch(XrmTranslator.errorHandler);
            return;
        }

        promise
            .then(function () {
                return XrmTranslator.RefreshPublishXmlJobState({
                    silent: true,
                    countAttempt: true,
                    notifyForcedClear: true
                });
            })
            .then(function (publishState) {
                if (publishState && publishState.hasRunningJob) {
                    XrmTranslator.UnlockGrid();
                    XrmTranslator.SetSaveButtonDisabled(true);
                    return publishState;
                }

                if (publishState && publishState.cleared) {
                    XrmTranslator.SetSaveButtonDisabled(false);
                }

                return publishState;
            })
            .then(function (publishState) {
                if (publishState && publishState.hasRunningJob) {
                    return;
                }

                return loadSelectedHandler();
            })
            .catch(XrmTranslator.errorHandler);
    }

    function HandleToolbarClick(event) {
        if (XrmTranslator.hasAllowedRole === false) {
            return;
        }

        var target = String(event.target || "");

        if (target === "about") {
            ShowAbout();
            return;
        }

        if (target === "help") {
            ShowHelp();
            return;
        }

        if (target.startsWith("solutionSelect:")) {
            var selectedSolutionId = target.replace("solutionSelect:", "");
            RepopulateEntitySelector(selectedSolutionId);
            return;
        }

        if (target.startsWith("type:")) {
            var selectedType = target.replace("type:", "");
            UpdateComponentDropdown(selectedType);
            return;
        }

        if (target.startsWith("entitySelect:")) {
            if (!HasSelectedSolution()) {
                return;
            }

            ApplyTypeVisibilityForEntity(target);
            RefreshToolbar();
            return;
        }

        if (target.indexOf("expandAll") !== -1) {
            ToggleExpandCollapse(true);
            return;
        }

        if (target.indexOf("collapseAll") !== -1) {
            ToggleExpandCollapse(false);
            return;
        }

        switch (target) {
            case "autoTranslate":
                TranslationHandler.ShowTranslationPrompt();
                break;
            case "aiSettings":
                TranslationHandler.ShowAppSettings();
                break;
        }
    }

    function InitializeGrid(entities) {
        var toolbarItems = [
            {
                type: "menu-radio",
                id: "solutionSelect",
                icon: "icon-solution",
                tooltip: "Solution",
                text: function (item) {
                    var el = this.get("solutionSelect:" + item.selected);
                    if (el) {
                        return CompactToolbarText(GetToolbarDisplayName(el.text), 24);
                    }
                    return "Solution";
                },
                selected: null,
                items: []
            },
            {
                type: "menu-radio",
                id: "entitySelect",
                icon: "icon-entity",
                tooltip: "Entity",
                text: function (item) {
                    return GetEntityToolbarText(item, this);
                },
                selected: "none",
                items: [{ id: "none", text: "None", icon: "icon-empty" }, { text: "--" }]
            },
            {
                type: "menu-radio",
                id: "type",
                icon: "icon-type",
                tooltip: "Translation type",
                text: function (item) {
                    var el = this.get("type:" + item.selected);
                    return el ? CompactToolbarText(el.text, 18, true) : "Type";
                },
                selected: "sitemap",
                items: (XrmTranslator.showAllInOneType
                    ? [
                          { id: "allInOne", text: "All-In-One", icon: "icon-grid" },
                          { id: "entitySeparator", text: "--" }
                      ]
                    : []
                ).concat([
                    { id: "attributes", text: "Attributes", icon: "icon-attribute" },
                    { id: "options", text: "Option Sets", icon: "icon-options" },
                    { id: "forms", text: "Forms", icon: "icon-form" },
                    { id: "views", text: "Views", icon: "icon-view" },
                    { id: "formMeta", text: "Form Metadata", icon: "icon-layout" },
                    { id: "entityMeta", text: "Entity Metadata", icon: "icon-entity" },
                    { id: "relationships", text: "Relationships", icon: "icon-link" },
                    { id: "charts", text: "Charts", icon: "icon-chart" },
                    { id: "bpf", text: "Business Process Flows", icon: "icon-flow" },
                    { id: "businessRules", text: "Business Rules", icon: "icon-flow" },
                    { id: "ribbons", text: "Ribbons", icon: "icon-grid" },
                    { id: "commands", text: "Commands", icon: "icon-component" },
                    { id: "entityMessages", text: "Entity Messages", icon: "icon-description" },
                    { id: "content", text: "Content Snippets", icon: "icon-code" },
                    { id: "sitemap", text: "Sitemap", icon: "icon-sitemap" },
                    { id: "dashboards", text: "Dashboards", icon: "icon-dashboard" },
                    { id: "webresources", text: "Web Resources", icon: "icon-file-code" },
                    { id: "globalOptionSet", text: "Global Option Set", icon: "icon-global-options" }
                ])
            },
            {
                type: "menu-radio",
                id: "component",
                icon: "icon-component",
                tooltip: "Component",
                text: function (item) {
                    var el = this.get("component:" + item.selected);
                    return el ? CompactToolbarText(el.text, 18) : "Component";
                },
                selected: "DisplayText",
                items: [
                    { id: "DisplayText", text: "Display Text", icon: "icon-label" },
                    { id: "Description", text: "Description", icon: "icon-description" }
                ]
            },
            { type: "break" },
            {
                type: "button",
                id: "load",
                text: "Load",
                tooltip: "Load selected data",
                icon: "icon-load",
                onClick: LoadHandler
            },
            { type: "break", id: "break-context" }
        ];

        toolbarItems.push({
            type: "button",
            hidden: true,
            id: "removeOverriddenAttributeLabels",
            text: "",
            tooltip: "Remove overridden attribute labels",
            icon: "icon-eraser",
            onClick: function (event) {
                FormHandler.RemoveOverriddenCellLabels();
            }
        });

        toolbarItems.push({
            type: "button",
            id: "autoTranslate",
            text: "",
            tooltip: "Auto Translate",
            icon: "icon-translate"
        });
        toolbarItems.push({
            type: "button",
            id: "aiSettings",
            text: "",
            tooltip: "App Settings",
            icon: "w2ui-icon-settings"
        });

        toolbarItems.push({ type: "break" });

        toolbarItems.push({
            type: "button",
            id: "applyDictionary",
            text: "",
            tooltip: "Apply dictionary",
            icon: "icon-book-check",
            onClick: function () {
                TranslationHandler.ShowApplyDictionaryPrompt();
            }
        });

        toolbarItems.push({
            type: "button",
            id: "addSelectedDictionary",
            text: "",
            tooltip: "Add selected translation to dictionary",
            icon: "icon-book-plus",
            onClick: function () {
                ShowAddSelectedTranslationToDictionary();
            }
        });

        toolbarItems.push({
            type: "button",
            id: "dictionary",
            text: "",
            tooltip: "Manage dictionary",
            icon: "icon-book",
            onClick: function () {
                if (window.TranslationDictionaryService && TranslationDictionaryService.ShowDictionaryPrompt) {
                    TranslationDictionaryService.ShowDictionaryPrompt();
                }
            }
        });

        toolbarItems.push({ type: "break", id: "break-filter" });
        toolbarItems.push({
            type: "check",
            id: "filterUntranslated",
            icon: "icon-funnel",
            tooltip: "Show only untranslated records",
            onClick: function () {
                ToggleUntranslatedFilter();
            }
        });

        toolbarItems.push({ type: "spacer" });

        toolbarItems.push(
            { type: "button", id: "about", text: "", tooltip: "About", icon: "icon-about" },
            { type: "button", id: "help", text: "", tooltip: "Help", icon: "icon-help" }
        );

        new w2grid({
            name: "grid",
            box: "#grid",
            show: {
                toolbar: true,
                footer: true,
                toolbarSave: true,
                toolbarSearch: true,
                toolbarReload: false,
                statusRecordID: false
            },
            multiSearch: false,
            searches: [{ field: "schemaName", text: "Schema Name", type: "text", operator: "contains" }],
            columns: [
                {
                    field: "schemaName",
                    text: "Schema Name",
                    size: XrmTranslator.defaultSchemaNameSize,
                    sortable: true,
                    resizable: true,
                    frozen: true
                }
            ],
            onSave: function (event) {
                if (XrmTranslator.hasAllowedRole === false) {
                    return;
                }

                SetChangedCellFooter("");

                if (event && typeof event.preventDefault === "function") {
                    event.preventDefault();
                }

                var grid = XrmTranslator.GetGrid();
                var normalizedRecords = XrmTranslator.NormalizeGridChanges();
                for (var i = 0; i < normalizedRecords.length; i++) {
                    grid.refreshRow(normalizedRecords[i].recid);
                }

                XrmTranslator.SetSaveButtonDisabled(true);

                return XrmTranslator.CheckActiveOperationBeforeAction("Save")
                    .then(function (canContinue) {
                        if (!canContinue) {
                            return false;
                        }

                        if (XrmTranslator.GetType() === "globalOptionSet") {
                            return true;
                        }

                        return XrmTranslator.CheckPendingPublishJobBeforeSave();
                    })
                    .then(function (canSave) {
                        if (!canSave) {
                            XrmTranslator.SetSaveButtonDisabled(false);
                            return;
                        }

                        if (!currentHandler || typeof currentHandler.Save !== "function") {
                            throw new Error("No save handler is available for the selected type.");
                        }

                        if (!XrmTranslator.HasPendingChanges()) {
                            if (XrmTranslator.GetType() === "globalOptionSet") {
                                grid.refresh();
                                return currentHandler.Save().then(function (result) {
                                    XrmTranslator.EnableLoadAndSave();
                                    return result;
                                });
                            }

                            grid.refresh();
                            XrmTranslator.ShowStatusBanner({
                                tone: "success",
                                icon: "0",
                                message: "No changes to save.",
                                autoHideMs: 3000
                            });
                            XrmTranslator.SetLoadButtonDisabled(false);
                            XrmTranslator.SetSaveButtonDisabled(false);
                            return;
                        }

                        return currentHandler.Save();
                    })
                    .catch(function (error) {
                        XrmTranslator.SetSaveButtonDisabled(false);
                        XrmTranslator.errorHandler(error);
                    });
            },
            onChange: function (event) {
                event.onComplete = function () {
                    var normalizedRecords = XrmTranslator.NormalizeGridChanges();
                    for (var i = 0; i < normalizedRecords.length; i++) {
                        w2ui.grid.refreshRow(normalizedRecords[i].recid);
                    }

                    XrmTranslator.SetSaveButtonDisabled(false);
                    XrmTranslator.UpdateChangedCellFooter(event);
                };
            },
            onClick: function (event) {
                event.onComplete = function () {
                    XrmTranslator.UpdateChangedCellFooter(event);
                    XrmTranslator.SetSaveButtonDisabled(false);
                };
            },
            onDblClick: function (event) {
                event.onComplete = function () {
                    XrmTranslator.UpdateChangedCellFooter(event);
                    XrmTranslator.SetSaveButtonDisabled(false);
                };
            },
            onEditField: function (event) {
                event.onComplete = function () {
                    XrmTranslator.SetSaveButtonDisabled(false);
                };
            },
            onSearch: function (event) {
                event.onComplete = NormalizeGridSearchUiSoon;
            },
            toolbar: {
                items: toolbarItems,
                onClick: HandleToolbarClick
            }
        }).render();

        var gridToolbar = w2ui["grid_toolbar"];

        // Require selecting a solution before enabling scoped actions.
        SetToolbarItemsVisible(ENTITY_DEPENDENT_TYPE_ITEMS, false);
        SetToolbarItemsVisible(GLOBAL_TYPE_ITEMS, false);
        SetSolutionRequiredState(false);

        gridToolbar.insert("w2ui-search-advanced", {
            type: "menu",
            id: "toggle",
            text: "",
            tooltip: "Expand/collapse rows",
            icon: "icon-tree",
            items: [
                { type: "button", text: "Expand all records", id: "expandAll", icon: "w2ui-icon-expand" },
                { type: "button", text: "Collapse all records", id: "collapseAll", icon: "w2ui-icon-collapse" }
            ]
        });
        gridToolbar.insert("w2ui-search-advanced", { type: "break", id: "break-toggle" });

        gridToolbar.insert("w2ui-search-advanced", {
            type: "button",
            text: "",
            tooltip: "Find and replace",
            icon: "icon-find-replace",
            id: "findReplace",
            onClick: function (event) {
                OpenFindAndReplaceDialog();
            }
        });

        // Move Save button to the far right.
        var saveBtn = gridToolbar.get("w2ui-save");
        if (saveBtn) {
            saveBtn.text = "Save";
            saveBtn.tooltip = "Save changes";
            gridToolbar.remove("w2ui-save");
            gridToolbar.add(saveBtn);
        }

        PatchToolbarOperationGuard();
        PatchGridToolbarLock();
        NormalizeGridSearchUiSoon();
        XrmTranslator.StartAppLoading();
    }

    function FillEntitySelector(entities) {
        entities = entities.sort(XrmTranslator.EntityComparer);
        var entitySelect = GetToolbar().get("entitySelect").items;

        for (var i = 0; i < entities.length; i++) {
            var entity = entities[i];

            var localizedLabel = entity.DisplayName.UserLocalizedLabel || {};
            entitySelect.push({
                id: entity.SchemaName,
                text: localizedLabel.Label ? `${localizedLabel.Label} (${entity.LogicalName})` : entity.LogicalName,
                icon: "icon-entity"
            });
            XrmTranslator.entityMetadata[entity.SchemaName] = entity.MetadataId;
        }

        return entities;
    }

    function GetEntities() {
        var queryParams = "?$select=SchemaName,LogicalName,MetadataId,DisplayName&$filter=IsCustomizable/Value eq true";

        var request = {
            entityName: "EntityDefinition",
            queryParams: queryParams
        };

        return WebApiClient.Retrieve(request);
    }

    function GetSolutions() {
        return WebApiClient.Retrieve({
            entityName: "solution",
            queryParams:
                "?$select=uniquename,friendlyname,solutionid&$filter=ismanaged eq false and isvisible eq true and uniquename ne 'Default'&$orderby=friendlyname asc"
        });
    }

    function FillSolutionSelector(solutions) {
        var solutionSelect = GetToolbar().get("solutionSelect").items;

        for (var i = 0; i < solutions.length; i++) {
            var solution = solutions[i];
            solutionSelect.push({
                id: solution.solutionid,
                text: solution.friendlyname + " (" + solution.uniquename + ")",
                icon: "icon-solution"
            });
        }

        return solutions;
    }

    function GetSolutionEntities(solutionId) {
        return WebApiClient.Retrieve({
            entityName: "solutioncomponent",
            queryParams: "?$select=objectid&$filter=_solutionid_value eq " + solutionId + " and componenttype eq 1"
        }).then(function (response) {
            var metadataIds = response.value.map(function (c) {
                return c.objectid.toLowerCase();
            });
            return metadataIds;
        });
    }

    function RepopulateEntitySelector(solutionId) {
        var entitySelectItem = GetToolbar().get("entitySelect");
        entitySelectItem.selected = "none";
        entitySelectItem.items = [{ id: "none", text: "None", icon: "icon-empty" }, { text: "--" }];
        XrmTranslator.entityMetadata = {};

        if (!solutionId || solutionId === "all") {
            SetSolutionRequiredState(false);
            RefreshToolbar();
            return Promise.resolve();
        }

        XrmTranslator.LockGrid("Loading solution entities...");

        return GetSolutionEntities(solutionId)
            .then(function (metadataIds) {
                var solutionEntities = XrmTranslator.allEntities.filter(function (e) {
                    return metadataIds.indexOf(e.MetadataId.toLowerCase()) !== -1;
                });
                FillEntitySelector(solutionEntities);
                SetToolbarItemsEnabled(["entitySelect", "type", "load"], true);
                ApplyTypeVisibilityForEntity("entitySelect:none");
                UpdateComponentDropdown(GetToolbar().get("type").selected || "sitemap");
                RefreshToolbar();
                XrmTranslator.UnlockGrid();
            })
            .catch(function (error) {
                XrmTranslator.errorHandler(error);
            });
    }

    function GetUserId() {
        return WebApiClient.Execute(WebApiClient.Requests.WhoAmIRequest);
    }

    function GetUserSettings(userId) {
        return WebApiClient.Retrieve({
            overriddenSetName: "usersettingscollection",
            entityId: userId
        });
    }

    function RegisterReloadPrevention() {
        // Dashboards are automatically refreshed on browser window resize, we don't want to lose changes.
        window.onbeforeunload = function (e) {
            var unsavedChanges = XrmTranslator.HasPendingChanges();

            if (unsavedChanges) {
                var warning =
                    "There are unsaved changes in the dashboard, are you sure you want to reload and discard changes?";
                e.returnValue = warning;
                return warning;
            }
        };
    }

    XrmTranslator.GetAllRecords = function () {
        var records = XrmTranslator.GetGrid().records;

        return Array.from(new Set(FlattenRecords(records)));
    };

    XrmTranslator.GetColumns = function (includeSchemaName) {
        var columns = XrmTranslator.GetGrid().columns.map(function (c) {
            return c.field;
        });

        if (includeSchemaName) {
            return columns;
        }

        return columns.filter(function (c) {
            return c !== "schemaName";
        });
    };

    XrmTranslator.ClearColumns = function () {
        // Don't remove schema name column
        var columns = XrmTranslator.GetColumns(false);

        columns.forEach(function (l) {
            XrmTranslator.GetGrid().removeColumn(l);
        });
    };

    XrmTranslator.AddSummary = function (records, countChildParents) {
        var parentCount = records.length;
        var childCount = records
            .map(function (r) {
                return r.w2ui && r.w2ui.children && r.w2ui.children.length;
            })
            .reduce(function (a, b) {
                return a + (b || 0);
            }, 0);

        var count = 0;

        if (childCount > 0) {
            count = childCount;

            if (countChildParents) {
                count += parentCount;
            }
        } else {
            count = parentCount;
        }

        var summary = {
            w2ui: { summary: true },
            recid: "Summary-1",
            schemaName: '<span style="float: right;">Of ' + count + " labels in total</span>"
        };

        for (var i = 0; i < XrmTranslator.installedLanguages.LocaleIds.length; i++) {
            var language = XrmTranslator.installedLanguages.LocaleIds[i].toString();

            var translatedParents = records.filter(function (r) {
                return !!r[language];
            }).length;
            var translatedChildren = records
                .map(function (r) {
                    return (
                        r.w2ui &&
                        r.w2ui.children &&
                        r.w2ui.children.filter(function (c) {
                            return !!c[language];
                        })
                    );
                })
                .reduce(function (a, b) {
                    return a + (b || []).length;
                }, 0);

            var translatedRecords = 0;

            if (translatedChildren > 0) {
                translatedRecords = translatedChildren;

                if (countChildParents) {
                    translatedRecords += translatedParents;
                }
            } else {
                translatedRecords = translatedParents;
            }

            summary[language] = translatedRecords + " translated (" + (count - translatedRecords) + " untranslated)";
        }

        records.push(summary);
    };

    XrmTranslator.Initialize = function (hasAllowedRole) {
        XrmTranslator.hasAllowedRole = hasAllowedRole === true;

        if (XrmTranslator.hasAllowedRole === false) {
            InitializeGrid();
            XrmTranslator.ClearAppLoading();
            DisableAllToolbarItems();
            return;
        }

        XrmTranslator.GetBaseLanguage()
            .then(function () {
                InitializeGrid();
                RegisterReloadPrevention();

                return GetUserId();
            })
            .then(function (response) {
                XrmTranslator.userId = response.UserId;

                return GetUserSettings(XrmTranslator.userId);
            })
            .then(function (response) {
                XrmTranslator.userSettings = response;

                return Promise.all([GetEntities(), GetSolutions()]);
            })
            .then(function (results) {
                var entities = results[0].value;
                var solutions = results[1].value;

                XrmTranslator.allEntities = entities;

                FillSolutionSelector(solutions);
                return entities;
            })
            .then(function () {
                return TranslationHandler.GetAvailableLanguages();
            })
            .then(function (languages) {
                XrmTranslator.installedLanguages = languages;
                return TranslationHandler.FillLanguageCodes(languages.LocaleIds, XrmTranslator.userSettings);
            })
            .then(function () {
                if (window.TranslationDictionaryService && TranslationDictionaryService.EnsureInitialized) {
                    XrmTranslator.LockGrid("Preparing dictionary storage...");

                    return TranslationDictionaryService.EnsureInitialized().catch(function (error) {
                        if (window.console && window.console.warn) {
                            window.console.warn("Dictionary bootstrap failed.", error);
                        }
                    });
                }

                return null;
            })
            .then(function () {
                XrmTranslator.ClearAppLoading();
                XrmTranslator.ApplyStoredOperationStatus();
                return XrmTranslator.RefreshPublishXmlJobState({
                    silent: true,
                    countAttempt: false,
                    notifyForcedClear: false
                });
            })
            .catch(function (error) {
                XrmTranslator.ClearAppLoading();
                XrmTranslator.errorHandler(error);
            });
    };
})((window.XrmTranslator = window.XrmTranslator || {}));
