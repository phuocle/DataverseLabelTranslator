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
    public class ViewAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "views";
        private const string PublishKind = "entity";

        private const string AttributeNameName = "name";
        private const string AttributeNameDescription = "description";

        private static readonly Dictionary<int, string> ViewTypeMap = new Dictionary<int, string>
        {
            { 0,       "Public" },
            { 1,       "Advanced Find" },
            { 2,       "Associated" },
            { 4,       "Quick Find" },
            { 16,      "Address Book" },
            { 32,      "Sub Grid" },
            { 64,      "Lookup" },
            { 128,     "Offline Filters" },
            { 256,     "Offline Template" },
            { 1024,    "Saved Filters" },
            { 2048,    "Multi-entity Lookup" },
            { 4096,    "Custom Defined" },
            { 8192,    "Outlook" },
            { 32768,   "Service Appointment Book" },
            { 131072,  "Power BI" },
            { 262144,  "Modern Search" },
            { 1048576, "Copilot" }
        };

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();
            var isNone = string.IsNullOrWhiteSpace(entityName) ||
                         string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase);
            var attributeName = IsDescriptionComponent(input.component) ? AttributeNameDescription : AttributeNameName;

            if (isNone)
            {
                return LoadAll(serviceAdmin, baseLanguage, attributeName, input.solutionId);
            }

            return LoadForEntity(serviceAdmin, baseLanguage, entityName, attributeName);
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                // gridKey = "views|{savedqueryid}|{name|description}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var viewId = ValidateGuid(parts[1], "View savedqueryid");
                var attributeNameFromKey = parts[2];
                var entityNameFromKey = parts[3];

                ValidateAttributeName(attributeNameFromKey);

                var currentLabel = RetrieveLocLabel(context.ServiceAdmin, viewId, attributeNameFromKey);
                var mergedLabels = BuildMergedLabel(currentLabel, changes);

                context.ServiceAdmin.Execute(new SetLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("savedquery", viewId),
                    AttributeName = attributeNameFromKey,
                    Labels = mergedLabels
                });

                output.changedRowCount++;
                var publishEntity = string.IsNullOrWhiteSpace(entityNameFromKey) ? entityName : entityNameFromKey;
                if (!string.IsNullOrWhiteSpace(publishEntity) &&
                    !string.Equals(publishEntity, "none", StringComparison.OrdinalIgnoreCase))
                {
                    AddPublishTarget(output, seenTargets, PublishKind, publishEntity);
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
            string attributeName,
            string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "Views");

            // Retrieve all customizable savedqueries, optionally filtered by solution entity list
            var allViews = RetrieveAllViews(serviceAdmin);
            if (allViews.Count == 0)
            {
                return EmptyOutput(baseLanguage);
            }

            // Optionally filter to entities in solution
            HashSet<string> solutionEntities = null;
            if (solutionGuid.HasValue)
            {
                solutionEntities = GetSolutionEntityLogicalNames(serviceAdmin, solutionGuid.Value, baseLanguage);
            }

            // Get entity display names for parent labels
            var entityDisplayNames = GetEntityDisplayNames(serviceAdmin, baseLanguage);

            // Group views by entity
            var byEntity = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
            foreach (var view in allViews)
            {
                var code = view.GetAttributeValue<string>("returnedtypecode") ?? string.Empty;
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

                list.Add(view);
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
                    Recid = "views:entity:" + entityKey,
                    GridKey = BuildGridKey(TranslatorType, entityKey),
                    SchemaName = parentSchemaName,
                    RowType = "views.entity",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var entityViews = byEntity[entityKey];
                entityViews.Sort((a, b) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        a.GetAttributeValue<string>("name") ?? string.Empty,
                        b.GetAttributeValue<string>("name") ?? string.Empty));

                var children = new List<EasyTranslatorGridRowOutput>();

                foreach (var view in entityViews)
                {
                    var id = view.GetAttributeValue<Guid>("savedqueryid");
                    if (id == Guid.Empty)
                    {
                        continue;
                    }

                    var rawName = view.GetAttributeValue<string>("name") ?? string.Empty;
                    var queryType = view.GetAttributeValue<int>("querytype");
                    var typeName = GetViewTypeName(queryType);
                    var schemaName = string.IsNullOrWhiteSpace(rawName)
                        ? typeName
                        : rawName + " (" + typeName + ")";

                    var childRow = new EasyTranslatorGridRowOutput
                    {
                        Recid = "view:" + id.ToString("D"),
                        GridKey = BuildGridKey(TranslatorType, id.ToString("D"), attributeName, entityKey),
                        SchemaName = schemaName,
                        RowType = "views.row",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = entityKey }
                    };

                    var label = RetrieveLocLabel(serviceAdmin, id, attributeName);
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
                    title = "Views",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorLoadOutput LoadForEntity(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string entityName,
            string attributeName)
        {
            var views = RetrieveEntityViews(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var view in views)
            {
                var id = view.GetAttributeValue<Guid>("savedqueryid");
                if (id == Guid.Empty)
                {
                    continue;
                }

                var rawName = view.GetAttributeValue<string>("name") ?? string.Empty;
                var queryType = view.GetAttributeValue<int>("querytype");
                var typeName = GetViewTypeName(queryType);
                var schemaName = string.IsNullOrWhiteSpace(rawName)
                    ? typeName
                    : rawName + " (" + typeName + ")";

                var row = new EasyTranslatorGridRowOutput
                {
                    Recid = "view:" + id.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, id.ToString("D"), attributeName, entityName),
                    SchemaName = schemaName,
                    RowType = "views.row",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = entityName }
                };

                var label = RetrieveLocLabel(serviceAdmin, id, attributeName);
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
                    title = "Views",
                    rows = rows
                }
            };
        }

        // ─── Data retrieval ────────────────────────────────────────────────────────

        private static List<Entity> RetrieveAllViews(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "querytype", "name", "returnedtypecode")
            };
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);

            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static List<Entity> RetrieveEntityViews(IOrganizationService serviceAdmin, string entityName)
        {
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "querytype", "name")
            };
            query.Criteria.AddCondition("returnedtypecode", ConditionOperator.Equal, entityName);
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);

            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static Label RetrieveLocLabel(IOrganizationService serviceAdmin, Guid viewId, string attributeName)
        {
            var response = (RetrieveLocLabelsResponse)serviceAdmin.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("savedquery", viewId),
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
            foreach (var component in Helper.RetrieveAll(serviceAdmin, componentQuery))
            {
                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (idToName.TryGetValue(objectId, out var logicalName))
                {
                    names.Add(logicalName);
                }
            }

            return names;
        }

        private static string GetBaseLanguageLabel(Microsoft.Xrm.Sdk.Label label, int baseLanguage)
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

        // ─── Utilities ─────────────────────────────────────────────────────────────

        private static string GetViewTypeName(int queryType)
        {
            return ViewTypeMap.TryGetValue(queryType, out var name) ? name : "Type " + queryType;
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private static void ValidateAttributeName(string attributeName)
        {
            if (!string.Equals(attributeName, AttributeNameName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(attributeName, AttributeNameDescription, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(
                    $"View attributeName must be '{AttributeNameName}' or '{AttributeNameDescription}'.");
            }
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
                    title = "Views",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }
    }
}
