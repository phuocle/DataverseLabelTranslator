// filepath: DataverseLabelTranslator.Test\CustomActions\Synchronous\EasyTranslatorDispatchTests.cs
using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    /// <summary>
    /// Tests the EasyTranslator dispatch logic to all adapter types.
    /// </summary>
    [TestClass]
    public class EasyTranslatorDispatchTests
    {
        private EasyTranslator _action;
        private IOrganizationService _service;

        [TestInitialize]
        public void Setup()
        {
            _action = new EasyTranslator();
            _service = Substitute.For<IOrganizationService>();
            _service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
        }

        [TestMethod]
        public void Loading_Throws_WhenJsonIsNull()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Loading(null, _service, null, null, null));
        }

        [TestMethod]
        public void Loading_Throws_WhenJsonEmpty()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Loading(null, _service, null, null, ""));
        }

        [TestMethod]
        public void Loading_Throws_WhenTranslatorTypeMissing()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Loading(null, _service, null, null, "{}"));
        }

        [TestMethod]
        public void Loading_Throws_WhenTranslatorTypeUnknown()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Loading(null, _service, null, null, "{\"translatorType\":\"unknown\"}"));
        }

        [TestMethod]
        public void Saving_Throws_WhenJsonIsNull()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Saving(null, _service, null, null, null));
        }

        [TestMethod]
        public void Saving_Throws_WhenTranslatorTypeUnknown()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Saving(null, _service, null, null, "{\"translatorType\":\"unknown\"}"));
        }

        [TestMethod]
        public void Publishing_Throws_WhenJsonIsNull()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Publishing(null, _service, null, null, null));
        }

        [TestMethod]
        public void Publishing_Throws_WhenTranslatorTypeUnknown()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Publishing(null, _service, null, null, "{\"translatorType\":\"unknown\"}"));
        }

        [TestMethod]
        public void Published_Throws_WhenJsonIsNull()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Published(null, _service, null, null, null));
        }

        [TestMethod]
        public void Published_Throws_WhenTranslatorTypeUnknown()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => _action.Published(null, _service, null, null, "{\"translatorType\":\"unknown\"}"));
        }

        [TestMethod]
        public void Other_ReturnsTranslatorAndOperation()
        {
            var output = _action.Other(null, null, null, null, "{\"translatorType\":\"attributes\",\"operation\":\"noop\"}");
            var type = output.GetType();
            Assert.AreEqual("attributes", type.GetProperty("translatorType").GetValue(output));
            Assert.AreEqual("noop", type.GetProperty("operation").GetValue(output));
        }

        [TestMethod]
        public void Other_ReturnsNullOperation_WhenOperationMissing()
        {
            var output = _action.Other(null, null, null, null, "{\"translatorType\":\"attributes\"}");
            var type = output.GetType();
            Assert.AreEqual("attributes", type.GetProperty("translatorType").GetValue(output));
            Assert.IsNull(type.GetProperty("operation").GetValue(output));
        }

        [TestMethod]
        public void Other_ReturnsBlankOperation_WhenOperationBlank()
        {
            var output = _action.Other(null, null, null, null, "{\"translatorType\":\"attributes\",\"operation\":\"  \"}");
            var type = output.GetType();
            Assert.AreEqual("  ", type.GetProperty("operation").GetValue(output));
        }

        [TestMethod]
        public void Loading_DispatchesSitemap()
        {
            _action.Loading(null, _service, null, null, "{\"translatorType\":\"sitemap\"}");
        }

        [TestMethod]
        public void Loading_DispatchesDashboards()
        {
            _action.Loading(null, _service, null, null, "{\"translatorType\":\"dashboards\"}");
        }

        // Note: Loading_DispatchesWebResources/Attributes/OptionSet/EntityMeta/Relationships/Ribbons/EntityMessages
        // are covered by the corresponding adapter-specific test files.

        [TestMethod]
        public void Saving_DispatchesSitemap()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"sitemap\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesDashboards()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"dashboards\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesWebResources()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"webresources\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesCharts()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"charts\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesBpf()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"bpf\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesForms()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"forms\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesEntityMeta()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"entityMeta\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesBusinessRules()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"businessRules\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesContent()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"content\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesRibbons()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"ribbons\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesRelationships()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"relationships\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesCommands()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"commands\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Saving_DispatchesEntityMessages()
        {
            _action.Saving(null, _service, null, null, "{\"translatorType\":\"entityMessages\",\"changedRows\":[]}");
        }

        [TestMethod]
        public void Publishing_DispatchesSitemap()
        {
            _action.Publishing(null, _service, null, null, "{\"translatorType\":\"sitemap\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Publishing_DispatchesDashboards()
        {
            _action.Publishing(null, _service, null, null, "{\"translatorType\":\"dashboards\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Publishing_DispatchesWebResources()
        {
            _action.Publishing(null, _service, null, null, "{\"translatorType\":\"webresources\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Publishing_DispatchesCharts()
        {
            _action.Publishing(null, _service, null, null, "{\"translatorType\":\"charts\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Published_DispatchesSitemap()
        {
            _action.Published(null, _service, null, null, "{\"translatorType\":\"sitemap\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Published_DispatchesDashboards()
        {
            _action.Published(null, _service, null, null, "{\"translatorType\":\"dashboards\",\"publishTargets\":[]}");
        }

        [TestMethod]
        public void Published_DispatchesCharts()
        {
            _action.Published(null, _service, null, null, "{\"translatorType\":\"charts\",\"publishTargets\":[]}");
        }
    }
}
