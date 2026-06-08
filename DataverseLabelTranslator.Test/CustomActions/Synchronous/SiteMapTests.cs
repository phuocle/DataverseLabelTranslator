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
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class SiteMapTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            SiteMap.WaitAction = Thread.Sleep;
        }

        [TestMethod]
        public void Loading_ReturnsSitemapMetadataAndEntityLabels()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var sitemapId = Guid.NewGuid();
            var sitemapXml =
                "<sitemap><Area Id=\"A\"><Group Id=\"G\"><SubArea Id=\"S\" Entity=\"account\" /><SubArea Id=\"E\" Entity=\"empty\" /><SubArea Id=\"B\" Entity=\"broken\" /></Group></Area></sitemap>";

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "sitemap")
                {
                    return Entities(new Entity("sitemap", sitemapId)
                    {
                        ["sitemapid"] = sitemapId,
                        ["sitemapname"] = "Primary sitemap",
                        ["sitemapxml"] = sitemapXml
                    });
                }

                return Entities();
            });
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var retrieve = call[0] as RetrieveEntityRequest;
                if (retrieve?.LogicalName == "account")
                {
                    return RetrieveEntityResponse(new Label("Account", 1033));
                }

                if (retrieve?.LogicalName == "empty")
                {
                    return RetrieveEntityResponse(null);
                }

                throw new InvalidOperationException("Metadata missing.");
            });

            var output = (LoadingSiteMapOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}");

            Assert.AreEqual(1, output.sitemaps.Count);
            Assert.AreEqual(sitemapId.ToString("D"), output.sitemaps[0].sitemapid);
            Assert.AreEqual("Primary sitemap", output.sitemaps[0].sitemapname);
            Assert.AreEqual(sitemapXml, output.sitemaps[0].sitemapxml);
            Assert.AreEqual(1, output.sitemaps[0].entityLabels.Count);
            Assert.AreEqual("account", output.sitemaps[0].entityLabels[0].entityName);
            Assert.AreEqual("Account", output.sitemaps[0].entityLabels[0].labels[0].Label);
        }

        [TestMethod]
        public void Loading_UsesSolutionMembershipAndValidatesInput()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var solutionId = Guid.NewGuid();
            var sitemapId = Guid.NewGuid();
            QueryExpression componentQuery = null;
            QueryExpression sitemapQuery = null;

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "solutioncomponent")
                {
                    componentQuery = query;
                    return Entities(
                        new Entity("solutioncomponent"),
                        new Entity("solutioncomponent") { ["objectid"] = Guid.Empty },
                        new Entity("solutioncomponent") { ["objectid"] = sitemapId });
                }

                if (query.EntityName == "sitemap")
                {
                    sitemapQuery = query;
                    return Entities(new Entity("sitemap", sitemapId)
                    {
                        ["sitemapid"] = sitemapId,
                        ["sitemapxml"] = "<sitemap />"
                    });
                }

                return Entities();
            });

            var output = (LoadingSiteMapOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + solutionId.ToString("D") + "\"}");
            var invalid = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"bad\"}"));
            var missing = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, null));

            Assert.AreEqual("solutioncomponent", componentQuery.EntityName);
            Assert.AreEqual(2, componentQuery.Criteria.Conditions.Count);
            Assert.AreEqual("sitemap", sitemapQuery.EntityName);
            Assert.AreEqual(1, output.sitemaps.Count);
            Assert.AreEqual("SiteMap solutionId must be a GUID or all.", invalid.Message);
            Assert.AreEqual("Missing SiteMap input.", missing.Message);
        }

        [TestMethod]
        public void Loading_ReturnsEmpty_WhenSolutionHasNoSitemaps()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(Entities());

            var output = (LoadingSiteMapOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + Guid.NewGuid().ToString("D") + "\"}");

            Assert.AreEqual(0, output.sitemaps.Count);
            serviceAdmin.Received(1).RetrieveMultiple(Arg.Is<QueryExpression>(query => query.EntityName == "solutioncomponent"));
            serviceAdmin.DidNotReceive().RetrieveMultiple(Arg.Is<QueryExpression>(query => query.EntityName == "sitemap"));
        }

        [TestMethod]
        public void Saving_UpdatesChangedNodesOnlyAndDeduplicatesIds()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var sitemapId = Guid.NewGuid();

            serviceAdmin.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(call =>
            {
                var id = (Guid)call[1];
                if (id == Guid.Empty)
                {
                    return null;
                }

                return new Entity("sitemap", id)
                {
                    ["sitemapxml"] = "<sitemap><Area Id=\"A\"><Titles><Title LCID=\"1033\" Title=\"Area\" /></Titles><Group Id=\"G\"><SubArea Id=\"S\" /></Group></Area></sitemap>"
                };
            });

            var json = "{\"sitemapUpdates\":[" +
                "null," +
                "{\"sitemapId\":\"00000000-0000-0000-0000-000000000000\",\"compositeId\":\"A\",\"nodeType\":\"Area\",\"component\":\"DisplayText\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Ignored\"}]}," +
                "{\"sitemapId\":\"" + sitemapId.ToString("D") + "\",\"compositeId\":\"A\",\"nodeType\":\"Area\",\"component\":\"DisplayText\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Updated Area\"}]}," +
                "{\"sitemapId\":\"" + sitemapId.ToString("D") + "\",\"compositeId\":\"A|G|S\",\"nodeType\":\"SubArea\",\"component\":\"Description\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"SubArea desc\"}]}," +
                "{\"sitemapId\":\"" + sitemapId.ToString("D") + "\",\"compositeId\":\"missing\",\"nodeType\":\"Area\",\"component\":\"DisplayText\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"No node\"}]}" +
                "]}";

            var output = (SavingSiteMapOutput)action.Saving(null, serviceAdmin, null, null, json);

            Assert.AreEqual(1, output.sitemapIds.Count);
            Assert.AreEqual(sitemapId.ToString("D"), output.sitemapIds[0]);
            serviceAdmin.Received(2).Update(Arg.Is<Entity>(entity => entity.LogicalName == "sitemap" && entity.Id == sitemapId));
        }

        [TestMethod]
        public void Saving_ReturnsNoIdsForUnchangedOrMissingRowsAndValidatesIds()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var sitemapId = Guid.NewGuid();
            var nullXmlId = Guid.NewGuid();

            serviceAdmin.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(call =>
            {
                var id = (Guid)call[1];
                return new Entity("sitemap", id)
                {
                    ["sitemapxml"] = id == nullXmlId
                        ? null
                        : "<sitemap><Area Id=\"A\"><Titles><Title LCID=\"1033\" Title=\"Area\" /></Titles></Area></sitemap>"
                };
            });

            var output = (SavingSiteMapOutput)action.Saving(null, serviceAdmin, null, null,
                "{\"sitemapUpdates\":[" +
                "{\"sitemapId\":\"" + sitemapId.ToString("D") + "\",\"compositeId\":\"A\",\"nodeType\":\"Area\",\"component\":\"DisplayText\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Area\"}]}," +
                "{\"sitemapId\":\"" + nullXmlId.ToString("D") + "\",\"compositeId\":\"A\",\"nodeType\":\"Area\",\"component\":\"DisplayText\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Area\"}]}" +
                "]}");
            var empty = (SavingSiteMapOutput)action.Saving(null, serviceAdmin, null, null, "{\"sitemapUpdates\":null}");
            var invalid = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null,
                "{\"sitemapUpdates\":[{\"sitemapId\":\"not-a-guid\",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Area\"}]}]}"));
            var blank = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null,
                "{\"sitemapUpdates\":[{\"sitemapId\":\" \",\"labels\":[{\"LanguageCode\":\"1033\",\"Label\":\"Area\"}]}]}"));

            Assert.AreEqual(0, output.sitemapIds.Count);
            Assert.AreEqual(0, empty.sitemapIds.Count);
            Assert.AreEqual("SiteMap sitemapId must be a GUID.", invalid.Message);
            Assert.AreEqual("SiteMap sitemapId must be a GUID.", blank.Message);
            serviceAdmin.DidNotReceive().Update(Arg.Any<Entity>());
        }

        [TestMethod]
        public void PublishingPublishedAndOtherValidatePayloads()
        {
            var action = new SiteMap();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var valid = Guid.NewGuid().ToString("D");
            var waits = new List<int>();
            var requests = new List<OrganizationRequest>();
            SiteMap.WaitAction = waits.Add;
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                requests.Add((OrganizationRequest)call[0]);
                return new OrganizationResponse();
            });

            var publishing = (PublishingSiteMapOutput)action.Publishing(null, serviceAdmin, null, null,
                "{\"sitemapIds\":[\"" + valid + "\",\"bad\",\"" + valid + "\",\" \"]}");
            var noPublish = (PublishingSiteMapOutput)action.Publishing(null, serviceAdmin, null, null, "{\"sitemapIds\":null}");
            var published = (PublishedSiteMapOutput)action.Published(null, serviceAdmin, null, null,
                "{\"sitemapIds\":[\"" + valid + "\",\"bad\"]}");
            var other = (OtherSiteMapOutput)action.Other(null, serviceAdmin, null, null, "{\"sitemapOperation\":\"ping\"}");
            var invalidOther = ThrowsInvalidPluginExecutionException(() => action.Other(null, serviceAdmin, null, null, "{\"sitemapOperation\":\"\"}"));

            Assert.AreEqual(1, publishing.sitemapIds.Count);
            Assert.AreEqual(0, noPublish.sitemapIds.Count);
            Assert.AreEqual(1, published.sitemapIds.Count);
            Assert.AreEqual("ping", other.operation);
            Assert.AreEqual("SiteMap Other operation is required.", invalidOther.Message);
            var publish = (PublishXmlRequest)requests.Single();
            Assert.AreEqual("<importexportxml><sitemaps><sitemap>" + valid + "</sitemap></sitemaps></importexportxml>", publish.ParameterXml);
            Assert.AreEqual(10000, waits.Single());
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var retrieve = call[0] as RetrieveEntityRequest;
                if (retrieve?.LogicalName == "throw")
                {
                    throw new InvalidOperationException("missing");
                }

                if (retrieve?.LogicalName == "nullresponse")
                {
                    return null;
                }

                if (retrieve?.LogicalName == "nometadata")
                {
                    return new RetrieveEntityResponse();
                }

                return RetrieveEntityResponse(LabelWithNullLocalizedLabel("Entity", 1033));
            });

            Assert.IsNull(InvokePrivate("ParseSolutionId", "ALL"));
            Assert.IsNull(InvokePrivate("ParseSolutionId", " "));
            Assert.AreEqual(0, ((List<Entity>)InvokePrivate("ToList", new object[] { null })).Count);
            Assert.AreEqual(Guid.Empty, InvokePrivate("GetEntityGuid", null, "sitemapid"));
            var guid = Guid.NewGuid();
            Assert.AreEqual(guid, InvokePrivate("GetEntityGuid", new Entity("sitemap") { ["sitemapid"] = guid }, "sitemapid"));
            Assert.AreEqual(guid, InvokePrivate("GetEntityGuid", new Entity("sitemap") { ["sitemapid"] = new EntityReference("sitemap", guid) }, "sitemapid"));
            Assert.AreEqual(guid, InvokePrivate("GetEntityGuid", new Entity("sitemap", guid), "sitemapid"));

            Assert.AreEqual(0, ((List<string>)InvokePrivate("GetReferencedEntityNames", new object[] { null })).Count);
            Assert.AreEqual(0, ((List<string>)InvokePrivate("GetReferencedEntityNames", new List<SiteMapMetadataOutput> { null })).Count);
            Assert.AreEqual(0, ((List<string>)InvokePrivate("GetEntityNamesFromXml", " ")).Count);
            Assert.AreEqual(0, ((List<string>)InvokePrivate("GetEntityNamesFromXml", "<sitemap><Area>")).Count);
            var names = (List<string>)InvokePrivate("GetEntityNamesFromXml", "<sitemap><Area Id=\"A\"><Group Id=\"G\"><SubArea Entity=\"account\" /><SubArea Entity=\"\" /></Group></Area></sitemap>");
            Assert.AreEqual("account", names.Single());

            Assert.AreEqual(1, ((List<SiteMapLocalizedLabelOutput>)InvokePrivate("RetrieveEntityLabels", serviceAdmin, "account")).Count);
            Assert.AreEqual(0, ((List<SiteMapLocalizedLabelOutput>)InvokePrivate("RetrieveEntityLabels", serviceAdmin, "nullresponse")).Count);
            Assert.AreEqual(0, ((List<SiteMapLocalizedLabelOutput>)InvokePrivate("RetrieveEntityLabels", serviceAdmin, "nometadata")).Count);
            Assert.AreEqual(0, ((List<SiteMapLocalizedLabelOutput>)InvokePrivate("RetrieveEntityLabels", serviceAdmin, "throw")).Count);
            Assert.AreEqual(0, ((List<SiteMapLocalizedLabelOutput>)InvokePrivate("BuildLabelOutput", new object[] { null })).Count);
            Assert.IsFalse((bool)InvokePrivate("SitemapReferencesEntity", null, "account"));
            Assert.IsFalse((bool)InvokePrivate("SitemapReferencesEntity", new SiteMapMetadataOutput(), " "));
            Assert.IsTrue((bool)InvokePrivate("SitemapReferencesEntity", new SiteMapMetadataOutput
            {
                sitemapxml = "<sitemap><Area Id=\"A\"><Group Id=\"G\"><SubArea Entity=\"account\" /></Group></Area></sitemap>"
            }, "ACCOUNT"));
            Assert.IsFalse((bool)InvokePrivate("SitemapReferencesEntity", new SiteMapMetadataOutput { sitemapxml = "<sitemap />" }, "account"));

            Assert.AreEqual(" ", InvokePrivate("UpdateSitemapXml", " ", new SiteMapUpdateInput()));
            var xml = "<sitemap><Area Id=\"A\"><Titles><Title LCID=\"1033\" Title=\"Area\" /></Titles><Group Id=\"G\"><SubArea Id=\"S\" /></Group></Area></sitemap>";
            Assert.AreEqual(xml, InvokePrivate("UpdateSitemapXml", xml, null));
            Assert.AreEqual(xml, InvokePrivate("UpdateSitemapXml", xml, new SiteMapUpdateInput()));
            Assert.AreEqual(xml, InvokePrivate("UpdateSitemapXml", xml, new SiteMapUpdateInput
            {
                compositeId = "missing",
                labels = new List<SiteMapLabelInput> { new SiteMapLabelInput { LanguageCode = "1033", Label = "No node" } }
            }));
            Assert.AreEqual("<bad", InvokePrivate("UpdateSitemapXml", "<bad", new SiteMapUpdateInput
            {
                compositeId = "A",
                labels = new List<SiteMapLabelInput> { new SiteMapLabelInput { LanguageCode = "1033", Label = "No node" } }
            }));
            var descriptionXml = (string)InvokePrivate("UpdateSitemapXml", xml, new SiteMapUpdateInput
            {
                compositeId = "A|G|S",
                nodeType = "SubArea",
                component = "Description",
                labels = new List<SiteMapLabelInput>
                {
                    null,
                    new SiteMapLabelInput { LanguageCode = "bad", Label = "Bad" },
                    new SiteMapLabelInput { LanguageCode = "1033", Label = null }
                }
            });
            StringAssert.Contains(descriptionXml, "Descriptions");
            StringAssert.Contains(descriptionXml, "Description=\"\"");

            var missingIdXml = "<sitemap><Area><Group><SubArea /></Group></Area></sitemap>";
            var missingIdAreaXml = (string)InvokePrivate("UpdateSitemapXml", missingIdXml, new SiteMapUpdateInput
            {
                compositeId = "",
                nodeType = "Area",
                component = "DisplayText",
                labels = new List<SiteMapLabelInput> { new SiteMapLabelInput { LanguageCode = "1033", Label = "Missing Area" } }
            });
            StringAssert.Contains(missingIdAreaXml, "Title=\"Missing Area\"");

            var missingIdGroupXml = (string)InvokePrivate("UpdateSitemapXml", missingIdXml, new SiteMapUpdateInput
            {
                compositeId = "|",
                nodeType = "Group",
                component = "DisplayText",
                labels = new List<SiteMapLabelInput> { new SiteMapLabelInput { LanguageCode = "1033", Label = "Missing Group" } }
            });
            StringAssert.Contains(missingIdGroupXml, "Title=\"Missing Group\"");

            var missingIdSubAreaXml = (string)InvokePrivate("UpdateSitemapXml", missingIdXml, new SiteMapUpdateInput
            {
                compositeId = "||",
                nodeType = "SubArea",
                component = "DisplayText",
                labels = new List<SiteMapLabelInput> { new SiteMapLabelInput { LanguageCode = "1033", Label = "Missing SubArea" } }
            });
            StringAssert.Contains(missingIdSubAreaXml, "Title=\"Missing SubArea\"");

            var root = XDocument.Parse(xml).Root;
            Assert.IsNull(InvokePrivate("FindSitemapNode", null, "A", "Area"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, " ", "Area"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "|", "Area"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "A", "Group"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "A|missing", "Group"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "A|G", "Area"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "A|G|missing", "SubArea"));
            Assert.IsNull(InvokePrivate("FindSitemapNode", root, "A|G|S|extra", "SubArea"));
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", root, "A", "Area"));
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", root, "A|G", "Group"));
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", root, "A|G|S", "SubArea"));
            var missingIdRoot = XDocument.Parse(missingIdXml).Root;
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", missingIdRoot, "", "Area"));
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", missingIdRoot, "|", "Group"));
            Assert.IsNotNull(InvokePrivate("FindSitemapNode", missingIdRoot, "||", "SubArea"));
            Assert.IsFalse((bool)InvokePrivate("IsNodeType", null, "Area"));
            Assert.IsTrue((bool)InvokePrivate("IsNodeType", root.Element("Area"), " "));
            Assert.IsTrue((bool)InvokePrivate("IsNodeType", root.Element("Area"), "Area"));

            var ids = (List<string>)InvokePrivate("GetValidSitemapIds", new List<string> { null, "bad", guid.ToString("D"), guid.ToString("D") });
            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("<importexportxml><sitemaps><sitemap>" + guid.ToString("D") + "</sitemap></sitemaps></importexportxml>",
                InvokePrivate("BuildPublishXml", ids));
        }

        private static EntityCollection Entities(params Entity[] entities)
        {
            return new EntityCollection(entities.ToList());
        }

        private static RetrieveEntityResponse RetrieveEntityResponse(Label displayName)
        {
            var response = new RetrieveEntityResponse();
            response.Results["EntityMetadata"] = displayName == null
                ? new EntityMetadata()
                : new EntityMetadata { DisplayName = displayName };
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
            var method = typeof(SiteMap).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Private method not found: " + name);
            return method.Invoke(null, args);
        }
    }
}
