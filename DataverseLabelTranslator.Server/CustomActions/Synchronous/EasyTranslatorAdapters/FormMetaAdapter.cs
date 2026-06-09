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
    public class FormMetaAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "formMeta";
        private const string PublishKindEntity = "entity";
        private const string PublishKindDashboard = "dashboard";

        private const string AttributeNameName = "name";
        private const string AttributeNameDescription = "description";

        private const int FormTypeDashboard = 0;
        private const int FormTypeAppModuleMain = 10;

        private static readonly Dictionary<int, string> FormTypeMap = new Dictionary<int, string>
        {
            { 0,  "Dashboard" },
            { 2,  "Main" },
            { 5,  "Mobile Express" },
            { 6,  "Quick View" },
            { 7,  "Quick Create" },
            { 10, "App Module Main" },
            { 11, "Interactive Experience" },
            { 12, "Card Form" }
        };

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();
            var isNone = string.IsNullOrWhiteSpace(entityName) ||
                         string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase);
            var attributeName = IsDescriptionComponent(input.component) ? AttributeNameDescription : AttributeNameName;

            if (isNone)
            {
                return LoadAll(serviceAdmin, baseLanguage, attributeName, input.solutionId);
            }

            return LoadForEntity(serviceAdmin, baseLanguage, entityName, attributeName);
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                // gridKey = "formMeta|{formid}|{name|description}|{entity|dashboard}|{entityNameOrFormId}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 5);
                var formId = ValidateGuid(parts[1], "Form formid");
                var attributeNameFromKey = parts[2];
                var publishScope = parts[3]; // "entity" or "dashboard"
                var publishTarget = parts[4]; // entity logical name or form GUID for dashboard

                ValidateAttributeName(attributeNameFromKey);

                var currentLabel = RetrieveLocLabel(context.ServiceAdmin, formId, attributeNameFromKey);
                var mergedLabels = BuildMergedLabel(currentLabel, changes);

                context.ServiceAdmin.Execute(new SetLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("systemform", formId),
                    AttributeName = attributeNameFromKey,
                    Labels = mergedLabels
                });

                output.changedRowCount++;

                if (string.Equals(publishScope, "dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    AddPublishTarget(output, seenTargets, PublishKindDashboard, publishTarget);
                }
                else if (!string.IsNullOrWhiteSpace(publishTarget))
                {
                    AddPublishTarget(output, seenTargets, PublishKindEntity, publishTarget);
                }
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var entityNames = GetPublishTargetIds(input, PublishKindEntity, false);
            var dashboardIds = GetPublishTargetIds(input, PublishKindDashboard, true);

            if (entityNames.Count > 0 || dashboardIds.Count > 0)
            {
                var xml = "<importexportxml>" +
                    (entityNames.Count > 0 ? BuildEntitySection(entityNames) : string.Empty) +
                    (dashboardIds.Count > 0 ? BuildDashboardSection(dashboardIds) : string.Empty) +
                    "</importexportxml>";

                context.ServiceAdmin.Execute(new PublishXmlRequest { ParameterXml = xml });
            }

            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in entityNames)
            {
                AddPublishTarget(output, seen, PublishKindEntity, name);
            }

            foreach (var id in dashboardIds)
            {
                AddPublishTarget(output, seen, PublishKindDashboard, id);
            }

            return output;
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            WaitAction(PublishedWaitMilliseconds);
            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in GetPublishTargetIds(input, PublishKindEntity, false))
            {
                AddPublishTarget(output, seen, PublishKindEntity, name);
            }

            foreach (var id in GetPublishTargetIds(input, PublishKindDashboard, true))
            {
                AddPublishTarget(output, seen, PublishKindDashboard, id);
            }

            return output;
        }

        // ─── Load helpers ──────────────────────────────────────────────────────────

        private static EasyTranslatorLoadOutput LoadAll(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string attributeName,
            string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "FormMeta");

            var allForms = RetrieveAllForms(serviceAdmin);
            if (allForms.Count == 0)
            {
                return EmptyOutput(baseLanguage);
            }

            HashSet<string> solutionEntities = null;
            if (solutionGuid.HasValue)
            {
                solutionEntities = GetSolutionEntityLogicalNames(serviceAdmin, solutionGuid.Value);
            }

            var entityDisplayNames = GetEntityDisplayNames(serviceAdmin, baseLanguage);

            // Group forms by objecttypecode
            var byEntity = new Dictionary<string, List<Entity>>(StringComparer.OrdinalIgnoreCase);
            foreach (var form in allForms)
            {
                var code = form.GetAttributeValue<string>("objecttypecode") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (solutionEntities != null && !solutionEntities.Contains(code))
                {
                    continue;
                }

                if (!byEntity.TryGetValue(code, out var list))
                {
                    list = new List<Entity>();
                    byEntity[code] = list;
                }

                list.Add(form);
            }

            var entityKeys = new List<string>(byEntity.Keys);
            entityKeys.Sort(StringComparer.OrdinalIgnoreCase);

            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var entityKey in entityKeys)
            {
                entityDisplayNames.TryGetValue(entityKey, out var displayName);
                var parentSchemaName = string.IsNullOrWhiteSpace(displayName)
                    ? entityKey
                    : displayName + " (" + entityKey + ")";

                var parentRow = new EasyTranslatorGridRowOutput
                {
                    Recid = "formMeta:entity:" + entityKey,
                    GridKey = BuildGridKey(TranslatorType, entityKey),
                    SchemaName = parentSchemaName,
                    RowType = "formMeta.entity",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var entityForms = byEntity[entityKey];
                entityForms.Sort((a, b) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        a.GetAttributeValue<string>("name") ?? string.Empty,
                        b.GetAttributeValue<string>("name") ?? string.Empty));

                var children = new List<EasyTranslatorGridRowOutput>();
                foreach (var form in entityForms)
                {
                    var childRow = BuildFormRow(serviceAdmin, form, attributeName, entityKey);
                    if (childRow != null)
                    {
                        children.Add(childRow);
                    }
                }

                if (children.Count > 0)
                {
                    parentRow.Children = children;
                    rows.Add(parentRow);
                }
            }

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Form Metadata",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorLoadOutput LoadForEntity(
            IOrganizationService serviceAdmin,
            int baseLanguage,
            string entityName,
            string attributeName)
        {
            var forms = RetrieveEntityForms(serviceAdmin, entityName);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var form in forms)
            {
                var row = BuildFormRow(serviceAdmin, form, attributeName, entityName);
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
                    title = "Form Metadata",
                    rows = rows
                }
            };
        }

        private static EasyTranslatorGridRowOutput BuildFormRow(
            IOrganizationService serviceAdmin,
            Entity form,
            string attributeName,
            string entityName)
        {
            var id = form.Contains("formid") ? form.GetAttributeValue<Guid>("formid") : form.Id;
            if (id == Guid.Empty)
            {
                return null;
            }

            var rawName = form.GetAttributeValue<string>("name") ?? string.Empty;
            var formType = GetOptionValue(form, "type") ?? 0;
            var typeName = GetFormTypeName(formType);
            var schemaName = string.IsNullOrWhiteSpace(rawName)
                ? typeName
                : rawName + " (" + typeName + ")";
            var isDashboardForm = formType == FormTypeDashboard || formType == FormTypeAppModuleMain;

            // gridKey includes publish scope and target: formMeta|{id}|{attr}|{entity|dashboard}|{entityName|formId}
            var publishScope = isDashboardForm ? "dashboard" : "entity";
            var publishTarget = isDashboardForm ? id.ToString("D") : entityName;

            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "formMeta:" + id.ToString("D"),
                GridKey = BuildGridKey(TranslatorType, id.ToString("D"), attributeName, publishScope, publishTarget),
                SchemaName = schemaName,
                RowType = "formMeta.row",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true, location = isDashboardForm ? "dashboard" : entityName }
            };

            var label = RetrieveLocLabel(serviceAdmin, id, attributeName);
            AddLabelValues(row, label);
            return row;
        }

        // ─── Data retrieval ────────────────────────────────────────────────────────

        private static List<Entity> RetrieveAllForms(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "type", "name", "objecttypecode")
            };
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);
            query.Criteria.AddCondition("formactivationstate", ConditionOperator.Equal, 1);

            return ToList(serviceAdmin.RetrieveMultiple(query));
        }

        private static List<Entity> RetrieveEntityForms(IOrganizationService serviceAdmin, string entityName)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "type", "name", "objecttypecode")
            };
            query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, entityName);
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);
            query.Criteria.AddCondition("formactivationstate", ConditionOperator.Equal, 1);

            return ToList(serviceAdmin.RetrieveMultiple(query));
        }

        private static Label RetrieveLocLabel(IOrganizationService serviceAdmin, Guid formId, string attributeName)
        {
            var response = (RetrieveLocLabelsResponse)serviceAdmin.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("systemform", formId),
                AttributeName = attributeName,
                IncludeUnpublished = true
            });

            return response?.Label ?? new Label();
        }

        private static Dictionary<string, string> GetEntityDisplayNames(IOrganizationService serviceAdmin, int baseLanguage)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entity in response.EntityMetadata)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName))
                {
                    map[entity.LogicalName] = GetBaseLanguageLabel(entity.DisplayName, baseLanguage);
                }
            }

            return map;
        }

        private static HashSet<string> GetSolutionEntityLogicalNames(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);

            var idToName = new Dictionary<Guid, string>();
            foreach (var entity in response.EntityMetadata)
            {
                if (!string.IsNullOrWhiteSpace(entity.LogicalName) && entity.MetadataId.HasValue)
                {
                    idToName[entity.MetadataId.Value] = entity.LogicalName;
                }
            }

            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in ToList(serviceAdmin.RetrieveMultiple(componentQuery)))
            {
                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (idToName.TryGetValue(objectId, out var logicalName))
                {
                    names.Add(logicalName);
                }
            }

            return names;
        }

        private static string GetBaseLanguageLabel(Microsoft.Xrm.Sdk.Label label, int baseLanguage)
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

        // ─── Utilities ─────────────────────────────────────────────────────────────

        private static string GetFormTypeName(int formType)
        {
            return FormTypeMap.TryGetValue(formType, out var name) ? name : "Type " + formType;
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private static void ValidateAttributeName(string attributeName)
        {
            if (!string.Equals(attributeName, AttributeNameName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(attributeName, AttributeNameDescription, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException(
                    $"FormMeta attributeName must be '{AttributeNameName}' or '{AttributeNameDescription}'.");
            }
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

        private static string BuildEntitySection(List<string> entityNames)
        {
            var sb = new StringBuilder("<entities>");
            foreach (var name in entityNames)
            {
                sb.Append("<entity>").Append(name).Append("</entity>");
            }

            return sb.Append("</entities>").ToString();
        }

        private static string BuildDashboardSection(List<string> dashboardIds)
        {
            var sb = new StringBuilder("<dashboards>");
            foreach (var id in dashboardIds)
            {
                sb.Append("<dashboard>{").Append(id).Append("}</dashboard>");
            }

            return sb.Append("</dashboards>").ToString();
        }

        private static EasyTranslatorLoadOutput EmptyOutput(int baseLanguage)
        {
            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Form Metadata",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }
    }
}
