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
    public class ChartAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { ChartAdapter.WaitAction = System.Threading.Thread.Sleep; }

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
        public void Load_AllEntities_ReturnsEmptyGrid()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmptyGrid()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_NullEntityName_ReturnsEmptyGrid()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = null, component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_AllEntities_WithCharts_ReturnsTreeRows()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var chartId = Guid.NewGuid();
            var chart = new Entity("savedqueryvisualization") { Id = chartId };
            chart["savedqueryvisualizationid"] = chartId;
            chart["name"] = "Account Chart";
            chart["primaryentitytypecode"] = "account";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedqueryvisualization") return AdapterTestHelpers.Entities(chart);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(new EntityMetadata
                    {
                        LogicalName = "account",
                        MetadataId = Guid.NewGuid(),
                        DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account"))
                    });
                }
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Account Chart")));
                }
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(1, children.Count);
        }

        [TestMethod]
        public void Load_SpecificEntity_NoCharts_ReturnsEmpty()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_SpecificEntity_WithChart_ReturnsFlatRow()
        {
            var adapter = new ChartAdapter();
            var service = CreateServiceWithBaseLanguage();
            var chartId = Guid.NewGuid();
            var chart = new Entity("savedqueryvisualization") { Id = chartId };
            chart["savedqueryvisualizationid"] = chartId;
            chart["name"] = "Account Chart";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedqueryvisualization") return AdapterTestHelpers.Entities(chart);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Account Chart"), (1031, "Konto")));
                }
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account Chart", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Save_WithValidRow_UpdatesLabel()
        {
            var adapter = new ChartAdapter();
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
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|account",
                        changes = new Dictionary<string, string> { { "1033", "NewName" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(captured);
            Assert.IsInstanceOfType(captured, typeof(SetLocLabelsRequest));
        }

        [TestMethod]
        public void Save_NullChangedRows_ReturnsEmpty()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithBlankEntityName_DoesNotPublish()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|",
                        changes = new Dictionary<string, string> { { "1033", "NewName" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Save_WithNullCurrentLabelAndNullChangeValue_UsesEmptyLabel()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            SetLocLabelsRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    var response = new RetrieveLocLabelsResponse();
                    response.Results["Label"] = null;
                    return response;
                }

                captured = (SetLocLabelsRequest)call[0];
                return new OrganizationResponse();
            });
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|account",
                        changes = new Dictionary<string, string> { { "1033", null } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(captured);
            Assert.AreEqual(string.Empty, captured.Labels.Single().Label);
        }

        [TestMethod]
        public void Save_WithNonNumericChange_SkipsRow()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|account",
                        changes = new Dictionary<string, string> { { "not-a-language", "Ignored" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
        }

        [TestMethod]
        public void Save_WithInvalidChartId_Throws()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "charts|not-a-guid|name|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Chart savedqueryvisualizationid must be a GUID");
        }

        [TestMethod]
        public void Save_WithBlankChartId_Throws()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "charts|%20%20%20|name|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Chart savedqueryvisualizationid must be a GUID");
        }

        [TestMethod]
        public void Save_WithDuplicateExistingLabels_MergesFirstExistingAndChanges()
        {
            var adapter = new ChartAdapter();
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

            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|account",
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
        public void RetrieveLocLabel_WithNullResponse_ReturnsEmptyLabel()
        {
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns((OrganizationResponse)null);

            var label = AdapterTestHelpers.InvokeStatic<Label>(
                typeof(ChartAdapter),
                "RetrieveLocLabel",
                service,
                Guid.NewGuid(),
                "name");

            Assert.IsNotNull(label);
            Assert.AreEqual(0, label.LocalizedLabels.Count);
        }

        [TestMethod]
        public void BuildMergedLabel_WithNullCurrentAndNullChangeLabel_ReturnsEmptyLocalizedLabel()
        {
            var changes = new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = null }
            };

            var labels = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(
                typeof(ChartAdapter),
                "BuildMergedLabel",
                null,
                changes);

            Assert.AreEqual(1, labels.Length);
            Assert.AreEqual(1033, labels[0].LanguageCode);
            Assert.AreEqual(string.Empty, labels[0].Label);
        }

        [TestMethod]
        public void Save_WithBadAttributeName_Throws()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|description|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenEntityNameNone()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var chartId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"charts|{chartId:D}|name|none",
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
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new ChartAdapter();
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
        public void Published_WithTargets_WaitsAndReturnsTargets()
        {
            var adapter = new ChartAdapter();
            var service = Substitute.For<IOrganizationService>();
            var waited = 0;
            ChartAdapter.WaitAction = milliseconds => waited = milliseconds;
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };

            var output = adapter.Published(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(10000, waited);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("account", output.publishTargets[0].id);
        }

        [TestMethod]
        public void Load_AllEntities_WithSolutionFilter_SkipsUnmatchedAndInvalidRows()
        {
            var adapter = new ChartAdapter();
            var solutionId = Guid.NewGuid();
            var accountMetadataId = Guid.NewGuid();
            var accountChartId = Guid.NewGuid();
            var contactChartId = Guid.NewGuid();
            var accountChart = new Entity("savedqueryvisualization") { Id = accountChartId };
            accountChart["savedqueryvisualizationid"] = accountChartId;
            accountChart["name"] = null;
            accountChart["primaryentitytypecode"] = "account";
            var contactChart = new Entity("savedqueryvisualization") { Id = contactChartId };
            contactChart["savedqueryvisualizationid"] = contactChartId;
            contactChart["name"] = "Contact Chart";
            contactChart["primaryentitytypecode"] = "contact";
            var emptyEntityChart = new Entity("savedqueryvisualization") { Id = Guid.NewGuid() };
            emptyEntityChart["savedqueryvisualizationid"] = emptyEntityChart.Id;
            emptyEntityChart["name"] = "No Entity";
            emptyEntityChart["primaryentitytypecode"] = "";
            var missingEntityChart = new Entity("savedqueryvisualization") { Id = Guid.NewGuid() };
            missingEntityChart["savedqueryvisualizationid"] = missingEntityChart.Id;
            missingEntityChart["name"] = "Missing Entity";
            var emptyIdChart = new Entity("savedqueryvisualization") { Id = Guid.NewGuid() };
            emptyIdChart["savedqueryvisualizationid"] = Guid.Empty;
            emptyIdChart["name"] = "No Id";
            emptyIdChart["primaryentitytypecode"] = "account";
            var solutionComponent = new Entity("solutioncomponent");
            solutionComponent["objectid"] = accountMetadataId;

            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedqueryvisualization") return AdapterTestHelpers.Entities(contactChart, emptyEntityChart, missingEntityChart, emptyIdChart, accountChart);
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities(solutionComponent);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(
                        new EntityMetadata
                        {
                            LogicalName = "account",
                            MetadataId = accountMetadataId,
                            DisplayName = AdapterTestHelpers.BuildLabel((1041, "取引先企業"))
                        },
                        new EntityMetadata
                        {
                            LogicalName = "contact",
                            MetadataId = Guid.NewGuid(),
                            DisplayName = AdapterTestHelpers.BuildLabel((1033, "Contact"))
                        },
                        new EntityMetadata
                        {
                            LogicalName = "",
                            MetadataId = Guid.NewGuid(),
                            DisplayName = AdapterTestHelpers.BuildLabel((1033, "Blank"))
                        },
                        new EntityMetadata
                        {
                            LogicalName = "lead",
                            DisplayName = null
                        });
                }

                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(null);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = solutionId.ToString("D") });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("account", output.grid.rows[0].SchemaName);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(1, children.Count);
            Assert.AreEqual("Chart", children[0].SchemaName);
        }

        [TestMethod]
        public void Load_SpecificEntity_SkipsEmptyIdAndUsesDefaultName()
        {
            var adapter = new ChartAdapter();
            var chartId = Guid.NewGuid();
            var emptyIdChart = new Entity("savedqueryvisualization") { Id = Guid.NewGuid() };
            emptyIdChart["savedqueryvisualizationid"] = Guid.Empty;
            emptyIdChart["name"] = "No Id";
            var unnamedChart = new Entity("savedqueryvisualization") { Id = chartId };
            unnamedChart["savedqueryvisualizationid"] = chartId;
            unnamedChart["name"] = null;

            var service = CreateServiceWithBaseLanguage();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "savedqueryvisualization") return AdapterTestHelpers.Entities(emptyIdChart, unnamedChart);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Chart")));
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Chart", output.grid.rows[0].SchemaName);
        }
    }
}
