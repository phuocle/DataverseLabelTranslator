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
using System.Text;

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
    }
}
