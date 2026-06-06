(function (AppSettingsService, undefined) {
    "use strict";

    var APP_SETTINGS_WEBRESOURCE_UNIQUE_NAME = "pl_/DataverseLabelTranslator/data/AppSettings.xml";
    var APP_SETTINGS_WEBRESOURCE_DISPLAY_NAME = "App Settings";
    var APP_SETTINGS_WEBRESOURCE_DESCRIPTION = "Stores environment-owned app settings for Dataverse Label Translator.";

    var storageOptions = {
        uniqueName: APP_SETTINGS_WEBRESOURCE_UNIQUE_NAME,
        displayName: APP_SETTINGS_WEBRESOURCE_DISPLAY_NAME,
        description: APP_SETTINGS_WEBRESOURCE_DESCRIPTION,
        webResourceType: 4,
        defaultContent: getDefaultSettingsXml()
    };

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

    function ensureInitialized(forceRefresh) {
        return DataverseDataWebResourceService.EnsureTextWebResource(storageOptions);
    }

    function readSettings(forceRefresh) {
        return ensureInitialized(!!forceRefresh)
            .then(function () {
                return DataverseDataWebResourceService.ReadText(storageOptions);
            })
            .then(function (content) {
                return parseSettingsContent(content);
            });
    }

    function writeSettings(settings) {
        var normalized = normalizeSettings(settings);
        normalized.updatedOn = new Date().toISOString();
        normalized.updatedBy = getCurrentUserInfo();

        return DataverseDataWebResourceService.WriteText(storageOptions, serializeSettingsXml(normalized)).then(
            function () {
                return normalized;
            }
        );
    }

    AppSettingsService.EnsureInitialized = function (forceRefresh) {
        return ensureInitialized(!!forceRefresh);
    };

    AppSettingsService.GetSettings = function (forceRefresh) {
        return readSettings(!!forceRefresh);
    };

    AppSettingsService.SaveSettings = function (settings) {
        return readSettings(true).then(function (latest) {
            return writeSettings(mergeObjects(latest, settings || {}));
        });
    };

    AppSettingsService.GetAISettings = function (forceRefresh) {
        return readSettings(!!forceRefresh).then(function (settings) {
            return clone(settings.ai);
        });
    };

    AppSettingsService.SaveAISettings = function (aiSettings) {
        return readSettings(true)
            .then(function (latest) {
                latest.ai = mergeObjects(latest.ai || getDefaultSettings().ai, aiSettings || {});
                return writeSettings(latest);
            })
            .then(function (settings) {
                return clone(settings.ai);
            });
    };

    AppSettingsService.GetProviderConfig = function (providerKey, forceRefresh) {
        return AppSettingsService.GetAISettings(!!forceRefresh).then(function (aiSettings) {
            var normalizedProviderKey = normalizeProviderKey(providerKey);
            var providers = aiSettings.providers || {};

            return clone(providers[normalizedProviderKey] || createEmptyProviderConfig());
        });
    };

    AppSettingsService.NormalizeProviderKey = function (providerKey) {
        return normalizeProviderKey(providerKey);
    };

    AppSettingsService.GetStorageOptions = function () {
        return clone(storageOptions);
    };
})((window.AppSettingsService = window.AppSettingsService || {}));
