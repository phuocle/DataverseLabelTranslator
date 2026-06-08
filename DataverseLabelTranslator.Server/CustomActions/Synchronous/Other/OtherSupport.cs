using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal static class OtherSupport
    {
        private const string AppSettingsWebResourceName = "pl_/DataverseLabelTranslator/data/AppSettings.xml";
        private const string DictionaryWebResourceName = "pl_/DataverseLabelTranslator/data/TranslationDictionary.xml";

        public static OtherAiSettings LoadAiSettings(IOrganizationService serviceAdmin)
        {
            var json = ReadAppSettingsJson(serviceAdmin);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new OtherAiSettings();
            }

            var settings = DevKitJson.Deserialize<OtherAppSettingsRoot>(json);
            return settings?.ai ?? new OtherAiSettings();
        }

        public static OtherDictionaryModel LoadDictionary(IOrganizationService serviceAdmin)
        {
            var xml = ReadTextWebResource(serviceAdmin, DictionaryWebResourceName);
            return ParseDictionaryXml(xml);
        }

        public static bool IsDictionarySourceMatch(OtherDictionaryModel dictionary, string fromLcid)
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

        public static bool IsProviderConfigured(OtherProviderConfig provider)
        {
            return provider != null &&
                provider.enabled &&
                !string.IsNullOrWhiteSpace(provider.baseUrl) &&
                !string.IsNullOrWhiteSpace(provider.apiKey) &&
                !string.IsNullOrWhiteSpace(provider.modelName);
        }

        public static OtherProviderConfig GetProvider(OtherAiSettings settings, string providerId)
        {
            if (providerId == "google") return settings.providers.google;
            if (providerId == "openai") return settings.providers.openai;
            if (providerId == "azure") return settings.providers.azure;
            return null;
        }

        public static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing Other input.");
            }

            return input;
        }

        public static string Normalize(string value)
        {
            return StringValue(value).Trim().ToLowerInvariant();
        }

        public static string NormalizeProvider(string provider)
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

        public static string GetString(object value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        public static string TrimTrailingSlash(string value)
        {
            return StringValue(value).Trim().TrimEnd('/');
        }

        public static object GetPath(object current, params object[] path)
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

        public static string BuildLookupKey(string value)
        {
            var decoded = WebUtility.HtmlDecode(StringValue(value));
            var withoutTags = Regex.Replace(decoded, "<[^>]*>", string.Empty);

            return withoutTags
                .Replace("\u00a0", " ")
                .Trim()
                .ToLowerInvariant();
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

        private static OtherDictionaryModel ParseDictionaryXml(string xmlContent)
        {
            var model = new OtherDictionaryModel();
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
                return new OtherDictionaryModel();
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
    }
}
