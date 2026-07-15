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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class BpfAdapterSaveTests
    {
        [TestInitialize]
        public void Initialize() { BpfAdapter.WaitAction = _ => { }; }

        [TestCleanup]
        public void Cleanup() { BpfAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static Guid _wfId = Guid.NewGuid();

        // BPF XAML containing a stage (StageId=S1) with a sco:Collection closing tag.
        private const string StageXaml =
            "<Activity x:Class=\"Test\">" +
            "<sco:Collection>" +
            "<mcwo:StepLabel Description=\"Old\" LabelId=\"S1\" LanguageCode=\"1033\" />" +
            "</sco:Collection>" +
            "<x:String x:Key=\"StageId\">S1</x:String>" +
            "</Activity>";

        private static IOrganizationService CreateSaveService(string xaml = StageXaml, int statecode = 0)
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
                return new OrganizationResponse();
            });
            service.Retrieve("workflow", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(callInfo =>
            {
                var wf = new Entity("workflow") { Id = (Guid)callInfo[1] };
                wf["workflowid"] = wf.Id;
                wf["name"] = "MyBPF";
                wf["statecode"] = new OptionSetValue(statecode);
                wf["statuscode"] = new OptionSetValue(statecode == 1 ? 2 : 1);
                wf["xaml"] = xaml;
                return wf;
            });
            return service;
        }

        private static Entity CapturedUpdate(IOrganizationService service, string entityName)
        {
            return service.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Update")
                .Select(c => (Entity)c.GetArguments()[0])
                .FirstOrDefault(e => string.Equals(e.LogicalName, entityName, StringComparison.OrdinalIgnoreCase) && e.Contains("xaml"));
        }

        [TestMethod]
        public void Save_NoRows_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_NullRows_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyChanges_Skipped()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string>() }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridKey_Throws()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "x|y", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_InvalidWorkflowId_Throws()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|no-guid|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "BPF workflowid");
        }

        [TestMethod]
        public void Save_EmptyLabelId_Throws()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "||stage|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "stage or field id");
        }

        [TestMethod]
        public void Save_EmptyXaml_Throws()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService(null);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "does not contain workflow XAML");
        }

        [TestMethod]
        public void Save_ExistingLabelUpdated_ChangesRecorded()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
            var update = CapturedUpdate(service, "workflow");
            Assert.IsNotNull(update);
            Assert.IsTrue(((string)update["xaml"]).Contains("New"));
        }

        [TestMethod]
        public void Save_NewLanguage_InsertsLabel()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1031", "Deutsch" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service, "workflow");
            Assert.IsTrue(((string)update["xaml"]).Contains("LanguageCode=\"1031\""));
        }

        [TestMethod]
        public void Save_ActiveWorkflow_DeactivatesReactivatesAndUpdates()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService(StageXaml, statecode: 1);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            // We expect at least 3 updates: deactivate -> xaml update -> reactivate
            var updates = service.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Update")
                .Select(c => (Entity)c.GetArguments()[0])
                .ToList();
            var xamlUpdate = updates.FirstOrDefault(e => e.Contains("xaml"));
            Assert.IsNotNull(xamlUpdate);
        }

        [TestMethod]
        public void Save_LabelNotFoundForStage_NoChange()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            // LabelId not in the XAML marker -> FindStageOrStepMarker returns -1 -> no change
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|Missing|stage|account", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_FieldMarker_TriesProcessStepIdLookup()
        {
            var adapter = new BpfAdapter();
            var fieldXaml =
                "<Activity x:Class=\"Test\">" +
                "<sco:Collection>" +
                "<mcwo:StepLabel Description=\"Old\" LabelId=\"F1\" LanguageCode=\"1033\" />" +
                "</sco:Collection>" +
                "<x:String x:Key=\"ProcessStepId\">F1</x:String>" +
                "</Activity>";
            var service = CreateSaveService(fieldXaml);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|F1|field|account", changes = new Dictionary<string, string> { { "1033", "NewField" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service, "workflow");
            Assert.IsTrue(((string)update["xaml"]).Contains("NewField"));
        }

        [TestMethod]
        public void Save_MultipleLanguages_AllApplied()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "EN" }, { "1031", "DE" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service, "workflow");
            var xml = (string)update["xaml"];
            Assert.IsTrue(xml.Contains("LanguageCode=\"1033\""));
            Assert.IsTrue(xml.Contains("LanguageCode=\"1031\""));
        }

        [TestMethod]
        public void Save_SameWorkflowAndLabel_MergesRows()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "EN" } } },
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1031", "DE" } } }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            var update = CapturedUpdate(service, "workflow");
            var xml = (string)update["xaml"];
            Assert.IsTrue(xml.Contains("LanguageCode=\"1033\""));
            Assert.IsTrue(xml.Contains("LanguageCode=\"1031\""));
        }

        [TestMethod]
        public void Save_RestoreOnException_WrapsErrorAndRestores()
        {
            var adapter = new BpfAdapter();
            // Two services: first Retrieve succeeds, then Update throws on xaml-write.
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity()));
            service.Retrieve("workflow", Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(callInfo =>
            {
                var wf = new Entity("workflow") { Id = (Guid)callInfo[1] };
                wf["workflowid"] = wf.Id;
                wf["name"] = "MyBPF";
                wf["statecode"] = new OptionSetValue(1);
                wf["statuscode"] = new OptionSetValue(2);
                wf["xaml"] = StageXaml;
                return wf;
            });
            var calls = 0;
            service.When(x => x.Update(Arg.Any<Entity>())).Do(callInfo =>
            {
                calls++;
                if (calls == 1 || calls == 2) return; // deactivate call, then xaml update succeeds
                throw new Exception("simulated failure");
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage|account", changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "Failed to save Business Process Flow");
        }

        [TestMethod]
        public void Save_FallbackEntityFromInput_UsedWhenGridKeyPartEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateSaveService();
            var input = new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "bpf|" + _wfId.ToString("D") + "|S1|stage| ", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }
    }
}
