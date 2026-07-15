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
using System.Reflection;

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
        public void Load_WithCommands_ReturnsSortedTreeRowsAndChildLabelRows()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var parentId = Guid.NewGuid();
            var childId = Guid.NewGuid();
            var skippedId = Guid.Empty;

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "appaction")
                {
                    return AdapterTestHelpers.Entities(
                        MakeCommand(childId, "Child", "aaa.child", 1, 0, "Child Text", parentId),
                        MakeCommand(parentId, "Parent", "aaa.parent", 0, 3, "Parent Text"),
                        MakeCommand(skippedId, "Skipped", "aaa.skipped", 0, 0, "Skipped Text"));
                }

                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, "Localized")));
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(2, output.grid.rows.Count);
            Assert.AreEqual("Form / Group: Parent Text", output.grid.rows[0].SchemaName);
            Assert.AreEqual("Main Grid / Group: Parent Text / Button: Child Text", output.grid.rows[1].SchemaName);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(5, children.Count);
            Assert.AreEqual("Accessibility Text", children[0].SchemaName);
            Assert.AreEqual("Parent accessibility", children[0]["1033"]);
        }

        [TestMethod]
        public void Load_WithSolutionFilter_ReturnsOnlySolutionCommands()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var includedId = Guid.NewGuid();
            var excludedId = Guid.NewGuid();
            var solutionId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("solutioncomponent") { ["objectid"] = includedId },
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty });
                }
                if (q.EntityName == "appaction")
                {
                    return AdapterTestHelpers.Entities(
                        MakeCommand(includedId, "Included", "included.command", 0, 0, "Included Text"),
                        MakeCommand(excludedId, "Excluded", "excluded.command", 0, 0, "Excluded Text"));
                }

                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
                call[0] is RetrieveLocLabelsRequest
                    ? AdapterTestHelpers.RetrieveLabelsResponse(new Label())
                    : new OrganizationResponse());

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = solutionId.ToString("D")
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.IsTrue(output.grid.rows[0].SchemaName.Contains("Included"));
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
        public void Save_SkipsRowsWithNoValidLabelChanges()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{Guid.NewGuid():D}|buttonlabeltext|account",
                        changes = new Dictionary<string, string> { { "not-a-language", "Ignored" } }
                    }
                }
            });

            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
        }

        [TestMethod]
        public void Save_NullRowsAndFallbackEntityPublishTarget_AreHandled()
        {
            var adapter = new CommandAdapter();
            var service = CreateServiceWithBaseLanguage();
            var empty = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, empty.changedRowCount);

            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
                call[0] is RetrieveLocLabelsRequest
                    ? AdapterTestHelpers.RetrieveLabelsResponse(new Label())
                    : new OrganizationResponse());

            var commandId = Guid.NewGuid();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"commands|{commandId:D}|buttonlabeltext| ",
                        changes = new Dictionary<string, string> { { "1041", "JP" } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
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

        [TestMethod]
        public void Published_WithTargets_WaitsAndReturnsTargets()
        {
            var waited = 0;
            CommandAdapter.WaitAction = ms => waited = ms;
            var adapter = new CommandAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Published(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" }
                }
            });

            Assert.AreEqual(10000, waited);
            Assert.AreEqual(1, output.publishTargets.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverCommandAdapterFallbackBranches()
        {
            var adapterType = typeof(CommandAdapter);
            var commandId = Guid.NewGuid();
            var command = MakeCommand(commandId, null, null, 99, 99, null);
            command["buttonlabeltext"] = new AliasedValue("appaction", "buttonlabeltext", "Aliased Text");
            Assert.AreEqual("Aliased Text", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, "buttonlabeltext"));

            command["buttonlabeltext"] = new Label("User Label", 1033);
            Assert.AreEqual("User Label", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, "buttonlabeltext"));

            command["buttonlabeltext"] = AdapterTestHelpers.BuildLabel((1041, "First Localized"));
            Assert.AreEqual("First Localized", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, "buttonlabeltext"));

            command["buttonlabeltext"] = 123;
            Assert.AreEqual("123", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, "buttonlabeltext"));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", null, "buttonlabeltext"));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, ""));
            Assert.AreEqual("Type 99", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCommandTypeText", command));
            Assert.AreEqual("Location 99", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetLocationText", command));
            Assert.AreEqual("007", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSortNumber", 7, 3));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "FirstNonEmpty", new object[] { new[] { "", " ", null } }));

            var decimalEntity = new Entity("appaction");
            decimalEntity["d"] = 1.25m;
            decimalEntity["double"] = 2.5d;
            decimalEntity["int"] = 3;
            decimalEntity["other"] = "not decimal";
            Assert.AreEqual(1.25m, AdapterTestHelpers.InvokeStatic<decimal>(adapterType, "GetDecimalValue", decimalEntity, "d"));
            Assert.AreEqual(2.5m, AdapterTestHelpers.InvokeStatic<decimal>(adapterType, "GetDecimalValue", decimalEntity, "double"));
            Assert.AreEqual(3m, AdapterTestHelpers.InvokeStatic<decimal>(adapterType, "GetDecimalValue", decimalEntity, "int"));
            Assert.AreEqual(0m, AdapterTestHelpers.InvokeStatic<decimal>(adapterType, "GetDecimalValue", decimalEntity, "missing"));
            Assert.AreEqual(0m, AdapterTestHelpers.InvokeStatic<decimal>(adapterType, "GetDecimalValue", decimalEntity, "other"));

            var map = new Dictionary<Guid, Entity>();
            command["appactionid"] = commandId;
            command["name"] = null;
            command["uniquename"] = null;
            map[commandId] = command;
            Assert.AreEqual(commandId.ToString("D").ToLowerInvariant(), AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCommandLogicalSortPath", command, map));

            var emptyIdCommand = MakeCommand(Guid.Empty, "Empty", "empty", 0, 0, "Empty");
            Assert.AreEqual("Form / ", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCommandNodeText", emptyIdCommand, map));

            var parentReferenceCommand = MakeCommand(commandId, "Broken", "broken", 0, 0, "Broken", Guid.NewGuid());
            Assert.AreEqual(commandId.ToString("D"), AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCommandLogicalSortPathSafe", parentReferenceCommand, null));

            var userLabel = new Label();
            var userLocalizedProperty = typeof(Label).GetProperty("UserLocalizedLabel");
            Assert.IsNotNull(userLocalizedProperty);
            userLocalizedProperty.SetValue(userLabel, new LocalizedLabel("User Label", 1033));
            command["buttonlabeltext"] = userLabel;
            Assert.AreEqual("User Label", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeText", command, "buttonlabeltext"));

            var currentLabel = AdapterTestHelpers.BuildLabel((1033, "Old"), (1033, "Duplicate"));
            currentLabel.LocalizedLabels.Insert(0, null);
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel", currentLabel, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = "JP" },
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "EN" }
            });
            Assert.AreEqual(2, merged.Length);
            Assert.AreEqual("EN", merged.First(label => label.LanguageCode == 1033).Label);

            var method = adapterType.GetMethod("ValidateGuid", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            try
            {
                method.Invoke(null, new object[] { string.Empty, "Command appactionid" });
                Assert.Fail("Expected InvalidPluginExecutionException.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidPluginExecutionException));
                Assert.IsTrue(ex.InnerException.Message.Contains("Command appactionid must be a GUID."));
            }
        }

        private static Entity MakeCommand(
            Guid id,
            string name,
            string uniqueName,
            int location,
            int type,
            string buttonText,
            Guid? parentId = null)
        {
            var command = new Entity("appaction") { Id = id };
            command["appactionid"] = id;
            command["name"] = name;
            command["uniquename"] = uniqueName;
            command["location"] = new OptionSetValue(location);
            command["type"] = new OptionSetValue(type);
            command["buttonlabeltext"] = buttonText;
            command["buttontooltiptitle"] = name == null ? null : name + " title";
            command["buttontooltipdescription"] = name == null ? null : name + " description";
            command["buttonaccessibilitytext"] = name == null ? null : name + " accessibility";
            command["grouptitle"] = name == null ? null : name + " group";
            if (parentId.HasValue)
            {
                command["parentappactionid"] = new EntityReference("appaction", parentId.Value);
            }

            return command;
        }
    }
}
