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
    public class RelationshipAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "relationships";
        private const string PublishKind = "entity";

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

                // gridKey = "relationships|{MetadataId}|{relType}|{SchemaName}|{Entity1AssociatedMenuConfiguration|Entity2AssociatedMenuConfiguration|AssociatedMenuConfiguration}|{navEntity}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 6);
                var metadataId = ValidateGuid(parts[1], "Relationship MetadataId");
                var relType = parts[2];
                var schemaName = parts[3];
                var menuConfigProperty = parts[4];
                var navEntity = parts[5];

                // Retrieve relationship metadata to get current AssociatedMenuConfiguration
                var rel = RetrieveRelationshipMetadata(context.ServiceAdmin, metadataId, relType);
                if (rel == null)
                {
                    continue;
                }

                var menuConfig = GetAssociatedMenuConfiguration(rel, menuConfigProperty);
                if (menuConfig == null || menuConfig.IsCustomizable == false)
                {
                    continue;
                }

                // If switching from UseCollectionName to UseLabel, pre-populate with plural names
                if ((!menuConfig.Behavior.HasValue || menuConfig.Behavior.Value == AssociatedMenuBehavior.UseCollectionName) && !string.IsNullOrWhiteSpace(navEntity))
                {
                    var plurals = GetEntityPluralNames(context.ServiceAdmin, navEntity);
                    menuConfig.Label = new Label();
                    foreach (var plural in plurals)
                    {
                        menuConfig.Label.LocalizedLabels.Add(new LocalizedLabel(plural.Value, plural.Key));
                    }
                }

                menuConfig.Behavior = AssociatedMenuBehavior.UseLabel;
                var mergedLabels = BuildMergedLabel(menuConfig.Label, changes);
                menuConfig.Label = new Label();
                menuConfig.Label.LocalizedLabels.AddRange(mergedLabels);

                UpdateAssociatedMenuConfiguration(context.ServiceAdmin, metadataId, relType, schemaName, menuConfigProperty, menuConfig);

                output.changedRowCount++;
                
                // Add publish target
                var publishEntity = string.IsNullOrWhiteSpace(navEntity) ? string.Empty : navEntity;
                if (!string.IsNullOrWhiteSpace(publishEntity))
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
            string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "Relationships");

            // Retrieve all entities to fetch relationships
            var entities = solutionGuid.HasValue
                ? RetrieveEntitiesBySolution(serviceAdmin, solutionGuid.Value)
                : RetrieveAllEntities(serviceAdmin);

            var entityDisplayNames = GetEntityDisplayNames(serviceAdmin, baseLanguage);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var entity in entities)
            {
                if (string.IsNullOrWhiteSpace(entity.LogicalName) || entity.IsCustomizable?.Value == false)
                {
                    continue;
                }

                var entityKey = entity.LogicalName;
                var relationships = RetrieveEntityRelationships(serviceAdmin, entityKey);
                if (relationships.Count == 0)
                {
                    continue;
                }

                entityDisplayNames.TryGetValue(entityKey, out var displayName);
                var parentSchemaName = string.IsNullOrWhiteSpace(displayName)
                    ? entityKey
                    : displayName + " (" + entityKey + ")";

                var parentRow = new EasyTranslatorGridRowOutput
                {
                    Recid = "relationships:entity:" + entityKey,
                    GridKey = BuildGridKey(TranslatorType, entityKey),
                    SchemaName = parentSchemaName,
                    RowType = "relationships.entity",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var children = new List<EasyTranslatorGridRowOutput>();
                var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var rel in relationships)
                {
                    var childRow = BuildRelationshipRow(serviceAdmin, rel, entityKey, pluralCache);
                    if (childRow != null)
                    {
                        children.Add(childRow);
                    }
                }

                if (children.Count > 0)
                {
                    children.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));
                    parentRow.Children = children;
                    rows.Add(parentRow);
                }
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Relationships",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorLoadOutput LoadForEntity(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string entityName)
        {
            var relationships = RetrieveEntityRelationships(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var rel in relationships)
            {
                var row = BuildRelationshipRow(serviceAdmin, rel, entityName, pluralCache);
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Relationships",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorGridRowOutput BuildRelationshipRow(
            IOrganizationService serviceAdmin,
            RelationshipMetadataBase rel,
            string entityName,
            Dictionary<string, Dictionary<int, string>> pluralCache)
        {
            if (rel.IsCustomizable?.Value == false)
            {
                return null;
            }

            string relTypeLabel = "";
            string menuConfigProperty = "AssociatedMenuConfiguration";
            string navEntity = "";
            AssociatedMenuConfiguration menuConfig = null;

            if (rel is OneToManyRelationshipMetadata oneToMany)
            {
                if (oneToMany.AssociatedMenuConfiguration == null || oneToMany.AssociatedMenuConfiguration.IsCustomizable == false)
                {
                    return null;
                }
                
                var isOneToMany = string.Equals(oneToMany.ReferencedEntity, entityName, StringComparison.OrdinalIgnoreCase);
                if (isOneToMany)
                {
                    relTypeLabel = "1:N \u2192 " + oneToMany.ReferencingEntity;
                    navEntity = oneToMany.ReferencingEntity;
                }
                else
                {
                    relTypeLabel = "N:1 \u2190 " + oneToMany.ReferencedEntity;
                    navEntity = entityName;
                }
                menuConfig = oneToMany.AssociatedMenuConfiguration;
            }
            else if (rel is ManyToManyRelationshipMetadata manyToMany)
            {
                var isEntity1Side = string.Equals(manyToMany.Entity1LogicalName, entityName, StringComparison.OrdinalIgnoreCase);
                menuConfigProperty = isEntity1Side ? "Entity1AssociatedMenuConfiguration" : "Entity2AssociatedMenuConfiguration";
                menuConfig = isEntity1Side ? manyToMany.Entity1AssociatedMenuConfiguration : manyToMany.Entity2AssociatedMenuConfiguration;

                if (menuConfig == null || menuConfig.IsCustomizable == false)
                {
                    return null;
                }

                var otherEntity = isEntity1Side ? manyToMany.Entity2LogicalName : manyToMany.Entity1LogicalName;
                relTypeLabel = "N:N \u2194 " + otherEntity;
                navEntity = otherEntity;
            }

            if (menuConfig == null)
            {
                return null;
            }

            var schemaName = rel.SchemaName + " (" + relTypeLabel + ")";
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "relationship:" + rel.MetadataId.GetValueOrDefault().ToString("D"),
                GridKey = BuildGridKey(TranslatorType, rel.MetadataId.GetValueOrDefault().ToString("D"), relTypeLabel, rel.SchemaName, menuConfigProperty, navEntity),
                SchemaName = schemaName,
                RowType = "relationships.row",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = navEntity }
            };

            if (menuConfig.Behavior.GetValueOrDefault() == AssociatedMenuBehavior.UseCollectionName && !string.IsNullOrWhiteSpace(navEntity))
            {
                if (!pluralCache.TryGetValue(navEntity, out var plurals))
                {
                    plurals = GetEntityPluralNames(serviceAdmin, navEntity);
                    pluralCache[navEntity] = plurals;
                }

                foreach (var plural in plurals)
                {
                    row.SetLanguageValue(plural.Key.ToString(), plural.Value);
                }
            }
            else if (menuConfig.Label != null)
            {
                AddLabelValues(row, menuConfig.Label);
            }

            return row;
        }

        // ─── Data retrieval ────────────────────────────────────────────────────────

        private static List<RelationshipMetadataBase> RetrieveEntityRelationships(IOrganizationService serviceAdmin, string entityName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Relationships,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveEntityResponse)serviceAdmin.Execute(request);
            var list = new List<RelationshipMetadataBase>();
            if (response?.EntityMetadata == null)
            {
                return list;
            }

            if (response.EntityMetadata.OneToManyRelationships != null)
            {
                list.AddRange(response.EntityMetadata.OneToManyRelationships);
            }
            if (response.EntityMetadata.ManyToOneRelationships != null)
            {
                list.AddRange(response.EntityMetadata.ManyToOneRelationships);
            }
            if (response.EntityMetadata.ManyToManyRelationships != null)
            {
                list.AddRange(response.EntityMetadata.ManyToManyRelationships);
            }

            return list;
        }

        private static RelationshipMetadataBase RetrieveRelationshipMetadata(IOrganizationService serviceAdmin, Guid metadataId, string relType)
        {
            var isManyToMany = relType.Contains("N:N");
            var request = new RetrieveRelationshipRequest
            {
                MetadataId = metadataId,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveRelationshipResponse)serviceAdmin.Execute(request);
            return response?.RelationshipMetadata;
        }

        private static void UpdateAssociatedMenuConfiguration(
            IOrganizationService serviceAdmin,
            Guid metadataId,
            string relType,
            string schemaName,
            string menuConfigProperty,
            AssociatedMenuConfiguration menuConfig)
        {
            var isManyToMany = relType.Contains("N:N");
            RelationshipMetadataBase relUpdate;

            if (isManyToMany)
            {
                var manyToMany = new ManyToManyRelationshipMetadata
                {
                    MetadataId = metadataId,
                    SchemaName = schemaName
                };

                if (string.Equals(menuConfigProperty, "Entity1AssociatedMenuConfiguration", StringComparison.OrdinalIgnoreCase))
                {
                    manyToMany.Entity1AssociatedMenuConfiguration = menuConfig;
                }
                else
                {
                    manyToMany.Entity2AssociatedMenuConfiguration = menuConfig;
                }
                relUpdate = manyToMany;
            }
            else
            {
                relUpdate = new OneToManyRelationshipMetadata
                {
                    MetadataId = metadataId,
                    SchemaName = schemaName,
                    AssociatedMenuConfiguration = menuConfig
                };
            }

            var request = new UpdateRelationshipRequest
            {
                Relationship = relUpdate,
                MergeLabels = true
            };

            serviceAdmin.Execute(request);
        }

        private static AssociatedMenuConfiguration GetAssociatedMenuConfiguration(RelationshipMetadataBase rel, string menuConfigProperty)
        {
            if (rel is OneToManyRelationshipMetadata oneToMany)
            {
                return oneToMany.AssociatedMenuConfiguration;
            }
            if (rel is ManyToManyRelationshipMetadata manyToMany)
            {
                return string.Equals(menuConfigProperty, "Entity1AssociatedMenuConfiguration", StringComparison.OrdinalIgnoreCase)
                    ? manyToMany.Entity1AssociatedMenuConfiguration
                    : manyToMany.Entity2AssociatedMenuConfiguration;
            }
            return null;
        }

        private static Dictionary<int, string> GetEntityPluralNames(IOrganizationService serviceAdmin, string logicalName)
        {
            var request = new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveEntityResponse)serviceAdmin.Execute(request);
            var map = new Dictionary<int, string>();
            if (response?.EntityMetadata?.DisplayCollectionName?.LocalizedLabels == null)
            {
                return map;
            }

            foreach (var label in response.EntityMetadata.DisplayCollectionName.LocalizedLabels)
            {
                if (label != null)
                {
                    map[label.LanguageCode] = label.Label ?? string.Empty;
                }
            }

            return map;
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
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1); // Entity

            var all = RetrieveAllEntities(serviceAdmin);
            var metadataById = new Dictionary<Guid, EntityMetadata>();
            foreach (var entity in all)
            {
                if (entity.MetadataId.HasValue)
                {
                    metadataById[entity.MetadataId.Value] = entity;
                }
            }

            var filtered = new List<EntityMetadata>();
            foreach (var component in Helper.RetrieveAll(serviceAdmin, query))
            {
                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (objectId != Guid.Empty && metadataById.TryGetValue(objectId, out var entity))
                {
                    filtered.Add(entity);
                }
            }

            return filtered;
        }

        private static Dictionary<string, string> GetEntityDisplayNames(IOrganizationService serviceAdmin, int baseLanguage)
        {
            var all = RetrieveAllEntities(serviceAdmin);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in all)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    map[entity.LogicalName] = GetBaseLanguageLabel(entity.DisplayName, baseLanguage);
                }
            }

            return map;
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
    }
}
