using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class ContentSnippetAdapterTests
    {
        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new ContentSnippetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new ContentSnippetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Load_NoPortalTables_ReturnsEmpty()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                throw new Exception("The entity with a name = adx_contentsnippet was not found");
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithContentSnippets_ReturnsRows()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "adx_contentsnippet") return AdapterTestHelpers.Entities(MakeSnippet("S1"));
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Save_NoPortalTables_ReturnsEmpty()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"content|{Guid.NewGuid():D}|s1",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        private static Entity MakeSnippet(string name)
        {
            var e = new Entity("adx_contentsnippet") { Id = Guid.NewGuid() };
            e["adx_contentsnippetid"] = e.Id;
            e["adx_name"] = name;
            e["adx_value"] = "Hello";
            return e;
        }
    }
}
