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
    public class GlobalOptionSetAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { GlobalOptionSetAdapter.WaitAction = System.Threading.Thread.Sleep; }

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

        private static T SetGlobal<T>(T metadata, bool? value = true) where T : OptionSetMetadataBase
        {
            var property = typeof(OptionSetMetadataBase).GetProperty("IsGlobal", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            property.SetValue(metadata, value);
            return metadata;
        }

        private static RetrieveOptionSetResponse OptionSetResponse(OptionSetMetadataBase metadata)
        {
            return AdapterTestHelpers.SetResultsAndReturn(
                new RetrieveOptionSetResponse(),
                "OptionSetMetadata",
                metadata);
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionIdInvalid()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "not-a-guid" }));
        }

        [TestMethod]
        public void Load_AllSolution_ReturnsEmptyGrid()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptySolutionId_ReturnsEmpty()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_Solution_CoversEligibilityPicklistBooleanAndSorting()
        {
            var solutionId = Guid.NewGuid();
            var nullId = Guid.NewGuid();
            var noCustomizableId = Guid.NewGuid();
            var disabledId = Guid.NewGuid();
            var nonglobalId = Guid.NewGuid();
            var picklistId = Guid.NewGuid();
            var booleanId = Guid.NewGuid();
            var service = Substitute.For<IOrganizationService>();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity(1041));
                }

                if (query.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("solutioncomponent"),
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty },
                        new Entity("solutioncomponent") { ["objectid"] = nullId },
                        new Entity("solutioncomponent") { ["objectid"] = noCustomizableId },
                        new Entity("solutioncomponent") { ["objectid"] = disabledId },
                        new Entity("solutioncomponent") { ["objectid"] = nonglobalId },
                        new Entity("solutioncomponent") { ["objectid"] = booleanId },
                        new Entity("solutioncomponent") { ["objectid"] = picklistId });
                }

                return AdapterTestHelpers.Entities();
            });

            var picklist = SetGlobal(new OptionSetMetadata
            {
                Name = "pl_z_picklist",
                IsCustomizable = new BooleanManagedProperty(true),
                Description = AdapterTestHelpers.BuildLabel((1033, "Picklist description")),
                Options =
                {
                    new OptionMetadata(AdapterTestHelpers.BuildLabel((1033, "Second")), 2),
                    new OptionMetadata(AdapterTestHelpers.BuildLabel((1033, "First")), 1),
                    new OptionMetadata(),
                    null
                }
            });
            picklist.Options[0].Description = AdapterTestHelpers.BuildLabel((1033, "Second description"));

            var boolean = SetGlobal(new BooleanOptionSetMetadata
            {
                Name = "pl_a_boolean",
                IsCustomizable = new BooleanManagedProperty(true),
                TrueOption = new OptionMetadata(AdapterTestHelpers.BuildLabel((1033, "Yes")), 1),
                FalseOption = new OptionMetadata(AdapterTestHelpers.BuildLabel((1033, "No")), 0)
            });

            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (RetrieveOptionSetRequest)call[0];
                if (request.MetadataId == nullId) return OptionSetResponse(null);
                if (request.MetadataId == noCustomizableId) return OptionSetResponse(SetGlobal(new OptionSetMetadata { Name = "no_customizable" }));
                if (request.MetadataId == disabledId) return OptionSetResponse(SetGlobal(new OptionSetMetadata { Name = "disabled", IsCustomizable = new BooleanManagedProperty(false) }));
                if (request.MetadataId == nonglobalId) return OptionSetResponse(SetGlobal(new OptionSetMetadata { Name = "nonglobal", IsCustomizable = new BooleanManagedProperty(true) }, false));
                if (request.MetadataId == booleanId) return OptionSetResponse(boolean);
                if (request.MetadataId == picklistId) return OptionSetResponse(picklist);
                return new OrganizationResponse();
            });

            var adapter = new GlobalOptionSetAdapter();
            var display = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { solutionId = solutionId.ToString(), component = "DisplayText" });
            var description = adapter.Load(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorLoadInput { solutionId = solutionId.ToString(), component = "Description" });

            Assert.AreEqual("1041", display.baseLanguage);
            Assert.AreEqual("Global Option Sets", display.grid.title);
            Assert.AreEqual(2, display.grid.rows.Count);
            Assert.AreEqual("pl_a_boolean", display.grid.rows[0].SchemaName);
            Assert.AreEqual(2, ((List<EasyTranslatorGridRowOutput>)display.grid.rows[0]["children"]).Count);
            Assert.AreEqual("pl_z_picklist", display.grid.rows[1].SchemaName);
            var displayChildren = (List<EasyTranslatorGridRowOutput>)display.grid.rows[1]["children"];
            var descriptionChildren = (List<EasyTranslatorGridRowOutput>)description.grid.rows[1]["children"];
            Assert.AreEqual(2, displayChildren.Count);
            Assert.AreEqual("1", displayChildren[0].SchemaName);
            Assert.AreEqual("First", displayChildren[0]["1033"]);
            Assert.IsFalse((bool)display.grid.rows[1]["isEditable"]);
            Assert.IsTrue((bool)description.grid.rows[1]["isEditable"]);
            Assert.AreEqual("Picklist description", description.grid.rows[1]["1033"]);
            Assert.AreEqual("Second description", descriptionChildren[1]["1033"]);
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_Description_UpdatesOptionSet()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (captured is RetrieveOptionSetRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveOptionSetResponse(), "OptionSetMetadata", new OptionSetMetadata { Name = "pl_test" });
                }
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|description",
                        changes = new Dictionary<string, string> { { "1033", "Test desc" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
        }

        [TestMethod]
        public void Save_OptionLabel_UpdatesOptionValue()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|option|1|label",
                        changes = new Dictionary<string, string> { { "1033", "Label" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsInstanceOfType(captured, typeof(UpdateOptionValueRequest));
        }

        [TestMethod]
        public void Save_OptionDescription_SkipsInvalidChangesAndDeduplicatesPublishTarget()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var requests = new List<OrganizationRequest>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                requests.Add((OrganizationRequest)call[0]);
                return new OrganizationResponse();
            });

            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "globalOptionSet|pl_test|option|1|label" },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|option|1|description",
                        changes = new Dictionary<string, string> { { "not-a-language", "ignored" }, { "1041", null } }
                    },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|option|2|description",
                        changes = new Dictionary<string, string> { { "1041", "Description" } }
                    },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|option|3|label",
                        changes = new Dictionary<string, string> { { "1041", "Label" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            var optionRequests = requests.Cast<UpdateOptionValueRequest>().ToList();

            Assert.AreEqual(3, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual(3, optionRequests.Count);
            Assert.IsNotNull(optionRequests[0].Description);
            Assert.IsNull(optionRequests[0].Label);
            Assert.IsNotNull(optionRequests[2].Label);
        }

        [TestMethod]
        public void Save_NullRowsAndEmptyOptionSetName_AreHandled()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();

            var empty = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, empty.changedRowCount);

            var invalid = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet||description",
                        changes = new Dictionary<string, string> { { "1033", "Description" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), invalid),
                "optionSetName is required");
        }

        [TestMethod]
        public void Save_InvalidGridKey_Throws()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|invalid",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));

            input.changedRows[0].gridKey = "globalOptionSet|pl_test";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));

            input.changedRows[0].gridKey = "globalOptionSet|pl_test|invalid|1|label";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_OptionValueInvalid_Throws()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "globalOptionSet|pl_test|option|notanumber|label",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTarget_ExecutesPublishXml()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "globalOptionSet", id = "pl_test" } }
            };
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void PublishAndPublished_CoverWaitTargetsAndInvalidName()
        {
            var adapter = new GlobalOptionSetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var waits = new List<int>();
            GlobalOptionSetAdapter.WaitAction = milliseconds => waits.Add(milliseconds);

            var noTargets = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            var published = adapter.Published(
                AdapterTestHelpers.Context(service),
                new EasyTranslatorPublishInput
                {
                    publishTargets = new List<EasyTranslatorPublishTarget>
                    {
                        new EasyTranslatorPublishTarget { kind = "globalOptionSet", id = "pl_test" }
                    }
                });

            Assert.AreEqual(0, noTargets.publishTargets.Count);
            Assert.AreEqual(1, published.publishTargets.Count);
            CollectionAssert.AreEqual(new[] { 10000, 10000 }, waits);

            try
            {
                AdapterTestHelpers.InvokeStatic<string>(
                    typeof(GlobalOptionSetAdapter),
                    "BuildPublishXml",
                    new List<string> { " " });
                Assert.Fail("Expected invalid option-set name.");
            }
            catch (TargetInvocationException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(InvalidPluginExecutionException));
            }
        }

        [TestMethod]
        public void PrivateGuards_CoverNullLabelsOptionsAndEligibility()
        {
            var type = typeof(GlobalOptionSetAdapter);
            var rows = new List<EasyTranslatorGridRowOutput>();

            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(type, "IsEligibleGlobalOptionSet", (object)null));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(type, "IsEligibleGlobalOptionSet", SetGlobal(new OptionSetMetadata())));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(type, "IsEligibleGlobalOptionSet", SetGlobal(new OptionSetMetadata { IsCustomizable = new BooleanManagedProperty(false) })));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(type, "IsEligibleGlobalOptionSet", SetGlobal(new OptionSetMetadata { IsCustomizable = new BooleanManagedProperty(true) }, false)));
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<bool>(type, "IsEligibleGlobalOptionSet", SetGlobal(new OptionSetMetadata { IsCustomizable = new BooleanManagedProperty(true) })));

            AdapterTestHelpers.InvokeStatic(type, "AddOptionRow", rows, "pl_test", null, "DisplayText");
            AdapterTestHelpers.InvokeStatic(type, "AddOptionRow", rows, "pl_test", new OptionMetadata(), "DisplayText");
            Assert.AreEqual(0, rows.Count);

            var label = AdapterTestHelpers.InvokeStatic<Label>(type, "BuildLabel", (object)null);
            Assert.AreEqual(0, label.LocalizedLabels.Count);

            label = AdapterTestHelpers.InvokeStatic<Label>(
                type,
                "BuildLabel",
                new List<EasyTranslatorLabelChange>
                {
                    new EasyTranslatorLabelChange { LanguageCode = 1033, Label = null }
                });
            Assert.AreEqual(string.Empty, label.LocalizedLabels[0].Label);
        }
    }
}
