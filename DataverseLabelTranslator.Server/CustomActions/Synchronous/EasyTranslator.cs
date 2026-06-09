using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class EasyTranslator : ICustomAction
    {
        private static readonly Dictionary<string, Func<IEasyTranslatorTypeAdapter>> AdapterFactories =
            new Dictionary<string, Func<IEasyTranslatorTypeAdapter>>(StringComparer.OrdinalIgnoreCase)
            {
                { "sitemap", () => new SitemapAdapter() },
                { "dashboards", () => new DashboardAdapter() },
                { "webresources", () => new WebResourceAdapter() },
                { "globalOptionSet", () => new GlobalOptionSetAdapter() },
                { "attributes", () => new AttributeAdapter() },
                { "options", () => new OptionSetAdapter() },
                { "entityMeta", () => new EntityMetadataAdapter() },
                { "views", () => new ViewAdapter() },
                { "formMeta", () => new FormMetaAdapter() },
                { "relationships", () => new RelationshipAdapter() },
                { "charts", () => new ChartAdapter() },
                { "bpf", () => new BpfAdapter() },
                { "entityMessages", () => new EntityMessageAdapter() },
                { "commands", () => new CommandAdapter() },
                { "businessRules", () => new BusinessRuleAdapter() },
                { "content", () => new ContentSnippetAdapter() }
            };

        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<EasyTranslatorLoadInput>(json);
            return ResolveAdapter(input.translatorType).Load(CreateRuntimeContext(context, serviceAdmin, service, tracing), input);
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<EasyTranslatorSaveInput>(json);
            return ResolveAdapter(input.translatorType).Save(CreateRuntimeContext(context, serviceAdmin, service, tracing), input);
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<EasyTranslatorPublishInput>(json);
            return ResolveAdapter(input.translatorType).Publish(CreateRuntimeContext(context, serviceAdmin, service, tracing), input);
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<EasyTranslatorPublishInput>(json);
            return ResolveAdapter(input.translatorType).Published(CreateRuntimeContext(context, serviceAdmin, service, tracing), input);
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = Deserialize<EasyTranslatorLoadInput>(json);
            return new
            {
                translatorType = input.translatorType,
                operation = input.operation
            };
        }

        private static EasyTranslatorRuntimeContext CreateRuntimeContext(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing)
        {
            return new EasyTranslatorRuntimeContext
            {
                PluginContext = context,
                ServiceAdmin = serviceAdmin,
                Service = service,
                Tracing = tracing
            };
        }

        private static IEasyTranslatorTypeAdapter ResolveAdapter(string translatorType)
        {
            if (string.IsNullOrWhiteSpace(translatorType))
            {
                throw new InvalidPluginExecutionException("EasyTranslator translatorType is required.");
            }

            if (!AdapterFactories.TryGetValue(translatorType, out var factory))
            {
                throw new InvalidPluginExecutionException($"Unsupported EasyTranslator translatorType: {translatorType}");
            }

            return factory();
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            var input = DevKitJson.Deserialize<T>(json);
            if (input == null)
            {
                throw new InvalidPluginExecutionException("Missing EasyTranslator input.");
            }

            return input;
        }
    }
}
