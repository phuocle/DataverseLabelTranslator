using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class ContentSnippetAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const string TranslatorType = "content";

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);

            try
            {
                var portalLanguages = RetrievePortalLanguages(context.ServiceAdmin);
                var localeNames = RetrieveLanguageLocales(context.ServiceAdmin);
                var snippets = RetrieveContentSnippets(context.ServiceAdmin);
                var websites = RetrieveWebsites(context.ServiceAdmin, snippets);
                var rows = BuildRows(portalLanguages, snippets, websites);

                return new EasyTranslatorLoadOutput
                {
                    baseLanguage = baseLanguage.ToString(),
                    grid = new EasyTranslatorGridOutput
                    {
                        mode = "tree",
                        title = "Content Snippets",
                        languageColumns = BuildLanguageColumns(portalLanguages, localeNames),
                        rows = rows
                    }
                };
            }
            catch (Exception ex)
            {
                if (IsMissingPortalTable(ex))
                {
                    context.Tracing?.Trace("ContentSnippetAdapter: portal tables are not available. " + ex.Message);
                    return EmptyOutput(baseLanguage);
                }

                throw;
            }
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            if (input.changedRows == null || input.changedRows.Count == 0)
            {
                return new EasyTranslatorSaveOutput();
            }

            try
            {
                var portalLanguages = RetrievePortalLanguages(context.ServiceAdmin);
                var snippets = RetrieveContentSnippets(context.ServiceAdmin);
                var output = new EasyTranslatorSaveOutput();

                foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
                {
                    var changes = GetValidLabelChanges(row);
                    if (changes.Count == 0)
                    {
                        continue;
                    }

                    // gridKey = "content|{websiteid}|{snippetName}"
                    var parts = ParseGridKey(row.gridKey, TranslatorType, 3);
                    var websiteId = ValidateGuid(parts[1], "Content snippet websiteid");
                    var snippetName = parts[2];

                    if (string.IsNullOrWhiteSpace(snippetName))
                    {
                        throw new InvalidPluginExecutionException("Content snippet name is required.");
                    }

                    foreach (var change in changes)
                    {
                        if (string.IsNullOrEmpty(change.Label))
                        {
                            continue;
                        }

                        var language = FindPortalLanguage(portalLanguages, websiteId, change.LanguageCode.ToString());
                        if (language == null)
                        {
                            continue;
                        }

                        var snippet = FindSnippet(snippets, websiteId, language.WebsiteLanguageId, snippetName);
                        UpsertSnippet(context.ServiceAdmin, snippet, websiteId, language.WebsiteLanguageId, snippetName, change.Label);
                        output.changed = true;
                        output.changedRowCount++;
                    }
                }

                return output;
            }
            catch (Exception ex)
            {
                if (IsMissingPortalTable(ex))
                {
                    throw new InvalidPluginExecutionException("Content Snippets require the legacy Power Pages adx_* tables.", ex);
                }

                throw;
            }
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            return new EasyTranslatorSaveOutput();
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            return new EasyTranslatorSaveOutput();
        }

        private static List<EasyTranslatorGridRowOutput> BuildRows(
            List<PortalLanguageInfo> portalLanguages,
            List<ContentSnippetInfo> snippets,
            Dictionary<Guid, string> websites)
        {
            var rows = new List<EasyTranslatorGridRowOutput>();
            var snippetsByWebsite = new Dictionary<Guid, Dictionary<string, List<ContentSnippetInfo>>>();

            foreach (var snippet in snippets)
            {
                if (snippet.WebsiteId == Guid.Empty || string.IsNullOrWhiteSpace(snippet.Name))
                {
                    continue;
                }

                if (!snippetsByWebsite.TryGetValue(snippet.WebsiteId, out var websiteSnippets))
                {
                    websiteSnippets = new Dictionary<string, List<ContentSnippetInfo>>(StringComparer.OrdinalIgnoreCase);
                    snippetsByWebsite[snippet.WebsiteId] = websiteSnippets;
                }

                if (!websiteSnippets.TryGetValue(snippet.Name, out var snippetGroup))
                {
                    snippetGroup = new List<ContentSnippetInfo>();
                    websiteSnippets[snippet.Name] = snippetGroup;
                }

                snippetGroup.Add(snippet);
            }

            var websiteIds = new List<Guid>(snippetsByWebsite.Keys);
            websiteIds.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(GetWebsiteName(websites, a), GetWebsiteName(websites, b)));

            foreach (var websiteId in websiteIds)
            {
                var parent = new EasyTranslatorGridRowOutput
                {
                    Recid = "content:" + websiteId.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, websiteId.ToString("D")),
                    SchemaName = GetWebsiteName(websites, websiteId),
                    RowType = "content.website",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var children = new List<EasyTranslatorGridRowOutput>();
                var names = new List<string>(snippetsByWebsite[websiteId].Keys);
                names.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var snippetName in names)
                {
                    var child = new EasyTranslatorGridRowOutput
                    {
                        Recid = "content:" + websiteId.ToString("D") + ":" + snippetName,
                        GridKey = BuildGridKey(TranslatorType, websiteId.ToString("D"), snippetName),
                        SchemaName = snippetName,
                        RowType = "content.snippet",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = parent.SchemaName }
                    };

                    foreach (var snippet in snippetsByWebsite[websiteId][snippetName])
                    {
                        var language = FindPortalLanguage(portalLanguages, websiteId, snippet.WebsiteLanguageId);
                        if (language != null)
                        {
                            child.SetLanguageValue(language.Lcid, snippet.Value);
                        }
                    }

                    children.Add(child);
                }

                parent.Children = children;
                rows.Add(parent);
            }

            return rows;
        }

        private static List<EasyTranslatorLanguageColumnOutput> BuildLanguageColumns(
            List<PortalLanguageInfo> portalLanguages,
            Dictionary<string, LocaleInfo> localeNames)
        {
            var columns = new List<EasyTranslatorLanguageColumnOutput>();
            var seenLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            portalLanguages.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.LanguageCode, b.LanguageCode));

            foreach (var language in portalLanguages)
            {
                if (string.IsNullOrWhiteSpace(language.Lcid) ||
                    string.IsNullOrWhiteSpace(language.LanguageCode) ||
                    !seenLanguages.Add(language.LanguageCode))
                {
                    continue;
                }

                columns.Add(new EasyTranslatorLanguageColumnOutput
                {
                    field = language.Lcid,
                    text = FormatLanguageColumnText(language, localeNames)
                });
            }

            return columns;
        }

        private static Dictionary<string, LocaleInfo> RetrieveLanguageLocales(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("languagelocale")
            {
                ColumnSet = new ColumnSet("localeid", "language", "code")
            };
            var map = new Dictionary<string, LocaleInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var locale in Helper.RetrieveAll(serviceAdmin, query))
            {
                var localeId = locale.Contains("localeid") && locale["localeid"] != null
                    ? Convert.ToString(locale["localeid"])
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(localeId))
                {
                    continue;
                }

                map[localeId] = new LocaleInfo
                {
                    Language = locale.GetAttributeValue<string>("language") ?? localeId,
                    Code = locale.GetAttributeValue<string>("code") ?? string.Empty
                };
            }

            return map;
        }

        private static string FormatLanguageColumnText(PortalLanguageInfo language, Dictionary<string, LocaleInfo> localeNames)
        {
            if (localeNames != null && localeNames.TryGetValue(language.Lcid, out var locale))
            {
                var localeCode = !string.IsNullOrWhiteSpace(locale.Code)
                    ? " (" + locale.Code + ")"
                    : string.Empty;
                return (locale.Language ?? language.Lcid) + localeCode + " (" + language.Lcid + ")";
            }

            var portalCode = !string.IsNullOrWhiteSpace(language.LanguageCode)
                ? " (" + language.LanguageCode.ToLowerInvariant() + ")"
                : string.Empty;
            return language.Lcid + portalCode + " (" + language.Lcid + ")";
        }

        private static List<PortalLanguageInfo> RetrievePortalLanguages(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("adx_websitelanguage")
            {
                ColumnSet = new ColumnSet("adx_websitelanguageid", "adx_websiteid", "adx_portallanguageid")
            };
            var portalLink = query.AddLink("adx_portallanguage", "adx_portallanguageid", "adx_portallanguageid", JoinOperator.Inner);
            portalLink.EntityAlias = "portal";
            portalLink.Columns = new ColumnSet("adx_lcid", "adx_languagecode");

            var rows = Helper.RetrieveAll(serviceAdmin, query);
            var languages = new List<PortalLanguageInfo>();

            foreach (var row in rows)
            {
                var website = row.GetAttributeValue<EntityReference>("adx_websiteid");
                var lcid = GetAliasedValue(row, "portal.adx_lcid");
                var languageCode = GetAliasedValue(row, "portal.adx_languagecode");

                if (website == null || string.IsNullOrWhiteSpace(lcid) || string.IsNullOrWhiteSpace(languageCode))
                {
                    continue;
                }

                languages.Add(new PortalLanguageInfo
                {
                    WebsiteLanguageId = row.GetAttributeValue<Guid>("adx_websitelanguageid"),
                    WebsiteId = website.Id,
                    Lcid = lcid,
                    LanguageCode = languageCode
                });
            }

            return languages;
        }

        private static List<ContentSnippetInfo> RetrieveContentSnippets(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("adx_contentsnippet")
            {
                ColumnSet = new ColumnSet(
                    "adx_contentsnippetid",
                    "adx_name",
                    "adx_value",
                    "adx_websiteid",
                    "adx_contentsnippetlanguageid")
            };
            query.Criteria.AddCondition("adx_contentsnippetlanguageid", ConditionOperator.NotNull);
            query.Orders.Add(new OrderExpression("adx_name", OrderType.Ascending));

            var snippets = new List<ContentSnippetInfo>();
            foreach (var row in Helper.RetrieveAll(serviceAdmin, query))
            {
                var website = row.GetAttributeValue<EntityReference>("adx_websiteid");
                var language = row.GetAttributeValue<EntityReference>("adx_contentsnippetlanguageid");

                if (website == null || language == null)
                {
                    continue;
                }

                snippets.Add(new ContentSnippetInfo
                {
                    SnippetId = row.GetAttributeValue<Guid>("adx_contentsnippetid"),
                    WebsiteId = website.Id,
                    WebsiteLanguageId = language.Id,
                    Name = row.GetAttributeValue<string>("adx_name") ?? string.Empty,
                    Value = row.GetAttributeValue<string>("adx_value") ?? string.Empty
                });
            }

            return snippets;
        }

        private static Dictionary<Guid, string> RetrieveWebsites(IOrganizationService serviceAdmin, List<ContentSnippetInfo> snippets)
        {
            var websiteIds = new HashSet<Guid>();
            foreach (var snippet in snippets)
            {
                if (snippet.WebsiteId != Guid.Empty)
                {
                    websiteIds.Add(snippet.WebsiteId);
                }
            }

            var websites = new Dictionary<Guid, string>();
            if (websiteIds.Count == 0)
            {
                return websites;
            }

            var query = new QueryExpression("adx_website")
            {
                ColumnSet = new ColumnSet("adx_websiteid", "adx_name")
            };
            var websiteIdValues = new Guid[websiteIds.Count];
            websiteIds.CopyTo(websiteIdValues);
            var ids = new object[websiteIdValues.Length];
            for (var i = 0; i < websiteIdValues.Length; i++)
            {
                ids[i] = websiteIdValues[i];
            }
            query.Criteria.AddCondition("adx_websiteid", ConditionOperator.In, ids);

            foreach (var website in Helper.RetrieveAll(serviceAdmin, query))
            {
                var id = website.GetAttributeValue<Guid>("adx_websiteid");
                if (id != Guid.Empty)
                {
                    websites[id] = website.GetAttributeValue<string>("adx_name") ?? id.ToString("D");
                }
            }

            return websites;
        }

        private static void UpsertSnippet(
            IOrganizationService serviceAdmin,
            ContentSnippetInfo snippet,
            Guid websiteId,
            Guid languageId,
            string snippetName,
            string value)
        {
            if (snippet != null && snippet.SnippetId != Guid.Empty)
            {
                var update = new Entity("adx_contentsnippet", snippet.SnippetId);
                update["adx_value"] = value ?? string.Empty;
                serviceAdmin.Update(update);
                return;
            }

            var create = new Entity("adx_contentsnippet");
            create["adx_name"] = snippetName;
            create["adx_value"] = value ?? string.Empty;
            create["adx_websiteid"] = new EntityReference("adx_website", websiteId);
            create["adx_contentsnippetlanguageid"] = new EntityReference("adx_websitelanguage", languageId);
            serviceAdmin.Create(create);
        }

        private static PortalLanguageInfo FindPortalLanguage(List<PortalLanguageInfo> languages, Guid websiteId, Guid websiteLanguageId)
        {
            foreach (var language in languages)
            {
                if (language.WebsiteId == websiteId && language.WebsiteLanguageId == websiteLanguageId)
                {
                    return language;
                }
            }

            return null;
        }

        private static PortalLanguageInfo FindPortalLanguage(List<PortalLanguageInfo> languages, Guid websiteId, string lcid)
        {
            foreach (var language in languages)
            {
                if (language.WebsiteId == websiteId && string.Equals(language.Lcid, lcid, StringComparison.Ordinal))
                {
                    return language;
                }
            }

            return null;
        }

        private static ContentSnippetInfo FindSnippet(
            List<ContentSnippetInfo> snippets,
            Guid websiteId,
            Guid websiteLanguageId,
            string snippetName)
        {
            foreach (var snippet in snippets)
            {
                if (snippet.WebsiteId == websiteId &&
                    snippet.WebsiteLanguageId == websiteLanguageId &&
                    string.Equals(snippet.Name, snippetName, StringComparison.OrdinalIgnoreCase))
                {
                    return snippet;
                }
            }

            return null;
        }

        private static string GetWebsiteName(Dictionary<Guid, string> websites, Guid websiteId)
        {
            return websites.TryGetValue(websiteId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : websiteId.ToString("D");
        }

        private static string GetAliasedValue(Entity row, string alias)
        {
            if (!row.Contains(alias) || row[alias] == null)
            {
                return string.Empty;
            }

            var aliased = row[alias] as AliasedValue;
            var value = aliased != null ? aliased.Value : row[alias];
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static bool IsMissingPortalTable(Exception ex)
        {
            var text = ex?.ToString() ?? string.Empty;
            return text.IndexOf("adx_contentsnippet", StringComparison.OrdinalIgnoreCase) != -1 ||
                text.IndexOf("adx_websitelanguage", StringComparison.OrdinalIgnoreCase) != -1 ||
                text.IndexOf("adx_portallanguage", StringComparison.OrdinalIgnoreCase) != -1 ||
                text.IndexOf("adx_website", StringComparison.OrdinalIgnoreCase) != -1 ||
                text.IndexOf("was not found", StringComparison.OrdinalIgnoreCase) != -1 ||
                text.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) != -1;
        }

        private static EasyTranslatorLoadOutput EmptyOutput(int baseLanguage)
        {
            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Content Snippets",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private class PortalLanguageInfo
        {
            public Guid WebsiteLanguageId { get; set; }
            public Guid WebsiteId { get; set; }
            public string Lcid { get; set; }
            public string LanguageCode { get; set; }
        }

        private class LocaleInfo
        {
            public string Language { get; set; }
            public string Code { get; set; }
        }

        private class ContentSnippetInfo
        {
            public Guid SnippetId { get; set; }
            public Guid WebsiteId { get; set; }
            public Guid WebsiteLanguageId { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}
