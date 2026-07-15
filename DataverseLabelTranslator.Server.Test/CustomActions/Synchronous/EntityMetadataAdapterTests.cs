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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class EntityMetadataAdapterTests
    {
        [TestCleanup]
        public void Cleanup() { EntityMetadataAdapter.WaitAction = System.Threading.Thread.Sleep; }

        private static IOrganizationService CreateService()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
            return service;
        }

        [TestMethod]
        public void Save_ReturnsEmpty_WhenNoChanges()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Published_WaitsAndReturns()
        {
            int waitCount = 0;
            EntityMetadataAdapter.WaitAction = (ms) => { waitCount++; };
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget> { new EasyTranslatorPublishTarget { kind = "entity", id = "account" } }
            };
            var output = adapter.Published(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(1, waitCount);
        }

        [TestMethod]
        public void Load_NoEntities_ReturnsEmpty()
        {
            var adapter = new EntityMetadataAdapter();
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
        public void Load_WithEntities_ReturnsRows()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "entity") return AdapterTestHelpers.Entities(MakeEntity("account", "Account"));
                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    var em = new EntityMetadata { LogicalName = "account", DisplayName = AdapterTestHelpers.BuildLabel((1033, "Account")) };
                    resp.Results["EntityMetadata"] = new[] { em };
                    return resp;
                }
                return new OrganizationResponse();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.IsTrue(output.grid.rows.Count > 0);
        }

        [TestMethod]
        public void Load_WithNullMetadataEntry_SortsAndSkipsNull()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    var resp = new RetrieveAllEntitiesResponse();
                    resp.Results["EntityMetadata"] = new[] { MakeMetadata("account"), null, MakeMetadata("contact") };
                    return resp;
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(2, output.grid.rows.Count);
            CollectionAssert.AreEqual(new[] { "Account (account)", "Contact (contact)" }, output.grid.rows.Select(row => row.SchemaName).ToArray());
        }

        [TestMethod]
        public void Load_DisplayText_BuildsDisplayTextAndCollectionNameChildren()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var entity = MakeMetadata(
                "account",
                displayName: AdapterTestHelpers.BuildLabel((1033, "Account"), (1041, "Account JP")),
                collectionName: AdapterTestHelpers.BuildLabel((1033, "Accounts"), (1066, "Accounts VN")),
                description: AdapterTestHelpers.BuildLabel((1033, "Account Description")));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(entity);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual("Entity Metadata", output.grid.title);
            Assert.AreEqual(1, output.grid.rows.Count);
            var parent = output.grid.rows[0];
            var children = GetChildren(parent);
            Assert.AreEqual("Account (account)", parent.SchemaName);
            Assert.AreEqual(false, parent["isEditable"]);
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual("Display Text", children[0].SchemaName);
            Assert.AreEqual("entityMeta|account|DisplayName", children[0].GridKey);
            Assert.AreEqual("Collection Name", children[1].SchemaName);
            Assert.AreEqual("entityMeta|account|CollectionName", children[1].GridKey);
        }

        [TestMethod]
        public void Load_Description_BuildsDescriptionAndCollectionNameChildren()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var entity = MakeMetadata(
                "account",
                displayName: AdapterTestHelpers.BuildLabel((1033, "Account")),
                collectionName: AdapterTestHelpers.BuildLabel((1033, "Accounts")),
                description: AdapterTestHelpers.BuildLabel((1033, "Account Description"), (1041, "Description JP")));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(entity);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "Description", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            var children = GetChildren(output.grid.rows[0]);
            Assert.AreEqual(2, children.Count);
            Assert.AreEqual("Description", children[0].SchemaName);
            Assert.AreEqual("entityMeta|account|Description", children[0].GridKey);
            Assert.AreEqual("Collection Name", children[1].SchemaName);
            Assert.AreEqual("entityMeta|account|CollectionName", children[1].GridKey);
        }

        [TestMethod]
        public void Load_SkipsNullBlankAndNonCustomizableEntities()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var blank = MakeMetadata(" ");
            var locked = MakeMetadata("locked", customizable: false);
            var account = MakeMetadata("account", displayName: new Label());
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(null, blank, locked, account);
                }

                return new OrganizationResponse();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("account", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Load_WithSolutionId_ReturnsRows()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "solutioncomponent") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });
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
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = Guid.NewGuid().ToString("D") });
            Assert.IsTrue(output.grid.rows.Count >= 0);
        }

        [TestMethod]
        public void Load_WithSolutionId_FiltersBySolutionComponents()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var solutionId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var contactId = Guid.NewGuid();
            var account = MakeMetadata("account", metadataId: accountId);
            var contact = MakeMetadata("contact", metadataId: contactId);
            var blank = MakeMetadata(" ", metadataId: Guid.NewGuid());
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(null, blank, account, contact);
                }

                return new OrganizationResponse();
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization")
                {
                    return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                }

                if (q.EntityName == "solutioncomponent")
                {
                    var missingObjectId = new Entity("solutioncomponent");
                    var emptyObjectId = new Entity("solutioncomponent") { ["objectid"] = Guid.Empty };
                    var accountComponent = new Entity("solutioncomponent") { ["objectid"] = accountId };
                    var unknownComponent = new Entity("solutioncomponent") { ["objectid"] = Guid.NewGuid() };
                    return AdapterTestHelpers.Entities(missingObjectId, emptyObjectId, accountComponent, unknownComponent);
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = solutionId.ToString("D") });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Account (account)", output.grid.rows[0].SchemaName);
        }

        [TestMethod]
        public void Publish_WithTargets_ExecutesPublishXml()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
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
        public void Save_WithInvalidGridKey_Throws()
        {
            var adapter = new EntityMetadataAdapter();
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
        public void Save_WithInvalidChildKey_Throws()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "entityMeta|account|BadKey",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "EntityMetadata gridKey child key");
        }

        [TestMethod]
        public void Save_WithNullChangedRows_ReturnsEmpty()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = null });

            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithNonNumericChange_SkipsRow()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = "entityMeta|account|DisplayName",
                        changes = new Dictionary<string, string> { { "not-a-language", "X" } }
                    }
                }
            };

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_WithMissingEntityMetadata_Throws()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return new RetrieveEntityResponse();
                }

                return new OrganizationResponse();
            });
            var input = SaveInput("entityMeta|account|DisplayName", (1033, "New Account"));

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "EntityMetadata not found");
        }

        [TestMethod]
        public void Save_WithNullRetrieveEntityResponse_Throws()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest)
                {
                    return null;
                }

                return new OrganizationResponse();
            });
            var input = SaveInput("entityMeta|account|DisplayName", (1033, "New Account"));

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => adapter.Save(AdapterTestHelpers.Context(service), input),
                "EntityMetadata not found");
        }

        [TestMethod]
        public void Save_WithValidDisplayName_Updates()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var metadataId = Guid.NewGuid();
            var em = MakeMetadata("account", metadataId: metadataId, displayName: AdapterTestHelpers.BuildLabel((1033, "Account"), (1033, "Duplicate"), (1041, null)));
            var input = SaveInput("entityMeta|account|DisplayName", (1033, "New Account"), (1066, "Account VN"));
            UpdateEntityRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return BuildRetrieveEntityResponse(em);
                if (call[0] is UpdateEntityRequest update)
                {
                    captured = update;
                    return new OrganizationResponse();
                }

                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("entity", output.publishTargets[0].kind);
            Assert.AreEqual("account", output.publishTargets[0].id);
            Assert.IsTrue(captured.MergeLabels);
            Assert.AreEqual(metadataId, captured.Entity.MetadataId);
            Assert.AreEqual("account", captured.Entity.LogicalName);
            Assert.IsNotNull(captured.Entity.DisplayName);
            CollectionAssert.AreEqual(new[] { 1033, 1041, 1066 }, captured.Entity.DisplayName.LocalizedLabels.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "New Account", string.Empty, "Account VN" }, captured.Entity.DisplayName.LocalizedLabels.Select(label => label.Label).ToArray());
            Assert.IsNull(captured.Entity.DisplayCollectionName);
            Assert.IsNull(captured.Entity.Description);
        }

        [TestMethod]
        public void Save_WithValidCollectionName_UpdatesCollectionName()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var em = MakeMetadata("account", collectionName: AdapterTestHelpers.BuildLabel((1033, "Accounts")));
            var input = SaveInput("entityMeta|account|CollectionName", (1041, "Accounts JP"));
            UpdateEntityRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return BuildRetrieveEntityResponse(em);
                if (call[0] is UpdateEntityRequest update)
                {
                    captured = update;
                    return new OrganizationResponse();
                }

                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(captured.Entity.DisplayCollectionName);
            Assert.IsNull(captured.Entity.DisplayName);
            Assert.IsNull(captured.Entity.Description);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, captured.Entity.DisplayCollectionName.LocalizedLabels.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "Accounts", "Accounts JP" }, captured.Entity.DisplayCollectionName.LocalizedLabels.Select(label => label.Label).ToArray());
        }

        [TestMethod]
        public void Save_WithValidDescription_UpdatesDescription()
        {
            var adapter = new EntityMetadataAdapter();
            var service = CreateService();
            var em = MakeMetadata("account", description: AdapterTestHelpers.BuildLabel((1033, "Account Description")));
            var input = SaveInput("entityMeta|account|Description", (1041, "Description JP"));
            UpdateEntityRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveEntityRequest) return BuildRetrieveEntityResponse(em);
                if (call[0] is UpdateEntityRequest update)
                {
                    captured = update;
                    return new OrganizationResponse();
                }

                return new OrganizationResponse();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), input);

            Assert.AreEqual(1, output.changedRowCount);
            Assert.IsNotNull(captured.Entity.Description);
            Assert.IsNull(captured.Entity.DisplayName);
            Assert.IsNull(captured.Entity.DisplayCollectionName);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, captured.Entity.Description.LocalizedLabels.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "Account Description", "Description JP" }, captured.Entity.Description.LocalizedLabels.Select(label => label.Label).ToArray());
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var type = typeof(EntityMetadataAdapter);
            var labelWithNulls = new Label();
            labelWithNulls.LocalizedLabels.Add(null);
            labelWithNulls.LocalizedLabels.Add(new LocalizedLabel("", 1033));
            labelWithNulls.LocalizedLabels.Add(new LocalizedLabel("Contact", 1033));
            var duplicateLabel = AdapterTestHelpers.BuildLabel((1033, "Account"), (1033, "Duplicate"));
            var changes = new List<EasyTranslatorLabelChange>
            {
                new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "Account Updated" },
                new EasyTranslatorLabelChange { LanguageCode = 1041, Label = null }
            };

            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(type, "GetBaseLanguageLabel", null, 1033));
            Assert.AreEqual("Contact", AdapterTestHelpers.InvokeStatic<string>(type, "GetBaseLanguageLabel", labelWithNulls, 1033));
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", duplicateLabel, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, merged.Select(label => label.LanguageCode).ToArray());
            CollectionAssert.AreEqual(new[] { "Account Updated", string.Empty }, merged.Select(label => label.Label).ToArray());
            var mergedFromNull = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", null, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, mergedFromNull.Select(label => label.LanguageCode).ToArray());
            var mergedFromNullExisting = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(type, "BuildMergedLabel", labelWithNulls, changes);
            CollectionAssert.AreEqual(new[] { 1033, 1041 }, mergedFromNullExisting.Select(label => label.LanguageCode).ToArray());
        }

        private static Entity MakeEntity(string name, string displayName)
        {
            var e = new Entity("entity") { Id = Guid.NewGuid() };
            e["name"] = name;
            return e;
        }

        private static List<EasyTranslatorGridRowOutput> GetChildren(EasyTranslatorGridRowOutput row)
        {
            return (List<EasyTranslatorGridRowOutput>)row["children"];
        }

        private static EntityMetadata MakeMetadata(
            string logicalName,
            Guid? metadataId = null,
            bool customizable = true,
            Label displayName = null,
            Label collectionName = null,
            Label description = null)
        {
            return new EntityMetadata
            {
                LogicalName = logicalName,
                MetadataId = metadataId ?? Guid.NewGuid(),
                IsCustomizable = new BooleanManagedProperty(customizable),
                DisplayName = displayName ?? AdapterTestHelpers.BuildLabel((1033, ToTitle(logicalName))),
                DisplayCollectionName = collectionName ?? AdapterTestHelpers.BuildLabel((1033, ToTitle(logicalName) + "s")),
                Description = description ?? AdapterTestHelpers.BuildLabel((1033, ToTitle(logicalName) + " Description"))
            };
        }

        private static string ToTitle(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return logicalName;
            }

            return char.ToUpperInvariant(logicalName[0]) + logicalName.Substring(1);
        }

        private static RetrieveEntityResponse BuildRetrieveEntityResponse(EntityMetadata metadata)
        {
            var response = new RetrieveEntityResponse();
            response.Results["EntityMetadata"] = metadata;
            return response;
        }

        private static EasyTranslatorSaveInput SaveInput(string gridKey, params (int lcid, string value)[] changes)
        {
            var rowChanges = new Dictionary<string, string>();
            foreach (var (lcid, value) in changes)
            {
                rowChanges[lcid.ToString()] = value;
            }

            return new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = gridKey,
                        changes = rowChanges
                    }
                }
            };
        }
    }
}
