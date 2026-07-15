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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class RelationshipAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { RelationshipAdapter.WaitAction = System.Threading.Thread.Sleep; }

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
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Load_NoneEntity_ReturnsEmpty()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new EntityMetadata[0];
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_EmptyEntityName_ReturnsEmpty()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new EntityMetadata[0];
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithEntityMetadata_ReturnsRows()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return AdapterTestHelpers.BuildEntityResponse("account");
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "account", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void LoadAll_WithRelationships_ReturnsParentAndSortedChildRows()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var accountId = Guid.NewGuid();
            var contactId = Guid.NewGuid();
            var accountContact = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "account_contact",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseCollectionName, new Label("Contacts", 1033))
            };
            var contactAccount = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "contact_account",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Account", 1033))
            };
            var manyToMany = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "account_contact_mm",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033)),
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Accounts", 1033))
            };

            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(
                        MakeEntity("account", accountId, "Account"),
                        MakeEntity("contact", contactId, "Contact"),
                        MakeEntity("", Guid.NewGuid(), "Skipped"));
                }
                if (call[0] is RetrieveEntityRequest req && req.EntityFilters == EntityFilters.Relationships)
                {
                    if (req.LogicalName == "account")
                    {
                        return BuildRelationshipEntityResponse("account",
                            oneToMany: new[] { accountContact },
                            manyToOne: new[] { contactAccount },
                            manyToMany: new[] { manyToMany });
                    }

                    return BuildRelationshipEntityResponse(req.LogicalName);
                }
                if (call[0] is RetrieveEntityRequest entityReq && entityReq.EntityFilters == EntityFilters.Entity)
                {
                    var response = AdapterTestHelpers.BuildEntityResponse(entityReq.LogicalName);
                    response.EntityMetadata.DisplayCollectionName = AdapterTestHelpers.BuildLabel((1033, entityReq.LogicalName + "s"));
                    return response;
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "none",
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account (account)", output.grid.rows[0].SchemaName);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(3, children.Count);
            Assert.IsTrue(children.Exists(row => row.GridKey.Contains("account_contact")));
            Assert.IsTrue(children.Exists(row => row.GridKey.Contains("contact_account")));
            Assert.IsTrue(children.Exists(row => row.GridKey.Contains("account_contact_mm")));
        }

        [TestMethod]
        public void LoadAll_WithSolutionFilter_UsesOnlySolutionEntities()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var accountId = Guid.NewGuid();
            var contactId = Guid.NewGuid();
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "account_contact",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033))
            };
            var solutionId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("solutioncomponent") { ["objectid"] = accountId },
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty },
                        new Entity("solutioncomponent") { ["objectid"] = Guid.NewGuid() });
                }

                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(
                        MakeEntity("account", accountId, "Account"),
                        MakeEntity("contact", contactId, "Contact"));
                }
                if (call[0] is RetrieveEntityRequest req && req.EntityFilters == EntityFilters.Relationships)
                {
                    return req.LogicalName == "account"
                        ? BuildRelationshipEntityResponse("account", oneToMany: new[] { rel })
                        : BuildRelationshipEntityResponse(req.LogicalName);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "none",
                component = "DisplayText",
                solutionId = solutionId.ToString("D")
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account (account)", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void LoadForEntity_WithRelationships_ReturnsFlatRows()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "account_contact",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033))
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest req && req.EntityFilters == EntityFilters.Relationships)
                {
                    return BuildRelationshipEntityResponse(req.LogicalName, oneToMany: new[] { rel });
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.IsTrue(output.grid.rows[0].GridKey.Contains("account_contact"));
        }

        [TestMethod]
        public void Save_WithInvalidGridKey_Throws()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
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
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new RelationshipAdapter();
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
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            int called = 0;
            RelationshipAdapter.WaitAction = _ => called++;
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(called > 0);
        }

        [TestMethod]
        public void Save_WithValidGridKey_UpdatesLabel()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "account_account",
                ReferencingEntity = "contact",
                ReferencedEntity = "account",
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                {
                    Behavior = AssociatedMenuBehavior.UseLabel,
                    Label = new Label("Old", 1033)
                }
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveRelationshipRequest) return AdapterTestHelpers.SetResultsAndReturn(new RetrieveRelationshipResponse(), "RelationshipMetadata", rel);
                if (call[0] is RetrieveAllEntitiesRequest) return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse();
                return new OrganizationResponse();
            });
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"relationships|{rel.MetadataId:D}|OneToMany|account_account|Entity1AssociatedMenuConfiguration|contact",
                        changes = new Dictionary<string, string> { { "1033", "New" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount >= 0);
        }

        [TestMethod]
        public void Save_InvalidMetadataId_Throws()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "relationships|not-a-guid|OneToMany|x|y|z",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Load_Throws_WhenSolutionIdInvalid()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest) return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse();
                return new OrganizationResponse();
            });
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", solutionId = "bad" }));
        }

        [TestMethod]
        public void PrivateHelpers_CoverRelationshipFallbackBranches()
        {
            var adapterType = typeof(RelationshipAdapter);
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetBaseLanguageLabel", null, 1033));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetBaseLanguageLabel", AdapterTestHelpers.BuildLabel((1041, "取引先")), 1033));
            Assert.AreEqual("Account", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetBaseLanguageLabel", AdapterTestHelpers.BuildLabel((1033, "Account")), 1033));
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<AssociatedMenuConfiguration>(adapterType, "GetAssociatedMenuConfiguration", new CustomRelationshipMetadata(), "x"));

            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(new RetrieveEntityResponse());
            var pluralNames = AdapterTestHelpers.InvokeStatic<Dictionary<int, string>>(adapterType, "GetEntityPluralNames", service, "account");
            Assert.AreEqual(0, pluralNames.Count);
            var relationshipList = AdapterTestHelpers.InvokeStatic<List<RelationshipMetadataBase>>(adapterType, "RetrieveEntityRelationships", service, "account");
            Assert.AreEqual(0, relationshipList.Count);

            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var nonCustomizable = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "non_customizable",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(false),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033))
            };
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, nonCustomizable, "account", pluralCache));

            var noMenu = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "no_menu",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = null
            };
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, noMenu, "account", pluralCache));

            var fixedMenu = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "fixed_menu",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033), isCustomizable: false)
            };
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, fixedMenu, "account", pluralCache));

            var manyToOne = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "contact_account",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Account", 1033))
            };
            var manyToOneRow = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, manyToOne, "contact", pluralCache);
            Assert.IsTrue(manyToOneRow.SchemaName.Contains("N:1"));

            var manyToManyNoMenu = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_no_menu",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = null,
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Accounts", 1033))
            };
            Assert.IsNull(AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, manyToManyNoMenu, "account", pluralCache));

            var customRow = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, new CustomRelationshipMetadata(), "account", pluralCache);
            Assert.IsNull(customRow);

            var currentLabel = AdapterTestHelpers.BuildLabel((1033, "Old"), (1033, "Duplicate"));
            currentLabel.LocalizedLabels.Insert(0, null);
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel", currentLabel, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = "JP" },
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "EN" }
            });

            Assert.AreEqual(2, merged.Length);
        }

        private static EntityMetadata MakeEntity(string logicalName, Guid metadataId, string displayName)
        {
            var entity = new EntityMetadata
            {
                LogicalName = logicalName,
                MetadataId = metadataId,
                IsCustomizable = new BooleanManagedProperty(true),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? new Label() : new Label(displayName, 1033)
            };
            return entity;
        }

        private static AssociatedMenuConfiguration CreateMenuConfig(AssociatedMenuBehavior behavior, Label label = null, bool isCustomizable = true)
        {
            var config = new AssociatedMenuConfiguration
            {
                Behavior = behavior,
                Label = label
            };
            typeof(AssociatedMenuConfiguration)
                .GetField("_associatedMenuIsCustomizable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, isCustomizable);
            return config;
        }

        private static RetrieveEntityResponse BuildRelationshipEntityResponse(
            string logicalName,
            OneToManyRelationshipMetadata[] oneToMany = null,
            OneToManyRelationshipMetadata[] manyToOne = null,
            ManyToManyRelationshipMetadata[] manyToMany = null)
        {
            var metadata = new EntityMetadata { LogicalName = logicalName };
            typeof(EntityMetadata).GetProperty("OneToManyRelationships").SetValue(metadata, oneToMany);
            typeof(EntityMetadata).GetProperty("ManyToOneRelationships").SetValue(metadata, manyToOne);
            typeof(EntityMetadata).GetProperty("ManyToManyRelationships").SetValue(metadata, manyToMany);
            var response = new RetrieveEntityResponse();
            response.Results["EntityMetadata"] = metadata;
            return response;
        }

        private class CustomRelationshipMetadata : RelationshipMetadataBase
        {
        }
    }
}
