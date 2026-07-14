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
    public class EntityMetadataAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int EntityComponentType = 1;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "entityMeta";
        private const string PublishKind = "entity";

        // Stable child keys used in gridKey – server-side identity, not display text
        private const string ChildKeyDisplayName = "DisplayName";
        private const string ChildKeyCollectionName = "CollectionName";
        private const string ChildKeyDescription = "Description";

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var solutionId = ParseSolutionId(input.solutionId, "EntityMetadata");
            var isDescription = IsDescriptionComponent(input.component);

            var entities = solutionId.HasValue
                ? RetrieveEntitiesBySolution(serviceAdmin, solutionId.Value)
                : RetrieveAllEntities(serviceAdmin);

            // Sort by logical name before building rows
            entities.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a?.LogicalName, b?.LogicalName));

            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var entity in entities)
            {
                if (!IsEntityCandidate(entity))
                {
                    continue;
                }

                var parentRow = BuildEntityParentRow(entity, baseLanguage);
                parentRow.Children = BuildEntityChildRows(entity, isDescription);
                rows.Add(parentRow);
            }

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Entity Metadata",
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

                // gridKey = "entityMeta|{logicalName}|{DisplayName|CollectionName|Description}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 3);
                var logicalName = parts[1];
                var childKey = parts[2];

                if (!string.Equals(childKey, ChildKeyDisplayName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(childKey, ChildKeyCollectionName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(childKey, ChildKeyDescription, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException(
                        $"EntityMetadata gridKey child key must be {ChildKeyDisplayName}, {ChildKeyCollectionName}, or {ChildKeyDescription}.");
                }

                var entityMetadata = RetrieveEntityMetadata(context.ServiceAdmin, logicalName);
                Label currentLabel;

                if (string.Equals(childKey, ChildKeyDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    currentLabel = entityMetadata.DisplayName;
                }
                else if (string.Equals(childKey, ChildKeyCollectionName, StringComparison.OrdinalIgnoreCase))
                {
                    currentLabel = entityMetadata.DisplayCollectionName;
                }
                else
                {
                    currentLabel = entityMetadata.Description;
                }

                var mergedLabels = BuildMergedLabel(currentLabel, changes);
                UpdateEntityLabel(context.ServiceAdmin, entityMetadata.MetadataId.GetValueOrDefault(), logicalName, childKey, mergedLabels);

                output.changedRowCount++;
                AddPublishTarget(output, seenTargets, PublishKind, logicalName);
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
                    ParameterXml = BuildPublishXml(entityNames)
                });
            }

            return ToPublishOutput(PublishKind, entityNames);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            WaitAction(PublishedWaitMilliseconds);
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, false));
        }

        private static List<EntityMetadata> RetrieveAllEntities(IOrganizationService serviceAdmin)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);
            return new List<EntityMetadata>(response.EntityMetadata);
        }

        private static List<EntityMetadata> RetrieveEntitiesBySolution(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var logicalNames = GetSolutionEntityLogicalNames(serviceAdmin, solutionId);
            if (logicalNames.Count == 0)
            {
                return new List<EntityMetadata>();
            }

            var all = RetrieveAllEntities(serviceAdmin);
            var filtered = new List<EntityMetadata>();

            foreach (var entity in all)
            {
                if (entity != null &&
                    !string.IsNullOrWhiteSpace(entity.LogicalName) &&
                    logicalNames.Contains(entity.LogicalName))
                {
                    filtered.Add(entity);
                }
            }

            return filtered;
        }

        private static HashSet<string> GetSolutionEntityLogicalNames(IOrganizationService serviceAdmin, Guid solutionId)
        {
            // Retrieve solutioncomponent objectids for entity component type
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, EntityComponentType);

            // Build metadataId -> logicalName map from all entities
            var allEntities = RetrieveAllEntities(serviceAdmin);
            var metadataById = new Dictionary<Guid, string>();
            foreach (var entity in allEntities)
            {
                if (entity != null &&
                    entity.MetadataId.HasValue &&
                    !string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    metadataById[entity.MetadataId.Value] = entity.LogicalName;
                }
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in Helper.RetrieveAll(serviceAdmin, query))
            {
                if (!component.Contains("objectid"))
                {
                    continue;
                }

                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (objectId != Guid.Empty && metadataById.TryGetValue(objectId, out var logicalName))
                {
                    names.Add(logicalName);
                }
            }

            return names;
        }

        private static bool IsEntityCandidate(EntityMetadata entity)
        {
            if (entity == null || string.IsNullOrWhiteSpace(entity.LogicalName))
            {
                return false;
            }

            var customizable = entity.IsCustomizable;
            if (customizable != null && !customizable.Value)
            {
                return false;
            }

            return true;
        }

        private static EasyTranslatorGridRowOutput BuildEntityParentRow(EntityMetadata entity, int baseLanguage)
        {
            var displayName = GetBaseLanguageLabel(entity.DisplayName, baseLanguage);
            var schemaName = string.IsNullOrWhiteSpace(displayName)
                ? entity.LogicalName
                : displayName + " (" + entity.LogicalName + ")";

            return new EasyTranslatorGridRowOutput
            {
                Recid = "entityMetadata:" + entity.LogicalName,
                GridKey = BuildGridKey(TranslatorType, entity.LogicalName),
                SchemaName = schemaName,
                RowType = "entityMetadata.entity",
                IsEditable = false,
                IsTranslatable = false,
                Ai = new EasyTranslatorAiOutput { include = false }
            };
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

        private static List<EasyTranslatorGridRowOutput> BuildEntityChildRows(EntityMetadata entity, bool isDescription)
        {
            var children = new List<EasyTranslatorGridRowOutput>();

            if (isDescription)
            {
                // Description component: Description label + CollectionName label
                var descRow = new EasyTranslatorGridRowOutput
                {
                    Recid = "entityMetadata:" + entity.LogicalName + ":description",
                    GridKey = BuildGridKey(TranslatorType, entity.LogicalName, ChildKeyDescription),
                    SchemaName = "Description",
                    RowType = "entityMetadata.label",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = entity.LogicalName }
                };
                AddLabelValues(descRow, entity.Description);
                children.Add(descRow);
            }
            else
            {
                // DisplayText component: DisplayName label
                var displayTextRow = new EasyTranslatorGridRowOutput
                {
                    Recid = "entityMetadata:" + entity.LogicalName + ":displayText",
                    GridKey = BuildGridKey(TranslatorType, entity.LogicalName, ChildKeyDisplayName),
                    SchemaName = "Display Text",
                    RowType = "entityMetadata.label",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = entity.LogicalName }
                };
                AddLabelValues(displayTextRow, entity.DisplayName);
                children.Add(displayTextRow);
            }

            // Collection Name always shown for both components
            var collectionNameRow = new EasyTranslatorGridRowOutput
            {
                Recid = "entityMetadata:" + entity.LogicalName + ":collectionName",
                GridKey = BuildGridKey(TranslatorType, entity.LogicalName, ChildKeyCollectionName),
                SchemaName = "Collection Name",
                RowType = "entityMetadata.label",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = entity.LogicalName }
            };
            AddLabelValues(collectionNameRow, entity.DisplayCollectionName);
            children.Add(collectionNameRow);

            return children;
        }

        private static EntityMetadata RetrieveEntityMetadata(IOrganizationService serviceAdmin, string logicalName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveEntityResponse)serviceAdmin.Execute(request);
            if (response?.EntityMetadata == null)
            {
                throw new InvalidPluginExecutionException($"EntityMetadata not found for entity: {logicalName}");
            }

            return response.EntityMetadata;
        }

        private static void UpdateEntityLabel(
            IOrganizationService serviceAdmin,
            Guid metadataId,
            string logicalName,
            string childKey,
            LocalizedLabel[] mergedLabels)
        {
            var entityToUpdate = new EntityMetadata
            {
                MetadataId = metadataId,
                LogicalName = logicalName
            };

            var label = new Label();
            label.LocalizedLabels.AddRange(mergedLabels);

            if (string.Equals(childKey, ChildKeyDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                entityToUpdate.DisplayName = label;
            }
            else if (string.Equals(childKey, ChildKeyCollectionName, StringComparison.OrdinalIgnoreCase))
            {
                entityToUpdate.DisplayCollectionName = label;
            }
            else
            {
                entityToUpdate.Description = label;
            }

            var updateRequest = new UpdateEntityRequest
            {
                Entity = entityToUpdate,
                MergeLabels = true
            };

            serviceAdmin.Execute(updateRequest);
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

        private static string BuildPublishXml(List<string> entityNames)
        {
            var sb = new StringBuilder();
            foreach (var name in entityNames)
            {
                sb.Append("<entity>").Append(name).Append("</entity>");
            }

            return string.Concat("<importexportxml><entities>", sb.ToString(), "</entities></importexportxml>");
        }
    }
}
