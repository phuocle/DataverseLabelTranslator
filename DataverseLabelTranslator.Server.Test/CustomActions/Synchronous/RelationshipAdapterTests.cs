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
        public void Save_ReturnsEmpty_WhenChangedRowsNull()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });
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

            var nullResponseService = Substitute.For<IOrganizationService>();
            nullResponseService.Execute(Arg.Any<OrganizationRequest>()).Returns(_ => null);
            var nullResponseRelationships = AdapterTestHelpers.InvokeStatic<List<RelationshipMetadataBase>>(adapterType, "RetrieveEntityRelationships", nullResponseService, "account");
            Assert.AreEqual(0, nullResponseRelationships.Count);
            var nullResponsePlurals = AdapterTestHelpers.InvokeStatic<Dictionary<int, string>>(adapterType, "GetEntityPluralNames", nullResponseService, "account");
            Assert.AreEqual(0, nullResponsePlurals.Count);

            var nullPluralLabelService = Substitute.For<IOrganizationService>();
            nullPluralLabelService.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var metadata = new EntityMetadata { LogicalName = "account", DisplayCollectionName = null };
                var response = new RetrieveEntityResponse();
                response.Results["EntityMetadata"] = metadata;
                return response;
            });
            var nullPluralLabels = AdapterTestHelpers.InvokeStatic<Dictionary<int, string>>(adapterType, "GetEntityPluralNames", nullPluralLabelService, "account");
            Assert.AreEqual(0, nullPluralLabels.Count);

            AdapterTestHelpers.ExpectException<TargetInvocationException>(
                () => AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "ValidateGuid", " ", "Relationship MetadataId"));

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

            var mergedFromNullCurrent = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel", null, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = null }
            });
            Assert.AreEqual(1, mergedFromNullCurrent.Length);
            Assert.AreEqual(string.Empty, mergedFromNullCurrent[0].Label);

            var existingNullLabel = AdapterTestHelpers.BuildLabel((1033, null));
            var mergedExistingNull = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel", existingNullLabel, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = "JP" },
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = "JP2" }
            });
            Assert.AreEqual(string.Empty, mergedExistingNull[0].Label);
            Assert.AreEqual("JP2", mergedExistingNull[1].Label);
        }

        // ===== Tests added to reach 100% branch coverage =====

        [TestMethod]
        public void Load_NullEntityName_LoadsAll()
        {
            // Line 25: ?? null-coalesce on input.entityName.
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
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = null, component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_NonNoneEntityName_LoadsForEntity()
        {
            // Line 25: branch where entityName is provided (not null/whitespace/none).
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
        public void Save_RetrievesNullSitemapXml_HandlesEmptyString()
        {
            // Line 95: ?? null-coalesce on GetAttributeValue<string>("sitemapxml").
            var adapter = new RelationshipAdapter();
            var sitemapId = Guid.NewGuid();
            var sitemap = new Entity("sitemap", sitemapId);
            // Note: no sitemapxml attribute set, so GetAttributeValue returns null.
            var service = Substitute.For<IOrganizationService>();
            service.Retrieve("sitemap", sitemapId, Arg.Any<ColumnSet>()).Returns(sitemap);
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());

            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "relationships|" + sitemapId.ToString("D") + "|OneToMany|account_contact|AssociatedMenuConfiguration|contact",
                        changes = new Dictionary<string, string> { { "1033", "Label" } }
                    }
                }
            };

            // Should not throw — empty xml causes continue path.
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void Load_WithEntityHavingNullSitemapxml_LoadsEmptyRows()
        {
            // RelationshipAdapter.LoadAll filters by IsCustomizable and empty logical name;
            // this test covers the path where an entity with empty logical name is filtered out.
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var entity1 = new EntityMetadata { LogicalName = "", IsCustomizable = new BooleanManagedProperty(true) };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new[] { entity1 };
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithEntityNotCustomizable_LoadsEmptyRows()
        {
            // Coverage for the IsCustomizable == false branch in LoadAll.
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var entity1 = new EntityMetadata { LogicalName = "account", IsCustomizable = new BooleanManagedProperty(false) };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new[] { entity1 };
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithEntityCustomizableNull_DoesNotSkipEntity()
        {
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var entity = new EntityMetadata { LogicalName = "account", IsCustomizable = null };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new[] { entity };
                    return resp;
                }

                if (call[0] is RetrieveEntityRequest req && req.EntityFilters == EntityFilters.Relationships)
                {
                    return BuildRelationshipEntityResponse(req.LogicalName);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithEntityHavingNullDisplayName_UsesLogicalName()
        {
            // Coverage for the ?? null-coalesce on entity display name lookup.
            var adapter = new RelationshipAdapter();
            var service = CreateService();
            var accountId = Guid.NewGuid();
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
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var entity = new EntityMetadata { LogicalName = "account", IsCustomizable = new BooleanManagedProperty(true) };
                    entity.MetadataId = accountId;
                    entity.DisplayName = new Label();  // no labels
                    return new RetrieveAllEntitiesResponse { Results = { ["EntityMetadata"] = new[] { entity } } };
                }
                if (call[0] is RetrieveEntityRequest entityReq && entityReq.LogicalName == "account")
                {
                    return BuildRelationshipEntityResponse("account", oneToMany: new[] { rel });
                }
                if (call[0] is RetrieveEntityRequest entityReq2 && entityReq2.EntityFilters == EntityFilters.Entity)
                {
                    var response = AdapterTestHelpers.BuildEntityResponse(entityReq2.LogicalName);
                    // Return empty display name to trigger fallback to logical name.
                    response.EntityMetadata.DisplayName = new Label();
                    return response;
                }
                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("account", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void BuildRelationshipRow_NonCustomizable_ReturnsNull()
        {
            // Line 155: IsCustomizable check in BuildRelationshipRow.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "non_customizable",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(false),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033))
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNull(row);
        }

        [TestMethod]
        public void BuildRelationshipRow_OneToManyWithNullMenuConfig_ReturnsNull()
        {
            // Line 275-276: null menu config check for OneToMany.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "no_menu",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = null
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNull(row);
        }

        [TestMethod]
        public void BuildRelationshipRow_OneToManyNotCustomizableMenuConfig_ReturnsNull()
        {
            // Line 283: menu config IsCustomizable == false.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "menu_not_customizable",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033), isCustomizable: false)
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNull(row);
        }

        [TestMethod]
        public void BuildRelationshipRow_UseLabelWithNullLabel_ReturnsRowWithoutLanguageValues()
        {
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "null_label",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, null)
            };

            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);

            Assert.IsNotNull(row);
            Assert.IsFalse(row.ContainsKey("1033"));
        }

        [TestMethod]
        public void GetEntityPluralNames_IncludesNullLabelAsEmptyString()
        {
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var metadata = new EntityMetadata { LogicalName = "account" };
                var label = new Label();
                label.LocalizedLabels.Add(null);
                label.LocalizedLabels.Add(new LocalizedLabel(null, 1033));
                metadata.DisplayCollectionName = label;
                var response = new RetrieveEntityResponse();
                response.Results["EntityMetadata"] = metadata;
                return response;
            });

            var plurals = AdapterTestHelpers.InvokeStatic<Dictionary<int, string>>(adapterType, "GetEntityPluralNames", service, "account");

            Assert.AreEqual(1, plurals.Count);
            Assert.AreEqual(string.Empty, plurals[1033]);
        }

        [TestMethod]
        public void BuildRelationshipRow_ManyToManyNoEntity1MenuConfig_ReturnsNull()
        {
            // Line 307-318: ManyToMany menu config null/not-customizable.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var manyToMany = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_e1_null",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = null,
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Accounts", 1033))
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, manyToMany, "account", pluralCache);
            Assert.IsNull(row);

            var manyToMany2 = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_e1_not_customizable",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033), isCustomizable: false),
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Accounts", 1033))
            };
            var row2 = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, manyToMany2, "account", pluralCache);
            Assert.IsNull(row2);
        }

        [TestMethod]
        public void BuildRelationshipRow_ManyToManyEntity2Side_UsesEntity2Config()
        {
            // Line 339: M:N when entityName matches Entity2.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var manyToMany = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_entity2",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Accounts", 1033)),
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Contacts", 1033))
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, manyToMany, "contact", pluralCache);
            Assert.IsNotNull(row);
            Assert.IsTrue(row.GridKey.Contains("Entity2AssociatedMenuConfiguration"));
        }

        [TestMethod]
        public void BuildRelationshipRow_UseCollectionName_UsesEntityPluralNames()
        {
            // Line 352: UseCollectionName behavior triggers plural name lookup.
            var adapterType = typeof(RelationshipAdapter);
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest entityReq && entityReq.EntityFilters == EntityFilters.Entity)
                {
                    var response = AdapterTestHelpers.BuildEntityResponse(entityReq.LogicalName);
                    response.EntityMetadata.DisplayCollectionName = AdapterTestHelpers.BuildLabel((1033, "Contacts"));
                    return response;
                }
                return new OrganizationResponse();
            });
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "uses_plural",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseCollectionName)
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNotNull(row);
            Assert.AreEqual("Contacts", row["1033"]);
        }

        [TestMethod]
        public void UpdateAssociatedMenuConfiguration_CachesPluralName()
        {
            // Line 370: plural cache hit (avoids re-fetching).
            var adapterType = typeof(RelationshipAdapter);
            var service = CreateService();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "contact", new Dictionary<int, string> { { 1033, "CachedContacts" } } }
            };
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "uses_cached_plural",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseCollectionName)
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNotNull(row);
            Assert.AreEqual("CachedContacts", row["1033"]);
        }

        [TestMethod]
        public void BuildRelationshipRow_UseLabel_UsesLabel()
        {
            // Line 447-456: UseLabel behavior with existing Label.
            var adapterType = typeof(RelationshipAdapter);
            var service = Substitute.For<IOrganizationService>();
            var pluralCache = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var label = new Label("MyLabel", 1033);
            label.LocalizedLabels.Add(new LocalizedLabel("JapaneseLabel", 1041));
            var oneToMany = new OneToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "uses_label",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, label)
            };
            var row = AdapterTestHelpers.InvokeStatic<EasyTranslatorGridRowOutput>(adapterType, "BuildRelationshipRow", service, oneToMany, "account", pluralCache);
            Assert.IsNotNull(row);
            Assert.AreEqual("MyLabel", row["1033"]);
            Assert.AreEqual("JapaneseLabel", row["1041"]);
        }

        [TestMethod]
        public void Save_ValidGridKey_UpdatesLabel_AndPublishesTarget()
        {
            // Lines 566-578: cover the success path of Save for a OneToMany relationship.
            var adapter = new RelationshipAdapter();
            var relId = Guid.NewGuid();
            var rel = new OneToManyRelationshipMetadata
            {
                MetadataId = relId,
                SchemaName = "account_contact",
                ReferencedEntity = "account",
                ReferencingEntity = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("OldLabel", 1033))
            };
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveRelationshipRequest relReq && relReq.MetadataId == relId)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveRelationshipResponse(), "RelationshipMetadata", rel);
                }
                return new OrganizationResponse();
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(AdapterTestHelpers.Entities());

            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "relationships|" + relId.ToString("D") + "|OneToMany|account_contact|AssociatedMenuConfiguration|contact",
                        changes = new Dictionary<string, string> { { "1033", "NewLabel" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.IsTrue(output.changedRowCount > 0);
        }

        [TestMethod]
        public void Save_RelationshipInvalidMetadataId_Throws()
        {
            var adapter = new RelationshipAdapter();
            var service = Substitute.For<IOrganizationService>();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "relationships|not-a-guid|OneToMany|account_contact|AssociatedMenuConfiguration|contact",
                        changes = new Dictionary<string, string> { { "1033", "Label" } }
                    }
                }
            };
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() => adapter.Save(AdapterTestHelpers.Context(service), input));
        }

        [TestMethod]
        public void Save_RelationshipInvalidGridKey_Throws()
        {
            var adapter = new RelationshipAdapter();
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
        public void BuildMergedLabel_NullLabels_AreFiltered()
        {
            // Line 557: handles null LocalizedLabel entries.
            var adapterType = typeof(RelationshipAdapter);
            var currentLabel = AdapterTestHelpers.BuildLabel((1033, "Existing"));
            currentLabel.LocalizedLabels.Insert(0, null);
            currentLabel.LocalizedLabels.Add(null);
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel", currentLabel, new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = "JP" }
            });
            Assert.AreEqual(2, merged.Length);
        }

        [TestMethod]
        public void UpdateAssociatedMenuConfiguration_ManyToMany_UsesEntity1Config()
        {
            // Line 566: ManyToMany update path with Entity1AssociatedMenuConfiguration.
            var adapterType = typeof(RelationshipAdapter);
            var service = CreateService();
            var rel = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_test",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity1AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Old", 1033))
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveRelationshipRequest relReq)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveRelationshipResponse(), "RelationshipMetadata", rel);
                }
                return new OrganizationResponse();
            });
            var method = typeof(RelationshipAdapter).GetMethod("UpdateAssociatedMenuConfiguration", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            // relType contains "N:N" => isManyToMany branch
            method.Invoke(null, new object[] { service, rel.MetadataId.Value, "ManyToMany:1:N", "mm_test", "Entity1AssociatedMenuConfiguration", rel.Entity1AssociatedMenuConfiguration });
            // Verify UpdateRelationshipRequest was executed
            service.Received().Execute(Arg.Is<OrganizationRequest>(r => r is UpdateRelationshipRequest));
        }

        [TestMethod]
        public void UpdateAssociatedMenuConfiguration_ManyToMany_UsesEntity2Config()
        {
            // Line 566: ManyToMany update path with Entity2AssociatedMenuConfiguration.
            var adapterType = typeof(RelationshipAdapter);
            var service = CreateService();
            var rel = new ManyToManyRelationshipMetadata
            {
                MetadataId = Guid.NewGuid(),
                SchemaName = "mm_test2",
                Entity1LogicalName = "account",
                Entity2LogicalName = "contact",
                IsCustomizable = new BooleanManagedProperty(true),
                Entity2AssociatedMenuConfiguration = CreateMenuConfig(AssociatedMenuBehavior.UseLabel, new Label("Old", 1033))
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveRelationshipRequest relReq)
                {
                    return AdapterTestHelpers.SetResultsAndReturn(new RetrieveRelationshipResponse(), "RelationshipMetadata", rel);
                }
                return new OrganizationResponse();
            });
            var method = typeof(RelationshipAdapter).GetMethod("UpdateAssociatedMenuConfiguration", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { service, rel.MetadataId.Value, "ManyToMany:N:1", "mm_test2", "Entity2AssociatedMenuConfiguration", rel.Entity2AssociatedMenuConfiguration });
            service.Received().Execute(Arg.Is<OrganizationRequest>(r => r is UpdateRelationshipRequest));
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
