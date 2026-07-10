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
using System.Linq;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class AttributeAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { AttributeAdapter.WaitAction = System.Threading.Thread.Sleep; }

        [TestMethod]
        public void Load_ReturnsSortedAttributeRows()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var a = AdapterTestHelpers.CreateStringAttribute("new_a", "new_a", (1033, "Alpha"));
            var b = AdapterTestHelpers.CreateStringAttribute("new_b", "new_b", (1033, "Beta"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.BuildEntityResponse("account", b, a);
                }
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(2, output.grid.rows.Count);
            Assert.AreEqual("new_a", output.grid.rows[0].SchemaName);
            Assert.AreEqual("new_b", output.grid.rows[1].SchemaName);
        }

        [TestMethod]
        public void Load_Throws_WhenEntityNameMissing()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service),
                    new EasyTranslatorLoadInput { component = "DisplayText", solutionId = "all" }),
                "entityName");
        }

        [TestMethod]
        public void Load_ExcludesNonCustomizable()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var inc = AdapterTestHelpers.CreateStringAttribute("new_i", "new_i", (1033, "I"));
            var nc = AdapterTestHelpers.CreateStringAttribute("new_nc", "new_nc", (1033, "NC"));
            nc.IsCustomizable = new BooleanManagedProperty(false);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", inc, nc);
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("new_i", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Load_ExcludesBigInt()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var bi = new BigIntAttributeMetadata { LogicalName = "bi", SchemaName = "bi", MetadataId = Guid.NewGuid(), IsCustomizable = new BooleanManagedProperty(true), DisplayName = AdapterTestHelpers.BuildLabel((1033, "B")) };
            var s = AdapterTestHelpers.CreateStringAttribute("new_s", "new_s", (1033, "S"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", bi, s);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("new_s", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Load_HandlesDescription()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var a = AdapterTestHelpers.CreateStringAttribute("new_a", "new_a", (1033, "A"));
            a.Description = AdapterTestHelpers.BuildLabel((1033, "Desc"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", a);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "Description", solutionId = "all" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Description", output.grid.rows[0].GridKey.Split('|').Last());
        }

        [TestMethod]
        public void Save_UpdatesDisplayName()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var captured = new List<OrganizationRequest>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var req = (OrganizationRequest)call[0];
                captured.Add(req);
                if (req is RetrieveAttributeRequest)
                {
                    return AdapterTestHelpers.BuildAttributeResponse(new StringAttributeMetadata { LogicalName = "new_field", SchemaName = "new_field", DisplayName = new Label("Old", 1033) });
                }
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "attributes|account|new_field|DisplayName",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(captured.OfType<UpdateAttributeRequest>().Any());
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "entity" && t.id == "account"));
        }

        [TestMethod]
        public void Save_Throws_WhenLabelKeyInvalid()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "attributes|account|new_field|BadKey",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_Throws_WhenGridKeyInvalidType()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_field|DisplayName",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_ExecutesPublishXml()
        {
            var adapter = new AttributeAdapter();
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
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }

        [TestMethod]
        public void Published_WaitsAndReturnsTargets()
        {
            int wait = 0;
            AttributeAdapter.WaitAction = (ms) => { wait++; };
            var adapter = new AttributeAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, wait);
            Assert.AreEqual(1, output.publishTargets.Count);
        }
    }
}
