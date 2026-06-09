using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class BpfAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "bpf";
        private const string PublishKind = "entity";

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

            var workflows = RetrieveBusinessProcessFlows(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var workflow in workflows)
            {
                var workflowId = workflow.GetAttributeValue<Guid>("workflowid");
                if (workflowId == Guid.Empty)
                {
                    continue;
                }

                var clientData = workflow.GetAttributeValue<string>("clientdata") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(clientData))
                {
                    continue;
                }

                var stages = ParseStages(clientData);
                if (stages.Count == 0)
                {
                    continue;
                }

                var name = workflow.GetAttributeValue<string>("name") ?? workflowId.ToString("D");
                var parent = new EasyTranslatorGridRowOutput
                {
                    Recid = "bpf:" + workflowId.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, workflowId.ToString("D"), entityName),
                    SchemaName = name,
                    RowType = "bpf.process",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var children = new List<EasyTranslatorGridRowOutput>();
                foreach (var stage in stages)
                {
                    var stageRow = new EasyTranslatorGridRowOutput
                    {
                        Recid = "bpf:" + workflowId.ToString("D") + ":" + stage.StageId,
                        GridKey = BuildGridKey(TranslatorType, workflowId.ToString("D"), stage.StageId, "stage", entityName),
                        SchemaName = "[Stage] " + (stage.Description ?? string.Empty),
                        RowType = "bpf.stage",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = name }
                    };

                    AddBpfLabelValues(stageRow, stage.Labels);

                    var fieldRows = new List<EasyTranslatorGridRowOutput>();
                    foreach (var field in stage.Fields)
                    {
                        var fieldRow = new EasyTranslatorGridRowOutput
                        {
                            Recid = "bpf:" + workflowId.ToString("D") + ":" + field.StepStepId,
                            GridKey = BuildGridKey(TranslatorType, workflowId.ToString("D"), field.StepStepId, "field", entityName),
                            SchemaName = "[Field] " + (field.Description ?? string.Empty),
                            RowType = "bpf.field",
                            IsEditable = true,
                            IsTranslatable = true,
                            Ai = new EasyTranslatorAiOutput { include = true, location = name + " / " + stage.Description }
                        };

                        AddBpfLabelValues(fieldRow, field.Labels);
                        fieldRows.Add(fieldRow);
                    }

                    stageRow.Children = fieldRows;
                    children.Add(stageRow);
                }

                parent.Children = children;
                rows.Add(parent);
            }

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Business Process Flows",
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

                // gridKey = "bpf|{workflowid}|{stageOrStepId}|{stage|field}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 5);
                var workflowId = ValidateGuid(parts[1], "BPF workflowid");
                var labelId = parts[2];
                var entityName = string.IsNullOrWhiteSpace(parts[4]) ? fallbackEntityName : parts[4];

                if (string.IsNullOrWhiteSpace(labelId))
                {
                    throw new InvalidPluginExecutionException("BPF stage or field id is required.");
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
                    "Business Process Flow " + GetWorkflowName(workflow, workflowUpdate.WorkflowId) + " does not contain workflow XAML.");
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
                RestoreWorkflow(serviceAdmin, workflowUpdate.WorkflowId, deactivated ? originalXaml : null, wasActive && deactivated);
                throw new InvalidPluginExecutionException(
                    "Failed to save Business Process Flow translations for " +
                    GetWorkflowName(workflow, workflowUpdate.WorkflowId) +
                    ". The BPF was restored to its original state.\n\nOriginal error: " +
                    ex.Message,
                    ex);
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

        private static List<Entity> RetrieveBusinessProcessFlows(IOrganizationService serviceAdmin, string entityName)
        {
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid", "name", "primaryentity", "category", "statecode", "statuscode", "clientdata")
            };
            query.Criteria.AddCondition("category", ConditionOperator.Equal, 4);
            query.Criteria.AddCondition("primaryentity", ConditionOperator.Equal, entityName);
            query.Orders.Add(new OrderExpression("name", OrderType.Ascending));

            return RetrieveAll(serviceAdmin, query);
        }

        private static List<Entity> RetrieveAll(IOrganizationService serviceAdmin, QueryExpression query)
        {
            var results = new List<Entity>();
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1
            };

            while (true)
            {
                var page = serviceAdmin.RetrieveMultiple(query);
                results.AddRange(ToList(page));

                if (!page.MoreRecords)
                {
                    return results;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        private static List<BpfStage> ParseStages(string clientData)
        {
            var stages = new List<BpfStage>();

            try
            {
                var root = DevKitJson.Deserialize(clientData);
                FindStageSteps(root, stages);
            }
            catch
            {
                return stages;
            }

            return stages;
        }

        private static void FindStageSteps(object step, List<BpfStage> results)
        {
            var node = AsDictionary(step);
            if (node == null)
            {
                return;
            }

            var className = GetString(node, "__class");
            var isStage = className.IndexOf("StageStep:#", StringComparison.OrdinalIgnoreCase) != -1 ||
                className.IndexOf("PageStep:#", StringComparison.OrdinalIgnoreCase) != -1;

            if (isStage && !string.IsNullOrWhiteSpace(GetString(node, "description")))
            {
                var stage = new BpfStage
                {
                    StageId = GetString(node, "stageId"),
                    Description = GetString(node, "description")
                };
                stage.Labels.AddRange(ParseLabels(GetValue(node, "stepLabels")));

                foreach (var child in GetSteps(node))
                {
                    var field = AsDictionary(child);
                    if (field == null)
                    {
                        continue;
                    }

                    var fieldClassName = GetString(field, "__class");
                    var stepStepId = GetString(field, "stepStepId");
                    if (fieldClassName.IndexOf("StepStep:#", StringComparison.OrdinalIgnoreCase) == -1 ||
                        string.IsNullOrWhiteSpace(stepStepId))
                    {
                        continue;
                    }

                    var fieldInfo = new BpfField
                    {
                        StepStepId = stepStepId,
                        Description = GetString(field, "description")
                    };
                    fieldInfo.Labels.AddRange(ParseLabels(GetValue(field, "stepLabels")));
                    stage.Fields.Add(fieldInfo);
                }

                results.Add(stage);
            }

            foreach (var child in GetSteps(node))
            {
                FindStageSteps(child, results);
            }
        }

        private static List<BpfLabel> ParseLabels(object stepLabels)
        {
            var labels = new List<BpfLabel>();
            var labelContainer = AsDictionary(stepLabels);
            if (labelContainer == null)
            {
                return labels;
            }

            foreach (var labelItem in GetList(GetValue(labelContainer, "list")))
            {
                var label = AsDictionary(labelItem);
                if (label == null)
                {
                    continue;
                }

                var languageCode = GetString(label, "languageCode");
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    continue;
                }

                labels.Add(new BpfLabel
                {
                    LanguageCode = languageCode,
                    Description = GetString(label, "description")
                });
            }

            return labels;
        }

        private static IEnumerable<object> GetSteps(Dictionary<string, object> node)
        {
            var steps = AsDictionary(GetValue(node, "steps"));
            return steps == null ? new List<object>() : GetList(GetValue(steps, "list"));
        }

        private static void AddBpfLabelValues(EasyTranslatorGridRowOutput row, List<BpfLabel> labels)
        {
            foreach (var label in labels)
            {
                row.SetLanguageValue(label.LanguageCode, label.Description);
            }
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
            var markerIndex = FindStageOrStepMarker(xaml, labelId);
            if (markerIndex == -1)
            {
                return xaml;
            }

            var closingTag = "</sco:Collection>";
            var beforeMarker = xaml.Substring(0, markerIndex);
            var closingIndex = beforeMarker.LastIndexOf(closingTag, StringComparison.Ordinal);
            if (closingIndex == -1)
            {
                return xaml;
            }

            var existing = FindExistingStepLabel(xaml, closingIndex, labelId, languageCode);
            var encodedDescription = EncodeXmlAttribute(description);
            if (existing != null)
            {
                var updatedTag = UpdateDescriptionAttribute(existing.Tag, encodedDescription);
                return xaml.Substring(0, existing.Start) + updatedTag + xaml.Substring(existing.End);
            }

            var newStepLabel =
                "<mcwo:StepLabel Description=\"" +
                encodedDescription +
                "\" LabelId=\"" +
                labelId +
                "\" LanguageCode=\"" +
                languageCode +
                "\" />";

            return xaml.Substring(0, closingIndex) + newStepLabel + xaml.Substring(closingIndex);
        }

        private static int FindStageOrStepMarker(string xaml, string labelId)
        {
            var stageMarker = "<x:String x:Key=\"StageId\">" + labelId + "</x:String>";
            var stepMarker = "<x:String x:Key=\"ProcessStepId\">" + labelId + "</x:String>";
            var markerIndex = xaml.IndexOf(stageMarker, StringComparison.Ordinal);
            return markerIndex == -1 ? xaml.IndexOf(stepMarker, StringComparison.Ordinal) : markerIndex;
        }

        private static BpfStepLabelTag FindExistingStepLabel(string xaml, int closingIndex, string labelId, string languageCode)
        {
            var searchStart = Math.Max(0, closingIndex - 500);
            var searchLength = closingIndex - searchStart;
            var searchText = xaml.Substring(searchStart, searchLength);
            var labelRegex = new Regex(
                "<mcwo:StepLabel[^/]*LabelId=\"" +
                Regex.Escape(labelId) +
                "\"[^/]*LanguageCode=\"" +
                Regex.Escape(languageCode) +
                "\"[^/]*/>",
                RegexOptions.Singleline);
            var match = labelRegex.Match(searchText);

            if (!match.Success)
            {
                return null;
            }

            return new BpfStepLabelTag
            {
                Tag = match.Value,
                Start = searchStart + match.Index,
                End = searchStart + match.Index + match.Length
            };
        }

        private static string UpdateDescriptionAttribute(string tag, string encodedDescription)
        {
            if (Regex.IsMatch(tag, "Description\\s*=\\s*(['\"]).*?\\1", RegexOptions.Singleline))
            {
                return Regex.Replace(
                    tag,
                    "Description\\s*=\\s*(['\"]).*?\\1",
                    "Description=\"" + encodedDescription + "\"",
                    RegexOptions.Singleline);
            }

            return Regex.Replace(tag, "/>$", " Description=\"" + encodedDescription + "\" />");
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
                    title = "Business Process Flows",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private static string GetWorkflowName(Entity workflow, Guid workflowId)
        {
            var name = workflow?.GetAttributeValue<string>("name");
            return string.IsNullOrWhiteSpace(name) ? workflowId.ToString("D") : name;
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static object GetValue(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out var value))
            {
                return null;
            }

            return value;
        }

        private static string GetString(Dictionary<string, object> node, string key)
        {
            var value = GetValue(node, key);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static List<object> GetList(object value)
        {
            var list = new List<object>();
            if (value == null)
            {
                return list;
            }

            var objectList = value as List<object>;
            if (objectList != null)
            {
                return objectList;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return list;
            }

            foreach (var item in enumerable)
            {
                list.Add(item);
            }

            return list;
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

        private class BpfStage
        {
            public string StageId { get; set; }
            public string Description { get; set; }
            public List<BpfLabel> Labels { get; } = new List<BpfLabel>();
            public List<BpfField> Fields { get; } = new List<BpfField>();
        }

        private class BpfField
        {
            public string StepStepId { get; set; }
            public string Description { get; set; }
            public List<BpfLabel> Labels { get; } = new List<BpfLabel>();
        }

        private class BpfLabel
        {
            public string LanguageCode { get; set; }
            public string Description { get; set; }
        }

        private class BpfStepLabelTag
        {
            public string Tag { get; set; }
            public int Start { get; set; }
            public int End { get; set; }
        }
    }
}
