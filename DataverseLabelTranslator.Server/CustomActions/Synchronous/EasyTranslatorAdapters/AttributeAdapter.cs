using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class AttributeAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "attributes";
        private const string PublishKind = "entity";
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var entityName = RequireEntityName(input.entityName);
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var entity = RetrieveEntityMetadata(context.ServiceAdmin, entityName);
            var excludedSchemaNames = BuildExcludedSchemaNames(entity.Attributes);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var attribute in entity.Attributes ?? new AttributeMetadata[0])
            {
                if (!IsAttributeCandidate(attribute, excludedSchemaNames, input.component))
                {
                    continue;
                }

                rows.Add(BuildAttributeRow(entityName, attribute, input.component));
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Attributes",
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

                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var entityName = RequireEntityName(parts[1]);
                var attributeName = RequireAttributeName(parts[2]);
                var labelKey = parts[3];

                if (!IsDisplayNameKey(labelKey) && !IsDescriptionKey(labelKey))
                {
                    throw new InvalidPluginExecutionException("Attribute gridKey label key must be DisplayName or Description.");
                }

                SaveAttributeLabel(context.ServiceAdmin, entityName, attributeName, labelKey, changes);
                output.changedRowCount++;
                AddPublishTarget(output, seenTargets, PublishKind, entityName);
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

        private static EntityMetadata RetrieveEntityMetadata(IOrganizationService serviceAdmin, string entityName)
        {
            var response = (RetrieveEntityResponse)serviceAdmin.Execute(new RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = true
            });

            if (response?.EntityMetadata == null)
            {
                throw new InvalidPluginExecutionException("Attribute entity metadata was not found.");
            }

            return response.EntityMetadata;
        }

        private static AttributeMetadata RetrieveAttributeMetadata(IOrganizationService serviceAdmin, string entityName, string attributeName)
        {
            var response = (RetrieveAttributeResponse)serviceAdmin.Execute(new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = true
            });

            if (response?.AttributeMetadata == null)
            {
                throw new InvalidPluginExecutionException("Attribute metadata was not found.");
            }

            return response.AttributeMetadata;
        }

        private static HashSet<string> BuildExcludedSchemaNames(AttributeMetadata[] attributes)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attribute in attributes ?? new AttributeMetadata[0])
            {
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.SchemaName))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(attribute.AttributeOf))
                {
                    excluded.Add(attribute.SchemaName);
                    continue;
                }

                if (HasFormulaDefinition(attribute))
                {
                    if (attribute.AttributeType == AttributeTypeCode.Money)
                    {
                        excluded.Add(attribute.SchemaName + "_Base");
                    }

                    excluded.Add(attribute.SchemaName + "_Date");
                    excluded.Add(attribute.SchemaName + "_State");
                    continue;
                }

                if (attribute.IsRenameable != null && !attribute.IsRenameable.Value)
                {
                    excluded.Add(attribute.SchemaName);
                    continue;
                }

                if (attribute.AttributeType == AttributeTypeCode.BigInt)
                {
                    excluded.Add(attribute.SchemaName);
                }
            }

            return excluded;
        }

        private static bool IsAttributeCandidate(AttributeMetadata attribute, HashSet<string> excludedSchemaNames, string component)
        {
            if (attribute == null ||
                !attribute.MetadataId.HasValue ||
                string.IsNullOrWhiteSpace(attribute.LogicalName) ||
                string.IsNullOrWhiteSpace(attribute.SchemaName))
            {
                return false;
            }

            if (attribute.IsCustomizable != null && !attribute.IsCustomizable.Value)
            {
                return false;
            }

            if (excludedSchemaNames != null && excludedSchemaNames.Contains(attribute.SchemaName))
            {
                return false;
            }

            var label = GetLabel(attribute, component);
            return label?.LocalizedLabels != null && label.LocalizedLabels.Count > 0;
        }

        private static bool HasFormulaDefinition(AttributeMetadata attribute)
        {
            if (attribute == null)
            {
                return false;
            }

            var formulaProperty = attribute.GetType().GetProperty("FormulaDefinition");
            var formula = formulaProperty?.GetValue(attribute, null) as string;
            if (!string.IsNullOrWhiteSpace(formula))
            {
                return true;
            }

            var sourceTypeProperty = attribute.GetType().GetProperty("SourceType");
            var sourceTypeValue = sourceTypeProperty?.GetValue(attribute, null);
            if (sourceTypeValue == null)
            {
                return false;
            }

            if (sourceTypeValue is int)
            {
                return (int)sourceTypeValue != 0;
            }

            if (sourceTypeValue is int?)
            {
                var value = (int?)sourceTypeValue;
                return value.HasValue && value.Value != 0;
            }

            return int.TryParse(Convert.ToString(sourceTypeValue), out var parsed) && parsed != 0;
        }

        private static EasyTranslatorGridRowOutput BuildAttributeRow(string entityName, AttributeMetadata attribute, string component)
        {
            var labelKey = GetLabelKey(component);
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "attributes:" + entityName + ":" + attribute.LogicalName,
                GridKey = BuildGridKey(TranslatorType, entityName, attribute.LogicalName, labelKey),
                SchemaName = attribute.SchemaName,
                RowType = "attributes.attribute",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = entityName }
            };

            AddLabelValues(row, GetLabel(attribute, component));
            return row;
        }

        private static void SaveAttributeLabel(
            IOrganizationService serviceAdmin,
            string entityName,
            string attributeName,
            string labelKey,
            List<EasyTranslatorLabelChange> changes)
        {
            var attribute = RetrieveAttributeMetadata(serviceAdmin, entityName, attributeName);
            var label = BuildLabel(changes);

            if (IsDisplayNameKey(labelKey))
            {
                attribute.DisplayName = label;
            }
            else
            {
                attribute.Description = label;
            }

            serviceAdmin.Execute(new UpdateAttributeRequest
            {
                EntityName = entityName,
                Attribute = attribute,
                MergeLabels = true
            });
        }

        private static Label GetLabel(AttributeMetadata attribute, string component)
        {
            return IsDescriptionComponent(component) ? attribute.Description : attribute.DisplayName;
        }

        private static string GetLabelKey(string component)
        {
            return IsDescriptionComponent(component) ? "Description" : "DisplayName";
        }

        private static bool IsDisplayNameKey(string value)
        {
            return string.Equals(value, "DisplayName", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDescriptionKey(string value)
        {
            return string.Equals(value, "Description", StringComparison.OrdinalIgnoreCase);
        }

        private static Label BuildLabel(List<EasyTranslatorLabelChange> labels)
        {
            var label = new Label();
            foreach (var item in labels ?? new List<EasyTranslatorLabelChange>())
            {
                label.LocalizedLabels.Add(new LocalizedLabel(item.Label ?? string.Empty, item.LanguageCode));
            }

            return label;
        }

        private static string BuildPublishXml(List<string> entityNames)
        {
            var sb = new StringBuilder();
            foreach (var entityName in entityNames ?? new List<string>())
            {
                sb.Append("<entity>").Append(RequireEntityName(entityName)).Append("</entity>");
            }

            return string.Concat("<importexportxml><entities>", sb.ToString(), "</entities></importexportxml>");
        }

        private static string RequireEntityName(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new InvalidPluginExecutionException("Attribute entityName is required.");
            }

            return entityName.ToLowerInvariant();
        }

        private static string RequireAttributeName(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidPluginExecutionException("Attribute name is required.");
            }

            return attributeName;
        }
    }
}
