using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal class GlobalOptionSet : ICustomAction
    {
        private const int OptionSetComponentType = 9;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<LoadingGlobalOptionSetInput>(json);
            var optionSets = new List<GlobalOptionSetMetadataOutput>();

            if (string.IsNullOrWhiteSpace(input.solutionId) || string.Equals(input.solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                return new LoadingGlobalOptionSetOutput { optionSets = optionSets };
            }

            if (!Guid.TryParse(input.solutionId, out var solutionId))
            {
                throw new InvalidPluginExecutionException("GlobalOptionSet solutionId must be a GUID or all.");
            }

            var optionSetIds = GetSolutionGlobalOptionSetIds(serviceAdmin, solutionId);
            foreach (var optionSetId in optionSetIds)
            {
                var optionSet = RetrieveGlobalOptionSet(serviceAdmin, optionSetId);
                if (optionSet == null || optionSet.IsCustomizable == null || !optionSet.IsCustomizable.Value || !optionSet.IsGlobal.GetValueOrDefault())
                {
                    continue;
                }

                optionSets.Add(BuildOptionSetOutput(optionSet));
            }

            optionSets.Sort((a, b) => string.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            return new LoadingGlobalOptionSetOutput { optionSets = optionSets };
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<SavingGlobalOptionSetInput>(json);
            var optionSetNames = new List<string>();
            var optionSetNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SaveOptionSetDescriptions(serviceAdmin, input.optionSetDescriptionUpdates, optionSetNames, optionSetNameSet);
            SaveOptionValues(serviceAdmin, input.optionValueUpdates, optionSetNames, optionSetNameSet);

            return new SavingGlobalOptionSetOutput
            {
                optionSetNames = optionSetNames,
                optionSetDescriptionUpdateCount = input.optionSetDescriptionUpdates?.Count ?? 0,
                optionValueUpdateCount = input.optionValueUpdates?.Count ?? 0
            };
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishingGlobalOptionSetInput>(json);
            var optionSetNames = GetValidOptionSetNames(input.optionSetNames);

            if (optionSetNames.Count > 0)
            {
                serviceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(optionSetNames)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return new PublishingGlobalOptionSetOutput { optionSetNames = optionSetNames };
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishedGlobalOptionSetInput>(json);
            Wait(PublishedWaitMilliseconds);
            return new PublishedGlobalOptionSetOutput { optionSetNames = GetValidOptionSetNames(input.optionSetNames) };
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<OtherGlobalOptionSetInput>(json);
            if (string.IsNullOrWhiteSpace(input.operation))
            {
                throw new InvalidPluginExecutionException("GlobalOptionSet Other operation is required.");
            }

            return new OtherGlobalOptionSetOutput { operation = input.operation };
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing GlobalOptionSet input.");
            }

            return input;
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
            var result = serviceAdmin.RetrieveMultiple(query);
            foreach (var component in result.Entities)
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

        private static GlobalOptionSetMetadataOutput BuildOptionSetOutput(OptionSetMetadataBase optionSet)
        {
            var output = new GlobalOptionSetMetadataOutput
            {
                MetadataId = optionSet.MetadataId.HasValue ? optionSet.MetadataId.Value.ToString("D") : string.Empty,
                Name = optionSet.Name,
                IsGlobal = optionSet.IsGlobal.GetValueOrDefault(),
                IsCustomizable = BuildManagedPropertyOutput(optionSet.IsCustomizable),
                Description = BuildLabelOutput(optionSet.Description)
            };

            var picklist = optionSet as OptionSetMetadata;
            if (picklist != null && picklist.Options != null)
            {
                foreach (var option in picklist.Options)
                {
                    output.Options.Add(BuildOptionOutput(option));
                }
            }

            var boolean = optionSet as BooleanOptionSetMetadata;
            if (boolean != null)
            {
                output.TrueOption = BuildOptionOutput(boolean.TrueOption);
                output.FalseOption = BuildOptionOutput(boolean.FalseOption);
            }

            return output;
        }

        private static ManagedPropertyOutput BuildManagedPropertyOutput(BooleanManagedProperty property)
        {
            return new ManagedPropertyOutput { Value = property != null && property.Value };
        }

        private static OptionMetadataOutput BuildOptionOutput(OptionMetadata option)
        {
            if (option == null)
            {
                return null;
            }

            return new OptionMetadataOutput
            {
                Value = option.Value,
                Label = BuildLabelOutput(option.Label),
                Description = BuildLabelOutput(option.Description)
            };
        }

        private static LabelOutput BuildLabelOutput(Label label)
        {
            var output = new LabelOutput();
            if (label == null || label.LocalizedLabels == null)
            {
                return output;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel == null)
                {
                    continue;
                }

                output.LocalizedLabels.Add(new LocalizedLabelOutput
                {
                    LanguageCode = localizedLabel.LanguageCode,
                    Label = localizedLabel.Label
                });
            }

            return output;
        }

        private static void SaveOptionSetDescriptions(IOrganizationService serviceAdmin, List<OptionSetDescriptionUpdate> updates, List<string> optionSetNames, HashSet<string> optionSetNameSet)
        {
            if (updates == null || updates.Count == 0)
            {
                return;
            }

            foreach (var update in updates)
            {
                ValidateOptionSetName(update?.optionSetName);
                var labels = BuildLabel(update.labels);

                var retrieveResponse = (RetrieveOptionSetResponse)serviceAdmin.Execute(new RetrieveOptionSetRequest
                {
                    Name = update.optionSetName,
                    RetrieveAsIfPublished = true
                });

                var optionSet = retrieveResponse.OptionSetMetadata;
                optionSet.Description = labels;

                serviceAdmin.Execute(new UpdateOptionSetRequest
                {
                    OptionSet = optionSet,
                    MergeLabels = true
                });

                AddOptionSetName(update.optionSetName, optionSetNames, optionSetNameSet);
            }
        }

        private static void SaveOptionValues(IOrganizationService serviceAdmin, List<OptionValueUpdate> updates, List<string> optionSetNames, HashSet<string> optionSetNameSet)
        {
            if (updates == null || updates.Count == 0)
            {
                return;
            }

            foreach (var update in updates)
            {
                ValidateOptionSetName(update?.optionSetName);
                if (!update.value.HasValue)
                {
                    throw new InvalidPluginExecutionException("GlobalOptionSet option value is required.");
                }

                var request = new UpdateOptionValueRequest
                {
                    OptionSetName = update.optionSetName,
                    Value = update.value.Value,
                    MergeLabels = true
                };

                var label = BuildLabel(update.labels);
                if (IsDescriptionComponent(update.component))
                {
                    request.Description = label;
                }
                else
                {
                    request.Label = label;
                }

                serviceAdmin.Execute(request);
                AddOptionSetName(update.optionSetName, optionSetNames, optionSetNameSet);
            }
        }

        private static List<string> GetValidOptionSetNames(List<string> optionSetNames)
        {
            var names = new List<string>();
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (optionSetNames == null)
            {
                return names;
            }

            foreach (var optionSetName in optionSetNames)
            {
                ValidateOptionSetName(optionSetName);
                if (nameSet.Add(optionSetName))
                {
                    names.Add(optionSetName);
                }
            }

            return names;
        }

        private static string BuildPublishXml(List<string> optionSetNames)
        {
            var optionSetXml = new StringBuilder();
            foreach (var optionSetName in optionSetNames)
            {
                optionSetXml.Append("<optionset>").Append(optionSetName).Append("</optionset>");
            }

            return "<importexportxml><optionsets>" + optionSetXml + "</optionsets></importexportxml>";
        }

        private static void Wait(int milliseconds)
        {
            if (milliseconds > 0)
            {
                Thread.Sleep(milliseconds);
            }
        }

        private static void AddOptionSetName(string optionSetName, List<string> optionSetNames, HashSet<string> optionSetNameSet)
        {
            if (optionSetNameSet.Add(optionSetName))
            {
                optionSetNames.Add(optionSetName);
            }
        }

        private static Label BuildLabel(List<LocalizedLabelInput> labels)
        {
            var label = new Label();
            if (labels == null)
            {
                return label;
            }

            foreach (var item in labels)
            {
                if (item == null || !item.languageCode.HasValue)
                {
                    continue;
                }

                label.LocalizedLabels.Add(new LocalizedLabel(item.label ?? string.Empty, item.languageCode.Value));
            }

            return label;
        }

        private static bool IsDescriptionComponent(string component)
        {
            return string.Equals(component, "Description", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateOptionSetName(string optionSetName)
        {
            if (string.IsNullOrWhiteSpace(optionSetName))
            {
                throw new InvalidPluginExecutionException("GlobalOptionSet optionSetName is required.");
            }
        }
    }

    internal class LoadingGlobalOptionSetInput : CustomActionInput
    {
        public string solutionId { get; set; }
    }

    internal class SavingGlobalOptionSetInput : CustomActionInput
    {
        public string component { get; set; }
        public List<OptionValueUpdate> optionValueUpdates { get; set; } = new List<OptionValueUpdate>();
        public List<OptionSetDescriptionUpdate> optionSetDescriptionUpdates { get; set; } = new List<OptionSetDescriptionUpdate>();
    }

    internal class PublishingGlobalOptionSetInput : CustomActionInput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
    }

    internal class PublishedGlobalOptionSetInput : CustomActionInput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
    }

    internal class OtherGlobalOptionSetInput : CustomActionInput
    {
    }

    internal class LoadingGlobalOptionSetOutput
    {
        public List<GlobalOptionSetMetadataOutput> optionSets { get; set; } = new List<GlobalOptionSetMetadataOutput>();
    }

    internal class SavingGlobalOptionSetOutput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
        public int optionValueUpdateCount { get; set; }
        public int optionSetDescriptionUpdateCount { get; set; }
    }

    internal class PublishingGlobalOptionSetOutput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
    }

    internal class PublishedGlobalOptionSetOutput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
    }

    internal class OtherGlobalOptionSetOutput
    {
        public string operation { get; set; }
    }

    internal class GlobalOptionSetMetadataOutput
    {
        public string MetadataId { get; set; }
        public string Name { get; set; }
        public bool IsGlobal { get; set; }
        public ManagedPropertyOutput IsCustomizable { get; set; }
        public LabelOutput Description { get; set; }
        public List<OptionMetadataOutput> Options { get; set; } = new List<OptionMetadataOutput>();
        public OptionMetadataOutput TrueOption { get; set; }
        public OptionMetadataOutput FalseOption { get; set; }
    }

    internal class ManagedPropertyOutput
    {
        public bool Value { get; set; }
    }

    internal class OptionMetadataOutput
    {
        public int? Value { get; set; }
        public LabelOutput Label { get; set; }
        public LabelOutput Description { get; set; }
    }

    internal class LabelOutput
    {
        public List<LocalizedLabelOutput> LocalizedLabels { get; set; } = new List<LocalizedLabelOutput>();
    }

    internal class LocalizedLabelOutput
    {
        public int LanguageCode { get; set; }
        public string Label { get; set; }
    }

    internal class OptionValueUpdate
    {
        public string optionSetName { get; set; }
        public int? value { get; set; }
        public string component { get; set; }
        public List<LocalizedLabelInput> labels { get; set; } = new List<LocalizedLabelInput>();
    }

    internal class OptionSetDescriptionUpdate
    {
        public string optionSetName { get; set; }
        public List<LocalizedLabelInput> labels { get; set; } = new List<LocalizedLabelInput>();
    }

    internal class LocalizedLabelInput
    {
        public int? languageCode { get; set; }
        public string label { get; set; }
    }
}
