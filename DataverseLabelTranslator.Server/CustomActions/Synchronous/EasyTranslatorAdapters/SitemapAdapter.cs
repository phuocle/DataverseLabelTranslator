using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class SitemapAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int SitemapComponentType = 62;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "sitemap";
        private const string AppModulePublishKind = "appmodule";
        private const string SitemapPublishKind = "sitemap";
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var solutionId = ParseSolutionId(input.solutionId, "SiteMap");
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var sitemaps = solutionId.HasValue
                ? RetrieveSitemapsBySolution(context.ServiceAdmin, solutionId.Value)
                : RetrieveAllSitemaps(context.ServiceAdmin);
            var entityLabels = RetrieveReferencedEntityLabels(context.ServiceAdmin, sitemaps);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var sitemap in sitemaps)
            {
                if (string.IsNullOrWhiteSpace(sitemap.sitemapxml))
                {
                    continue;
                }

                var root = new EasyTranslatorGridRowOutput
                {
                    Recid = "sitemap:" + sitemap.sitemapid,
                    GridKey = BuildGridKey(TranslatorType, sitemap.sitemapid),
                    SchemaName = sitemap.sitemapname ?? sitemap.sitemapid,
                    RowType = "sitemap.root",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                root.Children = BuildSitemapNodeRows(sitemap, input.component, baseLanguage, entityLabels);
                rows.Add(root);
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Sitemap",
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                var parts = ParseGridKey(row.gridKey, TranslatorType, 5);
                var sitemapId = ValidateSitemapId(parts[1]);
                var update = new SitemapNodeUpdate
                {
                    sitemapId = sitemapId,
                    nodeType = parts[2],
                    compositeId = parts[3],
                    component = parts[4],
                    labels = changes
                };

                var entity = context.ServiceAdmin.Retrieve("sitemap", sitemapId, new ColumnSet("sitemapxml"));
                var currentXml = Convert.ToString(entity.GetAttributeValue<string>("sitemapxml") ?? string.Empty);
                var updatedXml = UpdateSitemapXml(currentXml, update);
                if (string.Equals(currentXml, updatedXml, StringComparison.Ordinal))
                {
                    continue;
                }

                entity["sitemapxml"] = updatedXml;
                context.ServiceAdmin.Update(entity);

                output.changedRowCount++;
                var appModuleIds = ResolveAppModuleIdsForSitemap(context.ServiceAdmin, sitemapId);
                if (appModuleIds.Count == 0)
                {
                    AddPublishTarget(output, seenTargets, SitemapPublishKind, sitemapId.ToString("D"));
                }

                foreach (var appModuleId in appModuleIds)
                {
                    AddPublishTarget(output, seenTargets, AppModulePublishKind, appModuleId.ToString("D"));
                }
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var appModuleIds = GetPublishTargetIds(input, AppModulePublishKind, true);
            var sitemapIds = GetPublishTargetIds(input, SitemapPublishKind, true);

            if (appModuleIds.Count > 0 || sitemapIds.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(appModuleIds, sitemapIds)
                });
            }

            return ToPublishOutput(appModuleIds, sitemapIds);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            Wait(PublishedWaitMilliseconds);
            return ToPublishOutput(
                GetPublishTargetIds(input, AppModulePublishKind, true),
                GetPublishTargetIds(input, SitemapPublishKind, true));
        }

        private static List<SitemapInfo> RetrieveAllSitemaps(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("sitemap")
            {
                ColumnSet = new ColumnSet("sitemapid", "sitemapname", "sitemapxml")
            };

            return Helper.RetrieveAll(serviceAdmin, query).Select(ToSitemapInfo).ToList();
        }

        private static List<SitemapInfo> RetrieveSitemapsBySolution(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var ids = GetSolutionSitemapIds(serviceAdmin, solutionId);
            if (ids.Count == 0)
            {
                return new List<SitemapInfo>();
            }

            var query = new QueryExpression("sitemap")
            {
                ColumnSet = new ColumnSet("sitemapid", "sitemapname", "sitemapxml")
            };
            query.Criteria.AddCondition("sitemapid", ConditionOperator.In, ids.Cast<object>().ToArray());

            return Helper.RetrieveAll(serviceAdmin, query).Select(ToSitemapInfo).ToList();
        }

        private static SitemapInfo ToSitemapInfo(Entity entity)
        {
            return new SitemapInfo
            {
                sitemapid = GetEntityGuid(entity, "sitemapid").ToString("D"),
                sitemapname = entity.GetAttributeValue<string>("sitemapname"),
                sitemapxml = entity.GetAttributeValue<string>("sitemapxml")
            };
        }

        private static List<Guid> GetSolutionSitemapIds(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, SitemapComponentType);

            var ids = new List<Guid>();
            foreach (var entity in Helper.RetrieveAll(serviceAdmin, query))
            {
                var id = entity.GetAttributeValue<Guid>("objectid");
                if (id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static List<Guid> ResolveAppModuleIdsForSitemap(IOrganizationService serviceAdmin, Guid sitemapId)
        {
            var appModuleIds = new List<Guid>();
            var seen = new HashSet<Guid>();
            var query = new QueryExpression("appmodulecomponent")
            {
                ColumnSet = new ColumnSet("appmoduleidunique"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("componenttype", ConditionOperator.Equal, SitemapComponentType),
                        new ConditionExpression("objectid", ConditionOperator.Equal, sitemapId)
                    }
                }
            };

            var link = query.AddLink("appmodule", "appmoduleidunique", "appmoduleidunique", JoinOperator.Inner);
            link.Columns = new ColumnSet("appmoduleid");
            link.EntityAlias = "app";

            foreach (var component in Helper.RetrieveAll(serviceAdmin, query))
            {
                var appModuleId = GetAliasedGuid(component, "app.appmoduleid");
                if (appModuleId.HasValue && seen.Add(appModuleId.Value))
                {
                    appModuleIds.Add(appModuleId.Value);
                }
            }

            return appModuleIds;
        }

        private static Guid? GetAliasedGuid(Entity entity, string alias)
        {
            if (entity == null || !entity.Attributes.TryGetValue(alias, out var value))
            {
                return null;
            }

            if (value is AliasedValue aliasedValue)
            {
                if (aliasedValue.Value is Guid guid)
                {
                    return guid;
                }

                if (aliasedValue.Value is EntityReference reference)
                {
                    return reference.Id;
                }
            }

            return null;
        }

        private static List<EasyTranslatorGridRowOutput> BuildSitemapNodeRows(SitemapInfo sitemap, string component, int baseLanguage, Dictionary<string, Label> entityLabels)
        {
            var document = XDocument.Parse(sitemap.sitemapxml);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var area in document.Root.Elements("Area"))
            {
                var areaId = GetNodeId(area);
                rows.Add(BuildNodeRow(sitemap, area, "Area", areaId, component, baseLanguage, entityLabels));

                foreach (var group in area.Elements("Group"))
                {
                    var groupId = GetNodeId(group);
                    var groupCompositeId = areaId + "|" + groupId;
                    rows.Add(BuildNodeRow(sitemap, group, "Group", groupCompositeId, component, baseLanguage, entityLabels));

                    foreach (var subArea in group.Elements("SubArea"))
                    {
                        var subAreaId = GetNodeId(subArea);
                        var subAreaCompositeId = areaId + "|" + groupId + "|" + subAreaId;
                        rows.Add(BuildNodeRow(sitemap, subArea, "SubArea", subAreaCompositeId, component, baseLanguage, entityLabels));
                    }
                }
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));
            return rows;
        }

        private static EasyTranslatorGridRowOutput BuildNodeRow(
            SitemapInfo sitemap,
            XElement node,
            string nodeType,
            string compositeId,
            string component,
            int baseLanguage,
            Dictionary<string, Label> entityLabels)
        {
            var nodeId = GetNodeId(node);
            var entityName = (string)node.Attribute("Entity") ?? string.Empty;
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "sitemap:" + sitemap.sitemapid + ":" + nodeType.ToLowerInvariant() + ":" + compositeId,
                GridKey = BuildGridKey(TranslatorType, sitemap.sitemapid, nodeType, compositeId, component),
                SchemaName = "[" + nodeType + "] " + nodeId + (string.IsNullOrWhiteSpace(entityName) ? string.Empty : " (" + entityName + ")"),
                RowType = "sitemap.node",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = sitemap.sitemapname }
            };

            var isDescription = IsDescriptionComponent(component);
            var labels = ExtractXmlLabels(node, isDescription);
            if (labels.Count > 0)
            {
                foreach (var label in labels)
                {
                    row.SetLanguageValue(label.LanguageCode.ToString(), label.Label);
                }
            }
            else if (!isDescription && !string.IsNullOrWhiteSpace(entityName) && entityLabels.ContainsKey(entityName))
            {
                AddLabelValues(row, entityLabels[entityName]);
            }
            else if (!isDescription)
            {
                var defaultTitle = (string)node.Attribute("Title");
                if (!string.IsNullOrWhiteSpace(defaultTitle))
                {
                    row.SetLanguageValue(baseLanguage.ToString(), defaultTitle);
                }
            }

            return row;
        }

        private static List<EasyTranslatorLabelChange> ExtractXmlLabels(XElement node, bool isDescription)
        {
            var labels = new List<EasyTranslatorLabelChange>();
            var containerName = isDescription ? "Descriptions" : "Titles";
            var itemName = isDescription ? "Description" : "Title";
            var attributeName = itemName;
            var container = node.Element(containerName);
            if (container == null)
            {
                return labels;
            }

            foreach (var item in container.Elements(itemName))
            {
                if (int.TryParse((string)item.Attribute("LCID"), out var languageCode))
                {
                    labels.Add(new EasyTranslatorLabelChange
                    {
                        LanguageCode = languageCode,
                        Label = (string)item.Attribute(attributeName) ?? string.Empty
                    });
                }
            }

            return labels;
        }

        private static Dictionary<string, Label> RetrieveReferencedEntityLabels(IOrganizationService serviceAdmin, List<SitemapInfo> sitemaps)
        {
            var labels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
            foreach (var entityName in GetReferencedEntityNames(sitemaps))
            {
                try
                {
                    var response = (RetrieveEntityResponse)serviceAdmin.Execute(new RetrieveEntityRequest
                    {
                        LogicalName = entityName,
                        EntityFilters = EntityFilters.Entity,
                        RetrieveAsIfPublished = true
                    });

                    if (response?.EntityMetadata?.DisplayName != null)
                    {
                        labels[entityName] = response.EntityMetadata.DisplayName;
                    }
                }
                catch (Exception)
                {
                }
            }

            return labels;
        }

        private static List<string> GetReferencedEntityNames(List<SitemapInfo> sitemaps)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sitemap in sitemaps ?? new List<SitemapInfo>())
            {
                if (string.IsNullOrWhiteSpace(sitemap?.sitemapxml))
                {
                    continue;
                }

                try
                {
                    var document = XDocument.Parse(sitemap.sitemapxml);
                    foreach (var subArea in document.Descendants("SubArea"))
                    {
                        var entityName = (string)subArea.Attribute("Entity");
                        if (!string.IsNullOrWhiteSpace(entityName) && seen.Add(entityName))
                        {
                            names.Add(entityName);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return names;
        }

        private static Guid GetEntityGuid(Entity entity, string attributeName)
        {
            if (entity == null)
            {
                return Guid.Empty;
            }

            if (entity.Contains(attributeName) && entity[attributeName] is Guid id)
            {
                return id;
            }

            if (entity.Contains(attributeName) && entity[attributeName] is EntityReference reference)
            {
                return reference.Id;
            }

            return entity.Id;
        }

        private static Guid ValidateSitemapId(string sitemapId)
        {
            if (string.IsNullOrWhiteSpace(sitemapId) || !Guid.TryParse(sitemapId, out var id))
            {
                throw new InvalidPluginExecutionException("SiteMap sitemapId must be a GUID.");
            }

            return id;
        }

        private static string UpdateSitemapXml(string xml, SitemapNodeUpdate update)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new InvalidPluginExecutionException("SiteMap sitemapxml is empty.");
            }

            if (update?.labels == null || update.labels.Count == 0)
            {
                return xml;
            }

            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var node = FindSitemapNode(document.Root, update.compositeId, update.nodeType);
            if (node == null)
            {
                throw new InvalidPluginExecutionException($"SiteMap node was not found for gridKey path: {update.compositeId}");
            }

            var isDescription = IsDescriptionComponent(update.component);
            var containerName = isDescription ? "Descriptions" : "Titles";
            var itemName = isDescription ? "Description" : "Title";
            var attributeName = itemName;
            var container = node.Element(containerName);
            var changed = false;

            foreach (var label in update.labels)
            {
                if (container == null)
                {
                    container = new XElement(containerName);
                    InsertContainerInSchemaOrder(node, container, update.nodeType);
                    changed = true;
                }

                var existing = container.Elements(itemName).FirstOrDefault(element =>
                    string.Equals((string)element.Attribute("LCID"), label.LanguageCode.ToString(), StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new XElement(itemName);
                    container.Add(existing);
                    changed = true;
                }

                var nextLabel = label.Label ?? string.Empty;
                if (!string.Equals((string)existing.Attribute("LCID"), label.LanguageCode.ToString(), StringComparison.Ordinal) ||
                    !string.Equals((string)existing.Attribute(attributeName), nextLabel, StringComparison.Ordinal))
                {
                    existing.SetAttributeValue("LCID", label.LanguageCode);
                    existing.SetAttributeValue(attributeName, nextLabel);
                    changed = true;
                }
            }

            return changed ? document.ToString(SaveOptions.DisableFormatting) : xml;
        }

        // Inserts a <Titles> or <Descriptions> container into a Sitemap node while
        // preserving the element order required by the SiteMap XSD. The required
        // order is Titles, Descriptions, then child nodes (Group for Area,
        // SubArea for Group, nothing for SubArea). node.Add() would place the new
        // container at the end, which causes XSD validation to fail when a child
        // node (Group / SubArea) already exists before Titles/Descriptions.
        private static void InsertContainerInSchemaOrder(XElement node, XElement container, string nodeType)
        {
            if (node == null || container == null)
            {
                return;
            }

            var containerName = container.Name.LocalName;
            var siblingOrder = string.Equals(nodeType, "SubArea", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Titles", "Descriptions" }
                : new[] { "Titles", "Descriptions", GetChildNodeName(nodeType) };

            XElement insertAfter = null;
            foreach (var name in siblingOrder)
            {
                if (string.Equals(name, containerName, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var existing = node.Element(name);
                if (existing != null)
                {
                    insertAfter = existing;
                }
            }

            if (insertAfter == null)
            {
                node.AddFirst(container);
            }
            else
            {
                insertAfter.AddAfterSelf(container);
            }
        }

        private static string GetChildNodeName(string nodeType)
        {
            if (string.Equals(nodeType, "Area", StringComparison.OrdinalIgnoreCase))
            {
                return "Group";
            }

            if (string.Equals(nodeType, "Group", StringComparison.OrdinalIgnoreCase))
            {
                return "SubArea";
            }

            return null;
        }

        private static XElement FindSitemapNode(XElement root, string compositeId, string nodeType)
        {
            if (root == null || compositeId == null)
            {
                return null;
            }

            var identifiers = compositeId.Split(new[] { "|" }, StringSplitOptions.None);
            foreach (var area in root.Elements("Area"))
            {
                if (!string.Equals(GetNodeId(area), identifiers[0], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (identifiers.Length == 1)
                {
                    return IsNodeType(area, nodeType) ? area : null;
                }

                foreach (var group in area.Elements("Group"))
                {
                    if (!string.Equals(GetNodeId(group), identifiers[1], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (identifiers.Length == 2)
                    {
                        return IsNodeType(group, nodeType) ? group : null;
                    }

                    foreach (var subArea in group.Elements("SubArea"))
                    {
                        if (string.Equals(GetNodeId(subArea), identifiers[2], StringComparison.OrdinalIgnoreCase))
                        {
                            return identifiers.Length == 3 && IsNodeType(subArea, nodeType) ? subArea : null;
                        }
                    }

                    return null;
                }

                return null;
            }

            return null;
        }

        private static string GetNodeId(XElement node)
        {
            return (string)node?.Attribute("Id") ?? string.Empty;
        }

        private static bool IsNodeType(XElement node, string nodeType)
        {
            return node != null &&
                (string.IsNullOrWhiteSpace(nodeType) || string.Equals(node.Name.LocalName, nodeType, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildPublishXml(List<string> appModuleIds, List<string> sitemapIds)
        {
            var appModules = new StringBuilder();
            foreach (var appModuleId in appModuleIds)
            {
                appModules.Append("<appmodule>").Append(appModuleId).Append("</appmodule>");
            }

            var sitemaps = new StringBuilder();
            foreach (var sitemapId in sitemapIds)
            {
                sitemaps.Append("<sitemap>").Append(sitemapId).Append("</sitemap>");
            }

            return string.Concat(
                "<importexportxml><appmodules>",
                appModules.ToString(),
                "</appmodules><sitemaps>",
                sitemaps.ToString(),
                "</sitemaps></importexportxml>");
        }

        private static EasyTranslatorSaveOutput ToPublishOutput(List<string> appModuleIds, List<string> sitemapIds)
        {
            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var appModuleId in appModuleIds ?? new List<string>())
            {
                AddPublishTarget(output, seen, AppModulePublishKind, appModuleId);
            }

            foreach (var sitemapId in sitemapIds ?? new List<string>())
            {
                AddPublishTarget(output, seen, SitemapPublishKind, sitemapId);
            }

            return output;
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }

        private class SitemapInfo
        {
            public string sitemapid { get; set; }
            public string sitemapname { get; set; }
            public string sitemapxml { get; set; }
        }

        private class SitemapNodeUpdate
        {
            public Guid sitemapId { get; set; }
            public string compositeId { get; set; }
            public string nodeType { get; set; }
            public string component { get; set; }
            public List<EasyTranslatorLabelChange> labels { get; set; } = new List<EasyTranslatorLabelChange>();
        }
    }
}
