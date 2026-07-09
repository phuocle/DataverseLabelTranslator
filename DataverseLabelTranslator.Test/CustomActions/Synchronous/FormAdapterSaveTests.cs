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

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class FormAdapterSaveTests
    {
        [TestInitialize]
        public void Initialize() { FormAdapter.WaitAction = _ => { }; }

        [TestCleanup]
        public void Cleanup() { FormAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static Guid _formId = Guid.NewGuid();
        private static Guid _userId = Guid.NewGuid();

        private const string CellFormXml =
            "<form><tabs><tab id=\"tab1\" name=\"Tab1\"><columns><column width=\"100\">" +
            "<sections><section id=\"sec1\" name=\"Sec1\"><rows><row><cells>" +
            "<cell id=\"cellA\"><labels><label description=\"Old\" languagecode=\"1033\"/></labels>" +
            "<control datafieldname=\"new_field\"/></cell>" +
            "</cells></row></rows></section></sections></column></columns></tab></tabs></form>";

        private static EasyTranslatorRuntimeContext CreateContext(IOrganizationService service)
        {
            var pluginCtx = Substitute.For<IPluginExecutionContext>();
            pluginCtx.UserId.Returns(_userId);
            return new EasyTranslatorRuntimeContext
            {
                ServiceAdmin = service,
                Service = service,
                PluginContext = pluginCtx
            };
        }

        private static IOrganizationService CreateSaveService(string formXml = CellFormXml)
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(MakeFormRow(formXml));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                return new OrganizationResponse();
            });
            var userSettings = new Entity("usersettings");
            userSettings["uilanguageid"] = 1033;
            userSettings["helplanguageid"] = 1033;
            service.Retrieve("usersettings", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(userSettings);
            return service;
        }

        private static Entity MakeFormRow(string formXml)
        {
            var e = new Entity("systemform") { Id = _formId };
            e["formid"] = _formId;
            e["formidunique"] = _formId;
            e["name"] = "MainForm";
            e["type"] = new OptionSetValue(2);
            e["objecttypecode"] = "account";
            e["formxml"] = formXml;
            return e;
        }

        private static Entity CapturedUpdate(IOrganizationService service, string entityName)
        {
            var calls = service.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "Update");
            return calls
                .Select(c => (Entity)c.GetArguments()[0])
                .FirstOrDefault(e => string.Equals(e.LogicalName, entityName, StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void Save_NoRows_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(CreateContext(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Save_NullRows_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(CreateContext(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyChanges_Skipped()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA|account", changes = new Dictionary<string, string>() }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridKeyType_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "attributes|x|y|z", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input));
        }

        [TestMethod]
        public void Save_NotEnoughParts_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|x", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input));
        }

        [TestMethod]
        public void Save_InvalidFormId_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|not-a-guid|cellA|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input), "Form formid");
        }

        [TestMethod]
        public void Save_EmptyNodeId_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "||account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input), "node id");
        }

        [TestMethod]
        public void Save_NodeNotFound_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|missingCell|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input), "was not found");
        }

        [TestMethod]
        public void Save_ValidChange_UpdatesAndRecordsPublishTarget()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA|account", changes = new Dictionary<string, string> { { "1033", "New Label" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.changed);
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "entity" && t.id == "account"));
            var formUpdate = CapturedUpdate(service, "systemform");
            Assert.IsNotNull(formUpdate);
            Assert.IsTrue(((string)formUpdate["formxml"]).Contains("New Label"));
        }

        [TestMethod]
        public void Save_MultipleLanguages_AllApplied()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "forms|" + _formId.ToString("D") + "|cellA|account",
                        changes = new Dictionary<string, string> { { "1033", "English" }, { "1031", "German" } }
                    }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var formUpdate = CapturedUpdate(service, "systemform");
            var xml = (string)formUpdate["formxml"];
            Assert.IsTrue(xml.Contains("English"));
            Assert.IsTrue(xml.Contains("German"));
        }

        [TestMethod]
        public void Save_EmptyEntityName_FallsBackFromInput()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            // white space in entity-name part forces fallback to input.entityName = account
            var input = new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA| ", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }

        [TestMethod]
        public void Save_UnchangedXml_NoUpdate()
        {
            var adapter = new FormAdapter();
            // The form already has Old label for 1033 - feed back "Old" so XML should not change.
            var service = CreateSaveService(CellFormXml);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA|account", changes = new Dictionary<string, string> { { "1033", "Old" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_NewLanguage_AddsLabelElement()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA|account", changes = new Dictionary<string, string> { { "1036", "Francais" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var formUpdate = CapturedUpdate(service, "systemform");
            Assert.IsTrue(((string)formUpdate["formxml"]).Contains("languagecode=\"1036\""));
        }

        [TestMethod]
        public void Save_MultipleRowsSameForm_AggregatesNodes()
        {
            var adapter = new FormAdapter();
            var multiCellXml =
                "<form><tabs><tab id=\"tab1\"><columns><column width=\"100\">" +
                "<sections><section id=\"sec1\"><rows><row><cells>" +
                "<cell id=\"c1\"><labels><label description=\"A\" languagecode=\"1033\"/></labels></cell>" +
                "<cell id=\"c2\"><labels><label description=\"B\" languagecode=\"1033\"/></labels></cell>" +
                "</cells></row></rows></section></sections></column></columns></tab></tabs></form>";
            var service = CreateSaveService(multiCellXml);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|c1|account", changes = new Dictionary<string, string> { { "1033", "X1" } } },
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|c2|account", changes = new Dictionary<string, string> { { "1033", "X2" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(2, output.changedRowCount);
            var formUpdate = CapturedUpdate(service, "systemform");
            Assert.IsTrue(((string)formUpdate["formxml"]).Contains("X1"));
            Assert.IsTrue(((string)formUpdate["formxml"]).Contains("X2"));
        }

        [TestMethod]
        public void Save_NonNumericLanguage_SkippedChange()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|" + _formId.ToString("D") + "|cellA|account", changes = new Dictionary<string, string> { { "not-a-lang", "X" } } }
                }
            };
            var output = adapter.Save(CreateContext(service), input);
            // No valid changes -> no node updates -> SaveForm returns unchanged -> not counted
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_RemoveOverriddenCellLabelsOperation_RewritesCellIds()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService(CellFormXml);
            var input = new EasyTranslatorSaveInput
            {
                operation = "RemoveOverriddenCellLabels",
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>()
            };
            var output = adapter.Save(CreateContext(service), input);
            // cell with control+datafieldname present -> rewritten -> changed
            Assert.IsTrue(output.changedRowCount >= 1);
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "entity" && t.id == "account"));
        }

        [TestMethod]
        public void Save_RemoveOverriddenCellLabels_MissingEntityName_Throws()
        {
            var adapter = new FormAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                operation = "RemoveOverriddenCellLabels",
                entityName = "",
                changedRows = new List<EasyTranslatorChangedRowInput>()
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(CreateContext(service), input), "entityName");
        }

        [TestMethod]
        public void Save_RemoveOverriddenCellLabels_NoChanges_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            // form with no cell/control/datafield -> no changes
            var emptyXml = "<form><tabs><tab id=\"t\"><columns><column width=\"100\">" +
                "<sections><section id=\"s\"><rows><row></row></rows></section></sections></column></columns></tab></tabs></form>";
            var service = CreateSaveService(emptyXml);
            var input = new EasyTranslatorSaveInput
            {
                operation = "RemoveOverriddenCellLabels",
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>()
            };
            var output = adapter.Save(CreateContext(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }
    }
}
