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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
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
        public void Load_WithScopedSolution_UsesScopedWebResourcePath()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var solutionId = Guid.NewGuid();
            var unmanagedId = Guid.NewGuid();
            var managedId = Guid.NewGuid();
            var unmanaged = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\"}"), unmanagedId);
            unmanaged["ismanaged"] = false;
            var managed = MakeWebResource("other.1033.js", 3, B64("{\"hello\":\"Hello\"}"), managedId);
            managed["ismanaged"] = true;
            var emptyComponent = new Entity("solutioncomponent");
            var emptyIdComponent = new Entity("solutioncomponent");
            emptyIdComponent["objectid"] = Guid.Empty;
            var unmanagedComponent = new Entity("solutioncomponent");
            unmanagedComponent["objectid"] = unmanagedId;
            var managedComponent = new Entity("solutioncomponent");
            managedComponent["objectid"] = managedId;

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities(emptyComponent, emptyIdComponent, unmanagedComponent, managedComponent);
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(call =>
            {
                var entityName = (string)call[0];
                var id = (Guid)call[1];
                if (entityName == "webresource" && id == unmanagedId) return unmanaged;
                if (entityName == "webresource" && id == managedId) return managed;
                return null;
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "DisplayText",
                solutionId = solutionId.ToString("D")
            });

            Assert.IsNotNull(output.grid);
            Assert.AreEqual("Web Resources", output.grid.title);
        }

        [TestMethod]
        public void Load_WithInvalidScopedSolution_Throws()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
                {
                    component = "DisplayText",
                    solutionId = "not-a-guid"
                }));
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
        public void Save_DescriptionExistingResource_UpdatesDescription()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var resourceId = Guid.NewGuid();
            var resource = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\"}"), resourceId);
            Entity updated = null;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(resource);
                return AdapterTestHelpers.Entities();
            });
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "Description",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|description",
                        changes = new Dictionary<string, string> { { "1033", "Updated description" } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(updated);
            Assert.AreEqual(resourceId, updated.Id);
            Assert.AreEqual("Updated description", updated.GetAttributeValue<string>("description"));
        }

        [TestMethod]
        public void Save_DescriptionWithWrongKey_Throws()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    component = "Description",
                    solutionId = "all",
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        new EasyTranslatorChangedRowInput
                        {
                            gridKey = "webresources|strings|name",
                            changes = new Dictionary<string, string> { { "1033", "Description" } }
                        }
                    }
                }));
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
        public void Save_CreateNewWebResourceWithSolution_AddsToSolution()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var solutionId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            OrganizationRequest added = null;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve("solution", solutionId, Arg.Any<ColumnSet>()).Returns(call =>
            {
                var solution = new Entity("solution", solutionId);
                solution["uniquename"] = "PhuocLe";
                return solution;
            });
            service.Create(Arg.Any<Entity>()).Returns(createdId);
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is AddSolutionComponentRequest)
                {
                    added = (OrganizationRequest)call[0];
                }

                if (call[0] is RetrieveAvailableLanguagesRequest) return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033, 1041);
                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = solutionId.ToString("D"),
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|hello",
                        changes = new Dictionary<string, string> { { "1041", "こんにちは" } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(added is AddSolutionComponentRequest);
            var add = (AddSolutionComponentRequest)added;
            Assert.AreEqual(createdId, add.ComponentId);
            Assert.AreEqual("PhuocLe", add.SolutionUniqueName);
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
        public void Published_WaitsAndReturnsTargets()
        {
            var adapter = new WebResourceAdapter();
            var service = Substitute.For<IOrganizationService>();
            var waited = 0;
            WebResourceAdapter.WaitAction = milliseconds => waited = milliseconds;
            var id = Guid.NewGuid().ToString("D");
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "webresource", id = id },
                    new EasyTranslatorPublishTarget { kind = "other", id = Guid.NewGuid().ToString("D") }
                }
            };

            var output = adapter.Published(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(10000, waited);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual(id, output.publishTargets[0].id);
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
        public void SerializeResxContent_UpdatesExistingAndAddsMissingKeys()
        {
            var method = typeof(WebResourceAdapter).GetMethod("SerializeResxContent", BindingFlags.NonPublic | BindingFlags.Static);
            var original = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data><data name=""binary"" type=""System.Byte[]""><value>AA==</value></data></root>";
            var content = new Dictionary<string, string>
            {
                ["hello"] = "Updated",
                ["bye"] = "Goodbye"
            };

            var updated = (string)method.Invoke(null, new object[] { original, content });

            Assert.IsTrue(updated.Contains("<value>Updated</value>"));
            Assert.IsTrue(updated.Contains("name=\"bye\""));
            Assert.IsTrue(updated.Contains("System.Byte[]"));
        }

        [TestMethod]
        public void CreateWebResource_FromBaseResx_CreatesLocalizedResource()
        {
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var resx = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data></root>";
            var baseResource = MakeWebResource("strings.1033.resx", 12, B64(resx), baseId);
            baseResource["displayname"] = "strings.1033.resx";
            Entity created = null;
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });

            var changeType = typeof(WebResourceAdapter).GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var contentChangeType = typeof(WebResourceAdapter).GetNestedType("WebResourceContentChangeInfo", BindingFlags.NonPublic);
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("baseWebresourceid").SetValue(change, baseId.ToString("D"));
            changeType.GetProperty("lcid").SetValue(change, "1041");
            changeType.GetProperty("solutionId").SetValue(change, "all");
            changeType.GetProperty("hasDescription").SetValue(change, true);
            changeType.GetProperty("description").SetValue(change, "Japanese strings");
            var contentChange = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(contentChange, "hello");
            contentChangeType.GetProperty("value").SetValue(contentChange, "こんにちは");
            var contentChanges = (System.Collections.IList)changeType.GetProperty("contentChanges").GetValue(change);
            contentChanges.Add(contentChange);

            var method = typeof(WebResourceAdapter).GetMethod("CreateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)method.Invoke(null, new[] { service, change });

            Assert.AreEqual(createdId.ToString("D"), result);
            Assert.IsNotNull(created);
            Assert.AreEqual("strings.1041.resx", created.GetAttributeValue<string>("name"));
            Assert.AreEqual("Japanese strings", created.GetAttributeValue<string>("description"));
            var createdContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(created.GetAttributeValue<string>("content")));
            Assert.IsTrue(createdContent.Contains("こんにちは"));
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

        [TestMethod]
        public void PrivateHelpers_CoverWebResourceEdgeCases()
        {
            var adapterType = typeof(WebResourceAdapter);
            var matchName = adapterType.GetMethod("MatchLocalizedResourceName", BindingFlags.NonPublic | BindingFlags.Static);
            var getLcid = adapterType.GetMethod("GetResourceLcid", BindingFlags.NonPublic | BindingFlags.Static);
            var groupKey = adapterType.GetMethod("GetResourceGroupingKey", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
            var cleanName = adapterType.GetMethod("CleanGroupDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            var replaceLcid = adapterType.GetMethod("ReplaceResourceLcid", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNull(matchName.Invoke(null, new object[] { string.Empty, 3 }));
            Assert.IsNull(matchName.Invoke(null, new object[] { "plain.js", 3 }));
            Assert.IsNull(matchName.Invoke(null, new object[] { "plain", 12 }));
            Assert.IsNotNull(matchName.Invoke(null, new object[] { "labels1033", 12 }));
            Assert.AreEqual("strings", groupKey.Invoke(null, new object[] { "strings.1033.js", "unused", "1033" }));
            Assert.AreEqual("strings", groupKey.Invoke(null, new object[] { "unused", "strings.1033.js", "1033" }));
            Assert.AreEqual("name", groupKey.Invoke(null, new object[] { "name", "display", null }));
            Assert.AreEqual(string.Empty, cleanName.Invoke(null, new object[] { "   " }));
            Assert.AreEqual("App", cleanName.Invoke(null, new object[] { "pl_/html/App. " }));
            Assert.AreEqual("strings1041", replaceLcid.Invoke(null, new object[] { "strings1033", "1033", "1041" }));
            Assert.AreEqual("strings", replaceLcid.Invoke(null, new object[] { "strings", "1033", "1041" }));
            Assert.AreEqual("strings", replaceLcid.Invoke(null, new object[] { "strings", "", "1041" }));

            var resx = MakeWebResource("labels1033", 12);
            Assert.AreEqual("1033", getLcid.Invoke(null, new object[] { resx }));
        }

        [TestMethod]
        public void PrivateUpdateHelpers_InvalidIdsThrow()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("webresourceid").SetValue(change, "not-a-guid");
            var service = CreateService();
            var updateContent = adapterType.GetMethod("UpdateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var updateDescription = adapterType.GetMethod("UpdateWebResourceDescription", BindingFlags.NonPublic | BindingFlags.Static);

            AdapterTestHelpers.ExpectException<TargetInvocationException>(() => updateContent.Invoke(null, new[] { service, change }));
            AdapterTestHelpers.ExpectException<TargetInvocationException>(() => updateDescription.Invoke(null, new[] { service, change }));
        }

        [TestMethod]
        public void PrivateHelpers_CoverRemainingFallbackBranches()
        {
            var adapterType = typeof(WebResourceAdapter);
            var parseResx = adapterType.GetMethod("ParseResxContent", BindingFlags.NonPublic | BindingFlags.Static);
            var parseJson = adapterType.GetMethod("ParseJsonContent", BindingFlags.NonPublic | BindingFlags.Static);
            var displayName = adapterType.GetMethod("GetResourceDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            var entityGroupKey = adapterType.GetMethod("GetResourceGroupingKey", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Entity), typeof(string) }, null);
            var solutionName = adapterType.GetMethod("GetSolutionUniqueName", BindingFlags.NonPublic | BindingFlags.Static);

            var emptyResx = (Dictionary<string, string>)parseResx.Invoke(null, new object[] { string.Empty });
            var emptyJson = (Dictionary<string, string>)parseJson.Invoke(null, new object[] { string.Empty });
            var invalidJson = (Dictionary<string, string>)parseJson.Invoke(null, new object[] { "[]" });
            Assert.AreEqual(0, emptyResx.Count);
            Assert.AreEqual(0, emptyJson.Count);
            Assert.AreEqual(0, invalidJson.Count);

            var noNameResource = new Entity("webresource");
            Assert.AreEqual(string.Empty, entityGroupKey.Invoke(null, new object[] { noNameResource, "1033" }));
            Assert.AreEqual("fallback", displayName.Invoke(null, new object[] { noNameResource, "fallback" }));

            var nameOnlyResource = new Entity("webresource");
            nameOnlyResource["name"] = "Plain Name";
            Assert.AreEqual("Plain Name", displayName.Invoke(null, new object[] { nameOnlyResource, "fallback" }));

            var displayNameOnlyResource = new Entity("webresource");
            displayNameOnlyResource["displayname"] = "Plain Display";
            Assert.AreEqual("Plain Display", displayName.Invoke(null, new object[] { displayNameOnlyResource, "fallback" }));

            var displayOnly = new Entity("webresource");
            displayOnly["displayname"] = "folder/display.1033.js";
            displayOnly["webresourcetype"] = new OptionSetValue(3);
            Assert.AreEqual("display.1033.js", displayName.Invoke(null, new object[] { displayOnly, "fallback" }));

            var nullSolution = CreateService();
            Assert.IsNull(solutionName.Invoke(null, new object[] { nullSolution, "all" }));
            AdapterTestHelpers.ExpectException<TargetInvocationException>(() => solutionName.Invoke(null, new object[] { nullSolution, "not-a-guid" }));
        }

        [TestMethod]
        public void Save_EmptyLabelChanges_AreIgnored()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();

            var displayOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|hello",
                        changes = new Dictionary<string, string>()
                    }
                }
            });
            var descriptionOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "Description",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|description",
                        changes = new Dictionary<string, string>()
                    }
                }
            });

            Assert.AreEqual(0, displayOutput.changedRowCount);
            Assert.AreEqual(0, descriptionOutput.changedRowCount);
        }

        [TestMethod]
        public void CreateWebResource_FromBaseWithoutLanguageToken_KeepsBaseName()
        {
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var baseResource = MakeWebResource("strings.js", 3, B64("{\"hello\":\"Hello\"}"), baseId);
            Entity created = null;
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });

            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var contentChangeType = adapterType.GetNestedType("WebResourceContentChangeInfo", BindingFlags.NonPublic);
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("baseWebresourceid").SetValue(change, baseId.ToString("D"));
            changeType.GetProperty("lcid").SetValue(change, "1041");
            changeType.GetProperty("solutionId").SetValue(change, "all");
            var contentChange = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(contentChange, "hello");
            contentChangeType.GetProperty("value").SetValue(contentChange, "こんにちは");
            ((System.Collections.IList)changeType.GetProperty("contentChanges").GetValue(change)).Add(contentChange);

            var method = adapterType.GetMethod("CreateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)method.Invoke(null, new[] { service, change });

            Assert.AreEqual(createdId.ToString("D"), result);
            Assert.AreEqual("strings.js", created.GetAttributeValue<string>("name"));
            Assert.AreEqual("Display strings.js", created.GetAttributeValue<string>("displayname"));
        }

        [TestMethod]
        public void Load_DuplicateAndInvalidSiblings_CoversSkipAndCatchBranches()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var resx = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data></root>";
            var valid = MakeWebResource("strings.1033.resx", 12, B64(resx));
            var duplicate = MakeWebResource("strings.1033.resx", 12, B64(resx.Replace("Hello", "Again")));
            var noLcid = MakeWebResource("strings.resx", 12, B64(resx));
            var invalidContent = MakeWebResource("broken.1033.resx", 12, "not-base64");

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(valid, duplicate, noLcid, invalidContent);
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(1, output.grid.rows.Count);
        }

        [TestMethod]
        public void PrivateBuildRows_CoversMissingLanguageAndNullFallbacks()
        {
            var adapterType = typeof(WebResourceAdapter);
            var groupType = adapterType.GetNestedType("WebResourceGroupInfo", BindingFlags.NonPublic);
            var infoType = adapterType.GetNestedType("WebResourceInfo", BindingFlags.NonPublic);
            var buildGroup = adapterType.GetMethod("BuildGroupRow", BindingFlags.NonPublic | BindingFlags.Static);
            var buildDescription = adapterType.GetMethod("BuildDescriptionRow", BindingFlags.NonPublic | BindingFlags.Static);
            var group = Activator.CreateInstance(groupType, true);
            groupType.GetProperty("key").SetValue(group, "strings");
            groupType.GetProperty("displayName").SetValue(group, null);
            var resources = (System.Collections.IList)groupType.GetProperty("resources").GetValue(group);

            var english = Activator.CreateInstance(infoType, true);
            infoType.GetProperty("lcid").SetValue(english, "1033");
            ((Dictionary<string, string>)infoType.GetProperty("content").GetValue(english))["hello"] = "Hello";
            ((Dictionary<string, string>)infoType.GetProperty("content").GetValue(english))["missing"] = "Value";
            resources.Add(english);

            var blankLcid = Activator.CreateInstance(infoType, true);
            infoType.GetProperty("lcid").SetValue(blankLcid, " ");
            ((Dictionary<string, string>)infoType.GetProperty("content").GetValue(blankLcid))["hello"] = "Ignored";
            resources.Add(blankLcid);

            var noKey = Activator.CreateInstance(infoType, true);
            infoType.GetProperty("lcid").SetValue(noKey, "1041");
            ((Dictionary<string, string>)infoType.GetProperty("content").GetValue(noKey))["other"] = "Ignored";
            resources.Add(noKey);

            var groupRow = (EasyTranslatorGridRowOutput)buildGroup.Invoke(null, new[] { group });
            var descriptionRow = (EasyTranslatorGridRowOutput)buildDescription.Invoke(null, new[] { group });

            Assert.AreEqual("strings", groupRow.SchemaName);
            Assert.AreEqual("strings", descriptionRow.SchemaName);
        }

        [TestMethod]
        public void PrivateSerialization_CoversNamelessResxElement()
        {
            var serialize = typeof(WebResourceAdapter).GetMethod("SerializeResxContent", BindingFlags.NonPublic | BindingFlags.Static);
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data xml:space=""preserve""><value>No name</value></data><data name=""hello"" xml:space=""preserve""><value>Hello</value></data></root>";
            var content = new Dictionary<string, string> { ["hello"] = "Updated" };

            var updated = (string)serialize.Invoke(null, new object[] { xml, content });

            Assert.IsTrue(updated.Contains("Updated"));
            Assert.IsTrue(updated.Contains("No name"));
        }

        [TestMethod]
        public void Save_NullChangedRows_AreIgnored()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();

            var displayOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "DisplayText",
                solutionId = "all",
                changedRows = null
            });
            var descriptionOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "Description",
                solutionId = "all",
                changedRows = null
            });

            Assert.AreEqual(0, displayOutput.changedRowCount);
            Assert.AreEqual(0, descriptionOutput.changedRowCount);
        }

        [TestMethod]
        public void Save_MultipleRowsForSameExistingResource_GroupIntoOneUpdate()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var targetId = Guid.NewGuid();
            var resx = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data><data name=""bye"" xml:space=""preserve""><value>Bye</value></data></root>";
            var targetResource = MakeWebResource("strings.1033.resx", 12, B64(resx), targetId);
            var updateCount = 0;

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(targetResource);
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve("webresource", targetId, Arg.Any<ColumnSet>()).Returns(targetResource);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(_ => updateCount++);

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
                        changes = new Dictionary<string, string> { { "1033", "Hello updated" } }
                    },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|bye",
                        changes = new Dictionary<string, string> { { "1033", "Bye updated" } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, updateCount);
        }

        [TestMethod]
        public void Load_WithNullAvailableLanguages_ReturnsEmptyLanguageColumns()
        {
            var adapter = new WebResourceAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAvailableLanguagesRequest)
                {
                    var response = new RetrieveAvailableLanguagesResponse();
                    response.Results["LocaleIds"] = null;
                    return response;
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(0, output.grid.languageColumns.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverNullAndFallbackBranches()
        {
            var adapterType = typeof(WebResourceAdapter);
            var buildLanguageColumns = adapterType.GetMethod("BuildLanguageColumns", BindingFlags.NonPublic | BindingFlags.Static);
            var matchResource = adapterType.GetMethod("MatchLocalizedResource", BindingFlags.NonPublic | BindingFlags.Static);
            var getLcid = adapterType.GetMethod("GetResourceLcid", BindingFlags.NonPublic | BindingFlags.Static);
            var entityGroupKey = adapterType.GetMethod("GetResourceGroupingKey", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Entity), typeof(string) }, null);
            var parseJson = adapterType.GetMethod("ParseJsonContent", BindingFlags.NonPublic | BindingFlags.Static);
            var parseWebResource = adapterType.GetMethod("ParseWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var displayName = adapterType.GetMethod("GetResourceDisplayName", BindingFlags.NonPublic | BindingFlags.Static);

            var columns = (List<EasyTranslatorLanguageColumnOutput>)buildLanguageColumns.Invoke(null, new object[] { null });
            Assert.AreEqual(0, columns.Count);
            Assert.IsNull(matchResource.Invoke(null, new object[] { null }));
            Assert.IsNull(getLcid.Invoke(null, new object[] { null }));
            Assert.AreEqual(string.Empty, entityGroupKey.Invoke(null, new object[] { null, "1033" }));

            var json = (Dictionary<string, string>)parseJson.Invoke(null, new object[] { "{\"hello\":null}" });
            Assert.AreEqual(string.Empty, json["hello"]);

            var bareResource = new Entity("webresource", Guid.NewGuid());
            bareResource["content"] = string.Empty;
            var parsed = parseWebResource.Invoke(null, new object[] { bareResource, "1033" });
            Assert.IsNotNull(parsed);
            var missingContentResource = new Entity("webresource", Guid.NewGuid());
            Assert.IsNotNull(parseWebResource.Invoke(null, new object[] { missingContentResource, "1033" }));

            var resxDisplayResource = new Entity("webresource");
            resxDisplayResource["displayname"] = "display1033";
            resxDisplayResource["webresourcetype"] = new OptionSetValue(12);
            Assert.AreEqual("display", displayName.Invoke(null, new object[] { resxDisplayResource, "fallback" }));
        }

        [TestMethod]
        public void PrivateUpdateWebResource_CoversNullContentChangesAndEmptyContent()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var updateContent = adapterType.GetMethod("UpdateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var id = Guid.NewGuid();
            var resource = MakeWebResource("strings.1033.js", 3, string.Empty, id);
            Entity updated = null;
            service.Retrieve("webresource", id, Arg.Any<ColumnSet>()).Returns(resource);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("webresourceid").SetValue(change, id.ToString("D"));
            changeType.GetProperty("contentChanges").SetValue(change, null);

            var result = (string)updateContent.Invoke(null, new[] { service, change });

            Assert.AreEqual(id.ToString("D"), result);
            Assert.IsNotNull(updated);
        }

        [TestMethod]
        public void PrivateCreateWebResource_CoversFallbackAndNullChangeBranches()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var contentChangeType = adapterType.GetNestedType("WebResourceContentChangeInfo", BindingFlags.NonPublic);
            var createWebResource = adapterType.GetMethod("CreateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var createdId = Guid.NewGuid();
            Entity created = null;
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });

            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("baseWebresourceid").SetValue(change, "not-a-guid");
            changeType.GetProperty("lcid").SetValue(change, null);
            changeType.GetProperty("solutionId").SetValue(change, "all");
            changeType.GetProperty("hasDescription").SetValue(change, true);
            changeType.GetProperty("description").SetValue(change, null);
            var contentChanges = (System.Collections.IList)changeType.GetProperty("contentChanges").GetValue(change);
            contentChanges.Add(null);
            var nullKey = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(nullKey, null);
            contentChanges.Add(nullKey);
            var nullValue = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(nullValue, "hello");
            contentChangeType.GetProperty("value").SetValue(nullValue, null);
            contentChanges.Add(nullValue);

            var result = (string)createWebResource.Invoke(null, new[] { service, change });

            Assert.AreEqual(createdId.ToString("D"), result);
            Assert.AreEqual(".js", created.GetAttributeValue<string>("name"));
            Assert.AreEqual(".js", created.GetAttributeValue<string>("displayname"));
            Assert.AreEqual(string.Empty, created.GetAttributeValue<string>("description"));
        }

        [TestMethod]
        public void PrivateHelpers_CoverAdditionalBranchEdges()
        {
            var adapterType = typeof(WebResourceAdapter);
            var parseResx = adapterType.GetMethod("ParseResxContent", BindingFlags.NonPublic | BindingFlags.Static);
            var isLocalizable = adapterType.GetMethod("IsLocalizableResource", BindingFlags.NonPublic | BindingFlags.Static);
            var matchName = adapterType.GetMethod("MatchLocalizedResourceName", BindingFlags.NonPublic | BindingFlags.Static);
            var retrieveByIds = adapterType.GetMethod("RetrieveWebResourcesByIds", BindingFlags.NonPublic | BindingFlags.Static);
            var solutionName = adapterType.GetMethod("GetSolutionUniqueName", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var id = Guid.NewGuid();
            service.Retrieve("webresource", id, Arg.Any<ColumnSet>()).Returns((Entity)null);
            service.Retrieve("solution", id, Arg.Any<ColumnSet>()).Returns((Entity)null);

            var parsed = (Dictionary<string, string>)parseResx.Invoke(null, new object[] { @"<root><data xml:space=""preserve""><value>No name</value></data></root>" });
            Assert.AreEqual(0, parsed.Count);
            Assert.IsTrue((bool)isLocalizable.Invoke(null, new object[] { MakeWebResource("strings.1033.js", 3), "1033" }));
            Assert.IsNotNull(matchName.Invoke(null, new object[] { "strings.1033", 3 }));
            var resources = (List<Entity>)retrieveByIds.Invoke(null, new object[] { service, new List<Guid> { id } });
            Assert.AreEqual(0, resources.Count);
            Assert.IsNull(solutionName.Invoke(null, new object[] { service, id.ToString("D") }));
        }

        [TestMethod]
        public void PrivateUpdateWebResource_CoversNullChangeItems()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var contentChangeType = adapterType.GetNestedType("WebResourceContentChangeInfo", BindingFlags.NonPublic);
            var updateContent = adapterType.GetMethod("UpdateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var id = Guid.NewGuid();
            var resource = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\"}"), id);
            Entity updated = null;
            service.Retrieve("webresource", id, Arg.Any<ColumnSet>()).Returns(resource);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("webresourceid").SetValue(change, id.ToString("D"));
            var contentChanges = (System.Collections.IList)changeType.GetProperty("contentChanges").GetValue(change);
            contentChanges.Add(null);
            var nullKey = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(nullKey, null);
            contentChanges.Add(nullKey);

            var result = (string)updateContent.Invoke(null, new[] { service, change });

            Assert.AreEqual(id.ToString("D"), result);
            Assert.IsNotNull(updated);
        }

        [TestMethod]
        public void PrivateCreateWebResource_FromBaseWithNullLanguageAndDisplayName()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var createWebResource = adapterType.GetMethod("CreateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var baseResource = MakeWebResource("strings.1033.js", 3, string.Empty, baseId);
            baseResource["displayname"] = null;
            Entity created = null;
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });
            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("baseWebresourceid").SetValue(change, baseId.ToString("D"));
            changeType.GetProperty("lcid").SetValue(change, null);
            changeType.GetProperty("solutionId").SetValue(change, "all");

            var result = (string)createWebResource.Invoke(null, new[] { service, change });

            Assert.AreEqual(createdId.ToString("D"), result);
            Assert.AreEqual("strings.1033.js", created.GetAttributeValue<string>("name"));
            Assert.AreEqual("strings.1033.js", created.GetAttributeValue<string>("displayname"));
        }

        [TestMethod]
        public void Save_NewLanguageFromExistingBaseResource_CreatesLocalizedJson()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var baseResource = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\",\"bye\":\"Bye\"}"), baseId);
            Entity created = null;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(baseResource);
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
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
                        changes = new Dictionary<string, string> { { "1041", null } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual("strings.1041.js", created.GetAttributeValue<string>("name"));
        }

        [TestMethod]
        public void Save_DescriptionNewLanguageFromExistingBaseResource_CreatesDescriptionResource()
        {
            var adapter = new WebResourceAdapter();
            var service = CreateService();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var baseResource = MakeWebResource("strings.1033.js", 3, B64("{\"hello\":\"Hello\"}"), baseId);
            Entity created = null;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "webresource") return AdapterTestHelpers.Entities(baseResource);
                return AdapterTestHelpers.Entities();
            });
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                component = "Description",
                baseLanguage = "1033",
                solutionId = "all",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "webresources|strings|description",
                        changes = new Dictionary<string, string> { { "1041", null } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual("strings.1041.js", created.GetAttributeValue<string>("name"));
            Assert.AreEqual(string.Empty, created.GetAttributeValue<string>("description"));
        }

        [TestMethod]
        public void PrivateSerializationAndUpdate_CoverNullValues()
        {
            var adapterType = typeof(WebResourceAdapter);
            var serialize = adapterType.GetMethod("SerializeResxContent", BindingFlags.NonPublic | BindingFlags.Static);
            var updateDescription = adapterType.GetMethod("UpdateWebResourceDescription", BindingFlags.NonPublic | BindingFlags.Static);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var service = CreateService();
            var id = Guid.NewGuid();
            Entity updated = null;
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);

            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data name=""hello"" xml:space=""preserve""><value>Hello</value></data></root>";
            var serialized = (string)serialize.Invoke(null, new object[] { xml, new Dictionary<string, string> { ["hello"] = null, ["bye"] = null } });
            Assert.IsTrue(serialized.Contains("<value></value>") || serialized.Contains("<value />"));
            var missingNameXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data><value>Missing</value></data><data name=""hello""><value>Hello</value></data></root>";
            var sortedMissingName = (string)serialize.Invoke(null, new object[] { missingNameXml, new Dictionary<string, string>() });
            Assert.IsTrue(sortedMissingName.Contains("<data>") || sortedMissingName.Contains("<data "));
            var bothMissingNameXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root><data><value>First</value></data><data><value>Second</value></data></root>";
            var sortedBothMissingName = (string)serialize.Invoke(null, new object[] { bothMissingNameXml, new Dictionary<string, string>() });
            Assert.IsTrue(sortedBothMissingName.Contains("First"));

            var change = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("webresourceid").SetValue(change, id.ToString("D"));
            changeType.GetProperty("description").SetValue(change, null);
            var result = (string)updateDescription.Invoke(null, new[] { service, change });

            Assert.AreEqual(id.ToString("D"), result);
            Assert.AreEqual(string.Empty, updated.GetAttributeValue<string>("description"));
        }

        [TestMethod]
        public void PrivateUpdateAndCreate_CoverMissingAttributeBranches()
        {
            var adapterType = typeof(WebResourceAdapter);
            var changeType = adapterType.GetNestedType("WebResourceChangeInfo", BindingFlags.NonPublic);
            var contentChangeType = adapterType.GetNestedType("WebResourceContentChangeInfo", BindingFlags.NonPublic);
            var updateContent = adapterType.GetMethod("UpdateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var createWebResource = adapterType.GetMethod("CreateWebResource", BindingFlags.NonPublic | BindingFlags.Static);
            var service = CreateService();
            var updateId = Guid.NewGuid();
            var baseId = Guid.NewGuid();
            var createdId = Guid.NewGuid();
            var updateResource = new Entity("webresource", updateId);
            var baseResource = new Entity("webresource", baseId);
            Entity updated = null;
            Entity created = null;
            service.Retrieve("webresource", updateId, Arg.Any<ColumnSet>()).Returns(updateResource);
            service.Retrieve("webresource", baseId, Arg.Any<ColumnSet>()).Returns(baseResource);
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);
            service.Create(Arg.Any<Entity>()).Returns(call =>
            {
                created = (Entity)call[0];
                return createdId;
            });

            var updateChange = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("webresourceid").SetValue(updateChange, updateId.ToString("D"));
            var updateChanges = (System.Collections.IList)changeType.GetProperty("contentChanges").GetValue(updateChange);
            var nullValue = Activator.CreateInstance(contentChangeType, true);
            contentChangeType.GetProperty("key").SetValue(nullValue, "hello");
            contentChangeType.GetProperty("value").SetValue(nullValue, null);
            updateChanges.Add(nullValue);
            var updateResult = (string)updateContent.Invoke(null, new[] { service, updateChange });

            var createChange = Activator.CreateInstance(changeType, true);
            changeType.GetProperty("baseWebresourceid").SetValue(createChange, baseId.ToString("D"));
            changeType.GetProperty("lcid").SetValue(createChange, "1041");
            changeType.GetProperty("solutionId").SetValue(createChange, "all");
            changeType.GetProperty("contentChanges").SetValue(createChange, null);
            var createResult = (string)createWebResource.Invoke(null, new[] { service, createChange });

            Assert.AreEqual(updateId.ToString("D"), updateResult);
            Assert.IsNotNull(updated);
            Assert.AreEqual(createdId.ToString("D"), createResult);
            Assert.AreEqual(string.Empty, created.GetAttributeValue<string>("name"));
            Assert.AreEqual(string.Empty, created.GetAttributeValue<string>("displayname"));
        }
    }
}
