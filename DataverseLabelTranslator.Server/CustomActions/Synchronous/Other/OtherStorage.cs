using Microsoft.Xrm.Sdk;
using System;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal class OtherStorage
    {
        internal const string ReadAppSettingsOperationName = "ReadAppSettings";
        internal const string WriteAppSettingsOperationName = "WriteAppSettings";
        internal const string ReadDictionaryOperationName = "ReadDictionary";
        internal const string WriteDictionaryOperationName = "WriteDictionary";

        internal static bool IsStorageOperation(string operation)
        {
            var normalized = OtherSupport.Normalize(operation);
            return normalized == OtherSupport.Normalize(ReadAppSettingsOperationName) ||
                normalized == OtherSupport.Normalize(WriteAppSettingsOperationName) ||
                normalized == OtherSupport.Normalize(ReadDictionaryOperationName) ||
                normalized == OtherSupport.Normalize(WriteDictionaryOperationName);
        }

        internal OtherStorageOutput Execute(IOrganizationService serviceAdmin, string json)
        {
            var input = OtherSupport.Deserialize<OtherStorageInput>(json);
            var operation = OtherSupport.Normalize(input.operation);
            var options = ResolveOptions(operation);

            if (operation == OtherSupport.Normalize(WriteAppSettingsOperationName) ||
                operation == OtherSupport.Normalize(WriteDictionaryOperationName))
            {
                var updated = OtherSupport.WriteTextWebResource(
                    serviceAdmin,
                    options.WebResourceName,
                    options.DisplayName,
                    options.Description,
                    input.content,
                    options.DefaultContent);

                return new OtherStorageOutput
                {
                    operation = options.OperationName,
                    content = OtherSupport.ReadTextWebResource(serviceAdmin, options.WebResourceName),
                    webResourceName = updated.GetAttributeValue<string>("name")
                };
            }

            var webResource = OtherSupport.EnsureTextWebResource(
                serviceAdmin,
                options.WebResourceName,
                options.DisplayName,
                options.Description,
                options.DefaultContent);

            return new OtherStorageOutput
            {
                operation = options.OperationName,
                content = OtherSupport.ReadTextWebResource(serviceAdmin, options.WebResourceName),
                webResourceName = webResource.GetAttributeValue<string>("name")
            };
        }

        private static StorageOptions ResolveOptions(string normalizedOperation)
        {
            if (normalizedOperation == OtherSupport.Normalize(ReadAppSettingsOperationName) ||
                normalizedOperation == OtherSupport.Normalize(WriteAppSettingsOperationName))
            {
                return new StorageOptions
                {
                    OperationName = normalizedOperation == OtherSupport.Normalize(ReadAppSettingsOperationName)
                        ? ReadAppSettingsOperationName
                        : WriteAppSettingsOperationName,
                    WebResourceName = OtherSupport.AppSettingsName,
                    DisplayName = "App Settings",
                    Description = "Stores environment-owned app settings for Dataverse Label Translator.",
                    DefaultContent = GetDefaultAppSettingsXml()
                };
            }

            if (normalizedOperation == OtherSupport.Normalize(ReadDictionaryOperationName) ||
                normalizedOperation == OtherSupport.Normalize(WriteDictionaryOperationName))
            {
                return new StorageOptions
                {
                    OperationName = normalizedOperation == OtherSupport.Normalize(ReadDictionaryOperationName)
                        ? ReadDictionaryOperationName
                        : WriteDictionaryOperationName,
                    WebResourceName = OtherSupport.DictionaryName,
                    DisplayName = "Translation Dictionary",
                    Description = "Stores customer dictionary whitelist for Dataverse Label Translator.",
                    DefaultContent = GetDefaultDictionaryXml()
                };
            }

            throw new InvalidPluginExecutionException("Other storage operation is not supported.");
        }

        private static string GetDefaultAppSettingsXml()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                    "<appSettings contentType=\"application/json\"><![CDATA[",
                    "{",
                    "  \"schemaVersion\": 1,",
                    "  \"updatedOn\": \"\",",
                    "  \"updatedBy\": {",
                    "    \"id\": \"\",",
                    "    \"name\": \"\"",
                    "  },",
                    "  \"general\": {},",
                    "  \"ai\": {",
                    "    \"selectedProvider\": \"google\",",
                    "    \"providers\": {",
                    "      \"google\": { \"enabled\": false, \"baseUrl\": \"\", \"apiKey\": \"\", \"modelName\": \"\", \"customPrompt\": \"\" },",
                    "      \"openai\": { \"enabled\": false, \"baseUrl\": \"\", \"apiKey\": \"\", \"modelName\": \"\", \"customPrompt\": \"\" },",
                    "      \"azure\": { \"enabled\": false, \"baseUrl\": \"\", \"apiKey\": \"\", \"modelName\": \"\", \"customPrompt\": \"\" }",
                    "    }",
                    "  }",
                    "}",
                    "]]></appSettings>"
                });
        }

        private static string GetDefaultDictionaryXml()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                    "<dictionary version=\"2.0\" sourceLcid=\"\">",
                    "  <entries>",
                    "  </entries>",
                    "</dictionary>"
                });
        }

        private class StorageOptions
        {
            public string OperationName { get; set; }
            public string WebResourceName { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string DefaultContent { get; set; }
        }
    }
}
