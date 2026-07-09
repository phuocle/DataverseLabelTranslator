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

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class BusinessRuleAdapterSaveTests
    {
        [TestInitialize]
        public void Initialize() { BusinessRuleAdapter.WaitAction = _ => { }; }

        [TestCleanup]
        public void Cleanup() { BusinessRuleAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static Guid _wfId = Guid.NewGuid();

        // XAML with an existing mcwo:StepLabel inside a sco:Collection so UpsertStepLabel can insert.
        private const string RuleXaml =
            "<Activity x:Class=\"Test\">" +
            "<gc:Activity DesignerName=\"Rule\">" +
            "<sco:Collection x:Key=\"StepLabels\">" +
            "<mcwo:StepLabel Description=\"Old\" LabelId=\"L1\" LanguageCode=\"1033\" />" +
            "</sco:Collection>" +
            "</gc:Activity>" +
            "</Activity>";

        private static IOrganizationService CreateSaveService(string xaml = RuleXaml, int statecode = 0)
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Retrieve("workflow", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(callInfo =>
            {
                var wf = new Entity("workflow") { Id = (Guid)callInfo[1] };
                wf["workflowid"] = wf.Id;
                wf["name"] = "MyRule";
                wf["statecode"] = new OptionSetValue(statecode);
                wf["statuscode"] = new OptionSetValue(statecode == 1 ? 2 : 1);
                wf["xaml"] = xaml;
                return wf;
            });
            return service;
        }

        private static Entity CapturedUpdate(IOrganizationService service)
        {
            return service.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Update")
                .Select(c => (Entity)c.GetArguments()[0])
                .FirstOrDefault(e => string.Equals(e.LogicalName, "workflow", StringComparison.OrdinalIgnoreCase) && e.Contains("xaml"));
        }

        private static string BuildLabelIdKey(string labelId) { return "businessRules|" + _wfId.ToString("D") + "|" + labelId + "|account"; }

        [TestMethod]
        public void Save_NoRows_ReturnsEmpty()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_NullRows_ReturnsEmpty()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyChanges_Skipped()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string>() }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridKeyType_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|null|L1|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "businessRules");
        }

        [TestMethod]
        public void Save_InvalidWorkflowId_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "businessRules|not-a-guid|L1|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "Business rule workflowid");
        }

        [TestMethod]
        public void Save_EmptyLabelId_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "businessRules|" + _wfId.ToString("D") + "||account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "labelId is required");
        }

        [TestMethod]
        public void Save_EmptyXaml_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService(null);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "does not contain workflow XAML");
        }

        [TestMethod]
        public void Save_LabelNotFound_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("MISSING"), changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "was not found");
        }

        [TestMethod]
        public void Save_ExistingLabelUpdated_ChangeRecorded()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "NewLabel" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
            var update = CapturedUpdate(service);
            Assert.IsNotNull(update);
            Assert.IsTrue(((string)update["xaml"]).Contains("NewLabel"));
        }

        [TestMethod]
        public void Save_NewLanguage_InsertsLabel()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1031", "Deutsch" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service);
            Assert.IsTrue(((string)update["xaml"]).Contains("LanguageCode=\"1031\""));
            Assert.IsTrue(((string)update["xaml"]).Contains("Deutsch"));
        }

        [TestMethod]
        public void Save_MultipleLanguages_AllApplied()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "EN" }, { "1031", "DE" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service);
            var xml = (string)update["xaml"];
            Assert.IsTrue(xml.Contains("LanguageCode=\"1033\""));
            Assert.IsTrue(xml.Contains("LanguageCode=\"1031\""));
        }

        [TestMethod]
        public void Save_ActiveRule_DeactivatesReactivates()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService(RuleXaml, statecode: 1);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            // Expect updates: deactivate, xaml update, reactivate.
            var updates = service.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Update")
                .Select(c => (Entity)c.GetArguments()[0])
                .ToList();
            Assert.IsTrue(updates.Count >= 2);
        }

        [TestMethod]
        public void Save_UnchangedXaml_NoUpdate()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            // Existing has Description="Old" for 1033 -> feed back "Old" -> no XML change.
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "Old" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_FallbackEntityFromInput_Used()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "businessRules|" + _wfId.ToString("D") + "|L1| ", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }

        [TestMethod]
        public void Save_NonNumericLanguage_Skipped()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "xx", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_RestoreOnException_WrapsAndRestores()
        {
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Retrieve("workflow", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(callInfo =>
            {
                var wf = new Entity("workflow") { Id = (Guid)callInfo[1] };
                wf["workflowid"] = wf.Id;
                wf["name"] = "MyRule";
                wf["statecode"] = new OptionSetValue(1);
                wf["statuscode"] = new OptionSetValue(2);
                wf["xaml"] = RuleXaml;
                return wf;
            });
            var calls = 0;
            service.When(x => x.Update(Arg.Any<Entity>())).Do(callInfo =>
            {
                calls++;
                if (calls == 1) return; // deactivate call
                if (calls == 2) return; // xaml update succeeds
                throw new Exception("simulated failure on reactivate");
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "Failed to save Business Rule");
        }

        [TestMethod]
        public void Save_MultipleRowsSameWorkflow_Aggregates()
        {
            var adapter = new BusinessRuleAdapter();
            var xaml =
                "<Activity x:Class=\"Test\"><gc:Activity DesignerName=\"Rule\">" +
                "<sco:Collection x:Key=\"StepLabels\">" +
                "<mcwo:StepLabel Description=\"A\" LabelId=\"L1\" LanguageCode=\"1033\" />" +
                "<mcwo:StepLabel Description=\"B\" LabelId=\"L2\" LanguageCode=\"1033\" />" +
                "</sco:Collection></gc:Activity></Activity>";
            var service = CreateSaveService(xaml);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L1"), changes = new Dictionary<string, string> { { "1033", "N1" } } },
                    new EasyTranslatorChangedRowInput { gridKey = BuildLabelIdKey("L2"), changes = new Dictionary<string, string> { { "1033", "N2" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(2, output.changedRowCount);
            var update = CapturedUpdate(service);
            var xml = (string)update["xaml"];
            Assert.IsTrue(xml.Contains("N1"));
            Assert.IsTrue(xml.Contains("N2"));
        }
    }
}
