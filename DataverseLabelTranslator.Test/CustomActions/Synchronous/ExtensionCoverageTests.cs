using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using DataverseLabelTranslator.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    /// <summary>
    /// Exercises the internal static <c>Extension</c> helper methods compiled into
    /// DataverseLabelTranslator.Server.dll from DataverseLabelTranslator.Shared/Lib/Extension.cs.
    /// The Server assembly declares InternalsVisibleTo("DataverseLabelTranslator.Test"), so the
    /// internal extension methods and the internal abstract EntityBase type are reachable here.
    /// </summary>
    [TestClass]
    public class ExtensionCoverageTests
    {
        /// <summary>
        /// Concrete, test-only EntityBase subclass mirroring how production code derives EntityBase.
        /// Declared internal (EntityBase is internal) with a public constructor so
        /// Activator.CreateInstance(typeof(T), new object[]{ entity }) works for RetrieveMultiple&lt;T&gt;.
        /// </summary>
        internal sealed class TestEntityBase : EntityBase
        {
            public TestEntityBase() { Entity = new Entity(); }
            public TestEntityBase(Entity entity) { Entity = entity; }
            public void SetPreEntity(Entity preEntity) { PreEntity = preEntity; }
        }

        /// <summary>Public type with a public (Entity) constructor used to exercise the success
        /// path of Retrieve&lt;T&gt;/RetrieveMultiple&lt;T&gt; without EntityBase's abstract restrictions.</summary>
        public class TestRetrieveEntity
        {
            public Entity Inner { get; }
            public TestRetrieveEntity() { }
            public TestRetrieveEntity(Entity entity) { Inner = entity; }
        }

        private static TestEntityBase BuildBaseForUpdate(Guid id)
        {
            var entity = new Entity("account", id) { ["name"] = "Current", ["statuscode"] = new OptionSetValue(1) };
            var @base = new TestEntityBase(entity);
            // Empty pre-image so every attribute on Entity is treated as changed by GetUpdateEntity.
            @base.SetPreEntity(new Entity("account"));
            return @base;
        }

        private const string FetchXml = "<fetch><entity name='account'><all-attributes/></entity></fetch>";

        // 1. ToUpdateRequest overloads
        [TestMethod]
        public void ToUpdateRequest_CoversEntityAndEntityBaseOverloads()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id) { ["name"] = "Acme" };

            var r1 = (UpdateRequest)entity.ToUpdateRequest();
            Assert.AreSame(entity, r1.Target);

            var r2 = (UpdateRequest)entity.ToUpdateRequest("tag");
            Assert.AreSame(entity, r2.Target);
            Assert.AreEqual("tag", r2["tag"]);

            var @base = BuildBaseForUpdate(id);
            var r3 = (UpdateRequest)@base.ToUpdateRequest();
            Assert.AreEqual("account", r3.Target.LogicalName);
            Assert.AreEqual(id, r3.Target.Id);
            Assert.IsTrue(r3.Target.Contains("name"));

            var r4 = (UpdateRequest)@base.ToUpdateRequest("tagbase");
            Assert.AreEqual("tagbase", r4["tag"]);
            Assert.AreEqual(id, r4.Target.Id);
        }

        // 2. ToCreateRequest overloads
        [TestMethod]
        public void ToCreateRequest_CoversEntityAndEntityBaseOverloads()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id) { ["name"] = "Acme" };

            var r1 = (CreateRequest)entity.ToCreateRequest();
            Assert.AreSame(entity, r1.Target);

            var r2 = (CreateRequest)entity.ToCreateRequest("tag");
            Assert.AreSame(entity, r2.Target);
            Assert.AreEqual("tag", r2["tag"]);

            var @base = new TestEntityBase(entity);
            var r3 = (CreateRequest)@base.ToCreateRequest();
            Assert.AreSame(entity, r3.Target);

            var r4 = (CreateRequest)@base.ToCreateRequest("tagbase");
            Assert.AreSame(entity, r4.Target);
            Assert.AreEqual("tagbase", r4["tag"]);
        }

        // 3. ToDeleteRequest overloads
        [TestMethod]
        public void ToDeleteRequest_CoversAllOverloads()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id);
            var reference = new EntityReference("account", id);

            var r1 = (DeleteRequest)entity.ToDeleteRequest();
            Assert.AreEqual(id, r1.Target.Id);

            var r2 = (DeleteRequest)reference.ToDeleteRequest();
            Assert.AreSame(reference, r2.Target);

            var r3 = (DeleteRequest)entity.ToDeleteRequest("etag");
            Assert.AreEqual("etag", r3["tag"]);
            Assert.AreEqual(id, r3.Target.Id);

            var r4 = (DeleteRequest)reference.ToDeleteRequest("rtag");
            Assert.AreEqual("rtag", r4["tag"]);
            Assert.AreSame(reference, r4.Target);

            var @base = new TestEntityBase(entity);
            var r5 = (DeleteRequest)@base.ToDeleteRequest();
            Assert.AreEqual(id, r5.Target.Id);

            var r6 = (DeleteRequest)@base.ToDeleteRequest("btag");
            Assert.AreEqual("btag", r6["tag"]);
            Assert.AreEqual(id, r6.Target.Id);
        }

        // 4. ToUpsertRequest overloads
        [TestMethod]
        public void ToUpsertRequest_CoversEntityAndEntityBaseOverloads()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id) { ["name"] = "Acme" };

            var r1 = (UpsertRequest)entity.ToUpsertRequest();
            Assert.AreSame(entity, r1.Target);

            var r2 = (UpsertRequest)entity.ToUpsertRequest("tag");
            Assert.AreSame(entity, r2.Target);
            Assert.AreEqual("tag", r2["tag"]);

            var @base = new TestEntityBase(entity);
            var r3 = (UpsertRequest)@base.ToUpsertRequest();
            Assert.AreSame(entity, r3.Target);

            var r4 = (UpsertRequest)@base.ToUpsertRequest("tagbase");
            Assert.AreSame(entity, r4.Target);
            Assert.AreEqual("tagbase", r4["tag"]);
        }

        // 5. Create / Update / Delete / Upsert service extension methods
        [TestMethod]
        public void ServiceCreateUpdateDeleteUpsert_CoversEntityBaseExtensions()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var service = Substitute.For<IOrganizationService>();
            service.Create(Arg.Any<Entity>()).Returns(id1);
            service.Execute(Arg.Any<UpsertRequest>()).Returns(new UpsertResponse());

            var baseNoId = new TestEntityBase(new Entity("account") { ["name"] = "Acme" });
            Assert.AreEqual(id1, service.Create(baseNoId));

            var baseForUpdate = BuildBaseForUpdate(id2);
            service.Update(baseForUpdate);
            service.Received().Update(Arg.Is<Entity>(e => e.LogicalName == "account" && e.Id == id2 && e.Contains("name")));

            service.Delete(new TestEntityBase(new Entity("account", id1)));
            service.Received().Delete("account", id1);

            service.Delete(new EntityReference("account", id2));
            service.Received().Delete("account", id2);

            var entity = new Entity("account", id1) { ["name"] = "Acme" };
            var upsertResponse = service.Upsert(entity);
            Assert.IsNotNull(upsertResponse);

            var upsertResponse2 = service.Upsert(baseNoId);
            Assert.IsNotNull(upsertResponse2);

            service.Received(2).Execute(Arg.Any<UpsertRequest>());
        }

        // 6. SetState overloads
        [TestMethod]
        public void SetState_CoversAllOverloads()
        {
            var id = Guid.NewGuid();
            var service = Substitute.For<IOrganizationService>();

            service.SetState(new EntityReference("account", id), 0, 1);
            service.SetState(new Entity("account", id), 1, 2);
            service.SetState(new TestEntityBase(new Entity("account", id)), 3, 4);

            service.Received(3).Update(Arg.Any<Entity>());
            service.Received().Update(Arg.Is<Entity>(e =>
                ((OptionSetValue)e["statecode"]).Value == 1 &&
                ((OptionSetValue)e["statuscode"]).Value == 2));
        }

        // 7. Associate / Disassociate
        [TestMethod]
        public void AssociateAndDisassociate_InvokeUnderlyingServiceMethods()
        {
            var service = Substitute.For<IOrganizationService>();
            var target = new EntityReference("account", Guid.NewGuid());
            var related1 = new EntityReference("contact", Guid.NewGuid());
            var related2 = new EntityReference("contact", Guid.NewGuid());

            service.Associate(target, "account_contacts", related1, related2);
            service.Received().Associate(
                "account", target.Id,
                Arg.Is<Relationship>(r => r.SchemaName == "account_contacts"),
                Arg.Is<EntityReferenceCollection>(c => c.Count == 2 && c.Contains(related1) && c.Contains(related2)));

            service.Disassociate(target, "account_contacts", related1);
            service.Received().Disassociate(
                "account", target.Id,
                Arg.Is<Relationship>(r => r.SchemaName == "account_contacts"),
                Arg.Is<EntityReferenceCollection>(c => c.Count == 1 && c.Contains(related1)));
        }

        // 8. ExecuteMultiple batch boundaries and no-op
        [TestMethod]
        public void ExecuteMultiple_CoversBatchBoundaryAndNoOp()
        {
            var service = Substitute.For<IOrganizationService>();

            // 0 requests -> no Execute calls.
            service.ExecuteMultiple(new List<OrganizationRequest>(), true, 2);
            service.DidNotReceive().Execute(Arg.Any<ExecuteMultipleRequest>());

            // 2 requests, batch size 2 -> exactly one batch (hits count == batchSize then reset; tail count == 0).
            service.ClearReceivedCalls();
            var two = new List<OrganizationRequest>
            {
                new CreateRequest { Target = new Entity("account") },
                new UpdateRequest { Target = new Entity("account", Guid.NewGuid()) }
            };
            service.ExecuteMultiple(two, true, 2);
            service.Received(1).Execute(Arg.Any<ExecuteMultipleRequest>());

            // 3 requests, batch size 2 -> one full batch + one tail batch => 2 Execute calls.
            service.ClearReceivedCalls();
            var three = new List<OrganizationRequest>
            {
                new CreateRequest { Target = new Entity("account") },
                new CreateRequest { Target = new Entity("account") },
                new CreateRequest { Target = new Entity("account") }
            };
            service.ExecuteMultiple(three, false, 2);
            service.Received(2).Execute(Arg.Any<ExecuteMultipleRequest>());
        }

        // 9. Retrieve<T> returns default(T) when service.Retrieve throws
        [TestMethod]
        public void RetrieveGeneric_ReturnsDefaultWhenServiceThrows()
        {
            var throwing = Substitute.For<IOrganizationService>();
            throwing.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>())
                .Returns(_ => throw new InvalidOperationException("boom"));

            Assert.IsNull(throwing.Retrieve<string>("account", Guid.NewGuid(), new ColumnSet(true)));
            Assert.IsNull(throwing.Retrieve<string>("account", Guid.NewGuid(), "name"));
            Assert.AreEqual(0, throwing.Retrieve<int>("account", Guid.NewGuid(), new ColumnSet(true)));
        }

        // 10. Retrieve entityName & EntityReference overloads incl. null-reference paths
        [TestMethod]
        public void Retrieve_CoversNameReferenceAndNullPaths()
        {
            var id = Guid.NewGuid();
            var service = Substitute.For<IOrganizationService>();
            var entity = new Entity("account", id) { ["name"] = "Acme" };
            service.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(entity);

            // string-name overloads
            Assert.AreSame(entity, service.Retrieve("account", id, new ColumnSet(true)));
            Assert.AreSame(entity, service.Retrieve("account", id, "name", "statuscode"));

            // generic success path (type with public Entity constructor)
            var got = service.Retrieve<TestRetrieveEntity>("account", id, new ColumnSet(true));
            Assert.IsNotNull(got);
            Assert.AreSame(entity, got.Inner);

            var gotParams = service.Retrieve<TestRetrieveEntity>("account", id, "name");
            Assert.IsNotNull(gotParams);

            // generic Activator-failure path -> default (string has no Entity constructor)
            Assert.IsNull(service.Retrieve<string>("account", id, new ColumnSet(true)));
            Assert.IsNull(service.Retrieve<string>("account", id, "name"));

            // EntityReference-based overloads
            var reference = new EntityReference("account", id);
            Assert.AreSame(entity, service.Retrieve(reference, new ColumnSet(true)));
            Assert.AreSame(entity, service.Retrieve(reference, "name"));

            var refGot = service.Retrieve<TestRetrieveEntity>(reference, new ColumnSet(true));
            Assert.IsNotNull(refGot);

            // null EntityReference short-circuits to null/default without needing the service.
            // We assert the return-only contract; we cannot assert "no service call" because the
            // substitute's instance Retrieve(string,Guid,ColumnSet) is a partial mock whose default
            // return for reference types is null already, so a missed guard would still return null.
            Assert.IsNull(service.Retrieve((EntityReference)null, new ColumnSet(true)));
            Assert.IsNull(service.Retrieve((EntityReference)null, "name"));
            Assert.IsNull(service.Retrieve<TestRetrieveEntity>((EntityReference)null, new ColumnSet(true)));
            Assert.AreEqual(0, service.Retrieve<int>((EntityReference)null, new ColumnSet(true)));
        }

        // 11. RetrieveMultiple paging loop + EntityCollection accumulation
        [TestMethod]
        public void RetrieveMultiple_CoversPagingAndEntityCollectionAccumulation()
        {
            var service = Substitute.For<IOrganizationService>();

            // Single page, no more records.
            var single = new EntityCollection(new[]
            {
                new Entity("account"){ ["name"] = "A" },
                new Entity("account"){ ["name"] = "B" }
            }) { MoreRecords = false };
            service.RetrieveMultiple(Arg.Any<FetchExpression>()).Returns(single);

            var flat = service.RetrieveMultiple(FetchXml);
            Assert.AreEqual(2, flat.Entities.Count);

            // Generic typed variant (T : EntityBase) with a single page.
            var typed = service.RetrieveMultiple<TestEntityBase>(FetchXml);
            Assert.AreEqual(2, typed.Count);
            Assert.AreEqual("A", typed[0].Entity.GetAttributeValue<string>("name"));

            // Paging: first page signals MoreRecords=true, subsequent pages false -> loop runs twice.
            // Use a fresh service + fresh counter so the EntityCollection loop and typed loop are independent.
            var pagedService = Substitute.For<IOrganizationService>();
            var calls = 0;
            pagedService.RetrieveMultiple(Arg.Any<FetchExpression>()).Returns(x =>
            {
                calls++;
                var c = new EntityCollection(new[] { new Entity("account"){ ["name"] = "r" + calls } });
                c.MoreRecords = calls == 1;
                c.PagingCookie = "cookie-" + calls;
                return c;
            });

            var accumulated = pagedService.RetrieveMultiple(FetchXml);
            Assert.AreEqual(2, accumulated.Entities.Count);
            Assert.AreEqual(2, calls);

            // Typed paging: same engine, fresh service/counter, exercises the generic paging branch.
            var typedPagedService = Substitute.For<IOrganizationService>();
            var typedCalls = 0;
            typedPagedService.RetrieveMultiple(Arg.Any<FetchExpression>()).Returns(x =>
            {
                typedCalls++;
                var c = new EntityCollection(new[] { new Entity("account"){ ["name"] = "t" + typedCalls } });
                c.MoreRecords = typedCalls == 1;
                c.PagingCookie = "cookie-" + typedCalls;
                return c;
            });

            var typedPaged = typedPagedService.RetrieveMultiple<TestEntityBase>(FetchXml);
            Assert.AreEqual(2, typedPaged.Count);
            Assert.AreEqual(2, typedCalls);
        }

        // 12. GetAliasedValue conversion paths
        [TestMethod]
        public void GetAliasedValue_CoversConversionPaths()
        {
            var refId = Guid.NewGuid();
            var entity = new Entity("account", Guid.NewGuid());
            entity["alias.name"] = new AliasedValue("contact", "fullname", "Ada");
            entity["alias.ref"] = new AliasedValue("contact", "contactid", refId);
            entity["alias.acc"] = new AliasedValue("account", "accountid", new EntityReference("account", refId));
            entity["alias.empty"] = new AliasedValue("contact", "x", null);

            // string typed value (alias + attribute)
            Assert.AreEqual("Ada", entity.GetAliasedValue<string>("alias", "name"));
            // string typed value (single aliased name)
            Assert.AreEqual("Ada", entity.GetAliasedValue<string>("alias.name"));

            // Guid value that is already a Guid -> direct cast.
            Assert.AreEqual(refId, entity.GetAliasedValue<Guid>("alias", "ref"));

            // EntityReference-from-Guid conversion path (aliased.Value is Guid, T == EntityReference).
            var refVal = entity.GetAliasedValue<EntityReference>("alias.ref");
            Assert.IsNotNull(refVal);
            Assert.AreEqual("contact", refVal.LogicalName);
            Assert.AreEqual(refId, refVal.Id);

            // Guid-from-EntityReference conversion path (aliased.Value is EntityReference, T == Guid).
            Assert.AreEqual(refId, entity.GetAliasedValue<Guid>("alias.acc"));

            // Missing key -> default(T).
            Assert.IsNull(entity.GetAliasedValue<string>("alias.missing"));

            // Present key but aliased.Value == null -> default(T).
            Assert.IsNull(entity.GetAliasedValue<string>("alias.empty"));
        }

        // 13. GetFormattedValue
        [TestMethod]
        public void GetFormattedValue_ReturnsFormattedOrFallback()
        {
            var entity = new Entity("account", Guid.NewGuid());
            entity.FormattedValues["statuscode"] = "Active";
            Assert.AreEqual("Active", entity.GetFormattedValue("statuscode"));
            Assert.IsNull(entity.GetFormattedValue("missing"));
        }

        // 14. MergeAttributes
        [TestMethod]
        public void MergeAttributes_MergesAndIgnoresNullSource()
        {
            var target = new Entity("account") { ["name"] = "Keep" };
            var source = new Entity("account") { ["name"] = "ShouldNotOverride", ["description"] = "New" };

            // null source is a no-op.
            target.MergeAttributes(null);
            Assert.AreEqual("Keep", target["name"]);
            Assert.IsFalse(target.Contains("description"));

            // existing attribute preserved, missing attribute copied.
            target.MergeAttributes(source);
            Assert.AreEqual("Keep", target["name"]);
            Assert.AreEqual("New", target["description"]);
        }

        // 15. HasChanged branch coverage
        [TestMethod]
        public void HasChanged_CoversAllBranches()
        {
            var id = Guid.NewGuid();
            var contactRef = new EntityReference("contact", Guid.NewGuid()) { Name = "C" };

            // preImage null -> returns target.Contains(attribute)
            var target = new Entity("account", id) { ["name"] = "X" };
            Assert.IsTrue(target.HasChanged("name", null));
            Assert.IsFalse(target.HasChanged("missing", null));

            // target missing attribute -> false (regardless of preimage having it)
            Assert.IsFalse(target.HasChanged("absent", new Entity("account", id) { ["absent"] = "Y" }));

            // target has, preimage missing -> true
            Assert.IsTrue(target.HasChanged("name", new Entity("account", id)));

            // both present, both null -> false
            var tNull = new Entity("account", id) { ["v"] = null };
            var pNull = new Entity("account", id) { ["v"] = null };
            Assert.IsFalse(tNull.HasChanged("v", pNull));

            // one null, the other not -> true (both directions)
            var tNotNull = new Entity("account", id) { ["v"] = "x" };
            var pNullOnly = new Entity("account", id) { ["v"] = null };
            Assert.IsTrue(tNotNull.HasChanged("v", pNullOnly));

            var tNullOnly = new Entity("account", id) { ["v"] = null };
            var pNotNull = new Entity("account", id) { ["v"] = "x" };
            Assert.IsTrue(tNullOnly.HasChanged("v", pNotNull));

            // OptionSetValue equal -> false, different -> true
            var osvSame = new Entity("account", id) { ["statuscode"] = new OptionSetValue(1) };
            Assert.IsFalse(osvSame.HasChanged("statuscode", new Entity("account", id) { ["statuscode"] = new OptionSetValue(1) }));
            Assert.IsTrue(osvSame.HasChanged("statuscode", new Entity("account", id) { ["statuscode"] = new OptionSetValue(2) }));

            // Money equal -> false, different -> true
            var moneySame = new Entity("account", id) { ["amount"] = new Money(10m) };
            Assert.IsFalse(moneySame.HasChanged("amount", new Entity("account", id) { ["amount"] = new Money(10m) }));
            Assert.IsTrue(moneySame.HasChanged("amount", new Entity("account", id) { ["amount"] = new Money(99m) }));

            // EntityReference equal -> false, different Id -> true, different LogicalName -> true
            var erSame = new Entity("account", id) { ["primarycontactid"] = contactRef };
            Assert.IsFalse(erSame.HasChanged("primarycontactid", new Entity("account", id) { ["primarycontactid"] = contactRef }));
            Assert.IsTrue(erSame.HasChanged("primarycontactid", new Entity("account", id) { ["primarycontactid"] = new EntityReference("contact", Guid.NewGuid()) }));
            Assert.IsTrue(erSame.HasChanged("primarycontactid", new Entity("account", id) { ["primarycontactid"] = new EntityReference("account", contactRef.Id) }));

            // plain value equality
            var plainSame = new Entity("account", id) { ["name"] = "X" };
            Assert.IsFalse(plainSame.HasChanged("name", new Entity("account", id) { ["name"] = "X" }));
            Assert.IsTrue(plainSame.HasChanged("name", new Entity("account", id) { ["name"] = "Z" }));
        }

        // 16. ContainsValue / ContainsAny
        [TestMethod]
        public void ContainsValueAndContainsAny_CoverPresenceAndNulls()
        {
            var entity = new Entity("account") { ["a"] = 1, ["b"] = null };

            Assert.IsTrue(entity.ContainsValue("a"));
            Assert.IsFalse(entity.ContainsValue("b"));      // present but null
            Assert.IsFalse(entity.ContainsValue("c"));      // missing
            Assert.IsFalse(entity.ContainsValue("a", "b")); // one null
            Assert.IsFalse(entity.ContainsValue("a", "c")); // one missing

            Assert.IsFalse(entity.ContainsAny("c"));
            Assert.IsFalse(entity.ContainsAny("b"));        // present but null
            Assert.IsTrue(entity.ContainsAny("c", "a"));
        }

        // 17. AddOrUpdateAttribute / RemoveAttributes
        [TestMethod]
        public void AddOrUpdateAndRemoveAttributes_ModifyEntity()
        {
            var entity = new Entity("account");
            entity.AddOrUpdateAttribute("name", "First");
            Assert.AreEqual("First", entity["name"]);

            entity.AddOrUpdateAttribute("name", "Second"); // overwrite
            Assert.AreEqual("Second", entity["name"]);

            entity.AddOrUpdateAttribute("description", "D");
            entity.RemoveAttributes("name", "description");
            Assert.IsFalse(entity.Contains("name"));
            Assert.IsFalse(entity.Contains("description"));
        }

        // 18. GetTargetEntity / GetTargetEntity<T> / GetTargetEntityReference
        [TestMethod]
        public void GetTargetEntityAndReference_CoverContextInputs()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id) { ["name"] = "Acme" };
            var reference = new EntityReference("account", id);

            // No Target -> nulls
            var ctxEmpty = Substitute.For<IExecutionContext>();
            ctxEmpty.InputParameters.Returns(new ParameterCollection());
            Assert.IsNull(ctxEmpty.GetTargetEntity());
            Assert.IsNull(ctxEmpty.GetTargetEntity<Entity>());
            Assert.IsNull(ctxEmpty.GetTargetEntityReference());

            // Target is an Entity
            var ctxEntity = Substitute.For<IExecutionContext>();
            ctxEntity.InputParameters.Returns(new ParameterCollection { ["Target"] = entity });
            Assert.AreSame(entity, ctxEntity.GetTargetEntity());
            var typed = ctxEntity.GetTargetEntity<Entity>();
            Assert.IsNotNull(typed);
            Assert.AreEqual("account", typed.LogicalName);
            Assert.AreEqual(id, typed.Id);
            Assert.IsNull(ctxEntity.GetTargetEntityReference()); // not an EntityReference

            // Target is an EntityReference
            var ctxRef = Substitute.For<IExecutionContext>();
            ctxRef.InputParameters.Returns(new ParameterCollection { ["Target"] = reference });
            Assert.AreSame(reference, ctxRef.GetTargetEntityReference());
            Assert.IsNull(ctxRef.GetTargetEntity()); // not an Entity
        }

        // 19. GetPreImage / GetPostImage
        [TestMethod]
        public void GetPreAndPostImages_CoverEmptyNamedAndFallback()
        {
            var preImage = new Entity("account", Guid.NewGuid()) { ["name"] = "Pre" };
            var postImage = new Entity("account", Guid.NewGuid()) { ["name"] = "Post" };
            var other = new Entity("account", Guid.NewGuid()) { ["name"] = "Other" };

            // Null/empty image collections -> null
            var ctxNoPre = Substitute.For<IExecutionContext>();
            ctxNoPre.PreEntityImages.Returns((EntityImageCollection)null);
            Assert.IsNull(ctxNoPre.GetPreImage());

            var ctxEmptyPre = Substitute.For<IExecutionContext>();
            ctxEmptyPre.PreEntityImages.Returns(new EntityImageCollection());
            Assert.IsNull(ctxEmptyPre.GetPreImage());

            var ctxNoPost = Substitute.For<IExecutionContext>();
            ctxNoPost.PostEntityImages.Returns((EntityImageCollection)null);
            Assert.IsNull(ctxNoPost.GetPostImage());

            // Named image present
            var ctxPre = Substitute.For<IExecutionContext>();
            ctxPre.PreEntityImages.Returns(new EntityImageCollection { ["PreImage"] = preImage });
            Assert.AreSame(preImage, ctxPre.GetPreImage());
            Assert.AreSame(preImage, ctxPre.GetPreImage("PreImage"));

            // Named image missing but collection non-empty -> falls back to first value
            var ctxPreFallback = Substitute.For<IExecutionContext>();
            ctxPreFallback.PreEntityImages.Returns(new EntityImageCollection { ["Other"] = other });
            Assert.AreSame(other, ctxPreFallback.GetPreImage()); // default name lookup misses, returns first

            // Post-image named + fallback
            var ctxPost = Substitute.For<IExecutionContext>();
            ctxPost.PostEntityImages.Returns(new EntityImageCollection { ["PostImage"] = postImage });
            Assert.AreSame(postImage, ctxPost.GetPostImage());
            Assert.AreSame(postImage, ctxPost.GetPostImage("PostImage"));

            var ctxPostFallback = Substitute.For<IExecutionContext>();
            ctxPostFallback.PostEntityImages.Returns(new EntityImageCollection { ["Other"] = other });
            Assert.AreSame(other, ctxPostFallback.GetPostImage());
        }

        // 20. GetSharedVariable / SetSharedVariable + GetValue(ParameterCollection)
        [TestMethod]
        public void GetSetSharedVariableAndGetValue_CoverTypedAndMissing()
        {
            var ctx = Substitute.For<IExecutionContext>();
            ctx.SharedVariables.Returns(new ParameterCollection { ["name"] = "value", ["count"] = 7 });

            // GetSharedVariable typed / present / missing / wrong-type
            Assert.AreEqual("value", ctx.GetSharedVariable<string>("name"));
            Assert.AreEqual(7, ctx.GetSharedVariable<int>("count"));
            Assert.IsNull(ctx.GetSharedVariable<string>("missing"));
            Assert.AreEqual(0, ctx.GetSharedVariable<int>("name")); // wrong type -> default

            // SetSharedVariable then read back
            ctx.SetSharedVariable("newkey", 42);
            Assert.AreEqual(42, ctx.GetSharedVariable<int>("newkey"));

            // GetValue over ParameterCollection
            var parameters = new ParameterCollection
            {
                ["id"] = Guid.NewGuid(),
                ["text"] = "hello",
                ["wrong"] = "not-an-int"
            };
            Assert.AreEqual("hello", parameters.GetValue<string>("text"));
            Assert.AreNotEqual(Guid.Empty, parameters.GetValue<Guid>("id"));
            Assert.AreEqual(0, parameters.GetValue<int>("missing"));
            Assert.AreEqual(0, parameters.GetValue<int>("wrong")); // wrong type -> default
        }

        // 21. Compress / Decompress round-trip
        [TestMethod]
        public void CompressAndDecompress_RoundTrip()
        {
            var original = "Hello, Dataverse Label Translator!\nMulti-line\twith\ttabs.";
            var compressed = original.Compress();
            Assert.AreNotEqual(original, compressed);
            Assert.AreEqual(original, compressed.Decompress());

            Assert.AreEqual("round trip", "round trip".Compress().Decompress());
        }

        // 22. GetImage null / missing / present
        [TestMethod]
        public void GetImage_CoversNullMissingAndPresent()
        {
            var entity = new Entity("account", Guid.NewGuid());

            Assert.IsNull(((EntityImageCollection)null).GetImage("PreImage"));
            Assert.IsNull(new EntityImageCollection().GetImage("PreImage"));
            Assert.IsNull(new EntityImageCollection { ["Other"] = entity }.GetImage("PreImage"));

            var images = new EntityImageCollection { ["PreImage"] = entity };
            Assert.AreSame(entity, images.GetImage("PreImage"));
        }

        // 23. LogMessage (DEBUG-gated)
        [TestMethod]
        public void LogMessage_DoesNotThrow()
        {
            var tracing = Substitute.For<ITracingService>();
            tracing.LogMessage("hello trace");
#if DEBUG
            tracing.Received(1).Trace("hello trace");
#else
            tracing.DidNotReceive().Trace(Arg.Any<string>());
#endif
        }

}
}
