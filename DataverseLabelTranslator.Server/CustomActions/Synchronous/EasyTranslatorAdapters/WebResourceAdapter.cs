using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class WebResourceAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int WebResourceComponentType = 61;
        private const int WebResourceTypeScript = 3;
        private const int WebResourceTypeResx = 12;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "webresources";
        private const string PublishKind = "webresource";
        private static readonly Regex LocalizedNameRegex = new Regex(@"\.([0-9]+)(?:\.([^./\\]+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ResxNameLcidSuffixRegex = new Regex(@"([0-9]+)$", RegexOptions.Compiled);
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin).ToString();
            var languages = RetrieveAvailableLanguages(context.ServiceAdmin);
            var groups = LoadGroups(context.ServiceAdmin, input.solutionId, baseLanguage);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var group in groups)
            {
                rows.Add(IsDescriptionComponent(input.component) ? BuildDescriptionRow(group) : BuildGroupRow(group));
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage,
                grid = new EasyTranslatorGridOutput
                {
                    mode = IsDescriptionComponent(input.component) ? "flat" : "tree",
                    title = "Web Resources",
                    languageColumns = BuildLanguageColumns(languages),
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var groupedChanges = IsDescriptionComponent(input.component)
                ? BuildDescriptionChanges(context.ServiceAdmin, input)
                : BuildResourceChanges(context.ServiceAdmin, input);

            foreach (var change in groupedChanges)
            {
                string savedId;
                if (!string.IsNullOrWhiteSpace(change.webresourceid))
                {
                    savedId = change.hasDescription
                        ? UpdateWebResourceDescription(context.ServiceAdmin, change)
                        : UpdateWebResource(context.ServiceAdmin, change);
                }
                else
                {
                    savedId = CreateWebResource(context.ServiceAdmin, change);
                }

                output.changedRowCount++;
                AddPublishTarget(output, seenTargets, PublishKind, savedId);
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var ids = GetPublishTargetIds(input, PublishKind, true);

            if (ids.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(ids)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return ToPublishOutput(PublishKind, ids);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            Wait(PublishedWaitMilliseconds);
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, true));
        }

        private static List<WebResourceGroupInfo> LoadGroups(IOrganizationService serviceAdmin, string solutionIdText, string baseLanguage)
        {
            var solutionResources = GetScopedWebResources(serviceAdmin, solutionIdText);
            var candidates = solutionResources ?? RetrieveWebResourcesByLanguage(serviceAdmin, baseLanguage);

            var groups = new List<WebResourceGroupInfo>();
            var seenGroupKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var candidate in candidates)
            {
                if (!IsLocalizableResource(candidate, baseLanguage))
                {
                    continue;
                }

                var groupKey = GetResourceGroupingKey(candidate, baseLanguage);

                if (!seenGroupKeys.Add(groupKey))
                {
                    continue;
                }

                var group = LoadGroup(serviceAdmin, groupKey, candidate, solutionResources);
                if (group.resources.Count > 0)
                {
                    groups.Add(group);
                }
            }

            groups.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));
            return groups;
        }

        private static WebResourceGroupInfo LoadGroup(IOrganizationService serviceAdmin, string groupKey, Entity baseResource, List<Entity> scopedResources)
        {
            var resources = new List<WebResourceInfo>();
            var siblings = scopedResources ?? RetrieveSiblingWebResources(serviceAdmin, groupKey);
            foreach (var sibling in siblings)
            {
                var match = MatchLocalizedResource(sibling);
                if (!string.Equals(match?.groupKey, groupKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lcid = match.lcid;
                if (lcid == null)
                {
                    continue;
                }

                try
                {
                    var parsed = ParseWebResource(sibling, lcid);
                    if (parsed != null)
                    {
                        resources.Add(parsed);
                    }
                }
                catch (Exception)
                {
                }
            }

            return new WebResourceGroupInfo
            {
                key = groupKey,
                displayName = GetResourceDisplayName(baseResource, groupKey),
                resources = resources
            };
        }

        private static EasyTranslatorGridRowOutput BuildGroupRow(WebResourceGroupInfo group)
        {
            var parent = new EasyTranslatorGridRowOutput
            {
                Recid = "webresource:" + group.key,
                GridKey = BuildGridKey(TranslatorType, group.key),
                SchemaName = group.displayName ?? group.key,
                RowType = "webresource.group",
                IsEditable = false,
                IsTranslatable = false,
                Ai = new EasyTranslatorAiOutput { include = false }
            };

            var children = new List<EasyTranslatorGridRowOutput>();
            foreach (var property in GetGroupProperties(group))
            {
                var child = new EasyTranslatorGridRowOutput
                {
                    Recid = "webresource:" + group.key + ":" + property,
                    GridKey = BuildGridKey(TranslatorType, group.key, property),
                    SchemaName = property,
                    RowType = "webresource.key",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = group.displayName ?? group.key }
                };

                foreach (var resource in group.resources)
                {
                    if (string.IsNullOrWhiteSpace(resource.lcid) || !resource.content.ContainsKey(property))
                    {
                        continue;
                    }

                    child.SetLanguageValue(resource.lcid, resource.content[property]);
                }

                children.Add(child);
            }

            parent.Children = children;
            return parent;
        }

        private static EasyTranslatorGridRowOutput BuildDescriptionRow(WebResourceGroupInfo group)
        {
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "webresource:" + group.key,
                GridKey = BuildGridKey(TranslatorType, group.key, "description"),
                SchemaName = group.displayName ?? group.key,
                RowType = "webresource.description",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true }
            };

            foreach (var resource in group.resources)
            {
                if (!string.IsNullOrWhiteSpace(resource.lcid))
                {
                    row.SetLanguageValue(resource.lcid, resource.description);
                }
            }

            return row;
        }

        private static List<string> GetGroupProperties(WebResourceGroupInfo group)
        {
            var properties = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var resource in group.resources)
            {
                foreach (var property in resource.content.Keys)
                {
                    if (seen.Add(property))
                    {
                        properties.Add(property);
                    }
                }
            }

            properties.Sort(StringComparer.OrdinalIgnoreCase);
            return properties;
        }

        private static List<WebResourceChangeInfo> BuildResourceChanges(IOrganizationService serviceAdmin, EasyTranslatorSaveInput input)
        {
            var changesByTarget = new Dictionary<string, WebResourceChangeInfo>(StringComparer.OrdinalIgnoreCase);
            var baseLanguage = !string.IsNullOrWhiteSpace(input.baseLanguage) ? input.baseLanguage : GetBaseLanguage(serviceAdmin).ToString();
            var groupCache = new Dictionary<string, WebResourceGroupInfo>(StringComparer.Ordinal);
            var scopedResources = GetScopedWebResources(serviceAdmin, input.solutionId);

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var labelChanges = GetValidLabelChanges(row);
                if (labelChanges.Count == 0)
                {
                    continue;
                }

                var parts = ParseGridKey(row.gridKey, TranslatorType, 3);
                var groupKey = parts[1];
                var property = parts[2];

                if (!groupCache.ContainsKey(groupKey))
                {
                    groupCache[groupKey] = LoadGroup(serviceAdmin, groupKey, null, scopedResources);
                }

                var group = groupCache[groupKey];
                foreach (var label in labelChanges)
                {
                    var lcid = label.LanguageCode.ToString();
                    var existingResource = FindResourceByLcid(group, lcid);
                    WebResourceChangeInfo change;
                    if (existingResource != null && !string.IsNullOrWhiteSpace(existingResource.webresourceid))
                    {
                        var changeKey = existingResource.webresourceid;
                        if (!changesByTarget.ContainsKey(changeKey))
                        {
                            changesByTarget[changeKey] = new WebResourceChangeInfo
                            {
                                webresourceid = existingResource.webresourceid
                            };
                        }

                        change = changesByTarget[changeKey];
                    }
                    else
                    {
                        var changeKey = "__new__" + groupKey + "__" + lcid;
                        if (!changesByTarget.ContainsKey(changeKey))
                        {
                            var baseResource = FindResourceByLcid(group, baseLanguage);
                            changesByTarget[changeKey] = new WebResourceChangeInfo
                            {
                                lcid = lcid,
                                baseWebresourceid = baseResource?.webresourceid,
                                solutionId = input.solutionId
                            };
                        }

                        change = changesByTarget[changeKey];
                    }

                    change.contentChanges.Add(new WebResourceContentChangeInfo
                    {
                        key = property,
                        value = label.Label ?? string.Empty
                    });
                }
            }

            return new List<WebResourceChangeInfo>(changesByTarget.Values);
        }

        private static List<WebResourceChangeInfo> BuildDescriptionChanges(IOrganizationService serviceAdmin, EasyTranslatorSaveInput input)
        {
            var changes = new List<WebResourceChangeInfo>();
            var baseLanguage = !string.IsNullOrWhiteSpace(input.baseLanguage) ? input.baseLanguage : GetBaseLanguage(serviceAdmin).ToString();
            var groupCache = new Dictionary<string, WebResourceGroupInfo>(StringComparer.Ordinal);
            var scopedResources = GetScopedWebResources(serviceAdmin, input.solutionId);

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var labelChanges = GetValidLabelChanges(row);
                if (labelChanges.Count == 0)
                {
                    continue;
                }

                var parts = ParseGridKey(row.gridKey, TranslatorType, 3);
                var groupKey = parts[1];
                if (!string.Equals(parts[2], "description", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException("WebResource description save requires a description gridKey.");
                }

                if (!groupCache.ContainsKey(groupKey))
                {
                    groupCache[groupKey] = LoadGroup(serviceAdmin, groupKey, null, scopedResources);
                }

                var group = groupCache[groupKey];
                foreach (var label in labelChanges)
                {
                    var lcid = label.LanguageCode.ToString();
                    var existingResource = FindResourceByLcid(group, lcid);
                    var change = new WebResourceChangeInfo
                    {
                        hasDescription = true,
                        description = label.Label ?? string.Empty
                    };

                    if (existingResource != null && !string.IsNullOrWhiteSpace(existingResource.webresourceid))
                    {
                        change.webresourceid = existingResource.webresourceid;
                    }
                    else
                    {
                        var baseResource = FindResourceByLcid(group, baseLanguage);
                        change.lcid = lcid;
                        change.baseWebresourceid = baseResource?.webresourceid;
                        change.solutionId = input.solutionId;
                    }

                    changes.Add(change);
                }
            }

            return changes;
        }

        private static WebResourceInfo FindResourceByLcid(WebResourceGroupInfo group, string lcid)
        {
            foreach (var resource in group.resources)
            {
                if (string.Equals(resource.lcid, lcid, StringComparison.OrdinalIgnoreCase))
                {
                    return resource;
                }
            }

            return null;
        }

        private static List<Guid> GetSolutionWebResourceIds(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, WebResourceComponentType);

            var ids = new List<Guid>();
            foreach (var component in Helper.RetrieveAll(serviceAdmin, query))
            {
                if (!component.Contains("objectid"))
                {
                    continue;
                }

                var id = component.GetAttributeValue<Guid>("objectid");
                if (id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static List<Entity> RetrieveWebResourcesByIds(IOrganizationService serviceAdmin, List<Guid> ids)
        {
            var resources = new List<Entity>();
            foreach (var id in ids)
            {
                var entity = serviceAdmin.Retrieve("webresource", id, new ColumnSet("webresourceid", "name", "displayname", "description", "content", "webresourcetype", "ismanaged"));
                if (entity != null && !entity.GetAttributeValue<bool>("ismanaged"))
                {
                    resources.Add(entity);
                }
            }

            return resources;
        }

        private static List<Entity> GetScopedWebResources(IOrganizationService serviceAdmin, string solutionIdText)
        {
            if (string.IsNullOrWhiteSpace(solutionIdText) || string.Equals(solutionIdText, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionIdText, out var solutionId))
            {
                throw new InvalidPluginExecutionException("WebResource solutionId must be a GUID or all.");
            }

            return RetrieveWebResourcesByIds(serviceAdmin, GetSolutionWebResourceIds(serviceAdmin, solutionId));
        }

        private static List<int> RetrieveAvailableLanguages(IOrganizationService serviceAdmin)
        {
            var response = (RetrieveAvailableLanguagesResponse)serviceAdmin.Execute(new RetrieveAvailableLanguagesRequest());
            return response.LocaleIds == null ? new List<int>() : new List<int>(response.LocaleIds);
        }

        private static List<EasyTranslatorLanguageColumnOutput> BuildLanguageColumns(List<int> languages)
        {
            var columns = new List<EasyTranslatorLanguageColumnOutput>();
            foreach (var language in languages ?? new List<int>())
            {
                columns.Add(new EasyTranslatorLanguageColumnOutput
                {
                    field = language.ToString(),
                    text = language.ToString()
                });
            }

            return columns;
        }

        private static List<Entity> RetrieveWebResourcesByLanguage(IOrganizationService serviceAdmin, string baseLanguage)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "description", "content", "webresourcetype", "ismanaged")
            };
            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
            var nameFilter = new FilterExpression(LogicalOperator.Or);
            nameFilter.AddCondition("name", ConditionOperator.Like, "%." + baseLanguage + "%");
            var resxSuffixFilter = new FilterExpression(LogicalOperator.And);
            resxSuffixFilter.AddCondition("webresourcetype", ConditionOperator.Equal, WebResourceTypeResx);
            resxSuffixFilter.AddCondition("name", ConditionOperator.Like, "%" + baseLanguage);
            nameFilter.AddFilter(resxSuffixFilter);
            query.Criteria.AddFilter(nameFilter);

            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static List<Entity> RetrieveSiblingWebResources(IOrganizationService serviceAdmin, string groupKey)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "description", "content", "webresourcetype", "ismanaged")
            };
            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
            var nameFilter = new FilterExpression(LogicalOperator.Or);
            nameFilter.AddCondition("name", ConditionOperator.Like, groupKey + ".%");
            var resxSuffixFilter = new FilterExpression(LogicalOperator.And);
            resxSuffixFilter.AddCondition("webresourcetype", ConditionOperator.Equal, WebResourceTypeResx);
            resxSuffixFilter.AddCondition("name", ConditionOperator.Like, groupKey + "%");
            nameFilter.AddFilter(resxSuffixFilter);
            query.Criteria.AddFilter(nameFilter);

            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static bool IsLocalizableResource(Entity resource, string baseLanguage)
        {
            var match = MatchLocalizedResource(resource);
            return match != null && match.lcid == baseLanguage && (match.format == "js" || match.format == "resx");
        }

        private static LocalizedResourceMatch MatchLocalizedResource(Entity resource)
        {
            var name = resource?.GetAttributeValue<string>("name") ?? string.Empty;
            var displayName = resource?.GetAttributeValue<string>("displayname") ?? string.Empty;
            var webresourcetype = GetOptionValue(resource, "webresourcetype") ?? 0;

            var match = MatchLocalizedResourceName(name, webresourcetype);
            if (match != null)
            {
                return match;
            }

            return webresourcetype == WebResourceTypeResx
                ? MatchLocalizedResourceName(displayName, webresourcetype)
                : null;
        }

        private static LocalizedResourceMatch MatchLocalizedResourceName(string value, int webresourcetype)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var nameMatch = LocalizedNameRegex.Match(value);
            if (nameMatch.Success)
            {
                var extension = nameMatch.Groups[2].Success ? nameMatch.Groups[2].Value.ToLowerInvariant() : null;
                return new LocalizedResourceMatch
                {
                    lcid = nameMatch.Groups[1].Value,
                    format = webresourcetype == WebResourceTypeResx || extension == "resx" ? "resx" : "json",
                    token = nameMatch.Groups[1].Value,
                    groupKey = value.Substring(0, nameMatch.Index)
                };
            }

            if (webresourcetype != WebResourceTypeResx)
            {
                return null;
            }

            var suffixMatch = ResxNameLcidSuffixRegex.Match(value);
            if (!suffixMatch.Success)
            {
                return null;
            }

            return new LocalizedResourceMatch
            {
                lcid = suffixMatch.Groups[1].Value,
                format = "resx",
                token = suffixMatch.Groups[1].Value,
                groupKey = value.Substring(0, suffixMatch.Index)
            };
        }

        private static string GetResourceLcid(Entity resource)
        {
            return MatchLocalizedResource(resource)?.lcid;
        }

        private static string GetResourceLanguageToken(Entity resource)
        {
            return MatchLocalizedResource(resource)?.token;
        }

        private static bool IsResxResource(Entity resource)
        {
            var webresourcetype = GetOptionValue(resource, "webresourcetype") ?? 0;
            var match = MatchLocalizedResource(resource);
            return webresourcetype == WebResourceTypeResx || string.Equals(match?.format, "resx", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetResourceGroupingKey(Entity resource, string fallbackToken)
        {
            var name = resource?.GetAttributeValue<string>("name") ?? string.Empty;
            var displayName = resource?.GetAttributeValue<string>("displayname") ?? string.Empty;
            var match = MatchLocalizedResource(resource);
            var groupKey = match?.groupKey;
            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                return groupKey;
            }

            return GetResourceGroupingKey(name, displayName, fallbackToken);
        }

        private static string GetResourceGroupingKey(string name, string displayName, string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var index = name.IndexOf("." + token, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    return name.Substring(0, index);
                }

                index = displayName.IndexOf("." + token, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    return displayName.Substring(0, index);
                }
            }

            return !string.IsNullOrEmpty(name) ? name : displayName;
        }

        private static string GetResourceDisplayName(Entity resource, string fallback)
        {
            if (resource == null)
            {
                return CleanGroupDisplayName(fallback);
            }

            var name = resource.GetAttributeValue<string>("name");
            var displayName = resource.GetAttributeValue<string>("displayname");
            var match = MatchLocalizedResource(resource);
            if (!string.IsNullOrWhiteSpace(match?.groupKey))
            {
                return CleanGroupDisplayName(match.groupKey);
            }

            if (!string.IsNullOrEmpty(match?.token))
            {
                var groupKey = GetResourceGroupingKey(name ?? string.Empty, displayName ?? string.Empty, match.token);
                if (!string.IsNullOrWhiteSpace(groupKey))
                {
                    return CleanGroupDisplayName(groupKey);
                }
            }

            return CleanGroupDisplayName(!string.IsNullOrEmpty(name) ? name : !string.IsNullOrEmpty(displayName) ? displayName : fallback);
        }

        private static string CleanGroupDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var cleaned = value.Trim().TrimEnd('.', '_', '-', ' ');
            var slashIndex = cleaned.LastIndexOf('/');
            return slashIndex >= 0 ? cleaned.Substring(slashIndex + 1) : cleaned;
        }

        private static WebResourceInfo ParseWebResource(Entity resource, string lcid)
        {
            var id = resource.Id.ToString("D");
            var name = resource.GetAttributeValue<string>("name") ?? string.Empty;
            var displayName = resource.GetAttributeValue<string>("displayname") ?? string.Empty;
            var description = resource.GetAttributeValue<string>("description") ?? string.Empty;
            var webresourcetype = GetOptionValue(resource, "webresourcetype") ?? 0;
            var rawBase64 = resource.GetAttributeValue<string>("content") ?? string.Empty;
            var rawText = string.IsNullOrEmpty(rawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(rawBase64));
            var match = MatchLocalizedResource(resource);
            var format = match?.format ?? "json";

            return new WebResourceInfo
            {
                webresourceid = id,
                name = name,
                displayname = displayName,
                description = description,
                lcid = lcid,
                format = format,
                webresourcetype = webresourcetype,
                content = format == "resx" ? ParseResxContent(rawText) : ParseJsonContent(rawText)
            };
        }

        private static Dictionary<string, string> ParseResxContent(string xml)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(xml))
            {
                return result;
            }

            var doc = XDocument.Parse(xml);
            foreach (var dataElem in doc.Root.Elements("data"))
            {
                if (!IsStringResxDataElement(dataElem))
                {
                    continue;
                }

                var name = dataElem.Attribute("name")?.Value;
                var valueElem = dataElem.Element("value");
                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = valueElem.Value;
                }
            }

            return result;
        }

        private static Dictionary<string, string> ParseJsonContent(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            var parsed = DevKitJson.Deserialize(json) as Dictionary<string, object>;
            if (parsed == null)
            {
                return result;
            }

            foreach (var kv in parsed)
            {
                result[kv.Key] = kv.Value == null ? string.Empty : Convert.ToString(kv.Value);
            }

            return result;
        }

        private static string SerializeResxContent(string originalXml, Dictionary<string, string> content)
        {
            var doc = XDocument.Parse(originalXml);
            var existingNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var dataElem in doc.Root.Elements("data"))
            {
                var name = dataElem.Attribute("name")?.Value;
                if (name == null)
                {
                    continue;
                }

                existingNames.Add(name);
                if (IsStringResxDataElement(dataElem) && content.ContainsKey(name))
                {
                    var valueElem = dataElem.Element("value");
                    if (valueElem != null)
                    {
                        valueElem.Value = content[name] ?? string.Empty;
                    }
                }
            }

            foreach (var kv in content)
            {
                if (existingNames.Contains(kv.Key))
                {
                    continue;
                }

                doc.Root.Add(new XElement("data",
                    new XAttribute("name", kv.Key),
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", kv.Value ?? string.Empty)));
            }

            SortResxDataElements(doc);
            return WriteResxDocument(doc);
        }

        private static void SortResxDataElements(XDocument doc)
        {
            var dataElements = new List<XElement>(doc.Root.Elements("data"));
            dataElements.Sort((a, b) => string.Compare(
                a.Attribute("name")?.Value ?? string.Empty,
                b.Attribute("name")?.Value ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));

            foreach (var dataElem in dataElements)
            {
                dataElem.Remove();
            }

            doc.Root.Add(dataElements);
        }

        private static bool IsStringResxDataElement(XElement dataElem)
        {
            return dataElem.Attribute("type") == null
                && dataElem.Attribute("mimetype") == null
                && dataElem.Element("value") != null;
        }

        private static string WriteResxDocument(XDocument doc)
        {
            using (var writer = new Utf8StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    OmitXmlDeclaration = doc.Declaration == null
                }))
                {
                    doc.Save(xmlWriter);
                }

                return writer.ToString();
            }
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        private static string UpdateWebResource(IOrganizationService serviceAdmin, WebResourceChangeInfo change)
        {
            if (!Guid.TryParse(change.webresourceid, out var id))
            {
                throw new InvalidPluginExecutionException($"WebResource webresourceid is not a valid GUID: {change.webresourceid}");
            }

            var current = serviceAdmin.Retrieve("webresource", id, new ColumnSet("content", "webresourcetype", "name"));
            var rawBase64 = current.GetAttributeValue<string>("content") ?? string.Empty;
            var rawText = string.IsNullOrEmpty(rawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(rawBase64));
            var format = IsResxResource(current) ? "resx" : "json";
            var content = format == "resx" ? ParseResxContent(rawText) : ParseJsonContent(rawText);

            foreach (var cc in change.contentChanges ?? new List<WebResourceContentChangeInfo>())
            {
                if (cc?.key != null)
                {
                    content[cc.key] = cc.value ?? string.Empty;
                }
            }

            var updatedText = format == "resx" ? SerializeResxContent(rawText, content) : DevKitJson.Serialize(content);
            var entity = new Entity("webresource", id);
            entity["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedText));
            serviceAdmin.Update(entity);

            return id.ToString("D");
        }

        private static string UpdateWebResourceDescription(IOrganizationService serviceAdmin, WebResourceChangeInfo change)
        {
            if (!Guid.TryParse(change.webresourceid, out var id))
            {
                throw new InvalidPluginExecutionException($"WebResource webresourceid is not a valid GUID: {change.webresourceid}");
            }

            var entity = new Entity("webresource", id);
            entity["description"] = change.description ?? string.Empty;
            serviceAdmin.Update(entity);

            return id.ToString("D");
        }

        private static string CreateWebResource(IOrganizationService serviceAdmin, WebResourceChangeInfo change)
        {
            string format;
            int webresourcetype;
            string rawText;
            Dictionary<string, string> baseContent;
            string name;
            string displayName;

            if (!string.IsNullOrWhiteSpace(change.baseWebresourceid) && Guid.TryParse(change.baseWebresourceid, out var baseId))
            {
                var baseEntity = serviceAdmin.Retrieve("webresource", baseId, new ColumnSet("content", "webresourcetype", "name", "displayname"));
                var baseWebresourcetype = GetOptionValue(baseEntity, "webresourcetype") ?? 0;
                format = IsResxResource(baseEntity) ? "resx" : "json";
                webresourcetype = baseWebresourcetype;

                var baseRawBase64 = baseEntity.GetAttributeValue<string>("content") ?? string.Empty;
                rawText = string.IsNullOrEmpty(baseRawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(baseRawBase64));
                baseContent = format == "resx" ? ParseResxContent(rawText) : ParseJsonContent(rawText);

                var baseToken = GetResourceLanguageToken(baseEntity);
                var baseName = baseEntity.GetAttributeValue<string>("name") ?? string.Empty;
                var baseDisplayName = baseEntity.GetAttributeValue<string>("displayname") ?? baseName;

                if (!string.IsNullOrEmpty(baseToken) && !string.IsNullOrEmpty(change.lcid))
                {
                    name = ReplaceResourceLcid(baseName, baseToken, change.lcid);
                    displayName = ReplaceResourceLcid(baseDisplayName, baseToken, change.lcid);
                }
                else
                {
                    name = baseName;
                    displayName = baseDisplayName;
                }
            }
            else
            {
                format = "json";
                webresourcetype = WebResourceTypeScript;
                rawText = "{}";
                baseContent = new Dictionary<string, string>();
                name = (change.lcid ?? string.Empty) + ".js";
                displayName = name;
            }

            var content = new Dictionary<string, string>();
            foreach (var kv in baseContent)
            {
                content[kv.Key] = null;
            }

            foreach (var cc in change.contentChanges ?? new List<WebResourceContentChangeInfo>())
            {
                if (cc?.key != null)
                {
                    content[cc.key] = cc.value ?? string.Empty;
                }
            }

            string updatedText;
            if (format == "resx")
            {
                updatedText = SerializeResxContent(rawText, content);
            }
            else
            {
                var nonNullContent = new Dictionary<string, string>();
                foreach (var kv in content)
                {
                    nonNullContent[kv.Key] = kv.Value ?? string.Empty;
                }
                updatedText = DevKitJson.Serialize(nonNullContent);
            }

            var newEntity = new Entity("webresource");
            newEntity["name"] = name;
            newEntity["displayname"] = !string.IsNullOrEmpty(displayName) ? displayName : name;
            newEntity["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedText));
            newEntity["webresourcetype"] = new OptionSetValue(webresourcetype);
            if (change.hasDescription)
            {
                newEntity["description"] = change.description ?? string.Empty;
            }

            var createdId = serviceAdmin.Create(newEntity);
            AddWebResourceToSolution(serviceAdmin, createdId, change.solutionId);
            return createdId.ToString("D");
        }

        private static void AddWebResourceToSolution(IOrganizationService serviceAdmin, Guid webresourceId, string solutionIdText)
        {
            var solutionUniqueName = GetSolutionUniqueName(serviceAdmin, solutionIdText);
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
            {
                return;
            }

            serviceAdmin.Execute(new AddSolutionComponentRequest
            {
                ComponentId = webresourceId,
                ComponentType = WebResourceComponentType,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = false
            });
        }

        private static string GetSolutionUniqueName(IOrganizationService serviceAdmin, string solutionIdText)
        {
            if (string.IsNullOrWhiteSpace(solutionIdText) || string.Equals(solutionIdText, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionIdText, out var solutionId))
            {
                throw new InvalidPluginExecutionException("WebResource solutionId must be a GUID or all.");
            }

            var solution = serviceAdmin.Retrieve("solution", solutionId, new ColumnSet("uniquename"));
            return solution?.GetAttributeValue<string>("uniquename");
        }

        private static string BuildPublishXml(List<string> ids)
        {
            var sb = new StringBuilder();
            foreach (var id in ids)
            {
                sb.Append("<webresource>").Append(id).Append("</webresource>");
            }

            return string.Concat("<importexportxml><webresources>", sb.ToString(), "</webresources></importexportxml>");
        }

        private static string ReplaceResourceLcid(string value, string oldLcid, string newLcid)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldLcid) || string.IsNullOrEmpty(newLcid))
            {
                return value;
            }

            var index = value.IndexOf("." + oldLcid, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return value.Substring(0, index) + "." + newLcid + value.Substring(index + oldLcid.Length + 1);
            }

            if (value.EndsWith(oldLcid, StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring(0, value.Length - oldLcid.Length) + newLcid;
            }

            return value;
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }

        private class WebResourceGroupInfo
        {
            public string key { get; set; }
            public string displayName { get; set; }
            public List<WebResourceInfo> resources { get; set; } = new List<WebResourceInfo>();
        }

        private class WebResourceInfo
        {
            public string webresourceid { get; set; }
            public string name { get; set; }
            public string displayname { get; set; }
            public string description { get; set; }
            public string lcid { get; set; }
            public string format { get; set; }
            public int webresourcetype { get; set; }
            public Dictionary<string, string> content { get; set; } = new Dictionary<string, string>();
        }

        private class WebResourceChangeInfo
        {
            public string webresourceid { get; set; }
            public string lcid { get; set; }
            public string baseWebresourceid { get; set; }
            public string solutionId { get; set; }
            public bool hasDescription { get; set; }
            public string description { get; set; }
            public List<WebResourceContentChangeInfo> contentChanges { get; set; } = new List<WebResourceContentChangeInfo>();
        }

        private class WebResourceContentChangeInfo
        {
            public string key { get; set; }
            public string value { get; set; }
        }

        private class LocalizedResourceMatch
        {
            public string lcid { get; set; }
            public string format { get; set; }
            public string token { get; set; }
            public string groupKey { get; set; }
        }
    }
}
