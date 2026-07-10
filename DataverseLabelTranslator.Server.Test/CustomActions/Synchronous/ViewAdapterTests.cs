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
    public class ViewAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { ViewAdapter.WaitAction = System.Threading.Thread.Sleep; }

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
        public void Load_NoneEntity_NoViews_ReturnsEmpty()
        {
            var adapter = new ViewAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new ViewAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_SpecificEntity_NoViews_ReturnsEmpty()
        {
            var adapter = new ViewAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Save_WithValidRow_UpdatesLabel()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "OldName")));
                }
                return new OrganizationResponse();
            });
            var viewId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"views|{viewId:D}|name|account",
                        changes = new Dictionary<string, string> { { "1033", "NewName" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsInstanceOfType(captured, typeof(SetLocLabelsRequest));
        }

        [TestMethod]
        public void Save_WithInvalidAttribute_Throws()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            var viewId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"views|{viewId:D}|otherattribute|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenEntityNameNone()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var viewId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"views|{viewId:D}|name|none",
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
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new ViewAdapter();
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

        [TestMethod]
        public void Load_WithViews_ReturnsRows()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedquery") return AdapterTestHelpers.Entities(MakeView("MyView", "account"));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "My View")));
                if (call[0] is RetrieveAllEntitiesRequest) return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse();
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_DescriptionComponent_ReturnsRows()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedquery") return AdapterTestHelpers.Entities(MakeView("MyView", "account"));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "My View Description")));
                if (call[0] is RetrieveAllEntitiesRequest) return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse();
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "Description", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_ForEntity_ReturnsRows()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedquery") return AdapterTestHelpers.Entities(MakeView("MyView", "account"));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "My View")));
                if (call[0] is RetrieveAllEntitiesRequest) return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse();
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            ViewAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            adapter.Published(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Save_Description_UpdatesLabel()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "OldDescription")));
                return new OrganizationResponse();
            });
            var viewId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"views|{viewId:D}|description|account",
                        changes = new Dictionary<string, string> { { "1033", "NewDescription" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyInput_ReturnsEmpty()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput());
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridKey_Throws()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
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
        public void Save_NoLabelChanges_SkipsRow()
        {
            var adapter = new ViewAdapter();
            var service = Substitute.For<IOrganizationService>();
            var viewId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"views|{viewId:D}|name|account",
                        changes = new Dictionary<string, string>()
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        private static Entity MakeView(string name, string entityName)
        {
            var e = new Entity("savedquery") { Id = Guid.NewGuid() };
            e["savedqueryid"] = e.Id;
            e["name"] = name;
            e["returnedtypecode"] = entityName;
            e["querytype"] = 0;
            e["isprivate"] = false;
            e["isquickfindquery"] = false;
            return e;
        }
    }
}
