using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class BpfAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { BpfAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateService()
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

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_NoWorkflows_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithBpf_ReturnsRows()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var wr = new Entity("workflow") { Id = Guid.NewGuid() };
            wr["workflowid"] = wr.Id;
            wr["name"] = "TestBPF";
            wr["primaryentity"] = "account";
            wr["category"] = new OptionSetValue(4);
            wr["clientdata"] = "{\"steps\":{\"list\":[{\"__class\":\"StageStep:#account\",\"stageId\":\"S1\",\"description\":\"Stage One\"}]}}";
            wr["statecode"] = new OptionSetValue(1);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(wr);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithStageFieldAndLabels_ReturnsNestedRowsWithLabels()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var emptyId = new Entity("workflow") { Id = Guid.NewGuid() };
            emptyId["workflowid"] = Guid.Empty;
            emptyId["name"] = "Skipped";
            emptyId["clientdata"] = RichClientData();

            var noStages = new Entity("workflow") { Id = Guid.NewGuid() };
            noStages["workflowid"] = noStages.Id;
            noStages["name"] = "NoStages";
            noStages["clientdata"] = "{ bad json";

            var workflow = new Entity("workflow") { Id = Guid.NewGuid() };
            workflow["workflowid"] = workflow.Id;
            workflow["name"] = "TestBPF";
            workflow["clientdata"] = RichClientData();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(emptyId, noStages, workflow);
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            var stages = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(1, stages.Count);
            Assert.AreEqual("Stage EN", stages[0]["1033"]);
            var fields = (List<EasyTranslatorGridRowOutput>)stages[0]["children"];
            Assert.AreEqual(1, fields.Count);
            Assert.AreEqual("Field EN", fields[0]["1033"]);
        }

        [TestMethod]
        public void Load_EmptyClientData_SkipsWorkflow()
        {
            var adapter = new BpfAdapter();
            var service = CreateService();
            var wr = new Entity("workflow") { Id = Guid.NewGuid() };
            wr["workflowid"] = wr.Id;
            wr["name"] = "TestBPF";
            wr["clientdata"] = "";
            wr["statecode"] = new OptionSetValue(1);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(wr);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new BpfAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithInvalidGridKey_Throws()
        {
            var adapter = new BpfAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "invalid|key",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new BpfAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new BpfAdapter();
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
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Published_WaitAction_Called()
        {
            var adapter = new BpfAdapter();
            var service = Substitute.For<IOrganizationService>();
            int called = 0;
            BpfAdapter.WaitAction = _ => called++;
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(called > 0);
        }

        [TestMethod]
        public void PrivateHelpers_CoverBpfParserBranches()
        {
            var adapterType = typeof(BpfAdapter);
            var parseStages = adapterType.GetMethod("ParseStages", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(parseStages);
            var stages = parseStages.Invoke(null, new object[] { RichClientData() });
            Assert.IsNotNull(stages);

            var getList = adapterType.GetMethod("GetList", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(getList);
            Assert.AreEqual(0, ((List<object>)getList.Invoke(null, new object[] { null })).Count);
            Assert.AreEqual(0, ((List<object>)getList.Invoke(null, new object[] { "text" })).Count);
            Assert.AreEqual(2, ((List<object>)getList.Invoke(null, new object[] { new[] { "a", "b" } })).Count);

            var addValues = adapterType.GetMethod("AddBpfLabelValues", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(addValues);
            var labelType = adapterType.GetNestedType("BpfLabel", BindingFlags.NonPublic);
            var label = Activator.CreateInstance(labelType);
            labelType.GetProperty("LanguageCode").SetValue(label, "1033");
            labelType.GetProperty("Description").SetValue(label, "Label");
            var labelListType = typeof(List<>).MakeGenericType(labelType);
            var labelList = Activator.CreateInstance(labelListType);
            labelListType.GetMethod("Add").Invoke(labelList, new[] { label });
            var row = new EasyTranslatorGridRowOutput();
            addValues.Invoke(null, new[] { row, labelList });
            Assert.AreEqual("Label", row["1033"]);

            var parseStagesMethod = adapterType.GetMethod("ParseStages", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(parseStagesMethod);
            var invalidStages = parseStagesMethod.Invoke(null, new object[] { "{ not json" });
            Assert.IsNotNull(invalidStages);
            var nullStages = parseStagesMethod.Invoke(null, new object[] { null });
            Assert.IsNotNull(nullStages);

            var upsert = adapterType.GetMethod("UpsertStepLabel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(upsert);
            var noClosing = "<Activity><x:String x:Key=\"StageId\">S1</x:String></Activity>";
            Assert.AreEqual(noClosing, (string)upsert.Invoke(null, new object[] { noClosing, "S1", "1033", "X" }));
            var withCollection = "<Activity><sco:Collection><mcwo:StepLabel LabelId=\"S1\" LanguageCode=\"1033\" /></sco:Collection><x:String x:Key=\"StageId\">S1</x:String></Activity>";
            var updated = (string)upsert.Invoke(null, new object[] { withCollection, "S1", "1033", "X" });
            Assert.IsTrue(updated.Contains("Description=\"X\""));

            var encodeXml = adapterType.GetMethod("EncodeXmlAttribute", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(encodeXml);
            Assert.AreEqual(string.Empty, (string)encodeXml.Invoke(null, new object[] { null }));
            Assert.AreEqual("&amp;&quot;&lt;&gt;", (string)encodeXml.Invoke(null, new object[] { "&\"<>" }));

            var validateGuid = adapterType.GetMethod("ValidateGuid", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(validateGuid);
            var validId = Guid.NewGuid();
            Assert.AreEqual(validId, (Guid)validateGuid.Invoke(null, new object[] { validId.ToString("D"), "id" }));

            var getWorkflowName = adapterType.GetMethod("GetWorkflowName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(getWorkflowName);
            var workflowId = Guid.NewGuid();
            Assert.AreEqual(workflowId.ToString("D"), (string)getWorkflowName.Invoke(null, new object[] { null, workflowId }));
            var unnamedWorkflow = new Entity("workflow", workflowId);
            unnamedWorkflow["name"] = " ";
            Assert.AreEqual(workflowId.ToString("D"), (string)getWorkflowName.Invoke(null, new object[] { unnamedWorkflow, workflowId }));

            var getValue = adapterType.GetMethod("GetValue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(getValue);
            var dictionary = new Dictionary<string, object> { { "a", "b" } };
            Assert.AreEqual("b", getValue.Invoke(null, new object[] { dictionary, "a" }));
            Assert.IsNull(getValue.Invoke(null, new object[] { dictionary, "missing" }));
            Assert.IsNull(getValue.Invoke(null, new object[] { null, "missing" }));

            var service = Substitute.For<IOrganizationService>();
            var restore = adapterType.GetMethod("RestoreWorkflow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(restore);
            restore.Invoke(null, new object[] { service, Guid.NewGuid(), "original", true });
            service.Received(2).Update(Arg.Any<Entity>());

            var tryRestore = adapterType.GetMethod("TryRestoreWorkflow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(tryRestore);
            Assert.IsNull(tryRestore.Invoke(null, new object[] { service, Guid.NewGuid(), null, false }));

            var failingService = Substitute.For<IOrganizationService>();
            failingService.When(s => s.Update(Arg.Any<Entity>())).Do(_ => throw new Exception("restore failed"));
            var restoreError = (Exception)tryRestore.Invoke(null, new object[] { failingService, Guid.NewGuid(), "original", false });
            Assert.IsNotNull(restoreError);
        }

        private static string RichClientData()
        {
            return "{" +
                "\"steps\":{\"list\":[" +
                "{" +
                "\"__class\":\"StageStep:#account\"," +
                "\"stageId\":\"S1\"," +
                "\"description\":\"Stage One\"," +
                "\"stepLabels\":{\"list\":[" +
                "{\"languageCode\":\"1033\",\"description\":\"Stage EN\"}," +
                "{\"languageCode\":\"\",\"description\":\"Skip\"}," +
                "\"not-object\"" +
                "]}," +
                "\"steps\":{\"list\":[" +
                "{" +
                "\"__class\":\"StepStep:#account\"," +
                "\"stepStepId\":\"F1\"," +
                "\"description\":\"Field One\"," +
                "\"stepLabels\":{\"list\":[{\"languageCode\":\"1033\",\"description\":\"Field EN\"}]}" +
                "}," +
                "{\"__class\":\"StepStep:#account\",\"description\":\"Missing id\"}," +
                "\"not-object\"" +
                "]}" +
                "}" +
                "]}" +
                "}";
        }
    }
}
