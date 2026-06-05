using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class WebResourceTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            WebResource.WaitAction = Thread.Sleep;
        }

        [TestMethod]
        public void Loading_ReturnsSortedGroups_FromAllWebResources()
        {
            var action = new WebResource();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var baseJson = CreateWebResource(Guid.NewGuid(), "pl_/b/messages.1033.js", "", JsonContent(new Dictionary<string, string>
            {
                { "hello", "Hello" },
                { "empty", null }
            }), 3);
            var duplicateBase = CreateWebResource(Guid.NewGuid(), "pl_/b/messages.1033.js", "", JsonContent(new Dictionary<string, string>
            {
                { "ignored", "Ignored" }
            }), 3);
            var baseResx = CreateWebResource(Guid.NewGuid(), "", "pl_/a/labels.1033.resx", ResxContent(new Dictionary<string, string>
            {
                { "title", "Title" },
                { "blank", null }
            }), 12);
            var numericResx = CreateWebResource(Guid.NewGuid(), "", "1033", ResxContent(new Dictionary<string, string>
            {
                { "only", "Only" }
            }), 12);
            var nullDisplayName = CreateWebResource(Guid.NewGuid(), "pl_/c/messages.1033.js", null, JsonContent(new Dictionary<string, string>
            {
                { "fallback", "Fallback" }
            }), 3);
            var nullName = CreateWebResource(Guid.NewGuid(), null, "prefix1033", ResxContent(new Dictionary<string, string>
            {
                { "prefix", "Prefix" }
            }), 12);
            var wrongLanguage = CreateWebResource(Guid.NewGuid(), "pl_/skip/messages.1041.js", "", JsonContent(new Dictionary<string, string>()), 3);
            var unsupported = CreateWebResource(Guid.NewGuid(), "pl_/skip/readme.1033.txt", "", JsonContent(new Dictionary<string, string>()), 3);
            var noLocalizedName = CreateWebResource(Guid.NewGuid(), "", "", JsonContent(new Dictionary<string, string>()), 3);

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var query = (QueryExpression)call[0];
                if (query.EntityName == "organization")
                {
                    return Entities(new Entity("organization") { ["languagecode"] = 1033 });
                }

                if (query.EntityName == "webresource" && QueryContains(query, "1033"))
                {
                    return Entities(baseJson, duplicateBase, baseResx, numericResx, nullDisplayName, nullName, wrongLanguage, unsupported, noLocalizedName);
                }

                if (query.EntityName == "webresource" && QueryContains(query, "pl_/b/messages."))
                {
                    return Entities(
                        baseJson,
                        CreateWebResource(Guid.NewGuid(), "pl_/b/messages.1041.js", "", JsonContent(new Dictionary<string, string>
                        {
                            { "hello", "Konnichiwa" },
                            { "added", "Added" }
                        }), 3),
                        CreateWebResource(Guid.NewGuid(), "pl_/b/messages.1066.js", "", "not-base64", 3),
                        CreateWebResource(Guid.NewGuid(), "pl_/b/messages.3082.resx", "", Convert.ToBase64String(Encoding.UTF8.GetBytes("<root><data name=\"bad\"><value>Bad</value>")), 12),
                        CreateWebResource(Guid.NewGuid(), "pl_/b/messages.txt", "", JsonContent(new Dictionary<string, string>()), 3));
                }

                if (query.EntityName == "webresource" && QueryContains(query, "pl_/a/labels."))
                {
                    return Entities(
                        baseResx,
                        CreateWebResource(Guid.NewGuid(), "pl_/a/labels.1041.resx", "", ResxContent(new Dictionary<string, string>
                        {
                            { "title", "Title JA" },
                            { "novalue", null }
                        }), 12));
                }

                if (query.EntityName == "webresource" && QueryContains(query, string.Empty))
                {
                    return Entities(numericResx);
                }

                if (query.EntityName == "webresource" && QueryContains(query, "pl_/c/messages."))
                {
                    return Entities(nullDisplayName);
                }

                if (query.EntityName == "webresource" && QueryContains(query, "prefix"))
                {
                    return Entities(nullName);
                }

                return Entities();
            });

            var output = (LoadingWebResourceOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}");
            var whitespaceOutput = (LoadingWebResourceOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"   \"}");

            Assert.AreEqual("1033", output.baseLanguage);
            Assert.AreEqual("1033", whitespaceOutput.baseLanguage);
            Assert.AreEqual(5, output.groups.Count);
            Assert.AreEqual(string.Empty, output.groups[0].key);
            Assert.AreEqual("1033", output.groups[0].displayName);
            Assert.AreEqual("only", output.groups[0].resources[0].content.Keys.Single());
            Assert.AreEqual("pl_/a/labels.", output.groups[1].key);
            Assert.AreEqual("pl_/a/labels.1033.resx", output.groups[1].displayName);
            Assert.AreEqual("Title", output.groups[1].resources.Single(r => r.lcid == "1033").content["title"]);
            Assert.AreEqual(string.Empty, output.groups[1].resources.Single(r => r.lcid == "1041").content["novalue"]);
            Assert.AreEqual("pl_/b/messages.", output.groups[2].key);
            Assert.AreEqual(2, output.groups[2].resources.Count);
            Assert.AreEqual("js", output.groups[2].resources.Single(r => r.lcid == "1033").format);
            Assert.AreEqual(string.Empty, output.groups[2].resources.Single(r => r.lcid == "1033").content["empty"]);
            Assert.AreEqual("Konnichiwa", output.groups[2].resources.Single(r => r.lcid == "1041").content["hello"]);
            Assert.AreEqual("Added", output.groups[2].resources.Single(r => r.lcid == "1041").content["added"]);
            Assert.AreEqual("pl_/c/messages.", output.groups[3].key);
            Assert.AreEqual("pl_/c/messages.1033.js", output.groups[3].displayName);
            Assert.AreEqual("prefix", output.groups[4].key);
            Assert.AreEqual("prefix1033", output.groups[4].displayName);
        }

        [TestMethod]
        public void Loading_UsesSolutionMembershipAndValidatesInput()
        {
            var action = new WebResource();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var solutionId = Guid.NewGuid();
            var id = Guid.NewGuid();
            QueryExpression capturedComponentQuery = null;
            var baseResource = CreateWebResource(id, "pl_/messages.1033.js", "", JsonContent(new Dictionary<string, string>
            {
                { "hello", "Hello" }
            }), 3);

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
                        new Entity("solutioncomponent") { ["objectid"] = id });
                }

                if (query.EntityName == "webresource")
                {
                    return Entities(baseResource);
                }

                return Entities();
            });
            serviceAdmin.Retrieve("webresource", id, Arg.Any<ColumnSet>()).Returns(baseResource);

            var output = (LoadingWebResourceOutput)action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"" + solutionId.ToString("D") + "\"}");

            Assert.AreEqual("solutioncomponent", capturedComponentQuery.EntityName);
            Assert.AreEqual(2, capturedComponentQuery.Criteria.Conditions.Count);
            Assert.AreEqual(1, output.groups.Count);
            serviceAdmin.Received(1).Retrieve("webresource", id, Arg.Any<ColumnSet>());

            var invalidSolution = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"not-a-guid\"}"));
            Assert.AreEqual("WebResource solutionId must be a GUID or all.", invalidSolution.Message);

            serviceAdmin.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(Entities());
            var noOrganization = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, "{\"solutionId\":\"all\"}"));
            Assert.AreEqual("Could not retrieve organization base language.", noOrganization.Message);

            var missingInput = ThrowsInvalidPluginExecutionException(() => action.Loading(null, serviceAdmin, null, null, null));
            Assert.AreEqual("Missing WebResource input.", missingInput.Message);
        }

        [TestMethod]
        public void Saving_UpdatesCreatesSkipsAndDeduplicatesIds()
        {
            var action = new WebResource();
            var serviceAdmin = Substitute.For<IOrganizationService>();
            var jsonId = Guid.NewGuid();
            var resxId = Guid.NewGuid();
            var emptyJsonId = Guid.NewGuid();
            var nullContentJsonId = Guid.NewGuid();
            var baseJsonId = Guid.NewGuid();
            var baseResxId = Guid.NewGuid();
            var blankBaseId = Guid.NewGuid();
            var nullBaseId = Guid.NewGuid();
            var fallbackCreateId = Guid.NewGuid();
            var createdFromJsonId = Guid.NewGuid();
            var createdFromResxId = Guid.NewGuid();
            var createdFromBlankBaseId = Guid.NewGuid();
            var createdWithoutLcidId = Guid.NewGuid();
            var updates = new List<Entity>();
            var creates = new List<Entity>();

            serviceAdmin.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(call =>
            {
                var id = (Guid)call[1];
                if (id == jsonId)
                {
                    return CreateWebResource(jsonId, "pl_/messages.1033.js", "", JsonContent(new Dictionary<string, string>
                    {
                        { "hello", "Hello" },
                        { "clear", "Value" }
                    }), 3);
                }

                if (id == resxId)
                {
                    return CreateWebResource(resxId, "pl_/labels.1033.resx", "", ResxContent(new Dictionary<string, string>
                    {
                        { "title", "Title" },
                        { "old", "Old" }
                    }), 12);
                }

                if (id == emptyJsonId)
                {
                    return CreateWebResource(emptyJsonId, "pl_/empty.1033.js", "", string.Empty, 3);
                }

                if (id == nullContentJsonId)
                {
                    var entity = CreateWebResource(nullContentJsonId, "pl_/nullcontent.1033.js", "", string.Empty, 3);
                    entity.Attributes.Remove("content");
                    return entity;
                }

                if (id == baseJsonId)
                {
                    return CreateWebResource(baseJsonId, "pl_/messages.1033.js", "Messages 1033", JsonContent(new Dictionary<string, string>
                    {
                        { "hello", "Hello" },
                        { "existing", "Existing" }
                    }), 3);
                }

                if (id == baseResxId)
                {
                    return CreateWebResource(baseResxId, "pl_/labels.1033.resx", "", ResxContent(new Dictionary<string, string>
                    {
                        { "title", "Title" },
                        { "existing", "Existing" }
                    }), 12);
                }

                if (id == blankBaseId)
                {
                    return CreateWebResource(blankBaseId, string.Empty, string.Empty, string.Empty, 3);
                }

                if (id == nullBaseId)
                {
                    var entity = CreateWebResource(nullBaseId, null, null, string.Empty, 3);
                    entity.Attributes.Remove("content");
                    return entity;
                }

                return null;
            });
            serviceAdmin.When(s => s.Update(Arg.Any<Entity>())).Do(call => updates.Add((Entity)call[0]));
            serviceAdmin.Create(Arg.Any<Entity>()).Returns(call =>
            {
                var entity = (Entity)call[0];
                creates.Add(entity);
                var name = entity.GetAttributeValue<string>("name");
                if (name == "1066.js")
                {
                    return fallbackCreateId;
                }

                if (name == ".js")
                {
                    return createdWithoutLcidId;
                }

                if (string.IsNullOrEmpty(name))
                {
                    return createdFromBlankBaseId;
                }

                if (name != null && name.EndsWith(".1041.resx", StringComparison.OrdinalIgnoreCase))
                {
                    return createdFromResxId;
                }

                return createdFromJsonId;
            });

            var json =
                "{"
                + "\"resourceChanges\":["
                + "null,"
                + "{\"webresourceid\":\"" + jsonId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"hello\",\"value\":\"Hi\"},{\"key\":\"clear\",\"value\":null},null,{\"value\":\"skip\"}]}," 
                + "{\"webresourceid\":\"" + jsonId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"hello\",\"value\":\"Hi again\"}]}," 
                + "{\"webresourceid\":\"" + resxId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"title\",\"value\":\"Title Updated\"},{\"key\":\"new\",\"value\":null}]}," 
                + "{\"webresourceid\":\"" + emptyJsonId.ToString("D") + "\",\"contentChanges\":null}," 
                + "{\"webresourceid\":\"" + nullContentJsonId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"new\",\"value\":\"New\"}]}," 
                + "{\"lcid\":\"1041\",\"baseWebresourceid\":\"" + baseJsonId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"hello\",\"value\":\"Konnichiwa\"}]}," 
                + "{\"lcid\":\"1041\",\"baseWebresourceid\":\"" + baseResxId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"title\",\"value\":\"Title JA\"}]}," 
                + "{\"lcid\":\"1041\",\"baseWebresourceid\":\"" + blankBaseId.ToString("D") + "\",\"contentChanges\":[null,{\"value\":\"skip\"}]}," 
                + "{\"lcid\":\"1041\",\"baseWebresourceid\":\"" + nullBaseId.ToString("D") + "\",\"contentChanges\":[{\"key\":\"nullable\",\"value\":null}]}," 
                + "{\"lcid\":\"1066\",\"contentChanges\":[{\"key\":\"fallback\",\"value\":\"Fallback\"}]}," 
                + "{\"contentChanges\":[null,{\"value\":\"skip\"},{\"key\":\"nullable\",\"value\":null}]}," 
                + "{\"lcid\":\"1066\",\"contentChanges\":null}"
                + "]"
                + "}";

            var output = (SavingWebResourceOutput)action.Saving(null, serviceAdmin, null, null, json);

            CollectionAssert.AreEqual(new[]
            {
                jsonId.ToString("D"),
                resxId.ToString("D"),
                emptyJsonId.ToString("D"),
                nullContentJsonId.ToString("D"),
                createdFromJsonId.ToString("D"),
                createdFromResxId.ToString("D"),
                createdFromBlankBaseId.ToString("D"),
                fallbackCreateId.ToString("D"),
                createdWithoutLcidId.ToString("D")
            }, output.webresourceIds);

            Assert.AreEqual(5, updates.Count);
            var firstJsonUpdate = DecodeJson(updates.First(e => e.Id == jsonId).GetAttributeValue<string>("content"));
            var lastJsonUpdate = DecodeJson(updates.Last(e => e.Id == jsonId).GetAttributeValue<string>("content"));
            Assert.AreEqual("Hi", firstJsonUpdate["hello"]);
            Assert.AreEqual(string.Empty, firstJsonUpdate["clear"]);
            Assert.AreEqual("Hi again", lastJsonUpdate["hello"]);
            Assert.AreEqual("Value", lastJsonUpdate["clear"]);
            Assert.AreEqual(0, DecodeJson(updates.Single(e => e.Id == emptyJsonId).GetAttributeValue<string>("content")).Count);
            Assert.AreEqual("New", DecodeJson(updates.Single(e => e.Id == nullContentJsonId).GetAttributeValue<string>("content"))["new"]);

            var resxUpdate = Decode(updates.Single(e => e.Id == resxId).GetAttributeValue<string>("content"));
            StringAssert.Contains(resxUpdate, "<value>Title Updated</value>");
            StringAssert.Contains(resxUpdate, "name=\"new\"");
            StringAssert.Contains(resxUpdate, "<value></value>");

            Assert.AreEqual(7, creates.Count);
            var jsonCreate = creates.Single(e => e.GetAttributeValue<string>("name") == "pl_/messages.1041.js");
            Assert.AreEqual("Messages 1041", jsonCreate.GetAttributeValue<string>("displayname"));
            Assert.AreEqual(3, jsonCreate.GetAttributeValue<OptionSetValue>("webresourcetype").Value);
            Assert.AreEqual("Konnichiwa", DecodeJson(jsonCreate.GetAttributeValue<string>("content"))["hello"]);
            Assert.AreEqual(string.Empty, DecodeJson(jsonCreate.GetAttributeValue<string>("content"))["existing"]);

            var resxCreate = creates.Single(e => e.GetAttributeValue<string>("name") == "pl_/labels.1041.resx");
            Assert.AreEqual("pl_/labels.1041.resx", resxCreate.GetAttributeValue<string>("displayname"));
            Assert.AreEqual(12, resxCreate.GetAttributeValue<OptionSetValue>("webresourcetype").Value);
            StringAssert.Contains(Decode(resxCreate.GetAttributeValue<string>("content")), "<value>Title JA</value>");

            var fallbackCreate = creates.First(e => e.GetAttributeValue<string>("name") == "1066.js");
            Assert.AreEqual("1066.js", fallbackCreate.GetAttributeValue<string>("displayname"));
            Assert.AreEqual("Fallback", DecodeJson(fallbackCreate.GetAttributeValue<string>("content"))["fallback"]);

            var blankBaseCreate = creates.First(e => e.GetAttributeValue<string>("name") == string.Empty);
            Assert.AreEqual(string.Empty, blankBaseCreate.GetAttributeValue<string>("displayname"));
            Assert.AreEqual(0, DecodeJson(blankBaseCreate.GetAttributeValue<string>("content")).Count);

            var noLcidCreate = creates.Single(e => e.GetAttributeValue<string>("name") == ".js");
            Assert.AreEqual(".js", noLcidCreate.GetAttributeValue<string>("displayname"));
            Assert.AreEqual(string.Empty, DecodeJson(noLcidCreate.GetAttributeValue<string>("content"))["nullable"]);

            var emptyCreate = creates.Last();
            Assert.AreEqual("1066.js", emptyCreate.GetAttributeValue<string>("name"));
            Assert.AreEqual(0, DecodeJson(emptyCreate.GetAttributeValue<string>("content")).Count);
        }

        [TestMethod]
        public void Saving_HandlesEmptyInputAndThrowsForInvalidUpdateId()
        {
            var action = new WebResource();
            var serviceAdmin = Substitute.For<IOrganizationService>();

            var emptyOutput = (SavingWebResourceOutput)action.Saving(null, serviceAdmin, null, null, "{\"resourceChanges\":null}");

            Assert.AreEqual(0, emptyOutput.webresourceIds.Count);
            serviceAdmin.DidNotReceiveWithAnyArgs().Update(default(Entity));
            serviceAdmin.DidNotReceiveWithAnyArgs().Create(default(Entity));

            var ex = ThrowsInvalidPluginExecutionException(() => action.Saving(null, serviceAdmin, null, null, "{\"resourceChanges\":[{\"webresourceid\":\"not-a-guid\"}]}"));
            Assert.AreEqual("WebResource webresourceid is not a valid GUID: not-a-guid", ex.Message);
        }

        [TestMethod]
        public void PublishingAndPublished_FilterIdsPublishAndWait()
        {
            var action = new WebResource();
            var valid = Guid.NewGuid().ToString("D");
            var duplicate = valid.ToUpperInvariant();
            var requests = new List<OrganizationRequest>();
            var waits = new List<int>();
            WebResource.WaitAction = milliseconds => waits.Add(milliseconds);
            var serviceAdmin = Substitute.For<IOrganizationService>();
            serviceAdmin.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                var request = (OrganizationRequest)call[0];
                requests.Add(request);
                return new OrganizationResponse();
            });

            var publishingOutput = (PublishingWebResourceOutput)action.Publishing(null, serviceAdmin, null, null, "{\"webresourceIds\":[\"" + valid + "\",\"" + duplicate + "\",\" \",\"not-a-guid\"]}");
            var publish = (PublishXmlRequest)requests.Single();

            CollectionAssert.AreEqual(new[] { valid }, publishingOutput.webresourceIds);
            Assert.AreEqual("<importexportxml><webresources><webresource>" + valid + "</webresource></webresources></importexportxml>", publish.ParameterXml);

            var emptyPublishingOutput = (PublishingWebResourceOutput)action.Publishing(null, serviceAdmin, null, null, "{\"webresourceIds\":null}");
            Assert.AreEqual(0, emptyPublishingOutput.webresourceIds.Count);
            Assert.AreEqual(10000, waits.Single());

            var publishedOutput = (PublishedWebResourceOutput)action.Published(null, serviceAdmin, null, null, "{\"webresourceIds\":[\"" + valid + "\",\"not-a-guid\"]}");
            CollectionAssert.AreEqual(new[] { valid }, publishedOutput.webresourceIds);
            Assert.AreEqual(2, waits.Count);
            Assert.AreEqual(10000, waits[1]);
        }

        [TestMethod]
        public void Other_ReturnsOperationOrThrowsWhenMissing()
        {
            var action = new WebResource();

            var output = (OtherWebResourceOutput)action.Other(null, null, null, null, "{\"operation\":\"noop\"}");
            var ex = ThrowsInvalidPluginExecutionException(() => action.Other(null, null, null, null, "{\"operation\":\" \"}"));

            Assert.AreEqual("noop", output.operation);
            Assert.AreEqual("WebResource Other operation is required.", ex.Message);
        }

        [TestMethod]
        public void PrivateHelpers_CoverFallbackBranches()
        {
            var nonNumericResx = CreateWebResource(Guid.NewGuid(), "plain.resx", "display", ResxContent(new Dictionary<string, string>()), 12);
            var noContent = CreateWebResource(Guid.NewGuid(), null, null, string.Empty, 3);
            var noName = CreateWebResource(Guid.NewGuid(), null, null, ResxContent(new Dictionary<string, string>()), 12);
            noContent.Attributes.Remove("content");

            Assert.IsNull(InvokePrivate("MatchLocalizedResource", nonNumericResx));
            Assert.AreEqual("plain", InvokePrivate("GetResourceGroupingKey", "plain", "display", "1033"));
            Assert.AreEqual("display", InvokePrivate("GetResourceGroupingKey", string.Empty, "display", "1033"));
            Assert.AreEqual("fallback", InvokePrivate("GetResourceDisplayName", noName, "fallback"));
            Assert.AreEqual(0, ((Dictionary<string, string>)InvokePrivate("ParseResxContent", string.Empty)).Count);
            Assert.AreEqual(0, ((Dictionary<string, string>)InvokePrivate("ParseResxContent", "<root><data><value>Missing name</value></data></root>")).Count);
            Assert.AreEqual("Value", ((Dictionary<string, string>)InvokePrivate("ParseResxContent", "<root><data name=\"hasvalue\"><value>Value</value></data></root>"))["hasvalue"]);
            Assert.AreEqual(string.Empty, ((Dictionary<string, string>)InvokePrivate("ParseResxContent", "<root><data name=\"novalue\"></data></root>"))["novalue"]);
            Assert.AreEqual(0, ((Dictionary<string, string>)InvokePrivate("ParseJsonContent", string.Empty)).Count);
            Assert.AreEqual(0, ((Dictionary<string, string>)InvokePrivate("ParseJsonContent", "[]")).Count);

            var serialized = (string)InvokePrivate(
                "SerializeResxContent",
                "<root><data><value>Missing name</value></data><data name=\"hasvalue\"><value>Old</value></data><data name=\"novalue\"></data></root>",
                new Dictionary<string, string>
                {
                    { "hasvalue", "Updated" },
                    { "novalue", "Updated" },
                    { "added", null }
                });
            StringAssert.Contains(serialized, "<value>Updated</value>");
            StringAssert.Contains(serialized, "name=\"added\"");
            StringAssert.Contains(serialized, "<value></value>");

            var parsed = (WebResourceOutput)InvokePrivate("ParseWebResource", noContent, "9999");
            Assert.AreEqual(string.Empty, parsed.name);
            Assert.AreEqual(string.Empty, parsed.displayname);
            Assert.AreEqual("json", parsed.format);
            Assert.AreEqual(0, parsed.content.Count);
        }

        private static EntityCollection Entities(params Entity[] entities)
        {
            return new EntityCollection(entities.ToList());
        }

        private static Entity CreateWebResource(Guid id, string name, string displayName, string content, int webResourceType)
        {
            var entity = new Entity("webresource", id);
            entity["webresourceid"] = id;
            entity["name"] = name;
            entity["displayname"] = displayName;
            entity["content"] = content;
            entity["webresourcetype"] = webResourceType;
            return entity;
        }

        private static string JsonContent(Dictionary<string, string> content)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            foreach (var kv in content)
            {
                if (!first)
                {
                    sb.Append(",");
                }

                first = false;
                sb.Append("\"").Append(EscapeJson(kv.Key)).Append("\":");
                if (kv.Value == null)
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append("\"").Append(EscapeJson(kv.Value)).Append("\"");
                }
            }
            sb.Append("}");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static Dictionary<string, string> DecodeJson(string base64)
        {
            var parsed = DevKitJson.Deserialize(Decode(base64)) as Dictionary<string, object>;
            var result = new Dictionary<string, string>();
            foreach (var kv in parsed)
            {
                result[kv.Key] = kv.Value == null ? string.Empty : Convert.ToString(kv.Value);
            }

            return result;
        }

        private static string ResxContent(Dictionary<string, string> values)
        {
            var sb = new StringBuilder();
            sb.Append("<root>");
            foreach (var kv in values)
            {
                sb.Append("<data name=\"").Append(kv.Key).Append("\" xml:space=\"preserve\">");
                if (kv.Value != null)
                {
                    sb.Append("<value>").Append(kv.Value).Append("</value>");
                }
                sb.Append("</data>");
            }
            sb.Append("</root>");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static string Decode(string base64)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool QueryContains(QueryExpression query, string value)
        {
            return query.Criteria.Filters
                .SelectMany(filter => filter.Conditions)
                .Any(condition => condition.Values.Any(v => string.Equals(Convert.ToString(v), "%" + value + "%", StringComparison.Ordinal)));
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
            var method = typeof(WebResource).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Private method not found: " + name);
            return method.Invoke(null, args);
        }
    }
}
