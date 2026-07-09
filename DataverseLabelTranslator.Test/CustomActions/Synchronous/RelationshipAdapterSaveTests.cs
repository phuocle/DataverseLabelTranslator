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
    public class RelationshipAdapterSaveTests
    {
        [TestInitialize]
        public void Initialize() { RelationshipAdapter.WaitAction = _ => { }; }

        [TestCleanup]
        public void Cleanup() { RelationshipAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static Guid _relId = Guid.NewGuid();

        private static IOrganizationService CreateService(Func<OrganizationRequest, OrganizationResponse> execute)
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call => execute((OrganizationRequest)call[0]));
            return service;
        }

        private static OrganizationResponse DispatchStandard(OrganizationRequest req, RelationshipMetadataBase rel)
        {
            if (req is RetrieveRelationshipRequest)
            {
                return AdapterTestHelpers.SetResultsAndReturn(new RetrieveRelationshipResponse(), "RelationshipMetadata", rel);
            }
            if (req is UpdateRelationshipRequest)
            {
                return new UpdateRelationshipResponse();
            }
            if (req is PublishXmlRequest)
            {
                return new PublishXmlResponse();
            }
            return new OrganizationResponse();
        }

        [TestMethod]
        public void Save_NoRows_ReturnsEmpty()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "account_contact",
                ReferencingEntity = "contact",
                ReferencedEntity = "account",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, Label = new Label("Old", 1033) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_EmptyChanges_Skipped()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "account_contact",
                ReferencingEntity = "contact",
                ReferencedEntity = "account",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, Label = new Label("Old", 1033) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|account_contact|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string>() }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WhitespaceGridKey_Throws()
        {
            var service = CreateService(r => DispatchStandard(r, null));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "  ", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "gridKey is required");
        }

        [TestMethod]
        public void Save_InvalidGridKeyType_Throws()
        {
            var service = CreateService(r => DispatchStandard(r, null));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "forms|x|y|z|w|v", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "relationships");
        }

        [TestMethod]
        public void Save_NotEnoughParts_Throws()
        {
            var service = CreateService(r => DispatchStandard(r, null));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|x|y", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "relationships");
        }

        [TestMethod]
        public void Save_InvalidMetadataId_Throws()
        {
            var service = CreateService(r => DispatchStandard(r, null));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|no-guid|1:N|x|y|z", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input), "Relationship MetadataId");
        }

        [TestMethod]
        public void Save_RelationshipMetadataNull_Skipped()
        {
            var service = CreateService(r =>
            {
                if (r is RetrieveRelationshipRequest) return new RetrieveRelationshipResponse(); // no relationship metadata
                return new OrganizationResponse();
            });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_MenuConfigNull_Skipped()
        {
            // Unknown property -> returns null menu config in GetAssociatedMenuConfiguration fallback
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = null
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_MenuConfigNotCustomizable_Skipped()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = false, Label = new Label("Old", 1033) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_ValidOneToMany_UpdatesAndRecordsEntityTarget()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "account_contact",
                ReferencingEntity = "contact",
                ReferencedEntity = "account",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old", 1033) }
            };
            var captured = new List<OrganizationRequest>();
            var service = CreateService(r => { captured.Add(r); return DispatchStandard(r, rel); });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|account_contact|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "New" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.kind == "entity" && t.id == "contact"));
            Assert.IsTrue(captured.OfType<UpdateRelationshipRequest>().Any());
        }

        [TestMethod]
        public void Save_SwitchFromUseCollectionName_PopulatesFromPlurals()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseCollectionName, IsCustomizable = true, Label = new Label() }
            };
            int pluralCalls = 0;
            var service = CreateService(r =>
            {
                if (r is RetrieveEntityRequest)
                {
                    pluralCalls++;
                    var em = new EntityMetadata { LogicalName = "contact" };
                    em.DisplayCollectionName = new Label(new LocalizedLabel("Contacts", 1033));
                    var resp = new RetrieveEntityResponse();
                    resp.Results["EntityMetadata"] = em;
                    return resp;
                }
                return DispatchStandard(r, rel);
            });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "Contacts-2" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(pluralCalls > 0);
        }

        [TestMethod]
        public void Save_BehaviorNull_PopulatesFromPlurals()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = null, IsCustomizable = true }
            };
            var service = CreateService(r =>
            {
                if (r is RetrieveEntityRequest)
                {
                    var em = new EntityMetadata { LogicalName = "contact" };
                    em.DisplayCollectionName = new Label(new LocalizedLabel("Contacts", 1033));
                    var resp = new RetrieveEntityResponse();
                    resp.Results["EntityMetadata"] = em;
                    return resp;
                }
                return DispatchStandard(r, rel);
            });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
        }

        [TestMethod]
        public void Save_ManyToMany_Entity1_UpdatesEntity1Config()
        {
            var rel = new ManyToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "account_contact_mm",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                Entity1AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old", 1033) },
                Entity2AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old2", 1033) }
            };
            var captured = new List<OrganizationRequest>();
            var service = CreateService(r => { captured.Add(r); return DispatchStandard(r, rel); });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|N:N|account_contact_mm|Entity1AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "New1" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "contact"));
            var upd = captured.OfType<UpdateRelationshipRequest>().First();
            Assert.IsTrue(upd.Relationship is ManyToManyRelationshipMetadata);
        }

        [TestMethod]
        public void Save_ManyToMany_Entity2_UpdatesEntity2Config()
        {
            var rel = new ManyToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "account_contact_mm",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                Entity1AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old", 1033) },
                Entity2AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old2", 1033) }
            };
            var captured = new List<OrganizationRequest>();
            var service = CreateService(r => { captured.Add(r); return DispatchStandard(r, rel); });
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "relationships|" + _relId.ToString("D") + "|N:N|account_contact_mm|Entity2AssociatedMenuConfiguration|account",
                        changes = new Dictionary<string, string> { { "1033", "New2" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsTrue(output.publishTargets.Any(t => t.id == "account"));
        }

        [TestMethod]
        public void Save_EmptyNavEntity_NoPublishTarget()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old", 1033) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|", changes = new Dictionary<string, string> { { "1033", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Save_Publication_MultipleLanguagesMerged()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label(new LocalizedLabel("OldEN", 1033), new LocalizedLabel("OldDE", 1031)) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "1033", "NewEN" }, { "1036", "Fr" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, output.changedRowCount);
        }

        [TestMethod]
        public void Save_NoSelectedLanguageChange_SkippedByGetValid()
        {
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = _relId,
                SchemaName = "sch",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration { Behavior = AssociatedMenuBehavior.UseLabel, IsCustomizable = true, Label = new Label("Old", 1033) }
            };
            var service = CreateService(r => DispatchStandard(r, rel));
            var adapter = new RelationshipAdapter();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput { gridKey = "relationships|" + _relId.ToString("D") + "|1:N|sch|AssociatedMenuConfiguration|contact", changes = new Dictionary<string, string> { { "abc", "X" } } }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }
    }
}
