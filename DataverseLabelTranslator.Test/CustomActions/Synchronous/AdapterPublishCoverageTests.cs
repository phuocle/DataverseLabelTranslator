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

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    /// <summary>
    /// Cross-adapter coverage for Publish/Published, empty-save and a few Load edge cases.
    /// These exercise the shared base-class helpers (GetPublishTargetIds, ToPublishOutput,
    /// AddPublishTarget, GetValidLabelChanges, ParseGridKey) through every adapter so that
    /// branch coverage of the common publishing path rises across all adapters.
    /// </summary>
    [TestClass]
    public class AdapterPublishCoverageTests
    {
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
                if (call[0] is PublishXmlRequest) return new OrganizationResponse();
                if (call[0] is PublishAllXmlRequest) return new OrganizationResponse();
                if (call[0] is RetrieveAvailableLanguagesRequest)
                {
                    return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033);
                }
                return new OrganizationResponse();
            });
            return service;
        }

        private static EasyTranslatorPublishInput EmptyPublish() => new EasyTranslatorPublishInput();

        private static EasyTranslatorPublishInput PublishTarget(string kind, string id)
        {
            return new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = kind, id = id }
                }
            };
        }

        private static EasyTranslatorPublishInput PublishTargets(string kind, params string[] ids)
        {
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>()
            };
            foreach (var id in ids)
            {
                input.publishTargets.Add(new EasyTranslatorPublishTarget { kind = kind, id = id });
            }
            return input;
        }

        private static void AssertEmptySave(EasyTranslatorSaveOutput output)
        {
            Assert.IsNotNull(output);
            Assert.AreEqual(0, output.changedRowCount);
        }

        private static void AssertPublishCount(EasyTranslatorSaveOutput output, int expected)
        {
            Assert.IsNotNull(output);
            Assert.AreEqual(expected, output.publishTargets.Count);
        }

        private static IOrganizationService CreateSaveService()
        {
            // Service that satisfies adapters which call GetBaseLanguage() up-front
            // (organization query) plus RetrieveEntityRequest for EntityMessageAdapter.
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest req)
                {
                    var em = new EntityMetadata { LogicalName = req.LogicalName ?? "account" };
                    typeof(EntityMetadata).GetProperty("ObjectTypeCode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(em, 1);
                    typeof(EntityMetadata).GetProperty("IsCustomEntity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(em, false);
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveEntityResponse(), "EntityMetadata", em);
                }
                return new OrganizationResponse();
            });
            return service;
        }

        // ---- Save with no changed rows returns empty for every adapter ----

        [TestMethod]
        public void Save_Empty_NoChanges_AllAdapters()
        {
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new OptionSetAdapter(),
                new FormAdapter(),
                new ViewAdapter(),
                new FormMetaAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new BusinessRuleAdapter(),
                new RibbonAdapter(),
                new CommandAdapter(),
                new EntityMessageAdapter(),
                new ContentSnippetAdapter(),
                new SitemapAdapter(),
                new DashboardAdapter(),
                new WebResourceAdapter(),
                new GlobalOptionSetAdapter()
            };
            // Use the full save service so adapters that fetch base language / entity
            // metadata up-front resolve successfully instead of throwing.
            var service = CreateSaveService();
            var saveInput = new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() };
            foreach (var adapter in adapters)
            {
                var output = adapter.Save(AdapterTestHelpers.Context(service), saveInput);
                AssertEmptySave(output);
            }
        }

        [TestMethod]
        public void Save_NullChangedRows_DoesNotThrow_AllAdapters()
        {
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new OptionSetAdapter(),
                new FormAdapter(),
                new ViewAdapter(),
                new FormMetaAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new BusinessRuleAdapter(),
                new RibbonAdapter(),
                new CommandAdapter(),
                new ContentSnippetAdapter(),
                new SitemapAdapter(),
                new DashboardAdapter(),
                new WebResourceAdapter(),
                new GlobalOptionSetAdapter()
            };
            var service = CreateSaveService();
            var saveInput = new EasyTranslatorSaveInput { changedRows = null };
            foreach (var adapter in adapters)
            {
                var output = adapter.Save(AdapterTestHelpers.Context(service), saveInput);
                AssertEmptySave(output);
            }
        }

        // ---- Publish with no targets returns empty and does not execute (entity-kind adapters) ----

        [TestMethod]
        public void Publish_NoTargets_EntityKind_DoesNotExecute()
        {
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new OptionSetAdapter(),
                new FormAdapter(),
                new ViewAdapter(),
                new FormMetaAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new BusinessRuleAdapter(),
                new CommandAdapter(),
                new EntityMessageAdapter(),
                new EntityMetadataAdapter()
            };
            foreach (var adapter in adapters)
            {
                var service = CreateService();
                var output = adapter.Publish(AdapterTestHelpers.Context(service), EmptyPublish());
                AssertPublishCount(output, 0);
                service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
            }
        }

        [TestMethod]
        public void Publish_TargetMatches_ExecutesPublishXml_EntityKind()
        {
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new ViewAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new CommandAdapter(),
                new EntityMessageAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter()
            };
            foreach (var adapter in adapters)
            {
                var service = CreateService();
                var output = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("entity", "account"));
                AssertPublishCount(output, 1);
                service.Received().Execute(Arg.Is<PublishXmlRequest>(r => r != null));
            }
        }

        [TestMethod]
        public void Publish_IgnoresNonMatchingKind()
        {
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new FormAdapter(),
                new ViewAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new BusinessRuleAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter()
            };
            foreach (var adapter in adapters)
            {
                var service = CreateService();
                var output = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("other", "account"));
                AssertPublishCount(output, 0);
                service.DidNotReceive().Execute(Arg.Any<OrganizationRequest>());
            }
        }

        [TestMethod]
        public void Publish_Dashboard_ExecutesOnlyWhenGuidTarget()
        {
            var service = CreateService();
            var adapter = new DashboardAdapter();

            var noTargets = adapter.Publish(AdapterTestHelpers.Context(service), EmptyPublish());
            AssertPublishCount(noTargets, 0);

            var invalidGuid = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("dashboard", "not-a-guid"));
            AssertPublishCount(invalidGuid, 0);

            var valid = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("dashboard", Guid.NewGuid().ToString("D")));
            AssertPublishCount(valid, 1);
        }

        [TestMethod]
        public void Publish_WebResource_ExecutesOnlyWhenGuidTarget()
        {
            var service = CreateService();
            var adapter = new WebResourceAdapter();

            var noTargets = adapter.Publish(AdapterTestHelpers.Context(service), EmptyPublish());
            AssertPublishCount(noTargets, 0);

            var invalidGuid = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("webresource", "abc"));
            AssertPublishCount(invalidGuid, 0);

            var valid = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("webresource", Guid.NewGuid().ToString("D")));
            AssertPublishCount(valid, 1);
        }

        [TestMethod]
        public void Publish_GlobalOptionSet_ExecutesWhenGuidTarget()
        {
            var service = CreateService();
            var adapter = new GlobalOptionSetAdapter();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), PublishTarget("globalOptionSet", Guid.NewGuid().ToString("D")));
            AssertPublishCount(output, 1);
        }

        [TestMethod]
        public void Publish_Ribbon_ExecutesPublishAll_WhenTargets()
        {
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is PublishAllXmlAsyncRequest)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(
                        new PublishAllXmlAsyncResponse(), "AsyncOperationId", Guid.NewGuid());
                }
                return new OrganizationResponse();
            });
            var adapter = new RibbonAdapter();
            var empty = adapter.Publish(AdapterTestHelpers.Context(service), EmptyPublish());
            // Ribbon Publish always returns a single async publish operation regardless of target count.
            Assert.IsTrue(empty.changed);

            var withTargets = adapter.Publish(AdapterTestHelpers.Context(service), PublishTargets("publishAllXml", "ribbon1", "ribbon2"));
            Assert.IsTrue(withTargets.changed);
        }

        // ---- Published (post-publish wait) returns the matching targets ----

        [TestMethod]
        public void Published_ReturnsMatchingTargets_EntityKind()
        {
            var id = Guid.NewGuid().ToString("D");
            var adapters = new IEasyTranslatorTypeAdapter[]
            {
                new AttributeAdapter(),
                new OptionSetAdapter(),
                new ViewAdapter(),
                new ChartAdapter(),
                new BpfAdapter(),
                new CommandAdapter(),
                new EntityMetadataAdapter(),
                new RelationshipAdapter(),
                new EntityMessageAdapter()
            };
            foreach (var adapter in adapters)
            {
                var service = CreateService();
                var output = adapter.Published(AdapterTestHelpers.Context(service), PublishTarget("entity", id));
                AssertPublishCount(output, 1);
            }
        }

        [TestMethod]
        public void Published_Dashboard_ReturnsMatchingGuidTargets()
        {
            var service = CreateService();
            var id = Guid.NewGuid().ToString("D");
            var adapter = new DashboardAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(service), PublishTarget("dashboard", id));
            AssertPublishCount(output, 1);
        }

        [TestMethod]
        public void Published_WebResource_ReturnsMatchingGuidTargets()
        {
            var service = CreateService();
            var id = Guid.NewGuid().ToString("D");
            var adapter = new WebResourceAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(service), PublishTarget("webresource", id));
            AssertPublishCount(output, 1);
        }

        [TestMethod]
        public void Published_GlobalOptionSet_ReturnsMatchingGuidTargets()
        {
            var service = CreateService();
            var id = Guid.NewGuid().ToString("D");
            var adapter = new GlobalOptionSetAdapter();
            var output = adapter.Published(AdapterTestHelpers.Context(service), PublishTarget("globalOptionSet", id));
            AssertPublishCount(output, 1);
        }

        // ---- Load edge cases that exercise exception/empty branches ----

        [TestMethod]
        public void Load_EntityMessage_ThrowsWhenEntityNameMissing()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service),
                    new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" }),
                "select an entity");
        }

        [TestMethod]
        public void Load_EntityMessage_ThrowsWhenEntityNameNone()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateService();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service),
                    new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" }),
                "select an entity");
        }

        [TestMethod]
        public void Save_EntityMessage_ThrowsWhenNoSolutionSelected()
        {
            var adapter = new EntityMessageAdapter();
            var service = CreateSaveService();
            var row = new EasyTranslatorChangedRowInput
            {
                gridKey = "entityMessages|key",
                changes = new Dictionary<string, string> { { "1033", "Account" } }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service),
                    new EasyTranslatorSaveInput { entityName = "account", changedRows = new List<EasyTranslatorChangedRowInput> { row } }),
                "solution");
        }

        [TestMethod]
        public void Save_EntityMetadata_SkipsInvalidChanges()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var row = new EasyTranslatorChangedRowInput
            {
                gridKey = "entityMetadata|account",
                changes = new Dictionary<string, string> { { "not-a-number", "x" } }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service),
                new EasyTranslatorSaveInput { entityName = "account", changedRows = new List<EasyTranslatorChangedRowInput> { row } });
            Assert.AreEqual(0, output.changedRowCount);
        }

        // ---- Save with invalid gridKey throws on adapters that parse gridKey ----

        [TestMethod]
        public void Save_BusinessRule_ThrowsOnInvalidGridKey()
        {
            var adapter = new BusinessRuleAdapter();
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("workflow", Arg.Any<Guid>(), Arg.Any<ColumnSet>())
                .Returns(new Entity("workflow", Guid.NewGuid()) { ["xaml"] = "<x/>" });
            var row = new EasyTranslatorChangedRowInput
            {
                gridKey = "other|guid|label",
                changes = new Dictionary<string, string> { { "1033", "x" } }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service),
                    new EasyTranslatorSaveInput { entityName = "account", changedRows = new List<EasyTranslatorChangedRowInput> { row } }),
                "businessRules");
        }

        [TestMethod]
        public void Save_Relationship_ThrowsOnInvalidGridKey()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var row = new EasyTranslatorChangedRowInput
            {
                gridKey = "other|guid|type",
                changes = new Dictionary<string, string> { { "1033", "x" } }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service),
                    new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput> { row } }),
                "relationships");
        }
    }
}
