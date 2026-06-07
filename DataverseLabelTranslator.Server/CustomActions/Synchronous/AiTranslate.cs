using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class AiTranslate : ICustomAction
    {
        private const string AppSettingsWebResourceName = "pl_/DataverseLabelTranslator/data/AppSettings.xml";
        private const string DictionaryWebResourceName = "pl_/DataverseLabelTranslator/data/TranslationDictionary.xml";
        private const string OperationProviders = "Providers";
        private const string OperationTranslate = "Translate";
        private const string GoogleDefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("AiTranslate Loading operation is not supported.");
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("AiTranslate Saving operation is not supported.");
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("AiTranslate Publishing operation is not supported.");
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("AiTranslate Published operation is not supported.");
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<AiTranslateInput>(json);
            var operation = Normalize(input.operation);

            if (operation == "providers")
            {
                var settings = LoadAiSettings(serviceAdmin);
                return BuildProvidersOutput(settings);
            }

            if (operation == "translate")
            {
                return Translate(serviceAdmin, input);
            }

            throw new InvalidPluginExecutionException("AiTranslate operation is required.");
        }

        private static AiTranslateOutput Translate(IOrganizationService serviceAdmin, AiTranslateInput input)
        {
            var providerId = NormalizeProvider(input.provider);
            var fromLanguage = StringValue(input.fromLanguage);
            var toLanguage = StringValue(input.toLanguage);
            var fromLcid = StringValue(input.fromLcid);
            var toLcid = StringValue(input.toLcid);
            var items = NormalizeTranslateItems(input);
            var useDictionary = input.useDictionary;

            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new InvalidPluginExecutionException("AiTranslate provider is required.");
            }

            if (string.IsNullOrWhiteSpace(fromLanguage) || string.IsNullOrWhiteSpace(toLanguage))
            {
                throw new InvalidPluginExecutionException("AiTranslate source and target languages are required.");
            }

            if (string.Equals(fromLanguage, toLanguage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException("AiTranslate source and target languages must be different.");
            }

            if (items.Count == 0 || items.All(item => string.IsNullOrWhiteSpace(item.text)))
            {
                throw new InvalidPluginExecutionException("AiTranslate phrases are required.");
            }

            var translations = new string[items.Count];
            var usedDictionary = new bool[items.Count];
            var aiPhrases = new List<string>();
            var aiIndexes = new List<int>();
            var dictionary = useDictionary ? LoadDictionary(serviceAdmin) : null;
            var canUseDictionary = useDictionary && IsDictionarySourceMatch(dictionary, fromLcid);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var sourceText = StringValue(item.text);
                var dictionaryMatch = canUseDictionary
                    ? dictionary.Find(sourceText, toLcid)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(dictionaryMatch))
                {
                    translations[i] = dictionaryMatch;
                    usedDictionary[i] = true;
                    continue;
                }

                aiIndexes.Add(i);
                aiPhrases.Add(sourceText);
            }

            if (aiPhrases.Count > 0)
            {
                var settings = LoadAiSettings(serviceAdmin);
                var provider = GetProvider(settings, providerId);
                ValidateProvider(providerId, provider);

                var aiTranslations = CallProvider(providerId, provider, fromLanguage, toLanguage, aiPhrases);
                if (aiTranslations.Count != aiPhrases.Count)
                {
                    throw new InvalidPluginExecutionException($"AiTranslate returned {aiTranslations.Count} translations but {aiPhrases.Count} were expected.");
                }

                for (var i = 0; i < aiTranslations.Count; i++)
                {
                    translations[aiIndexes[i]] = aiTranslations[i];
                }
            }

            return new AiTranslateOutput
            {
                operation = OperationTranslate,
                translations = translations.Select(StringValue).ToList(),
                results = translations
                    .Select((translation, index) => new AiTranslateResultOutput
                    {
                        translation = StringValue(translation),
                        usedDictionary = usedDictionary[index]
                    })
                    .ToList()
            };
        }

        private static List<AiTranslateItemInput> NormalizeTranslateItems(AiTranslateInput input)
        {
            if (input.items != null && input.items.Count > 0)
            {
                return input.items
                    .Select(item => new AiTranslateItemInput
                    {
                        text = StringValue(item?.text)
                    })
                    .ToList();
            }

            return (input.phrases ?? new List<string>())
                .Select(phrase => new AiTranslateItemInput
                {
                    text = StringValue(phrase)
                })
                .ToList();
        }

        private static bool IsDictionarySourceMatch(DictionaryModel dictionary, string fromLcid)
        {
            if (dictionary == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(dictionary.SourceLcid))
            {
                return true;
            }

            return string.Equals(dictionary.SourceLcid, StringValue(fromLcid), StringComparison.OrdinalIgnoreCase);
        }

        private static ProvidersOutput BuildProvidersOutput(AiSettings settings)
        {
            var providers = new List<ProviderItemOutput>();
            AddProviderItem(providers, "google", "Google", settings.providers.google);
            AddProviderItem(providers, "openai", "OpenAI", settings.providers.openai);
            AddProviderItem(providers, "azure", "Azure", settings.providers.azure);

            var selectedProvider = NormalizeProvider(settings.selectedProvider);
            if (!providers.Any(provider => string.Equals(provider.id, selectedProvider, StringComparison.OrdinalIgnoreCase)))
            {
                selectedProvider = providers.Count > 0 ? providers[0].id : string.Empty;
            }

            return new ProvidersOutput
            {
                operation = OperationProviders,
                selectedProvider = selectedProvider,
                providers = providers
            };
        }

        private static void AddProviderItem(List<ProviderItemOutput> providers, string id, string text, ProviderConfig config)
        {
            if (IsProviderConfigured(config))
            {
                providers.Add(new ProviderItemOutput { id = id, text = text });
            }
        }

        private static List<string> CallProvider(string providerId, ProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases)
        {
            if (providerId == "google")
            {
                return CallGoogle(provider, fromLanguage, toLanguage, phrases);
            }

            if (providerId == "openai")
            {
                return CallOpenAiCompatible("OpenAI", provider, fromLanguage, toLanguage, phrases, false);
            }

            if (providerId == "azure")
            {
                return CallOpenAiCompatible("Azure", provider, fromLanguage, toLanguage, phrases, true);
            }

            throw new InvalidPluginExecutionException("AiTranslate provider is not supported: " + providerId);
        }

        private static List<string> CallGoogle(ProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases)
        {
            var baseUrl = TrimTrailingSlash(provider.baseUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = GoogleDefaultBaseUrl;
            }

            var url = baseUrl + "/models/" + Uri.EscapeDataString(provider.modelName) + ":generateContent?key=" + Uri.EscapeDataString(provider.apiKey);
            var prompt = BuildPrompt(fromLanguage, toLanguage, phrases.Count, provider.customPrompt);
            var body = new Dictionary<string, object>
            {
                {
                    "contents",
                    new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "role", "user" },
                            {
                                "parts",
                                new object[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        { "text", prompt + "\n\nLabels to translate:\n" + DevKitJson.Serialize(phrases) }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var response = PostJson(url, null, DevKitJson.Serialize(body));
            var root = DevKitJson.Deserialize(response) as Dictionary<string, object>;
            var text = GetString(GetPath(root, "candidates", 0, "content", "parts", 0, "text"));

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidPluginExecutionException("Google API returned no translation text.");
            }

            return ParseTranslationArray(text);
        }

        private static List<string> CallOpenAiCompatible(string providerName, ProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases, bool useApiKeyHeader)
        {
            var url = BuildOpenAiChatUrl(provider.baseUrl);
            var prompt = BuildPrompt(fromLanguage, toLanguage, phrases.Count, provider.customPrompt);
            var body = new Dictionary<string, object>
            {
                { "model", provider.modelName },
                { "stream", false },
                { "temperature", 0 },
                {
                    "messages",
                    new object[]
                    {
                        new Dictionary<string, object> { { "role", "system" }, { "content", prompt } },
                        new Dictionary<string, object> { { "role", "user" }, { "content", "Labels to translate:\n" + DevKitJson.Serialize(phrases) } }
                    }
                }
            };

            var headers = useApiKeyHeader
                ? new Dictionary<string, string> { { "api-key", provider.apiKey } }
                : new Dictionary<string, string> { { "Authorization", "Bearer " + provider.apiKey } };
            var response = PostJson(url, headers, DevKitJson.Serialize(body));
            var root = DevKitJson.Deserialize(response) as Dictionary<string, object>;
            var text = GetString(GetPath(root, "choices", 0, "message", "content"));

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidPluginExecutionException(providerName + " API returned no translation text.");
            }

            return ParseTranslationArray(text);
        }

        private static string BuildPrompt(string fromLanguage, string toLanguage, int count, string customPrompt)
        {
            return "You are a professional translator for a Microsoft Dynamics CRM / Dataverse system. " +
                "Translate the following labels from " +
                fromLanguage +
                " to " +
                toLanguage +
                ". " +
                (string.IsNullOrWhiteSpace(customPrompt) ? string.Empty : customPrompt + " ") +
                "Return ONLY a valid JSON array of translated strings in the exact same order as provided. " +
                "Do not add any explanation, markdown formatting, or code fences. " +
                "The array must have exactly " +
                count +
                " elements.";
        }

        private static string PostJson(string url, Dictionary<string, string> headers, string body)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        client.Headers[header.Key] = header.Value;
                    }
                }

                try
                {
                    return client.UploadString(url, "POST", body);
                }
                catch (WebException ex)
                {
                    var responseText = ReadWebExceptionResponse(ex);
                    throw new InvalidPluginExecutionException(string.IsNullOrWhiteSpace(responseText) ? ex.Message : responseText, ex);
                }
            }
        }

        private static string ReadWebExceptionResponse(WebException ex)
        {
            if (ex.Response == null)
            {
                return string.Empty;
            }

            using (var stream = ex.Response.GetResponseStream())
            {
                if (stream == null)
                {
                    return string.Empty;
                }

                using (var reader = new System.IO.StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static List<string> ParseTranslationArray(string text)
        {
            var json = StripCodeFence(text);
            var parsed = DevKitJson.Deserialize(json) as List<object>;
            if (parsed == null)
            {
                throw new InvalidPluginExecutionException("AiTranslate provider returned invalid translation JSON.");
            }

            return parsed.Select(item => item == null ? string.Empty : item.ToString()).ToList();
        }

        private static string StripCodeFence(string text)
        {
            var value = StringValue(text).Trim();
            if (value.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = value.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    value = value.Substring(firstNewLine + 1);
                }
            }

            if (value.EndsWith("```", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 3);
            }

            return value.Trim();
        }

        private static object GetPath(object current, params object[] path)
        {
            for (var i = 0; i < path.Length; i++)
            {
                if (current == null)
                {
                    return null;
                }

                if (path[i] is string key)
                {
                    var dictionary = current as Dictionary<string, object>;
                    if (dictionary == null || !dictionary.ContainsKey(key))
                    {
                        return null;
                    }

                    current = dictionary[key];
                    continue;
                }

                if (path[i] is int index)
                {
                    var list = current as List<object>;
                    if (list == null || index < 0 || index >= list.Count)
                    {
                        return null;
                    }

                    current = list[index];
                }
            }

            return current;
        }

        private static string BuildOpenAiChatUrl(string baseUrl)
        {
            var normalized = TrimTrailingSlash(baseUrl);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidPluginExecutionException("AiTranslate provider URL is required.");
            }

            return normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized + "/chat/completions";
        }

        private static void ValidateProvider(string providerId, ProviderConfig provider)
        {
            if (!IsProviderConfigured(provider))
            {
                throw new InvalidPluginExecutionException("AiTranslate provider is disabled or incomplete: " + providerId);
            }
        }

        private static bool IsProviderConfigured(ProviderConfig provider)
        {
            return provider != null &&
                provider.enabled &&
                !string.IsNullOrWhiteSpace(provider.baseUrl) &&
                !string.IsNullOrWhiteSpace(provider.apiKey) &&
                !string.IsNullOrWhiteSpace(provider.modelName);
        }

        private static ProviderConfig GetProvider(AiSettings settings, string providerId)
        {
            if (providerId == "google") return settings.providers.google;
            if (providerId == "openai") return settings.providers.openai;
            if (providerId == "azure") return settings.providers.azure;
            return null;
        }

        private static AiSettings LoadAiSettings(IOrganizationService serviceAdmin)
        {
            var json = ReadAppSettingsJson(serviceAdmin);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AiSettings();
            }

            var settings = DevKitJson.Deserialize<AppSettingsRoot>(json);
            return settings?.ai ?? new AiSettings();
        }

        private static string ReadAppSettingsJson(IOrganizationService serviceAdmin)
        {
            var xmlOrJson = ReadTextWebResource(serviceAdmin, AppSettingsWebResourceName).Trim();
            if (xmlOrJson.StartsWith("{", StringComparison.Ordinal))
            {
                return xmlOrJson;
            }

            var document = XDocument.Parse(xmlOrJson);
            return document.Root?.Value ?? string.Empty;
        }

        private static DictionaryModel LoadDictionary(IOrganizationService serviceAdmin)
        {
            var xml = ReadTextWebResource(serviceAdmin, DictionaryWebResourceName);
            return ParseDictionaryXml(xml);
        }

        private static string ReadTextWebResource(IOrganizationService serviceAdmin, string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("content")
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            query.TopCount = 1;

            var result = serviceAdmin.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                return string.Empty;
            }

            var encoded = result.Entities[0].GetAttributeValue<string>("content");
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        private static DictionaryModel ParseDictionaryXml(string xmlContent)
        {
            var model = new DictionaryModel();
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return model;
            }

            try
            {
                var document = XDocument.Parse(xmlContent);
                var dictionary = document.Root;
                if (dictionary == null)
                {
                    return model;
                }

                model.SourceLcid = StringValue(dictionary.Attribute("sourceLcid")?.Value);

                foreach (var entryNode in document.Descendants("entry"))
                {
                    var sourceText = StringValue(entryNode.Element("sourceText")?.Value).Trim();
                    if (string.IsNullOrWhiteSpace(sourceText))
                    {
                        continue;
                    }

                    var isActive = ParseBoolean(
                        entryNode.Attribute("active")?.Value,
                        ParseBoolean(entryNode.Element("isActive")?.Value, true)
                    );
                    if (!isActive)
                    {
                        continue;
                    }

                    var entry = model.GetOrCreateEntry(sourceText);
                    foreach (var targetNode in entryNode.Descendants("target"))
                    {
                        var targetLcid = StringValue(targetNode.Attribute("lcid")?.Value).Trim();
                        var targetText = StringValue(targetNode.Value);
                        if (!string.IsNullOrWhiteSpace(targetLcid))
                        {
                            entry.Targets[targetLcid] = targetText;
                        }
                    }

                    if (entry.Targets.Count == 0)
                    {
                        var legacySourceLcid = StringValue(entryNode.Element("sourceLcid")?.Value).Trim();
                        var legacyTargetLcid = StringValue(entryNode.Element("targetLcid")?.Value).Trim();
                        var legacyTargetText = StringValue(entryNode.Element("targetText")?.Value);

                        if (string.IsNullOrWhiteSpace(model.SourceLcid) && !string.IsNullOrWhiteSpace(legacySourceLcid))
                        {
                            model.SourceLcid = legacySourceLcid;
                        }

                        if (!string.IsNullOrWhiteSpace(legacyTargetLcid))
                        {
                            entry.Targets[legacyTargetLcid] = legacyTargetText;
                        }
                    }
                }
            }
            catch
            {
                return new DictionaryModel();
            }

            return model;
        }

        private static bool ParseBoolean(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized != "false" && normalized != "0" && normalized != "no";
        }

        public static string BuildLookupKey(string value)
        {
            var decoded = WebUtility.HtmlDecode(StringValue(value));
            var withoutTags = Regex.Replace(decoded, "<[^>]*>", string.Empty);

            return withoutTags
                .Replace("\u00a0", " ")
                .Trim()
                .ToLowerInvariant();
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing AiTranslate input.");
            }

            return input;
        }

        private static string Normalize(string value)
        {
            return StringValue(value).Trim().ToLowerInvariant();
        }

        private static string NormalizeProvider(string provider)
        {
            var normalized = Normalize(provider);
            if (normalized == "gemini" || normalized == "google-gemini") return "google";
            if (normalized == "azure-foundry" || normalized == "azurefoundry") return "azure";
            if (normalized == "openai-compatible" || normalized == "openai_compatible") return "openai";
            return normalized;
        }

        public static string StringValue(string value)
        {
            return value ?? string.Empty;
        }

        private static string GetString(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static string TrimTrailingSlash(string value)
        {
            return StringValue(value).Trim().TrimEnd('/');
        }
    }

    public class AiTranslateInput : CustomActionInput
    {
        public string provider { get; set; }
        public string fromLanguage { get; set; }
        public string toLanguage { get; set; }
        public string fromLcid { get; set; }
        public string toLcid { get; set; }
        public bool useDictionary { get; set; }
        public List<string> phrases { get; set; } = new List<string>();
        public List<AiTranslateItemInput> items { get; set; } = new List<AiTranslateItemInput>();
    }

    public class AiTranslateItemInput
    {
        public string text { get; set; }
    }

    public class AiTranslateOutput
    {
        public string operation { get; set; }
        public List<string> translations { get; set; } = new List<string>();
        public List<AiTranslateResultOutput> results { get; set; } = new List<AiTranslateResultOutput>();
    }

    public class AiTranslateResultOutput
    {
        public string translation { get; set; }
        public bool usedDictionary { get; set; }
    }

    public class ProvidersOutput
    {
        public string operation { get; set; }
        public string selectedProvider { get; set; }
        public List<ProviderItemOutput> providers { get; set; } = new List<ProviderItemOutput>();
    }

    public class ProviderItemOutput
    {
        public string id { get; set; }
        public string text { get; set; }
    }

    public class AppSettingsRoot
    {
        public AiSettings ai { get; set; } = new AiSettings();
    }

    public class AiSettings
    {
        public string selectedProvider { get; set; } = "google";
        public ProviderSettings providers { get; set; } = new ProviderSettings();
    }

    public class ProviderSettings
    {
        public ProviderConfig google { get; set; } = new ProviderConfig();
        public ProviderConfig openai { get; set; } = new ProviderConfig();
        public ProviderConfig azure { get; set; } = new ProviderConfig();
    }

    public class ProviderConfig
    {
        public bool enabled { get; set; }
        public string baseUrl { get; set; }
        public string apiKey { get; set; }
        public string modelName { get; set; }
        public string customPrompt { get; set; }
    }

    public class DictionaryModel
    {
        public string SourceLcid { get; set; }
        public Dictionary<string, DictionaryEntryModel> Entries { get; } = new Dictionary<string, DictionaryEntryModel>();

        public DictionaryEntryModel GetOrCreateEntry(string sourceText)
        {
            var key = AiTranslate.BuildLookupKey(sourceText);
            if (!Entries.ContainsKey(key))
            {
                Entries[key] = new DictionaryEntryModel();
            }

            return Entries[key];
        }

        public string Find(string sourceText, string targetLcid)
        {
            var key = AiTranslate.BuildLookupKey(sourceText);
            var lcid = AiTranslate.StringValue(targetLcid);

            if (!Entries.ContainsKey(key) || !Entries[key].Targets.ContainsKey(lcid))
            {
                return string.Empty;
            }

            return Entries[key].Targets[lcid];
        }
    }

    public class DictionaryEntryModel
    {
        public Dictionary<string, string> Targets { get; } = new Dictionary<string, string>();
    }
}
