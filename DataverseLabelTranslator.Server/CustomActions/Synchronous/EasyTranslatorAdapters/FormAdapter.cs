using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class FormAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "forms";
        private const string PublishKind = "entity";
        private const string OperationRemoveOverriddenCellLabels = "RemoveOverriddenCellLabels";

        private static readonly int[] SupportedFormTypes = { 2, 5, 6, 7, 11, 12 };
        private static readonly Dictionary<int, string> FormTypeMap = new Dictionary<int, string>
        {
            { 2, "Main" },
            { 5, "Mobile Express" },
            { 6, "Quick View" },
            { 7, "Quick Create" },
            { 11, "Interactive Experience" },
            { 12, "Card" }
        };

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var entityName = NormalizeEntityName(input.entityName);
            if (string.IsNullOrWhiteSpace(entityName))
            {
                return EmptyOutput(baseLanguage);
            }

            var languages = RetrieveAvailableLanguages(context.ServiceAdmin);
            if (!languages.Contains(baseLanguage))
            {
                languages.Insert(0, baseLanguage);
            }

            var formsByLanguage = RetrieveFormsByLanguage(context, entityName, languages);
            if (!formsByLanguage.TryGetValue(baseLanguage, out var baseForms) || baseForms.Count == 0)
            {
                baseForms = formsByLanguage.Values.SelectMany(forms => forms).GroupBy(form => form.FormId).Select(group => group.First()).ToList();
            }

            baseForms.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));

            var rows = new List<EasyTranslatorGridRowOutput>();
            foreach (var form in baseForms)
            {
                var parent = BuildFormParentRow(form, entityName);
                var children = BuildFormNodeRows(form, formsByLanguage, entityName);
                if (children.Count == 0)
                {
                    continue;
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
                    title = "Forms",
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            if (string.Equals(input.operation, OperationRemoveOverriddenCellLabels, StringComparison.OrdinalIgnoreCase))
            {
                return RemoveOverriddenCellLabels(context, input);
            }

            var updatesByForm = BuildFormUpdates(input);
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var formUpdate in updatesByForm.Values)
            {
                var changed = SaveForm(context, formUpdate);
                if (!changed)
                {
                    continue;
                }

                output.changedRowCount += formUpdate.Nodes.Count;
                AddPublishTarget(output, seenTargets, PublishKind, formUpdate.EntityName);
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

        private static EasyTranslatorGridRowOutput BuildFormParentRow(FormInfo form, string entityName)
        {
            return new EasyTranslatorGridRowOutput
            {
                Recid = "forms:" + form.FormId.ToString("D"),
                GridKey = BuildGridKey(TranslatorType, form.FormId.ToString("D"), entityName),
                SchemaName = form.DisplayName,
                RowType = "forms.form",
                IsEditable = false,
                IsTranslatable = false,
                Ai = new EasyTranslatorAiOutput { include = false }
            };
        }

        private static List<EasyTranslatorGridRowOutput> BuildFormNodeRows(
            FormInfo baseForm,
            Dictionary<int, List<FormInfo>> formsByLanguage,
            string entityName)
        {
            var rows = new List<EasyTranslatorGridRowOutput>();
            foreach (var node in GetTranslatableNodes(baseForm.Document))
            {
                var nodeId = GetAttributeValue(node, "id");
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                var row = new EasyTranslatorGridRowOutput
                {
                    Recid = "forms:" + baseForm.FormId.ToString("D") + ":" + nodeId,
                    GridKey = BuildGridKey(TranslatorType, baseForm.FormId.ToString("D"), nodeId, entityName),
                    SchemaName = GetNodeDisplayName(node),
                    RowType = "forms.node",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = baseForm.DisplayName }
                };

                AddNodeLabels(row, node);
                foreach (var formsForLanguage in formsByLanguage.Values)
                {
                    var localizedForm = formsForLanguage.FirstOrDefault(form => form.FormId == baseForm.FormId);
                    var localizedNode = localizedForm == null ? null : FindNodeById(localizedForm.Document, nodeId);
                    AddNodeLabels(row, localizedNode);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static Dictionary<Guid, FormUpdate> BuildFormUpdates(EasyTranslatorSaveInput input)
        {
            var updatesByForm = new Dictionary<Guid, FormUpdate>();
            var fallbackEntityName = NormalizeEntityName(input.entityName);

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                // gridKey = "forms|{formid}|{formXmlNodeId}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var formId = ValidateGuid(parts[1], "Form formid");
                var nodeId = parts[2];
                var entityName = string.IsNullOrWhiteSpace(parts[3]) ? fallbackEntityName : parts[3];

                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    throw new InvalidPluginExecutionException("Form node id is required.");
                }

                if (!updatesByForm.TryGetValue(formId, out var formUpdate))
                {
                    formUpdate = new FormUpdate
                    {
                        FormId = formId,
                        EntityName = entityName
                    };
                    updatesByForm[formId] = formUpdate;
                }

                if (!formUpdate.Nodes.TryGetValue(nodeId, out var nodeUpdate))
                {
                    nodeUpdate = new NodeUpdate { NodeId = nodeId };
                    formUpdate.Nodes[nodeId] = nodeUpdate;
                }

                foreach (var change in changes)
                {
                    nodeUpdate.Labels[change.LanguageCode.ToString()] = change.Label ?? string.Empty;
                }
            }

            return updatesByForm;
        }

        private static bool SaveForm(EasyTranslatorRuntimeContext context, FormUpdate formUpdate)
        {
            return RunAsUserLanguage(context, GetBaseLanguage(context.ServiceAdmin), () =>
            {
                var form = RetrieveForm(context.Service, formUpdate.FormId);
                var originalXml = form.GetAttributeValue<string>("formxml") ?? string.Empty;
                var document = ParseFormXml(originalXml);

                foreach (var nodeUpdate in formUpdate.Nodes.Values)
                {
                    var node = FindNodeById(document, nodeUpdate.NodeId);
                    if (node == null)
                    {
                        throw new InvalidPluginExecutionException("Form XML node " + nodeUpdate.NodeId + " was not found.");
                    }

                    ApplyLabelUpdates(node, nodeUpdate.Labels);
                }

                var updatedXml = SerializeFormXml(document);
                var changed = !string.Equals(updatedXml, originalXml, StringComparison.Ordinal);
                if (changed)
                {
                    UpdateFormXml(context.ServiceAdmin, formUpdate.FormId, updatedXml);
                }

                return changed;
            });
        }

        private static EasyTranslatorSaveOutput RemoveOverriddenCellLabels(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var entityName = NormalizeEntityName(input.entityName);
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new InvalidPluginExecutionException("Forms entityName is required.");
            }

            if (string.IsNullOrWhiteSpace(input.formId) || !Guid.TryParse(input.formId, out var formId))
            {
                throw new InvalidPluginExecutionException("Forms formId is required.");
            }

            var changed = RunAsUserLanguage(context, GetBaseLanguage(context.ServiceAdmin), () =>
            {
                var form = RetrieveForm(context.Service, formId);
                var originalXml = form.GetAttributeValue<string>("formxml") ?? string.Empty;
                var document = ParseFormXml(originalXml);

                foreach (var cell in document.Descendants().Where(element => IsElementName(element, "cell")).ToList())
                {
                    var control = cell.Descendants().FirstOrDefault(element => IsElementName(element, "control"));
                    if (control == null || string.IsNullOrWhiteSpace(GetAttributeValue(control, "datafieldname")))
                    {
                        continue;
                    }

                    cell.SetAttributeValue("id", Guid.NewGuid().ToString("D"));
                    foreach (var labels in cell.Elements().Where(element => IsElementName(element, "labels")))
                    {
                        labels.Elements().Where(element => IsElementName(element, "label")).Remove();
                    }
                }

                var updatedXml = SerializeFormXml(document);
                var hasChanges = !string.Equals(updatedXml, originalXml, StringComparison.Ordinal);
                if (hasChanges)
                {
                    UpdateFormXml(context.ServiceAdmin, formId, updatedXml);
                }

                return hasChanges;
            });

            var output = new EasyTranslatorSaveOutput();
            if (changed)
            {
                output.changedRowCount = 1;
                AddPublishTarget(output, new HashSet<string>(StringComparer.OrdinalIgnoreCase), PublishKind, entityName);
            }

            return output;
        }

        private static Dictionary<int, List<FormInfo>> RetrieveFormsByLanguage(
            EasyTranslatorRuntimeContext context,
            string entityName,
            List<int> languages)
        {
            var formsByLanguage = new Dictionary<int, List<FormInfo>>();
            RunWithUserLanguageRestore(context, () =>
            {
                foreach (var language in languages)
                {
                    SetUserLanguage(context.ServiceAdmin, context.PluginContext.UserId, language);
                    formsByLanguage[language] = RetrieveEntityForms(context.Service, entityName, language);
                }

                return true;
            });

            return formsByLanguage;
        }

        private static List<FormInfo> RetrieveEntityForms(IOrganizationService service, string entityName, int languageCode)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "type", "name", "objecttypecode", "formxml")
            };
            query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, entityName);
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);
            query.Criteria.AddCondition("formactivationstate", ConditionOperator.Equal, 1);
            query.Criteria.AddCondition("type", ConditionOperator.In, SupportedFormTypes.Cast<object>().ToArray());
            query.Orders.Add(new OrderExpression("name", OrderType.Ascending));

            var forms = new List<FormInfo>();
            foreach (var entity in RetrieveAll(service, query))
            {
                var formId = entity.Contains("formid") ? entity.GetAttributeValue<Guid>("formid") : entity.Id;
                if (formId == Guid.Empty)
                {
                    continue;
                }

                var formType = GetOptionValue(entity, "type") ?? 0;
                forms.Add(new FormInfo
                {
                    FormId = formId,
                    Name = entity.GetAttributeValue<string>("name") ?? string.Empty,
                    FormType = formType,
                    LanguageCode = languageCode,
                    Document = ParseFormXml(entity.GetAttributeValue<string>("formxml") ?? string.Empty)
                });
            }

            return forms;
        }

        private static Entity RetrieveForm(IOrganizationService service, Guid formId)
        {
            return service.Retrieve("systemform", formId, new ColumnSet("formid", "name", "type", "formxml", "objecttypecode"));
        }

        private static void UpdateFormXml(IOrganizationService serviceAdmin, Guid formId, string formXml)
        {
            var update = new Entity("systemform", formId);
            update["formxml"] = formXml;
            serviceAdmin.Update(update);
        }

        private static List<int> RetrieveAvailableLanguages(IOrganizationService serviceAdmin)
        {
            var response = (RetrieveAvailableLanguagesResponse)serviceAdmin.Execute(new RetrieveAvailableLanguagesRequest());
            return response.LocaleIds == null ? new List<int>() : response.LocaleIds.ToList();
        }

        private static List<Entity> RetrieveAll(IOrganizationService service, QueryExpression query)
        {
            var results = new List<Entity>();
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1
            };

            while (true)
            {
                var page = service.RetrieveMultiple(query);
                results.AddRange(ToList(page));

                if (!page.MoreRecords)
                {
                    return results;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        private static List<XElement> GetTranslatableNodes(XDocument document)
        {
            return document
                .Descendants()
                .Where(element =>
                    !string.IsNullOrWhiteSpace(GetAttributeValue(element, "id")) &&
                    element.Elements().Any(child => IsElementName(child, "labels")) &&
                    element.Descendants().Any(child => IsElementName(child, "control")))
                .ToList();
        }

        private static XElement FindNodeById(XDocument document, string nodeId)
        {
            if (document == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return null;
            }

            return document.Descendants().FirstOrDefault(element =>
                string.Equals(GetAttributeValue(element, "id"), nodeId, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddNodeLabels(EasyTranslatorGridRowOutput row, XElement node)
        {
            var labels = node?.Elements().FirstOrDefault(element => IsElementName(element, "labels"));
            if (labels == null)
            {
                return;
            }

            foreach (var label in labels.Elements().Where(element => IsElementName(element, "label")))
            {
                var languageCode = GetAttributeValue(label, "languagecode");
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    continue;
                }

                row.SetLanguageValue(languageCode, GetAttributeValue(label, "description"));
            }
        }

        private static void ApplyLabelUpdates(XElement node, Dictionary<string, string> labelsByLanguage)
        {
            var labels = node.Elements().FirstOrDefault(element => IsElementName(element, "labels"));
            if (labels == null)
            {
                labels = new XElement("labels");
                node.AddFirst(labels);
            }

            foreach (var update in labelsByLanguage)
            {
                var existingLabels = labels
                    .Elements()
                    .Where(element =>
                        IsElementName(element, "label") &&
                        string.Equals(GetAttributeValue(element, "languagecode"), update.Key, StringComparison.Ordinal))
                    .ToList();

                var first = existingLabels.FirstOrDefault();
                foreach (var duplicate in existingLabels.Skip(1))
                {
                    duplicate.Remove();
                }

                if (first == null)
                {
                    labels.Add(new XElement(
                        "label",
                        new XAttribute("description", update.Value ?? string.Empty),
                        new XAttribute("languagecode", update.Key)));
                }
                else
                {
                    first.SetAttributeValue("description", update.Value ?? string.Empty);
                }
            }
        }

        private static T RunAsUserLanguage<T>(EasyTranslatorRuntimeContext context, int languageCode, Func<T> action)
        {
            return RunWithUserLanguageRestore(context, () =>
            {
                SetUserLanguage(context.ServiceAdmin, context.PluginContext.UserId, languageCode);
                return action();
            });
        }

        private static T RunWithUserLanguageRestore<T>(EasyTranslatorRuntimeContext context, Func<T> action)
        {
            var userId = context.PluginContext.UserId;
            var settings = context.ServiceAdmin.Retrieve("usersettings", userId, new ColumnSet("uilanguageid", "helplanguageid"));
            var originalUiLanguage = settings.GetAttributeValue<int?>("uilanguageid");
            var originalHelpLanguage = settings.GetAttributeValue<int?>("helplanguageid");

            try
            {
                return action();
            }
            finally
            {
                var restore = new Entity("usersettings", userId);
                if (originalUiLanguage.HasValue)
                {
                    restore["uilanguageid"] = originalUiLanguage.Value;
                }

                if (originalHelpLanguage.HasValue)
                {
                    restore["helplanguageid"] = originalHelpLanguage.Value;
                }

                context.ServiceAdmin.Update(restore);
            }
        }

        private static void SetUserLanguage(IOrganizationService serviceAdmin, Guid userId, int languageCode)
        {
            var update = new Entity("usersettings", userId);
            update["uilanguageid"] = languageCode;
            update["helplanguageid"] = languageCode;
            serviceAdmin.Update(update);
        }

        private static XDocument ParseFormXml(string formXml)
        {
            if (string.IsNullOrWhiteSpace(formXml))
            {
                return new XDocument(new XElement("form"));
            }

            return XDocument.Parse(formXml, LoadOptions.PreserveWhitespace);
        }

        private static string SerializeFormXml(XDocument document)
        {
            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static string GetNodeDisplayName(XElement node)
        {
            var name = GetAttributeValue(node, "name");
            return string.IsNullOrWhiteSpace(name) ? node.Name.LocalName : name;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var attribute = element.Attributes().FirstOrDefault(item =>
                string.Equals(item.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase));
            return attribute == null ? string.Empty : attribute.Value;
        }

        private static bool IsElementName(XElement element, string name)
        {
            return element != null && string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEntityName(string entityName)
        {
            var normalized = (entityName ?? string.Empty).Trim().ToLowerInvariant();
            return string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : normalized;
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
                    title = "Forms",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private class FormInfo
        {
            public Guid FormId { get; set; }
            public string Name { get; set; }
            public int FormType { get; set; }
            public int LanguageCode { get; set; }
            public XDocument Document { get; set; }

            public string DisplayName
            {
                get
                {
                    var type = FormTypeMap.TryGetValue(FormType, out var name) ? name : "Type " + FormType;
                    return string.IsNullOrWhiteSpace(Name) ? type : Name + " [" + type + "]";
                }
            }
        }

        private class FormUpdate
        {
            public Guid FormId { get; set; }
            public string EntityName { get; set; }
            public Dictionary<string, NodeUpdate> Nodes { get; } = new Dictionary<string, NodeUpdate>(StringComparer.OrdinalIgnoreCase);
        }

        private class NodeUpdate
        {
            public string NodeId { get; set; }
            public Dictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
