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

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class SiteMap : ICustomAction
    {
        private const int SitemapComponentType = 62;
        private const int PublishedWaitMilliseconds = 10000;
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<LoadingSiteMapInput>(json);
            var solutionId = ParseSolutionId(input.solutionId);
            var sitemaps = solutionId.HasValue
                ? RetrieveSitemapsBySolution(serviceAdmin, solutionId.Value)
                : RetrieveAllSitemaps(serviceAdmin);
            AddEntityLabels(serviceAdmin, sitemaps);

            return new LoadingSiteMapOutput
            {
                sitemaps = sitemaps
            };
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<SavingSiteMapInput>(json);
            var sitemapIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var update in input.sitemapUpdates ?? new List<SiteMapUpdateInput>())
            {
                if (update == null)
                {
                    continue;
                }

                var id = ValidateSitemapId(update.sitemapId);
                var entity = serviceAdmin.Retrieve("sitemap", id, new ColumnSet("sitemapxml"));
                if (entity == null)
                {
                    continue;
                }

                var currentXml = Convert.ToString(entity.GetAttributeValue<string>("sitemapxml") ?? string.Empty);
                var updatedXml = UpdateSitemapXml(currentXml, update);
                if (string.Equals(currentXml, updatedXml, StringComparison.Ordinal))
                {
                    continue;
                }

                entity["sitemapxml"] = updatedXml;
                serviceAdmin.Update(entity);

                var idText = id.ToString("D");
                if (seen.Add(idText))
                {
                    sitemapIds.Add(idText);
                }
            }

            return new SavingSiteMapOutput
            {
                sitemapIds = sitemapIds
            };
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishingSiteMapInput>(json);
            var sitemapIds = GetValidSitemapIds(input.sitemapIds);

            if (sitemapIds.Count > 0)
            {
                serviceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(sitemapIds)
                });
            }

            return new PublishingSiteMapOutput
            {
                sitemapIds = sitemapIds
            };
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishedSiteMapInput>(json);
            Wait(PublishedWaitMilliseconds);
            return new PublishedSiteMapOutput
            {
                sitemapIds = GetValidSitemapIds(input.sitemapIds)
            };
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<OtherSiteMapInput>(json);
            if (string.IsNullOrWhiteSpace(input.sitemapOperation))
            {
                throw new InvalidPluginExecutionException("SiteMap Other operation is required.");
            }

            return new OtherSiteMapOutput
            {
                operation = input.sitemapOperation
            };
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing SiteMap input.");
            }

            return input;
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }

        private static Guid? ParseSolutionId(string solutionId)
        {
            if (string.IsNullOrWhiteSpace(solutionId) || string.Equals(solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionId, out var id))
            {
                throw new InvalidPluginExecutionException("SiteMap solutionId must be a GUID or all.");
            }

            return id;
        }

        private static List<SiteMapMetadataOutput> RetrieveAllSitemaps(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("sitemap")
            {
                ColumnSet = new ColumnSet("sitemapid", "sitemapname", "sitemapxml")
            };

            return ToList(serviceAdmin.RetrieveMultiple(query)).Select(entity => new SiteMapMetadataOutput
            {
                sitemapid = GetEntityGuid(entity, "sitemapid").ToString("D"),
                sitemapname = entity.GetAttributeValue<string>("sitemapname"),
                sitemapxml = entity.GetAttributeValue<string>("sitemapxml")
            }).ToList();
        }

        private static List<SiteMapMetadataOutput> RetrieveSitemapsBySolution(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var ids = GetSolutionSitemapIds(serviceAdmin, solutionId);
            if (ids.Count == 0)
            {
                return new List<SiteMapMetadataOutput>();
            }

            var query = new QueryExpression("sitemap")
            {
                ColumnSet = new ColumnSet("sitemapid", "sitemapname", "sitemapxml")
            };
            query.Criteria.AddCondition("sitemapid", ConditionOperator.In, ids.Cast<object>().ToArray());

            return ToList(serviceAdmin.RetrieveMultiple(query)).Select(entity => new SiteMapMetadataOutput
            {
                sitemapid = GetEntityGuid(entity, "sitemapid").ToString("D"),
                sitemapname = entity.GetAttributeValue<string>("sitemapname"),
                sitemapxml = entity.GetAttributeValue<string>("sitemapxml")
            }).ToList();
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
            foreach (var entity in ToList(serviceAdmin.RetrieveMultiple(query)))
            {
                var id = entity.GetAttributeValue<Guid>("objectid");
                if (id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static void AddEntityLabels(IOrganizationService serviceAdmin, List<SiteMapMetadataOutput> sitemaps)
        {
            var entityNames = GetReferencedEntityNames(sitemaps);
            if (entityNames.Count == 0)
            {
                return;
            }

            foreach (var entityName in entityNames)
            {
                var labels = RetrieveEntityLabels(serviceAdmin, entityName);
                if (labels.Count == 0)
                {
                    continue;
                }

                foreach (var sitemap in sitemaps)
                {
                    if (SitemapReferencesEntity(sitemap, entityName))
                    {
                        sitemap.entityLabels.Add(new SiteMapEntityLabelOutput
                        {
                            entityName = entityName,
                            labels = labels
                        });
                    }
                }
            }
        }

        private static List<string> GetReferencedEntityNames(List<SiteMapMetadataOutput> sitemaps)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sitemap in sitemaps ?? new List<SiteMapMetadataOutput>())
            {
                foreach (var entityName in GetEntityNamesFromXml(sitemap?.sitemapxml))
                {
                    if (seen.Add(entityName))
                    {
                        names.Add(entityName);
                    }
                }
            }

            return names;
        }

        private static List<string> GetEntityNamesFromXml(string sitemapXml)
        {
            var names = new List<string>();
            if (string.IsNullOrWhiteSpace(sitemapXml))
            {
                return names;
            }

            try
            {
                var document = XDocument.Parse(sitemapXml);
                foreach (var subArea in document.Descendants("SubArea"))
                {
                    var entityName = (string)subArea.Attribute("Entity");
                    if (!string.IsNullOrWhiteSpace(entityName))
                    {
                        names.Add(entityName);
                    }
                }
            }
            catch (Exception)
            {
                return new List<string>();
            }

            return names;
        }

        private static List<SiteMapLocalizedLabelOutput> RetrieveEntityLabels(IOrganizationService serviceAdmin, string entityName)
        {
            try
            {
                var response = (RetrieveEntityResponse)serviceAdmin.Execute(new RetrieveEntityRequest
                {
                    LogicalName = entityName,
                    EntityFilters = EntityFilters.Entity,
                    RetrieveAsIfPublished = true
                });

                return BuildLabelOutput(response?.EntityMetadata?.DisplayName);
            }
            catch (Exception)
            {
                return new List<SiteMapLocalizedLabelOutput>();
            }
        }

        private static List<SiteMapLocalizedLabelOutput> BuildLabelOutput(Label label)
        {
            var labels = new List<SiteMapLocalizedLabelOutput>();
            if (label?.LocalizedLabels == null)
            {
                return labels;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel == null)
                {
                    continue;
                }

                labels.Add(new SiteMapLocalizedLabelOutput
                {
                    LanguageCode = localizedLabel.LanguageCode,
                    Label = localizedLabel.Label
                });
            }

            return labels;
        }

        private static bool SitemapReferencesEntity(SiteMapMetadataOutput sitemap, string entityName)
        {
            if (sitemap == null || string.IsNullOrWhiteSpace(entityName))
            {
                return false;
            }

            return GetEntityNamesFromXml(sitemap.sitemapxml).Any(name =>
                string.Equals(name, entityName, StringComparison.OrdinalIgnoreCase));
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

        private static List<Entity> ToList(EntityCollection collection)
        {
            var list = new List<Entity>();
            if (collection == null)
            {
                return list;
            }

            foreach (var entity in collection.Entities)
            {
                list.Add(entity);
            }

            return list;
        }

        private static Guid ValidateSitemapId(string sitemapId)
        {
            if (string.IsNullOrWhiteSpace(sitemapId) || !Guid.TryParse(sitemapId, out var id))
            {
                throw new InvalidPluginExecutionException("SiteMap sitemapId must be a GUID.");
            }

            return id;
        }

        private static string UpdateSitemapXml(string xml, SiteMapUpdateInput update)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }

            if (update?.labels == null || update.labels.Count == 0)
            {
                return xml;
            }

            try
            {
                var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                var node = FindSitemapNode(document.Root, update.compositeId, update.nodeType);
                if (node == null)
                {
                    return xml;
                }

                var isDescription = string.Equals(update.component, "Description", StringComparison.OrdinalIgnoreCase);
                var containerName = isDescription ? "Descriptions" : "Titles";
                var itemName = isDescription ? "Description" : "Title";
                var attributeName = itemName;
                var container = node.Element(containerName);
                var changed = false;

                foreach (var label in update.labels)
                {
                    if (label == null || !int.TryParse(Convert.ToString(label.LanguageCode), out var languageCode))
                    {
                        continue;
                    }

                    if (container == null)
                    {
                        container = new XElement(containerName);
                        node.Add(container);
                        changed = true;
                    }

                    var existing = container.Elements(itemName).FirstOrDefault(element =>
                        string.Equals((string)element.Attribute("LCID"), languageCode.ToString(), StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new XElement(itemName);
                        container.Add(existing);
                        changed = true;
                    }

                    var nextLabel = label.Label ?? string.Empty;
                    if (!string.Equals((string)existing.Attribute("LCID"), languageCode.ToString(), StringComparison.Ordinal) ||
                        !string.Equals((string)existing.Attribute(attributeName), nextLabel, StringComparison.Ordinal))
                    {
                        existing.SetAttributeValue("LCID", languageCode);
                        existing.SetAttributeValue(attributeName, nextLabel);
                        changed = true;
                    }
                }

                return changed ? document.ToString(SaveOptions.DisableFormatting) : xml;
            }
            catch (Exception)
            {
                return xml;
            }
        }

        private static XElement FindSitemapNode(XElement root, string compositeId, string nodeType)
        {
            if (root == null || string.IsNullOrWhiteSpace(compositeId))
            {
                return null;
            }

            var identifiers = compositeId.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
            if (identifiers.Length == 0)
            {
                return null;
            }

            XElement current = null;
            foreach (var area in root.Elements("Area"))
            {
                if (!string.Equals((string)area.Attribute("Id"), identifiers[0], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current = area;
                if (identifiers.Length == 1)
                {
                    return IsNodeType(current, nodeType) ? current : null;
                }

                foreach (var group in area.Elements("Group"))
                {
                    if (!string.Equals((string)group.Attribute("Id"), identifiers[1], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    current = group;
                    if (identifiers.Length == 2)
                    {
                        return IsNodeType(current, nodeType) ? current : null;
                    }

                    foreach (var subArea in group.Elements("SubArea"))
                    {
                        if (string.Equals((string)subArea.Attribute("Id"), identifiers[2], StringComparison.OrdinalIgnoreCase))
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

        private static bool IsNodeType(XElement node, string nodeType)
        {
            return node != null &&
                (string.IsNullOrWhiteSpace(nodeType) || string.Equals(node.Name.LocalName, nodeType, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetValidSitemapIds(List<string> sitemapIds)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (sitemapIds == null)
            {
                return ids;
            }

            foreach (var id in sitemapIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out _))
                {
                    continue;
                }

                if (seen.Add(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static string BuildPublishXml(List<string> sitemapIds)
        {
            var sb = new StringBuilder();
            foreach (var sitemapId in sitemapIds)
            {
                sb.Append("<sitemap>").Append(sitemapId).Append("</sitemap>");
            }

            return string.Concat("<importexportxml><sitemaps>", sb.ToString(), "</sitemaps></importexportxml>");
        }
    }

    public class LoadingSiteMapInput : CustomActionInput
    {
        public string solutionId { get; set; }
    }

    public class SavingSiteMapInput : CustomActionInput
    {
        public List<SiteMapUpdateInput> sitemapUpdates { get; set; } = new List<SiteMapUpdateInput>();
    }

    public class PublishingSiteMapInput : CustomActionInput
    {
        public List<string> sitemapIds { get; set; } = new List<string>();
    }

    public class PublishedSiteMapInput : CustomActionInput
    {
        public List<string> sitemapIds { get; set; } = new List<string>();
    }

    public class OtherSiteMapInput : CustomActionInput
    {
        public string sitemapOperation { get; set; }
    }

    public class LoadingSiteMapOutput
    {
        public List<SiteMapMetadataOutput> sitemaps { get; set; } = new List<SiteMapMetadataOutput>();
    }

    public class SavingSiteMapOutput
    {
        public List<string> sitemapIds { get; set; } = new List<string>();
    }

    public class PublishingSiteMapOutput
    {
        public List<string> sitemapIds { get; set; } = new List<string>();
    }

    public class PublishedSiteMapOutput
    {
        public List<string> sitemapIds { get; set; } = new List<string>();
    }

    public class OtherSiteMapOutput
    {
        public string operation { get; set; }
    }

    public class SiteMapMetadataOutput
    {
        public string sitemapid { get; set; }
        public string sitemapname { get; set; }
        public string sitemapxml { get; set; }
        public List<SiteMapEntityLabelOutput> entityLabels { get; set; } = new List<SiteMapEntityLabelOutput>();
    }

    public class SiteMapEntityLabelOutput
    {
        public string entityName { get; set; }
        public List<SiteMapLocalizedLabelOutput> labels { get; set; } = new List<SiteMapLocalizedLabelOutput>();
    }

    public class SiteMapLocalizedLabelOutput
    {
        public int LanguageCode { get; set; }
        public string Label { get; set; }
    }

    public class SiteMapUpdateInput
    {
        public string sitemapId { get; set; }
        public string compositeId { get; set; }
        public string nodeType { get; set; }
        public string component { get; set; }
        public List<SiteMapLabelInput> labels { get; set; } = new List<SiteMapLabelInput>();
    }

    public class SiteMapLabelInput
    {
        public string LanguageCode { get; set; }
        public string Label { get; set; }
    }
}
