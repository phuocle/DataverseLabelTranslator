using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OtherSolutionsTests
    {
        private static IOrganizationService MockServiceWithBaseLanguage(int languageCode = 1033)
        {
            var service = Substitute.For<IOrganizationService>();
            var org = new Entity("organization") { Id = Guid.NewGuid() };
            org["languagecode"] = languageCode;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "organization") return new EntityCollection(new[] { org });
                return new EntityCollection();
            });
            return service;
        }

        [TestMethod]
        public void IsXrmOperation_TrueForKnownNames()
        {
            Assert.IsTrue(OtherSolutions.IsXrmOperation("GetSolutions"));
            Assert.IsTrue(OtherSolutions.IsXrmOperation("GetEntities"));
            Assert.IsTrue(OtherSolutions.IsXrmOperation("GetBaseLanguage"));
            Assert.IsTrue(OtherSolutions.IsXrmOperation("GetAllNoneBaseLanguageCodes"));
            Assert.IsTrue(OtherSolutions.IsXrmOperation("GetLanguageLocales"));
        }

        [TestMethod]
        public void IsXrmOperation_FalseForUnknown()
        {
            Assert.IsFalse(OtherSolutions.IsXrmOperation("Unknown"));
            Assert.IsFalse(OtherSolutions.IsXrmOperation(""));
        }

        [TestMethod]
        public void GetBaseLanguage_Operation_ReturnsLanguageCode()
        {
            var action = new OtherAction();
            var service = MockServiceWithBaseLanguage(1033);
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetBaseLanguage\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void GetAllNoneBaseLanguageCodes_Operation_ReturnsList()
        {
            var action = new OtherAction();
            var service = MockServiceWithBaseLanguage();
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is Microsoft.Crm.Sdk.Messages.RetrieveAvailableLanguagesRequest)
                {
                    return AdapterTestHelpers.RetrieveAvailableLanguagesResponse(1033, 1043, 1031);
                }
                return new OrganizationResponse();
            });
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetAllNoneBaseLanguageCodes\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void GetLanguageLocales_Operation_ReturnsList()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            var locale = new Entity("languagelocale") { Id = Guid.NewGuid() };
            locale["language"] = "English";
            locale["localeid"] = 1033;
            locale["code"] = "en-US";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { locale }));
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetLanguageLocales\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void GetLanguageLocales_InvalidLocaleId_Skipped()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            var locale = new Entity("languagelocale") { Id = Guid.NewGuid() };
            locale["language"] = "Invalid";
            locale["localeid"] = 0;
            locale["code"] = "";
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { locale }));
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetLanguageLocales\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void GetSolutions_Operation_ReturnsSolutions()
        {
            var action = new OtherAction();
            var service = Substitute.For<IOrganizationService>();
            var sol = new Entity("solution") { Id = Guid.NewGuid() };
            sol["friendlyname"] = "Test Solution";
            sol["uniquename"] = "TestSolution";
            sol["ismanaged"] = false;
            sol["isvisible"] = true;
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { sol }));
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetSolutions\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void GetEntities_Operation_ReturnsEntities()
        {
            var action = new OtherAction();
            var service = MockServiceWithBaseLanguage();
            var em = new EntityMetadata
            {
                LogicalName = "account",
                SchemaName = "Account",
                MetadataId = Guid.NewGuid(),
                IsCustomizable = new BooleanManagedProperty(true),
                DisplayName = new Label("Account", 1033)
            };
            service.Execute(Arg.Any<OrganizationRequest>()).Returns(call =>
            {
                if (call[0] is Microsoft.Xrm.Sdk.Messages.RetrieveAllEntitiesRequest)
                {
                    var r = new Microsoft.Xrm.Sdk.Messages.RetrieveAllEntitiesResponse();
                    r.Results["EntityMetadata"] = new[] { em };
                    return r;
                }
                return new OrganizationResponse();
            });
            var output = action.Other(null, service, null, null, "{\"operation\":\"GetEntities\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void UnknownXrmOperation_Throws()
        {
            var action = new OtherAction();
            var service = MockServiceWithBaseLanguage();
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => action.Other(null, service, null, null, "{\"operation\":\"SomeUnknownXrmOp\"}"));
        }
    }
}
