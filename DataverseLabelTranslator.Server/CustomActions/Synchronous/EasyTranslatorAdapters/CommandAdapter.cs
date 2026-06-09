using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class CommandAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const int AppActionComponentType = 10298;
        private const int PublishedWaitMilliseconds = 10000;
        private const string TranslatorType = "commands";
        private const string PublishKind = "entity";

        private static readonly CommandLabelProperty[] TranslatableProperties =
        {
            new CommandLabelProperty("buttonlabeltext", "Text"),
            new CommandLabelProperty("buttontooltiptitle", "Title"),
            new CommandLabelProperty("buttontooltipdescription", "Description"),
            new CommandLabelProperty("buttonaccessibilitytext", "Accessibility Text"),
            new CommandLabelProperty("grouptitle", "Group Title")
        };

        private static readonly Dictionary<int, string> LocationDisplayNames = new Dictionary<int, string>
        {
            { 0, "Form" },
            { 1, "Main Grid" },
            { 2, "Sub Grid" },
            { 3, "Associated Grid" },
            { 4, "Quick Form" },
            { 5, "Global Header" },
            { 6, "Dashboard" }
        };

        private static readonly Dictionary<int, string> CommandTypeDisplayNames = new Dictionary<int, string>
        {
            { 0, "Button" },
            { 1, "Dropdown" },
            { 2, "Split Button" },
            { 3, "Group" }
        };

        public static Action<int> WaitAction { get; set; } = Thread.Sleep;

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var serviceAdmin = context.ServiceAdmin;
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var entityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(entityName) || string.Equals(entityName, "none", StringComparison.OrdinalIgnoreCase))
            {
                return EmptyOutput(baseLanguage);
            }

            var solutionId = ParseSolutionId(input.solutionId, "Commands");
            var solutionCommandIds = solutionId.HasValue
                ? GetSolutionAppActionIds(serviceAdmin, solutionId.Value)
                : null;
            var commands = RetrieveEntityCommands(serviceAdmin, entityName, solutionCommandIds);
            var commandById = BuildCommandMap(commands);

            commands.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(
                GetCommandSortPath(a, commandById),
                GetCommandSortPath(b, commandById)));

            var rows = new List<EasyTranslatorGridRowOutput>();

            foreach (var command in commands)
            {
                var commandId = command.GetAttributeValue<Guid>("appactionid");
                if (commandId == Guid.Empty)
                {
                    continue;
                }

                var parent = new EasyTranslatorGridRowOutput
                {
                    Recid = "command:" + commandId.ToString("D"),
                    GridKey = BuildGridKey(TranslatorType, commandId.ToString("D"), entityName),
                    SchemaName = GetCommandNodeText(command, commandById),
                    RowType = "commands.command",
                    IsEditable = false,
                    IsTranslatable = false,
                    Ai = new EasyTranslatorAiOutput { include = false }
                };

                var children = new List<EasyTranslatorGridRowOutput>();
                foreach (var property in TranslatableProperties)
                {
                    var child = new EasyTranslatorGridRowOutput
                    {
                        Recid = "command:" + commandId.ToString("D") + ":" + property.PropertyName,
                        GridKey = BuildGridKey(TranslatorType, commandId.ToString("D"), property.PropertyName, entityName),
                        SchemaName = property.DisplayName,
                        RowType = "commands.label",
                        IsEditable = true,
                        IsTranslatable = true,
                        Ai = new EasyTranslatorAiOutput { include = true, location = parent.SchemaName }
                    };

                    var label = RetrieveLocLabel(serviceAdmin, commandId, property.PropertyName);
                    AddLabelValues(child, label);
                    children.Add(child);
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
                    title = "Commands",
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fallbackEntityName = (input.entityName ?? string.Empty).Trim().ToLowerInvariant();

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                // gridKey = "commands|{appactionid}|{propertyName}|{entityLogicalName}"
                var parts = ParseGridKey(row.gridKey, TranslatorType, 4);
                var commandId = ValidateGuid(parts[1], "Command appactionid");
                var propertyName = parts[2];
                var entityName = parts[3];

                ValidatePropertyName(propertyName);

                var currentLabel = RetrieveLocLabel(context.ServiceAdmin, commandId, propertyName);
                var mergedLabels = BuildMergedLabel(currentLabel, changes);

                context.ServiceAdmin.Execute(new SetLocLabelsRequest
                {
                    EntityMoniker = new EntityReference("appaction", commandId),
                    AttributeName = propertyName,
                    Labels = mergedLabels
                });

                output.changedRowCount++;
                var publishEntity = string.IsNullOrWhiteSpace(entityName) ? fallbackEntityName : entityName;
                if (!string.IsNullOrWhiteSpace(publishEntity) &&
                    !string.Equals(publishEntity, "none", StringComparison.OrdinalIgnoreCase))
                {
                    AddPublishTarget(output, seenTargets, PublishKind, publishEntity);
                }
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

        private static HashSet<Guid> GetSolutionAppActionIds(IOrganizationService serviceAdmin, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, AppActionComponentType);

            var ids = new HashSet<Guid>();
            foreach (var component in RetrieveAll(serviceAdmin, query))
            {
                var id = component.GetAttributeValue<Guid>("objectid");
                if (id != Guid.Empty)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static List<Entity> RetrieveEntityCommands(
            IOrganizationService serviceAdmin,
            string entityName,
            HashSet<Guid> solutionCommandIds)
        {
            var query = new QueryExpression("appaction")
            {
                ColumnSet = new ColumnSet(
                    "appactionid",
                    "name",
                    "uniquename",
                    "location",
                    "context",
                    "contextvalue",
                    "type",
                    "buttonlabeltext",
                    "buttontooltiptitle",
                    "buttontooltipdescription",
                    "buttonaccessibilitytext",
                    "grouptitle",
                    "parentappactionid",
                    "sequence",
                    "componentstate",
                    "ismanaged",
                    "isdisabled",
                    "hidden",
                    "origin",
                    "statecode")
            };
            query.Criteria.AddCondition("contextvalue", ConditionOperator.Equal, entityName);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            query.Orders.Add(new OrderExpression("location", OrderType.Ascending));
            query.Orders.Add(new OrderExpression("sequence", OrderType.Ascending));
            query.Orders.Add(new OrderExpression("name", OrderType.Ascending));

            var commands = RetrieveAll(serviceAdmin, query);
            if (solutionCommandIds == null)
            {
                return commands;
            }

            var filtered = new List<Entity>();
            foreach (var command in commands)
            {
                var commandId = command.GetAttributeValue<Guid>("appactionid");
                if (solutionCommandIds.Contains(commandId))
                {
                    filtered.Add(command);
                }
            }

            return filtered;
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

        private static Dictionary<Guid, Entity> BuildCommandMap(List<Entity> commands)
        {
            var map = new Dictionary<Guid, Entity>();
            foreach (var command in commands)
            {
                var commandId = command.GetAttributeValue<Guid>("appactionid");
                if (commandId != Guid.Empty)
                {
                    map[commandId] = command;
                }
            }

            return map;
        }

        private static string GetCommandNodeText(Entity command, Dictionary<Guid, Entity> commandById)
        {
            var parts = new List<string>();
            var current = command;
            var guard = new HashSet<Guid>();

            while (current != null)
            {
                var currentId = current.GetAttributeValue<Guid>("appactionid");
                if (currentId == Guid.Empty || !guard.Add(currentId))
                {
                    break;
                }

                parts.Insert(0, GetCommandTypeText(current) + ": " + GetPrimaryCommandText(current));

                var parent = current.GetAttributeValue<EntityReference>("parentappactionid");
                current = parent != null && commandById.TryGetValue(parent.Id, out var parentCommand)
                    ? parentCommand
                    : null;
            }

            return GetLocationText(command) + " / " + string.Join(" / ", parts);
        }

        private static string GetCommandSortPath(Entity command, Dictionary<Guid, Entity> commandById)
        {
            var parts = new List<string>();
            var current = command;
            var guard = new HashSet<Guid>();

            while (current != null)
            {
                var currentId = current.GetAttributeValue<Guid>("appactionid");
                if (currentId == Guid.Empty || !guard.Add(currentId))
                {
                    break;
                }

                parts.Insert(0, string.Join("~", new[]
                {
                    GetSortNumber(GetOptionValue(current, "location") ?? 99, 3),
                    GetSortNumber((int)Math.Round(GetDecimalValue(current, "sequence") * 1000), 14),
                    GetSortNumber(GetOptionValue(current, "type") ?? 99, 3),
                    GetPrimaryCommandText(current).ToLowerInvariant(),
                    currentId.ToString("D")
                }));

                var parent = current.GetAttributeValue<EntityReference>("parentappactionid");
                current = parent != null && commandById.TryGetValue(parent.Id, out var parentCommand)
                    ? parentCommand
                    : null;
            }

            return string.Join(">", parts);
        }

        private static Label RetrieveLocLabel(IOrganizationService serviceAdmin, Guid commandId, string attributeName)
        {
            var response = (RetrieveLocLabelsResponse)serviceAdmin.Execute(new RetrieveLocLabelsRequest
            {
                EntityMoniker = new EntityReference("appaction", commandId),
                AttributeName = attributeName,
                IncludeUnpublished = true
            });

            return response?.Label ?? new Label();
        }

        private static string GetPrimaryCommandText(Entity command)
        {
            return FirstNonEmpty(
                command.GetAttributeValue<string>("buttonlabeltext"),
                command.GetAttributeValue<string>("grouptitle"),
                command.GetAttributeValue<string>("buttontooltiptitle"),
                command.GetAttributeValue<string>("name"),
                command.GetAttributeValue<string>("uniquename"),
                command.GetAttributeValue<Guid>("appactionid").ToString("D"));
        }

        private static string GetCommandTypeText(Entity command)
        {
            var commandType = GetOptionValue(command, "type") ?? 0;
            return CommandTypeDisplayNames.TryGetValue(commandType, out var name) ? name : "Type " + commandType;
        }

        private static string GetLocationText(Entity command)
        {
            var location = GetOptionValue(command, "location") ?? 0;
            return LocationDisplayNames.TryGetValue(location, out var name) ? name : "Location " + location;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static decimal GetDecimalValue(Entity entity, string attributeName)
        {
            if (entity == null || !entity.Contains(attributeName) || entity[attributeName] == null)
            {
                return 0m;
            }

            if (entity[attributeName] is decimal decimalValue)
            {
                return decimalValue;
            }

            if (entity[attributeName] is double doubleValue)
            {
                return Convert.ToDecimal(doubleValue);
            }

            if (entity[attributeName] is int intValue)
            {
                return intValue;
            }

            return 0m;
        }

        private static string GetSortNumber(int value, int digits)
        {
            return value.ToString().PadLeft(digits, '0');
        }

        private static Guid ValidateGuid(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} must be a GUID.");
            }

            return id;
        }

        private static void ValidatePropertyName(string propertyName)
        {
            foreach (var property in TranslatableProperties)
            {
                if (string.Equals(property.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new InvalidPluginExecutionException("Command propertyName is not supported: " + propertyName);
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
                    title = "Commands",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private class CommandLabelProperty
        {
            public CommandLabelProperty(string propertyName, string displayName)
            {
                PropertyName = propertyName;
                DisplayName = displayName;
            }

            public string PropertyName { get; }
            public string DisplayName { get; }
        }
    }
}
