using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class WebResource : ICustomAction
    {
        private const int WebResourceComponentType = 61;
        private const int WebResourceTypeScript = 3;
        private const int WebResourceTypeResx = 12;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;
        private static readonly Regex LocalizedFileRegex = new Regex(@"([0-9]+)\.(js|resx)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LocalizedNameRegex = new Regex(@"([0-9]+)$", RegexOptions.Compiled);
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<LoadingWebResourceInput>(json);
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var baseLanguageStr = baseLanguage.ToString();

            List<Entity> candidates;
            if (string.IsNullOrWhiteSpace(input.solutionId) || string.Equals(input.solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                candidates = RetrieveWebResourcesByLanguage(serviceAdmin, baseLanguageStr);
            }
            else
            {
                if (!Guid.TryParse(input.solutionId, out var solutionId))
                {
                    throw new InvalidPluginExecutionException("WebResource solutionId must be a GUID or all.");
                }
                var ids = GetSolutionWebResourceIds(serviceAdmin, solutionId);
                candidates = RetrieveWebResourcesByIds(serviceAdmin, ids);
            }

            var baseResources = new List<Entity>();
            foreach (var r in candidates)
            {
                if (IsLocalizableResource(r, baseLanguageStr))
                {
                    baseResources.Add(r);
                }
            }

            var groups = new List<WebResourceGroupOutput>();
            var seenGroupKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var baseResource in baseResources)
            {
                var baseName = baseResource.GetAttributeValue<string>("name") ?? string.Empty;
                var baseDisplayName = baseResource.GetAttributeValue<string>("displayname") ?? baseName;
                var groupKey = GetResourceGroupingKey(baseName, baseDisplayName, baseLanguageStr);

                if (!seenGroupKeys.Add(groupKey))
                {
                    continue;
                }

                var siblings = RetrieveSiblingWebResources(serviceAdmin, groupKey);
                var resources = new List<WebResourceOutput>();

                foreach (var sibling in siblings)
                {
                    var lcid = GetResourceLcid(sibling);
                    if (lcid == null)
                    {
                        continue;
                    }

                    WebResourceOutput parsed;
                    try
                    {
                        parsed = ParseWebResource(sibling, lcid);
                    }
                    catch
                    {
                        continue;
                    }

                    if (parsed != null)
                    {
                        resources.Add(parsed);
                    }
                }

                if (resources.Count > 0)
                {
                    groups.Add(new WebResourceGroupOutput
                    {
                        key = groupKey,
                        displayName = GetResourceDisplayName(baseResource, groupKey),
                        resources = resources
                    });
                }
            }

            groups.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));

            return new LoadingWebResourceOutput
            {
                baseLanguage = baseLanguageStr,
                groups = groups
            };
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<SavingWebResourceInput>(json);
            var savedIds = new List<string>();
            var savedIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (input.resourceChanges != null)
            {
                foreach (var change in input.resourceChanges)
                {
                    if (change == null)
                    {
                        continue;
                    }

                    string savedId;
                    if (!string.IsNullOrWhiteSpace(change.webresourceid))
                    {
                        savedId = UpdateWebResource(serviceAdmin, change);
                    }
                    else
                    {
                        savedId = CreateWebResource(serviceAdmin, change);
                    }

                    if (savedIdSet.Add(savedId))
                    {
                        savedIds.Add(savedId);
                    }
                }
            }

            return new SavingWebResourceOutput { webresourceIds = savedIds };
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishingWebResourceInput>(json);
            var ids = GetValidWebResourceIds(input.webresourceIds);

            if (ids.Count > 0)
            {
                serviceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(ids)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return new PublishingWebResourceOutput { webresourceIds = ids };
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishedWebResourceInput>(json);
            Wait(PublishedWaitMilliseconds);
            return new PublishedWebResourceOutput { webresourceIds = GetValidWebResourceIds(input.webresourceIds) };
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<OtherWebResourceInput>(json);
            if (string.IsNullOrWhiteSpace(input.operation))
            {
                throw new InvalidPluginExecutionException("WebResource Other operation is required.");
            }

            return new OtherWebResourceOutput { operation = input.operation };
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing WebResource input.");
            }

            return input;
        }

        private static int GetBaseLanguage(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("organization")
            {
                ColumnSet = new ColumnSet("languagecode"),
                TopCount = 1
            };
            var result = serviceAdmin.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                throw new InvalidPluginExecutionException("Could not retrieve organization base language.");
            }

            return result.Entities[0].GetAttributeValue<int>("languagecode");
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
            var result = serviceAdmin.RetrieveMultiple(query);
            foreach (var component in result.Entities)
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
                var entity = serviceAdmin.Retrieve("webresource", id, new ColumnSet("webresourceid", "name", "displayname", "content", "webresourcetype"));
                if (entity != null)
                {
                    resources.Add(entity);
                }
            }

            return resources;
        }

        private static List<Entity> RetrieveWebResourcesByLanguage(IOrganizationService serviceAdmin, string baseLanguage)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "content", "webresourcetype")
            };
            var nameFilter = new FilterExpression(LogicalOperator.Or);
            nameFilter.AddCondition("name", ConditionOperator.Like, "%" + baseLanguage + "%");
            nameFilter.AddCondition("displayname", ConditionOperator.Like, "%" + baseLanguage + "%");
            query.Criteria.AddFilter(nameFilter);

            var result = serviceAdmin.RetrieveMultiple(query);
            var resources = new List<Entity>();
            foreach (var entity in result.Entities)
            {
                resources.Add(entity);
            }

            return resources;
        }

        private static List<Entity> RetrieveSiblingWebResources(IOrganizationService serviceAdmin, string groupKey)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "displayname", "content", "webresourcetype")
            };
            var nameFilter = new FilterExpression(LogicalOperator.Or);
            nameFilter.AddCondition("name", ConditionOperator.Like, "%" + groupKey + "%");
            nameFilter.AddCondition("displayname", ConditionOperator.Like, "%" + groupKey + "%");
            query.Criteria.AddFilter(nameFilter);

            var result = serviceAdmin.RetrieveMultiple(query);
            var resources = new List<Entity>();
            foreach (var entity in result.Entities)
            {
                resources.Add(entity);
            }

            return resources;
        }

        private static bool IsLocalizableResource(Entity resource, string baseLanguage)
        {
            var match = MatchLocalizedResource(resource);
            return match != null && match.lcid == baseLanguage && (match.format == "js" || match.format == "resx");
        }

        private static LocalizedResourceMatch MatchLocalizedResource(Entity resource)
        {
            var name = resource.GetAttributeValue<string>("name") ?? string.Empty;
            var displayName = resource.GetAttributeValue<string>("displayname") ?? string.Empty;
            var webresourcetype = GetOptionValue(resource, "webresourcetype");
            var candidates = new[] { name, displayName };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                var fileMatch = LocalizedFileRegex.Match(candidate);
                if (fileMatch.Success)
                {
                    return new LocalizedResourceMatch
                    {
                        lcid = fileMatch.Groups[1].Value,
                        format = fileMatch.Groups[2].Value.ToLowerInvariant()
                    };
                }
            }

            if (webresourcetype != WebResourceTypeResx)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                var nameMatch = LocalizedNameRegex.Match(candidate);
                if (nameMatch.Success)
                {
                    return new LocalizedResourceMatch
                    {
                        lcid = nameMatch.Groups[1].Value,
                        format = "resx"
                    };
                }
            }

            return null;
        }

        private static string GetResourceLcid(Entity resource)
        {
            var match = MatchLocalizedResource(resource);
            return match?.lcid;
        }

        private static int GetOptionValue(Entity entity, string attributeName)
        {
            if (entity == null || !entity.Contains(attributeName) || entity[attributeName] == null)
            {
                return 0;
            }

            var option = entity[attributeName] as OptionSetValue;
            if (option != null)
            {
                return option.Value;
            }

            if (entity[attributeName] is int)
            {
                return (int)entity[attributeName];
            }

            throw new InvalidPluginExecutionException($"WebResource {attributeName} has unsupported value type.");
        }

        private static string GetResourceGroupingKey(string name, string displayName, string lcid)
        {
            var index = name.IndexOf(lcid, StringComparison.Ordinal);
            if (index >= 0)
            {
                return name.Substring(0, index);
            }

            index = displayName.IndexOf(lcid, StringComparison.Ordinal);
            if (index >= 0)
            {
                return displayName.Substring(0, index);
            }

            return !string.IsNullOrEmpty(name) ? name : displayName;
        }

        private static string GetResourceDisplayName(Entity resource, string fallback)
        {
            var name = resource.GetAttributeValue<string>("name");
            var displayName = resource.GetAttributeValue<string>("displayname");
            return !string.IsNullOrEmpty(name) ? name : !string.IsNullOrEmpty(displayName) ? displayName : fallback;
        }

        private static WebResourceOutput ParseWebResource(Entity resource, string lcid)
        {
            var id = resource.Id.ToString("D");
            var name = resource.GetAttributeValue<string>("name") ?? string.Empty;
            var displayName = resource.GetAttributeValue<string>("displayname") ?? string.Empty;
            var webresourcetype = GetOptionValue(resource, "webresourcetype");
            var rawBase64 = resource.GetAttributeValue<string>("content") ?? string.Empty;
            var rawText = string.IsNullOrEmpty(rawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(rawBase64));

            var match = MatchLocalizedResource(resource);
            var format = match?.format ?? "json";

            Dictionary<string, string> content;
            if (format == "resx")
            {
                content = ParseResxContent(rawText);
            }
            else
            {
                content = ParseJsonContent(rawText);
            }

            return new WebResourceOutput
            {
                webresourceid = id,
                name = name,
                displayname = displayName,
                lcid = lcid,
                format = format,
                webresourcetype = webresourcetype,
                content = content
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
                var name = dataElem.Attribute("name")?.Value;
                var valueElem = dataElem.Element("value");
                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = valueElem == null ? string.Empty : valueElem.Value;
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

                if (content.ContainsKey(name))
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

            return doc.ToString();
        }

        private static string UpdateWebResource(IOrganizationService serviceAdmin, WebResourceChangeInput change)
        {
            if (!Guid.TryParse(change.webresourceid, out var id))
            {
                throw new InvalidPluginExecutionException($"WebResource webresourceid is not a valid GUID: {change.webresourceid}");
            }

            var current = serviceAdmin.Retrieve("webresource", id, new ColumnSet("content", "webresourcetype", "name"));
            var webresourcetype = GetOptionValue(current, "webresourcetype");
            var rawBase64 = current.GetAttributeValue<string>("content") ?? string.Empty;
            var rawText = string.IsNullOrEmpty(rawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(rawBase64));
            var format = webresourcetype == WebResourceTypeResx ? "resx" : "json";

            Dictionary<string, string> content;
            if (format == "resx")
            {
                content = ParseResxContent(rawText);
            }
            else
            {
                content = ParseJsonContent(rawText);
            }

            if (change.contentChanges != null)
            {
                foreach (var cc in change.contentChanges)
                {
                    if (cc != null && cc.key != null)
                    {
                        content[cc.key] = cc.value ?? string.Empty;
                    }
                }
            }

            var updatedText = format == "resx" ? SerializeResxContent(rawText, content) : DevKitJson.Serialize(content);
            var updatedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedText));

            var entity = new Entity("webresource", id);
            entity["content"] = updatedBase64;
            serviceAdmin.Update(entity);

            return id.ToString("D");
        }

        private static string CreateWebResource(IOrganizationService serviceAdmin, WebResourceChangeInput change)
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
                var baseWebresourcetype = GetOptionValue(baseEntity, "webresourcetype");
                format = baseWebresourcetype == WebResourceTypeResx ? "resx" : "json";
                webresourcetype = baseWebresourcetype;

                var baseRawBase64 = baseEntity.GetAttributeValue<string>("content") ?? string.Empty;
                rawText = string.IsNullOrEmpty(baseRawBase64) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(baseRawBase64));

                if (format == "resx")
                {
                    baseContent = ParseResxContent(rawText);
                }
                else
                {
                    baseContent = ParseJsonContent(rawText);
                }

                var baseLcid = GetResourceLcid(baseEntity);
                var baseName = baseEntity.GetAttributeValue<string>("name") ?? string.Empty;
                var baseDisplayName = baseEntity.GetAttributeValue<string>("displayname") ?? baseName;

                if (!string.IsNullOrEmpty(baseLcid) && !string.IsNullOrEmpty(change.lcid))
                {
                    name = baseName.Replace(baseLcid, change.lcid);
                    displayName = baseDisplayName.Replace(baseLcid, change.lcid);
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

            if (change.contentChanges != null)
            {
                foreach (var cc in change.contentChanges)
                {
                    if (cc != null && cc.key != null)
                    {
                        content[cc.key] = cc.value ?? string.Empty;
                    }
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

            var updatedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedText));

            var newEntity = new Entity("webresource");
            newEntity["name"] = name;
            newEntity["displayname"] = !string.IsNullOrEmpty(displayName) ? displayName : name;
            newEntity["content"] = updatedBase64;
            newEntity["webresourcetype"] = new OptionSetValue(webresourcetype);

            var newId = serviceAdmin.Create(newEntity);
            return newId.ToString("D");
        }

        private static List<string> GetValidWebResourceIds(List<string> ids)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ids == null)
            {
                return result;
            }

            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (Guid.TryParse(id, out _) && seen.Add(id))
                {
                    result.Add(id);
                }
            }

            return result;
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

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }

        private class LocalizedResourceMatch
        {
            public string lcid { get; set; }
            public string format { get; set; }
        }
    }

    public class LoadingWebResourceInput : CustomActionInput
    {
        public string solutionId { get; set; }
    }

    public class SavingWebResourceInput : CustomActionInput
    {
        public List<WebResourceChangeInput> resourceChanges { get; set; } = new List<WebResourceChangeInput>();
    }

    public class PublishingWebResourceInput : CustomActionInput
    {
        public List<string> webresourceIds { get; set; } = new List<string>();
    }

    public class PublishedWebResourceInput : CustomActionInput
    {
        public List<string> webresourceIds { get; set; } = new List<string>();
    }

    public class OtherWebResourceInput : CustomActionInput
    {
    }

    public class WebResourceChangeInput
    {
        public string webresourceid { get; set; }
        public string lcid { get; set; }
        public string baseWebresourceid { get; set; }
        public List<WebResourceContentChange> contentChanges { get; set; } = new List<WebResourceContentChange>();
    }

    public class WebResourceContentChange
    {
        public string key { get; set; }
        public string value { get; set; }
    }

    public class LoadingWebResourceOutput
    {
        public string baseLanguage { get; set; }
        public List<WebResourceGroupOutput> groups { get; set; } = new List<WebResourceGroupOutput>();
    }

    public class SavingWebResourceOutput
    {
        public List<string> webresourceIds { get; set; } = new List<string>();
    }

    public class PublishingWebResourceOutput
    {
        public List<string> webresourceIds { get; set; } = new List<string>();
    }

    public class PublishedWebResourceOutput
    {
        public List<string> webresourceIds { get; set; } = new List<string>();
    }

    public class OtherWebResourceOutput
    {
        public string operation { get; set; }
    }

    public class WebResourceGroupOutput
    {
        public string key { get; set; }
        public string displayName { get; set; }
        public List<WebResourceOutput> resources { get; set; } = new List<WebResourceOutput>();
    }

    public class WebResourceOutput
    {
        public string webresourceid { get; set; }
        public string name { get; set; }
        public string displayname { get; set; }
        public string lcid { get; set; }
        public string format { get; set; }
        public int webresourcetype { get; set; }
        public Dictionary<string, string> content { get; set; } = new Dictionary<string, string>();
    }
}
