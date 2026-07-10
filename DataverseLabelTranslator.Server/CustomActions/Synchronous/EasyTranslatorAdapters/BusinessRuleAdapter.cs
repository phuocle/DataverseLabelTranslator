using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class BusinessRuleAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "businessRules";
        private const string PublishKind = "entity";

        private static readonly Regex AttributeRegex = new Regex(
            @"([\w:]+)\s*=\s*(['""])(.*?)\2",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex StepLabelRegex = new Regex(
            @"<mcwo:StepLabel\b[^>]*/>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex SetMessageRegex = new Regex(
            @"<mcwc:SetMessage(?!\.)\b[^>]*>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(entityName) ||
                string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase))
            {
                return EmptyOutput(baseLanguage);
            }

            var attributeDisplayNames = LoadAttributeDisplayNames(serviceAdmin, entityName, baseLanguage);
            var workflows = RetrieveBusinessRules(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var workflow in workflows)
            {
                var workflowId = workflow.GetAttributeValue<Guid>("workflowid");
                if (workflowId == Guid.Empty)
                {
                    continue;
                }

                var name = workflow.GetAttributeValue<string>("name") ?? workflowId.ToString("D");
                var parent = new EasyTranslatorGridRowOutput
                {
                    Recid = "businessRule:" + workflowId.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, workflowId.ToString("D"), entityName),
                    SchemaName = "Business Rule: " + name + " (" + GetStateText(workflow) + ")",
                    RowType = "businessRules.rule",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var children = new List<EasyTranslatorGridRowOutput>();
                var labels = ParseStepLabels(workflow.GetAttributeValue<string>("xaml") ?? string.Empty, attributeDisplayNames);

                foreach (var label in labels)
                {
                    var child = new EasyTranslatorGridRowOutput
                    {
                        Recid = "businessRule:" + workflowId.ToString("D") + ":" + label.LabelId,
                        GridKey = BuildGridKey(TranslatorType, workflowId.ToString("D"), label.LabelId, entityName),
                        SchemaName = GetLabelRowName(label, attributeDisplayNames),
                        RowType = "businessRules.label",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = name }
                    };

                    foreach (var localizedLabel in label.Labels)
                    {
                        child.SetLanguageValue(localizedLabel.Key, localizedLabel.Value);
                    }

                    children.Add(child);
                }

                if (children.Count > 0)
                {
                    parent.Children = children;
                    rows.Add(parent);
                }
            }

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Business Rules",
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var updatesByWorkflow = BuildWorkflowUpdates(input);
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var workflowUpdate in updatesByWorkflow.Values)
            {
                var changed = SaveWorkflow(context.ServiceAdmin, workflowUpdate);
                if (!changed)
                {
                    continue;
                }

                output.changedRowCount += workflowUpdate.Labels.Count;
                AddPublishTarget(output, seenTargets, PublishKind, workflowUpdate.EntityName);
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

        private static Dictionary<Guid, WorkflowUpdate> BuildWorkflowUpdates(EasyTranslatorSaveInput input)
        {
            var updatesByWorkflow = new Dictionary<Guid, WorkflowUpdate>();
            var fallbackEntityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                // gridKey = "businessRules|{workflowid}|{labelId}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var workflowId = ValidateGuid(parts[1], "Business rule workflowid");
                var labelId = parts[2];
                var entityName = string.IsNullOrWhiteSpace(parts[3]) ? fallbackEntityName : parts[3];

                if (string.IsNullOrWhiteSpace(labelId))
                {
                    throw new InvalidPluginExecutionException("Business rule labelId is required.");
                }

                if (!updatesByWorkflow.TryGetValue(workflowId, out var workflowUpdate))
                {
                    workflowUpdate = new WorkflowUpdate
                    {
                        WorkflowId = workflowId,
                        EntityName = entityName
                    };
                    updatesByWorkflow[workflowId] = workflowUpdate;
                }

                if (!workflowUpdate.Labels.TryGetValue(labelId, out var labelUpdate))
                {
                    labelUpdate = new LabelUpdate { LabelId = labelId };
                    workflowUpdate.Labels[labelId] = labelUpdate;
                }

                foreach (var change in changes)
                {
                    labelUpdate.Labels[change.LanguageCode.ToString()] = change.Label ?? string.Empty;
                }
            }

            return updatesByWorkflow;
        }

        private static bool SaveWorkflow(IOrganizationService serviceAdmin, WorkflowUpdate workflowUpdate)
        {
            var workflow = serviceAdmin.Retrieve(
                "workflow",
                workflowUpdate.WorkflowId,
                new ColumnSet("workflowid", "name", "statecode", "statuscode", "xaml"));

            var originalXaml = workflow.GetAttributeValue<string>("xaml") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(originalXaml))
            {
                throw new InvalidPluginExecutionException(
                    "Business rule " + GetWorkflowName(workflow, workflowUpdate.WorkflowId) + " does not contain workflow XAML.");
            }

            var wasActive = GetOptionValue(workflow, "statecode") == 1;
            var deactivated = false;

            try
            {
                if (wasActive)
                {
                    SetWorkflowState(serviceAdmin, workflowUpdate.WorkflowId, 0, 1);
                    deactivated = true;
                }

                var updatedXaml = ApplyXamlUpdates(originalXaml, workflowUpdate.Labels.Values);
                var changed = !string.Equals(updatedXaml, originalXaml, StringComparison.Ordinal);

                if (changed)
                {
                    var update = new Entity("workflow", workflowUpdate.WorkflowId);
                    update["xaml"] = updatedXaml;
                    serviceAdmin.Update(update);
                }

                if (wasActive)
                {
                    SetWorkflowState(serviceAdmin, workflowUpdate.WorkflowId, 1, 2);
                }

                return changed;
            }
            catch (Exception ex)
            {
                var restoreError = TryRestoreWorkflow(serviceAdmin, workflowUpdate.WorkflowId, deactivated ? originalXaml : null, wasActive && deactivated);
                var restoreMessage = restoreError == null
                    ? ". The rule was restored to its original state."
                    : ". The rule restore also failed: " + restoreError.Message;

                throw new InvalidPluginExecutionException(
                    "Failed to save Business Rule translations for " +
                    GetWorkflowName(workflow, workflowUpdate.WorkflowId) +
                    restoreMessage +
                    "\n\nOriginal error: " +
                    ex.Message,
                    ex);
            }
        }

        private static Exception TryRestoreWorkflow(IOrganizationService serviceAdmin, Guid workflowId, string originalXaml, bool reactivate)
        {
            try
            {
                RestoreWorkflow(serviceAdmin, workflowId, originalXaml, reactivate);
                return null;
            }
            catch (Exception restoreError)
            {
                return restoreError;
            }
        }

        private static void RestoreWorkflow(IOrganizationService serviceAdmin, Guid workflowId, string originalXaml, bool reactivate)
        {
            if (!string.IsNullOrEmpty(originalXaml))
            {
                var update = new Entity("workflow", workflowId);
                update["xaml"] = originalXaml;
                serviceAdmin.Update(update);
            }

            if (reactivate)
            {
                SetWorkflowState(serviceAdmin, workflowId, 1, 2);
            }
        }

        private static void SetWorkflowState(IOrganizationService serviceAdmin, Guid workflowId, int stateCode, int statusCode)
        {
            var update = new Entity("workflow", workflowId);
            update["statecode"] = new OptionSetValue(stateCode);
            update["statuscode"] = new OptionSetValue(statusCode);
            serviceAdmin.Update(update);
        }

        private static List<Entity> RetrieveBusinessRules(IOrganizationService serviceAdmin, string entityName)
        {
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid", "name", "primaryentity", "category", "statecode", "statuscode", "scope", "clientdata", "xaml")
            };
            query.Criteria.AddCondition("category", ConditionOperator.Equal, 2);
            query.Criteria.AddCondition("primaryentity", ConditionOperator.Equal, entityName);
            query.Orders.Add(new OrderExpression("name", OrderType.Ascending));

            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static Dictionary<string, string> LoadAttributeDisplayNames(
            IOrganizationService serviceAdmin,
            string entityName,
            int baseLanguage)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var request = new RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.Attributes,
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveEntityResponse)serviceAdmin.Execute(request);

            foreach (var attribute in response.EntityMetadata.Attributes)
            {
                if (string.IsNullOrWhiteSpace(attribute.LogicalName))
                {
                    continue;
                }

                map[attribute.LogicalName] =
                    GetLabelText(attribute.DisplayName, baseLanguage) ??
                    attribute.SchemaName ??
                    attribute.LogicalName;
            }

            return map;
        }

        private static List<BusinessRuleStepLabel> ParseStepLabels(
            string xaml,
            Dictionary<string, string> attributeDisplayNames)
        {
            var labelsById = new Dictionary<string, BusinessRuleStepLabel>(StringComparer.OrdinalIgnoreCase);
            var orderedLabels = new List<BusinessRuleStepLabel>();

            foreach (Match match in StepLabelRegex.Matches(xaml ?? string.Empty))
            {
                var attributes = ParseAttributes(match.Value);
                attributes.TryGetValue("LabelId", out var labelId);
                attributes.TryGetValue("LanguageCode", out var languageCode);

                if (string.IsNullOrWhiteSpace(labelId) || string.IsNullOrWhiteSpace(languageCode))
                {
                    continue;
                }

                if (!labelsById.TryGetValue(labelId, out var label))
                {
                    var labelContext = GetLabelContext(xaml, match.Index);
                    label = new BusinessRuleStepLabel
                    {
                        LabelId = labelId,
                        Kind = labelContext.Kind,
                        SourceField = labelContext.SourceField,
                        Order = match.Index
                    };

                    labelsById[labelId] = label;
                    orderedLabels.Add(label);
                }

                attributes.TryGetValue("Description", out var description);
                label.Labels[languageCode] = description ?? string.Empty;
            }

            orderedLabels.Sort((a, b) => CompareStepLabels(a, b, attributeDisplayNames));
            return orderedLabels;
        }

        private static int CompareStepLabels(
            BusinessRuleStepLabel a,
            BusinessRuleStepLabel b,
            Dictionary<string, string> attributeDisplayNames)
        {
            var aLocation = GetFieldDisplayName(a.SourceField, attributeDisplayNames).ToLowerInvariant();
            var bLocation = GetFieldDisplayName(b.SourceField, attributeDisplayNames).ToLowerInvariant();
            var locationCompare = StringComparer.OrdinalIgnoreCase.Compare(aLocation, bLocation);
            if (locationCompare != 0)
            {
                return locationCompare;
            }

            var aKind = GetKindSortOrder(a.Kind);
            var bKind = GetKindSortOrder(b.Kind);
            if (aKind != bKind)
            {
                return aKind - bKind;
            }

            return a.Order - b.Order;
        }

        private static Dictionary<string, string> ParseAttributes(string tag)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in AttributeRegex.Matches(tag ?? string.Empty))
            {
                attributes[match.Groups[1].Value] = DecodeXmlAttribute(match.Groups[3].Value);
            }

            return attributes;
        }

        private static BusinessRuleLabelContext GetLabelContext(string xaml, int index)
        {
            var setMessageAttributes = ParseAttributes(GetActiveSetMessageTag(xaml, index) ?? string.Empty);
            setMessageAttributes.TryGetValue("ControlId", out var controlId);
            setMessageAttributes.TryGetValue("Level", out var level);

            if (IsInsideElement(xaml, index, "<mcwc:SetMessage.StepLabels>", "</mcwc:SetMessage.StepLabels>"))
            {
                return new BusinessRuleLabelContext
                {
                    Kind = string.Equals(level, "RECOMMENDATION", StringComparison.OrdinalIgnoreCase)
                        ? "Recommendation title"
                        : "Error message",
                    SourceField = controlId ?? string.Empty
                };
            }

            if (IsInsideElement(xaml, index, "x:Key=\"StepLabels\"", "</sco:Collection>"))
            {
                return new BusinessRuleLabelContext
                {
                    Kind = "Recommendation detail",
                    SourceField = controlId ?? string.Empty
                };
            }

            return new BusinessRuleLabelContext
            {
                Kind = "Business rule label",
                SourceField = controlId ?? string.Empty
            };
        }

        private static string GetActiveSetMessageTag(string xaml, int index)
        {
            var before = SafeSubstring(xaml, 0, index);
            Match lastMatch = null;

            foreach (Match match in SetMessageRegex.Matches(before))
            {
                lastMatch = match;
            }

            var openIdx = lastMatch != null ? lastMatch.Index : -1;
            var closeIdx = before.LastIndexOf("</mcwc:SetMessage>", StringComparison.Ordinal);

            return openIdx != -1 && openIdx > closeIdx ? lastMatch.Value : null;
        }

        private static bool IsInsideElement(string xaml, int index, string openTag, string closeTag)
        {
            var before = SafeSubstring(xaml, 0, index);
            var openIdx = before.LastIndexOf(openTag, StringComparison.Ordinal);
            var closeIdx = before.LastIndexOf(closeTag, StringComparison.Ordinal);

            return openIdx != -1 && openIdx > closeIdx;
        }

        private static string ApplyXamlUpdates(string xaml, ICollection<LabelUpdate> labelUpdates)
        {
            var updatedXaml = xaml ?? string.Empty;

            foreach (var update in labelUpdates)
            {
                foreach (var label in update.Labels)
                {
                    updatedXaml = UpsertStepLabel(updatedXaml, update.LabelId, label.Key, label.Value);
                }
            }

            return updatedXaml;
        }

        private static string UpsertStepLabel(string xaml, string labelId, string languageCode, string description)
        {
            var encodedDescription = EncodeXmlAttribute(description);
            var existing = FindStepLabelTag(xaml, attributes =>
                string.Equals(GetAttributeValue(attributes, "LabelId"), labelId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetAttributeValue(attributes, "LanguageCode"), languageCode, StringComparison.Ordinal));

            if (existing != null)
            {
                var updatedTag = UpdateDescriptionAttribute(existing.Tag, encodedDescription);
                return xaml.Substring(0, existing.Start) + updatedTag + xaml.Substring(existing.End);
            }

            var anchor = FindStepLabelTag(xaml, attributes =>
                string.Equals(GetAttributeValue(attributes, "LabelId"), labelId, StringComparison.OrdinalIgnoreCase));

            if (anchor == null)
            {
                throw new InvalidPluginExecutionException("Business rule label " + labelId + " was not found in workflow XAML.");
            }

            var closingIdx = FindLabelCollectionEnd(xaml, anchor.Start);
            if (closingIdx == -1)
            {
                throw new InvalidPluginExecutionException("Business rule label collection for " + labelId + " was not found in workflow XAML.");
            }

            var newTag =
                "<mcwo:StepLabel Description=\"" +
                encodedDescription +
                "\" LabelId=\"" +
                labelId +
                "\" LanguageCode=\"" +
                languageCode +
                "\" />";

            return xaml.Substring(0, closingIdx) + newTag + xaml.Substring(closingIdx);
        }

        private static BusinessRuleStepLabelTag FindStepLabelTag(
            string xaml,
            Func<Dictionary<string, string>, bool> predicate)
        {
            foreach (Match match in StepLabelRegex.Matches(xaml ?? string.Empty))
            {
                var attributes = ParseAttributes(match.Value);
                if (predicate(attributes))
                {
                    return new BusinessRuleStepLabelTag
                    {
                        Tag = match.Value,
                        Start = match.Index,
                        End = match.Index + match.Length
                    };
                }
            }

            return null;
        }

        private static string UpdateDescriptionAttribute(string tag, string encodedDescription)
        {
            if (Regex.IsMatch(tag, @"Description\s*=\s*(['""]).*?\1", RegexOptions.Singleline))
            {
                return Regex.Replace(
                    tag,
                    @"Description\s*=\s*(['""]).*?\1",
                    "Description=\"" + encodedDescription + "\"",
                    RegexOptions.Singleline);
            }

            return Regex.Replace(tag, @"/>$", " Description=\"" + encodedDescription + "\" />");
        }

        private static int FindLabelCollectionEnd(string xaml, int anchorStart)
        {
            var setMessageEnd = xaml.IndexOf("</mcwc:SetMessage.StepLabels>", anchorStart, StringComparison.Ordinal);
            var collectionEnd = xaml.IndexOf("</sco:Collection>", anchorStart, StringComparison.Ordinal);

            if (setMessageEnd == -1)
            {
                return collectionEnd;
            }

            if (collectionEnd == -1)
            {
                return setMessageEnd;
            }

            return Math.Min(setMessageEnd, collectionEnd);
        }

        private static string GetLabelRowName(
            BusinessRuleStepLabel label,
            Dictionary<string, string> attributeDisplayNames)
        {
            var location = GetFieldDisplayName(label.SourceField, attributeDisplayNames);
            var kind = label.Kind ?? "Business rule label";

            return string.IsNullOrWhiteSpace(location) ? kind : location + " / " + kind;
        }

        private static string GetFieldDisplayName(string logicalName, Dictionary<string, string> attributeDisplayNames)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return string.Empty;
            }

            return attributeDisplayNames.TryGetValue(logicalName, out var displayName) ? displayName : logicalName;
        }

        private static int GetKindSortOrder(string kind)
        {
            var normalized = (kind ?? string.Empty).ToLowerInvariant();

            if (normalized == "error message")
            {
                return 1;
            }

            if (normalized == "recommendation title")
            {
                return 2;
            }

            if (normalized == "recommendation detail")
            {
                return 3;
            }

            return 99;
        }

        private static string GetStateText(Entity workflow)
        {
            return GetOptionValue(workflow, "statecode") == 1 ? "Active" : "Draft";
        }

        private static string GetWorkflowName(Entity workflow, Guid workflowId)
        {
            var name = workflow?.GetAttributeValue<string>("name");
            return string.IsNullOrWhiteSpace(name) ? workflowId.ToString("D") : name;
        }

        private static string GetLabelText(Label label, int languageCode)
        {
            if (label?.LocalizedLabels == null)
            {
                return null;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel != null &&
                    localizedLabel.LanguageCode == languageCode &&
                    !string.IsNullOrWhiteSpace(localizedLabel.Label))
                {
                    return localizedLabel.Label;
                }
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel != null && !string.IsNullOrWhiteSpace(localizedLabel.Label))
                {
                    return localizedLabel.Label;
                }
            }

            return null;
        }

        private static string DecodeXmlAttribute(string value)
        {
            var text = value ?? string.Empty;

            for (var i = 0; i < 3; i++)
            {
                var decoded = WebUtility.HtmlDecode(text);
                if (string.Equals(decoded, text, StringComparison.Ordinal))
                {
                    break;
                }

                text = decoded;
            }

            return text;
        }

        private static string EncodeXmlAttribute(string value)
        {
            var text = value ?? string.Empty;
            return text
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string GetAttributeValue(Dictionary<string, string> attributes, string name)
        {
            return attributes.TryGetValue(name, out var value) ? value : string.Empty;
        }

        private static string SafeSubstring(string value, int startIndex, int length)
        {
            value = value ?? string.Empty;
            if (startIndex < 0 || startIndex >= value.Length || length <= 0)
            {
                return string.Empty;
            }

            return value.Substring(startIndex, Math.Min(length, value.Length - startIndex));
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
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
                    title = "Business Rules",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private class WorkflowUpdate
        {
            public Guid WorkflowId { get; set; }
            public string EntityName { get; set; }
            public Dictionary<string, LabelUpdate> Labels { get; } = new Dictionary<string, LabelUpdate>(StringComparer.OrdinalIgnoreCase);
        }

        private class LabelUpdate
        {
            public string LabelId { get; set; }
            public Dictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private class BusinessRuleStepLabel
        {
            public string LabelId { get; set; }
            public string Kind { get; set; }
            public string SourceField { get; set; }
            public int Order { get; set; }
            public Dictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private class BusinessRuleLabelContext
        {
            public string Kind { get; set; }
            public string SourceField { get; set; }
        }

        private class BusinessRuleStepLabelTag
        {
            public string Tag { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
        }
    }
}
