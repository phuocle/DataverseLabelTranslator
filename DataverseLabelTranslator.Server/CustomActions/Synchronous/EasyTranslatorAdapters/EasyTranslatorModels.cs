using DataverseLabelTranslator.Server.CustomActions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class EasyTranslatorRuntimeContext
    {
        public IPluginExecutionContext PluginContext { get; set; }
        public IOrganizationService ServiceAdmin { get; set; }
        public IOrganizationService Service { get; set; }
        public ITracingService Tracing { get; set; }
    }

    public interface IEasyTranslatorTypeAdapter
    {
        EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input);
        EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input);
        EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input);
        EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input);
    }

    public class EasyTranslatorLoadInput : CustomActionInput
    {
        public string translatorType { get; set; }
        public string solutionId { get; set; }
        public string entityName { get; set; }
        public string entityId { get; set; }
        public string component { get; set; }
    }

    public class EasyTranslatorLoadOutput
    {
        public string baseLanguage { get; set; }
        public EasyTranslatorGridOutput grid { get; set; } = new EasyTranslatorGridOutput();
    }

    public class EasyTranslatorGridOutput
    {
        public string mode { get; set; }
        public string title { get; set; }
        public List<EasyTranslatorGridRowOutput> rows { get; set; } = new List<EasyTranslatorGridRowOutput>();
    }

    public class EasyTranslatorGridRowOutput : Dictionary<string, object>
    {
        public string Recid
        {
            get { return GetString("recid"); }
            set { this["recid"] = value; }
        }

        public string GridKey
        {
            get { return GetString("gridKey"); }
            set { this["gridKey"] = value; }
        }

        public string SchemaName
        {
            get { return GetString("schemaName"); }
            set { this["schemaName"] = value; }
        }

        public string RowType
        {
            get { return GetString("rowType"); }
            set { this["rowType"] = value; }
        }

        public bool IsEditable
        {
            set { this["isEditable"] = value; }
        }

        public bool IsTranslatable
        {
            set { this["isTranslatable"] = value; }
        }

        public EasyTranslatorAiOutput Ai
        {
            set { this["ai"] = value; }
        }

        public List<EasyTranslatorGridRowOutput> Children
        {
            set
            {
                if (value != null && value.Count > 0)
                {
                    this["children"] = value;
                }
            }
        }

        public void SetLanguageValue(string languageCode, string label)
        {
            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                this[languageCode] = label ?? string.Empty;
            }
        }

        private string GetString(string key)
        {
            return ContainsKey(key) && this[key] != null ? Convert.ToString(this[key]) : string.Empty;
        }
    }

    public class EasyTranslatorAiOutput
    {
        public bool include { get; set; }
        public string location { get; set; }
    }

    public class EasyTranslatorSaveInput : CustomActionInput
    {
        public string translatorType { get; set; }
        public string solutionId { get; set; }
        public string entityName { get; set; }
        public string entityId { get; set; }
        public string component { get; set; }
        public string baseLanguage { get; set; }
        public List<EasyTranslatorChangedRowInput> changedRows { get; set; } = new List<EasyTranslatorChangedRowInput>();
    }

    public class EasyTranslatorChangedRowInput
    {
        public string gridKey { get; set; }
        public string recid { get; set; }
        public string rowType { get; set; }
        public Dictionary<string, string> changes { get; set; } = new Dictionary<string, string>();
    }

    public class EasyTranslatorPublishInput : CustomActionInput
    {
        public string translatorType { get; set; }
        public List<EasyTranslatorPublishTarget> publishTargets { get; set; } = new List<EasyTranslatorPublishTarget>();
    }

    public class EasyTranslatorSaveOutput
    {
        public bool changed { get; set; }
        public int changedRowCount { get; set; }
        public List<EasyTranslatorPublishTarget> publishTargets { get; set; } = new List<EasyTranslatorPublishTarget>();
    }

    public class EasyTranslatorPublishTarget
    {
        public string kind { get; set; }
        public string id { get; set; }
    }

    public class EasyTranslatorLabelChange
    {
        public int LanguageCode { get; set; }
        public string Label { get; set; }
    }

    public abstract class EasyTranslatorAdapterBase
    {
        protected const string ComponentDisplayText = "DisplayText";
        protected const string ComponentDescription = "Description";

        protected static int GetBaseLanguage(IOrganizationService serviceAdmin)
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

        protected static Guid? ParseSolutionId(string solutionId, string label)
        {
            if (string.IsNullOrWhiteSpace(solutionId) || string.Equals(solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionId, out var id))
            {
                throw new InvalidPluginExecutionException($"{label} solutionId must be a GUID or all.");
            }

            return id;
        }

        protected static List<Entity> ToList(EntityCollection collection)
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

        protected static int? GetOptionValue(Entity entity, string attributeName)
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

        protected static bool? GetManagedBooleanValue(Entity entity, string attributeName)
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

        protected static bool IsDescriptionComponent(string component)
        {
            return string.Equals(component, ComponentDescription, StringComparison.OrdinalIgnoreCase);
        }

        protected static bool IsDisplayTextComponent(string component)
        {
            return string.Equals(component, ComponentDisplayText, StringComparison.OrdinalIgnoreCase);
        }

        protected static string BuildGridKey(params string[] parts)
        {
            var encoded = new List<string>();
            foreach (var part in parts)
            {
                encoded.Add(Uri.EscapeDataString(part ?? string.Empty));
            }

            return string.Join("|", encoded);
        }

        protected static string[] ParseGridKey(string gridKey, string expectedTranslatorType, int expectedMinParts)
        {
            if (string.IsNullOrWhiteSpace(gridKey))
            {
                throw new InvalidPluginExecutionException("EasyTranslator gridKey is required.");
            }

            var rawParts = gridKey.Split(new[] { "|" }, StringSplitOptions.None);
            var parts = new string[rawParts.Length];
            for (var i = 0; i < rawParts.Length; i++)
            {
                parts[i] = Uri.UnescapeDataString(rawParts[i]);
            }

            if (parts.Length < expectedMinParts ||
                !string.Equals(parts[0], expectedTranslatorType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException($"EasyTranslator gridKey does not match {expectedTranslatorType}.");
            }

            return parts;
        }

        protected static List<EasyTranslatorLabelChange> GetValidLabelChanges(EasyTranslatorChangedRowInput row)
        {
            var changes = new List<EasyTranslatorLabelChange>();
            if (row?.changes == null)
            {
                return changes;
            }

            foreach (var change in row.changes)
            {
                if (!int.TryParse(Convert.ToString(change.Key), out var languageCode))
                {
                    continue;
                }

                changes.Add(new EasyTranslatorLabelChange
                {
                    LanguageCode = languageCode,
                    Label = change.Value ?? string.Empty
                });
            }

            return changes;
        }

        protected static void AddLabelValues(EasyTranslatorGridRowOutput row, Label label)
        {
            if (row == null || label?.LocalizedLabels == null)
            {
                return;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel == null)
                {
                    continue;
                }

                row.SetLanguageValue(localizedLabel.LanguageCode.ToString(), localizedLabel.Label);
            }
        }

        protected static void AddPublishTarget(EasyTranslatorSaveOutput output, HashSet<string> seen, string kind, string id)
        {
            if (output == null || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var key = kind + "|" + id;
            if (seen != null && !seen.Add(key))
            {
                return;
            }

            output.publishTargets.Add(new EasyTranslatorPublishTarget
            {
                kind = kind,
                id = id
            });
            output.changed = true;
        }

        protected static List<string> GetPublishTargetIds(EasyTranslatorPublishInput input, string kind, bool requireGuid)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in input?.publishTargets ?? new List<EasyTranslatorPublishTarget>())
            {
                if (target == null ||
                    !string.Equals(target.kind, kind, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(target.id))
                {
                    continue;
                }

                if (requireGuid && !Guid.TryParse(target.id, out _))
                {
                    continue;
                }

                if (seen.Add(target.id))
                {
                    ids.Add(target.id);
                }
            }

            return ids;
        }

        protected static EasyTranslatorSaveOutput ToPublishOutput(string kind, List<string> ids)
        {
            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids ?? new List<string>())
            {
                AddPublishTarget(output, seen, kind, id);
            }

            return output;
        }
    }
}
