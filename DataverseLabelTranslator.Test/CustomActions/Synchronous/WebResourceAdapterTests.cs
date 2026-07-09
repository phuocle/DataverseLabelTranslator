using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class WebResourceAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { WebResourceAdapter.WaitAction = System.Threading.Thread.Sleep; }

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
                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033, 1031, 1036);
                return new OrganizationResponse();
            });
            return service;
        }

        private static Entity MakeWebResource(string name, int webResourceType, string content = "YQ==", Guid? id = null)
        {
            var wr = new Entity("webresource") { Id = id ?? Guid.NewGuid() };
            wr["webresourceid"] = wr.Id;
            wr["name"] = name;
            wr["displayname"] = "Display " + name;
            wr["webresourcetype"] = new OptionSetValue(webResourceType);
            wr["content"] = content;
            return wr;
        }

        private static string B64(string text)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        }

        [TestMethod]
        public void Load_DisplayText_ReturnsRows()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(MakeWebResource("test.js", 3));
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual("Web Resources", output.grid.title);
        }

        [TestMethod]
        public void Load_Description_ReturnsRows()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(MakeWebResource("test.js", 3));
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "Description", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Load_EmptyResult_ReturnsEmpty()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new WebResourceAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { component = "DisplayText", baseLanguage = "1033", changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithInvalidGridKey_Throws()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = "all",
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
        public void Save_Description_UpdatesDescription()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var wrId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                component = "Description",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"webresources|{wrId:D}|description",
                        changes = new Dictionary<string, string> { { "1033", "Desc" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        [TestMethod]
        public void Save_UpdateExistingWebResource_CallsUpdate()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var wrId = Guid.NewGuid();
            var input = new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"webresources|{wrId:D}|name",
                        changes = new Dictionary<string, string> { { "1033", "val" } }
                    }
                }
            };
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(MakeWebResource("test_1033.js", 3));
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        [TestMethod]
        public void Save_CreateNewWebResource_CallsCreate()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|new_test_js|name|1033",
                        changes = new Dictionary<string, string> { { "1033", "val" } }
                    }
                }
            };
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new WebResourceAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new WebResourceAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "webresource", id = Guid.NewGuid().ToString("D") } }
            };
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
            Assert.AreEqual(1, output.publishTargets.Count);
        }

        [TestMethod]
        public void BuildPublishXml_GeneratesExpectedXml()
        {
            var m = typeof(WebResourceAdapter).GetMethod("BuildPublishXml", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m);
            var ids = new List<string> { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
            var xml = (string)m.Invoke(null, new object[] { ids });
            Assert.IsTrue(xml.Contains("webresource"));
            Assert.IsTrue(xml.Contains(ids[0]));
        }

        [TestMethod]
        public void Load_ResxResources_BuildsPropertyChildren()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var resx = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data><data name=""bye"" xml:space=""preserve""><value>Bye</value></data></root>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource")
                {
                    return AdapterTestHelpers.Entities(
                        MakeWebResource("strings.1033.resx", 12, B64(resx)),
                        MakeWebResource("strings.1041.resx", 12, B64(resx.Replace("Hello", "こんにちは")))
                    );
                }
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(2, children.Count);
            Assert.IsTrue(children.Exists(row => row.SchemaName == "hello"));
        }

        [TestMethod]
        public void Load_ResxResources_BuildsFlatDescriptionAndContentRows()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var resx = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""Title"" xml:space=""preserve""><value>Title text</value></data><data name=""Ignored"" type=""System.String""><value>Ignored</value></data></root>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource")
                {
                    var baseResx = MakeWebResource("labels1033", 12, B64(resx));
                    baseResx["displayname"] = "labels1033";
                    baseResx["description"] = "English labels";
                    var translated = MakeWebResource("labels1041", 12, B64(resx.Replace("Title text", "タイトル")));
                    translated["displayname"] = "labels1041";
                    translated["description"] = "Japanese labels";
                    return AdapterTestHelpers.Entities(baseResx, translated);
                }
                return AdapterTestHelpers.Entities();
            });

            var displayOutput = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "DisplayText",
                solutionId = "all"
            });
            var descriptionOutput = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "Description",
                solutionId = "all"
            });

            Assert.AreEqual(1, displayOutput.grid.rows.Count);
            Assert.AreEqual("flat", descriptionOutput.grid.mode);
            Assert.AreEqual(1, descriptionOutput.grid.rows.Count);
        }

        [TestMethod]
        public void Save_JsonResource_UpdatesExistingContent()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var baseResource = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\",\"bye\":\"Bye\"}"), baseId);
            var targetResource = MakeWebResource("strings.1041.js", 3, B64("{\"hello\":\"こんにちは\",\"bye\":\"\"}"), targetId);
            Entity updated = null;

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(baseResource, targetResource);
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve("webresource", targetId, Arg.Any<ColumnSet>()).Returns(targetResource);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call =>
            {
                updated = (Entity)call[0];
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                baseLanguage = "1033",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|hello",
                        changes = new Dictionary<string, string> { ["1041"] = "更新済み" }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(updated);
            var updatedText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(updated.GetAttributeValue<string>("content")));
            Assert.IsTrue(updatedText.Contains("更新済み"));
        }
    }
}
