using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class ContentSnippetAdapterTests
    {
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
            var adapter = new ContentSnippetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput { changedRows = new List<EasyTranslatorChangedRowInput>() });
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Publish_NoTargets_NoExecute()
        {
            var adapter = new ContentSnippetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Publish(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Load_NoPortalTables_ReturnsEmpty()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                throw new Exception("The entity with a name = adx_contentsnippet was not found");
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void Load_WithContentSnippets_ReturnsRows()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var languageId = Guid.NewGuid();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "languagelocale") return AdapterTestHelpers.Entities(MakeLocale("1033", "English", "en-US"));
                if (q.EntityName == "adx_websitelanguage") return AdapterTestHelpers.Entities(MakeWebsiteLanguage(websiteId, languageId, "1033", "EN"));
                if (q.EntityName == "adx_contentsnippet") return AdapterTestHelpers.Entities(MakeSnippet("S1", websiteId, languageId, "Hello"));
                if (q.EntityName == "adx_website") return AdapterTestHelpers.Entities(MakeWebsite(websiteId, "Portal"));
                return AdapterTestHelpers.Entities();
            });
            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput { entityName = "none", component = "DisplayText", solutionId = "all" });
            Assert.AreEqual(1, output.grid.rows.Count);
            Assert.AreEqual("Portal", output.grid.rows[0].SchemaName);
            var children = (List<EasyTranslatorGridRowOutput>)output.grid.rows[0]["children"];
            Assert.AreEqual("S1", children[0].SchemaName);
            Assert.AreEqual("Hello", children[0]["1033"]);
            Assert.AreEqual("English (en-US) (1033)", output.grid.languageColumns[0].text);
        }

        [TestMethod]
        public void Save_NoPortalTables_ReturnsEmpty()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var input = new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"content|{Guid.NewGuid():D}|s1",
                        changes = new Dictionary<string, string> { { "1033", "X" } }
                    }
                }
            };
            var output = adapter.Save(AdapterTestHelpers.Context(service), input);
            Assert.AreEqual(0, output.changedRowCount);
        }

        [TestMethod]
        public void Save_UpdatesExistingSnippetAndCreatesMissingLanguageSnippet()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var englishLanguageId = Guid.NewGuid();
            var japaneseLanguageId = Guid.NewGuid();
            var existingSnippetId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "adx_websitelanguage")
                {
                    return AdapterTestHelpers.Entities(
                        MakeWebsiteLanguage(websiteId, englishLanguageId, "1033", "EN"),
                        MakeWebsiteLanguage(websiteId, japaneseLanguageId, "1041", "JA"));
                }
                if (q.EntityName == "adx_contentsnippet")
                {
                    return AdapterTestHelpers.Entities(MakeSnippet("Header", websiteId, englishLanguageId, "Old", existingSnippetId));
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"content|{websiteId:D}|Header",
                        changes = new Dictionary<string, string>
                        {
                            { "1033", "Updated" },
                            { "1041", "Created" },
                            { "9999", "Ignored missing language" },
                            { "1066", string.Empty }
                        }
                    },
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"content|{websiteId:D}|NoChanges",
                        changes = new Dictionary<string, string>()
                    }
                }
            });

            Assert.IsTrue(output.changed);
            Assert.AreEqual(2, output.changedRowCount);
            service.Received(1).Update(Arg.Is<Entity>(e =>
                e.LogicalName == "adx_contentsnippet" &&
                e.Id == existingSnippetId &&
                (string)e["adx_value"] == "Updated"));
            service.Received(1).Create(Arg.Is<Entity>(e =>
                e.LogicalName == "adx_contentsnippet" &&
                (string)e["adx_name"] == "Header" &&
                (string)e["adx_value"] == "Created" &&
                ((EntityReference)e["adx_websiteid"]).Id == websiteId &&
                ((EntityReference)e["adx_contentsnippetlanguageid"]).Id == japaneseLanguageId));
        }

        [TestMethod]
        public void Save_CreatesWhenMatchedSnippetHasEmptyIdAndSkipsWrongWebsiteLanguage()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var otherWebsiteId = Guid.NewGuid();
            var languageId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "adx_websitelanguage")
                {
                    return AdapterTestHelpers.Entities(
                        MakeWebsiteLanguage(websiteId, languageId, "1033", "EN"),
                        MakeWebsiteLanguage(otherWebsiteId, Guid.NewGuid(), "1041", "JA"));
                }
                if (q.EntityName == "adx_contentsnippet")
                {
                    return AdapterTestHelpers.Entities(MakeSnippet("Header", websiteId, languageId, "Old", Guid.Empty));
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
            {
                changedRows = new List<EasyTranslatorChangedRowInput>
                {
                    new EasyTranslatorChangedRowInput
                    {
                        gridKey = $"content|{websiteId:D}|Header",
                        changes = new Dictionary<string, string>
                        {
                            { "1033", "Created because id is empty" },
                            { "1041", "Skipped because LCID belongs to another website" }
                        }
                    }
                }
            });

            Assert.AreEqual(1, output.changedRowCount);
            service.DidNotReceive().Update(Arg.Any<Entity>());
            service.Received(1).Create(Arg.Is<Entity>(e =>
                (string)e["adx_value"] == "Created because id is empty" &&
                ((EntityReference)e["adx_contentsnippetlanguageid"]).Id == languageId));
        }

        [TestMethod]
        public void Save_ThrowsFriendlyMessage_WhenPortalTablesAreMissing()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                throw new Exception("adx_websitelanguage does not exist");
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        new EasyTranslatorChangedRowInput
                        {
                            gridKey = $"content|{Guid.NewGuid():D}|Header",
                            changes = new Dictionary<string, string> { { "1033", "Updated" } }
                        }
                    }
                }),
                "Content Snippets require the legacy Power Pages adx_* tables.");
        }

        [TestMethod]
        public void Load_RethrowsNonPortalExceptions()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                throw new Exception("network down");
            });

            AdapterTestHelpers.ExpectException<Exception>(() =>
                adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput()),
                "network down");
        }

        [TestMethod]
        public void Save_RejectsBlankSnippetNameAndInvalidWebsiteId()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var languageId = Guid.NewGuid();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "adx_websitelanguage") return AdapterTestHelpers.Entities(MakeWebsiteLanguage(websiteId, languageId, "1033", "EN"));
                if (q.EntityName == "adx_contentsnippet") return AdapterTestHelpers.Entities();
                return AdapterTestHelpers.Entities();
            });

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        new EasyTranslatorChangedRowInput
                        {
                            gridKey = "content|not-guid|Header",
                            changes = new Dictionary<string, string> { { "1033", "Updated" } }
                        }
                    }
                }),
                "Content snippet websiteid must be a GUID.");

            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(() =>
                adapter.Save(AdapterTestHelpers.Context(service), new EasyTranslatorSaveInput
                {
                    changedRows = new List<EasyTranslatorChangedRowInput>
                    {
                        new EasyTranslatorChangedRowInput
                        {
                            gridKey = $"content|{websiteId:D}| ",
                            changes = new Dictionary<string, string> { { "1033", "Updated" } }
                        }
                    }
                }),
                "Content snippet name is required.");
        }

        [TestMethod]
        public void Published_NoTargets_NoExecute()
        {
            var adapter = new ContentSnippetAdapter();
            var service = Substitute.For<IOrganizationService>();
            var output = adapter.Published(AdapterTestHelpers.Context(service), new EasyTranslatorPublishInput());
            Assert.AreEqual(0, output.publishTargets.Count);
        }

        [TestMethod]
        public void Load_EdgeRows_CoversFallbacksAndSkipsInvalidPortalData()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var missingWebsiteId = Guid.NewGuid();
            var languageId = Guid.NewGuid();
            var unmatchedLanguageId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "languagelocale")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("languagelocale"),
                        MakeLocale("1033", null, null));
                }
                if (q.EntityName == "adx_websitelanguage")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("adx_websitelanguage"),
                        MakeWebsiteLanguage(websiteId, languageId, "1033", "EN", useAliasedValues: false),
                        MakeWebsiteLanguage(websiteId, Guid.NewGuid(), "9999", "EN", useAliasedValues: false));
                }
                if (q.EntityName == "adx_contentsnippet")
                {
                    return AdapterTestHelpers.Entities(
                        MakeSnippet(" ", websiteId, languageId, "Skipped blank name"),
                        MakeSnippet("MissingLanguage", websiteId, unmatchedLanguageId, "Skipped value"),
                        MakeSnippet("FallbackWebsite", missingWebsiteId, languageId, "Fallback"),
                        new Entity("adx_contentsnippet"));
                }
                if (q.EntityName == "adx_website")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("adx_website"),
                        MakeWebsite(websiteId, null));
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput());

            Assert.AreEqual(2, output.grid.rows.Count);
            Assert.AreEqual("1033 (1033)", output.grid.languageColumns[0].text);
            Assert.AreEqual(1, output.grid.languageColumns.Count);
            Assert.IsTrue(output.grid.rows.Exists(row => row.SchemaName == missingWebsiteId.ToString("D")));
        }

        [TestMethod]
        public void Load_WithOnlyInvalidSnippets_ReturnsNoWebsiteRows()
        {
            var adapter = new ContentSnippetAdapter();
            var service = CreateService();
            var websiteId = Guid.NewGuid();
            var languageId = Guid.NewGuid();

            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return AdapterTestHelpers.Entities(AdapterTestHelpers.CreateOrganizationEntity());
                if (q.EntityName == "languagelocale") return AdapterTestHelpers.Entities();
                if (q.EntityName == "adx_websitelanguage") return AdapterTestHelpers.Entities(MakeWebsiteLanguage(websiteId, languageId, "1033", "EN"));
                if (q.EntityName == "adx_contentsnippet")
                {
                    return AdapterTestHelpers.Entities(
                        new Entity("adx_contentsnippet"),
                        MakeSnippet("MissingRefs"));
                }
                if (q.EntityName == "adx_website")
                {
                    Assert.Fail("Website query should not run when no valid snippets exist.");
                }

                return AdapterTestHelpers.Entities();
            });

            var output = adapter.Load(AdapterTestHelpers.Context(service), new EasyTranslatorLoadInput());
            Assert.AreEqual(0, output.grid.rows.Count);
        }

        [TestMethod]
        public void PrivateHelpers_CoverContentSnippetFallbackBranches()
        {
            var adapterType = typeof(ContentSnippetAdapter);
            var languageType = adapterType.GetNestedType("PortalLanguageInfo", BindingFlags.NonPublic);
            var localeType = adapterType.GetNestedType("LocaleInfo", BindingFlags.NonPublic);
            Assert.IsNotNull(languageType);
            Assert.IsNotNull(localeType);

            var language = Activator.CreateInstance(languageType);
            languageType.GetProperty("WebsiteLanguageId").SetValue(language, Guid.NewGuid());
            languageType.GetProperty("WebsiteId").SetValue(language, Guid.NewGuid());
            languageType.GetProperty("Lcid").SetValue(language, "1033");
            languageType.GetProperty("LanguageCode").SetValue(language, string.Empty);

            var localeMapType = typeof(Dictionary<,>).MakeGenericType(typeof(string), localeType);
            var localeMap = Activator.CreateInstance(localeMapType);
            var locale = Activator.CreateInstance(localeType);
            localeType.GetProperty("Language").SetValue(locale, null);
            localeType.GetProperty("Code").SetValue(locale, string.Empty);
            localeMapType.GetMethod("Add").Invoke(localeMap, new[] { "1033", locale });

            var formatMethod = adapterType.GetMethod("FormatLanguageColumnText", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(formatMethod);
            Assert.AreEqual("1033 (1033)", formatMethod.Invoke(null, new[] { language, localeMap }));
            Assert.AreEqual("1033 (1033)", formatMethod.Invoke(null, new[] { language, null }));

            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAliasedValue", new Entity("x"), "missing"));
            var nullAliasRow = new Entity("x");
            nullAliasRow["alias"] = null;
            Assert.AreEqual(string.Empty, AdapterTestHelpers.InvokeStatic<string>(adapterType, "GetAliasedValue", nullAliasRow, "alias"));

            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsMissingPortalTable", new Exception("plain error")));
            Assert.IsFalse(AdapterTestHelpers.InvokeStatic<bool>(adapterType, "IsMissingPortalTable", (object)null));

            var validateMethod = adapterType.GetMethod("ValidateGuid", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(validateMethod);
            try
            {
                validateMethod.Invoke(null, new object[] { string.Empty, "Website" });
                Assert.Fail("Expected InvalidPluginExecutionException.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidPluginExecutionException));
                Assert.IsTrue(ex.InnerException.Message.Contains("Website must be a GUID."));
            }
        }

        private static Entity MakeSnippet(string name)
        {
            return MakeSnippet(name, Guid.Empty, Guid.Empty, "Hello");
        }

        private static Entity MakeSnippet(string name, Guid websiteId, Guid languageId, string value, Guid? snippetId = null)
        {
            var e = new Entity("adx_contentsnippet") { Id = snippetId ?? Guid.NewGuid() };
            e["adx_contentsnippetid"] = e.Id;
            e["adx_name"] = name;
            e["adx_value"] = value;
            if (websiteId != Guid.Empty)
            {
                e["adx_websiteid"] = new EntityReference("adx_website", websiteId);
            }
            if (languageId != Guid.Empty)
            {
                e["adx_contentsnippetlanguageid"] = new EntityReference("adx_websitelanguage", languageId);
            }
            return e;
        }

        private static Entity MakeWebsite(Guid websiteId, string name)
        {
            var e = new Entity("adx_website") { Id = websiteId };
            e["adx_websiteid"] = websiteId;
            e["adx_name"] = name;
            return e;
        }

        private static Entity MakeLocale(string localeId, string language, string code)
        {
            var e = new Entity("languagelocale");
            e["localeid"] = localeId;
            e["language"] = language;
            e["code"] = code;
            return e;
        }

        private static Entity MakeWebsiteLanguage(Guid websiteId, Guid languageId, string lcid, string languageCode, bool useAliasedValues = true)
        {
            var e = new Entity("adx_websitelanguage") { Id = languageId };
            e["adx_websitelanguageid"] = languageId;
            e["adx_websiteid"] = new EntityReference("adx_website", websiteId);
            e["portal.adx_lcid"] = useAliasedValues ? (object)new AliasedValue("adx_portallanguage", "adx_lcid", lcid) : lcid;
            e["portal.adx_languagecode"] = useAliasedValues ? (object)new AliasedValue("adx_portallanguage", "adx_languagecode", languageCode) : languageCode;
            return e;
        }
    }
}
