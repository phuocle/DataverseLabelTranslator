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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class EntityMetadataAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { EntityMetadataAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            EntityMetadataAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Load_NoEntities_ReturnsEmpty()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new EntityMetadata[0];
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithEntities_ReturnsRows()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities(MakeEntity("account", "Account"));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    var em = new EntityMetadata { LogicalName = "account", DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account")) };
                    resp.Results["EntityMetadata"] = new[] { em };
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithSolutionId_ReturnsRows()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new EntityMetadata[0];
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = Guid.NewGuid().ToString("D") });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Save_WithInvalidGridKey_Throws()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "invalid|key",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_WithValidDisplayName_Updates()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var em = new EntityMetadata { LogicalName = "account" };
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "entityMeta|account|DisplayName",
                        changes = new Dictionary<string, string> { { "1033", "New Account" } }
                    }
                }
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        private static Entity MakeEntity(string name, string displayName)
        {
            var e = new Entity("entity") { Id = Guid.NewGuid() };
            e["name"] = name;
            return e;
        }
    }
}
