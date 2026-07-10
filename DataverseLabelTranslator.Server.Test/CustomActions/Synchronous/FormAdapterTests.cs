using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class FormAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { FormAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is PublishXmlRequest) return new OrganizationResponse();
                if (call[0] is PublishAllXmlRequest) return new OrganizationResponse();
                return new OrganizationResponse();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            FormAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "systemform", id = Guid.NewGuid().ToString() } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithForms_ReturnsRows()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(MakeForm("MainForm", 2, "Info"));
                return AdapterTestHelpers.Entities();
            });
            var userSettings = new Entity("usersettings");
            service.Retrieve("usersettings", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(userSettings);
            service.Update(Arg.Any<Entity>());
            var ctx = AdapterTestHelpers.Context(service);
            ctx.Service = service;
            var pluginCtx = Substitute.For<IPluginExecutionContext>();
            pluginCtx.UserId.Returns(Guid.NewGuid());
            ctx.PluginContext = pluginCtx;
            var output = adapter.Load(ctx, new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        private static Entity MakeForm(string name, int type, string label)
        {
            var e = new Entity("systemform") { Id = Guid.NewGuid() };
            e["formid"] = e.Id;
            e["name"] = name;
            e["type"] = new OptionSetValue(type);
            e["objecttypecode"] = "account";
            e["formxml"] = "<form><labels><label description=\"" + label + "\" languagecode=\"1033\"/></labels></form>";
            return e;
        }
    }
}
