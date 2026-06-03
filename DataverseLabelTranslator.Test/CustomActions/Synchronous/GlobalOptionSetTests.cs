using DataverseLabelTranslator.Server.CustomActions.Synchronous;
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
using System.Threading;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class GlobalOptionSetTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            GlobalOptionSet.WaitAction = Thread.Sleep;
        }

        [TestMethod]
        public void Loading_ReturnsEmpty_WhenSolutionIsAllOrWhitespace()
        {
            var action = new GlobalOptionSet();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var allOutput = (LoadingGlobalOptionSetOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}");
            var blankOutput = (LoadingGlobalOptionSetOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"   \"}");

            Assert.AreEqual(0, allOutput.optionSets.Count);
            Assert.AreEqual(0, blankOutput.optionSets.Count);
            serviceAdmin.DidNotReceiveWithAnyArgs().RetrieveMultiple(default(QueryBase));
            serviceAdmin.DidNotReceiveWithAnyArgs().Execute(default(OrganizationRequest));
        }

        [TestMethod]
        public void Loading_Throws_WhenInputIsMissingOrSolutionIdIsInvalid()
        {
            var action = new GlobalOptionSet();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var missing = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, null));
            var invalid = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"not-a-guid\"}"));

            Assert.AreEqual("Missing GlobalOptionSet input.", missing.Message);
            Assert.AreEqual("GlobalOptionSet solutionId must be a GUID or all.", invalid.Message);
        }

        [TestMethod]
        public void Loading_ReturnsSortedCustomGlobalOptionSets()
        {
            var action = new GlobalOptionSet();
            var solutionId = Guid.NewGuid();
            var nullMetadataId = Guid.NewGuid();
            var noCustomizableId = Guid.NewGuid();
            var notCustomizableId = Guid.NewGuid();
            var localId = Guid.NewGuid();
            var booleanNullOptionId = Guid.NewGuid();
            var booleanId = Guid.NewGuid();
            var picklistId = Guid.NewGuid();
            QueryExpression capturedQuery = null;

            var metadataById = new Dictionary<Guid, OptionSetMetadataBase>
            {
                { nullMetadataId, null },
                { noCustomizableId, CreatePicklist(noCustomizableId, "no_customizable", null, true) },
                { notCustomizableId, CreatePicklist(notCustomizableId, "not_customizable", false, true) },
                { localId, CreatePicklist(localId, "local_options", true, false) },
                { booleanNullOptionId, CreateBoolean(null, null, true, true, null, null) },
                { booleanId, CreateBoolean(booleanId, "alpha_boolean", true, true, CreateOption(1, "Yes", "Yes desc"), CreateOption(0, "No", "No desc")) },
                { picklistId, CreatePicklist(picklistId, "beta_picklist", true, true) }
            };

            var components = new List<Entity>
            {
                new Entity("solutioncomponent"),
                new Entity("solutioncomponent") { ["objectid"] = Guid.Empty },
                new Entity("solutioncomponent") { ["objectid"] = nullMetadataId },
                new Entity("solutioncomponent") { ["objectid"] = noCustomizableId },
                new Entity("solutioncomponent") { ["objectid"] = notCustomizableId },
                new Entity("solutioncomponent") { ["objectid"] = localId },
                new Entity("solutioncomponent") { ["objectid"] = booleanNullOptionId },
                new Entity("solutioncomponent") { ["objectid"] = booleanId },
                new Entity("solutioncomponent") { ["objectid"] = picklistId }
            };

            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                capturedQuery = (QueryExpression)call[0];
                return new EntityCollection(components);
            });
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                var retrieve = request as RetrieveOptionSetRequest;
                if (retrieve != null)
                {
                    OptionSetMetadataBase metadata;
                    metadataById.TryGetValue(retrieve.MetadataId, out metadata);
                    return RetrieveResponse(metadata);
                }

                return new OrganizationResponse();
            });

            var output = (LoadingGlobalOptionSetOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + solutionId.ToString("D") + "\"}");

            Assert.AreEqual("solutioncomponent", capturedQuery.EntityName);
            Assert.AreEqual(2, capturedQuery.Criteria.Conditions.Count);
            Assert.AreEqual(3, output.optionSets.Count);
            Assert.IsNull(output.optionSets[0].Name);
            Assert.AreEqual(string.Empty, output.optionSets[0].MetadataId);
            Assert.IsTrue(output.optionSets[0].IsCustomizable.Value);
            Assert.IsNull(output.optionSets[0].TrueOption);
            Assert.IsNull(output.optionSets[0].FalseOption);
            Assert.AreEqual("alpha_boolean", output.optionSets[1].Name);
            Assert.AreEqual(1, output.optionSets[1].TrueOption.Value);
            Assert.AreEqual("Yes", output.optionSets[1].TrueOption.Label.LocalizedLabels[0].Label);
            Assert.AreEqual("beta_picklist", output.optionSets[2].Name);
            Assert.AreEqual(picklistId.ToString("D"), output.optionSets[2].MetadataId);
            Assert.AreEqual(2, output.optionSets[2].Options.Count);
            Assert.AreEqual(0, output.optionSets[2].Options[0].Label.LocalizedLabels.Count);
            Assert.AreEqual(0, output.optionSets[2].Options[0].Description.LocalizedLabels.Count);
            Assert.AreEqual(1, output.optionSets[2].Options[1].Label.LocalizedLabels.Count);
        }

        [TestMethod]
        public void Saving_ReturnsZero_WhenUpdatesAreNull()
        {
            var action = new GlobalOptionSet();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var input = new SavingGlobalOptionSetInput { component = "DisplayText" };
            var output = (SavingGlobalOptionSetOutput)action.Saving(null, serviceAdmin, null, null, "{\"optionSetDescriptionUpdates\":null,\"optionValueUpdates\":null}");

            Assert.AreEqual("DisplayText", input.component);
            Assert.AreEqual(0, output.optionSetDescriptionUpdateCount);
            Assert.AreEqual(0, output.optionValueUpdateCount);
            Assert.AreEqual(0, output.optionSetNames.Count);
            serviceAdmin.DidNotReceiveWithAnyArgs().Execute(default(OrganizationRequest));
        }

        [TestMethod]
        public void Saving_ExecutesDescriptionAndOptionValueUpdates()
        {
            var action = new GlobalOptionSet();
            var requests = new List<OrganizationRequest>();
            var metadataByName = new Dictionary<string, OptionSetMetadataBase>(StringComparer.OrdinalIgnoreCase)
            {
                { "pl_empty_labels", CreatePicklist(Guid.NewGuid(), "pl_empty_labels", true, true) },
                { "pl_status", CreatePicklist(Guid.NewGuid(), "pl_status", true, true) }
            };
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);
                var retrieve = request as RetrieveOptionSetRequest;
                if (retrieve != null)
                {
                    return RetrieveResponse(metadataByName[retrieve.Name]);
                }

                return new OrganizationResponse();
            });

            var json =
                "{"
                + "\"optionSetDescriptionUpdates\":["
                + "{\"optionSetName\":\"pl_empty_labels\",\"labels\":null},"
                + "{\"optionSetName\":\"pl_status\",\"labels\":[{\"languageCode\":1033,\"label\":\"Status desc\"},null,{\"label\":\"Skipped\"},{\"languageCode\":1036,\"label\":null}]}"
                + "],"
                + "\"optionValueUpdates\":["
                + "{\"optionSetName\":\"pl_status\",\"value\":1,\"component\":\"DisplayText\",\"labels\":[{\"languageCode\":1033,\"label\":\"Active\"}]},"
                + "{\"optionSetName\":\"PL_STATUS\",\"value\":2,\"component\":\"Description\",\"labels\":[{\"languageCode\":1033,\"label\":\"Inactive desc\"}]}"
                + "]"
                + "}";

            var output = (SavingGlobalOptionSetOutput)action.Saving(null, serviceAdmin, null, null, json);

            Assert.AreEqual(2, output.optionSetDescriptionUpdateCount);
            Assert.AreEqual(2, output.optionValueUpdateCount);
            CollectionAssert.AreEqual(new[] { "pl_empty_labels", "pl_status" }, output.optionSetNames);

            var optionSetUpdates = requests.OfType<UpdateOptionSetRequest>().ToList();
            Assert.AreEqual(2, optionSetUpdates.Count);
            Assert.IsTrue(optionSetUpdates.All(request => request.MergeLabels));
            Assert.AreEqual(0, optionSetUpdates.Single(request => request.OptionSet.Name == "pl_empty_labels").OptionSet.Description.LocalizedLabels.Count);

            var statusDescription = optionSetUpdates.Single(request => request.OptionSet.Name == "pl_status").OptionSet.Description.LocalizedLabels;
            Assert.AreEqual(2, statusDescription.Count);
            Assert.AreEqual("Status desc", statusDescription.Single(label => label.LanguageCode == 1033).Label);
            Assert.AreEqual(string.Empty, statusDescription.Single(label => label.LanguageCode == 1036).Label);

            var valueUpdates = requests.OfType<UpdateOptionValueRequest>().ToList();
            Assert.AreEqual(2, valueUpdates.Count);
            var labelUpdate = valueUpdates.Single(request => request.Value == 1);
            Assert.AreEqual("pl_status", labelUpdate.OptionSetName);
            Assert.IsTrue(labelUpdate.MergeLabels);
            Assert.AreEqual("Active", labelUpdate.Label.LocalizedLabels[0].Label);
            Assert.IsNull(labelUpdate.Description);

            var descriptionUpdate = valueUpdates.Single(request => request.Value == 2);
            Assert.AreEqual("PL_STATUS", descriptionUpdate.OptionSetName);
            Assert.IsNull(descriptionUpdate.Label);
            Assert.AreEqual("Inactive desc", descriptionUpdate.Description.LocalizedLabels[0].Label);
        }

        [TestMethod]
        public void Saving_Throws_WhenOptionSetNameOrOptionValueIsMissing()
        {
            var action = new GlobalOptionSet();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var missingName = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"optionSetDescriptionUpdates\":[{\"optionSetName\":\" \"}]}"));
            var missingValue = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"optionValueUpdates\":[{\"optionSetName\":\"pl_status\"}]}"));
            var nullDescriptionUpdate = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"optionSetDescriptionUpdates\":[null]}"));
            var nullValueUpdate = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"optionValueUpdates\":[null]}"));

            Assert.AreEqual("GlobalOptionSet optionSetName is required.", missingName.Message);
            Assert.AreEqual("GlobalOptionSet option value is required.", missingValue.Message);
            Assert.AreEqual("GlobalOptionSet optionSetName is required.", nullDescriptionUpdate.Message);
            Assert.AreEqual("GlobalOptionSet optionSetName is required.", nullValueUpdate.Message);
        }

        [TestMethod]
        public void Publishing_ExecutesPublishXmlWithDistinctValidNames()
        {
            var action = new GlobalOptionSet();
            var requests = new List<OrganizationRequest>();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);
                return new OrganizationResponse();
            });

            var output = (PublishingGlobalOptionSetOutput)action.Publishing(null, serviceAdmin, null, null, "{\"optionSetNames\":[\"pl_status\",\"PL_STATUS\",\"pl_category\"]}");

            CollectionAssert.AreEqual(new[] { "pl_status", "pl_category" }, output.optionSetNames);
            var publish = (PublishXmlRequest)requests.Single();
            Assert.AreEqual("<importexportxml><optionsets><optionset>pl_status</optionset><optionset>pl_category</optionset></optionsets></importexportxml>", publish.ParameterXml);
        }

        [TestMethod]
        public void Publishing_Waits_WhenThereAreNoOptionSetNames()
        {
            var action = new GlobalOptionSet();
            var waitMilliseconds = 0;
            GlobalOptionSet.WaitAction = milliseconds => waitMilliseconds = milliseconds;
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var output = (PublishingGlobalOptionSetOutput)action.Publishing(null, serviceAdmin, null, null, "{\"optionSetNames\":null}");

            Assert.AreEqual(0, output.optionSetNames.Count);
            Assert.AreEqual(10000, waitMilliseconds);
            serviceAdmin.DidNotReceiveWithAnyArgs().Execute(default(OrganizationRequest));
        }

        [TestMethod]
        public void Publishing_Throws_WhenOptionSetNameIsBlank()
        {
            var action = new GlobalOptionSet();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var ex = ThrowsInvalidPluginExecutionException(() => action.Publishing(null, serviceAdmin, null, null, "{\"optionSetNames\":[\"pl_status\",\"\"]}"));

            Assert.AreEqual("GlobalOptionSet optionSetName is required.", ex.Message);
        }

        [TestMethod]
        public void Published_WaitsAndReturnsDistinctNames()
        {
            var action = new GlobalOptionSet();
            var waitMilliseconds = 0;
            GlobalOptionSet.WaitAction = milliseconds => waitMilliseconds = milliseconds;

            var output = (PublishedGlobalOptionSetOutput)action.Published(null, null, null, null, "{\"optionSetNames\":[\"pl_status\",\"PL_STATUS\",\"pl_category\"]}");

            Assert.AreEqual(10000, waitMilliseconds);
            CollectionAssert.AreEqual(new[] { "pl_status", "pl_category" }, output.optionSetNames);
        }

        [TestMethod]
        public void Other_ReturnsOperationOrThrowsWhenOperationIsMissing()
        {
            var action = new GlobalOptionSet();

            var output = (OtherGlobalOptionSetOutput)action.Other(null, null, null, null, "{\"operation\":\"noop\"}");
            var ex = ThrowsInvalidPluginExecutionException(() => action.Other(null, null, null, null, "{\"operation\":\" \"}"));

            Assert.AreEqual("noop", output.operation);
            Assert.AreEqual("GlobalOptionSet Other operation is required.", ex.Message);
        }

        private static RetrieveOptionSetResponse RetrieveResponse(OptionSetMetadataBase metadata)
        {
            var response = new RetrieveOptionSetResponse();
            response.Results["OptionSetMetadata"] = metadata;
            return response;
        }

        private static InvalidPluginExecutionException ThrowsInvalidPluginExecutionException(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidPluginExecutionException ex)
            {
                return ex;
            }

            Assert.Fail("Expected InvalidPluginExecutionException.");
            return null;
        }

        private static OptionSetMetadata CreatePicklist(Guid? metadataId, string name, bool? isCustomizable, bool? isGlobal)
        {
            var metadata = new OptionSetMetadata
            {
                MetadataId = metadataId,
                Name = name,
                IsCustomizable = isCustomizable.HasValue ? new BooleanManagedProperty(isCustomizable.Value) : null,
                IsGlobal = isGlobal,
                Description = LabelWithNullLocalizedLabel("Picklist desc", 1033)
            };
            metadata.Options.Add(new OptionMetadata
            {
                Value = 10,
                Label = null,
                Description = null
            });
            metadata.Options.Add(new OptionMetadata
            {
                Value = 20,
                Label = LabelWithNullLocalizedLabel("Second", 1033),
                Description = new Label()
            });
            return metadata;
        }

        private static BooleanOptionSetMetadata CreateBoolean(Guid? metadataId, string name, bool? isCustomizable, bool? isGlobal, OptionMetadata trueOption, OptionMetadata falseOption)
        {
            return new BooleanOptionSetMetadata
            {
                MetadataId = metadataId,
                Name = name,
                IsCustomizable = isCustomizable.HasValue ? new BooleanManagedProperty(isCustomizable.Value) : null,
                IsGlobal = isGlobal,
                Description = null,
                TrueOption = trueOption,
                FalseOption = falseOption
            };
        }

        private static OptionMetadata CreateOption(int value, string label, string description)
        {
            return new OptionMetadata
            {
                Value = value,
                Label = new Label(label, 1033),
                Description = new Label(description, 1033)
            };
        }

        private static Label LabelWithNullLocalizedLabel(string label, int languageCode)
        {
            var output = new Label(label, languageCode);
            output.LocalizedLabels.Add(null);
            return output;
        }
    }
}
