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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class RibbonAdapterTests
    {
        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities(MakeEntity("account", false));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            return service;
        }

        private static Entity MakeEntity(string name, bool isCustom)
        {
            var e = new Entity("entity") { Id = Guid.NewGuid() };
            e["name"] = name;
            e["iscustomentity"] = isCustom;
            return e;
        }

        private static EntityMetadata CreateAccountMetadata()
        {
            var metadata = new EntityMetadata
            {
                LogicalName = "account",
                SchemaName = "Account",
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account"))
            };
            typeof(EntityMetadata).GetProperty("MetadataId").SetValue(metadata, Guid.NewGuid());
            return metadata;
        }

        private static byte[] BuildSolutionZip()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ImportExportXml>
  <Entities>
    <Entity>
      <Name>account</Name>
      <RibbonDiffXml>
        <CustomActions>
          <CustomAction Id=""account.HomepageGrid.Button.CustomAction"" Location=""Mscrm.HomepageGrid.account.MainTab.Management.Controls._children"">
            <CommandUIDefinition>
              <Button Id=""account.Button"" Command=""account.Command"" LabelText=""$LocLabels:account.Button.LabelText"" ToolTipTitle=""$LocLabels:account.Button.ToolTipTitle"" />
            </CommandUIDefinition>
          </CustomAction>
          <CustomAction Id=""account.Form.Button.CustomAction"" Location=""Mscrm.Form.account.MainTab.Management.Controls._children"">
            <CommandUIDefinition>
              <FlyoutAnchor Id=""account.Flyout"" Command=""account.Command"" LabelText=""$LocLabels:account.Flyout.LabelText"" ToolTipDescription=""$LocLabels:account.Flyout.ToolTipDescription"" />
            </CommandUIDefinition>
          </CustomAction>
        </CustomActions>
        <LocLabels>
          <LocLabel Id=""account.Button.LabelText""><Titles><Title languagecode=""1033"" description=""Account button"" /></Titles></LocLabel>
          <LocLabel Id=""account.Button.ToolTipTitle""><Titles><Title languagecode=""1033"" description=""Button title"" /></Titles></LocLabel>
          <LocLabel Id=""account.Flyout.LabelText""><Titles><Title languagecode=""1033"" description=""Flyout text"" /></Titles></LocLabel>
          <LocLabel Id=""account.Flyout.ToolTipDescription""><Titles><Title languagecode=""1033"" description=""Flyout description"" /></Titles></LocLabel>
        </LocLabels>
      </RibbonDiffXml>
    </Entity>
  </Entities>
