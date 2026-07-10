using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    /// <summary>
    /// Helper utilities for EasyTranslator adapter tests.
    /// </summary>
    public static class AdapterTestHelpers
    {
        public static EntityCollection Entities(params Entity[] entities)
        {
            return new EntityCollection(entities.ToList());
        }

        public static Entity CreateOrganizationEntity(int languageCode = 1033)
        {
            return new Entity("organization") { ["languagecode"] = languageCode };
        }

        public static Label BuildLabel(params (int lcid, string label)[] labels)
        {
            var label = new Label();
            foreach (var (lcid, text) in labels)
            {
                label.LocalizedLabels.Add(new LocalizedLabel(text, lcid));
            }
            return label;
        }

        public static RetrieveEntityResponse BuildEntityResponse(string name, params AttributeMetadata[] attributes)
        {
            var em = new EntityMetadata { LogicalName = name };
            var attrsProperty = typeof(EntityMetadata).GetProperty("Attributes", BindingFlags.Public | BindingFlags.Instance);
            if (attrsProperty != null)
            {
                attrsProperty.SetValue(em, attributes);
            }
            var response = new RetrieveEntityResponse();
            response.Results["EntityMetadata"] = em;
            return response;
        }

        public static RetrieveAttributeResponse BuildAttributeResponse(AttributeMetadata metadata)
        {
            var response = new RetrieveAttributeResponse();
            response.Results["AttributeMetadata"] = metadata;
            return response;
        }

        public static RetrieveAllEntitiesResponse BuildRetrieveAllEntitiesResponse(params EntityMetadata[] entities)
        {
            var r = new RetrieveAllEntitiesResponse();
            r.Results["EntityMetadata"] = entities ?? new EntityMetadata[0];
            return r;
        }

        public static T SetResultsAndReturn<T>(T response, string key, object value) where T : OrganizationResponse
        {
            response.Results[key] = value;
            return response;
        }

        public static StringAttributeMetadata CreateStringAttribute(string logicalName, string schemaName, params (int lcid, string label)[] labels)
        {
            return new StringAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                MetadataId = Guid.NewGuid(),
                IsCustomizable = new BooleanManagedProperty(true),
                IsRenameable = new BooleanManagedProperty(true),
                DisplayName = BuildLabel(labels)
            };
        }

        public static EasyTranslatorRuntimeContext Context(IOrganizationService serviceAdmin)
        {
            return new EasyTranslatorRuntimeContext { ServiceAdmin = serviceAdmin };
        }

        public static EasyTranslatorLoadInput LoadInput(string entityName, string component = "DisplayText", string solutionId = "all")
        {
            return new EasyTranslatorLoadInput
            {
                translatorType = component,
                solutionId = solutionId,
                entityName = entityName,
                component = component
            };
        }

        public static T InvokeStatic<T>(Type type, string method, params object[] args)
        {
            var m = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, $"Method not found: {type.Name}.{method}");
            return (T)m.Invoke(null, args);
        }

        public static object InvokeStatic(Type type, string method, params object[] args)
        {
            var m = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, $"Method not found: {type.Name}.{method}");
            return m.Invoke(null, args);
        }

        public static RetrieveLocLabelsResponse RetrieveLabelsResponse(Label label)
        {
            var r = new RetrieveLocLabelsResponse();
            r.Results["Label"] = label;
            return r;
        }

        public static Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesResponse RetrieveAvailableLanguagesResponse(params int[] localeIds)
        {
            var r = new Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesResponse();
            r.Results["LocaleIds"] = localeIds;
            return r;
        }

        public static string SerializeJson<T>(T value)
        {
            if (value == null) return "null";
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static void ExpectException<TEx>(Action action, string expectedMessagePart = null) where TEx : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected " + typeof(TEx).Name);
            }
            catch (TEx ex)
            {
                if (expectedMessagePart != null)
                {
                    Assert.IsTrue(ex.Message.Contains(expectedMessagePart),
                        $"Expected message to contain '{expectedMessagePart}' but was: {ex.Message}");
                }
            }
        }
    }
}
