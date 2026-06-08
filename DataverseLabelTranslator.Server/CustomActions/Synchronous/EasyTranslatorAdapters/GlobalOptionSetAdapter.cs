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
    public class GlobalOptionSetAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int OptionSetComponentType = 9;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "globalOptionSet";
        private const string PublishKind = "globalOptionSet";
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var rows = new List<EasyTranslatorGridRowOutput>();

            if (!string.IsNullOrWhiteSpace(input.solutionId) && !string.Equals(input.solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (!Guid.TryParse(input.solutionId, out var solutionId))
                {
                    throw new InvalidPluginExecutionException("GlobalOptionSet solutionId must be a GUID or all.");
                }

                foreach (var optionSetId in GetSolutionGlobalOptionSetIds(context.ServiceAdmin, solutionId))
                {
                    var optionSet = RetrieveGlobalOptionSet(context.ServiceAdmin, optionSetId);
                    if (!IsEligibleGlobalOptionSet(optionSet))
                    {
                        continue;
                    }

                    rows.Add(BuildOptionSetRow(optionSet, input.component));
                }
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Global Option Sets",
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

                var parts = ParseGridKey(row.gridKey, TranslatorType, 2);
                var optionSetName = parts[1];
                ValidateOptionSetName(optionSetName);

                if (parts.Length >= 3 && string.Equals(parts[2], "description", StringComparison.OrdinalIgnoreCase))
                {
                    SaveOptionSetDescription(context.ServiceAdmin, optionSetName, changes);
                    output.changedRowCount++;
                    AddPublishTarget(output, seenTargets, PublishKind, optionSetName);
                    continue;
                }

                if (parts.Length >= 5 && string.Equals(parts[2], "option", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(parts[3], out var optionValue))
                    {
                        throw new InvalidPluginExecutionException("GlobalOptionSet option value is required.");
                    }

                    SaveOptionValue(context.ServiceAdmin, optionSetName, optionValue, parts[4], changes);
                    output.changedRowCount++;
                    AddPublishTarget(output, seenTargets, PublishKind, optionSetName);
                    continue;
                }

                throw new InvalidPluginExecutionException("GlobalOptionSet gridKey does not target an editable row.");
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var optionSetNames = GetPublishTargetIds(input, PublishKind, false);

            if (optionSetNames.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(optionSetNames)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return ToPublishOutput(PublishKind, optionSetNames);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            Wait(PublishedWaitMilliseconds);
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, false));
        }

        private static List<Guid> GetSolutionGlobalOptionSetIds(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, OptionSetComponentType);

            var optionSetIds = new List<Guid>();
            foreach (var component in ToList(serviceAdmin.RetrieveMultiple(query)))
            {
                if (!component.Contains("objectid"))
                {
                    continue;
                }

                var optionSetId = component.GetAttributeValue<Guid>("objectid");
                if (optionSetId != Guid.Empty)
                {
                    optionSetIds.Add(optionSetId);
                }
            }

            return optionSetIds;
        }

        private static OptionSetMetadataBase RetrieveGlobalOptionSet(IOrganizationService serviceAdmin, Guid optionSetId)
        {
            var response = (RetrieveOptionSetResponse)serviceAdmin.Execute(new RetrieveOptionSetRequest
            {
                MetadataId = optionSetId,
                RetrieveAsIfPublished = true
            });

            return response.OptionSetMetadata;
        }

        private static bool IsEligibleGlobalOptionSet(OptionSetMetadataBase optionSet)
        {
            return optionSet != null &&
                optionSet.IsCustomizable != null &&
                optionSet.IsCustomizable.Value &&
                optionSet.IsGlobal.GetValueOrDefault();
        }

        private static EasyTranslatorGridRowOutput BuildOptionSetRow(OptionSetMetadataBase optionSet, string component)
        {
            var isDescription = IsDescriptionComponent(component);
            var parent = new EasyTranslatorGridRowOutput
            {
                Recid = "globalOptionSet:" + optionSet.Name,
                GridKey = isDescription
                    ? BuildGridKey(TranslatorType, optionSet.Name, "description")
                    : BuildGridKey(TranslatorType, optionSet.Name),
                SchemaName = optionSet.Name,
                RowType = "globalOptionSet.parent",
                IsEditable = isDescription,
                IsTranslatable = isDescription,
                Ai = new EasyTranslatorAiOutput { include = isDescription }
            };

            if (isDescription)
            {
                AddLabelValues(parent, optionSet.Description);
            }

            parent.Children = BuildOptionRows(optionSet, component, optionSet.Name);
            return parent;
        }

        private static List<EasyTranslatorGridRowOutput> BuildOptionRows(OptionSetMetadataBase optionSet, string component, string optionSetName)
        {
            var rows = new List<EasyTranslatorGridRowOutput>();
            var picklist = optionSet as OptionSetMetadata;
            if (picklist != null && picklist.Options != null)
            {
                foreach (var option in picklist.Options)
                {
                    AddOptionRow(rows, optionSetName, option, component);
                }
            }

            var boolean = optionSet as BooleanOptionSetMetadata;
            if (boolean != null)
            {
                AddOptionRow(rows, optionSetName, boolean.TrueOption, component);
                AddOptionRow(rows, optionSetName, boolean.FalseOption, component);
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));
            return rows;
        }

        private static void AddOptionRow(List<EasyTranslatorGridRowOutput> rows, string optionSetName, OptionMetadata option, string component)
        {
            if (option == null || !option.Value.HasValue)
            {
                return;
            }

            var optionValue = option.Value.Value.ToString();
            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "globalOptionSet:" + optionSetName + ":" + optionValue,
                GridKey = BuildGridKey(TranslatorType, optionSetName, "option", optionValue, component),
                SchemaName = optionValue,
                RowType = "globalOptionSet.option",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = optionSetName }
            };

            AddLabelValues(row, IsDescriptionComponent(component) ? option.Description : option.Label);
            rows.Add(row);
        }

        private static void SaveOptionSetDescription(IOrganizationService serviceAdmin, string optionSetName, List<EasyTranslatorLabelChange> changes)
        {
            var retrieveResponse = (RetrieveOptionSetResponse)serviceAdmin.Execute(new RetrieveOptionSetRequest
            {
                Name = optionSetName,
                RetrieveAsIfPublished = true
            });

            var optionSet = retrieveResponse.OptionSetMetadata;
            optionSet.Description = BuildLabel(changes);

            serviceAdmin.Execute(new UpdateOptionSetRequest
            {
                OptionSet = optionSet,
                MergeLabels = true
            });
        }

        private static void SaveOptionValue(IOrganizationService serviceAdmin, string optionSetName, int optionValue, string component, List<EasyTranslatorLabelChange> changes)
        {
            var request = new UpdateOptionValueRequest
            {
                OptionSetName = optionSetName,
                Value = optionValue,
                MergeLabels = true
            };

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

        private static string BuildPublishXml(List<string> optionSetNames)
        {
            var optionSetXml = new StringBuilder();
            foreach (var optionSetName in optionSetNames)
            {
                ValidateOptionSetName(optionSetName);
                optionSetXml.Append("<optionset>").Append(optionSetName).Append("</optionset>");
            }

            return string.Concat("<importexportxml><optionsets>", optionSetXml.ToString(), "</optionsets></importexportxml>");
        }

        private static void ValidateOptionSetName(string optionSetName)
        {
            if (string.IsNullOrWhiteSpace(optionSetName))
            {
                throw new InvalidPluginExecutionException("GlobalOptionSet optionSetName is required.");
            }
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }
    }
}
