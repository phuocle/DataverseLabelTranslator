using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class CommandAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { CommandAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateServiceWithBaseLanguage()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solution") return AdapterTestHelpers.Entities(new Entity("solution") { Id = Guid.NewGuid(), ["uniquename"] = "test" });
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmptyGrid()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmptyGrid()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_SpecificEntity_NoCommands_ReturnsEmpty()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Save_WithValidRow_UpdatesLabel()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "OldText")));
                }
                return new OrganizationResponse();
            });
            var cmdId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{cmdId:D}|buttonlabeltext|account",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsInstanceOfType(captured, typeof(SetLocLabelsRequest));
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }

        [TestMethod]
        public void Save_WithInvalidProperty_Throws()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            var cmdId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{cmdId:D}|badproperty|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_WithBlankLanguage_UpdatesOnlyNonBase()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var cmdId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{cmdId:D}|buttonlabeltext|account",
                        changes = new Dictionary<string, string> { { "1033", "" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenEntityNameNone()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var cmdId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{cmdId:D}|buttonlabeltext|none",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
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
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }
    }
}
