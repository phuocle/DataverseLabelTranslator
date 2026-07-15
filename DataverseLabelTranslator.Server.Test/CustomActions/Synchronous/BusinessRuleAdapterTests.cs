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
using System.Reflection;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class BusinessRuleAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { BusinessRuleAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateServiceWithBaseLanguage()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "attribute") return AdapterTestHelpers.Entities();
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmpty()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithBusinessRuleXaml_ReturnsRows()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateServiceWithBaseLanguage();
            var emptyId = new Entity("workflow") { Id = Guid.NewGuid() };
            emptyId["workflowid"] = Guid.Empty;
            emptyId["name"] = "Skipped";
            emptyId["xaml"] = "<mcwo:StepLabel LabelId=\"SKIP\" LanguageCode=\"1033\" Description=\"Skip\"/>";
            var noLabels = new Entity("workflow") { Id = Guid.NewGuid() };
            noLabels["workflowid"] = noLabels.Id;
            noLabels["name"] = "NoLabels";
            noLabels["xaml"] = "<Activity />";
            var wr = new Entity("workflow") { Id = Guid.NewGuid() };
            wr["name"] = "TestRule";
            wr["workflowid"] = wr.Id;
            wr["primaryentity"] = "account";
            wr["category"] = new OptionSetValue(2);
            wr["xaml"] = "<mcwo:StepLabel LabelId=\"L1\" LanguageCode=\"1033\" Text=\"Test Label\"/>";
            wr["statecode"] = new OptionSetValue(1);
            wr["statuscode"] = new OptionSetValue(2);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(emptyId, noLabels, wr);
                if (q.EntityName == "attribute") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithSetMessageXaml_ReturnsRows()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateServiceWithBaseLanguage();
            var wr = new Entity("workflow") { Id = Guid.NewGuid() };
            wr["name"] = "TestRule";
            wr["workflowid"] = wr.Id;
            wr["primaryentity"] = "account";
            wr["category"] = new OptionSetValue(2);
            wr["xaml"] = "<mcwc:SetMessage><mcwo:StepLabel LabelId=\"L1\" LanguageCode=\"1033\" Text=\"Test\"/></mcwc:SetMessage>";
            wr["statecode"] = new OptionSetValue(1);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(wr);
                if (q.EntityName == "attribute") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithAttributeRefs_PopulatesFromAttributes()
        {
            var adapter = new BusinessRuleAdapter();
            var service = CreateServiceWithBaseLanguage();
            var wr = new Entity("workflow") { Id = Guid.NewGuid() };
            wr["name"] = "TestRule";
            wr["workflowid"] = wr.Id;
            wr["primaryentity"] = "account";
            wr["category"] = new OptionSetValue(2);
            wr["xaml"] = "<mcwo:StepLabel LabelId=\"L1\" LanguageCode=\"1033\" Text=\"Test\"/>";
            wr["statecode"] = new OptionSetValue(1);
            var attr = new StringAttributeMetadata
            {
                LogicalName = "new_field",
                SchemaName = "new_field",
                MetadataId = Guid.NewGuid(),
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "My Field"))
            };
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "workflow") return AdapterTestHelpers.Entities(wr);
                if (q.EntityName == "attribute") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account", attr);
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Save_WithInvalidLabelId_Throws()
        {
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            var workflowId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"businessRules|{workflowId:D}||account",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new BusinessRuleAdapter();
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
        public void Published_WaitsAndReturnsTargets()
        {
            var wait = 0;
            BusinessRuleAdapter.WaitAction = _ => wait++;
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Published(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            });

            Assert.AreEqual(1, wait);
            Assert.AreEqual(1, output.publishTargets.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverBusinessRuleBranches()
        {
            var adapterType = typeof(BusinessRuleAdapter);
            var errorXaml = "<mcwc:SetMessage ControlId=\"new_name\"><mcwc:SetMessage.StepLabels><mcwo:StepLabel";
            Assert.AreEqual("Error message", GetContextKind(adapterType, errorXaml, errorXaml.Length));
            var recommendationXaml = "<mcwc:SetMessage ControlId=\"new_name\" Level=\"RECOMMENDATION\"><mcwc:SetMessage.StepLabels><mcwo:StepLabel";
            Assert.AreEqual("Recommendation title", GetContextKind(adapterType, recommendationXaml, recommendationXaml.Length));
            var detailXaml = "<mcwc:SetMessage ControlId=\"new_name\"><sco:Collection x:Key=\"StepLabels\"><mcwo:StepLabel";
            Assert.AreEqual("Recommendation detail", GetContextKind(adapterType, detailXaml, detailXaml.Length));
            Assert.AreEqual("Business rule label", GetContextKind(adapterType, "<root />", 0));

            var labels = (System.Collections.ICollection)AdapterTestHelpers.InvokeStatic(adapterType, "ParseStepLabels", "<mcwo:StepLabel LabelId=\"B\" LanguageCode=\"1033\" Description=\"B\" /><mcwo:StepLabel LabelId=\"A\" LanguageCode=\"1033\" Description=\"A\" />", new Dictionary<string, string>());
            Assert.AreEqual(2, labels.Count);
            labels = (System.Collections.ICollection)AdapterTestHelpers.InvokeStatic(adapterType, "ParseStepLabels", "<mcwo:StepLabel LanguageCode=\"1033\" Description=\"No id\" /><mcwo:StepLabel LabelId=\"NoLang\" Description=\"No lang\" />", new Dictionary<string, string>());
            Assert.AreEqual(0, labels.Count);

            var labelA = CreateStepLabel(adapterType, "A", "Error message", "new_name");
            var labelB = CreateStepLabel(adapterType, "B", "Recommendation detail", "zzz");
            var comparerResult = AdapterTestHelpers.InvokeStatic<int>(
                adapterType,
                "CompareStepLabels",
                labelA,
                labelB,
                new Dictionary<string, string> { ["new_name"] = "Display Name" });
            Assert.IsTrue(comparerResult < 0);
            var labelC = CreateStepLabel(adapterType, "C", "Recommendation title", "same");
            var labelD = CreateStepLabel(adapterType, "D", "Recommendation detail", "same");
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<int>(adapterType, "CompareStepLabels", labelC, labelD, new Dictionary<string, string>()) < 0);
            SetStepLabelOrder(labelC, 1);
            SetStepLabelOrder(labelD, 3);
            labelD.GetType().GetProperty("Kind").SetValue(labelD, "Recommendation title");
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<int>(adapterType, "CompareStepLabels", labelC, labelD, new Dictionary<string, string>()) < 0);
            Assert.AreEqual(1, AdapterTestHelpers.InvokeStatic<int>(adapterType, "GetKindSortOrder", "Error message"));
            Assert.AreEqual(2, AdapterTestHelpers.InvokeStatic<int>(adapterType, "GetKindSortOrder", "Recommendation title"));
            Assert.AreEqual(3, AdapterTestHelpers.InvokeStatic<int>(adapterType, "GetKindSortOrder", "Recommendation detail"));
            Assert.AreEqual(99, AdapterTestHelpers.InvokeStatic<int>(adapterType, "GetKindSortOrder", "other"));

            Assert.AreEqual("Display Name", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetFieldDisplayName", "new_name", new Dictionary<string, string> { ["new_name"] = "Display Name" }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetFieldDisplayName", null, new Dictionary<string, string>()));

            Assert.AreEqual("&<>\"", AdapterTestHelpers.InvokeStatic<string>(adapterType, "DecodeXmlAttribute", "&amp;&lt;&gt;&quot;"));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "DecodeXmlAttribute", new object[] { null }));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "EncodeXmlAttribute", new object[] { null }));

            var label = AdapterTestHelpers.BuildLabel((1041, ""), (1033, "Base"), (1031, "Other"));
            Assert.AreEqual("Base", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetLabelText", label, 1033));
            Assert.AreEqual("Base", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetLabelText", label, 1041));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetLabelText", new object[] { null, 1033 }));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetLabelText", AdapterTestHelpers.BuildLabel((1033, ""), (1041, " ")), 1033));

            var xaml = "<Activity><sco:Collection><mcwo:StepLabel LabelId=\"L1\" LanguageCode=\"1033\" /></sco:Collection></Activity>";
            var updated = AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpsertStepLabel", xaml, "L1", "1033", "X");
            StringAssert.Contains(updated, "Description=\"X\"");
            ExpectReflectionPluginException(() =>
                AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpsertStepLabel", "<Activity><mcwo:StepLabel LabelId=\"L1\" LanguageCode=\"1033\" /></Activity>", "L1", "1041", "X"),
                "Business rule label collection");

            Assert.AreEqual(-1, AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindLabelCollectionEnd", "<Activity />", 0));
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindLabelCollectionEnd", "<mcwc:SetMessage.StepLabels><mcwo:StepLabel /></mcwc:SetMessage.StepLabels>", 0) > 0);
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindLabelCollectionEnd", "<sco:Collection><mcwo:StepLabel /></sco:Collection>", 0) > 0);
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<int>(adapterType, "FindLabelCollectionEnd", "<sco:Collection><mcwc:SetMessage.StepLabels><mcwo:StepLabel /></mcwc:SetMessage.StepLabels></sco:Collection>", 0) > 0);

            var service = Substitute.For<IOrganizationService>();
            AdapterTestHelpers.InvokeStatic(adapterType, "RestoreWorkflow", service, Guid.NewGuid(), null, true);
            service.Received(1).Update(Arg.Any<Entity>());
            service = Substitute.For<IOrganizationService>();
            AdapterTestHelpers.InvokeStatic(adapterType, "RestoreWorkflow", service, Guid.NewGuid(), "original", false);
            service.Received(1).Update(Arg.Is<Entity>(entity => entity.Contains("xaml")));

            var blankLogical = new StringAttributeMetadata
            {
                SchemaName = "new_blank",
                MetadataId = Guid.NewGuid(),
                DisplayName = AdapterTestHelpers.BuildLabel((1033, "Blank"))
            };
            service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(AdapterTestHelpers.BuildEntityResponse("account", blankLogical));
            var displayNames = AdapterTestHelpers.InvokeStatic<Dictionary<string, string>>(adapterType, "LoadAttributeDisplayNames", service, "account", 1033);
            Assert.AreEqual(0, displayNames.Count);
        }

        private static object CreateStepLabel(Type adapterType, string labelId, string kind, string sourceField)
        {
            var labelType = adapterType.GetNestedType("BusinessRuleStepLabel", BindingFlags.NonPublic);
            var label = Activator.CreateInstance(labelType);
            labelType.GetProperty("LabelId").SetValue(label, labelId);
            labelType.GetProperty("Kind").SetValue(label, kind);
            labelType.GetProperty("SourceField").SetValue(label, sourceField);
            return label;
        }

        private static void SetStepLabelOrder(object label, int order)
        {
            label.GetType().GetProperty("SourceField").SetValue(label, "same");
            label.GetType().GetProperty("Order").SetValue(label, order);
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

        private static string GetContextKind(Type adapterType, string xaml, int index)
        {
            var context = AdapterTestHelpers.InvokeStatic(adapterType, "GetLabelContext", xaml, index);
            return Convert.ToString(context.GetType().GetProperty("Kind").GetValue(context));
        }
    }
}
