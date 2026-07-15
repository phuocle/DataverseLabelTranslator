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
using System.Reflection;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OptionSetAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { OptionSetAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateServiceWithBaseLanguage()
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
        public void Load_Throws_WhenEntityNameMissing()
        {
            var adapter = new OptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { component = "DisplayText" }));
        }

        [TestMethod]
        public void Load_NoOptions_ReturnsEmpty()
        {
            var adapter = new OptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                if (call[0] is Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithPicklistAttribute_ReturnsRows()
        {
            var adapter = new OptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            var picklist = new PicklistAttributeMetadata
            {
                LogicalName = "new_status",
                SchemaName = "new_status",
                MetadataId = Guid.NewGuid(),
                IsCustomizable = new BooleanManagedProperty(true),
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "Status")),
                OptionSet = new OptionSetMetadata
                {
                    Options = {
                        new OptionMetadata(new Microsoft.Xrm.Sdk.Label("Active", 1033), 1),
                        new OptionMetadata(new Microsoft.Xrm.Sdk.Label("Inactive", 1033), 2)
                    }
                }
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", picklist);
                if (call[0] is Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithBooleanAndInvalidAttributes_ReturnsBooleanRows()
        {
            var adapter = new OptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            var booleanAttribute = new BooleanAttributeMetadata
            {
                LogicalName = "new_flag",
                SchemaName = "new_flag",
                MetadataId = Guid.NewGuid(),
                IsCustomizable = new BooleanManagedProperty(true),
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "Flag")),
                Description = AdapterTestHelpers.BuildLabel((1033, "Flag description")),
                OptionSet = new BooleanOptionSetMetadata(
                    new OptionMetadata(new Label("Yes", 1033), 1),
                    new OptionMetadata(new Label("No", 1033), 0))
            };
            var missingMetadataId = new PicklistAttributeMetadata
            {
                LogicalName = "skip_no_id",
                SchemaName = "skip_no_id",
                OptionSet = new OptionSetMetadata { Options = { new OptionMetadata(new Label("A", 1033), 1) } }
            };
            var emptyLogical = new PicklistAttributeMetadata
            {
                MetadataId = Guid.NewGuid(),
                LogicalName = "",
                SchemaName = "skip_empty",
                OptionSet = new OptionSetMetadata { Options = { new OptionMetadata(new Label("A", 1033), 1) } }
            };
            var nullOptions = new PicklistAttributeMetadata
            {
                MetadataId = Guid.NewGuid(),
                LogicalName = "empty_options",
                SchemaName = "empty_options",
                OptionSet = new OptionSetMetadata()
            };
            nullOptions.OptionSet.Options.Clear();

            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", booleanAttribute, missingMetadataId, emptyLogical, nullOptions, new StringAttributeMetadata { LogicalName = "name" });
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1041);
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "Description" });

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.IsTrue(output.grid.languageColumns.Any(c => c.field == "1033"));
            Assert.AreEqual(2, output.grid.rows.Count);
            var flag = output.grid.rows.First(row => row.SchemaName == "new_flag");
            Assert.AreEqual("Flag description", flag["1033"]);
            var children = (List<EasyTranslatorGridRowOutput>)flag["children"];
            Assert.AreEqual(2, children.Count);
        }

        [TestMethod]
        public void Load_Throws_WhenEntityMetadataMissing()
        {
            var adapter = new OptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return new RetrieveEntityResponse();
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText" }),
                "OptionSet entity metadata was not found.");
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_RowWithNoValidLabelChanges_IsSkipped()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|description",
                        changes = new Dictionary<string, string> { { "not-lcid", "Ignored" } }
                    }
                }
            });

            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
        }

        [TestMethod]
        public void Save_Description_UpdatesAttributeDescription()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveAttributeRequest)
                {
                    return AdapterTestHelpers.BuildAttributeResponse(new PicklistAttributeMetadata
                    {
                        LogicalName = "new_status",
                        DisplayName = AdapterTestHelpers.BuildLabel((1033, "Status")),
                        Description = AdapterTestHelpers.BuildLabel((1033, "Old"))
                    });
                }
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|description",
                        changes = new Dictionary<string, string> { { "1033", "New desc" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(captured is UpdateAttributeRequest || captured is RetrieveAttributeRequest);
        }

        [TestMethod]
        public void Save_InvalidGridKeyPart_Throws()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|badkey",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_OptionRows_UpdateLocalAndGlobalOptions()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var requests = new List<OrganizationRequest>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);
                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|option|1|DisplayText|local|",
                        changes = new Dictionary<string, string> { { "1033", "Active" } }
                    },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_global|option|2|Description|global|new_global_options",
                        changes = new Dictionary<string, string> { { "1033", "Global desc" } }
                    }
                }
            });

            Assert.AreEqual(2, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "entity" && t.id == "account"));
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "globalOptionSet" && t.id == "new_global_options"));
            var local = (UpdateOptionValueRequest)requests[0];
            Assert.AreEqual("account", local.EntityLogicalName);
            Assert.AreEqual("new_status", local.AttributeLogicalName);
            Assert.IsNotNull(local.Label);
            var global = (UpdateOptionValueRequest)requests[1];
            Assert.AreEqual("new_global_options", global.OptionSetName);
            Assert.IsNotNull(global.Description);
        }

        [TestMethod]
        public void Save_OptionRows_ValidateGridKeyAndOptionFields()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|option|1|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            }), "OptionSet option gridKey is incomplete.");

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|option|not-int|DisplayText|local|",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            }), "OptionSet option value is required.");

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|option|1|DisplayText|global| ",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            }), "OptionSet optionSetName is required.");
        }

        [TestMethod]
        public void Save_Description_Throws_WhenAttributeMetadataMissing()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAttributeRequest) return new RetrieveAttributeResponse();
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_status|description",
                        changes = new Dictionary<string, string> { { "1033", "New desc" } }
                    }
                }
            }), "OptionSet attribute metadata was not found.");
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithEntityTarget_ExecutesPublishXml()
        {
            var adapter = new OptionSetAdapter();
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
        public void Publish_WithGlobalOptionSet_ExecutesPublishXml()
        {
            var adapter = new OptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "globalOptionSet", id = "100000" } }
            };
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Published_WaitsAndReturnsTargets()
        {
            var waited = 0;
            OptionSetAdapter.WaitAction = ms => waited = ms;
            var adapter = new OptionSetAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(Substitute.For<IOrganizationService>()), new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    new EasyTranslatorPublishTarget { kind = "globalOptionSet", id = "new_global_options" }
                }
            });

            Assert.AreEqual(10000, waited);
            Assert.AreEqual(2, output.publishTargets.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverOptionSetBranches()
        {
            var adapterType = typeof(OptionSetAdapter);
            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                adapterType.GetMethod("RequireAttributeName", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { " " }));
            Assert.AreEqual("name", AdapterTestHelpers.InvokeStatic<string>(adapterType, "RequireAttributeName", "name"));

            var buildLabel = adapterType.GetMethod("BuildLabel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(buildLabel);
            var label = (Label)buildLabel.Invoke(null, new object[] { null });
            Assert.AreEqual(0, label.LocalizedLabels.Count);

            var buildColumns = adapterType.GetMethod("BuildLanguageColumns", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(buildColumns);
            var columns = (List<EasyTranslatorLanguageColumnOutput>)buildColumns.Invoke(null, new object[] { null });
            Assert.AreEqual(0, columns.Count);

            var addOptionRow = adapterType.GetMethod("AddOptionRow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(addOptionRow);
            var rows = new List<EasyTranslatorGridRowOutput>();
            addOptionRow.Invoke(null, new object[] { rows, "account", "new_status", new OptionSetMetadata(), null, "DisplayText" });
            addOptionRow.Invoke(null, new object[] { rows, "account", "new_status", new OptionSetMetadata(), new OptionMetadata(new Label("No value", 1033), null), "DisplayText" });
            Assert.AreEqual(0, rows.Count);
        }
    }
}
