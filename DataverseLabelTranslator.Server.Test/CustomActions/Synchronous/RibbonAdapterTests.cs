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
    }
}
