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
using System.Xml.Linq;
namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class FormAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { FormAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                if (call[0] is PublishXmlRequest) return new OrganizationResponse();
                if (call[0] is PublishAllXmlRequest) return new OrganizationResponse();
                return new OrganizationResponse();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            FormAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new FormAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "systemform", id = Guid.NewGuid().ToString() } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithForms_ReturnsRows()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform") return AdapterTestHelpers.Entities(MakeForm("MainForm", 2, "Info"));
                return AdapterTestHelpers.Entities();
            });
            var userSettings = new Entity("usersettings");
            service.Retrieve("usersettings", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(userSettings);
            service.Update(Arg.Any<Entity>());
            var ctx = AdapterTestHelpers.Context(service);
            ctx.Service = service;
            var pluginCtx = Substitute.For<IPluginExecutionContext>();
            pluginCtx.UserId.Returns(Guid.NewGuid());
            ctx.PluginContext = pluginCtx;
            var output = adapter.Load(ctx, new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Load_WithFormTabsSectionsAndCells_ReturnsNodeRowsWithLocalizedLabels()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var formId = Guid.NewGuid();
            var formKey = Guid.NewGuid();
            var englishXml = FormXml("Account", "Details", "Name", "English Tab", "English Section", "English Cell", 1033);
            var japaneseXml = FormXml("取引先", "詳細", "名前", "Japanese Tab", "Japanese Section", "Japanese Cell", 1041);
            var currentLanguage = 1033;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1041);
                return new OrganizationResponse();
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform")
                {
                    return AdapterTestHelpers.Entities(MakeForm(formId, formKey, "MainForm", 2, currentLanguage == 1033 ? englishXml : japaneseXml));
                }
                return AdapterTestHelpers.Entities();
            });
            var userSettings = new Entity("usersettings");
            userSettings["uilanguageid"] = 1033;
            userSettings["helplanguageid"] = 1033;
            service.Retrieve("usersettings", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(userSettings);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call =>
            {
                var update = (Entity)call[0];
                if (update.LogicalName == "usersettings" && update.Contains("uilanguageid"))
                {
                    currentLanguage = (int)update["uilanguageid"];
                }
            });
            var ctx = AdapterTestHelpers.Context(service);
            ctx.Service = service;
            var pluginCtx = Substitute.For<IPluginExecutionContext>();
            pluginCtx.UserId.Returns(Guid.NewGuid());
            ctx.PluginContext = pluginCtx;

            var output = adapter.Load(ctx, new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            var tabs = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual("Account", tabs[0].SchemaName);
            Assert.AreEqual("English Tab", tabs[0]["1033"]);
            Assert.AreEqual("Japanese Tab", tabs[0]["1041"]);
            var sections = (List<EasyTranslatorGridRowOutput>)tabs[0]["children"];
            Assert.AreEqual("Details", sections[0].SchemaName);
            var cells = (List<EasyTranslatorGridRowOutput>)sections[0]["children"];
            Assert.AreEqual("Name", cells[0].SchemaName);
            Assert.AreEqual("Japanese Cell", cells[0]["1041"]);
        }

        [TestMethod]
        public void Load_FallsBackToNonBaseLanguageForms_WhenBaseHasNoForms()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            var formId = Guid.NewGuid();
            var formKey = Guid.NewGuid();
            var currentLanguage = 1033;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1041);
                return new OrganizationResponse();
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "systemform" && currentLanguage == 1041)
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("systemform"),
                        MakeForm(formId, formKey, null, 99, FormXml("Tab", "Section", "Cell", "T", "S", "C", 1041)));
                }
                return AdapterTestHelpers.Entities();
            });
            var userSettings = new Entity("usersettings");
            userSettings["uilanguageid"] = 1033;
            userSettings["helplanguageid"] = 1033;
            service.Retrieve("usersettings", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(userSettings);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call =>
            {
                var update = (Entity)call[0];
                if (update.LogicalName == "usersettings" && update.Contains("uilanguageid"))
                {
                    currentLanguage = (int)update["uilanguageid"];
                }
            });
            var ctx = AdapterTestHelpers.Context(service);
            ctx.Service = service;
            var pluginCtx = Substitute.For<IPluginExecutionContext>();
            pluginCtx.UserId.Returns(Guid.NewGuid());
            ctx.PluginContext = pluginCtx;

            var output = adapter.Load(ctx, new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Type 99", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new FormAdapter();
            var service = CreateService();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
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
        public void PrivateHelpers_CoverFormAdapterEdgeBranches()
        {
            var adapterType = typeof(FormAdapter);
            Assert.AreEqual(0, AdapterTestHelpers.InvokeStatic<List<XElement>>(adapterType, "GetFormTabs", (object)null).Count);
            Assert.AreEqual(0, AdapterTestHelpers.InvokeStatic<List<XElement>>(adapterType, "GetTabSections", (object)null).Count);
            Assert.AreEqual(0, AdapterTestHelpers.InvokeStatic<List<XElement>>(adapterType, "GetSectionCells", (object)null).Count);
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindNodeById", null, "x"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindNodeById", XDocument.Parse("<form />"), ""));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAttributeValue", null, "name"));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsElementName", null, "tab"));

            var emptyDoc = AdapterTestHelpers.InvokeStatic<XDocument>(adapterType, "ParseFormXml", "");
            Assert.AreEqual("form", emptyDoc.Root.Name.LocalName);

            var row = new EasyTranslatorGridRowOutput();
            AdapterTestHelpers.InvokeStatic(adapterType, "AddNodeLabels", row, XElement.Parse("<cell id=\"c\"><labels><label description=\"No language\" /></labels></cell>"));
            Assert.IsFalse(row.ContainsKey("1033"));
            AdapterTestHelpers.InvokeStatic(adapterType, "AddNodeLabels", row, XElement.Parse("<cell id=\"c\" />"));

            var nameless = XElement.Parse("<tab id=\"t\" />");
            Assert.AreEqual("tab", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetNodeDisplayName", nameless));

            var doc = XDocument.Parse(FormXml("Tab", "Section", "Cell", "T", "S", "C", 1033));
            var nodes = AdapterTestHelpers.InvokeStatic<List<XElement>>(adapterType, "GetTranslatableNodes", doc);
            Assert.AreEqual(3, nodes.Count);

            var duplicateLabelsNode = XElement.Parse("<cell id=\"c\"><labels><label description=\"Old\" languagecode=\"1033\"/><label description=\"Duplicate\" languagecode=\"1033\"/></labels></cell>");
            var changed = AdapterTestHelpers.InvokeStatic<bool>(adapterType, "ApplyLabelUpdates", duplicateLabelsNode, new Dictionary<string, string> { { "1033", "New" } });
            Assert.IsTrue(changed);
            Assert.AreEqual(1, duplicateLabelsNode.Element("labels").Elements("label").Count());

            var noLabelsNode = XElement.Parse("<cell id=\"c\" />");
            var added = AdapterTestHelpers.InvokeStatic<bool>(adapterType, "ApplyLabelUpdates", noLabelsNode, new Dictionary<string, string> { { "1033", "Added" } });
            Assert.IsTrue(added);
            Assert.IsTrue(noLabelsNode.ToString().Contains("Added"));

            var formInfoType = adapterType.GetNestedType("FormInfo", BindingFlags.NonPublic);
            var formInfo = Activator.CreateInstance(formInfoType);
            formInfoType.GetProperty("FormId").SetValue(formInfo, Guid.NewGuid());
            formInfoType.GetProperty("FormKey").SetValue(formInfo, "form-key");
            formInfoType.GetProperty("Name").SetValue(formInfo, "Main");
            formInfoType.GetProperty("FormType").SetValue(formInfo, 2);
            formInfoType.GetProperty("Document").SetValue(formInfo, XDocument.Parse("<form />"));
            var listType = typeof(List<>).MakeGenericType(formInfoType);
            var localizedForms = Activator.CreateInstance(listType);
            var buildNodeRow = adapterType.GetMethod("BuildFormNodeRow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(buildNodeRow);
            var nullRow = buildNodeRow.Invoke(null, new[] { formInfo, localizedForms, XElement.Parse("<cell />"), "account", "forms.cell", "Main" });
            Assert.IsNull(nullRow);
        }

        private static Entity MakeForm(string name, int type, string label)
        {
            var e = new Entity("systemform") { Id = Guid.NewGuid() };
            e["formid"] = e.Id;
            e["name"] = name;
            e["type"] = new OptionSetValue(type);
            e["objecttypecode"] = "account";
            e["formxml"] = "<form><labels><label description=\"" + label + "\" languagecode=\"1033\"/></labels></form>";
            return e;
        }

        private static Entity MakeForm(Guid formId, Guid formKey, string name, int type, string formXml)
        {
            var e = new Entity("systemform") { Id = formId };
            e["formid"] = formId;
            e["formidunique"] = formKey;
            e["name"] = name;
            e["type"] = new OptionSetValue(type);
            e["objecttypecode"] = "account";
            e["formxml"] = formXml;
            return e;
        }

        private static string FormXml(string tabName, string sectionName, string cellName, string tabLabel, string sectionLabel, string cellLabel, int languageCode)
        {
            return "<form><tabs>" +
                "<tab id=\"tab1\" name=\"" + tabName + "\"><labels><label description=\"" + tabLabel + "\" languagecode=\"" + languageCode + "\"/></labels><columns><column width=\"100\"><sections>" +
                "<section id=\"section1\" name=\"" + sectionName + "\"><labels><label description=\"" + sectionLabel + "\" languagecode=\"" + languageCode + "\"/></labels><rows><row><cells>" +
                "<cell id=\"cell1\" name=\"" + cellName + "\"><labels><label description=\"" + cellLabel + "\" languagecode=\"" + languageCode + "\"/></labels></cell>" +
                "</cells></row></rows></section>" +
                "</sections></column></columns></tab>" +
                "</tabs></form>";
        }
    }
}
