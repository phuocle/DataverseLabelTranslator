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
    public class DashboardAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { DashboardAdapter.WaitAction = System.Threading.Thread.Sleep; }

        [TestMethod]
        public void Load_ReturnsEmpty_WhenNoDashboards()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionIdInvalid()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "not-a-guid" }));
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            DashboardAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "dashboard", id = Guid.NewGuid().ToString() } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Load_WithDashboards_ReturnsRows()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(MakeDashboard("MyDashboard", 0));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "My Dashboard")));
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_SkipsInactiveNonCustomizableAndUnsupportedDashboards()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform")
                {
                    var inactive = MakeDashboard("Inactive", 0);
                    inactive["formactivationstate"] = new OptionSetValue(0);
                    var notCustomizable = MakeDashboard("Not Customizable", 0);
                    notCustomizable["iscustomizable"] = new BooleanManagedProperty(false);
                    var unsupported = MakeDashboard("Unsupported", 1);
                    return AdapterTestHelpers.Entities(inactive, notCustomizable, unsupported);
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all" });

            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithSolutionWithoutDashboardIds_ReturnsEmptyRows()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities(new Entity("solutioncomponent"));
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { solutionId = Guid.NewGuid().ToString("D") });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithSolutionDashboardIds_ReturnsFilteredRows()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var dashboardId = Guid.NewGuid();
            var dashboard = MakeDashboard("Solution Dashboard", 10);
            dashboard["formid"] = dashboardId;
            var missingObjectId = new Entity("solutioncomponent");
            var emptyObjectId = new Entity("solutioncomponent");
            emptyObjectId["objectid"] = Guid.Empty;
            var validObjectId = new Entity("solutioncomponent");
            validObjectId["objectid"] = dashboardId;

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities(missingObjectId, emptyObjectId, validObjectId);
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(dashboard);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Solution Dashboard")));
                return new OrganizationResponse();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { solutionId = Guid.NewGuid().ToString("D") });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("dashboard:" + dashboardId.ToString("D"), output.grid.rows[0].Recid);
        }

        [TestMethod]
        public void Load_WithSparseDashboardCandidate_ReturnsRow()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var dashboard = new Entity("systemform") { Id = Guid.NewGuid() };
            dashboard["name"] = "Sparse Dashboard";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(dashboard);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Sparse Dashboard")));
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("dashboard:" + dashboard.Id.ToString("D"), output.grid.rows[0].Recid);
        }

        [TestMethod]
        public void Save_WithValidRow_UpdatesLabel()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var dashId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"dashboards|{dashId:D}|name",
                        changes = new Dictionary<string, string> { { "1033", "NewName" } }
                    }
                }
            };
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(MakeDashboard("OldName", 0));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "OldName")));
                return new OrganizationResponse();
            });
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        [TestMethod]
        public void Save_NullChangedRows_ReturnsEmpty()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithNonNumericChange_SkipsRow()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"dashboards|{Guid.NewGuid():D}|name",
                        changes = new Dictionary<string, string> { { "not-a-language", "Ignored" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
        }

        [TestMethod]
        public void Save_WithNonNameGridKey_Throws()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"dashboards|{Guid.NewGuid():D}|description",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Dashboard gridKey must target the name component");
        }

        [TestMethod]
        public void Save_WithInvalidDashboardId_Throws()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "dashboards|not-a-guid|name",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Dashboard dashboardId must be a GUID");
        }

        [TestMethod]
        public void Save_WithBlankDashboardId_Throws()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "dashboards|%20%20%20|name",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Dashboard dashboardId must be a GUID");
        }

        [TestMethod]
        public void Save_WithDuplicateExistingLabels_MergesFirstExistingAndChanges()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            SetLocLabelsRequest captured = null;
            var current = AdapterTestHelpers.BuildLabel((1033, "Old English"), (1033, "Duplicate English"), (1041, null));
            current.LocalizedLabels.Add(null);
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(current);
                }

                captured = (SetLocLabelsRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"dashboards|{Guid.NewGuid():D}|name",
                        changes = new Dictionary<string, string> { { "1066", "Vietnamese" }, { "1041", "Japanese" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(captured);
            Assert.AreEqual("Old English", captured.Labels.First(label => label.LanguageCode == 1033).Label);
            Assert.AreEqual("Japanese", captured.Labels.First(label => label.LanguageCode == 1041).Label);
            Assert.AreEqual("Vietnamese", captured.Labels.First(label => label.LanguageCode == 1066).Label);
            Assert.AreEqual(3, captured.Labels.Length);
        }

        [TestMethod]
        public void Save_InvalidGridKey_Throws()
        {
            var adapter = new DashboardAdapter();
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
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new DashboardAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "dashboard", id = Guid.NewGuid().ToString() } }
            };
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns((OrganizationResponse)null);
            var dashboardId = Guid.NewGuid();
            var dashboard = new Entity("systemform") { Id = dashboardId };

            var label = AdapterTestHelpers.InvokeStatic<Label>(
                typeof(DashboardAdapter),
                "RetrieveDashboardLabel",
                service,
                dashboardId);
            var baseLabel = AdapterTestHelpers.InvokeStatic<string>(
                typeof(DashboardAdapter),
                "GetDashboardBaseLabel",
                service,
                dashboard,
                dashboardId,
                1033);
            var row = new EasyTranslatorGridRowOutput { SchemaName = "Schema" };
            var rowBaseLabel = AdapterTestHelpers.InvokeStatic<string>(
                typeof(DashboardAdapter),
                "GetRowBaseLabel",
                row,
                1033);
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(
                typeof(DashboardAdapter),
                "BuildMergedLabel",
                null,
                new List<EasyTranslatorLabelChange> { new EasyTranslatorLabelChange { LanguageCode = 1033, Label = null } });

            Assert.IsNotNull(label);
            Assert.AreEqual(0, label.LocalizedLabels.Count);
            Assert.AreEqual(dashboardId.ToString("D"), baseLabel);
            Assert.AreEqual("Schema", rowBaseLabel);
            Assert.AreEqual(string.Empty, merged.Single().Label);
        }

        [TestMethod]
        public void PrivateHelpers_CoverBaseLabelAndRowLabelFallbackBranches()
        {
            var service = Substitute.For<IOrganizationService>();
            var fallbackLabel = AdapterTestHelpers.BuildLabel((1041, "Japanese"), (1033, ""));
            fallbackLabel.LocalizedLabels.Add(null);
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(fallbackLabel);
                }

                return new OrganizationResponse();
            });
            var dashboardId = Guid.NewGuid();
            var dashboard = new Entity("systemform") { Id = dashboardId };
            dashboard["name"] = "Fallback Dashboard";
            var rowWithBaseValue = new EasyTranslatorGridRowOutput { SchemaName = "Schema" };
            rowWithBaseValue["1033"] = "Base Value";
            var rowWithNullBaseValue = new EasyTranslatorGridRowOutput { SchemaName = "Schema" };
            rowWithNullBaseValue["1033"] = null;

            var baseLabel = AdapterTestHelpers.InvokeStatic<string>(
                typeof(DashboardAdapter),
                "GetDashboardBaseLabel",
                service,
                dashboard,
                dashboardId,
                1033);
            var rowBaseValue = AdapterTestHelpers.InvokeStatic<string>(
                typeof(DashboardAdapter),
                "GetRowBaseLabel",
                rowWithBaseValue,
                1033);
            var rowNullValue = AdapterTestHelpers.InvokeStatic<string>(
                typeof(DashboardAdapter),
                "GetRowBaseLabel",
                rowWithNullBaseValue,
                1033);

            Assert.AreEqual("Fallback Dashboard", baseLabel);
            Assert.AreEqual("Base Value", rowBaseValue);
            Assert.AreEqual("Schema", rowNullValue);
        }

        private static Entity MakeDashboard(string name, int type)
        {
            var e = new Entity("systemform") { Id = Guid.NewGuid() };
            e["formid"] = e.Id;
            e["name"] = name;
            e["formactivationstate"] = new OptionSetValue(1);
            e["iscustomizable"] = new BooleanManagedProperty(true);
            e["type"] = new OptionSetValue(type);
            e["objecttypecode"] = "Dashboard";
            return e;
        }
    }
}
