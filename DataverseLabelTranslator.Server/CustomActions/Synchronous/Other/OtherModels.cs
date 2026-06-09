using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class OtherInput : CustomActionInput
    {
        public string provider { get; set; }
        public string fromLanguage { get; set; }
        public string toLanguage { get; set; }
        public string fromLcid { get; set; }
        public string toLcid { get; set; }
        public bool useDictionary { get; set; }
        public List<string> phrases { get; set; } = new List<string>();
        public List<OtherTranslateItemInput> items { get; set; } = new List<OtherTranslateItemInput>();
    }

    public class OtherTranslateItemInput
    {
        public string text { get; set; }
    }

    public class OtherTranslateOutput
    {
        public string operation { get; set; }
        public List<string> translations { get; set; } = new List<string>();
        public List<OtherTranslateResultOutput> results { get; set; } = new List<OtherTranslateResultOutput>();
    }

    public class OtherTranslateResultOutput
    {
        public string translation { get; set; }
        public bool usedDictionary { get; set; }
    }

    public class OtherProvidersOutput
    {
        public string operation { get; set; }
        public string selectedProvider { get; set; }
        public List<OtherProviderItemOutput> providers { get; set; } = new List<OtherProviderItemOutput>();
    }

    public class OtherStorageInput : OtherInput
    {
        public string content { get; set; }
    }

    public class OtherStorageOutput
    {
        public string operation { get; set; }
        public string content { get; set; }
        public string webResourceName { get; set; }
    }

    public class OtherProviderItemOutput
    {
        public string id { get; set; }
        public string text { get; set; }
    }

    public class OtherSolutionsOutput
    {
        public string operation { get; set; }
        public List<OtherSolutionItemOutput> solutions { get; set; } = new List<OtherSolutionItemOutput>();
    }

    public class OtherSolutionItemOutput
    {
        public string solutionid { get; set; }
        public string friendlyname { get; set; }
        public string uniquename { get; set; }
    }

    public class OtherEntitiesInput : OtherInput
    {
        public string solutionId { get; set; }
    }

    public class OtherEntitiesOutput
    {
        public string operation { get; set; }
        public List<OtherEntityItemOutput> entities { get; set; } = new List<OtherEntityItemOutput>();
    }

    public class OtherEntityItemOutput
    {
        public string MetadataId { get; set; }
        public string SchemaName { get; set; }
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
    }

    public class OtherBaseLanguageOutput
    {
        public string operation { get; set; }
        public int languageCode { get; set; }
    }

    public class OtherLanguageCodesOutput
    {
        public string operation { get; set; }
        public List<int> LocaleIds { get; set; } = new List<int>();
    }

    public class OtherAppSettingsRoot
    {
        public OtherAiSettings ai { get; set; } = new OtherAiSettings();
    }

    public class OtherAiSettings
    {
        public string selectedProvider { get; set; } = "google";
        public OtherProviderSettings providers { get; set; } = new OtherProviderSettings();
    }

    public class OtherProviderSettings
    {
        public OtherProviderConfig google { get; set; } = new OtherProviderConfig();
        public OtherProviderConfig openai { get; set; } = new OtherProviderConfig();
        public OtherProviderConfig azure { get; set; } = new OtherProviderConfig();
    }

    public class OtherProviderConfig
    {
        public bool enabled { get; set; }
        public string baseUrl { get; set; }
        public string apiKey { get; set; }
        public string modelName { get; set; }
        public string customPrompt { get; set; }
    }

    public class OtherDictionaryModel
    {
        public string SourceLcid { get; set; }
        public Dictionary<string, OtherDictionaryEntryModel> Entries { get; } = new Dictionary<string, OtherDictionaryEntryModel>();

        public OtherDictionaryEntryModel GetOrCreateEntry(string sourceText)
        {
            var key = OtherSupport.BuildLookupKey(sourceText);
            if (!Entries.ContainsKey(key))
            {
                Entries[key] = new OtherDictionaryEntryModel();
            }

            return Entries[key];
        }

        public string Find(string sourceText, string targetLcid)
        {
            var key = OtherSupport.BuildLookupKey(sourceText);
            var lcid = OtherSupport.StringValue(targetLcid);

            if (!Entries.ContainsKey(key) || !Entries[key].Targets.ContainsKey(lcid))
            {
                return string.Empty;
            }

            return Entries[key].Targets[lcid];
        }
    }

    public class OtherDictionaryEntryModel
    {
        public Dictionary<string, string> Targets { get; } = new Dictionary<string, string>();
    }
}
