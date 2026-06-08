using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    internal class OtherProvider
    {
        internal const string OperationName = "Providers";
        internal const string NormalizedOperationName = "providers";

        public OtherProvidersOutput Execute(IOrganizationService serviceAdmin)
        {
            var settings = OtherSupport.LoadAiSettings(serviceAdmin);
            return BuildProvidersOutput(settings);
        }

        private static OtherProvidersOutput BuildProvidersOutput(OtherAiSettings settings)
        {
            var providers = new List<OtherProviderItemOutput>();
            AddProviderItem(providers, "google", "Google", settings.providers.google);
            AddProviderItem(providers, "openai", "OpenAI", settings.providers.openai);
            AddProviderItem(providers, "azure", "Azure", settings.providers.azure);

            var selectedProvider = OtherSupport.NormalizeProvider(settings.selectedProvider);
            if (!providers.Any(provider => string.Equals(provider.id, selectedProvider, StringComparison.OrdinalIgnoreCase)))
            {
                selectedProvider = providers.Count > 0 ? providers[0].id : string.Empty;
            }

            return new OtherProvidersOutput
            {
                operation = OperationName,
                selectedProvider = selectedProvider,
                providers = providers
            };
        }

        private static void AddProviderItem(List<OtherProviderItemOutput> providers, string id, string text, OtherProviderConfig config)
        {
            if (OtherSupport.IsProviderConfigured(config))
            {
                providers.Add(new OtherProviderItemOutput { id = id, text = text });
            }
        }
    }
}
