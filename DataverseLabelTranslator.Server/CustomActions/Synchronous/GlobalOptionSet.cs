using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal class GlobalOptionSet : ICustomAction
    {
        public CustomActionOutput Execute(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = DevKitJson.Deserialize<SaveGlobalOptionSetInput>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing GlobalOptionSet input.");
            }

            var optionSetNames = new List<string>();
            var optionSetNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SaveOptionSetDescriptions(serviceAdmin, input.optionSetDescriptionUpdates, optionSetNames, optionSetNameSet);
            SaveOptionValues(serviceAdmin, input.optionValueUpdates, optionSetNames, optionSetNameSet);

            return new SaveGlobalOptionSetOutput
            {
                ok = true,
                optionSetNames = optionSetNames,
                optionSetDescriptionUpdateCount = input.optionSetDescriptionUpdates?.Count ?? 0,
                optionValueUpdateCount = input.optionValueUpdates?.Count ?? 0
            };
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

    internal class SaveGlobalOptionSetInput
    {
        public string component { get; set; }
        public List<OptionValueUpdate> optionValueUpdates { get; set; } = new List<OptionValueUpdate>();
        public List<OptionSetDescriptionUpdate> optionSetDescriptionUpdates { get; set; } = new List<OptionSetDescriptionUpdate>();
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

    internal class SaveGlobalOptionSetOutput : CustomActionOutput
    {
        public List<string> optionSetNames { get; set; } = new List<string>();
        public int optionValueUpdateCount { get; set; }
        public int optionSetDescriptionUpdateCount { get; set; }
    }
}
