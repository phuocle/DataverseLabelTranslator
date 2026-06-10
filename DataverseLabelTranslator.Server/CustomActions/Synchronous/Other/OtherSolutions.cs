using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal class OtherSolutions
    {
        internal const string GetSolutionsOperationName = "GetSolutions";
        internal const string GetEntitiesOperationName = "GetEntities";
        internal const string GetBaseLanguageOperationName = "GetBaseLanguage";
        internal const string GetAllNoneBaseLanguageCodesOperationName = "GetAllNoneBaseLanguageCodes";
        internal const string GetLanguageLocalesOperationName = "GetLanguageLocales";

        internal static bool IsXrmOperation(string operation)
        {
            var normalized = OtherSupport.Normalize(operation);
            return normalized == OtherSupport.Normalize(GetSolutionsOperationName) ||
                normalized == OtherSupport.Normalize(GetEntitiesOperationName) ||
                normalized == OtherSupport.Normalize(GetBaseLanguageOperationName) ||
                normalized == OtherSupport.Normalize(GetAllNoneBaseLanguageCodesOperationName) ||
                normalized == OtherSupport.Normalize(GetLanguageLocalesOperationName);
        }

        internal object Execute(IOrganizationService serviceAdmin, string json)
        {
            var input = OtherSupport.Deserialize<OtherEntitiesInput>(json);
            var operation = OtherSupport.Normalize(input.operation);

            if (operation == OtherSupport.Normalize(GetSolutionsOperationName))
            {
                return GetSolutions(serviceAdmin);
            }

            if (operation == OtherSupport.Normalize(GetEntitiesOperationName))
            {
                return GetEntities(serviceAdmin, input.solutionId);
            }

            if (operation == OtherSupport.Normalize(GetBaseLanguageOperationName))
            {
                return GetBaseLanguageOutput(serviceAdmin);
            }

            if (operation == OtherSupport.Normalize(GetAllNoneBaseLanguageCodesOperationName))
            {
                return GetAllNoneBaseLanguageCodes(serviceAdmin);
            }

            if (operation == OtherSupport.Normalize(GetLanguageLocalesOperationName))
            {
                return GetLanguageLocales(serviceAdmin);
            }

            throw new InvalidPluginExecutionException("Other Xrm operation is not supported.");
        }

        private static OtherSolutionsOutput GetSolutions(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "friendlyname", "uniquename")
            };
            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
            query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
            query.Criteria.AddCondition("uniquename", ConditionOperator.NotEqual, "Default");
            query.Orders.Add(new OrderExpression("friendlyname", OrderType.Ascending));

            var output = new OtherSolutionsOutput
            {
                operation = GetSolutionsOperationName
            };

            var results = serviceAdmin.RetrieveMultiple(query);
            foreach (var solution in results.Entities)
            {
                output.solutions.Add(new OtherSolutionItemOutput
                {
                    solutionid = solution.Id.ToString("D"),
                    friendlyname = solution.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                    uniquename = solution.GetAttributeValue<string>("uniquename") ?? string.Empty
                });
            }

            return output;
        }

        private static OtherEntitiesOutput GetEntities(IOrganizationService serviceAdmin, string solutionId)
        {
            var metadataIds = GetSolutionEntityMetadataIds(serviceAdmin, solutionId);
            var request = new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            };
            var response = (RetrieveAllEntitiesResponse)serviceAdmin.Execute(request);
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var output = new OtherEntitiesOutput
            {
                operation = GetEntitiesOperationName
            };

            foreach (var entity in response.EntityMetadata ?? new EntityMetadata[0])
            {
                if (entity == null || entity.MetadataId == null || entity.IsCustomizable?.Value != true)
                {
                    continue;
                }

                if (metadataIds != null && !metadataIds.Contains(entity.MetadataId.Value))
                {
                    continue;
                }

                output.entities.Add(new OtherEntityItemOutput
                {
                    MetadataId = entity.MetadataId.Value.ToString("D"),
                    SchemaName = entity.SchemaName ?? string.Empty,
                    LogicalName = entity.LogicalName ?? string.Empty,
                    DisplayName = GetLocalizedLabel(entity.DisplayName, baseLanguage)
                });
            }

            output.entities = output.entities
                .OrderBy(entity => string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.LogicalName : entity.DisplayName)
                .ThenBy(entity => entity.LogicalName)
                .ToList();

            return output;
        }

        private static OtherBaseLanguageOutput GetBaseLanguageOutput(IOrganizationService serviceAdmin)
        {
            return new OtherBaseLanguageOutput
            {
                operation = GetBaseLanguageOperationName,
                languageCode = GetBaseLanguage(serviceAdmin)
            };
        }

        private static OtherLanguageCodesOutput GetAllNoneBaseLanguageCodes(IOrganizationService serviceAdmin)
        {
            var baseLanguage = GetBaseLanguage(serviceAdmin);
            var request = new RetrieveAvailableLanguagesRequest();
            var response = (RetrieveAvailableLanguagesResponse)serviceAdmin.Execute(request);
            var output = new OtherLanguageCodesOutput
            {
                operation = GetAllNoneBaseLanguageCodesOperationName
            };

            foreach (var localeId in response.LocaleIds ?? new int[0])
            {
                if (localeId != baseLanguage)
                {
                    output.LocaleIds.Add(localeId);
                }
            }

            return output;
        }

        private static OtherLanguageLocalesOutput GetLanguageLocales(IOrganizationService serviceAdmin)
        {
            var query = new QueryExpression("languagelocale")
            {
                ColumnSet = new ColumnSet("language", "localeid")
            };
            var output = new OtherLanguageLocalesOutput
            {
                operation = GetLanguageLocalesOperationName
            };

            foreach (var locale in serviceAdmin.RetrieveMultiple(query).Entities)
            {
                var localeId = locale.GetAttributeValue<int>("localeid");
                if (localeId <= 0)
                {
                    continue;
                }

                output.locales.Add(new OtherLanguageLocaleItemOutput
                {
                    localeid = localeId,
                    language = locale.GetAttributeValue<string>("language") ?? localeId.ToString()
                });
            }

            output.locales = output.locales.OrderBy(locale => locale.language).ThenBy(locale => locale.localeid).ToList();
            return output;
        }

        private static HashSet<Guid> GetSolutionEntityMetadataIds(IOrganizationService serviceAdmin, string solutionId)
        {
            if (string.IsNullOrWhiteSpace(solutionId) || string.Equals(solutionId, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(solutionId, out var id))
            {
                throw new InvalidPluginExecutionException("GetEntities solutionId must be a GUID or all.");
            }

            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid")
            };
            query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, id);
            query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1);

            var metadataIds = new HashSet<Guid>();
            var result = serviceAdmin.RetrieveMultiple(query);
            foreach (var component in result.Entities)
            {
                var objectId = component.GetAttributeValue<Guid>("objectid");
                if (objectId != Guid.Empty)
                {
                    metadataIds.Add(objectId);
                }
            }

            return metadataIds;
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

        private static string GetLocalizedLabel(Label label, int languageCode)
        {
            if (label?.LocalizedLabels == null)
            {
                return string.Empty;
            }

            foreach (var localizedLabel in label.LocalizedLabels)
            {
                if (localizedLabel != null && localizedLabel.LanguageCode == languageCode)
                {
                    return localizedLabel.Label ?? string.Empty;
                }
            }

            var userLabel = label.UserLocalizedLabel;
            if (userLabel != null)
            {
                return userLabel.Label ?? string.Empty;
            }

            return label.LocalizedLabels.Count > 0 ? label.LocalizedLabels[0].Label ?? string.Empty : string.Empty;
        }
    }
}
