using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class DashboardTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            Dashboard.WaitAction = Thread.Sleep;
        }

        [TestMethod]
        public void Loading_ReturnsSortedParentDashboardRows()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var alphaId = Guid.NewGuid();
            var betaId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();
            var notCustomizableId = Guid.NewGuid();
            var chartId = Guid.NewGuid();
            QueryExpression capturedDashboardQuery = null;

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return Entities(new Entity("organization") { ["languagecode"] = 1033 });
                }

                if (query.EntityName == "systemform")
                {
                    capturedDashboardQuery = query;
                    return Entities(
                        CreateDashboard(betaId, "Beta fallback", 0, 1, true),
                        CreateDashboard(inactiveId, "Inactive", 0, 0, true),
                        CreateDashboard(notCustomizableId, "Not custom", 0, 1, false),
                        CreateDashboard(chartId, "Chart", 1, 1, true),
                        CreateDashboard(alphaId, "Alpha fallback", 10, 1, true));
                }

                return Entities();
            });
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                var retrieve = request as RetrieveLocLabelsRequest;
                if (retrieve != null && retrieve.EntityMoniker.Id == alphaId)
                {
                    return RetrieveLabelsResponse(new Label("Alpha", 1033));
                }

                if (retrieve != null && retrieve.EntityMoniker.Id == betaId)
                {
                    return RetrieveLabelsResponse(LabelWithNullLocalizedLabel("Beta", 1033));
                }

                return RetrieveLabelsResponse(new Label());
            });

            var output = (LoadingDashboardOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}");

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual("systemform", capturedDashboardQuery.EntityName);
            Assert.AreEqual(2, capturedDashboardQuery.Criteria.Conditions.Count);
            Assert.AreEqual(1, capturedDashboardQuery.Criteria.Filters.Count);
            Assert.AreEqual(2, output.dashboards.Count);
            Assert.AreEqual(alphaId.ToString("D"), output.dashboards[0].formid);
            Assert.AreEqual("Alpha fallback", output.dashboards[0].name);
            Assert.AreEqual(10, output.dashboards[0].type);
            Assert.AreEqual("none", output.dashboards[0].objecttypecode);
            Assert.AreEqual("Alpha", output.dashboards[0].Label.LocalizedLabels[0].Label);
            Assert.AreEqual(betaId.ToString("D"), output.dashboards[1].formid);
            Assert.AreEqual(1, output.dashboards[1].Label.LocalizedLabels.Count);
        }

        [TestMethod]
        public void Loading_UsesSolutionMembershipAndValidatesInput()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var solutionId = Guid.NewGuid();
            var dashboardId = Guid.NewGuid();
            QueryExpression capturedComponentQuery = null;
            QueryExpression capturedDashboardQuery = null;

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return Entities(new Entity("organization") { ["languagecode"] = 1033 });
                }

                if (query.EntityName == "solutioncomponent")
                {
                    capturedComponentQuery = query;
                    return Entities(
                        new Entity("solutioncomponent"),
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty },
                        new Entity("solutioncomponent") { ["objectid"] = dashboardId });
                }

                if (query.EntityName == "systemform")
                {
                    capturedDashboardQuery = query;
                    return Entities(CreateDashboard(dashboardId, "Dashboard", 0, 1, true));
                }

                return Entities();
            });
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(RetrieveLabelsResponse(new Label("Dashboard", 1033)));

            var output = (LoadingDashboardOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + solutionId.ToString("D") + "\"}");

            Assert.AreEqual("solutioncomponent", capturedComponentQuery.EntityName);
            Assert.AreEqual(2, capturedComponentQuery.Criteria.Conditions.Count);
            Assert.AreEqual("systemform", capturedDashboardQuery.EntityName);
            Assert.IsTrue(capturedDashboardQuery.Criteria.Conditions.Any(condition => condition.AttributeName == "formid"));
            Assert.AreEqual(1, output.dashboards.Count);

            var invalidSolution = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"not-a-guid\"}"));
            Assert.AreEqual("Dashboard solutionId must be a GUID or all.", invalidSolution.Message);

            var missingInput = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, null));
            Assert.AreEqual("Missing Dashboard input.", missingInput.Message);

            var noOrgService = Substitute.For<IOrganizationService>();
            noOrgService.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(Entities());
            var noOrganization = ThrowsInvalidPluginExecutionException(() => action.Loading(null, noOrgService, null, null, "{\"solutionId\":\"all\"}"));
            Assert.AreEqual("Could not retrieve organization base language.", noOrganization.Message);
        }

        [TestMethod]
        public void Loading_ReturnsEmpty_WhenSolutionHasNoDashboards()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return Entities(new Entity("organization") { ["languagecode"] = 1033 });
                }

                return Entities();
            });

            var output = (LoadingDashboardOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + Guid.NewGuid().ToString("D") + "\"}");

            Assert.AreEqual(0, output.dashboards.Count);
            serviceAdmin.DidNotReceiveWithAnyArgs().Execute(default(OrganizationRequest));
        }

        [TestMethod]
        public void Saving_MergesLabelsAndReturnsDistinctIds()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var dashboardId = Guid.NewGuid();
            var otherDashboardId = Guid.NewGuid();
            var requests = new List<OrganizationRequest>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);

                if (request is RetrieveLocLabelsRequest)
                {
                    var existing = new Label("Old", 1033);
                    existing.LocalizedLabels.Add(new LocalizedLabel("Spanish", 3082));
                    existing.LocalizedLabels.Add(null);
                    return RetrieveLabelsResponse(existing);
                }

                return new OrganizationResponse();
            });

            var json =
                "{"
                + "\"dashboardUpdates\":["
                + "{\"dashboardId\":\"" + dashboardId.ToString("D") + "\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Updated\"},{\"LanguageCode\":\"1041\",\"Label\":\"\"},{\"LanguageCode\":\"1066\",\"Label\":null},null,{\"Label\":\"Skipped\"}]},"
                + "{\"dashboardId\":\"" + dashboardId.ToString("D") + "\",\"labels\":[{\"LanguageCode\":\"1036\",\"Label\":\"French\"}]},"
                + "{\"dashboardId\":\"" + otherDashboardId.ToString("D") + "\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Other\"}]}"
                + "]"
                + "}";

            var output = (SavingDashboardOutput)action.Saving(null, serviceAdmin, null, null, json);

            CollectionAssert.AreEqual(new[] { dashboardId.ToString("D"), otherDashboardId.ToString("D") }, output.dashboardIds);
            var setRequests = requests.OfType<SetLocLabelsRequest>().ToList();
            Assert.AreEqual(3, setRequests.Count);
            Assert.AreEqual("systemform", setRequests[0].EntityMoniker.LogicalName);
            Assert.AreEqual(dashboardId, setRequests[0].EntityMoniker.Id);
            Assert.AreEqual("name", setRequests[0].AttributeName);
            Assert.AreEqual("Updated", setRequests[0].Labels.Single(label => label.LanguageCode == 1033).Label);
            Assert.AreEqual("Spanish", setRequests[0].Labels.Single(label => label.LanguageCode == 3082).Label);
            Assert.AreEqual(string.Empty, setRequests[0].Labels.Single(label => label.LanguageCode == 1041).Label);
            Assert.AreEqual(string.Empty, setRequests[0].Labels.Single(label => label.LanguageCode == 1066).Label);
            Assert.AreEqual("French", setRequests[1].Labels.Single(label => label.LanguageCode == 1036).Label);
        }

        [TestMethod]
        public void Saving_HandlesEmptyInputAndThrowsForInvalidDashboardId()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var output = (SavingDashboardOutput)action.Saving(null, serviceAdmin, null, null, "{\"dashboardUpdates\":null}");
            Assert.AreEqual(0, output.dashboardIds.Count);
            serviceAdmin.DidNotReceiveWithAnyArgs().Execute(default(OrganizationRequest));

            output = (SavingDashboardOutput)action.Saving(null, serviceAdmin, null, null, "{\"dashboardUpdates\":[null,{\"dashboardId\":\"not-a-guid\",\"labels\":null}]}");
            Assert.AreEqual(0, output.dashboardIds.Count);

            var invalid = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"dashboardUpdates\":[{\"dashboardId\":\"not-a-guid\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"X\"}]}]}"));
            Assert.AreEqual("Dashboard dashboardId must be a GUID.", invalid.Message);
        }

        [TestMethod]
        public void PublishingAndPublished_FilterIdsPublishAndWait()
        {
            var action = new Dashboard();
            var valid = Guid.NewGuid().ToString("D");
            var duplicate = valid.ToUpperInvariant();
            var requests = new List<OrganizationRequest>();
            var waits = new List<int>();
            Dashboard.WaitAction = milliseconds => waits.Add(milliseconds);
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);
                return new OrganizationResponse();
            });

            var publishingOutput = (PublishingDashboardOutput)action.Publishing(null, serviceAdmin, null, null, "{\"dashboardIds\":[\"" + valid + "\",\"" + duplicate + "\",\" \",\"not-a-guid\"]}");
            var publish = (PublishXmlRequest)requests.Single();

            CollectionAssert.AreEqual(new[] { valid }, publishingOutput.dashboardIds);
            Assert.AreEqual("<importexportxml><dashboards><dashboard>{" + valid + "}</dashboard></dashboards></importexportxml>", publish.ParameterXml);

            var emptyPublishingOutput = (PublishingDashboardOutput)action.Publishing(null, serviceAdmin, null, null, "{\"dashboardIds\":null}");
            Assert.AreEqual(0, emptyPublishingOutput.dashboardIds.Count);
            Assert.AreEqual(10000, waits.Single());

            var publishedOutput = (PublishedDashboardOutput)action.Published(null, serviceAdmin, null, null, "{\"dashboardIds\":[\"" + valid + "\",\"not-a-guid\"]}");
            CollectionAssert.AreEqual(new[] { valid }, publishedOutput.dashboardIds);
            Assert.AreEqual(2, waits.Count);
            Assert.AreEqual(10000, waits[1]);
        }

        [TestMethod]
        public void Other_ReturnsOperationOrThrowsWhenMissing()
        {
            var action = new Dashboard();

            var output = (OtherDashboardOutput)action.Other(null, null, null, null, "{\"operation\":\"noop\"}");
            var ex = ThrowsInvalidPluginExecutionException(() => action.Other(null, null, null, null, "{\"operation\":\" \"}"));

            Assert.AreEqual("noop", output.operation);
            Assert.AreEqual("Dashboard Other operation is required.", ex.Message);
        }

        [TestMethod]
        public void Loading_Throws_WhenBaseLanguageIsNotPositive()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(Entities(new Entity("organization") { ["languagecode"] = 0 }));

            var ex = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}"));

            Assert.AreEqual("Could not retrieve organization base language.", ex.Message);
        }

        [TestMethod]
        public void Saving_Throws_WhenDashboardIdIsBlank()
        {
            var action = new Dashboard();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var ex = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"dashboardUpdates\":[{\"dashboardId\":\" \",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"X\"}]}]}"));
            var missing = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"dashboardUpdates\":[{\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"X\"}]}]}"));

            Assert.AreEqual("Dashboard dashboardId must be a GUID.", ex.Message);
            Assert.AreEqual("Dashboard dashboardId must be a GUID.", missing.Message);
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var intValue = new Entity("systemform") { ["type"] = 10 };
            var unsupportedValue = new Entity("systemform") { ["type"] = "interactive" };
            var nullValue = new Entity("systemform") { ["type"] = null };
            var boolValue = new Entity("systemform") { ["iscustomizable"] = true };
            var unsupportedBool = new Entity("systemform") { ["iscustomizable"] = "yes" };
            var inactive = new Entity("systemform") { ["formactivationstate"] = 0 };
            var notCustomizable = new Entity("systemform") { ["iscustomizable"] = false };
            var unsupportedTypeCandidate = new Entity("systemform") { ["type"] = "dashboard" };

            Assert.IsNull(InvokePrivate("ParseSolutionId", "ALL"));
            Assert.IsNull(InvokePrivate("ParseSolutionId", " "));
            Assert.AreEqual(0, ((List<Entity>)InvokePrivate("ToList", new object[] { null })).Count);
            Assert.IsTrue((bool)InvokePrivate("IsDashboardCandidate", new Entity("systemform")));
            Assert.IsTrue((bool)InvokePrivate("IsDashboardCandidate", intValue));
            Assert.IsTrue((bool)InvokePrivate("IsDashboardCandidate", unsupportedTypeCandidate));
            Assert.IsFalse((bool)InvokePrivate("IsDashboardCandidate", inactive));
            Assert.IsFalse((bool)InvokePrivate("IsDashboardCandidate", notCustomizable));
            Assert.IsNull(InvokePrivate("GetOptionValue", null, "type"));
            Assert.IsNull(InvokePrivate("GetOptionValue", new Entity("systemform"), "type"));
            Assert.IsNull(InvokePrivate("GetOptionValue", nullValue, "type"));
            Assert.AreEqual(10, InvokePrivate("GetOptionValue", intValue, "type"));
            Assert.IsNull(InvokePrivate("GetOptionValue", unsupportedValue, "type"));
            Assert.IsNull(InvokePrivate("GetManagedBooleanValue", null, "iscustomizable"));
            Assert.IsNull(InvokePrivate("GetManagedBooleanValue", new Entity("systemform"), "iscustomizable"));
            Assert.IsNull(InvokePrivate("GetManagedBooleanValue", new Entity("systemform") { ["iscustomizable"] = null }, "iscustomizable"));
            Assert.AreEqual(true, InvokePrivate("GetManagedBooleanValue", boolValue, "iscustomizable"));
            Assert.IsNull(InvokePrivate("GetManagedBooleanValue", unsupportedBool, "iscustomizable"));
            Assert.AreEqual(0, ((DashboardLabelOutput)InvokePrivate("BuildLabelOutput", new object[] { null })).LocalizedLabels.Count);
            Assert.AreEqual(string.Empty, InvokePrivate("GetBaseLabel", null, 1033));
            Assert.AreEqual("fallback name", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput { name = "fallback name" }, 1033));
            Assert.AreEqual("fallback id", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput { formid = "fallback id" }, 1033));
            Assert.AreEqual("no labels", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput
            {
                name = "no labels",
                Label = new DashboardLabelOutput { LocalizedLabels = null }
            }, 1033));
            Assert.AreEqual("empty labels", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput
            {
                name = "empty labels",
                Label = new DashboardLabelOutput()
            }, 1033));
            Assert.AreEqual("nonbase fallback", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput
            {
                name = "nonbase fallback",
                Label = new DashboardLabelOutput
                {
                    LocalizedLabels = new List<DashboardLocalizedLabelOutput>
                    {
                        new DashboardLocalizedLabelOutput { LanguageCode = 1041, Label = "nonbase" }
                    }
                }
            }, 1033));
            Assert.AreEqual("base", InvokePrivate("GetBaseLabel", new DashboardMetadataOutput
            {
                name = "fallback",
                Label = new DashboardLabelOutput
                {
                    LocalizedLabels = new List<DashboardLocalizedLabelOutput>
                    {
                        new DashboardLocalizedLabelOutput { LanguageCode = 1033, Label = string.Empty },
                        new DashboardLocalizedLabelOutput { LanguageCode = 1033, Label = "base" }
                    }
                }
            }, 1033));

            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns((OrganizationResponse)null);
            var retrievedLabel = (Label)InvokePrivate("RetrieveDashboardLabel", serviceAdmin, Guid.NewGuid());
            Assert.AreEqual(0, retrievedLabel.LocalizedLabels.Count);

            var labelChangeType = typeof(Dashboard).GetNestedType("DashboardLabelChange", BindingFlags.NonPublic);
            var changesType = typeof(List<>).MakeGenericType(labelChangeType);
            var changes = (System.Collections.IList)Activator.CreateInstance(changesType);
            var change = Activator.CreateInstance(labelChangeType);
            labelChangeType.GetProperty("LanguageCode").SetValue(change, 1033);
            labelChangeType.GetProperty("Label").SetValue(change, null);
            changes.Add(change);
            var merged = (LocalizedLabel[])InvokePrivate("BuildMergedLabel", null, changes);
            Assert.AreEqual(string.Empty, merged.Single().Label);

            var duplicateChanges = (System.Collections.IList)Activator.CreateInstance(changesType);
            var firstChange = Activator.CreateInstance(labelChangeType);
            labelChangeType.GetProperty("LanguageCode").SetValue(firstChange, 1041);
            labelChangeType.GetProperty("Label").SetValue(firstChange, "first");
            duplicateChanges.Add(firstChange);
            var secondChange = Activator.CreateInstance(labelChangeType);
            labelChangeType.GetProperty("LanguageCode").SetValue(secondChange, 1041);
            labelChangeType.GetProperty("Label").SetValue(secondChange, "second");
            duplicateChanges.Add(secondChange);
            var current = new Label();
            current.LocalizedLabels.Add(new LocalizedLabel(null, 1033));
            current.LocalizedLabels.Add(new LocalizedLabel("duplicate", 1033));
            var mergedDuplicates = (LocalizedLabel[])InvokePrivate("BuildMergedLabel", current, duplicateChanges);
            Assert.AreEqual(string.Empty, mergedDuplicates.Single(label => label.LanguageCode == 1033).Label);
            Assert.AreEqual("second", mergedDuplicates.Single(label => label.LanguageCode == 1041).Label);
        }

        private static EntityCollection Entities(params Entity[] entities)
        {
            return new EntityCollection(entities.ToList());
        }

        private static Entity CreateDashboard(Guid id, string name, int type, int activeState, bool customizable)
        {
            var entity = new Entity("systemform", id);
            entity["formid"] = id;
            entity["name"] = name;
            entity["type"] = new OptionSetValue(type);
            entity["objecttypecode"] = "none";
            entity["formactivationstate"] = new OptionSetValue(activeState);
            entity["iscustomizable"] = new BooleanManagedProperty(customizable);
            return entity;
        }

        private static RetrieveLocLabelsResponse RetrieveLabelsResponse(Label label)
        {
            var response = new RetrieveLocLabelsResponse();
            response.Results["Label"] = label;
            return response;
        }

        private static Label LabelWithNullLocalizedLabel(string label, int languageCode)
        {
            var output = new Label(label, languageCode);
            output.LocalizedLabels.Add(null);
            return output;
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

        private static object InvokePrivate(string name, params object[] args)
        {
            var method = typeof(Dashboard).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Private method not found: " + name);
            return method.Invoke(null, args);
        }
    }
}
