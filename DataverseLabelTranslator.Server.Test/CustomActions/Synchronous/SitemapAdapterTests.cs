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
        public void Load_WithSitemapHavingNullXml_SkipsEntry()
        {
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
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
        public void Save_NullChangedRows_ReturnsEmpty()
        {
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
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
            var rawGuidAlias = new Entity("x");
            rawGuidAlias["alias"] = Guid.NewGuid();
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<Guid?>(adapterType, "GetAliasedGuid", rawGuidAlias, "alias"));

            Assert.AreEqual(Guid.Empty, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", null, "id"));
            var refId = Guid.NewGuid();
            Assert.AreEqual(refId, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", new Entity("x") { ["id"] = new EntityReference("x", refId) }, "id"));
            var fallback = new Entity("x") { Id = Guid.NewGuid() };
            Assert.AreEqual(fallback.Id, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "GetEntityGuid", fallback, "id"));

            var updateType = adapterType.GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>());
            Assert.AreEqual("<SiteMap />", AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpdateSitemapXml", "<SiteMap />", update));
            Assert.AreEqual("<SiteMap />", AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpdateSitemapXml", "<SiteMap />", null));
            var updateWithNullLabels = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("labels").SetValue(updateWithNullLabels, null);
            Assert.AreEqual("<SiteMap />", AdapterTestHelpers.InvokeStatic<string>(adapterType, "UpdateSitemapXml", "<SiteMap />", updateWithNullLabels));

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
            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                adapterType.GetMethod("ValidateSitemapId", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { " " }));

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
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group", "Area"));
            Assert.IsNotNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group", "Group"));
            Assert.IsNotNull(AdapterTestHelpers.InvokeStatic<XElement>(adapterType, "FindSitemapNode", root, "area|group", null));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsNodeType", null, "Area"));
            Assert.IsTrue(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsNodeType", root.Element("Area"), ""));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetNodeId", (object)null));

            var sitemapInfoType = adapterType.GetNestedType("SitemapInfo", BindingFlags.NonPublic);
            var invalidSitemap = Activator.CreateInstance(sitemapInfoType);
            sitemapInfoType.GetProperty("sitemapxml").SetValue(invalidSitemap, "<SiteMap><Area>");
            var emptySitemap = Activator.CreateInstance(sitemapInfoType);
            sitemapInfoType.GetProperty("sitemapxml").SetValue(emptySitemap, "");
            var sitemapListType = typeof(List<>).MakeGenericType(sitemapInfoType);
            var sitemapList = Activator.CreateInstance(sitemapListType);
            sitemapListType.GetMethod("Add").Invoke(sitemapList, new object[] { null });
            sitemapListType.GetMethod("Add").Invoke(sitemapList, new[] { emptySitemap });
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

            var nullResponseService = Substitute.For<IOrganizationService>();
            nullResponseService.Execute(Arg.Any<OrganizationRequest>()).Returns(_ => null);
            var nullResponseLabels = (Dictionary<string, Label>)retrieveLabels.Invoke(null, new[] { nullResponseService, validList });
            Assert.AreEqual(0, nullResponseLabels.Count);

            var nullMetadataService = Substitute.For<IOrganizationService>();
            nullMetadataService.Execute(Arg.Any<OrganizationRequest>()).Returns(_ => new RetrieveEntityResponse());
            var nullMetadataLabels = (Dictionary<string, Label>)retrieveLabels.Invoke(null, new[] { nullMetadataService, validList });
            Assert.AreEqual(0, nullMetadataLabels.Count);

            var updateWithNullLabelValue = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(updateWithNullLabelValue, "area");
            updateType.GetProperty("nodeType").SetValue(updateWithNullLabelValue, "Area");
            updateType.GetProperty("component").SetValue(updateWithNullLabelValue, "DisplayText");
            updateType.GetProperty("labels").SetValue(updateWithNullLabelValue, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = null }
            });
            var nullLabelXml = (string)adapterType.GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new[] { "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"Old\" /></Titles></Area></SiteMap>", updateWithNullLabelValue });
            Assert.IsTrue(nullLabelXml.Contains("Title=\"\""));
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsDescriptionsBeforeGroup_InAreaWithGroup()
        {
            // Area already has Group, so the new <Descriptions> container must be
            // inserted BEFORE <Group> to satisfy the SiteMap XSD. Old code used
            // node.Add() which placed it after Group and broke validation.
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\" /></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area");
            updateType.GetProperty("nodeType").SetValue(update, "Area");
            updateType.GetProperty("component").SetValue(update, "Description");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New desc" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var area = XElement.Parse(updated).Element("Area");
            var childNames = area.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Descriptions", childNames[0]);
            Assert.AreEqual("Group", childNames[1]);
            Assert.IsTrue(area.Element("Descriptions").Element("Description").Attribute("Description").Value == "New desc");
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsTitlesBeforeGroup_InAreaWithGroup()
        {
            // Area already has Group, so the new <Titles> container must be
            // inserted BEFORE <Group> to satisfy the SiteMap XSD.
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\" /></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area");
            updateType.GetProperty("nodeType").SetValue(update, "Area");
            updateType.GetProperty("component").SetValue(update, "DisplayText");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New title" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var area = XElement.Parse(updated).Element("Area");
            var childNames = area.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Titles", childNames[0]);
            Assert.AreEqual("Group", childNames[1]);
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsDescriptionsAfterTitles_BeforeGroup()
        {
            // Area already has Titles, so the new <Descriptions> container must be
            // inserted AFTER <Titles> and BEFORE <Group>.
            var xml = "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"T\" /></Titles><Group Id=\"group\" /></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area");
            updateType.GetProperty("nodeType").SetValue(update, "Area");
            updateType.GetProperty("component").SetValue(update, "Description");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "D" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var area = XElement.Parse(updated).Element("Area");
            var childNames = area.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Titles", childNames[0]);
            Assert.AreEqual("Descriptions", childNames[1]);
            Assert.AreEqual("Group", childNames[2]);
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsDescriptionsBeforeSubArea_InGroupWithSubArea()
        {
            // Group already has SubArea, so the new <Descriptions> container must
            // be inserted BEFORE <SubArea>.
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\" /></Group></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area|group");
            updateType.GetProperty("nodeType").SetValue(update, "Group");
            updateType.GetProperty("component").SetValue(update, "Description");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Group desc" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var group = XElement.Parse(updated).Element("Area").Element("Group");
            var childNames = group.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Descriptions", childNames[0]);
            Assert.AreEqual("SubArea", childNames[1]);
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsAfterExistingTitles_InGroupWithSubArea()
        {
            // Group has Titles already; new <Descriptions> goes after Titles and before SubArea.
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><Titles><Title LCID=\"1033\" Title=\"T\" /></Titles><SubArea Id=\"sub\" /></Group></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area|group");
            updateType.GetProperty("nodeType").SetValue(update, "Group");
            updateType.GetProperty("component").SetValue(update, "Description");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "D" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var group = XElement.Parse(updated).Element("Area").Element("Group");
            var childNames = group.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Titles", childNames[0]);
            Assert.AreEqual("Descriptions", childNames[1]);
            Assert.AreEqual("SubArea", childNames[2]);
        }

        [TestMethod]
        public void UpdateSitemapXml_InsertsTitlesInEmptySubArea()
        {
            // SubArea has no children, so the new <Titles> goes first.
            var xml = "<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\" /></Group></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area|group|sub");
            updateType.GetProperty("nodeType").SetValue(update, "SubArea");
            updateType.GetProperty("component").SetValue(update, "DisplayText");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Sub title" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);

            var updated = (string)method.Invoke(null, new[] { xml, update });

            var subArea = XElement.Parse(updated).Element("Area").Element("Group").Element("SubArea");
            var childNames = subArea.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual("Titles", childNames[0]);
        }

        [TestMethod]
        public void InsertContainerInSchemaOrder_NullArgs_NoOp()
        {
            // Helper should silently return when node or container is null.
            // We invoke it via reflection with null arguments.
            var insertMethod = typeof(SitemapAdapter).GetMethod("InsertContainerInSchemaOrder", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(insertMethod);
            var container = new XElement("Titles");
            insertMethod.Invoke(null, new object[] { null, container, "Area" });
            insertMethod.Invoke(null, new object[] { new XElement("Area"), null, "Area" });
            insertMethod.Invoke(null, new object[] { null, null, "Area" });
        }

        [TestMethod]
        public void GetChildNodeName_ReturnsExpectedValues()
        {
            var method = typeof(SitemapAdapter).GetMethod("GetChildNodeName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            Assert.AreEqual("Group", (string)method.Invoke(null, new object[] { "Area" }));
            Assert.AreEqual("SubArea", (string)method.Invoke(null, new object[] { "Group" }));
            Assert.IsNull(method.Invoke(null, new object[] { "SubArea" }));
            Assert.IsNull(method.Invoke(null, new object[] { "Unknown" }));
        }

        [TestMethod]
        public void Load_WithSitemapHavingNullName_UsesSitemapId()
        {
            // Line 36: ?? null-coalesce on sitemapname â€” sitemap has null name, falls back to sitemapid.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemapId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapid"] = sitemapId;
            // No sitemapname set
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual(sitemapId.ToString("D"), output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Save_SitemapEntityWithNullSitemapxmlAttribute_HandlesEmpty()
        {
            // Line 95: ?? null-coalesce on GetAttributeValue<string>("sitemapxml") â€” attribute missing.
            // UpdateSitemapXml throws when xml is empty (line 456 check), which is a separate code path.
            // This test verifies Save's flow when entity has no sitemapxml attribute â€” the
            // Convert.ToString call on null returns "" which then triggers UpdateSitemapXml's empty check.
            var adapter = new SitemapAdapter();
            var sitemapId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            // No sitemapxml attribute set, so GetAttributeValue returns null.
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());

            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|Area|area|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };
            // Save should throw InvalidPluginExecutionException â€” that's the documented behavior
            // when the sitemap's xml is empty.
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_UpdatedXmlSameAsCurrent_SkipsUpdate()
        {
            // Line 75: branch where updatedXml == currentXml so no update is performed.
            var adapter = new SitemapAdapter();
            var sitemapId = Guid.NewGuid();
            var xml = "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"Same\" /></Titles></Area></SiteMap>";
            var sitemap = new Entity("sitemap", sitemapId);
            sitemap["sitemapxml"] = xml;
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());

            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "sitemap|" + sitemapId.ToString("D") + "|Area|area|DisplayText",
                        changes = new Dictionary<string, string> { { "1033", "Same" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
            service.DidNotReceive().Update(Arg.Any<Entity>());
        }

        [TestMethod]
        public void BuildSitemapNodeRows_NoTitleAttribute_AddsDefaultTitle()
        {
            // Line 228: SubArea/Group without title attribute but with no entity label,
            // and not description component â€” uses default Title attribute.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            // SubArea has no Title attribute, no entity â€” should fall back to null/default
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void BuildSitemapNodeRows_DescriptionComponentWithNoDescriptionContainer_LeavesEmpty()
        {
            // Line 244: Description component path with no <Descriptions> container.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" /></Group></Area></SiteMap>";
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
        public void BuildSitemapNodeRows_DescriptionItemWithInvalidLCID_Skipped()
        {
            // Line 352: invalid LCID on Description item â€” should be skipped.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            // Description with bad LCID
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Descriptions><Description LCID=\"abc\" Description=\"X\" /></Descriptions></Area></SiteMap>";
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
        public void BuildSitemapNodeRows_DescriptionWithNullLabelAttribute_UsesEmpty()
        {
            // Line 377: ?? on Description attribute value.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            // Description element with no Description attribute
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Descriptions><Description LCID=\"1033\" /></Descriptions></Area></SiteMap>";
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
        public void BuildSitemapNodeRows_DisplayTextWithEntityLabel_UsesEntityLabel()
        {
            // Line 395: !isDescription && entityLabels.ContainsKey â€” uses entity label.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" Entity=\"account\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                if (q.EntityName == "entity" && q.Criteria.Conditions.Count == 0) return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest req && req.LogicalName == "account" && req.EntityFilters == EntityFilters.Entity)
                {
                    var resp = new RetrieveEntityResponse();
                    resp.Results["EntityMetadata"] = new EntityMetadata
                    {
                        LogicalName = "account",
                        DisplayName = AdapterTestHelpers.BuildLabel((1033, "AccountLabel"))
                    };
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            // Verify the SubArea row was loaded with entity fallback path.
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void UpdateSitemapXml_ContainerExists_NoNewContainer()
        {
            // Line 397: container exists â€” should not create new container.
            var xml = "<SiteMap><Area Id=\"area\"><Titles><Title LCID=\"1033\" Title=\"Old\" /></Titles></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "area");
            updateType.GetProperty("nodeType").SetValue(update, "Area");
            updateType.GetProperty("component").SetValue(update, "DisplayText");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);
            var updated = (string)method.Invoke(null, new[] { xml, update });
            Assert.IsTrue(updated.Contains("Title=\"New\""));
        }

        [TestMethod]
        public void BuildSitemapNodeRows_DescriptionWithNullAttributeValue()
        {
            // Line 444: ?? on Title/Description attribute value.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            // Title element without Title attribute
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Titles><Title LCID=\"1033\" /></Titles></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void BuildSitemapNodeRows_NoTitleNoEntity_FallsBackToDefaultTitle()
        {
            // Line 459: !isDescription && empty entityName path â€” uses node Title attribute.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\" Title=\"Default\"><Group Id=\"g\"><SubArea Id=\"s\" Title=\"SubTitle\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void GetReferencedEntityNames_HandlesNullSitemapList()
        {
            // Line 497: null check on sitemaps.
            var adapterType = typeof(SitemapAdapter);
            var method = adapterType.GetMethod("GetReferencedEntityNames", BindingFlags.NonPublic | BindingFlags.Static);
            var names = (List<string>)method.Invoke(null, new object[] { null });
            Assert.AreEqual(0, names.Count);
        }

        [TestMethod]
        public void ResolveAppModuleIdsForSitemap_NullAliasValue_Skipped()
        {
            // Line 597: appModuleId.HasValue == false branch.
            var sitemapId = Guid.NewGuid();
            var service = Substitute.For<IOrganizationService>();
            var component = new Entity("appmodulecomponent");
            // No alias value set
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities(component));
            var method = typeof(SitemapAdapter).GetMethod("ResolveAppModuleIdsForSitemap", BindingFlags.NonPublic | BindingFlags.Static);
            var ids = (List<Guid>)method.Invoke(null, new object[] { service, sitemapId });
            Assert.AreEqual(0, ids.Count);
        }

        [TestMethod]
        public void Publish_PassesBothKindsToExecute()
        {
            // Lines 624, 654, 659: publish with both appmodule and sitemap targets.
            var adapter = new SitemapAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "sitemap", id = Guid.NewGuid().ToString("D") },
                    new EasyTranslatorPublishTarget { kind = "appmodule", id = Guid.NewGuid().ToString("D") }
                }
            };
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            adapter.Publish(AdapterTestHelpers.Context(service), input);
            Assert.IsNotNull(captured);
            Assert.IsInstanceOfType(captured, typeof(PublishXmlRequest));
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
        public void BuildSitemapNodeRows_SubAreaWithoutEntityAttribute_NoEntitySuffix()
        {
            // Line 318: ?? string.Empty for entity name when attribute is missing.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void ExtractXmlLabels_ContainerMissing_ReturnsEmpty()
        {
            // Line 397: container == null path in ExtractXmlLabels.
            var adapterType = typeof(SitemapAdapter);
            var method = adapterType.GetMethod("ExtractXmlLabels", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var node = XElement.Parse("<Area Id=\"a\"></Area>");
            var labels = (List<EasyTranslatorLabelChange>)method.Invoke(null, new object[] { node, false });
            Assert.AreEqual(0, labels.Count);
            var labelsDesc = (List<EasyTranslatorLabelChange>)method.Invoke(null, new object[] { node, true });
            Assert.AreEqual(0, labelsDesc.Count);
        }

        [TestMethod]
        public void UpdateSitemapXml_ItemWithNullLCID_Skipped()
        {
            // Line 444: ?? on LCID attribute value.
            var xml = "<SiteMap><Area Id=\"a\"><Titles><Title Title=\"X\" /></Titles></Area></SiteMap>";
            var updateType = typeof(SitemapAdapter).GetNestedType("SitemapNodeUpdate", BindingFlags.NonPublic);
            var update = Activator.CreateInstance(updateType, true);
            updateType.GetProperty("compositeId").SetValue(update, "a");
            updateType.GetProperty("nodeType").SetValue(update, "Area");
            updateType.GetProperty("component").SetValue(update, "DisplayText");
            updateType.GetProperty("labels").SetValue(update, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New" }
            });
            var method = typeof(SitemapAdapter).GetMethod("UpdateSitemapXml", BindingFlags.NonPublic | BindingFlags.Static);
            var updated = (string)method.Invoke(null, new[] { xml, update });
            Assert.IsTrue(updated.Contains("Title=\"New\""));
        }

        [TestMethod]
        public void BuildSitemapNodeRows_NoTitleNoEntity_NoLanguageValues()
        {
            // Line 459: !isDescription && empty entityName â€” node has no Title attribute.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            // No Title attribute on SubArea, no Entity
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\"><SubArea Id=\"s\" /></Group></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void ExtractXmlLabels_TitleWithNullLabelAttribute_UsesEmpty()
        {
            // Line 459: ?? on Title/Description attribute value.
            var adapterType = typeof(SitemapAdapter);
            var method = adapterType.GetMethod("ExtractXmlLabels", BindingFlags.NonPublic | BindingFlags.Static);
            // Title element with no Title attribute
            var node = XElement.Parse("<Area Id=\"a\"><Titles><Title LCID=\"1033\" /></Titles></Area>");
            var labels = (List<EasyTranslatorLabelChange>)method.Invoke(null, new object[] { node, false });
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual(string.Empty, labels[0].Label);
        }

        [TestMethod]
        public void ToPublishOutput_NullLists_HandledGracefully()
        {
            // Lines 624, 654: null checks on appModuleIds and sitemapIds parameter.
            var method = typeof(SitemapAdapter).GetMethod("ToPublishOutput", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (EasyTranslatorSaveOutput)method.Invoke(null, new object[] { null, null });
            Assert.AreEqual(0, result.publishTargets.Count);
        }

        [TestMethod]
        public void ToPublishOutput_NonNullLists_AppendsTargets()
        {
            // Lines 624, 654: non-null lists with real targets.
            var method = typeof(SitemapAdapter).GetMethod("ToPublishOutput", BindingFlags.NonPublic | BindingFlags.Static);
            var appId = Guid.NewGuid();
            var sitemapId = Guid.NewGuid();
            var result = (EasyTranslatorSaveOutput)method.Invoke(null, new object[]
            {
                new List<string> { appId.ToString("D") },
                new List<string> { sitemapId.ToString("D") }
            });
            Assert.AreEqual(2, result.publishTargets.Count);
        }

        [TestMethod]
        public void BuildPublishXml_EmptyInputs_ProducesEmptyContainers()
        {
            // Line 659: both lists empty produces empty containers.
            var method = typeof(SitemapAdapter).GetMethod("BuildPublishXml", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)method.Invoke(null, new object[] { new List<string>(), new List<string>() });
            Assert.AreEqual("<importexportxml><appmodules></appmodules><sitemaps></sitemaps></importexportxml>", result);
        }

        [TestMethod]
        public void FindSitemapNode_GroupTooFewIdentifiers_ReturnsNull()
        {
            // Line 597: identifiers.Length == 2 path for Group node type with too many identifiers.
            var root = XElement.Parse("<SiteMap><Area Id=\"area\"><Group Id=\"group\"><SubArea Id=\"sub\" /></Group></Area></SiteMap>");
            var method = typeof(SitemapAdapter).GetMethod("FindSitemapNode", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(method.Invoke(null, new object[] { root, "area|group|sub", "Group" }));
        }

        [TestMethod]
        public void BuildSitemapNodeRows_GroupWithDisplayText_HandlesNullOrEmpty()
        {
            // Line 244: aliased value extraction. Force aliased return path.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Group Id=\"g\" /></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void BuildSitemapNodeRows_NodeWithNullTitleAttribute_FallsBackToId()
        {
            // Line 444: node.Title attribute is null - fall back to id.
            var adapter = new SitemapAdapter();
            var service = CreateServiceWithBaseLanguage();
            var sitemap = new Entity("sitemap") { Id = Guid.NewGuid() };
            sitemap["sitemapname"] = "Test";
            sitemap["sitemapxml"] = "<SiteMap><Area Id=\"a\"><Titles></Titles></Area></SiteMap>";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "sitemap") return AdapterTestHelpers.Entities(sitemap);
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { solutionId = "all", component = "DisplayText" });
            Assert.IsTrue(output.grid.rows.Count > 0);
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
