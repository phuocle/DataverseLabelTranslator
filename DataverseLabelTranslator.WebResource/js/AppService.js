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
})((window.AppService = window.AppService || {}));
