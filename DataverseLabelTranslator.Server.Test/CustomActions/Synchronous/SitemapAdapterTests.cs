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
using System.Reflection;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class SitemapAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { SitemapAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateServiceWithBaseLanguage()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solution") return AdapterTestHelpers.Entities(new Entity("solution") { Id = Guid.NewGuid(), ["uniquename"] = "test" });
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Load_NoSitemaps_ReturnsEmpty()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithSitemapHavingEmptyXml_SkipsEntry()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = ""; // empty
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithSitemapHavingValidXml_ReturnsRows()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Title=\"Test Area\"><Group Title=\"Test Group\"><SubArea Title=\"Test SubArea\"/></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithSolutionFilterAndEntityFallbackLabels_ReturnsRows()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemapId = Guid.NewGuid();
            var solutionId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapid"] = sitemapId;
            sitemap["sitemapname"] = null;
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\" Entity=\"account\" /></Group></Area></SiteMap>";

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("solutioncomponent") { ["objectid"] = sitemapId },
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty });
                }
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest req && req.LogicalName == "account")
                {
                    var metadata = new EntityMetadata { LogicalName = "account", DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account")) };
                    var response = new RetrieveEntityResponse();
                    response.Results["EntityMetadata"] = metadata;
                    return response;
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                solutionId = solutionId.ToString("D"),
                component = "DisplayText"
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual(sitemapId.ToString("D"), output.grid.rows[0].SchemaName);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.IsTrue(children.Exists(row => row.SchemaName.Contains("account") && Equals(row["1033"], "Account")));
        }

        [TestMethod]
        public void Load_WithSolutionFilterAndNoSolutionSitemapIds_ReturnsEmpty()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities();
                Assert.AreNotEqual("sitemap", q.EntityName, "Sitemap query should not run when solution has no sitemap ids.");
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                solutionId = Guid.NewGuid().ToString("D"),
                component = "DisplayText"
            });

            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithDescriptionComponent_UsesDescription()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Title=\"Test Area\"><Group><SubArea/></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "Description" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithDescriptionXmlLabels_UsesDescriptionValues()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"area\"><Descriptions><Description LCID=\"1033\" Description=\"Area description\" /></Descriptions></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "Description" });
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual("Area description", children[0]["1033"]);
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionIdInvalid()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "not-a-guid" }));
        }

        [TestMethod]
        public void Save_EmptyChanges_ReturnsEmpty()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_InvalidGridKey_Throws()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "options|account|new_field|DisplayName",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_ValidSubAreaTitle_UpdatesSitemapAndAddsPublishTarget()
        {
            var adapter = new SitemapAdapter();
            var sitemapId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\"><Titles><Title LCID=\"1033\" Title=\"Old\" /></Titles></SubArea></Group></Area></SiteMap>";
            var service = Substitute.For<IOrganizationService>();
            Entity updated = null;
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());
            service.When(s => s.Update(Arg.Any<Entity>())).Do(call => updated = (Entity)call[0]);
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|SubArea|area%7Cgroup%7Csub|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.IsNotNull(updated);
            Assert.IsTrue(updated.GetAttributeValue<string>("sitemapxml").Contains("Title=\"New\""));
        }

        [TestMethod]
        public void Save_NoValidChangesAndNoXmlChange_SkipsUpdates()
        {
            var adapter = new SitemapAdapter();
            var sitemapId = Guid.NewGuid();
            var xml = "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"Same\" /></Titles></Area></SiteMap>";
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapxml"] = xml;
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());

            var emptyOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|Area|area|DisplayText",
                        changes = new Dictionary<string, string> { { "bad", "Ignored" } }
                    }
                }
            });
            Assert.AreEqual(0, emptyOutput.changedRowCount);

            var sameOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|Area|area|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "Same" } }
                    }
                }
            });

            Assert.AreEqual(0, sameOutput.changedRowCount);
            service.DidNotReceive().Update(Arg.Any<Entity>());
        }

        [TestMethod]
        public void Save_WhenSitemapBelongsToAppModule_AddsAppModulePublishTarget()
        {
            var adapter = new SitemapAdapter();
            var sitemapId = Guid.NewGuid();
            var appModuleId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"Old\" /></Titles></Area></SiteMap>";
            var appComponent = new Entity("appmodulecomponent");
            appComponent["app.appmoduleid"] = new AliasedValue("appmodule", "appmoduleid", appModuleId);
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "appmodulecomponent") return AdapterTestHelpers.Entities(appComponent);
                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|Area|area|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual("appmodule", output.publishTargets[0].kind);
            Assert.AreEqual(appModuleId.ToString("D"), output.publishTargets[0].id);
        }

        [TestMethod]
        public void UpdateSitemapXml_UpdatesNestedSubAreaTitle()
        {
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\"><Titles><Title LCID=\"1033\" Title=\"Old\" /></Titles></SubArea></Group></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area|group|sub");
            updateType.GetProperty("nodeType").SetValue(update, "SubArea");
            updateType.GetProperty("component").SetValue(update, "DisplayText");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New" },
                new EasyTranslatorLabelChange { LanguageCode = 1031, Label = "Neu" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            Assert.IsTrue(updated.Contains("Title=\"New\""));
            Assert.IsTrue(updated.Contains("LCID=\"1031\""));
        }

        [TestMethod]
        public void ResolveAppModuleIdsForSitemap_ReturnsDistinctAliasedIds()
        {
            var sitemapId = Guid.NewGuid();
            var appId = Guid.NewGuid();
            var referencedAppId = Guid.NewGuid();
            var duplicate = new Entity("appmodulecomponent");
            duplicate["app.appmoduleid"] = new AliasedValue("appmodule", "appmoduleid", appId);
            var referenced = new Entity("appmodulecomponent");
            referenced["app.appmoduleid"] = new AliasedValue("appmodule", "appmoduleid", new EntityReference("appmodule", referencedAppId));
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(duplicate, duplicate, referenced));
            var method = typeof(SitemapAdapter).GetMethod("ResolveAppModuleIdsForSitemap", BindingFlags.NonPublic | BindingFlags.Static);

            var ids = (List<Guid>)method.Invoke(null, new object[] { service, sitemapId });

            Assert.AreEqual(2, ids.Count);
            CollectionAssert.Contains(ids, appId);
            CollectionAssert.Contains(ids, referencedAppId);
        }

        [TestMethod]
        public void PrivateHelpers_CoverSitemapEdgeBranches()
        {
            var adapterType = typeof(SitemapAdapter);
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<Guid?>(adapterType, "GetAliasedGuid", null, "x"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<Guid?>(adapterType, "GetAliasedGuid", new Entity("x"), "missing"));
            var nonGuidAlias = new Entity("x");
            nonGuidAlias["alias"] = new AliasedValue("x", "alias", "not-guid");
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<Guid?>(adapterType, "GetAliasedGuid", nonGuidAlias, "alias"));

            Assert.AreEqual(Guid.Empty, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", null, "id"));
            var refId = Guid.NewGuid();
            Assert.AreEqual(refId, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", new Entity("x") { ["id"] = new EntityReference("x", refId) }, "id"));
            var fallback = new Entity("x") { Id = Guid.NewGuid() };
            Assert.AreEqual(fallback.Id, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", fallback, "id"));

            var updateType = adapterType.GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>());
            Assert.AreEqual("<SiteMap />", AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpdateSitemapXml", "<SiteMap />", update));

            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                adapterType.GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "", update }));

            var updateWithMissingNode = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(updateWithMissingNode, "missing");
            updateType.GetProperty("nodeType").SetValue(updateWithMissingNode, "Area");
            updateType.GetProperty("component").SetValue(updateWithMissingNode, "DisplayText");
            updateType.GetProperty("labels").SetValue(updateWithMissingNode, new List<EasyTranslatorLabelChange> { new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "X" } });
            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                adapterType.GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { "<SiteMap />", updateWithMissingNode }));

            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                adapterType.GetMethod("ValidateSitemapId", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "bad" }));

            var updateCreateContainer = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(updateCreateContainer, "area");
            updateType.GetProperty("nodeType").SetValue(updateCreateContainer, "Area");
            updateType.GetProperty("component").SetValue(updateCreateContainer, "Description");
            updateType.GetProperty("labels").SetValue(updateCreateContainer, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Created description" }
            });
            var createdContainerXml = (string)adapterType.GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new[] { "<SiteMap><Area Id=\"area\" /></SiteMap>", updateCreateContainer });
            Assert.IsTrue(createdContainerXml.Contains("Descriptions"));

            var root = XElement.Parse("<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\" /></Group></Area></SiteMap>");
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", null, "area", "Area"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, null, "Area"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "", "Area"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "other", "Area"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|other", "Group"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group|other", "SubArea"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group|sub|extra", "SubArea"));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area", "Group"));
            Assert.IsNotNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group", "Group"));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsNodeType", null, "Area"));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetNodeId", (object)null));

            var sitemapInfoType = adapterType.GetNestedType("SitemapInfo", BindingFlags.NonPublic);
            var invalidSitemap = Activator.CreateInstance(sitemapInfoType);
            sitemapInfoType.GetProperty("sitemapxml").SetValue(invalidSitemap, "<SiteMap><Area>");
            var sitemapListType = typeof(List<>).MakeGenericType(sitemapInfoType);
            var sitemapList = Activator.CreateInstance(sitemapListType);
            sitemapListType.GetMethod("Add").Invoke(sitemapList, new[] { invalidSitemap });
            var getNames = adapterType.GetMethod("GetReferencedEntityNames", BindingFlags.NonPublic | BindingFlags.Static);
            var names = (List<string>)getNames.Invoke(null, new[] { sitemapList });
            Assert.AreEqual(0, names.Count);

            var retrieveLabels = adapterType.GetMethod("RetrieveReferencedEntityLabels", BindingFlags.NonPublic | BindingFlags.Static);
            var service = Substitute.For<IOrganizationService>();
            var validSitemap = Activator.CreateInstance(sitemapInfoType);
            sitemapInfoType.GetProperty("sitemapxml").SetValue(validSitemap, "<SiteMap><Area><Group><SubArea Entity=\"account\" /></Group></Area></SiteMap>");
            var validList = Activator.CreateInstance(sitemapListType);
            sitemapListType.GetMethod("Add").Invoke(validList, new[] { validSitemap });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(_ => throw new Exception("metadata unavailable"));
            var labels = (Dictionary<string, Label>)retrieveLabels.Invoke(null, new[] { service, validList });
            Assert.AreEqual(0, labels.Count);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Publish_WithSitemapTarget_ExecutesPublishXml()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "sitemap", id = Guid.NewGuid().ToString() } }
            };
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Publish_WithAppModuleTarget_ExecutesPublishXml()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "appmodule", id = Guid.NewGuid().ToString() } }
            };
            var output = adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(captured is PublishXmlRequest);
        }

        [TestMethod]
        public void Published_WaitsAndReturnsTargets()
        {
            var waited = 0;
            SitemapAdapter.WaitAction = ms => waited = ms;
            var adapter = new SitemapAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(Substitute.For<IOrganizationService>()), new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "sitemap", id = Guid.NewGuid().ToString("D") },
                    new EasyTranslatorPublishTarget { kind = "appmodule", id = Guid.NewGuid().ToString("D") }
                }
            });

            Assert.AreEqual(10000, waited);
            Assert.AreEqual(2, output.publishTargets.Count);
        }
    }
}
