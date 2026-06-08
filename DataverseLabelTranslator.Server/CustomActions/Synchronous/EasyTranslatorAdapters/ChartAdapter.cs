using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class ChartAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "charts";
        private const string PublishKind = "entity";
        private const string AttributeNameName = "name";

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();
            var isNone = string.IsNullOrWhiteSpace(entityName) ||
                         string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase);

            if (isNone)
            {
                return LoadAll(serviceAdmin, baseLanguage, input.solutionId);
            }

            return LoadForEntity(serviceAdmin, baseLanguage, entityName);
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

                // gridKey = "charts|{savedqueryvisualizationid}|{name}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var chartId = ValidateGuid(parts[1], "Chart savedqueryvisualizationid");
                var attributeName = parts[2];
                var entityName = parts[3];

                if (!string.Equals(attributeName, AttributeNameName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException($"Chart attributeName must be '{AttributeNameName}'.");
                }

                var currentLabel = RetrieveLocLabel(context.ServiceAdmin, chartId, attributeName);
                var mergedLabels = BuildMergedLabel(currentLabel, changes);

                context.ServiceAdmin.Execute(new SetLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("savedqueryvisualization", chartId),
                    AttributeName = attributeName,
                    Labels = mergedLabels
                });

                output.changedRowCount++;
                if (!string.IsNullOrWhiteSpace(entityName) && !string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase))
                {
                    AddPublishTarget(output, seenTargets, PublishKind, entityName);
                }
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var entityNames = GetPublishTargetIds(input, PublishKind, false);

            if (entityNames.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildEntityPublishXml(entityNames)
                });
            }

            return ToPublishOutput(PublishKind, entityNames);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            WaitAction(PublishedWaitMilliseconds);
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, false));
        }

        // ─── Load helpers ──────────────────────────────────────────────────────────

        private static EasyTranslatorLoadOutput LoadAll(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "Charts");

            var allCharts = RetrieveAllCharts(serviceAdmin);
            if (allCharts.Count == 0)
            {
                return EmptyOutput(baseLanguage);
            }

            HashSet<string> solutionEntities = null;
            if (solutionGuid.HasValue)
            {
                solutionEntities = GetSolutionEntityLogicalNames(serviceAdmin, solutionGuid.Value, baseLanguage);
            }

            var entityDisplayNames = GetEntityDisplayNames(serviceAdmin, baseLanguage);

            // Group charts by primaryentitytypecode
            var byEntity = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
            foreach (var chart in allCharts)
            {
                var code = chart.GetAttributeValue<string>("primaryentitytypecode") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (solutionEntities != null && !solutionEntities.Contains(code))
                {
                    continue;
                }

                if (!byEntity.TryGetValue(code, out var list))
                {
                    list = new List<Entity>();
                    byEntity[code] = list;
                }

                list.Add(chart);
            }

            var entityKeys = new List<string>(byEntity.Keys);
            entityKeys.Sort(StringComparer.OrdinalIgnoreCase);

            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var entityKey in entityKeys)
            {
                entityDisplayNames.TryGetValue(entityKey, out var displayName);
                var parentSchemaName = string.IsNullOrWhiteSpace(displayName)
                    ? entityKey
                    : displayName + " (" + entityKey + ")";

                var parentRow = new EasyTranslatorGridRowOutput
                {
                    Recid = "charts:entity:" + entityKey,
                    GridKey = BuildGridKey(TranslatorType, entityKey),
                    SchemaName = parentSchemaName,
                    RowType = "charts.entity",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var entityCharts = byEntity[entityKey];
                entityCharts.Sort((a, b) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        a.GetAttributeValue<string>("name") ?? string.Empty,
                        b.GetAttributeValue<string>("name") ?? string.Empty));

                var children = new List<EasyTranslatorGridRowOutput>();

                foreach (var chart in entityCharts)
                {
                    var id = chart.GetAttributeValue<Guid>("savedqueryvisualizationid");
                    if (id == Guid.Empty)
                    {
                        continue;
                    }

                    var rawName = chart.GetAttributeValue<string>("name") ?? string.Empty;
                    var schemaName = string.IsNullOrWhiteSpace(rawName) ? "Chart" : rawName;

                    var childRow = new EasyTranslatorGridRowOutput
                    {
                        Recid = "chart:" + id.ToString("D"),
                        GridKey = BuildGridKey(TranslatorType, id.ToString("D"), AttributeNameName, entityKey),
                        SchemaName = schemaName,
                        RowType = "charts.row",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = entityKey }
                    };

                    var label = RetrieveLocLabel(serviceAdmin, id, AttributeNameName);
                    AddLabelValues(childRow, label);
                    children.Add(childRow);
                }

                if (children.Count > 0)
                {
                    parentRow.Children = children;
                    rows.Add(parentRow);
                }
            }

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Charts",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorLoadOutput LoadForEntity(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string entityName)
        {
            var charts = RetrieveEntityCharts(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var chart in charts)
            {
                var id = chart.GetAttributeValue<Guid>("savedqueryvisualizationid");
                if (id == Guid.Empty)
                {
                    continue;
                }

                var rawName = chart.GetAttributeValue<string>("name") ?? string.Empty;
                var schemaName = string.IsNullOrWhiteSpace(rawName) ? "Chart" : rawName;

                var row = new EasyTranslatorGridRowOutput
                {
                    Recid = "chart:" + id.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, id.ToString("D"), AttributeNameName, entityName),
                    SchemaName = schemaName,
                    RowType = "charts.row",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = entityName }
                };

                var label = RetrieveLocLabel(serviceAdmin, id, AttributeNameName);
                AddLabelValues(row, label);
                rows.Add(row);
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Charts",
                    rows = rows
                }
            };
        }

        // ─── Data retrieval ────────────────────────────────────────────────────────

        private static List<Entity> RetrieveAllCharts(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("savedqueryvisualization")
            {
                ColumnSet = new ColumnSet("savedqueryvisualizationid", "name", "primaryentitytypecode")
            };
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);

            return ToList(serviceAdmin.RetrieveMultiple(query));
        }

        private static List<Entity> RetrieveEntityCharts(IOrganizationService serviceAdmin, string entityName)
        {
            var query = new QueryExpression("savedqueryvisualization")
            {
                ColumnSet = new ColumnSet("savedqueryvisualizationid", "name")
            };
            query.Criteria.AddCondition("primaryentitytypecode", ConditionOperator.Equal, entityName);
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);

            return ToList(serviceAdmin.RetrieveMultiple(query));
        }

        private static Label RetrieveLocLabel(IOrganizationService serviceAdmin, Guid chartId, string attributeName)
        {
            var response = (RetrieveLocLabelsResponse)serviceAdmin.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("savedqueryvisualization", chartId),
                AttributeName = attributeName,
                IncludeUnpublished = true
            });

            return response?.Label ?? new Label();
        }

        private static Dictionary<string, string> GetEntityDisplayNames(IOrganizationService serviceAdmin, int baseLanguage)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entity in response.EntityMetadata)
            {
                if (string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    continue;
                }

                var displayName = GetBaseLanguageLabel(entity.DisplayName, baseLanguage);
                map[entity.LogicalName] = displayName;
            }

            return map;
        }

        private static HashSet<string> GetSolutionEntityLogicalNames(
            IOrganizationService serviceAdmin,
            Guid solutionId,
            int baseLanguage)
        {
            var allEntities = GetEntityDisplayNames(serviceAdmin, baseLanguage);
            var metadataByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);

            foreach (var entity in response.EntityMetadata)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName) && entity.MetadataId.HasValue)
                {
                    metadataByName[entity.LogicalName] = entity.MetadataId.Value;
                }
            }

            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1); // Entity

            var idToName = new Dictionary<Guid, string>();
            foreach (var kv in metadataByName)
            {
                idToName[kv.Value] = kv.Key;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in ToList(serviceAdmin.RetrieveMultiple(componentQuery)))
            {
                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (idToName.TryGetValue(objectId, out var logicalName))
                {
                    names.Add(logicalName);
                }
            }

            return names;
        }

        private static string GetBaseLanguageLabel(Label label, int baseLanguage)
        {
            if (label?.LocalizedLabels == null)
            {
                return string.Empty;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel != null &&
                    localizedLabel.LanguageCode == baseLanguage &&
                    !string.IsNullOrWhiteSpace(localizedLabel.Label))
                {
                    return localizedLabel.Label;
                }
            }

            return string.Empty;
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private static LocalizedLabel[] BuildMergedLabel(Label currentLabel, List<EasyTranslatorLabelChange> changes)
        {
            var values = new Dictionary<int, string>();
            var languageOrder = new List<int>();

            if (currentLabel?.LocalizedLabels != null)
            {
                foreach (var existing in currentLabel.LocalizedLabels)
                {
                    if (existing == null || values.ContainsKey(existing.LanguageCode))
                    {
                        continue;
                    }

                    values[existing.LanguageCode] = existing.Label ?? string.Empty;
                    languageOrder.Add(existing.LanguageCode);
                }
            }

            foreach (var change in changes)
            {
                if (!values.ContainsKey(change.LanguageCode))
                {
                    languageOrder.Add(change.LanguageCode);
                }

                values[change.LanguageCode] = change.Label ?? string.Empty;
            }

            var labels = new List<LocalizedLabel>();
            foreach (var languageCode in languageOrder)
            {
                labels.Add(new LocalizedLabel(values[languageCode], languageCode));
            }

            return labels.ToArray();
        }

        private static string BuildEntityPublishXml(List<string> entityNames)
        {
            var sb = new StringBuilder();
            foreach (var name in entityNames)
            {
                sb.Append("<entity>").Append(name).Append("</entity>");
            }

            return string.Concat("<importexportxml><entities>", sb.ToString(), "</entities></importexportxml>");
        }

        private static EasyTranslatorLoadOutput EmptyOutput(int baseLanguage)
        {
            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Charts",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }
    }
}
