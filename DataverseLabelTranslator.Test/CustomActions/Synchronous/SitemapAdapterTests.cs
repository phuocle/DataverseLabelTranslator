using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
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
    }
}
