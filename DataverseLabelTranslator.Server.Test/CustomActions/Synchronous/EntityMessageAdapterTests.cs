using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class EntityMessageAdapterTests
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

        private static byte[] BuildTranslationZip()
        {
            var xml = @"<?xml version=""1.0""?>
<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet"" xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet"">
  <Worksheet ss:Name=""Display Strings"">
    <Table>
      <Row>
        <Cell><Data ss:Type=""String"">Display String Key</Data></Cell>
        <Cell><Data ss:Type=""String"">Default Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">Custom Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">Published Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">1033</Data></Cell>
        <Cell ss:Index=""7""><Data ss:Type=""String"">1041</Data></Cell>
        <Cell><Data ss:Type=""String"">Object Type Code</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String"">account.display.name</Data></Cell>
        <Cell><Data ss:Type=""String"">Account</Data></Cell>
        <Cell><Data ss:Type=""String"">Account custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Account published</Data></Cell>
        <Cell><Data ss:Type=""String"">Account</Data></Cell>
        <Cell ss:Index=""7""><Data ss:Type=""String"">取引先企業</Data></Cell>
        <Cell><Data ss:Type=""String"">account</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String"">contact.display.name</Data></Cell>
        <Cell><Data ss:Type=""String"">Contact</Data></Cell>
        <Cell><Data ss:Type=""String"">Contact custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Contact published</Data></Cell>
        <Cell><Data ss:Type=""String"">Contact</Data></Cell>
        <Cell ss:Index=""7""><Data ss:Type=""String"">連絡先</Data></Cell>
        <Cell><Data ss:Type=""String"">contact</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>";

            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("CrmTranslations.xml");
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(xml);
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

        private static byte[] BuildTranslationZipWithout1041()
        {
            var xml = @"<?xml version=""1.0""?>
<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet"" xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet"">
  <Worksheet ss:Name=""Display Strings"">
    <Table>
      <Row>
        <Cell><Data ss:Type=""String"">Display String Key</Data></Cell>
        <Cell><Data ss:Type=""String"">Default Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">Custom Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">Published Display String</Data></Cell>
        <Cell><Data ss:Type=""String"">1033</Data></Cell>
        <Cell><Data ss:Type=""String"">Object Type Code</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String"">account.display.name</Data></Cell>
        <Cell><Data ss:Type=""String"">Account</Data></Cell>
        <Cell><Data ss:Type=""String"">Account custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Account published</Data></Cell>
        <Cell><Data ss:Type=""String"">Account</Data></Cell>
        <Cell><Data ss:Type=""String"">account</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>";
            return BuildZip("CrmTranslations.xml", xml);
        }

        private static IOrganizationService CreateTranslationPackageService(byte[] packageBytes)
        {
            var service = CreateService();
            var solutionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            service.Retrieve("solution", solutionId, Arg.Any<ColumnSet>()).Returns(new Entity("solution", solutionId)
            {
                ["uniquename"] = "BaseSolution"
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities(MakeEntity("account", false));
                if (q.EntityName == "displaystring")
                {
                    return AdapterTestHelpers.Entities(new Entity("displaystring")
                    {
                        ["displaystringid"] = Guid.NewGuid(),
                        ["displaystringkey"] = "account.display.name"
                    });
                }
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                }
                if (call[0] is ExportTranslationRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportTranslationResponse(), "ExportTranslationFile", packageBytes);
                }
                if (call[0] is ImportTranslationRequest)
                {
                    return new ImportTranslationResponse();
                }
                return new OrganizationResponse();
            });
            return service;
        }

        private static EntityMetadata CreateAccountMetadata()
        {
            var metadata = new EntityMetadata
            {
                LogicalName = "account",
                SchemaName = "Account",
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account")),
                DisplayCollectionName = AdapterTestHelpers.BuildLabel((1033, "Accounts"))
            };
            typeof(EntityMetadata).GetProperty("ObjectTypeCode").SetValue(metadata, 1);
            typeof(EntityMetadata).GetProperty("IsCustomEntity").SetValue(metadata, false);
            return metadata;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Published_WithTargets_ReturnsOutputOnly()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };

            var output = adapter.Published(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.publishTargets.Count);
            service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
        }

        [TestMethod]
        public void Load_NoEntity_Throws()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" }));
        }

        [TestMethod]
        public void Load_CustomEntity_ReturnsEmpty()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            var em = new EntityMetadata { LogicalName = "new_custom" };
            typeof(EntityMetadata).GetProperty("IsCustomEntity").SetValue(em, true);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities(MakeEntity("new_custom", true));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    var resp = new RetrieveEntityResponse();
                    resp.Results["EntityMetadata"] = em;
                    return resp;
                }
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "new_custom", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_TranslationPackage_ReturnsMatchingEntityRows()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "11111111-1111-1111-1111-111111111111"
            });

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("entityMessages.row", output.grid.rows[0].RowType);
            Assert.AreEqual("Account", output.grid.rows[0]["defaultText"]);
            Assert.AreEqual("取引先企業", output.grid.rows[0]["1041"]);
        }

        [TestMethod]
        public void Save_TranslationPackage_UpdatesCellAndImportsPackage()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            var loaded = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "11111111-1111-1111-1111-111111111111"
            });

            OrganizationRequest imported = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                }
                if (call[0] is ExportTranslationRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportTranslationResponse(), "ExportTranslationFile", BuildTranslationZip());
                }
                if (call[0] is ImportTranslationRequest)
                {
                    imported = (OrganizationRequest)call[0];
                    return new ImportTranslationResponse();
                }
                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = loaded.grid.rows[0].GridKey,
                        changes = new Dictionary<string, string> { ["1041"] = "更新済み" }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.IsNotNull(imported);
            Assert.IsInstanceOfType(imported, typeof(ImportTranslationRequest));
        }

        [TestMethod]
        public void Save_CustomEntity_ReturnsEmpty()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            var em = new EntityMetadata { LogicalName = "new_custom" };
            typeof(EntityMetadata).GetProperty("IsCustomEntity").SetValue(em, true);
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", em);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "new_custom",
                solutionId = "11111111-1111-1111-1111-111111111111",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "entityMessages|key:any~row:1", changes = new Dictionary<string, string> { ["1033"] = "X" } }
                }
            });

            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_RowMissing_Throws()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "entityMessages|key:missing~row:99", changes = new Dictionary<string, string> { ["1033"] = "X" } }
                }
            }), "changed or disappeared");
        }

        [TestMethod]
        public void Save_LanguageColumnMissing_Throws()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            var loaded = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "11111111-1111-1111-1111-111111111111"
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                }
                if (call[0] is ExportTranslationRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportTranslationResponse(), "ExportTranslationFile", BuildTranslationZipWithout1041());
                }
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = loaded.grid.rows[0].GridKey, changes = new Dictionary<string, string> { ["1041"] = "X" } }
                }
            }), "Language column 1041");
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionUniqueNameMissing()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            service.Retrieve("solution", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(new Entity("solution", Guid.NewGuid()));

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111"
            }), "solution unique name");
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionIdMissing()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account"
            }), "select a solution");
        }

        [TestMethod]
        public void Load_Throws_WhenMetadataMissing()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(new RetrieveEntityResponse());

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111"
            }), "Could not resolve metadata");
        }

        [TestMethod]
        public void Load_Throws_WhenObjectTypeCodeMissing()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            var metadata = CreateAccountMetadata();
            typeof(EntityMetadata).GetProperty("ObjectTypeCode").SetValue(metadata, null);
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", metadata);
                }

                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111"
            }), "object type code");
        }

        [TestMethod]
        public void Load_Throws_WhenExportPackageEmptyOrMissingEntry()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(Array.Empty<byte>());

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111"
            }), "ZIP processing failed");

            service = CreateTranslationPackageService(BuildZip("Other.xml", "<root />"));
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111"
            }), "ZIP processing failed");
        }

        [TestMethod]
        public void Save_ImportFailure_WrapsError()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateTranslationPackageService(BuildTranslationZip());
            var loaded = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "11111111-1111-1111-1111-111111111111"
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", CreateAccountMetadata());
                }
                if (call[0] is ExportTranslationRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new ExportTranslationResponse(), "ExportTranslationFile", BuildTranslationZip());
                }
                if (call[0] is ImportTranslationRequest)
                {
                    throw new Exception("import failed");
                }
                return new OrganizationResponse();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                solutionId = "11111111-1111-1111-1111-111111111111",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = loaded.grid.rows[0].GridKey, changes = new Dictionary<string, string> { ["1041"] = "X" } }
                }
            }), "import failed");
        }

        [TestMethod]
        public void PrivateHelpers_CoverWorkbookParserBranches()
        {
            var adapterType = typeof(EntityMessageAdapter);
            var matchingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var xml = @"<?xml version=""1.0""?>
<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet"" xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet"">
  <Worksheet ss:Name=""Display String Resources"">
    <Table>
      <Row>
        <Cell><Data ss:Type=""String"">Custom Display String Id</Data></Cell>
        <Cell><Data ss:Type=""String"">Resource Key</Data></Cell>
        <Cell><Data ss:Type=""String"">Default Text</Data></Cell>
        <Cell><Data ss:Type=""String"">Custom Text</Data></Cell>
        <Cell><Data ss:Type=""String"">1033</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">Object Type Code</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String"">" + matchingId.ToString("D") + @"</Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String"">Id Default</Data></Cell>
        <Cell><Data ss:Type=""String"">Id Custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Id EN</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">contact</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String"">account.special.message</Data></Cell>
        <Cell><Data ss:Type=""String"">account.special.message</Data></Cell>
        <Cell><Data ss:Type=""String"">Key Custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Key EN</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">contact</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String"">account.inline.message</Data></Cell>
        <Cell><Data ss:Type=""String"">Inline Default</Data></Cell>
        <Cell><Data ss:Type=""String"">Inline Custom</Data></Cell>
        <Cell><Data ss:Type=""String"">Inline EN</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">contact</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String"">Custom Only</Data></Cell>
        <Cell><Data ss:Type=""String"">Custom EN</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">Account</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String""></Data></Cell>
        <Cell><Data ss:Type=""String"">Fallback EN</Data></Cell>
        <Cell ss:Index=""8""><Data ss:Type=""String"">Account</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>";

            var workbook = ParseWorkbook(adapterType, xml);
            var identitySet = CreateIdentitySet(adapterType, matchingId, "account.special.message");
            var entityInfo = CreateEntityInfo(adapterType, "account", "Account", "Account");
            var parsed = AdapterTestHelpers.InvokeStatic(adapterType, "BuildRecords", workbook, identitySet, entityInfo);
            var records = (IList)GetProperty(parsed, "Records");
            Assert.AreEqual(5, records.Count);
            Assert.IsTrue(records.Cast<EasyTranslatorGridRowOutput>().Any(row => row.GridKey.Contains(matchingId.ToString("D"))));
            Assert.IsTrue(records.Cast<EasyTranslatorGridRowOutput>().Any(row => row.GridKey.Contains("account.special.message")));
            Assert.IsTrue(records.Cast<EasyTranslatorGridRowOutput>().Any(row => row.SchemaName == "Custom Only"));
            Assert.IsTrue(records.Cast<EasyTranslatorGridRowOutput>().Any(row => row.SchemaName == "Entity Message"));

            var worksheets = (IList)GetProperty(workbook, "Worksheets");
            var worksheet = worksheets[0];
            var rows = (IList)GetProperty(worksheet, "Rows");
            var headerMap = AdapterTestHelpers.InvokeStatic(adapterType, "BuildHeaderMap", rows[0]);
            var rowWithoutFutureCell = rows[3];
            AdapterTestHelpers.InvokeStatic(adapterType, "SetCellText", rowWithoutFutureCell, 10, "Appended");
            Assert.AreEqual("Appended", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", rowWithoutFutureCell, 10));

            var rowWithFutureCell = rows[1];
            AdapterTestHelpers.InvokeStatic(adapterType, "SetCellText", rowWithFutureCell, 6, "Inserted");
            Assert.AreEqual("Inserted", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", rowWithFutureCell, 6));

            var rowWithMissingData = rows[2];
            var cell = ((IList)GetProperty(rowWithMissingData, "Cells"))[0];
            cell.GetType().GetProperty("DataNode").SetValue(cell, null);
            AdapterTestHelpers.InvokeStatic(adapterType, "SetCellText", rowWithMissingData, 0, "Created data");
            Assert.AreEqual("Created data", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", rowWithMissingData, 0));

            Assert.AreEqual(-1, AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindColumn", headerMap, new[] { "not found" }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", new object[] { null, 0 }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSpreadsheetAttribute", new object[] { null, "Name" }));

            var doc = new XmlDocument();
            doc.LoadXml("<Root><Child /></Root>");
            Assert.IsNotNull(AdapterTestHelpers.InvokeStatic<XmlElement>(adapterType, "SelectElement", doc.DocumentElement, "Child"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XmlElement>(adapterType, "SelectElement", doc.DocumentElement, "Missing"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XmlElement>(adapterType, "SelectDirectElement", doc.DocumentElement, "Missing"));

            var noWorksheet = ParseWorkbook(adapterType, "<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" />");
            ExpectReflectionPluginException(() => AdapterTestHelpers.InvokeStatic(adapterType, "BuildRecords", noWorksheet, identitySet, entityInfo), "Display Strings worksheet");

            var unrelatedWorksheet = ParseWorkbook(adapterType, "<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"><Worksheet ss:Name=\"Other\"><Table><Row /></Table></Worksheet></Workbook>");
            ExpectReflectionPluginException(() => AdapterTestHelpers.InvokeStatic(adapterType, "BuildRecords", unrelatedWorksheet, identitySet, entityInfo), "Display Strings worksheet");

            var emptyWorksheet = ParseWorkbook(adapterType, "<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"><Worksheet><Table /></Worksheet></Workbook>");
            var emptyWorksheets = (IList)GetProperty(emptyWorksheet, "Worksheets");
            ExpectReflectionPluginException(() => AdapterTestHelpers.InvokeStatic(adapterType, "FindHeaderRow", GetProperty(emptyWorksheets[0], null)), "does not contain rows");

            var fallbackWorkbook = ParseWorkbook(adapterType, @"<Workbook xmlns=""urn:schemas-microsoft-com:office:spreadsheet"" xmlns:ss=""urn:schemas-microsoft-com:office:spreadsheet"">
  <Worksheet ss:Name=""Any Display Strings Export""><Table>
    <Row><Cell><Data ss:Type=""String"">Name</Data></Cell></Row>
    <Row>
      <Cell><Data ss:Type=""String"">account inline message</Data></Cell>
      <Cell ss:Index=""8""><Data ss:Type=""String"">account</Data></Cell>
    </Row>
  </Table></Worksheet>
            </Workbook>");
            var fallbackWorksheet = ((IList)GetProperty(fallbackWorkbook, "Worksheets"))[0];
            var fallbackRows = (IList)GetProperty(fallbackWorksheet, "Rows");
            Assert.AreSame(fallbackRows[0], AdapterTestHelpers.InvokeStatic(adapterType, "FindHeaderRow", fallbackWorksheet));

            var containsHeaderMap = AdapterTestHelpers.InvokeStatic(adapterType, "BuildHeaderMap", fallbackRows[0]);
            Assert.AreEqual(0, AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindColumn", containsHeaderMap, new[] { "nam" }));

            var sparseRow = rows[1];
            var sparseCells = (IList)GetProperty(sparseRow, "Cells");
            GetProperty(sparseCells[7], "Node").GetType();
            sparseCells[7].GetType().GetProperty("Node").SetValue(sparseCells[7], null);
            AdapterTestHelpers.InvokeStatic(adapterType, "SetCellText", sparseRow, 6, "Inserted before null scan");
            Assert.AreEqual("Inserted before null scan", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", sparseRow, 6));

            var appendAfterNullScanRow = rows[4];
            AdapterTestHelpers.InvokeStatic(adapterType, "SetCellText", appendAfterNullScanRow, 6, "Appended after null scan");
            Assert.AreEqual("Appended after null scan", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetCellText", appendAfterNullScanRow, 6));

            var attrDoc = new XmlDocument();
            attrDoc.LoadXml("<Root Name=\"direct\" />");
            Assert.AreEqual("direct", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetSpreadsheetAttribute", attrDoc.DocumentElement, "Name"));

            var brokenPackage = CreateTranslationPackage(adapterType);
            ExpectReflectionPluginException(() => AdapterTestHelpers.InvokeStatic(adapterType, "WriteTranslationPackage", new EasyTranslatorRuntimeContext { Tracing = Substitute.For<ITracingService>() }, brokenPackage), "ZIP processing failed while writing");

            var tracing = Substitute.For<ITracingService>();
            AdapterTestHelpers.InvokeStatic(adapterType, "Trace", new EasyTranslatorRuntimeContext { Tracing = tracing }, "Hello {0}", new object[] { "trace" });
            tracing.Received().Trace("Hello trace");
        }

        private static object ParseWorkbook(Type adapterType, string xml)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xml);
            return AdapterTestHelpers.InvokeStatic(adapterType, "ParseWorkbook", doc);
        }

        private static object CreateIdentitySet(Type adapterType, Guid id, string key)
        {
            var type = adapterType.GetNestedType("EntityMessageIdentitySet", BindingFlags.NonPublic);
            var value = Activator.CreateInstance(type);
            ((HashSet<string>)type.GetProperty("Ids").GetValue(value)).Add(id.ToString("D"));
            var keys = (HashSet<string>)type.GetProperty("Keys").GetValue(value);
            keys.Add(key);
            keys.Add("accountspecialmessage");
            return value;
        }

        private static object CreateEntityInfo(Type adapterType, string logicalName, string schemaName, string displayName)
        {
            var type = adapterType.GetNestedType("EntityMessageEntityInfo", BindingFlags.NonPublic);
            var value = Activator.CreateInstance(type);
            type.GetProperty("LogicalName").SetValue(value, logicalName);
            type.GetProperty("SchemaName").SetValue(value, schemaName);
            type.GetProperty("DisplayName").SetValue(value, displayName);
            type.GetProperty("ObjectTypeCode").SetValue(value, 1);
            return value;
        }

        private static object CreateTranslationPackage(Type adapterType)
        {
            var packageType = adapterType.GetNestedType("TranslationPackage", BindingFlags.NonPublic);
            var workbookType = adapterType.GetNestedType("EntityMessageWorkbook", BindingFlags.NonPublic);
            var package = Activator.CreateInstance(packageType);
            var workbook = Activator.CreateInstance(workbookType);
            packageType.GetProperty("Bytes").SetValue(package, BuildZip("CrmTranslations.xml", "<root />"));
            packageType.GetProperty("Workbook").SetValue(package, workbook);
            return package;
        }

        private static object GetProperty(object target, string propertyName)
        {
            if (propertyName == null)
            {
                return target;
            }

            return target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
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
    }
}
