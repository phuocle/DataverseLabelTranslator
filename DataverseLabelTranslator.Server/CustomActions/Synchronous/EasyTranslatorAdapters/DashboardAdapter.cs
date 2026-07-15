using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class DashboardAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int DashboardComponentType = 60;
        private const int DashboardTypeSystem = 0;
        private const int DashboardTypeInteractive = 10;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "dashboards";
        private const string PublishKind = "dashboard";
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var solutionId = ParseSolutionId(input.solutionId, "Dashboard");
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var dashboards = solutionId.HasValue
                ? RetrieveDashboardsBySolution(serviceAdmin, solutionId.Value)
                : RetrieveAllDashboards(serviceAdmin);
            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var dashboard in dashboards)
            {
                if (!IsDashboardCandidate(dashboard))
                {
                    continue;
                }

                rows.Add(BuildDashboardRow(serviceAdmin, dashboard, baseLanguage));
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Dashboards",
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

                var parts = ParseGridKey(row.gridKey, TranslatorType, 3);
                if (!string.Equals(parts[2], "name", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException("Dashboard gridKey must target the name component.");
                }

                var dashboardId = ValidateDashboardId(parts[1]);
                var currentLabel = RetrieveDashboardLabel(context.ServiceAdmin, dashboardId);
                var mergedLabel = BuildMergedLabel(currentLabel, changes);

                context.ServiceAdmin.Execute(new SetLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("systemform", dashboardId),
                    AttributeName = "name",
                    Labels = mergedLabel
                });

                output.changedRowCount++;
                AddPublishTarget(output, seenTargets, PublishKind, dashboardId.ToString("D"));
            }

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var dashboardIds = GetPublishTargetIds(input, PublishKind, true);

            if (dashboardIds.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(dashboardIds)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return ToPublishOutput(PublishKind, dashboardIds);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            Wait(PublishedWaitMilliseconds);
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, true));
        }

        private static List<Entity> RetrieveAllDashboards(IOrganizationService serviceAdmin)
        {
            return Helper.RetrieveAll(serviceAdmin, CreateDashboardQuery());
        }

        private static List<Entity> RetrieveDashboardsBySolution(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var ids = GetSolutionDashboardIds(serviceAdmin, solutionId);
            if (ids.Count == 0)
            {
                return new List<Entity>();
            }

            var query = CreateDashboardQuery();
            var values = new object[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                values[i] = ids[i];
            }

            query.Criteria.AddCondition(new ConditionExpression("formid", ConditionOperator.In, values));
            return Helper.RetrieveAll(serviceAdmin, query);
        }

        private static QueryExpression CreateDashboardQuery()
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("formid", "name", "type", "objecttypecode", "formactivationstate", "iscustomizable")
            };
            query.Criteria.AddCondition("formactivationstate", ConditionOperator.Equal, 1);
            query.Criteria.AddCondition("iscustomizable", ConditionOperator.Equal, true);
            var typeFilter = new FilterExpression(LogicalOperator.Or);
            typeFilter.AddCondition("type", ConditionOperator.Equal, DashboardTypeSystem);
            typeFilter.AddCondition("type", ConditionOperator.Equal, DashboardTypeInteractive);
            query.Criteria.AddFilter(typeFilter);

            return query;
        }

        private static List<Guid> GetSolutionDashboardIds(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, DashboardComponentType);

            var ids = new List<Guid>();
            foreach (var component in Helper.RetrieveAll(serviceAdmin, query))
            {
                if (!component.Contains("objectid"))
                {
                    continue;
                }

                var id = component.GetAttributeValue<Guid>("objectid");
                if (id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static bool IsDashboardCandidate(Entity dashboard)
        {
            var activeState = GetOptionValue(dashboard, "formactivationstate");
            if (activeState.HasValue && activeState.Value != 1)
            {
                return false;
            }

            var customizable = GetManagedBooleanValue(dashboard, "iscustomizable");
            if (customizable.HasValue && !customizable.Value)
            {
                return false;
            }

            var type = GetOptionValue(dashboard, "type");
            return !type.HasValue || type.Value == DashboardTypeSystem || type.Value == DashboardTypeInteractive;
        }

        private static EasyTranslatorGridRowOutput BuildDashboardRow(IOrganizationService serviceAdmin, Entity dashboard, int baseLanguage)
        {
            var id = dashboard.Id;
            if (dashboard.Contains("formid"))
            {
                id = dashboard.GetAttributeValue<Guid>("formid");
            }

            var row = new EasyTranslatorGridRowOutput
            {
                Recid = "dashboard:" + id.ToString("D"),
                GridKey = BuildGridKey(TranslatorType, id.ToString("D"), "name"),
                SchemaName = dashboard.GetAttributeValue<string>("name") ?? GetDashboardBaseLabel(serviceAdmin, dashboard, id, baseLanguage),
                RowType = "dashboard.row",
                IsEditable = true,
                IsTranslatable = true,
                Ai = new EasyTranslatorAiOutput { include = true }
            };
            AddLabelValues(row, RetrieveDashboardLabel(serviceAdmin, id));

            return row;
        }

        private static string GetDashboardBaseLabel(IOrganizationService serviceAdmin, Entity dashboard, Guid dashboardId, int baseLanguage)
        {
            var label = RetrieveDashboardLabel(serviceAdmin, dashboardId);
            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel != null && localizedLabel.LanguageCode == baseLanguage && !string.IsNullOrEmpty(localizedLabel.Label))
                {
                    return localizedLabel.Label;
                }
            }

            return dashboard.GetAttributeValue<string>("name") ?? dashboardId.ToString("D");
        }

        private static string GetRowBaseLabel(EasyTranslatorGridRowOutput row, int baseLanguage)
        {
            var key = baseLanguage.ToString();
            return row.ContainsKey(key) && row[key] != null ? Convert.ToString(row[key]) : row.SchemaName;
        }

        private static Label RetrieveDashboardLabel(IOrganizationService serviceAdmin, Guid dashboardId)
        {
            var response = (RetrieveLocLabelsResponse)serviceAdmin.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("systemform", dashboardId),
                AttributeName = "name",
                IncludeUnpublished = true
            });

            return response?.Label ?? new Label();
        }

        private static Guid ValidateDashboardId(string dashboardId)
        {
            if (string.IsNullOrWhiteSpace(dashboardId) || !Guid.TryParse(dashboardId, out var id))
            {
                throw new InvalidPluginExecutionException("Dashboard dashboardId must be a GUID.");
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

        private static string BuildPublishXml(List<string> dashboardIds)
        {
            var sb = new StringBuilder();
            foreach (var dashboardId in dashboardIds)
            {
                sb.Append("<dashboard>{").Append(dashboardId).Append("}</dashboard>");
            }

            return string.Concat("<importexportxml><dashboards>", sb.ToString(), "</dashboards></importexportxml>");
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }
    }
}
