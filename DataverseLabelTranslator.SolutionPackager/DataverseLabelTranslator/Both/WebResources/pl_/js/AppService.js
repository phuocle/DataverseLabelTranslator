(function (AppService, undefined) {
    "use strict";

    var actionName = "Other";

    function clone(value) {
        if (value === undefined || value === null) {
            return value;
        }

        return JSON.parse(JSON.stringify(value));
    }

    function isPlainObject(value) {
        return Object.prototype.toString.call(value) === "[object Object]";
    }

    function mergeObjects(defaults, value) {
        var result = clone(defaults || {});
        var key;

        if (!isPlainObject(value)) {
            return result;
        }

        for (key in value) {
            if (!Object.prototype.hasOwnProperty.call(value, key)) {
                continue;
            }

            if (isPlainObject(result[key]) && isPlainObject(value[key])) {
                result[key] = mergeObjects(result[key], value[key]);
            } else {
                result[key] = clone(value[key]);
            }
        }

        return result;
    }

    function normalizeProviderKey(providerKey) {
        var key = String(providerKey || "")
            .trim()
            .toLowerCase();

        if (key === "gemini" || key === "google-gemini") {
            return "google";
        }

        if (key === "azure-foundry" || key === "azurefoundry") {
            return "azure";
        }

        if (key === "openai-compatible" || key === "openai_compatible") {
            return "openai";
        }

        return key;
    }

    function maskApiKey(key) {
        if (!key || key.length <= 5) {
            return key || "";
        }

        return key.substring(0, 5) + new Array(key.length - 4).join("*");
    }

    function isMaskedApiKey(value) {
        return String(value || "").indexOf("*") !== -1;
    }

    function getProviderSettings(aiSettings, providerKey) {
        var providers = aiSettings && aiSettings.providers ? aiSettings.providers : {};
        return providers[providerKey] || {};
    }

    function resolveApiKey(value, existingValue) {
        if (isMaskedApiKey(value) && existingValue) {
            return existingValue;
        }

        return value || "";
    }

    function isProviderEnabledInRecord(record, prefix) {
        return !!(record && record[prefix + "Enabled"]);
    }

    function setProviderRequiredFields(providerKey, required) {
        if (!w2ui.appSettings || !w2ui.appSettings.fields) {
            return;
        }

        var requiredFields = [providerKey + "BaseUrl", providerKey + "ApiKey", providerKey + "ModelName"];

        for (var i = 0; i < w2ui.appSettings.fields.length; i++) {
            if (requiredFields.indexOf(w2ui.appSettings.fields[i].field) !== -1) {
                w2ui.appSettings.fields[i].required = !!required;
            }
        }
    }

    function setProviderInputsEnabled(providerKey, enabled) {
        var fields = [
            providerKey + "BaseUrl",
            providerKey + "ApiKey",
            providerKey + "ModelName",
            providerKey + "CustomPrompt"
        ];
        var popup = document.querySelector("#w2ui-popup");

        if (!popup) {
            return;
        }

        for (var i = 0; i < fields.length; i++) {
            var input = popup.querySelector('[name="' + fields[i] + '"]');
            if (input) {
                input.disabled = !enabled;
                input.readOnly = !enabled;
            }
        }
    }

    function setProviderRequiredMarks(providerKey, enabled) {
        var page = document.querySelector(
            '#w2ui-popup .xqt-app-settings-provider-page[data-provider="' + providerKey + '"]'
        );
        var marks;

        if (!page) {
            return;
        }

        marks = page.querySelectorAll(".xqt-app-settings-required");
        for (var i = 0; i < marks.length; i++) {
            marks[i].style.display = enabled ? "" : "none";
        }
    }

    function updateProviderEnabledState(providerKey) {
        var enabled = isProviderEnabledInRecord(w2ui.appSettings && w2ui.appSettings.record, providerKey);

        setProviderRequiredFields(providerKey, enabled);
        setProviderInputsEnabled(providerKey, enabled);
        setProviderRequiredMarks(providerKey, enabled);
    }

    function updateAllProviderEnabledStates() {
        updateProviderEnabledState("google");
        updateProviderEnabledState("openai");
        updateProviderEnabledState("azure");
    }

    function validateAppSettingsRecord(record) {
        var providers = [
            { key: "google", label: "Google" },
            { key: "openai", label: "OpenAI" },
            { key: "azure", label: "Azure" }
        ];
        var requiredFields = [
            { suffix: "BaseUrl", label: "URL" },
            { suffix: "ApiKey", label: "API Key" },
            { suffix: "ModelName", label: "Model Name" }
        ];
        var errors = [];

        for (var i = 0; i < providers.length; i++) {
            var provider = providers[i];
            if (!isProviderEnabledInRecord(record, provider.key)) {
                continue;
            }

            for (var j = 0; j < requiredFields.length; j++) {
                var field = requiredFields[j];
                var value = record[provider.key + field.suffix];
                if (!String(value || "").trim()) {
                    errors.push(provider.label + ": " + field.label + " is required.");
                }
            }
        }

        return errors;
    }

    function activateAppSettingsTab(tabId, providerId) {
        if (!w2ui.appSettings || typeof w2ui.appSettings.goto !== "function") {
            return;
        }

        if (tabId === "tab-ai") {
            w2ui.appSettings.goto(1);
            renderAppSettingsProviderTabsAfterVisible(providerId);
            return;
        }

        w2ui.appSettings.goto(0);
    }

    function showAppSettingsProviderPage(providerId) {
        var normalizedProviderId = normalizeProviderKey(providerId) || "google";
        var pages = document.querySelectorAll("#w2ui-popup .xqt-app-settings-provider-page");

        for (var i = 0; i < pages.length; i++) {
            pages[i].style.display = pages[i].getAttribute("data-provider") === normalizedProviderId ? "block" : "none";
        }

        if (w2ui.appSettings && w2ui.appSettings.record) {
            w2ui.appSettings.record.selectedProvider = normalizedProviderId;
        }
    }

    function renderAppSettingsProviderTabs(providerId) {
        var target = document.querySelector("#w2ui-popup #app-settings-ai-tabs");
        var activeProvider = normalizeProviderKey(providerId) || "google";

        if (!target) {
            return;
        }

        if (!w2ui.appSettingsAiProviderTabs) {
            new w2tabs({
                name: "appSettingsAiProviderTabs",
                active: activeProvider,
                tabs: [
                    { id: "google", text: "Google" },
                    { id: "openai", text: "OpenAI" },
                    { id: "azure", text: "Azure" }
                ],
                onClick: function (event) {
                    showAppSettingsProviderPage(event.target);
                }
            });
            w2ui.appSettingsAiProviderTabs.render(target);
        } else {
            if (w2ui.appSettingsAiProviderTabs.box !== target) {
                w2ui.appSettingsAiProviderTabs.render(target);
            }

            if (w2ui.appSettingsAiProviderTabs.active !== activeProvider) {
                w2ui.appSettingsAiProviderTabs.click(activeProvider);
            } else {
                w2ui.appSettingsAiProviderTabs.refresh();
            }
        }

        showAppSettingsProviderPage(activeProvider);
    }

    function renderAppSettingsProviderTabsAfterVisible(providerId) {
        var render = function () {
            renderAppSettingsProviderTabs(providerId);
        };

        if (window.requestAnimationFrame) {
            window.requestAnimationFrame(render);
            return;
        }

        setTimeout(render, 0);
    }

    function destroyAppSettingsProviderTabs() {
        if (w2ui.appSettingsAiProviderTabs) {
            w2ui.appSettingsAiProviderTabs.destroy();
        }
    }

    function bindAppSettingsTopTabs(providerId) {
        if (!w2ui.appSettings || !w2ui.appSettings.tabs || typeof w2ui.appSettings.tabs.on !== "function") {
            return;
        }

        if (w2ui.appSettings.tabs._appSettingsProviderTabsBound) {
            return;
        }

        w2ui.appSettings.tabs._appSettingsProviderTabsBound = true;
        w2ui.appSettings.tabs.on("click", function (event) {
            if (event.target === "tab-ai") {
                renderAppSettingsProviderTabsAfterVisible(
                    w2ui.appSettings && w2ui.appSettings.record ? w2ui.appSettings.record.selectedProvider : providerId
                );
            }
        });
    }

    function buildProviderFormHtml(providerKey, prefix) {
        return (
            "" +
            '<div class="xqt-app-settings-provider-page" data-provider="' +
            providerKey +
            '">' +
            '    <div class="xqt-app-settings-row xqt-app-settings-row-checkbox">' +
            '        <label class="xqt-app-settings-label" for="' +
            prefix +
            'Enabled">Enable:</label>' +
            '        <div class="xqt-app-settings-control"><input name="' +
            prefix +
            'Enabled" type="checkbox" /></div>' +
            "    </div>" +
            '    <div class="xqt-app-settings-row">' +
            '        <label class="xqt-app-settings-label" for="' +
            prefix +
            'BaseUrl">URL: <span class="xqt-required xqt-app-settings-required">*</span></label>' +
            '        <div class="xqt-app-settings-control"><input name="' +
            prefix +
            'BaseUrl" type="text" /></div>' +
            "    </div>" +
            '    <div class="xqt-app-settings-row">' +
            '        <label class="xqt-app-settings-label" for="' +
            prefix +
            'ApiKey">API Key: <span class="xqt-required xqt-app-settings-required">*</span></label>' +
            '        <div class="xqt-app-settings-control"><input name="' +
            prefix +
            'ApiKey" type="password" /></div>' +
            "    </div>" +
            '    <div class="xqt-app-settings-row">' +
            '        <label class="xqt-app-settings-label" for="' +
            prefix +
            'ModelName">Model Name: <span class="xqt-required xqt-app-settings-required">*</span></label>' +
            '        <div class="xqt-app-settings-control"><input name="' +
            prefix +
            'ModelName" type="text" /></div>' +
            "    </div>" +
            '    <div class="xqt-app-settings-row xqt-app-settings-row-textarea">' +
            '        <label class="xqt-app-settings-label" for="' +
            prefix +
            'CustomPrompt">Custom Prompt:</label>' +
            '        <div class="xqt-app-settings-control"><textarea name="' +
            prefix +
            'CustomPrompt"></textarea></div>' +
            "    </div>" +
            "</div>"
        );
    }

    function createEmptyProviderConfig(defaults) {
        return mergeObjects(
            {
                enabled: false,
                baseUrl: "",
                apiKey: "",
                modelName: "",
                customPrompt: ""
            },
            defaults || {}
        );
    }

    function getDefaultSettings() {
        return {
            schemaVersion: 1,
            updatedOn: "",
            updatedBy: {
                id: "",
                name: ""
            },
            general: {},
            ai: {
                selectedProvider: "google",
                providers: {
                    google: createEmptyProviderConfig(),
                    openai: createEmptyProviderConfig(),
                    azure: createEmptyProviderConfig()
                }
            }
        };
    }

    function normalizeSettings(settings) {
        var normalized = mergeObjects(getDefaultSettings(), settings || {});
        var providers = normalized.ai.providers || {};

        normalized.ai.providers = {
            google: createEmptyProviderConfig(providers.google || {}),
            openai: createEmptyProviderConfig(providers.openai || {}),
            azure: createEmptyProviderConfig(providers.azure || {})
        };

        normalized.ai.selectedProvider = normalizeProviderKey(normalized.ai.selectedProvider) || "google";
        if (!normalized.ai.providers[normalized.ai.selectedProvider]) {
            normalized.ai.selectedProvider = "google";
        }

        return normalized;
    }

    function getCurrentUserInfo() {
        var user = {
            id: "",
            name: ""
        };

        try {
            if (window.Xrm && Xrm.Page && Xrm.Page.context) {
                if (Xrm.Page.context.getUserId) {
                    user.id = String(Xrm.Page.context.getUserId() || "").replace(/[{}]/g, "");
                }

                if (Xrm.Page.context.getUserName) {
                    user.name = Xrm.Page.context.getUserName() || "";
                }
            }
        } catch (e) {
            return user;
        }

        return user;
    }

    function toCData(value) {
        return String(value || "").replace(/\]\]>/g, "]]]]><![CDATA[>");
    }

    function getDefaultSettingsXml() {
        return serializeSettingsXml(getDefaultSettings());
    }

    function serializeSettingsXml(settings) {
        var normalized = normalizeSettings(settings);
        var json = JSON.stringify(normalized, null, 2);

        return [
            '<?xml version="1.0" encoding="utf-8"?>',
            '<appSettings contentType="application/json"><![CDATA[',
            toCData(json),
            "]]></appSettings>"
        ].join("\n");
    }

    function parseSettingsContent(content) {
        var raw = String(content || "").trim();
        var jsonText;
        var parser;
        var xml;
        var root;

        if (!raw) {
            return getDefaultSettings();
        }

        if (raw.charAt(0) === "{") {
            return normalizeSettings(JSON.parse(raw));
        }

        parser = new DOMParser();
        xml = parser.parseFromString(raw, "application/xml");

        if (xml.getElementsByTagName("parsererror").length > 0) {
            throw new Error("App Settings web resource content is not valid XML.");
        }

        root = xml.getElementsByTagName("appSettings")[0];
        if (!root) {
            throw new Error("App Settings web resource does not contain an appSettings root element.");
        }

        jsonText = root.textContent || "";
        if (!jsonText.trim()) {
            return getDefaultSettings();
        }

        return normalizeSettings(JSON.parse(jsonText));
    }

    function executeOther(operation, payload) {
        var input = Object.assign({ operation: operation }, payload || {});

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Other, input).then(
            function (result) {
                return Helper.GetCustomActionObject(result);
            }
        );
    }

    function ensureInitialized(forceRefresh) {
        return executeOther("ReadAppSettings");
    }

    function readSettings(forceRefresh) {
        return ensureInitialized(!!forceRefresh).then(function (storage) {
            return parseSettingsContent(storage && storage.content);
        });
    }

    function writeSettings(settings) {
        var normalized = normalizeSettings(settings);
        normalized.updatedOn = new Date().toISOString();
        normalized.updatedBy = getCurrentUserInfo();

        return executeOther("WriteAppSettings", { content: serializeSettingsXml(normalized) }).then(function () {
            return normalized;
        });
    }

    function buildAISettingsFromRecord(record, currentAISettings) {
        var currentGoogle = getProviderSettings(currentAISettings, "google");
        var currentOpenAI = getProviderSettings(currentAISettings, "openai");
        var currentAzure = getProviderSettings(currentAISettings, "azure");

        return {
            selectedProvider:
                normalizeProviderKey(record && record.selectedProvider) ||
                normalizeProviderKey(currentAISettings && currentAISettings.selectedProvider) ||
                "google",
            providers: {
                google: {
                    enabled: isProviderEnabledInRecord(record, "google"),
                    baseUrl: record.googleBaseUrl || "",
                    apiKey: resolveApiKey(record.googleApiKey, currentGoogle.apiKey),
                    modelName: record.googleModelName || "",
                    customPrompt: record.googleCustomPrompt || ""
                },
                openai: {
                    enabled: isProviderEnabledInRecord(record, "openai"),
                    baseUrl: record.openaiBaseUrl || "",
                    apiKey: resolveApiKey(record.openaiApiKey, currentOpenAI.apiKey),
                    modelName: record.openaiModelName || "",
                    customPrompt: record.openaiCustomPrompt || ""
                },
                azure: {
                    enabled: isProviderEnabledInRecord(record, "azure"),
                    baseUrl: record.azureBaseUrl || "",
                    apiKey: resolveApiKey(record.azureApiKey, currentAzure.apiKey),
                    modelName: record.azureModelName || "",
                    customPrompt: record.azureCustomPrompt || ""
                }
            }
        };
    }

    function initializeAppSettingsForm() {
        return AppService.GetAISettings(true).then(function (aiSettings) {
            var googleConfig = getProviderSettings(aiSettings, "google");
            var openaiConfig = getProviderSettings(aiSettings, "openai");
            var azureConfig = getProviderSettings(aiSettings, "azure");
            var appSettingsFormHTML =
                '<div class="w2ui-page page-0 xqt-app-settings-general">' +
                '    <div class="xqt-app-settings-row xqt-app-settings-row-checkbox">' +
                '        <label class="xqt-app-settings-label" for="generalDummy">Dummy:</label>' +
                '        <div class="xqt-app-settings-control"><input name="generalDummy" type="checkbox" /></div>' +
                "    </div>" +
                "</div>" +
                '<div class="w2ui-page page-1 xqt-app-settings-ai">' +
                '    <div id="app-settings-ai-tabs" class="xqt-app-settings-provider-tabs"></div>' +
                buildProviderFormHtml("google", "google") +
                buildProviderFormHtml("openai", "openai") +
                buildProviderFormHtml("azure", "azure") +
                "</div>" +
                '<div class="w2ui-buttons">' +
                '    <button class="w2ui-btn" name="cancel">Cancel</button>' +
                '    <button class="w2ui-btn" name="save">Save</button>' +
                "</div>";
            var appSettingsFields = [
                { field: "generalDummy", type: "checkbox", html: { page: 0 } },
                { field: "googleEnabled", type: "checkbox", html: { page: 1 } },
                { field: "googleBaseUrl", type: "text", required: false, html: { page: 1 } },
                { field: "googleApiKey", type: "text", required: false, html: { page: 1 } },
                { field: "googleModelName", type: "text", required: false, html: { page: 1 } },
                { field: "googleCustomPrompt", type: "text", html: { page: 1 } },
                { field: "openaiEnabled", type: "checkbox", html: { page: 1 } },
                { field: "openaiBaseUrl", type: "text", required: false, html: { page: 1 } },
                { field: "openaiApiKey", type: "text", required: false, html: { page: 1 } },
                { field: "openaiModelName", type: "text", required: false, html: { page: 1 } },
                { field: "openaiCustomPrompt", type: "text", html: { page: 1 } },
                { field: "azureEnabled", type: "checkbox", html: { page: 1 } },
                { field: "azureBaseUrl", type: "text", required: false, html: { page: 1 } },
                { field: "azureApiKey", type: "text", required: false, html: { page: 1 } },
                { field: "azureModelName", type: "text", required: false, html: { page: 1 } },
                { field: "azureCustomPrompt", type: "text", html: { page: 1 } }
            ];
            var appSettingsRecord = {
                generalDummy: false,
                googleEnabled:
                    googleConfig.enabled === true &&
                    !!(googleConfig.baseUrl || googleConfig.apiKey || googleConfig.modelName),
                googleBaseUrl: googleConfig.baseUrl || "",
                googleApiKey: maskApiKey(googleConfig.apiKey),
                googleModelName: googleConfig.modelName || "",
                googleCustomPrompt: googleConfig.customPrompt || "",
                openaiEnabled:
                    openaiConfig.enabled === true &&
                    !!(openaiConfig.baseUrl || openaiConfig.apiKey || openaiConfig.modelName),
                openaiBaseUrl: openaiConfig.baseUrl || "",
                openaiApiKey: maskApiKey(openaiConfig.apiKey),
                openaiModelName: openaiConfig.modelName || "",
                openaiCustomPrompt: openaiConfig.customPrompt || "",
                azureEnabled:
                    azureConfig.enabled === true &&
                    !!(azureConfig.baseUrl || azureConfig.apiKey || azureConfig.modelName),
                azureBaseUrl: azureConfig.baseUrl || "",
                azureApiKey: maskApiKey(azureConfig.apiKey),
                azureModelName: azureConfig.modelName || "",
                azureCustomPrompt: azureConfig.customPrompt || "",
                selectedProvider: normalizeProviderKey(aiSettings.selectedProvider) || "google"
            };

            if (w2ui.appSettings) {
                w2ui.appSettings.destroy();
            }

            new w2form({
                name: "appSettings",
                style: "border: 0px; background-color: transparent;",
                focus: -1,
                tabs: [
                    { id: "tab-general", text: "General" },
                    { id: "tab-ai", text: "AI" }
                ],
                onClick: function (event) {
                    activateAppSettingsTab(event.target, appSettingsRecord.selectedProvider);
                },
                formHTML: appSettingsFormHTML,
                fields: appSettingsFields,
                record: appSettingsRecord,
                onChange: function (event) {
                    var target = event.target || "";
                    var providerKey = null;

                    if (target === "googleEnabled") providerKey = "google";
                    if (target === "openaiEnabled") providerKey = "openai";
                    if (target === "azureEnabled") providerKey = "azure";

                    if (providerKey) {
                        event.onComplete = function () {
                            updateProviderEnabledState(providerKey);
                        };
                    }
                },
                actions: {
                    save: function () {
                        var form = this;
                        var validationErrors = validateAppSettingsRecord(form.record);

                        if (validationErrors.length > 0) {
                            w2alert(validationErrors.join("<br>"), "App Settings");
                            return;
                        }

                        w2popup.lock("Saving...", true);

                        AppService.GetAISettings(true)
                            .then(function (latestAISettings) {
                                var settingsToSave = buildAISettingsFromRecord(form.record, latestAISettings);
                                return AppService.SaveAISettings(settingsToSave);
                            })
                            .then(function () {
                                w2popup.unlock();
                                w2popup.close();
                                w2alert("App settings saved successfully.");
                            })
                            .catch(function (error) {
                                w2popup.unlock();
                                var message = error && error.message ? error.message : String(error);
                                if (window.DialogHelper && DialogHelper.alert) {
                                    return DialogHelper.alert(message, { title: "App Settings" });
                                }

                                w2alert(message);
                                return null;
                            });
                    },
                    cancel: function () {
                        w2popup.close();
                    }
                }
            });

            return {
                selectedProvider: normalizeProviderKey(aiSettings.selectedProvider) || "google"
            };
        });
    }

    AppService.EnsureInitialized = function (forceRefresh) {
        return ensureInitialized(!!forceRefresh);
    };

    AppService.GetSettings = function (forceRefresh) {
        return readSettings(!!forceRefresh);
    };

    AppService.SaveSettings = function (settings) {
        return readSettings(true).then(function (latest) {
            return writeSettings(mergeObjects(latest, settings || {}));
        });
    };

    AppService.GetAISettings = function (forceRefresh) {
        return readSettings(!!forceRefresh).then(function (settings) {
            return clone(settings.ai);
        });
    };

    AppService.SaveAISettings = function (aiSettings) {
        return readSettings(true)
            .then(function (latest) {
                latest.ai = mergeObjects(latest.ai || getDefaultSettings().ai, aiSettings || {});
                return writeSettings(latest);
            })
            .then(function (settings) {
                return clone(settings.ai);
            });
    };

    AppService.GetProviderConfig = function (providerKey, forceRefresh) {
        return AppService.GetAISettings(!!forceRefresh).then(function (aiSettings) {
            var normalizedProviderKey = normalizeProviderKey(providerKey);
            var providers = aiSettings.providers || {};

            return clone(providers[normalizedProviderKey] || createEmptyProviderConfig());
        });
    };

    AppService.NormalizeProviderKey = function (providerKey) {
        return normalizeProviderKey(providerKey);
    };

    AppService.ShowAppSettings = function () {
        destroyAppSettingsProviderTabs();
        w2popup.open({
            title: "App Settings",
            name: "appSettingsPopup",
            body: '<div id="form" class="xqt-app-settings-popup-form"><div class="xqt-app-settings-loading">Loading app settings...</div></div>',
            style: "padding: 0px; overflow-x: hidden;",
            width: 760,
            height: 540,
            showMax: false,
            onOpen: function (event) {
                event.onComplete = function () {
                    initializeAppSettingsForm()
                        .then(function (context) {
                            w2ui.appSettings.render("#w2ui-popup #form");
                            bindAppSettingsTopTabs(context.selectedProvider);
                            activateAppSettingsTab("tab-general", context.selectedProvider);
                            updateAllProviderEnabledStates();
                            w2ui.appSettings.resize();
                        })
                        .catch(function (error) {
                            if (window.DataverseLabelTranslator && DataverseLabelTranslator.errorHandler) {
                                DataverseLabelTranslator.errorHandler(error);
                                return;
                            }

                            throw error;
                        });
                };
            },
            onToggle: function (event) {
                if (w2ui.appSettings && w2ui.appSettings.box) {
                    w2ui.appSettings.box.style.display = "none";
                }
                event.onComplete = function () {
                    if (w2ui.appSettings && w2ui.appSettings.box) {
                        w2ui.appSettings.box.style.display = "";
                        w2ui.appSettings.resize();
                    }
                };
            },
            onClose: function () {
                destroyAppSettingsProviderTabs();
            }
        });
    };
})((window.AppService = window.AppService || {}));
