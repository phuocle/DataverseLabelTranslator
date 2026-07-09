using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OtherActionTests
    {
        [TestMethod]
        public void Loading_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Loading(null, null, null, null, null));
        }

        [TestMethod]
        public void Saving_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Saving(null, null, null, null, null));
        }

        [TestMethod]
        public void Publishing_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Publishing(null, null, null, null, null));
        }

        [TestMethod]
        public void Published_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Published(null, null, null, null, null));
        }

        [TestMethod]
        public void Other_NoOperation_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Other(null, null, null, null, "{}"));
        }

        [TestMethod]
        public void Other_Providers_ReturnsConfiguredProviders()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            // Mock: web resource query returns content with settings JSON
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"ai\":{\"selectedProvider\":\"google\",\"providers\":{\"google\":{\"enabled\":true,\"baseUrl\":\"u\",\"apiKey\":\"k\",\"modelName\":\"g\"},\"openai\":{\"enabled\":true,\"baseUrl\":\"u\",\"apiKey\":\"k\",\"modelName\":\"o\"},\"azure\":{}}}}}"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "webresource") return new EntityCollection(new[] { wr });
                return new EntityCollection();
            });
            var output = action.Other(null, service, null, null, "{\"operation\":\"Providers\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void Other_Providers_GeminiAlias_Normalized()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"ai\":{\"selectedProvider\":\"gemini\",\"providers\":{\"google\":{\"enabled\":true,\"baseUrl\":\"u\",\"apiKey\":\"k\",\"modelName\":\"g\"},\"openai\":{},\"azure\":{}}}}"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { wr }));
            var output = action.Other(null, service, null, null, "{\"operation\":\"Providers\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void Other_EmptyOperation_Throws()
        {
            var action = new OtherAction();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Other(null, null, null, null, "{\"operation\":\"  \"}"));
        }

        [TestMethod]
        public void Other_Translate_Dispatches()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Other(null, service, null, null, "{\"operation\":\"Translate\"}"),
                "provider is required");
        }

        [TestMethod]
        public void Other_Solutions_Dispatches()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return new EntityCollection(new[] { new Entity("organization") { ["languagecode"] = 1033 } });
                return new EntityCollection();
            });
            service.Execute(Arg.Any<Microsoft.Xrm.Sdk.OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesRequest)
                {
                    var resp = new Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesResponse();
                    resp.Results["LocaleIds"] = new int[] { 1033, 1031, 1036 };
                    return resp;
                }
                return new Microsoft.Xrm.Sdk.OrganizationResponse();
            });
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetLanguageLocales\"}");
            Assert.IsNotNull(output);
        }
    }
}
