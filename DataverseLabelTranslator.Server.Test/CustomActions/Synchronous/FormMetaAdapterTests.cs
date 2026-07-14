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
    public class FormMetaAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { FormMetaAdapter.WaitAction = System.Threading.Thread.Sleep; }

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
        public void Load_NoneEntity_NoForms_ReturnsEmpty()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_AllEntities_WithForms_ReturnsTreeRows()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var formId = Guid.NewGuid();
            var form = new Entity("systemform") { Id = formId };
            form["formid"] = formId;
            form["type"] = new OptionSetValue(2);
            form["name"] = "Main Form";
            form["objecttypecode"] = "account";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(form);
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
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Main Form")));
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
        public void Load_AllEntities_Description_FiltersBySolutionAndSkipsInvalidForms()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var solutionId = Guid.NewGuid();
            var accountMetadataId = Guid.NewGuid();
            var contactMetadataId = Guid.NewGuid();
            var accountMainFormId = Guid.NewGuid();
            var accountQuickFormId = Guid.NewGuid();
            var contactFormId = Guid.NewGuid();
            var missingCodeForm = MakeForm(Guid.NewGuid(), "Missing Code", 2, "");
            var emptyIdForm = MakeForm(Guid.Empty, "Empty Id", 2, "account");
            var accountMainForm = MakeForm(accountMainFormId, "Account Main", 2, "account");
            var accountQuickForm = MakeForm(accountQuickFormId, null, 6, "account");
            var contactForm = MakeForm(contactFormId, "Contact Main", 2, "contact");
            var missingObjectTypeCodeForm = new Entity("systemform") { Id = Guid.NewGuid() };
            missingObjectTypeCodeForm["formid"] = missingObjectTypeCodeForm.Id;
            missingObjectTypeCodeForm["name"] = "Missing Object Type Code";
            missingObjectTypeCodeForm["type"] = new OptionSetValue(2);

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(missingObjectTypeCodeForm, missingCodeForm, emptyIdForm, contactForm, accountQuickForm, accountMainForm);
                if (q.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("solutioncomponent") { ["objectid"] = accountMetadataId },
                        new Entity("solutioncomponent") { ["objectid"] = Guid.NewGuid() });
                }

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
                            DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account"))
                        },
                        new EntityMetadata
                        {
                            LogicalName = "contact",
                            MetadataId = contactMetadataId,
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
                            LogicalName = "noid",
                            DisplayName = AdapterTestHelpers.BuildLabel((1033, "No Id"))
                        });
                }

                if (call[0] is RetrieveLocLabelsRequest request)
                {
                    Assert.AreEqual("description", request.AttributeName);
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Description")));
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = "none", component = "Description", solutionId = solutionId.ToString("D") });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account (account)", output.grid.rows[0].SchemaName);
            var children = GetChildren(output.grid.rows[0]);
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual("Account Main (Main)", children[0].SchemaName);
            Assert.AreEqual($"formMeta|{accountMainFormId:D}|description|entity|account", children[0].GridKey);
            Assert.AreEqual("Quick View", children[1].SchemaName);
            Assert.AreEqual($"formMeta|{accountQuickFormId:D}|description|entity|account", children[1].GridKey);
        }

        [TestMethod]
        public void Load_NullEntityName_UsesAllFormsAndEntityNameFallback()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var formId = Guid.NewGuid();
            var form = MakeForm(formId, "Main Form", 2, "account");

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(form);
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
                        DisplayName = new Label()
                    });
                }

                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return null;
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = null, component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("account", output.grid.rows[0].SchemaName);
            Assert.AreEqual("Main Form (Main)", GetChildren(output.grid.rows[0])[0].SchemaName);
        }

        [TestMethod]
        public void Load_SpecificEntity_NoForms_ReturnsEmpty()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_SpecificEntity_WithForm_ReturnsFlatRow()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var formId = Guid.NewGuid();
            var form = new Entity("systemform") { Id = formId };
            form["formid"] = formId;
            form["type"] = new OptionSetValue(2);
            form["name"] = "Main Form";
            form["objecttypecode"] = "account";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(form);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Main Form"), (1031, "Hauptformular")));
                }
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Main Form (Main)", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Load_SpecificEntity_Description_SortsRowsAndBuildsDashboardScope()
        {
            var adapter = new FormMetaAdapter();
            var service = CreateServiceWithBaseLanguage();
            var dashboardFormId = Guid.NewGuid();
            var appModuleFormId = Guid.NewGuid();
            var customTypeFormId = Guid.NewGuid();
            var dashboardForm = MakeForm(dashboardFormId, "Dashboard Form", 0, "account");
            var appModuleForm = MakeForm(appModuleFormId, "App Form", 10, "account");
            var customTypeForm = MakeForm(customTypeFormId, "", 99, "account");

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(customTypeForm, dashboardForm, appModuleForm);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest request)
                {
                    Assert.AreEqual("description", request.AttributeName);
                    return AdapterTestHelpers.RetrieveLabelsResponse(null);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { entityName = "account", component = "Description", solutionId = "all" });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(3, output.grid.rows.Count);
            Assert.AreEqual("App Form (App Module Main)", output.grid.rows[0].SchemaName);
            Assert.AreEqual($"formMeta|{appModuleFormId:D}|description|dashboard|{appModuleFormId:D}", output.grid.rows[0].GridKey);
            Assert.AreEqual("Dashboard Form (Dashboard)", output.grid.rows[1].SchemaName);
            Assert.AreEqual($"formMeta|{dashboardFormId:D}|description|dashboard|{dashboardFormId:D}", output.grid.rows[1].GridKey);
            Assert.AreEqual("Type 99", output.grid.rows[2].SchemaName);
            Assert.AreEqual($"formMeta|{customTypeFormId:D}|description|entity|account", output.grid.rows[2].GridKey);
        }

        [TestMethod]
        public void Save_WithValidRow_UpdatesLabel()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Old")));
                }
                return new OrganizationResponse();
            });
            var formId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"formMeta|{formId:D}|name|entity|account",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsInstanceOfType(captured, typeof(SetLocLabelsRequest));
            var setRequest = (SetLocLabelsRequest)captured;
            Assert.AreEqual("name", setRequest.AttributeName);
            CollectionAssert.AreEqual(new[] { 1033 }, setRequest.Labels.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "New" }, setRequest.Labels.Select(label => label.Label).ToArray());
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("entity", output.publishTargets[0].kind);
            Assert.AreEqual("account", output.publishTargets[0].id);
        }

        [TestMethod]
        public void Save_WithInvalidAttribute_Throws()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var formId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"formMeta|{formId:D}|otherattr|entity|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_WithInvalidGuid_Throws()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "formMeta|not-a-guid|name|entity|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Form formid must be a GUID");
        }

        [TestMethod]
        public void Save_WithBlankGuid_Throws()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "formMeta| |name|entity|account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "Form formid must be a GUID");
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_NullChangedRowsAndInvalidLanguageChanges_ReturnsEmpty()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var formId = Guid.NewGuid();

            var nullOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            var invalidLanguageOutput = adapter.Save(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        new EasyTranslatorChangedRowInput
                        {
                            gridKey = $"formMeta|{formId:D}|name|entity|account",
                            changes = new Dictionary<string, string> { { "not-a-language", "X" } }
                        }
                    }
                });

            Assert.AreEqual(0, nullOutput.changedRowCount);
            Assert.AreEqual(0, invalidLanguageOutput.changedRowCount);
        }

        [TestMethod]
        public void Save_DashboardScope_AddsDashboardPublishTarget()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest) return AdapterTestHelpers.RetrieveLabelsResponse(new Label());
                return new OrganizationResponse();
            });
            var formId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"formMeta|{formId:D}|name|dashboard|{formId:D}",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("dashboard", output.publishTargets[0].kind);
            Assert.AreEqual(formId.ToString("D"), output.publishTargets[0].id);
        }

        [TestMethod]
        public void Save_DescriptionWithEmptyEntityPublishTarget_UpdatesWithoutPublishTarget()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            SetLocLabelsRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Old"), (1033, "Duplicate"), (1041, null)));
                }

                if (call[0] is SetLocLabelsRequest request)
                {
                    captured = request;
                }

                return new OrganizationResponse();
            });
            var formId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"formMeta|{formId:D}|description|entity|",
                        changes = new Dictionary<string, string>
                        {
                            { "1033", "New Description" },
                            { "1066", null }
                        }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
            Assert.AreEqual("description", captured.AttributeName);
            CollectionAssert.AreEqual(new[] { 1033, 1041, 1066 }, captured.Labels.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "New Description", string.Empty, string.Empty }, captured.Labels.Select(label => label.Label).ToArray());
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithEntityTarget_ExecutesPublishXml()
        {
            var adapter = new FormMetaAdapter();
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
            var publish = (PublishXmlRequest)captured;
            Assert.AreEqual("<importexportxml><entities><entity>account</entity></entities></importexportxml>", publish.ParameterXml);
            Assert.AreEqual(1, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithDashboardTarget_ExecutesPublishXml()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            PublishXmlRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (PublishXmlRequest)call[0];
                return new OrganizationResponse();
            });
            var dashboardId = Guid.NewGuid().ToString("D");
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "dashboard", id = dashboardId }
                }
            };

            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual($"<importexportxml><dashboards><dashboard>{{{dashboardId}}}</dashboard></dashboards></importexportxml>", captured.ParameterXml);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("dashboard", output.publishTargets[0].kind);
        }

        [TestMethod]
        public void Publish_WithEntityAndDashboardTargets_ExecutesCombinedPublishXml()
        {
            var adapter = new FormMetaAdapter();
            var service = Substitute.For<IOrganizationService>();
            PublishXmlRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (PublishXmlRequest)call[0];
                return new OrganizationResponse();
            });
            var dashboardId = Guid.NewGuid().ToString("D");
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    new EasyTranslatorPublishTarget { kind = "dashboard", id = dashboardId }
                }
            };

            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual($"<importexportxml><entities><entity>account</entity></entities><dashboards><dashboard>{{{dashboardId}}}</dashboard></dashboards></importexportxml>", captured.ParameterXml);
            Assert.AreEqual(2, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturnsEntityAndDashboardTargets()
        {
            var waitCount = 0;
            FormMetaAdapter.WaitAction = _ => waitCount++;
            var adapter = new FormMetaAdapter();
            var dashboardId = Guid.NewGuid().ToString("D");
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    new EasyTranslatorPublishTarget { kind = "dashboard", id = dashboardId }
                }
            };

            var output = adapter.Published(AdapterTestHelpers.Context(Substitute.For<IOrganizationService>()), input);

            Assert.AreEqual(1, waitCount);
            Assert.AreEqual(2, output.publishTargets.Count);
            Assert.AreEqual("entity", output.publishTargets[0].kind);
            Assert.AreEqual("dashboard", output.publishTargets[1].kind);
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var type = typeof(FormMetaAdapter);
            var labelWithNulls = new Label();
            labelWithNulls.LocalizedLabels.Add(null);
            labelWithNulls.LocalizedLabels.Add(new LocalizedLabel("", 1033));
            labelWithNulls.LocalizedLabels.Add(new LocalizedLabel("Base", 1033));
            var duplicateLabel = AdapterTestHelpers.BuildLabel((1033, "Old"), (1033, "Duplicate"));
            var changes = new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Updated" },
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = null }
            };

            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(type, "GetBaseLanguageLabel", null, 1033));
            Assert.AreEqual("Base", AdapterTestHelpers.InvokeStatic<string>(type, "GetBaseLanguageLabel", labelWithNulls, 1033));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(type, "GetBaseLanguageLabel", AdapterTestHelpers.BuildLabel((1041, "JP")), 1033));
            Assert.AreEqual("Type 123", AdapterTestHelpers.InvokeStatic<string>(type, "GetFormTypeName", 123));
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", duplicateLabel, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, merged.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "Updated", string.Empty }, merged.Select(label => label.Label).ToArray());
            var mergedFromNullExisting = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", labelWithNulls, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, mergedFromNullExisting.Select(label => label.LanguageCode).ToArray());
            var mergedFromNull = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", null, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, mergedFromNull.Select(label => label.LanguageCode).ToArray());
        }

        private static Entity MakeForm(Guid id, string name, int type, string objectTypeCode)
        {
            var form = new Entity("systemform") { Id = id };
            if (id != Guid.Empty)
            {
                form["formid"] = id;
            }

            if (name != null)
            {
                form["name"] = name;
            }

            form["type"] = new OptionSetValue(type);
            form["objecttypecode"] = objectTypeCode;
            return form;
        }

        private static List<EasyTranslatorGridRowOutput> GetChildren(EasyTranslatorGridRowOutput row)
        {
            return (List<EasyTranslatorGridRowOutput>)row["children"];
        }
    }
}
