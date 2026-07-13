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

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class ViewAdapterTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            ViewAdapter.WaitAction = System.Threading.Thread.Sleep;
        }

        private static IOrganizationService CreateService(params Entity[] views)
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                }

                if (query.EntityName == "savedquery")
                {
                    return AdapterTestHelpers.Entities(views);
                }

                if (query.EntityName == "solutioncomponent")
                {
                    return AdapterTestHelpers.Entities();
                }

                return AdapterTestHelpers.Entities();
            });
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest request)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel(
                        (1033, request.AttributeName + " English"),
                        (1041, request.AttributeName + " Japanese")));
                }

                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(
                        EntityMetadata("account", "Account"),
                        EntityMetadata("contact", "Contact"),
                        EntityMetadata("nolabel", null));
                }

                return new OrganizationResponse();
            });
            return service;
        }

        private static Entity View(Guid id, string name, string returnedTypeCode, int queryType)
        {
            var view = new Entity("savedquery", id);
            view["savedqueryid"] = id;
            if (name != null)
            {
                view["name"] = name;
            }

            if (returnedTypeCode != null)
            {
                view["returnedtypecode"] = returnedTypeCode;
            }

            view["querytype"] = queryType;
            return view;
        }

        private static EntityMetadata EntityMetadata(string logicalName, string displayName, Guid? metadataId = null, bool setMetadataId = true)
        {
            var metadata = new EntityMetadata
            {
                LogicalName = logicalName,
                DisplayName = displayName == null ? null : AdapterTestHelpers.BuildLabel((1033, displayName))
            };
            if (setMetadataId)
            {
                typeof(EntityMetadata)
                    .GetProperty("MetadataId", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(metadata, metadataId ?? Guid.NewGuid());
            }

            return metadata;
        }

        private static EasyTranslatorChangedRowInput ChangedRow(string gridKey, params (string lcid, string value)[] changes)
        {
            return new EasyTranslatorChangedRowInput
            {
                gridKey = gridKey,
                changes = changes.ToDictionary(x => x.lcid, x => x.value)
            };
        }

        [TestMethod]
        public void Load_AllViews_GroupsByEntityAndUsesDescriptionComponent()
        {
            var accountViewId = Guid.NewGuid();
            var contactViewId = Guid.NewGuid();
            var noLabelViewId = Guid.NewGuid();
            var emptyIdView = View(Guid.Empty, "Ignored", "account", 0);
            var missingEntityView = View(Guid.NewGuid(), "Ignored Missing Entity", "", 0);
            var adapter = new ViewAdapter();
            var service = CreateService(
                View(accountViewId, "Account Active", "account", 0),
                View(Guid.NewGuid(), null, "account", 64),
                View(noLabelViewId, "No Label View", "nolabel", 0),
                View(Guid.NewGuid(), null, null, 0),
                View(contactViewId, "", "contact", 999999),
                emptyIdView,
                missingEntityView);

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = null,
                component = "Description",
                solutionId = "all"
            });

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual("Views", output.grid.title);
            Assert.AreEqual(3, output.grid.rows.Count);
            Assert.AreEqual("Account (account)", output.grid.rows[0].SchemaName);

            var accountChildren = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual(2, accountChildren.Count);
            var accountChild = accountChildren.First(row => row.Recid == "view:" + accountViewId.ToString("D"));
            Assert.AreEqual($"views|{accountViewId:D}|description|account", accountChild.GridKey);
            Assert.AreEqual("description English", accountChild["1033"]);

            var contactChildren = (List<EasyTranslatorGridRowOutput>)output.grid.rows[1]["children"];
            Assert.AreEqual("Type 999999", contactChildren[0].SchemaName);
            Assert.AreEqual("nolabel", output.grid.rows[2].SchemaName);
        }

        [TestMethod]
        public void Load_AllViews_WithSolutionScope_FiltersToSolutionEntities()
        {
            var solutionId = Guid.NewGuid();
            var accountMetadataId = Guid.NewGuid();
            var contactMetadataId = Guid.NewGuid();
            var accountViewId = Guid.NewGuid();
            var contactViewId = Guid.NewGuid();
            var service = CreateService(
                View(accountViewId, "Account View", "account", 0),
                View(contactViewId, "Contact View", "contact", 0));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest request)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel((1033, request.AttributeName)));
                }

                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(
                        EntityMetadata("account", "Account", accountMetadataId),
                        EntityMetadata("contact", "Contact", contactMetadataId),
                        EntityMetadata("", "No Logical Name", Guid.NewGuid()),
                        EntityMetadata("nolabel", null, Guid.NewGuid()),
                        EntityMetadata("missingmetadataid", "Missing Metadata Id", null, false));
                }

                return new OrganizationResponse();
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (query.EntityName == "savedquery") return AdapterTestHelpers.Entities(
                    View(accountViewId, "Account View", "account", 0),
                    View(contactViewId, "Contact View", "contact", 0));
                if (query.EntityName == "solutioncomponent")
                {
                    var accountComponent = new Entity("solutioncomponent");
                    accountComponent["objectid"] = accountMetadataId;
                    var unknownComponent = new Entity("solutioncomponent");
                    unknownComponent["objectid"] = Guid.NewGuid();
                    return AdapterTestHelpers.Entities(accountComponent, unknownComponent);
                }

                return AdapterTestHelpers.Entities();
            });

            var output = new ViewAdapter().Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "none",
                component = "DisplayText",
                solutionId = solutionId.ToString("D")
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("views:entity:account", output.grid.rows[0].Recid);
        }

        [TestMethod]
        public void Load_AllViews_EmptyResult_ReturnsEmptyTree()
        {
            var output = new ViewAdapter().Load(
                AdapterTestHelpers.Context(CreateService()),
                new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });

            Assert.AreEqual("tree", output.grid.mode);
            Assert.AreEqual("Views", output.grid.title);
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_ForEntity_ReturnsSortedFlatRows()
        {
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var service = CreateService(
                View(secondId, "Zulu", "account", 0),
                View(Guid.Empty, "Ignored", "account", 0),
                View(firstId, null, "account", 64));

            var output = new ViewAdapter().Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual("flat", output.grid.mode);
            Assert.AreEqual(2, output.grid.rows.Count);
            Assert.AreEqual("Lookup", output.grid.rows[0].SchemaName);
            Assert.AreEqual("Zulu (Public)", output.grid.rows[1].SchemaName);
            Assert.AreEqual($"views|{firstId:D}|name|account", output.grid.rows[0].GridKey);
        }

        [TestMethod]
        public void Load_NullRetrieveLocLabelsResponse_ReturnsEmptyLabel()
        {
            var viewId = Guid.NewGuid();
            var service = CreateService(View(viewId, "Account View", "account", 0));
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return null;
                }

                if (call[0] is RetrieveAllEntitiesRequest)
                {
                    return AdapterTestHelpers.BuildRetrieveAllEntitiesResponse(EntityMetadata("account", ""));
                }

                return new OrganizationResponse();
            });

            var output = new ViewAdapter().Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput
            {
                entityName = "account",
                component = "DisplayText",
                solutionId = "all"
            });

            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.IsFalse(output.grid.rows[0].ContainsKey("1033"));
        }

        [TestMethod]
        public void Save_NameAndDescription_MergesLabelsAndAddsPublishTargets()
        {
            var nameId = Guid.NewGuid();
            var descriptionId = Guid.NewGuid();
            OrganizationRequest lastSetRequest = null;
            var service = CreateService();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is RetrieveLocLabelsRequest)
                {
                    return AdapterTestHelpers.RetrieveLabelsResponse(AdapterTestHelpers.BuildLabel(
                        (1033, "Old English"),
                        (1041, "Old Japanese"),
                        (1033, "Duplicate English")));
                }

                if (call[0] is SetLocLabelsRequest)
                {
                    lastSetRequest = (OrganizationRequest)call[0];
                    return new OrganizationResponse();
                }

                return new OrganizationResponse();
            });

            var output = new ViewAdapter().Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    ChangedRow($"views|{nameId:D}|name|account", ("1033", "New English"), ("bad", "Ignored")),
                    ChangedRow($"views|{descriptionId:D}|description|contact", ("1041", null), ("1066", "New Language")),
                    ChangedRow($"views|{Guid.NewGuid():D}|name|account")
                }
            });

            Assert.AreEqual(2, output.changedRowCount);
            Assert.AreEqual(2, output.publishTargets.Count);
            Assert.IsTrue(output.publishTargets.Exists(x => x.kind == "entity" && x.id == "account"));
            Assert.IsTrue(output.publishTargets.Exists(x => x.kind == "entity" && x.id == "contact"));
            Assert.IsInstanceOfType(lastSetRequest, typeof(SetLocLabelsRequest));
            var labels = ((SetLocLabelsRequest)lastSetRequest).Labels;
            Assert.IsTrue(labels.Any(x => x.LanguageCode == 1041 && x.Label == string.Empty));
            Assert.IsTrue(labels.Any(x => x.LanguageCode == 1066 && x.Label == "New Language"));
        }

        [TestMethod]
        public void Save_EmptyAndNoneEntityChanges_DoNotPublish()
        {
            var service = CreateService();
            var id = Guid.NewGuid();

            var output = new ViewAdapter().Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "none",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    ChangedRow($"views|{Guid.NewGuid():D}|name|account", ("not-a-number", "Ignored")),
                    ChangedRow($"views|{id:D}|name|none", ("1033", "New"))
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Save_NullChangedRowsAndBlankPublishEntity_DoNotPublish()
        {
            var adapter = new ViewAdapter();
            var service = CreateService();

            var nullRowsOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = null
            });
            var blankPublishOutput = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                entityName = "",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    ChangedRow($"views|{Guid.NewGuid():D}|name|", ("1033", "New"))
                }
            });

            Assert.AreEqual(0, nullRowsOutput.changedRowCount);
            Assert.AreEqual(1, blankPublishOutput.changedRowCount);
            Assert.AreEqual(0, blankPublishOutput.publishTargets.Count);
        }

        [TestMethod]
        public void Save_UsesInputEntityWhenGridKeyEntityIsEmpty()
        {
            var id = Guid.NewGuid();
            var output = new ViewAdapter().Save(AdapterTestHelpers.Context(CreateService()), new EasyTranslatorSaveInput
            {
                entityName = "account",
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    ChangedRow($"views|{id:D}|name|", ("1033", "New"))
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.AreEqual("account", output.publishTargets[0].id);
        }

        [TestMethod]
        public void Save_InvalidGridKeyGuidAndAttribute_Throw()
        {
            var adapter = new ViewAdapter();
            var service = CreateService();

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        ChangedRow("views|not-a-guid|name|account", ("1033", "New"))
                    }
                }), "GUID");

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        ChangedRow($"views|{Guid.NewGuid():D}|bad|account", ("1033", "New"))
                    }
                }), "attributeName");
        }

        [TestMethod]
        public void PublishAndPublished_HandleEntityTargets()
        {
            var service = Substitute.For<IOrganizationService>();
            OrganizationRequest captured = null;
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                captured = (OrganizationRequest)call[0];
                return new OrganizationResponse();
            });
            var waited = 0;
            ViewAdapter.WaitAction = milliseconds => waited = milliseconds;

            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    new EasyTranslatorPublishTarget { kind = "other", id = "ignored" }
                }
            };

            var publishOutput = new ViewAdapter().Publish(AdapterTestHelpers.Context(service), input);
            var publishedOutput = new ViewAdapter().Published(AdapterTestHelpers.Context(service), input);
            var emptyPublish = new ViewAdapter().Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());

            Assert.IsInstanceOfType(captured, typeof(PublishXmlRequest));
            Assert.IsTrue(((PublishXmlRequest)captured).ParameterXml.Contains("<entity>account</entity>"));
            Assert.AreEqual(1, publishOutput.publishTargets.Count);
            Assert.AreEqual(10000, waited);
            Assert.AreEqual(1, publishedOutput.publishTargets.Count);
            Assert.AreEqual(0, emptyPublish.publishTargets.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverViewAdapterEdges()
        {
            var adapterType = typeof(ViewAdapter);
            Assert.AreEqual("Public", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetViewTypeName", 0));
            Assert.AreEqual("Type 12345", AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetViewTypeName", 12345));

            var validGuid = Guid.NewGuid();
            Assert.AreEqual(validGuid, AdapterTestHelpers.InvokeStatic<Guid>(adapterType, "ValidateGuid", validGuid.ToString("D"), "id"));

            AdapterTestHelpers.ExpectException<TargetInvocationException>(() =>
                AdapterTestHelpers.InvokeStatic(adapterType, "ValidateGuid", "", "id"));

            var emptyLabel = AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetBaseLanguageLabel", null, 1033);
            Assert.AreEqual(string.Empty, emptyLabel);

            var nonMatchingLabel = AdapterTestHelpers.BuildLabel((1041, "Japanese"), (1033, ""));
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetBaseLanguageLabel", nonMatchingLabel, 1033));

            var labels = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel",
                null,
                new List<EasyTranslatorLabelChange>
                {
                    new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "English" }
                });
            Assert.AreEqual(1, labels.Length);
            Assert.AreEqual("English", labels[0].Label);

            var current = AdapterTestHelpers.BuildLabel((1033, null), (1033, "Duplicate"), (1041, "Japanese"));
            current.LocalizedLabels.Add(null);
            var merged = AdapterTestHelpers.InvokeStatic<LocalizedLabel[]>(adapterType, "BuildMergedLabel",
                current,
                new List<EasyTranslatorLabelChange>
                {
                    new EasyTranslatorLabelChange { LanguageCode = 1033, Label = "New" },
                    new EasyTranslatorLabelChange { LanguageCode = 1066, Label = null }
                });
            Assert.AreEqual(3, merged.Length);
            Assert.AreEqual("New", merged[0].Label);
            Assert.AreEqual(string.Empty, merged[2].Label);

            var xml = AdapterTestHelpers.InvokeStatic<string>(adapterType, "BuildEntityPublishXml", new List<string> { "account", "contact" });
            Assert.IsTrue(xml.Contains("<entity>account</entity>"));
            Assert.IsTrue(xml.Contains("<entity>contact</entity>"));
        }
    }
}
