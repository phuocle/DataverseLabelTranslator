(function (TranslationHandler, undefined) {
    "use strict";

    var locales = null;
    var GEMINI_DEFAULT_BASE_URL = "https://generativelanguage.googleapis.com/v1beta";
    var OPENAI_DEFAULT_BASE_URL = "https://api.openai.com/v1";
    var AI_PROVIDER_VISIBILITY = {
        azure: true,
        google: true,
        openAI: true
    };
    var TRANSLATION_PROMPT_KEY = "DataverseLabelTranslator_TranslationPrompt";
    var translationProviders = [];

    function GetCurrentGridValue(record, lcid) {
        if (!record) {
            return "";
        }

        if (record.w2ui && record.w2ui.changes && Object.prototype.hasOwnProperty.call(record.w2ui.changes, lcid)) {
            return record.w2ui.changes[lcid];
        }

        return record[lcid] || record[String(lcid)] || "";
    }

    function GetTranslationResultValue(result) {
        if (
            result.w2ui &&
            result.w2ui.changes &&
            Object.prototype.hasOwnProperty.call(result.w2ui.changes, "translation")
        ) {
            return result.w2ui.changes.translation;
        }

        return result.translation;
    }

    function NormalizeTranslationText(value) {
        if (value === null || typeof value === "undefined") {
            return "";
        }

        return String(value)
            .replace(/&nbsp;/gi, " ")
            .replace(/\u00a0/g, " ")
            .replace(/<[^>]*>/g, "")
            .trim();
    }

    function HasTranslationText(value) {
        return NormalizeTranslationText(value).length > 0;
    }

    function GetSavedTranslationPrompt() {
        try {
            var stored = localStorage.getItem(TRANSLATION_PROMPT_KEY);
            return stored ? JSON.parse(stored) : null;
        } catch (e) {
            return null;
        }
    }

    function SaveTranslationPrompt(values) {
        try {
            localStorage.setItem(TRANSLATION_PROMPT_KEY, JSON.stringify(values || {}));
        } catch (e) {
            return values;
        }

        return values;
    }

    function CreateEmptyAIConfig() {
        return {
            enabled: false,
            baseUrl: "",
            apiKey: "",
            modelName: "",
            customPrompt: ""
        };
    }

    function NormalizeProviderId(providerId) {
        if (window.AppService && AppService.NormalizeProviderKey) {
            return AppService.NormalizeProviderKey(providerId);
        }

        var normalized = String(providerId || "")
            .trim()
            .toLowerCase();
        if (normalized === "gemini" || normalized === "google-gemini") return "google";
        if (normalized === "azure-foundry" || normalized === "azurefoundry") return "azure";
        if (normalized === "openai-compatible" || normalized === "openai_compatible") return "openai";
        return normalized;
    }

    function GetProviderConfig(providerId, forceRefresh) {
        var normalizedProviderId = NormalizeProviderId(providerId);

        if (!window.AppService || !AppService.GetProviderConfig) {
            return Promise.resolve(CreateEmptyAIConfig());
        }

        return AppService.GetProviderConfig(normalizedProviderId, !!forceRefresh).then(function (config) {
            return Object.assign(CreateEmptyAIConfig(), config || {});
        });
    }

    function GetTranslationProviderContext(forceRefresh) {
        var settingsPromise =
            window.AppService && AppService.GetAISettings
                ? AppService.GetAISettings(!!forceRefresh)
                : Promise.resolve({
                      selectedProvider: "google",
                      providers: {}
                  });

        return settingsPromise.then(function (aiSettings) {
            aiSettings = aiSettings || {};
            var providerSettings = aiSettings.providers || {};
            var selectedProvider = NormalizeProviderId(aiSettings.selectedProvider || "google");
            var providers = [];

            for (var i = 0; i < translationProviders.length; i++) {
                var provider = translationProviders[i];
                var providerId = NormalizeProviderId(provider.id);
                var providerConfig = providerSettings[providerId] || {};

                if (providerConfig.enabled !== true) {
                    continue;
                }

                if (!providerConfig.baseUrl || !providerConfig.apiKey || !providerConfig.modelName) {
                    continue;
                }

                if (provider.enabled && !provider.enabled()) {
                    continue;
                }

                providers.push(provider);
            }

            if (!GetTranslationProvider(selectedProvider, providers) && providers.length > 0) {
                selectedProvider = providers[0].id;
            }

            return {
                aiSettings: aiSettings,
                selectedProvider: selectedProvider,
                providers: providers,
                items: providers.map(function (provider) {
                    return {
                        id: provider.id,
                        text: provider.text
                    };
                })
            };
        });
    }

    function normalizeBoolean(value, defaultValue) {
        if (value === undefined || value === null || value === "") {
            return defaultValue;
        }

        if (typeof value === "boolean") {
            return value;
        }

        var normalized = String(value).trim().toLowerCase();
        if (normalized === "false" || normalized === "0" || normalized === "no" || normalized === "off") {
            return false;
        }

        if (normalized === "true" || normalized === "1" || normalized === "yes" || normalized === "on") {
            return true;
        }

        return defaultValue;
    }

    function IsConfiguredProviderEnabled(providerKey) {
        return AI_PROVIDER_VISIBILITY[providerKey] === true;
    }

    function IsOpenAIEnabled() {
        return IsConfiguredProviderEnabled("openAI");
    }

    function IsGoogleEnabled() {
        return IsConfiguredProviderEnabled("google");
    }

    function IsAzureEnabled() {
        return IsConfiguredProviderEnabled("azure");
    }

    function IsProviderEnabled(provider) {
        return !provider.enabled || provider.enabled();
    }

    function NormalizeBaseUrl(baseUrl) {
        return String(baseUrl || "")
            .trim()
            .replace(/\/+$/, "");
    }

    function PostJson(url, headers, body) {
        return fetch(url, {
            method: "POST",
            headers: Object.assign({ "Content-Type": "application/json" }, headers || {}),
            body: JSON.stringify(body)
        })
            .then(function (response) {
                return response.text().then(function (text) {
                    var data = text ? JSON.parse(text) : {};

                    if (!response.ok) {
                        var errorMessage =
                            (data && data.error && data.error.message) || response.statusText || "Request failed";
                        throw new Error(errorMessage);
                    }

                    return data;
                });
            })
            .catch(function (error) {
                if (error instanceof TypeError) {
                    throw new Error(
                        "Network request failed. Check that the AI endpoint is reachable from this browser, uses HTTPS when the app is loaded over HTTPS, and allows CORS for this Dynamics origin."
                    );
                }

                throw error;
            });
    }

    function GetLanguageIsoByLcid(lcid) {
        var locByLocales = locales.find(function (loc) {
            return loc.localeid === lcid;
        });

        if (locByLocales) {
            return locByLocales.code.substr(0, 2);
        }

        var locByColumns = XrmTranslator.GetGrid().columns.find(function (c) {
            return c.field === lcid;
        });

        if (locByColumns) {
            return GetColumnDisplayText(locByColumns).substr(0, 2);
        }

        return null;
    }

    function RegisterTranslationProvider(provider) {
        if (!provider || !provider.id || typeof provider.create !== "function") {
            return;
        }

        translationProviders.push(provider);
    }

    function GetTranslationProvider(providerId, providers) {
        var normalized = NormalizeProviderId(providerId);
        var candidates = providers || translationProviders;

        for (var i = 0; i < candidates.length; i++) {
            var provider = candidates[i];
            if (NormalizeProviderId(provider.id) === normalized) {
                return provider;
            }
        }

        return null;
    }

    function GetColumnDisplayText(column) {
        if (!column) {
            return "";
        }

        return column.text || column.caption || column.label || String(column.field || "");
    }

    const geminiTranslator = function (baseUrl, apiKey, modelName, customPrompt) {
        var apiUrl =
            NormalizeBaseUrl(baseUrl || GEMINI_DEFAULT_BASE_URL) +
            "/models/" +
            encodeURIComponent(modelName) +
            ":generateContent";

        this.GetBatchTranslations = function (fromLanguage, destLanguage, phrases) {
            var systemInstructions =
                "You are a professional translator for a Microsoft Dynamics CRM / Dataverse system. " +
                "Translate the following labels from " +
                fromLanguage +
                " to " +
                destLanguage +
                ". " +
                (customPrompt ? customPrompt + " " : "") +
                "Return ONLY a valid JSON array of translated strings in the exact same order as provided. " +
                "Do not add any explanation, markdown formatting, or code fences. " +
                "The array must have exactly " +
                phrases.length +
                " elements.";

            var userMessage = JSON.stringify(phrases);

            var requestBody = {
                contents: [
                    {
                        role: "user",
                        parts: [{ text: systemInstructions + "\n\nLabels to translate:\n" + userMessage }]
                    }
                ]
            };

            return PostJson(apiUrl + "?key=" + encodeURIComponent(apiKey), null, requestBody).then(function (response) {
                if (
                    !response ||
                    !response.candidates ||
                    !response.candidates[0] ||
                    !response.candidates[0].content ||
                    !response.candidates[0].content.parts ||
                    !response.candidates[0].content.parts[0] ||
                    !response.candidates[0].content.parts[0].text
                ) {
                    var errorMsg = (response && response.error && response.error.message) || "No translation returned";
                    throw new Error("Gemini API error: " + errorMsg);
                }
                var text = response.candidates[0].content.parts[0].text;
                text = text
                    .replace(/^```(?:json)?\s*/i, "")
                    .replace(/\s*```\s*$/, "")
                    .trim();

                var translations = JSON.parse(text);

                if (!Array.isArray(translations) || translations.length !== phrases.length) {
                    throw new Error(
                        "Gemini returned " +
                            (translations ? translations.length : 0) +
                            " translations but " +
                            phrases.length +
                            " were expected."
                    );
                }

                return translations;
            });
        };

        this.AddTranslations = function (fromLcid, destLcid, updateRecords, translatedPhrases) {
            var translations = [];

            for (var i = 0; i < updateRecords.length; i++) {
                var translated = translatedPhrases[i];
                var record = updateRecords[i];

                if (!translated) {
                    continue;
                }

                var translation = w2utils.encodeTags(translated);

                translations.push({
                    recid: record.recid,
                    targetRecid: record.recid,
                    location: record.location,
                    schemaName: record.schemaName,
                    column: destLcid,
                    source: record[fromLcid],
                    translation: translation,
                    fromDictionary: false
                });
            }

            return translations;
        };

        this.CanTranslate = function (fromLcid, destLcid) {
            return WebApiClient.Promise.resolve({
                [fromLcid]: true,
                [destLcid]: true
            });
        };
    };

    RegisterTranslationProvider({
        id: "google",
        text: "Google",
        enabled: IsGoogleEnabled,
        validate: function (config) {
            if (!config || !config.apiKey) {
                return "Google: API Key is missing. Please configure it via App Settings.";
            }

            if (!config.baseUrl) {
                return "Google: URL is missing. Please configure it via App Settings.";
            }

            if (!config.modelName) {
                return "Google: Model Name is missing. Please configure it via App Settings.";
            }

            return null;
        },
        create: function (config) {
            return new geminiTranslator(config.baseUrl, config.apiKey, config.modelName, config.customPrompt);
        }
    });

    function BuildOpenAICompatibleChatUrl(baseUrl) {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        if (/\/chat\/completions$/i.test(normalizedBaseUrl)) {
            return normalizedBaseUrl;
        }

        return normalizedBaseUrl + "/chat/completions";
    }

    function BuildOpenAICompatibleHeaders(apiKey, authHeaderName) {
        var normalizedAuthHeaderName = String(authHeaderName || "")
            .trim()
            .toLowerCase();

        if (normalizedAuthHeaderName === "api-key") {
            return { "api-key": apiKey };
        }

        return { Authorization: "Bearer " + apiKey };
    }

    const openAICompatibleTranslator = function (
        providerName,
        baseUrl,
        apiKey,
        modelName,
        customPrompt,
        authHeaderName
    ) {
        var apiUrl = BuildOpenAICompatibleChatUrl(baseUrl);

        this.GetBatchTranslations = function (fromLanguage, destLanguage, phrases) {
            var systemPrompt =
                "You are a professional translator for a Microsoft Dynamics CRM / Dataverse system. " +
                "Translate the following labels from " +
                fromLanguage +
                " to " +
                destLanguage +
                ". " +
                (customPrompt ? customPrompt + " " : "") +
                "Return ONLY a valid JSON array of translated strings in the exact same order as provided. " +
                "Do not add any explanation, markdown formatting, or code fences. " +
                "The array must have exactly " +
                phrases.length +
                " elements.";

            var userMessage = JSON.stringify(phrases);

            var requestBody = {
                model: modelName,
                stream: false,
                temperature: 0,
                messages: [
                    { role: "system", content: systemPrompt },
                    { role: "user", content: "Labels to translate:\n" + userMessage }
                ]
            };

            return PostJson(apiUrl, BuildOpenAICompatibleHeaders(apiKey, authHeaderName), requestBody).then(
                function (response) {
                    if (
                        !response ||
                        !response.choices ||
                        !response.choices[0] ||
                        !response.choices[0].message ||
                        !response.choices[0].message.content
                    ) {
                        var errorMsg =
                            (response && response.error && response.error.message) || "No translation returned";
                        throw new Error(providerName + " API error: " + errorMsg);
                    }
                    var text = response.choices[0].message.content;
                    text = text
                        .replace(/^```(?:json)?\s*/i, "")
                        .replace(/\s*```\s*$/, "")
                        .trim();

                    var translations = JSON.parse(text);

                    if (!Array.isArray(translations) || translations.length !== phrases.length) {
                        throw new Error(
                            providerName +
                                " returned " +
                                (translations ? translations.length : 0) +
                                " translations but " +
                                phrases.length +
                                " were expected."
                        );
                    }

                    return translations;
                }
            );
        };

        this.AddTranslations = function (fromLcid, destLcid, updateRecords, translatedPhrases) {
            var translations = [];

            for (var i = 0; i < updateRecords.length; i++) {
                var translated = translatedPhrases[i];
                var record = updateRecords[i];

                if (!translated) {
                    continue;
                }

                var translation = w2utils.encodeTags(translated);

                translations.push({
                    recid: record.recid,
                    targetRecid: record.recid,
                    location: record.location,
                    schemaName: record.schemaName,
                    column: destLcid,
                    source: record[fromLcid],
                    translation: translation,
                    fromDictionary: false
                });
            }

            return translations;
        };

        this.CanTranslate = function (fromLcid, destLcid) {
            return WebApiClient.Promise.resolve({
                [fromLcid]: true,
                [destLcid]: true
            });
        };
    };

    const openAITranslator = function (baseUrl, apiKey, modelName, customPrompt) {
        return new openAICompatibleTranslator("OpenAI", baseUrl, apiKey, modelName, customPrompt);
    };

    const azureTranslator = function (baseUrl, apiKey, modelName, customPrompt) {
        return new openAICompatibleTranslator("Azure", baseUrl, apiKey, modelName, customPrompt, "api-key");
    };

    RegisterTranslationProvider({
        id: "openai",
        text: "OpenAI",
        enabled: IsOpenAIEnabled,
        validate: function (config) {
            if (!config || !config.baseUrl) {
                return "OpenAI: URL is missing. Please configure it via App Settings.";
            }

            if (
                typeof window !== "undefined" &&
                window.location &&
                window.location.protocol === "https:" &&
                /^http:\/\//i.test(config.baseUrl)
            ) {
                return "OpenAI: URL uses HTTP, but Dynamics is loaded over HTTPS. Browser blocks this mixed-content request. Please expose the endpoint over HTTPS and update URL.";
            }

            if (!config.apiKey) {
                return "OpenAI: API Key is missing. Please configure it via App Settings.";
            }

            if (!config.modelName) {
                return "OpenAI: Model Name is missing. Please configure it via App Settings.";
            }

            return null;
        },
        create: function (config) {
            return new openAITranslator(config.baseUrl, config.apiKey, config.modelName, config.customPrompt);
        }
    });

    RegisterTranslationProvider({
        id: "azure",
        text: "Azure",
        enabled: IsAzureEnabled,
        validate: function (config) {
            if (!config || !config.baseUrl) {
                return "Azure: URL is missing. Please configure it via App Settings.";
            }

            if (!config.apiKey) {
                return "Azure: API Key is missing. Please configure it via App Settings.";
            }

            if (!config.modelName) {
                return "Azure: Model Name is missing. Please configure it via App Settings.";
            }

            return null;
        },
        create: function (config) {
            return new azureTranslator(config.baseUrl, config.apiKey, config.modelName, config.customPrompt);
        }
    });

    TranslationHandler.ApplyTranslations = function (selected, results) {
        var grid = XrmTranslator.GetGrid();
        var gridChanged = false;

        function hasSearchValue(value) {
            return value !== null && typeof value !== "undefined" && String(value).trim() !== "";
        }

        function clearEmptySearchState() {
            var hasActiveSearch = false;
            var searchData = grid.searchData || [];

            for (var i = 0; i < searchData.length; i++) {
                if (hasSearchValue(searchData[i] && searchData[i].value)) {
                    hasActiveSearch = true;
                    break;
                }
            }

            if (
                !hasActiveSearch &&
                !hasSearchValue(grid.last && grid.last.search) &&
                typeof grid.searchReset === "function"
            ) {
                grid.searchReset(true);
                grid.refresh();
            }
        }

        var selectedResults =
            selected && selected.length
                ? selected
                      .map(function (select) {
                          return XrmTranslator.GetByRecId(results, select);
                      })
                      .filter(function (result) {
                          return !!result;
                      })
                : results || [];

        for (var i = 0; i < selectedResults.length; i++) {
            var result = selectedResults[i];
            if (!result) {
                continue;
            }

            var targetRecid = result.targetRecid || result.recid;
            var record = XrmTranslator.GetByRecId(XrmTranslator.GetAllRecords(), targetRecid);

            if (!record) {
                continue;
            }

            if (XrmTranslator.ApplyGridChangeValue(record, result.column, GetTranslationResultValue(result))) {
                gridChanged = true;
                grid.refreshRow(record.recid);
            }
        }

        if (gridChanged) {
            XrmTranslator.SetSaveButtonDisabled(!XrmTranslator.HasPendingChanges());
            grid.refresh();
        }

        clearEmptySearchState();
    };

    function ShowTranslationResults(results) {
        function getOptionSetResultLabel(result) {
            var targetRecid = result && (result.targetRecid || result.recid);
            var schemaName = result ? result.schemaName : "";
            var separatorIndex = targetRecid ? String(targetRecid).indexOf("|") : -1;

            if (separatorIndex === -1) {
                return schemaName;
            }

            var attributeId = String(targetRecid).substring(0, separatorIndex);
            var attribute = XrmTranslator.GetAttributeById(attributeId);

            if (!attribute || !attribute.LogicalName) {
                return schemaName;
            }

            return attribute.LogicalName + " | " + schemaName;
        }

        function normalizeResultRecords(rawResults) {
            var normalized = [];

            for (var i = 0; i < rawResults.length; i++) {
                var result = Object.assign({}, rawResults[i]);
                result.targetRecid = result.targetRecid || result.recid;
                result.recid = "translationResult_" + i;
                result.schemaName = getOptionSetResultLabel(result);
                normalized.push(result);
            }

            return normalized;
        }

        if (!w2ui.translationResultGrid) {
            new w2grid({
                name: "translationResultGrid",
                show: { selectColumn: false },
                multiSelect: false,
                columns: [
                    { field: "location", text: "Location", size: "28%", sortable: true, searchable: true },
                    { field: "schemaName", text: "Schema Name", size: "18%", sortable: true, searchable: true },
                    { field: "column", text: "Column LCID", sortable: true, searchable: true, hidden: true },
                    { field: "source", text: "Source Text", size: "21%", sortable: true, searchable: true },
                    {
                        field: "translation",
                        text: "Translated Text",
                        size: "21%",
                        sortable: true,
                        searchable: true,
                        editable: { type: "text" }
                    },
                    {
                        field: "fromDictionary",
                        text: "From Dictionary",
                        size: "12%",
                        sortable: true,
                        searchable: true,
                        render: function (record) {
                            return (
                                '<input type="checkbox" disabled ' + (record.fromDictionary ? "checked" : "") + " />"
                            );
                        }
                    }
                ],
                records: []
            });
        }

        results = normalizeResultRecords(results || []);
        w2ui.translationResultGrid.clear();
        w2ui.translationResultGrid.add(results);

        w2popup.open({
            title: "Apply Translation Results",
            buttons:
                '<button class="w2ui-btn" onclick="w2popup.close();">Cancel</button> ' +
                '<button class="w2ui-btn" onclick="TranslationHandler.ApplyTranslations(null, w2ui.translationResultGrid.records); w2popup.close();">Apply</button>',
            width: 900,
            height: 600,
            showMax: false,
            body: '<div id="main" style="position: absolute; left: 5px; top: 5px; right: 5px; bottom: 5px;"></div>',
            onOpen: function (event) {
                event.onComplete = function () {
                    w2ui.translationResultGrid.render("#w2ui-popup #main");
                    setTimeout(function () {
                        w2popup.max();
                        w2ui.translationResultGrid.resize();
                    }, 100);
                };
            },
            onToggle: function (event) {
                w2ui.translationResultGrid.box.style.display = "none";
                event.onComplete = function () {
                    w2ui.translationResultGrid.box.style.display = "";
                    w2ui.translationResultGrid.resize();
                };
            }
        });
    }

    function FindTranslator(fromLcid, destLcid, apiProviderId) {
        return GetTranslationProviderContext(true).then(function (providerContext) {
            var provider =
                GetTranslationProvider(apiProviderId, providerContext.providers) ||
                GetTranslationProvider(providerContext.selectedProvider, providerContext.providers) ||
                providerContext.providers[0];

            if (!provider) {
                return [null, "No translation provider registered."];
            }

            return GetProviderConfig(provider.id, true).then(function (config) {
                var validationError = provider.validate ? provider.validate(config) : null;
                if (validationError) {
                    return [null, validationError];
                }

                var translator = provider.create(config);
                if (!translator) {
                    return [null, provider.text + ": Failed to initialize translator."];
                }

                if (!translator.CanTranslate) {
                    return [translator];
                }

                return translator.CanTranslate(fromLcid, destLcid).then(function (canTranslate) {
                    if (canTranslate[fromLcid] && canTranslate[destLcid]) {
                        return [translator];
                    }

                    return [
                        null,
                        provider.text +
                            " does not support the current languages: " +
                            fromLcid +
                            "(" +
                            canTranslate[fromLcid] +
                            "), " +
                            destLcid +
                            "(" +
                            canTranslate[destLcid] +
                            ")"
                    ];
                });
            });
        });
    }

    TranslationHandler.ProposeTranslations = function (
        recordsRaw,
        fromLcid,
        destLcid,
        translateMissing,
        apiProvider,
        useDictionaryFirst
    ) {
        XrmTranslator.LockGrid("Translating...");

        var useDictionaryEnabled = normalizeBoolean(useDictionaryFirst, true);

        function shouldIncludeRecord(record, mode) {
            var sourceVal = GetCurrentGridValue(record, fromLcid);
            var targetVal = GetCurrentGridValue(record, destLcid);

            if (mode === "missing") {
                return !HasTranslationText(targetVal);
            }

            if (mode === "overwrite") {
                return true;
            }

            // Backward compatibility with previously saved empty mode.
            return !HasTranslationText(targetVal);
        }

        var mode = (translateMissing || "missing").trim();
        var records = recordsRaw.filter(function (record) {
            return shouldIncludeRecord(record, mode);
        });

        var fromIso = GetLanguageIsoByLcid(fromLcid);
        var toIso = GetLanguageIsoByLcid(destLcid);

        if (!fromIso || !toIso) {
            XrmTranslator.UnlockGrid();

            w2alert(
                "Could not find source or target language mapping, source iso:" + fromIso + ", target iso: " + toIso
            );

            return;
        }

        FindTranslator(fromIso, toIso, apiProvider)
            .then(function (result) {
                var translator = result[0];

                if (!translator) {
                    var errorMsg = result[1] || "(No error message returned - check API response)";
                    XrmTranslator.UnlockGrid();
                    w2alert(errorMsg);
                    return null;
                }

                var updateRecords = [];

                for (var i = 0; i < records.length; i++) {
                    var record = records[i];

                    // Skip records that have no source text
                    var sourceText = GetCurrentGridValue(record, fromLcid);
                    if (!HasTranslationText(sourceText)) {
                        continue;
                    }

                    var updateRecord = Object.assign({}, record);
                    updateRecord[fromLcid] = sourceText;
                    updateRecord[String(fromLcid)] = sourceText;
                    updateRecords.push(updateRecord);
                }

                if (updateRecords.length === 0) {
                    XrmTranslator.UnlockGrid();
                    w2alert(
                        "No records to translate. All selected records have empty source text for the source language."
                    );
                    return null;
                }

                var splitPromise =
                    useDictionaryEnabled && window.DictionaryService && DictionaryService.SplitRecordsByDictionary
                        ? DictionaryService.SplitRecordsByDictionary(fromLcid, destLcid, updateRecords)
                        : Promise.resolve({ matchedResults: [], unmatchedRecords: updateRecords });

                return splitPromise.then(function (split) {
                    var dictionaryResults = split && split.matchedResults ? split.matchedResults : [];
                    var recordsForAi = split && split.unmatchedRecords ? split.unmatchedRecords : updateRecords;

                    if (!recordsForAi || recordsForAi.length === 0) {
                        ShowTranslationResults(dictionaryResults);
                        XrmTranslator.UnlockGrid();
                        return null;
                    }

                    if (translator.GetBatchTranslations) {
                        var phrases = recordsForAi.map(function (record) {
                            return w2utils.decodeTags(GetCurrentGridValue(record, fromLcid));
                        });

                        return translator
                            .GetBatchTranslations(fromIso, toIso, phrases)
                            .then(function (translatedPhrases) {
                                var aiResults = translator.AddTranslations(
                                    fromLcid,
                                    destLcid,
                                    recordsForAi,
                                    translatedPhrases
                                );
                                var mergedResults = dictionaryResults.concat(aiResults);
                                ShowTranslationResults(mergedResults);
                                XrmTranslator.UnlockGrid();
                            });
                    }

                    // Generic per-record mode for future provider extensions.
                    var translationRequests = [];

                    for (var i = 0; i < recordsForAi.length; i++) {
                        var record = recordsForAi[i];

                        translationRequests.push(
                            translator.GetTranslation(
                                fromIso,
                                toIso,
                                w2utils.decodeTags(GetCurrentGridValue(record, fromLcid))
                            )
                        );
                    }

                    return WebApiClient.Promise.all(translationRequests).then(function (responses) {
                        var aiResults = translator.AddTranslations(fromLcid, destLcid, recordsForAi, responses);
                        var mergedResults = dictionaryResults.concat(aiResults);
                        ShowTranslationResults(mergedResults);
                        XrmTranslator.UnlockGrid();
                    });
                });
            })
            .catch(function (error) {
                XrmTranslator.errorHandler(error);
            });
    };

    function UpdateTranslationPromptProviderAvailability(hasAiProviders) {
        var okButton = document.querySelector('#w2ui-popup button[name="ok"]');

        if (okButton) {
            okButton.disabled = !hasAiProviders;
            if (hasAiProviders) {
                okButton.classList.remove("xqt-button-disabled");
            } else {
                okButton.classList.add("xqt-button-disabled");
            }
        }
    }

    function InitializeTranslationPrompt() {
        var languageItems = [];
        var availableLanguages = XrmTranslator.GetGrid().columns;

        for (var i = 0; i < availableLanguages.length; i++) {
            if (availableLanguages[i].field === "schemaName") {
                continue;
            }

            languageItems.push({ id: availableLanguages[i].field, text: GetColumnDisplayText(availableLanguages[i]) });
        }

        var saved = GetSavedTranslationPrompt();
        var translateMissingItems = [
            { id: "missing", text: "All Missing" },
            { id: "overwrite", text: "All Overwrite" }
        ];

        function findItem(items, id) {
            if (!id) return null;
            for (var i = 0; i < items.length; i++) {
                if (String(items[i].id) === String(id)) return items[i];
            }
            return null;
        }

        return GetTranslationProviderContext(true).then(function (providerContext) {
            var apiProviderItems = providerContext.items;
            var hasAiProviders = apiProviderItems.length > 0;
            var defaultApiProvider =
                GetTranslationProvider(providerContext.selectedProvider, providerContext.providers) ||
                providerContext.providers[0];
            var defaultApiProviderItem = defaultApiProvider ? findItem(apiProviderItems, defaultApiProvider.id) : null;
            var savedRecord = {};
            savedRecord.useDictionaryFirst = normalizeBoolean(saved && saved.useDictionaryFirst, true);

            if (saved) {
                var srcItem = findItem(languageItems, saved.sourceLcid);
                var tgtItem = findItem(languageItems, saved.targetLcid);
                if (srcItem) savedRecord.sourceLcid = srcItem;
                if (tgtItem) savedRecord.targetLcid = tgtItem;
                savedRecord.translateMissing =
                    findItem(translateMissingItems, saved.translateMissing) || translateMissingItems[0];
                savedRecord.apiProvider = findItem(apiProviderItems, saved.apiProvider) || defaultApiProviderItem;
            }

            if (!savedRecord.apiProvider && defaultApiProviderItem) {
                savedRecord.apiProvider = defaultApiProviderItem;
            }

            if (w2ui.translationPrompt) {
                w2ui.translationPrompt.destroy();
            }

            if (!w2ui.translationPrompt) {
                new w2form({
                    name: "translationPrompt",
                    style: "border: 0px; background-color: transparent;",
                    formHTML:
                        '<div class="w2ui-page page-0 xqt-translation-prompt-form">' +
                        '    <div class="xqt-translation-prompt-warning" style="' +
                        (hasAiProviders ? "display:none;" : "") +
                        '">No AI Provider has been set up. Open App Settings and configure an AI Provider.</div>' +
                        '    <div class="xqt-translation-prompt-row">' +
                        '        <label class="xqt-translation-prompt-label" for="sourceLcid">Source Lcid: <span class="xqt-required">*</span></label>' +
                        '        <div class="xqt-translation-prompt-control"><input name="sourceLcid" type="list" /></div>' +
                        "    </div>" +
                        '    <div class="xqt-translation-prompt-row">' +
                        '        <label class="xqt-translation-prompt-label" for="targetLcid">Target Lcid: <span class="xqt-required">*</span></label>' +
                        '        <div class="xqt-translation-prompt-control"><input name="targetLcid" type="list" /></div>' +
                        "    </div>" +
                        '    <div class="xqt-translation-prompt-row">' +
                        '        <label class="xqt-translation-prompt-label" for="translateMissing">Translate All:</label>' +
                        '        <div class="xqt-translation-prompt-control"><input name="translateMissing" type="list" /></div>' +
                        "    </div>" +
                        '    <div class="xqt-translation-prompt-row">' +
                        '        <label class="xqt-translation-prompt-label" for="apiProvider">AI Provider:</label>' +
                        '        <div class="xqt-translation-prompt-control"><input name="apiProvider" type="list" /></div>' +
                        "    </div>" +
                        '    <div class="xqt-translation-prompt-row xqt-translation-prompt-check">' +
                        '        <label class="xqt-translation-prompt-label" for="useDictionaryFirst">Use Dictionary as First Priority:</label>' +
                        '        <div class="xqt-translation-prompt-control"><input name="useDictionaryFirst" type="checkbox" /></div>' +
                        "    </div>" +
                        "</div>" +
                        '<div class="w2ui-buttons">' +
                        '    <button class="w2ui-btn" name="cancel">Cancel</button>' +
                        '    <button class="w2ui-btn" name="ok">Ok</button>' +
                        "</div>",
                    fields: [
                        { field: "targetLcid", type: "list", required: true, options: { items: languageItems } },
                        { field: "sourceLcid", type: "list", required: true, options: { items: languageItems } },
                        {
                            field: "translateMissing",
                            type: "list",
                            required: false,
                            options: { items: translateMissingItems }
                        },
                        { field: "apiProvider", type: "list", required: false, options: { items: apiProviderItems } },
                        { field: "useDictionaryFirst", type: "checkbox", required: false }
                    ],
                    record: savedRecord,
                    actions: {
                        ok: function () {
                            if (apiProviderItems.length === 0) {
                                w2alert(
                                    "No AI Provider has been set up. Open App Settings and configure an AI Provider.",
                                    "AI Translate"
                                );
                                return;
                            }

                            if (this.validate().length > 0) {
                                return;
                            }

                            w2popup.close();

                            var sourceLcid = this.record.sourceLcid.id;
                            var targetLcid = this.record.targetLcid.id;
                            var translateMissingVal = this.record.translateMissing
                                ? this.record.translateMissing.id.trim()
                                : "";
                            var apiProviderVal = this.record.apiProvider
                                ? this.record.apiProvider.id
                                : defaultApiProvider
                                  ? defaultApiProvider.id
                                  : "";
                            var useDictionaryFirstVal = normalizeBoolean(this.record.useDictionaryFirst, true);

                            SaveTranslationPrompt({
                                sourceLcid: sourceLcid,
                                targetLcid: targetLcid,
                                translateMissing: translateMissingVal,
                                apiProvider: apiProviderVal,
                                useDictionaryFirst: useDictionaryFirstVal
                            });

                            var recordFilter = null;
                            if (translateMissingVal && translateMissingVal !== "overwrite") {
                                recordFilter = function (record) {
                                    var targetVal = GetCurrentGridValue(record, targetLcid);

                                    // "missing" - only records without target translation
                                    return !HasTranslationText(targetVal);
                                };
                            }

                            var includeGlobalOptionSetDescriptionParent =
                                XrmTranslator.GetType() === "globalOptionSet" &&
                                XrmTranslator.IsDescriptionComponent &&
                                XrmTranslator.IsDescriptionComponent();
                            var includeBranchRecords =
                                ["bpf", "forms", "dashboards"].indexOf(XrmTranslator.GetType()) !== -1 ||
                                includeGlobalOptionSetDescriptionParent;

                            XrmTranslator.ShowRecordSelector(
                                "TranslationHandler.ProposeTranslations",
                                [sourceLcid, targetLcid, translateMissingVal, apiProviderVal, useDictionaryFirstVal],
                                XrmTranslator.GetGrid().getSelection() || [],
                                recordFilter,
                                {
                                    title: "Records to Translate",
                                    sourceLcid: sourceLcid,
                                    leafOnly: true,
                                    includeBranchRecords: includeBranchRecords,
                                    selectAllOnly: true,
                                    excludeEmptySource: true,
                                    emptyMessage:
                                        translateMissingVal === "overwrite"
                                            ? "No records with source text found for the selected source language."
                                            : "No matching records found. Records with empty source text are skipped, and the remaining records already have translations for the target language."
                                }
                            );
                        },
                        cancel: function () {
                            w2popup.close();
                        }
                    }
                });
            }
            return {
                hasAiProviders: hasAiProviders
            };
        });
    }

    TranslationHandler.ShowTranslationPrompt = function () {
        w2popup.open({
            title: "Choose translations source and destination",
            name: "translationPopup",
            body: '<div id="form" class="xqt-translation-prompt-popup-form"><div class="xqt-translation-prompt-loading">Loading AI translate settings...</div></div>',
            style: "padding: 0px; overflow-x: hidden;",
            width: 650,
            height: 440,
            showMax: false,
            onToggle: function (event) {
                if (w2ui.translationPrompt && w2ui.translationPrompt.box) {
                    w2ui.translationPrompt.box.style.display = "none";
                }
                event.onComplete = function () {
                    if (w2ui.translationPrompt && w2ui.translationPrompt.box) {
                        w2ui.translationPrompt.box.style.display = "";
                        w2ui.translationPrompt.resize();
                    }
                };
            },
            onOpen: function (event) {
                event.onComplete = function () {
                    InitializeTranslationPrompt()
                        .then(function (context) {
                            // specifying an onOpen handler instead is equivalent to specifying an onBeforeOpen handler, which would make this code execute too early and hence not deliver.
                            w2ui.translationPrompt.render("#w2ui-popup #form");
                            UpdateTranslationPromptProviderAvailability(context.hasAiProviders);
                        })
                        .catch(function (error) {
                            XrmTranslator.errorHandler(error);
                        });
                };
            }
        });
    };

    function MaskApiKey(key) {
        if (!key || key.length <= 5) return key || "";
        return key.substring(0, 5) + new Array(key.length - 4).join("*");
    }

    function IsMaskedApiKey(value) {
        return String(value || "").indexOf("*") !== -1;
    }

    function GetProviderSettings(aiSettings, providerKey) {
        var providers = aiSettings && aiSettings.providers ? aiSettings.providers : {};
        return providers[providerKey] || {};
    }

    function ResolveApiKey(value, existingValue) {
        if (IsMaskedApiKey(value) && existingValue) {
            return existingValue;
        }

        return value || "";
    }

    function IsProviderEnabledInRecord(record, prefix) {
        return !!(record && record[prefix + "Enabled"]);
    }

    function SetProviderRequiredFields(providerKey, required) {
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

    function SetProviderInputsEnabled(providerKey, enabled) {
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

    function SetProviderRequiredMarks(providerKey, enabled) {
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

    function UpdateProviderEnabledState(providerKey) {
        var enabled = IsProviderEnabledInRecord(w2ui.appSettings && w2ui.appSettings.record, providerKey);

        SetProviderRequiredFields(providerKey, enabled);
        SetProviderInputsEnabled(providerKey, enabled);
        SetProviderRequiredMarks(providerKey, enabled);
    }

    function UpdateAllProviderEnabledStates() {
        UpdateProviderEnabledState("google");
        UpdateProviderEnabledState("openai");
        UpdateProviderEnabledState("azure");
    }

    function ValidateAppSettingsRecord(record) {
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
            if (!IsProviderEnabledInRecord(record, provider.key)) {
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

    function ActivateAppSettingsTab(tabId, providerId) {
        if (!w2ui.appSettings || typeof w2ui.appSettings.goto !== "function") {
            return;
        }

        if (tabId === "tab-ai") {
            w2ui.appSettings.goto(1);
            RenderAppSettingsProviderTabsAfterVisible(providerId);
            return;
        }

        w2ui.appSettings.goto(0);
    }

    function ShowAppSettingsProviderPage(providerId) {
        var normalizedProviderId = NormalizeProviderId(providerId) || "google";
        var pages = document.querySelectorAll("#w2ui-popup .xqt-app-settings-provider-page");

        for (var i = 0; i < pages.length; i++) {
            pages[i].style.display = pages[i].getAttribute("data-provider") === normalizedProviderId ? "block" : "none";
        }

        if (w2ui.appSettings && w2ui.appSettings.record) {
            w2ui.appSettings.record.selectedProvider = normalizedProviderId;
        }
    }

    function RenderAppSettingsProviderTabs(providerId) {
        var target = document.querySelector("#w2ui-popup #app-settings-ai-tabs");
        var activeProvider = NormalizeProviderId(providerId) || "google";

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
                    ShowAppSettingsProviderPage(event.target);
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

        ShowAppSettingsProviderPage(activeProvider);
    }

    function RenderAppSettingsProviderTabsAfterVisible(providerId) {
        var render = function () {
            RenderAppSettingsProviderTabs(providerId);
        };

        if (window.requestAnimationFrame) {
            window.requestAnimationFrame(render);
            return;
        }

        setTimeout(render, 0);
    }

    function DestroyAppSettingsProviderTabs() {
        if (w2ui.appSettingsAiProviderTabs) {
            w2ui.appSettingsAiProviderTabs.destroy();
        }
    }

    function BindAppSettingsTopTabs(providerId) {
        if (!w2ui.appSettings || !w2ui.appSettings.tabs || typeof w2ui.appSettings.tabs.on !== "function") {
            return;
        }

        if (w2ui.appSettings.tabs._appSettingsProviderTabsBound) {
            return;
        }

        w2ui.appSettings.tabs._appSettingsProviderTabsBound = true;
        w2ui.appSettings.tabs.on("click", function (event) {
            if (event.target === "tab-ai") {
                RenderAppSettingsProviderTabsAfterVisible(
                    w2ui.appSettings && w2ui.appSettings.record ? w2ui.appSettings.record.selectedProvider : providerId
                );
            }
        });
    }

    function BuildProviderFormHtml(providerKey, prefix) {
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

    function BuildAISettingsFromRecord(record, currentAISettings) {
        var currentGoogle = GetProviderSettings(currentAISettings, "google");
        var currentOpenAI = GetProviderSettings(currentAISettings, "openai");
        var currentAzure = GetProviderSettings(currentAISettings, "azure");

        return {
            selectedProvider:
                NormalizeProviderId(record && record.selectedProvider) ||
                NormalizeProviderId(currentAISettings && currentAISettings.selectedProvider) ||
                "google",
            providers: {
                google: {
                    enabled: IsProviderEnabledInRecord(record, "google"),
                    baseUrl: record.googleBaseUrl || "",
                    apiKey: ResolveApiKey(record.googleApiKey, currentGoogle.apiKey),
                    modelName: record.googleModelName || "",
                    customPrompt: record.googleCustomPrompt || ""
                },
                openai: {
                    enabled: IsProviderEnabledInRecord(record, "openai"),
                    baseUrl: record.openaiBaseUrl || "",
                    apiKey: ResolveApiKey(record.openaiApiKey, currentOpenAI.apiKey),
                    modelName: record.openaiModelName || "",
                    customPrompt: record.openaiCustomPrompt || ""
                },
                azure: {
                    enabled: IsProviderEnabledInRecord(record, "azure"),
                    baseUrl: record.azureBaseUrl || "",
                    apiKey: ResolveApiKey(record.azureApiKey, currentAzure.apiKey),
                    modelName: record.azureModelName || "",
                    customPrompt: record.azureCustomPrompt || ""
                }
            }
        };
    }

    function InitializeAppSettingsForm() {
        return AppService.GetAISettings(true).then(function (aiSettings) {
            var googleConfig = GetProviderSettings(aiSettings, "google");
            var openaiConfig = GetProviderSettings(aiSettings, "openai");
            var azureConfig = GetProviderSettings(aiSettings, "azure");
            var appSettingsFormHTML =
                '<div class="w2ui-page page-0 xqt-app-settings-general">' +
                '    <div class="xqt-app-settings-row xqt-app-settings-row-checkbox">' +
                '        <label class="xqt-app-settings-label" for="generalDummy">Dummy:</label>' +
                '        <div class="xqt-app-settings-control"><input name="generalDummy" type="checkbox" /></div>' +
                "    </div>" +
                "</div>" +
                '<div class="w2ui-page page-1 xqt-app-settings-ai">' +
                '    <div id="app-settings-ai-tabs" class="xqt-app-settings-provider-tabs"></div>' +
                BuildProviderFormHtml("google", "google") +
                BuildProviderFormHtml("openai", "openai") +
                BuildProviderFormHtml("azure", "azure") +
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
                googleApiKey: MaskApiKey(googleConfig.apiKey),
                googleModelName: googleConfig.modelName || "",
                googleCustomPrompt: googleConfig.customPrompt || "",
                openaiEnabled:
                    openaiConfig.enabled === true &&
                    !!(openaiConfig.baseUrl || openaiConfig.apiKey || openaiConfig.modelName),
                openaiBaseUrl: openaiConfig.baseUrl || "",
                openaiApiKey: MaskApiKey(openaiConfig.apiKey),
                openaiModelName: openaiConfig.modelName || "",
                openaiCustomPrompt: openaiConfig.customPrompt || "",
                azureEnabled:
                    azureConfig.enabled === true &&
                    !!(azureConfig.baseUrl || azureConfig.apiKey || azureConfig.modelName),
                azureBaseUrl: azureConfig.baseUrl || "",
                azureApiKey: MaskApiKey(azureConfig.apiKey),
                azureModelName: azureConfig.modelName || "",
                azureCustomPrompt: azureConfig.customPrompt || "",
                selectedProvider: NormalizeProviderId(aiSettings.selectedProvider) || "google"
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
                    ActivateAppSettingsTab(event.target, appSettingsRecord.selectedProvider);
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
                            UpdateProviderEnabledState(providerKey);
                        };
                    }
                },
                actions: {
                    save: function () {
                        var form = this;
                        var validationErrors = ValidateAppSettingsRecord(form.record);

                        if (validationErrors.length > 0) {
                            w2alert(validationErrors.join("<br>"), "App Settings");
                            return;
                        }

                        w2popup.lock("Saving...", true);

                        AppService.GetAISettings(true)
                            .then(function (latestAISettings) {
                                var settingsToSave = BuildAISettingsFromRecord(form.record, latestAISettings);
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
                selectedProvider: NormalizeProviderId(aiSettings.selectedProvider) || "google"
            };
        });
    }

    TranslationHandler.ShowAppSettings = function () {
        DestroyAppSettingsProviderTabs();
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
                    InitializeAppSettingsForm()
                        .then(function (context) {
                            w2ui.appSettings.render("#w2ui-popup #form");
                            BindAppSettingsTopTabs(context.selectedProvider);
                            ActivateAppSettingsTab("tab-general", context.selectedProvider);
                            UpdateAllProviderEnabledStates();
                            w2ui.appSettings.resize();
                        })
                        .catch(function (error) {
                            XrmTranslator.errorHandler(error);
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
                DestroyAppSettingsProviderTabs();
            }
        });
    };

    TranslationHandler.ShowAISettings = function () {
        return TranslationHandler.ShowAppSettings();
    };

    TranslationHandler.ShowApplyDictionaryPrompt = function () {
        DialogHelper.ShowApplyDictionaryPrompt(ApplyDictionaryToGrid);
    };

    function ApplyDictionaryToGrid(mode) {
        if (!window.DictionaryService || !DictionaryService.SplitRecordsByDictionary) {
            w2alert("Dictionary service is not available.");
            return;
        }

        XrmTranslator.LockGrid("Applying dictionary...");

        XrmService.GetBaseLanguage()
            .then(function (baseLanguage) {
                var baseLcid = String(baseLanguage);
                var targetLcids = XrmTranslator.GetColumns(false).filter(function (c) {
                    return String(c) !== baseLcid;
                });

                if (!targetLcids.length) {
                    XrmTranslator.UnlockGrid();
                    w2alert("No target language columns found.");
                    return;
                }

                var allRecords = XrmTranslator.GetAllRecords();

                function getCurrentValue(record, lcid) {
                    if (
                        record.w2ui &&
                        record.w2ui.changes &&
                        Object.prototype.hasOwnProperty.call(record.w2ui.changes, lcid)
                    ) {
                        return record.w2ui.changes[lcid];
                    }
                    return record[lcid] || record[String(lcid)];
                }

                var promises = targetLcids.map(function (targetLcid) {
                    var filteredRecords = allRecords.filter(function (record) {
                        var sourceVal = getCurrentValue(record, baseLcid);
                        var targetVal = getCurrentValue(record, targetLcid);

                        if (!HasTranslationText(sourceVal)) return false;

                        if (mode === "missing") return !HasTranslationText(targetVal);
                        return true; // overwrite
                    });

                    if (!filteredRecords.length) {
                        return Promise.resolve([]);
                    }

                    return DictionaryService.SplitRecordsByDictionary(baseLcid, targetLcid, filteredRecords).then(
                        function (split) {
                            return split.matchedResults || [];
                        }
                    );
                });

                return Promise.all(promises).then(function (resultsPerLang) {
                    var grid = XrmTranslator.GetGrid();
                    var totalApplied = 0;

                    for (var i = 0; i < resultsPerLang.length; i++) {
                        var results = resultsPerLang[i];
                        for (var j = 0; j < results.length; j++) {
                            var result = results[j];
                            var record = XrmTranslator.GetByRecId(allRecords, result.recid);
                            if (!record) continue;

                            if (XrmTranslator.ApplyGridChangeValue(record, result.column, result.translation)) {
                                totalApplied++;
                                grid.refreshRow(record.recid);
                            }
                        }
                    }

                    XrmTranslator.SetSaveButtonDisabled(!XrmTranslator.HasPendingChanges());

                    XrmTranslator.UnlockGrid();
                    w2alert("Applied " + totalApplied + " dictionary translation(s).");
                });
            })
            .catch(function (error) {
                XrmTranslator.errorHandler(error);
            });
    }

    function GetLocales() {
        if (locales) {
            return Promise.resolve(locales);
        }

        return WebApiClient.Retrieve({
            overriddenSetName: "languagelocale",
            queryParams: "?$select=language,localeid,code"
        }).then(function (result) {
            locales = result.value;

            return locales;
        });
    }

    TranslationHandler.GetLanguageNamesByLcids = function (lcids) {
        return GetLocales().then(function (locales) {
            return lcids.map(function (lcid) {
                var locale =
                    locales.find(function (l) {
                        return l.localeid == lcid;
                    }) || {};

                return {
                    lcid: lcid,
                    locale: locale.language || lcid
                };
            });
        });
    };

    function FormatLanguageColumnText(language, locale) {
        var languageName = locale.language || language;
        var localeCode = locale.code ? " (" + locale.code + ")" : "";

        return languageName + localeCode + " (" + language + ")";
    }

    TranslationHandler.FillLanguageCodes = function (languages, userSettings) {
        var grid = XrmTranslator.GetGrid();
        var languageCount = languages.length;

        // Reset schema name col
        grid.columns[0].size = XrmTranslator.defaultSchemaNameSize;

        return GetLocales().then(function (locales) {
            // 100% full width, minus length of the schema name grid, divided by number of languages is space left for each language
            var columnWidth = (100 - parseInt(XrmTranslator.defaultSchemaNameSize.replace("%"))) / languageCount;

            for (var i = 0; i < languages.length; i++) {
                var language = languages[i];
                var locale =
                    locales.find(function (l) {
                        return l.localeid == language;
                    }) || {};

                var columnText = FormatLanguageColumnText(language, locale);

                grid.addColumn({
                    field: language,
                    text: columnText,
                    size: columnWidth + "%",
                    sortable: true,
                    editable: { type: "text" },
                    render: XrmTranslator.CreateTranslationCellRenderer(language)
                });
                grid.addSearch({ field: language, text: columnText, type: "text" });
            }

            return languages;
        });
    };

    TranslationHandler.FillPortalLanguageCodes = function (portalLanguages) {
        var grid = XrmTranslator.GetGrid();

        // Reset schema name col
        grid.columns[0].size = XrmTranslator.defaultSchemaNameSize;

        var languages = portalLanguages.reduce(function (all, cur) {
            if (!all[cur.adx_PortalLanguageId.adx_languagecode]) {
                all[cur.adx_PortalLanguageId.adx_languagecode] = cur.adx_PortalLanguageId.adx_lcid.toString();
            }
            return all;
        }, {});

        var locales = Object.keys(languages);
        var columnWidth = (100 - parseInt(XrmTranslator.defaultSchemaNameSize.replace("%"))) / locales.length;

        for (var i = 0; i < locales.length; i++) {
            var locale = locales[i];

            var editable = { type: "text" };

            grid.addColumn({
                field: languages[locale],
                text: locale,
                size: columnWidth + "%",
                sortable: true,
                editable: editable,
                render: XrmTranslator.CreateTranslationCellRenderer(languages[locale])
            });
            grid.addSearch({ field: languages[locale], text: locale, type: "text" });
        }

        return languages;
    };

    /**
     * Returns object with adx_websitelanguageid as key and string lcid as value
     */
    TranslationHandler.FindPortalLanguages = function () {
        return WebApiClient.Retrieve({
            entityName: "adx_websitelanguage",
            queryParams:
                "?$select=_adx_websiteid_value&$expand=adx_PortalLanguageId($select=adx_lcid,adx_languagecode,adx_portallanguageid)"
        }).then(function (r) {
            const languages = r.value;
            languages.sort(function (a, b) {
                return ((a.adx_PortalLanguageId || {}).adx_languagecode || "").localeCompare(
                    (b.adx_PortalLanguageId || {}).adx_languagecode || ""
                );
            });

            return languages;
        });
    };

    TranslationHandler.GetAvailableLanguages = function () {
        return XrmService.GetAllNoneBaseLanguageCodes();
    };
})((window.TranslationHandler = window.TranslationHandler || {}));
