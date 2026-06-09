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
    public class OptionSetAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "options";
        private const string PublishKindEntity = "entity";
        private const string PublishKindGlobalOptionSet = "globalOptionSet";
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var entityName = RequireEntityName(input.entityName);
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var entity = RetrieveEntityMetadata(context.ServiceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var attribute in entity.Attributes ?? new AttributeMetadata[0])
            {
                if (!TryGetOptionSet(attribute, out var optionSet) || optionSet == null)
                {
                    continue;
                }

                var row = BuildAttributeRow(entityName, attribute, optionSet, input.component);
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
                    mode = "tree",
                    title = "Option Sets",
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

                var parts = ParseGridKey(row.gridKey, TranslatorType, 8);
                var entityName = RequireEntityName(parts[1]);
                var attributeName = RequireAttributeName(parts[2]);
                if (!string.Equals(parts[3], "option", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException("OptionSet gridKey does not target an option row.");
                }

                if (!int.TryParse(parts[4], out var optionValue))
                {
                    throw new InvalidPluginExecutionException("OptionSet option value is required.");
                }

                var component = parts[5];
                var scope = parts[6];
                var optionSetName = parts[7];

                SaveOptionValue(context.ServiceAdmin, entityName, attributeName, optionSetName, scope, optionValue, component, changes);

                output.changedRowCount++;
                AddPublishTarget(output, seenTargets, PublishKindEntity, entityName);
                if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase))
                {
                    AddPublishTarget(output, seenTargets, PublishKindGlobalOptionSet, optionSetName);
                }
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var entityNames = GetPublishTargetIds(input, PublishKindEntity, false);
            var optionSetNames = GetPublishTargetIds(input, PublishKindGlobalOptionSet, false);

            if (entityNames.Count > 0 || optionSetNames.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(entityNames, optionSetNames)
                });
            }

            return ToPublishOutput(entityNames, optionSetNames);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            WaitAction(PublishedWaitMilliseconds);
            return ToPublishOutput(
                GetPublishTargetIds(input, PublishKindEntity, false),
                GetPublishTargetIds(input, PublishKindGlobalOptionSet, false));
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
                throw new InvalidPluginExecutionException("OptionSet entity metadata was not found.");
            }

            return response.EntityMetadata;
        }

        private static bool TryGetOptionSet(AttributeMetadata attribute, out OptionSetMetadataBase optionSet)
        {
            optionSet = null;

            var enumAttribute = attribute as EnumAttributeMetadata;
            if (enumAttribute != null)
            {
                optionSet = enumAttribute.OptionSet;
                return optionSet != null;
            }

            var booleanAttribute = attribute as BooleanAttributeMetadata;
            if (booleanAttribute != null)
            {
                optionSet = booleanAttribute.OptionSet;
                return optionSet != null;
            }

            return false;
        }

        private static EasyTranslatorGridRowOutput BuildAttributeRow(
            string entityName,
            AttributeMetadata attribute,
            OptionSetMetadataBase optionSet,
            string component)
        {
            if (attribute == null ||
                !attribute.MetadataId.HasValue ||
                string.IsNullOrWhiteSpace(attribute.LogicalName))
            {
                return null;
            }

            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "options:" + entityName + ":" + attribute.LogicalName,
                GridKey = BuildGridKey(TranslatorType, entityName, attribute.LogicalName),
                SchemaName = attribute.LogicalName,
                RowType = "options.attribute",
                IsEditable = false,
                IsTranslatable = false,
                Ai = new EasyTranslatorAiOutput { include = false }
            };

            row.Children = BuildOptionRows(entityName, attribute.LogicalName, optionSet, component);
            return row;
        }

        private static List<EasyTranslatorGridRowOutput> BuildOptionRows(
            string entityName,
            string attributeName,
            OptionSetMetadataBase optionSet,
            string component)
        {
            var rows = new List<EasyTranslatorGridRowOutput>();
            var picklist = optionSet as OptionSetMetadata;
            if (picklist != null && picklist.Options != null)
            {
                foreach (var option in picklist.Options)
                {
                    AddOptionRow(rows, entityName, attributeName, optionSet, option, component);
                }
            }

            var boolean = optionSet as BooleanOptionSetMetadata;
            if (boolean != null)
            {
                AddOptionRow(rows, entityName, attributeName, optionSet, boolean.TrueOption, component);
                AddOptionRow(rows, entityName, attributeName, optionSet, boolean.FalseOption, component);
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));
            return rows;
        }

        private static void AddOptionRow(
            List<EasyTranslatorGridRowOutput> rows,
            string entityName,
            string attributeName,
            OptionSetMetadataBase optionSet,
            OptionMetadata option,
            string component)
        {
            if (option == null || !option.Value.HasValue)
            {
                return;
            }

            var optionValue = option.Value.Value.ToString();
            var scope = optionSet != null && optionSet.IsGlobal.GetValueOrDefault() ? "global" : "local";
            var optionSetName = optionSet?.Name ?? string.Empty;
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "options:" + entityName + ":" + attributeName + ":" + optionValue,
                GridKey = BuildGridKey(TranslatorType, entityName, attributeName, "option", optionValue, component, scope, optionSetName),
                SchemaName = optionValue,
                RowType = "options.option",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = attributeName }
            };

            AddLabelValues(row, IsDescriptionComponent(component) ? option.Description : option.Label);
            rows.Add(row);
        }

        private static void SaveOptionValue(
            IOrganizationService serviceAdmin,
            string entityName,
            string attributeName,
            string optionSetName,
            string scope,
            int optionValue,
            string component,
            List<EasyTranslatorLabelChange> changes)
        {
            var request = new UpdateOptionValueRequest
            {
                Value = optionValue,
                MergeLabels = true
            };

            if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase))
            {
                ValidateOptionSetName(optionSetName);
                request.OptionSetName = optionSetName;
            }
            else
            {
                request.EntityLogicalName = entityName;
                request.AttributeLogicalName = attributeName;
            }

            var label = BuildLabel(changes);
            if (IsDescriptionComponent(component))
            {
                request.Description = label;
            }
            else
            {
                request.Label = label;
            }

            serviceAdmin.Execute(request);
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

        private static EasyTranslatorSaveOutput ToPublishOutput(List<string> entityNames, List<string> optionSetNames)
        {
            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entityName in entityNames ?? new List<string>())
            {
                AddPublishTarget(output, seen, PublishKindEntity, entityName);
            }

            foreach (var optionSetName in optionSetNames ?? new List<string>())
            {
                AddPublishTarget(output, seen, PublishKindGlobalOptionSet, optionSetName);
            }

            return output;
        }

        private static string BuildPublishXml(List<string> entityNames, List<string> optionSetNames)
        {
            var entityXml = new StringBuilder();
            foreach (var entityName in entityNames ?? new List<string>())
            {
                entityXml.Append("<entity>").Append(RequireEntityName(entityName)).Append("</entity>");
            }

            var optionSetXml = new StringBuilder();
            foreach (var optionSetName in optionSetNames ?? new List<string>())
            {
                optionSetXml.Append("<optionset>").Append(ValidateOptionSetName(optionSetName)).Append("</optionset>");
            }

            return string.Concat(
                "<importexportxml><entities>",
                entityXml.ToString(),
                "</entities><optionsets>",
                optionSetXml.ToString(),
                "</optionsets></importexportxml>");
        }

        private static string RequireEntityName(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new InvalidPluginExecutionException("OptionSet entityName is required.");
            }

            return entityName.ToLowerInvariant();
        }

        private static string RequireAttributeName(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new InvalidPluginExecutionException("OptionSet attribute name is required.");
            }

            return attributeName;
        }

        private static string ValidateOptionSetName(string optionSetName)
        {
            if (string.IsNullOrWhiteSpace(optionSetName))
            {
                throw new InvalidPluginExecutionException("OptionSet optionSetName is required.");
            }

            return optionSetName;
        }
    }
}
