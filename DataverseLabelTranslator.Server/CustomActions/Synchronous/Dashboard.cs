using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class Dashboard : ICustomAction
    {
        private const int DashboardComponentType = 60;
        private const int DashboardTypeSystem = 0;
        private const int DashboardTypeInteractive = 10;
        private const int PublishingWaitMilliseconds = 10000;
        private const int PublishedWaitMilliseconds = 10000;
        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<LoadingDashboardInput>(json);
            var solutionId = ParseSolutionId(input.solutionId);
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var dashboards = solutionId.HasValue
                ? RetrieveDashboardsBySolution(serviceAdmin, solutionId.Value)
                : RetrieveAllDashboards(serviceAdmin);
            var outputs = new List<DashboardMetadataOutput>();

            foreach (var dashboard in dashboards)
            {
                if (!IsDashboardCandidate(dashboard))
                {
                    continue;
                }

                outputs.Add(BuildDashboardOutput(serviceAdmin, dashboard));
            }

            outputs.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(GetBaseLabel(a, baseLanguage), GetBaseLabel(b, baseLanguage)));
            return new LoadingDashboardOutput
            {
                baseLanguage = baseLanguage.ToString(),
                dashboards = outputs
            };
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<SavingDashboardInput>(json);
            var dashboardIds = new List<string>();
            var dashboardIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (input.dashboardUpdates != null)
            {
                foreach (var update in input.dashboardUpdates)
                {
                    var changes = GetValidLabelChanges(update?.labels);
                    if (changes.Count == 0)
                    {
                        continue;
                    }

                    var dashboardId = ValidateDashboardId(update.dashboardId);
                    var currentLabel = RetrieveDashboardLabel(serviceAdmin, dashboardId);
                    var mergedLabel = BuildMergedLabel(currentLabel, changes);

                    serviceAdmin.Execute(new SetLocLabelsRequest
                    {
                        EntityMoniker = new EntityReference("systemform", dashboardId),
                        AttributeName = "name",
                        Labels = mergedLabel
                    });

                    var id = dashboardId.ToString("D");
                    if (dashboardIdSet.Add(id))
                    {
                        dashboardIds.Add(id);
                    }
                }
            }

            return new SavingDashboardOutput { dashboardIds = dashboardIds };
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishingDashboardInput>(json);
            var dashboardIds = GetValidDashboardIds(input.dashboardIds);

            if (dashboardIds.Count > 0)
            {
                serviceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildPublishXml(dashboardIds)
                });
            }
            else
            {
                Wait(PublishingWaitMilliseconds);
            }

            return new PublishingDashboardOutput { dashboardIds = dashboardIds };
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<PublishedDashboardInput>(json);
            Wait(PublishedWaitMilliseconds);
            return new PublishedDashboardOutput { dashboardIds = GetValidDashboardIds(input.dashboardIds) };
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<OtherDashboardInput>(json);
            if (string.IsNullOrWhiteSpace(input.operation))
            {
                throw new InvalidPluginExecutionException("Dashboard Other operation is required.");
            }

            return new OtherDashboardOutput { operation = input.operation };
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing Dashboard input.");
            }

            return input;
        }

        private static Guid? ParseSolutionId(string solutionId)
        {
            if (string.IsNullOrWhiteSpace(solutionId) || string.Equals(solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionId, out var id))
            {
                throw new InvalidPluginExecutionException("Dashboard solutionId must be a GUID or all.");
            }

            return id;
        }

        private static int GetBaseLanguage(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("organization")
            {
                ColumnSet = new ColumnSet("languagecode"),
                TopCount = 1
            };
            var result = serviceAdmin.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                throw new InvalidPluginExecutionException("Could not retrieve organization base language.");
            }

            var languageCode = result.Entities[0].GetAttributeValue<int>("languagecode");
            if (languageCode <= 0)
            {
                throw new InvalidPluginExecutionException("Could not retrieve organization base language.");
            }

            return languageCode;
        }

        private static List<Entity> RetrieveAllDashboards(IOrganizationService serviceAdmin)
        {
            var query = CreateDashboardQuery();
            var result = serviceAdmin.RetrieveMultiple(query);
            return ToList(result);
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

            var result = serviceAdmin.RetrieveMultiple(query);
            return ToList(result);
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
            var result = serviceAdmin.RetrieveMultiple(query);
            foreach (var component in result.Entities)
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

        private static List<Entity> ToList(EntityCollection collection)
        {
            var list = new List<Entity>();
            if (collection == null)
            {
                return list;
            }

            foreach (var entity in collection.Entities)
            {
                list.Add(entity);
            }

            return list;
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
            if (!type.HasValue)
            {
                return true;
            }

            return type.Value == DashboardTypeSystem || type.Value == DashboardTypeInteractive;
        }

        private static int? GetOptionValue(Entity entity, string attributeName)
        {
            if (entity == null || !entity.Contains(attributeName) || entity[attributeName] == null)
            {
                return null;
            }

            var option = entity[attributeName] as OptionSetValue;
            if (option != null)
            {
                return option.Value;
            }

            if (entity[attributeName] is int)
            {
                return (int)entity[attributeName];
            }

            return null;
        }

        private static bool? GetManagedBooleanValue(Entity entity, string attributeName)
        {
            if (entity == null || !entity.Contains(attributeName) || entity[attributeName] == null)
            {
                return null;
            }

            var managed = entity[attributeName] as BooleanManagedProperty;
            if (managed != null)
            {
                return managed.Value;
            }

            if (entity[attributeName] is bool)
            {
                return (bool)entity[attributeName];
            }

            return null;
        }

        private static DashboardMetadataOutput BuildDashboardOutput(IOrganizationService serviceAdmin, Entity dashboard)
        {
            var id = dashboard.Id;
            if (dashboard.Contains("formid"))
            {
                id = dashboard.GetAttributeValue<Guid>("formid");
            }

            return new DashboardMetadataOutput
            {
                formid = id.ToString("D"),
                name = dashboard.GetAttributeValue<string>("name"),
                type = GetOptionValue(dashboard, "type"),
                objecttypecode = dashboard.GetAttributeValue<string>("objecttypecode"),
                Label = BuildLabelOutput(RetrieveDashboardLabel(serviceAdmin, id))
            };
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

        private static DashboardLabelOutput BuildLabelOutput(Label label)
        {
            var output = new DashboardLabelOutput();
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

                output.LocalizedLabels.Add(new DashboardLocalizedLabelOutput
                {
                    LanguageCode = localizedLabel.LanguageCode,
                    Label = localizedLabel.Label
                });
            }

            return output;
        }

        private static List<DashboardLabelChange> GetValidLabelChanges(List<DashboardLocalizedLabelInput> labels)
        {
            var changes = new List<DashboardLabelChange>();
            if (labels == null)
            {
                return changes;
            }

            foreach (var label in labels)
            {
                if (label == null || !int.TryParse(Convert.ToString(label.LanguageCode), out var languageCode))
                {
                    continue;
                }

                changes.Add(new DashboardLabelChange
                {
                    LanguageCode = languageCode,
                    Label = label.Label ?? string.Empty
                });
            }

            return changes;
        }

        private static Guid ValidateDashboardId(string dashboardId)
        {
            if (string.IsNullOrWhiteSpace(dashboardId) || !Guid.TryParse(dashboardId, out var id))
            {
                throw new InvalidPluginExecutionException("Dashboard dashboardId must be a GUID.");
            }

            return id;
        }

        private static LocalizedLabel[] BuildMergedLabel(Label currentLabel, List<DashboardLabelChange> changes)
        {
            var values = new Dictionary<int, string>();
            var languageOrder = new List<int>();

            if (currentLabel != null && currentLabel.LocalizedLabels != null)
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

        private static List<string> GetValidDashboardIds(List<string> dashboardIds)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (dashboardIds == null)
            {
                return ids;
            }

            foreach (var id in dashboardIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (Guid.TryParse(id, out _) && seen.Add(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
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

        private static string GetBaseLabel(DashboardMetadataOutput dashboard, int baseLanguage)
        {
            if (dashboard?.Label?.LocalizedLabels != null)
            {
                foreach (var label in dashboard.Label.LocalizedLabels)
                {
                    if (label.LanguageCode == baseLanguage && !string.IsNullOrEmpty(label.Label))
                    {
                        return label.Label;
                    }
                }
            }

            return dashboard?.name ?? dashboard?.formid ?? string.Empty;
        }

        private static void Wait(int milliseconds)
        {
            WaitAction(milliseconds);
        }

        private class DashboardLabelChange
        {
            public int LanguageCode { get; set; }
            public string Label { get; set; }
        }
    }

    public class LoadingDashboardInput : CustomActionInput
    {
        public string solutionId { get; set; }
    }

    public class SavingDashboardInput : CustomActionInput
    {
        public List<DashboardUpdateInput> dashboardUpdates { get; set; } = new List<DashboardUpdateInput>();
    }

    public class PublishingDashboardInput : CustomActionInput
    {
        public List<string> dashboardIds { get; set; } = new List<string>();
    }

    public class PublishedDashboardInput : CustomActionInput
    {
        public List<string> dashboardIds { get; set; } = new List<string>();
    }

    public class OtherDashboardInput : CustomActionInput
    {
    }

    public class LoadingDashboardOutput
    {
        public string baseLanguage { get; set; }
        public List<DashboardMetadataOutput> dashboards { get; set; } = new List<DashboardMetadataOutput>();
    }

    public class SavingDashboardOutput
    {
        public List<string> dashboardIds { get; set; } = new List<string>();
    }

    public class PublishingDashboardOutput
    {
        public List<string> dashboardIds { get; set; } = new List<string>();
    }

    public class PublishedDashboardOutput
    {
        public List<string> dashboardIds { get; set; } = new List<string>();
    }

    public class OtherDashboardOutput
    {
        public string operation { get; set; }
    }

    public class DashboardUpdateInput
    {
        public string dashboardId { get; set; }
        public List<DashboardLocalizedLabelInput> labels { get; set; } = new List<DashboardLocalizedLabelInput>();
    }

    public class DashboardLocalizedLabelInput
    {
        public string LanguageCode { get; set; }
        public string Label { get; set; }
    }

    public class DashboardMetadataOutput
    {
        public string formid { get; set; }
        public string name { get; set; }
        public int? type { get; set; }
        public string objecttypecode { get; set; }
        public DashboardLabelOutput Label { get; set; }
    }

    public class DashboardLabelOutput
    {
        public List<DashboardLocalizedLabelOutput> LocalizedLabels { get; set; } = new List<DashboardLocalizedLabelOutput>();
    }

    public class DashboardLocalizedLabelOutput
    {
        public int LanguageCode { get; set; }
        public string Label { get; set; }
    }
}
