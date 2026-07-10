using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    [ExcludeFromCodeCoverage]
    internal class OtherTranslate
    {
        internal const string OperationName = "Translate";
        internal const string NormalizedOperationName = "translate";
        private const string GoogleDefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        public OtherTranslateOutput Execute(IOrganizationService serviceAdmin, string json)
        {
            var input = OtherSupport.Deserialize<OtherInput>(json);
            return Translate(serviceAdmin, input);
        }

        private static OtherTranslateOutput Translate(IOrganizationService serviceAdmin, OtherInput input)
        {
            var providerId = OtherSupport.NormalizeProvider(input.provider);
            var fromLanguage = OtherSupport.StringValue(input.fromLanguage);
            var toLanguage = OtherSupport.StringValue(input.toLanguage);
            var fromLcid = OtherSupport.StringValue(input.fromLcid);
            var toLcid = OtherSupport.StringValue(input.toLcid);
            var items = NormalizeTranslateItems(input);
            var useDictionary = input.useDictionary;

            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new InvalidPluginExecutionException("Other Translate provider is required.");
            }

            if (string.IsNullOrWhiteSpace(fromLanguage) || string.IsNullOrWhiteSpace(toLanguage))
            {
                throw new InvalidPluginExecutionException("Other Translate source and target languages are required.");
            }

            if (string.Equals(fromLanguage, toLanguage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException("Other Translate source and target languages must be different.");
            }

            if (items.Count == 0 || items.All(item => string.IsNullOrWhiteSpace(item.text)))
            {
                throw new InvalidPluginExecutionException("Other Translate phrases are required.");
            }

            var translations = new string[items.Count];
            var usedDictionary = new bool[items.Count];
            var aiPhrases = new List<string>();
            var aiIndexes = new List<int>();
            var dictionary = useDictionary ? OtherSupport.LoadDictionary(serviceAdmin) : null;
            var canUseDictionary = useDictionary && OtherSupport.IsDictionarySourceMatch(dictionary, fromLcid);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var sourceText = OtherSupport.StringValue(item.text);
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
                var settings = OtherSupport.LoadAiSettings(serviceAdmin);
                var provider = OtherSupport.GetProvider(settings, providerId);
                ValidateProvider(providerId, provider);

                var aiTranslations = CallProvider(providerId, provider, fromLanguage, toLanguage, aiPhrases);
                if (aiTranslations.Count != aiPhrases.Count)
                {
                    throw new InvalidPluginExecutionException($"Other Translate returned {aiTranslations.Count} translations but {aiPhrases.Count} were expected.");
                }

                for (var i = 0; i < aiTranslations.Count; i++)
                {
                    translations[aiIndexes[i]] = aiTranslations[i];
                }
            }

            return new OtherTranslateOutput
            {
                operation = OperationName,
                translations = translations.Select(OtherSupport.StringValue).ToList(),
                results = translations
                    .Select((translation, index) => new OtherTranslateResultOutput
                    {
                        translation = OtherSupport.StringValue(translation),
                        usedDictionary = usedDictionary[index]
                    })
                    .ToList()
            };
        }

        private static List<OtherTranslateItemInput> NormalizeTranslateItems(OtherInput input)
        {
            if (input.items != null && input.items.Count > 0)
            {
                return input.items
                    .Select(item => new OtherTranslateItemInput
                    {
                        text = OtherSupport.StringValue(item?.text)
                    })
                    .ToList();
            }

            return (input.phrases ?? new List<string>())
                .Select(phrase => new OtherTranslateItemInput
                {
                    text = OtherSupport.StringValue(phrase)
                })
                .ToList();
        }

        private static List<string> CallProvider(string providerId, OtherProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases)
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

            throw new InvalidPluginExecutionException("Other Translate provider is not supported: " + providerId);
        }

        private static List<string> CallGoogle(OtherProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases)
        {
            var baseUrl = OtherSupport.TrimTrailingSlash(provider.baseUrl);
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
            var text = OtherSupport.GetString(OtherSupport.GetPath(root, "candidates", 0, "content", "parts", 0, "text"));

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidPluginExecutionException("Google API returned no translation text.");
            }

            return ParseTranslationArray(text);
        }

        private static List<string> CallOpenAiCompatible(string providerName, OtherProviderConfig provider, string fromLanguage, string toLanguage, List<string> phrases, bool useApiKeyHeader)
        {
            var url = BuildOpenAiChatUrl(provider.baseUrl);
            var prompt = BuildPrompt(fromLanguage, toLanguage, phrases.Count, provider.customPrompt);
            var body = new Dictionary<string, object>
            {
                { "model", provider.modelName },
                { "stream", false },
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
            var text = OtherSupport.GetString(OtherSupport.GetPath(root, "choices", 0, "message", "content"));

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
                throw new InvalidPluginExecutionException("Other Translate provider returned invalid translation JSON.");
            }

            return parsed.Select(item => item == null ? string.Empty : item.ToString()).ToList();
        }

        private static string StripCodeFence(string text)
        {
            var value = OtherSupport.StringValue(text).Trim();
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

        private static string BuildOpenAiChatUrl(string baseUrl)
        {
            var normalized = OtherSupport.TrimTrailingSlash(baseUrl);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidPluginExecutionException("Other Translate provider URL is required.");
            }

            return normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : normalized + "/chat/completions";
        }

        private static void ValidateProvider(string providerId, OtherProviderConfig provider)
        {
            if (!OtherSupport.IsProviderConfigured(provider))
            {
                throw new InvalidPluginExecutionException("Other Translate provider is disabled or incomplete: " + providerId);
            }
        }
    }
}