</ImportExportXml>";
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("customizations.xml");
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(xml);
                    }
                }
                return ms.ToArray();
            }
        }

        private static byte[] BuildSolutionZip(string customizationsXml)
        {
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("customizations.xml");
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(customizationsXml);
                    }
                }

                return ms.ToArray();
            }
        }

        private static byte[] BuildZip(string entryName, string content)
        {
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry(entryName);
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(content);
                    }
                }

                return ms.ToArray();
            }
        }

        private static IOrganizationService CreateSolutionService()
        {
            var service = CreateService();
            var solutionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            service.Retrieve("solution", solutionId, Arg.Any<ColumnSet>()).Returns(new Entity("solution", solutionId)
            {
                ["uniquename"] = "RibbonSolution"
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033, 1041);
                if (call[0] is RetrieveEntityRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                if (call[0] is ExportSolutionRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportSolutionResponse(), "ExportSolutionFile", BuildSolutionZip());
                if (call[0] is ImportSolutionAsyncRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new ImportSolutionAsyncResponse(), "AsyncOperationId", Guid.NewGuid());
                if (call[0] is PublishAllXmlAsyncRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new PublishAllXmlAsyncResponse(), "AsyncOperationId", Guid.NewGuid());
                return new OrganizationResponse();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new RibbonAdapter();
            var service = CreateService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_ExecutesAsync()
        {
            var adapter = new RibbonAdapter();
            var service = CreateService();
            var response = new Microsoft.Crm.Sdk.Messages.PublishAllXmlAsyncResponse();
            response.Results["AsyncOperationId"] = Guid.NewGuid();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is PublishAllXmlAsyncRequest) return response;
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.IsNotNull(output.publish);
        }

        [TestMethod]
        public void Publish_NoTargets_CallsPublishAll()
        {
            var adapter = new RibbonAdapter();
            var service = CreateService();
            OrganizationRequest captured = null;
            var response = new Microsoft.Crm.Sdk.Messages.PublishAllXmlAsyncResponse();
            response.Results["AsyncOperationId"] = Guid.NewGuid();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (call[0] is PublishAllXmlAsyncRequest) return response;
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.IsTrue(captured is PublishAllXmlAsyncRequest);
        }

        [TestMethod]
        public void Load_SolutionRibbon_ReturnsControlRows()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            });

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual("Ribbons", output.grid.title);
            Assert.AreEqual(2, output.grid.rows.Count);
            Assert.IsTrue(((List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"]).Count > 0);
        }

        [TestMethod]
        public void Save_SolutionRibbon_ImportsUpdatedSolution()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            var loaded = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            });
            var row = ((List<EasyTranslatorGridRowOutput>)loaded.grid.rows[0]["children"])[0];

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = row.GridKey,
                        changes = new Dictionary<string, string> { ["1041"] = "更新済み" }
                    }
                }
            });

            Assert.IsTrue(output.changed);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(output.import);
            Assert.AreEqual("ribbons", output.import.translatorType);
        }

        [TestMethod]
        public void Published_ReturnsEmpty()
        {
            var adapter = new RibbonAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(CreateService()), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.changedRowCount);
            Assert.IsFalse(output.changed);
        }

        [TestMethod]
        public void Load_Throws_WhenEntityNodeMissing()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                if (call[0] is ExportSolutionRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportSolutionResponse(), "ExportSolutionFile", BuildSolutionZip(@"<ImportExportXml><Entities><Entity><Name>contact</Name><RibbonDiffXml /></Entity></Entities></ImportExportXml>"));
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "was not found");
        }

        [TestMethod]
        public void Save_NoXmlChange_ReturnsEmpty()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            var loaded = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            });
            var row = ((List<EasyTranslatorGridRowOutput>)loaded.grid.rows[0]["children"])[0];

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = row.GridKey,
                        changes = new Dictionary<string, string> { ["1033"] = Convert.ToString(row["1033"]) }
                    }
                }
            });

            Assert.IsFalse(output.changed);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridTarget_Throws()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "ribbons|control|account|form|Button|account.Button|LabelText|account.Button.LabelText",
                        changes = new Dictionary<string, string> { ["1033"] = "X" }
                    }
                }
            }), "label row");
        }

        [TestMethod]
        public void Save_InvalidLanguageChange_ReturnsEmpty()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "ribbons|label|account|form|Button|account.Button|LabelText|account.Button.LabelText",
                        changes = new Dictionary<string, string> { ["not-lcid"] = "X" }
                    }
                }
            });

            Assert.IsFalse(output.changed);
            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Retrieve("solution", Arg.Any<Guid>(), Arg.Any<ColumnSet>());
        }


        [TestMethod]
        public void Load_Throws_WhenSolutionMissingOrUnnamed()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "all"
            }), "select a solution");

            service = CreateSolutionService();
            service.Retrieve("solution", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(new Entity("solution", Guid.NewGuid()));
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "unique name");
        }

        [TestMethod]
        public void Load_Throws_WhenEntityMetadataMissingOrNameMissing()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest) return new RetrieveEntityResponse();
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "Could not resolve metadata");

            service = CreateSolutionService();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "none",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "select an entity");
        }

        [TestMethod]
        public void Load_Throws_WhenExportZipInvalid()
        {
            var adapter = new RibbonAdapter();
            var service = CreateSolutionService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                if (call[0] is ExportSolutionRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportSolutionResponse(), "ExportSolutionFile", Array.Empty<byte>());
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "ZIP processing failed");

            service = CreateSolutionService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is RetrieveEntityRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                if (call[0] is ExportSolutionRequest)
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportSolutionResponse(), "ExportSolutionFile", BuildZip("other.xml", "<root />"));
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "22222222-2222-2222-2222-222222222222"
            }), "ZIP processing failed");
        }

        [TestMethod]
        public void PrivateHelpers_CoverRibbonXmlBranches()
        {
            var adapterType = typeof(RibbonAdapter);
            var xml = @"<ImportExportXml><Entities><Entity><Name>account</Name><RibbonDiffXml><CustomActions><CustomAction Location=""Mscrm.SubGrid.account.Controls._children""><CommandUIDefinition><SplitButton Id=""account.Split"" Command=""account.Command"" /></CommandUIDefinition></CustomAction></CustomActions></RibbonDiffXml></Entity></Entities></ImportExportXml>";

            ExpectReflectionPluginException(() =>
                AdapterTestHelpers.InvokeStatic<string>(adapterType, "ApplyRibbonXmlChanges", xml, "contact", CreateRibbonUpdates("account.Split", "LabelText", "account.Split.LabelText")),
                "was not found");

            ExpectReflectionPluginException(() =>
                AdapterTestHelpers.InvokeStatic<string>(adapterType, "ApplyRibbonXmlChanges", "<ImportExportXml><Entities><Entity><Name>account</Name></Entity></Entities></ImportExportXml>", "account", CreateRibbonUpdates("account.Split", "LabelText", "account.Split.LabelText")),
                "RibbonDiffXml");

            ExpectReflectionPluginException(() =>
                AdapterTestHelpers.InvokeStatic<string>(adapterType, "ApplyRibbonXmlChanges", xml, "account", CreateRibbonUpdates("missing.Control", "LabelText", "missing.LabelText")),
                "was not found");

            var updated = AdapterTestHelpers.InvokeStatic<string>(
                adapterType,
                "ApplyRibbonXmlChanges",
                xml,
                "account",
                CreateRibbonUpdates("account.Split", "LabelText", "account.Split.LabelText"));
            StringAssert.Contains(updated, "LocLabel");
            StringAssert.Contains(updated, "Updated");

            updated = AdapterTestHelpers.InvokeStatic<string>(
                adapterType,
                "ApplyRibbonXmlChanges",
                xml,
                "account",
                CreateRibbonUpdates("account.Split", "LabelText", "account.Split.LabelText", "form"));
            StringAssert.Contains(updated, "Updated");

            ExpectReflectionPluginException(() =>
                AdapterTestHelpers.InvokeStatic<object>(adapterType, "ParseXml", "<not-closed"),
                "Invalid XML");

            var doc = XDocument.Parse(xml);
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "GetDirectChild", new object[] { null, "RibbonDiffXml" }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttribute", new object[] { null, "Id" }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "DetectSurface", new object[] { null }));
            Assert.AreEqual("Subgrid", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSurfaceLabel", "sub_grid"));
            Assert.AreEqual("Ribbon", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSurfaceLabel", string.Empty));
            Assert.AreEqual("Ribbon", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSurfaceLabel", "other"));
            Assert.AreEqual("LabelText", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetDefaultLocLabelId", string.Empty, "LabelText"));
            Assert.AreEqual(0, AdapterTestHelpers.InvokeStatic<IEnumerable<XElement>>(adapterType, "Descendants", new object[] { null }).Count());
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsControlNode", new object[] { null }));
            var map = AdapterTestHelpers.InvokeStatic<Dictionary<string, Dictionary<string, string>>>(
                adapterType,
                "BuildLocLabelMap",
                XElement.Parse("<RibbonDiffXml><LocLabels><LocLabel><Titles><Title languagecode=\"1033\" description=\"No id\" /></Titles></LocLabel></LocLabels></RibbonDiffXml>"));
            Assert.AreEqual(0, map.Count);

            var locLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var button = new XElement("Button", new XAttribute("Id", "account.Button"));
            Assert.AreEqual("Button", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetControlPreviewLabel", button, locLabels, 1033));
            button.SetAttributeValue("LabelText", "$LocLabels:empty");
            locLabels["empty"] = new Dictionary<string, string>();
            Assert.AreEqual("Button", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetControlPreviewLabel", button, locLabels, 1033));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetFirstLabel", new object[] { null, 1033 }));
            Assert.AreEqual("Fallback", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetFirstLabel", new Dictionary<string, string> { ["1041"] = "Fallback" }, 1033));

            var entityInfo = CreateRibbonEntityInfo(adapterType);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var control = XElement.Parse("<Button Id=\"account.Dedupe\" Command=\"account.Command\" LabelText=\"$LocLabels:account.Dedupe.LabelText\" />");
            var locLabelMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["account.Dedupe.LabelText"] = new Dictionary<string, string> { ["1033"] = "Dedupe" }
            };
            var children = AdapterTestHelpers.InvokeStatic<List<EasyTranslatorGridRowOutput>>(
                adapterType,
                "BuildControlChildren",
                control,
                entityInfo,
                locLabelMap,
                new List<int> { 1033 },
                1,
                seen);
            Assert.AreEqual(3, children.Count);
            var duplicateChildren = AdapterTestHelpers.InvokeStatic<List<EasyTranslatorGridRowOutput>>(
                adapterType,
                "BuildControlChildren",
                control,
                entityInfo,
                locLabelMap,
                new List<int> { 1033 },
                2,
                seen);
            Assert.AreEqual(0, duplicateChildren.Count);

            var locLabelsNode = new XElement("LocLabels", new XElement("LocLabel", new XAttribute("Id", "existing")));
            var changed = false;
            var existing = InvokeWithRefBool<XElement>(adapterType, "GetOrCreateLocLabel", locLabelsNode, "existing", changed, out changed);
            Assert.AreEqual("existing", existing.Attribute("Id").Value);
            Assert.IsFalse(changed);

            var titles = new XElement("Titles", new XElement("Title", new XAttribute("languagecode", "1033")));
            changed = false;
            var title = InvokeWithRefBool<XElement>(adapterType, "GetOrCreateTitle", titles, "1033", changed, out changed);
            Assert.AreEqual("1033", title.Attribute("languagecode").Value);
            Assert.IsFalse(changed);

            var tracing = Substitute.For<ITracingService>();
            AdapterTestHelpers.InvokeStatic(adapterType, "Trace", new EasyTranslatorRuntimeContext { Tracing = tracing }, "Hello", new object[0]);
            tracing.Received().Trace("Hello");
        }

        private static object CreateRibbonUpdates(string controlId, string property, string locLabelId, string surface = "sub_grid")
        {
            var adapterType = typeof(RibbonAdapter);
            var updateType = adapterType.GetNestedType("RibbonLabelUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType);
            updateType.GetProperty("EntityLogicalName").SetValue(update, "account");
            updateType.GetProperty("Surface").SetValue(update, surface);
            updateType.GetProperty("Component").SetValue(update, "SplitButton");
            updateType.GetProperty("ControlId").SetValue(update, controlId);
            updateType.GetProperty("Property").SetValue(update, property);
            updateType.GetProperty("LocLabelId").SetValue(update, locLabelId);
            var labels = (List<EasyTranslatorLabelChange>)updateType.GetProperty("Labels").GetValue(update);
            labels.Add(new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Updated" });
            var listType = typeof(List<>).MakeGenericType(updateType);
            var list = Activator.CreateInstance(listType);
            listType.GetMethod("Add").Invoke(list, new[] { update });
            return list;
        }

        private static object CreateRibbonEntityInfo(Type adapterType)
        {
            var entityInfoType = adapterType.GetNestedType("RibbonEntityInfo", BindingFlags.NonPublic);
            var entityInfo = Activator.CreateInstance(entityInfoType);
            entityInfoType.GetProperty("LogicalName").SetValue(entityInfo, "account");
            entityInfoType.GetProperty("SchemaName").SetValue(entityInfo, "Account");
            entityInfoType.GetProperty("MetadataId").SetValue(entityInfo, Guid.NewGuid());
            return entityInfo;
        }

        private static T InvokeWithRefBool<T>(Type type, string method, params object[] fixedArgsAndRef)
        {
            var args = fixedArgsAndRef;
            var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(methodInfo);
            return (T)methodInfo.Invoke(null, args);
        }

        private static void ExpectReflectionPluginException(Action action, string expectedMessagePart)
        {
            try
            {
                action();
                Assert.Fail("Expected InvalidPluginExecutionException");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidPluginExecutionException pluginException)
            {
                StringAssert.Contains(pluginException.Message, expectedMessagePart);
            }
        }

        private static T InvokeWithRefBool<T>(Type type, string method, XElement node, string value, bool initial, out bool changed)
        {
            var args = new object[] { node, value, initial };
            var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(methodInfo);
            var result = (T)methodInfo.Invoke(null, args);
            changed = (bool)args[2];
            return result;
        }
    }
}
