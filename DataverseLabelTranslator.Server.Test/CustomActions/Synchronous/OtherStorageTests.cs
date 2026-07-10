using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OtherStorageTests
    {
        [TestMethod]
        public void IsStorageOperation_TrueForKnownNames()
        {
            Assert.IsTrue(OtherStorage.IsStorageOperation("ReadAppSettings"));
            Assert.IsTrue(OtherStorage.IsStorageOperation("WriteAppSettings"));
            Assert.IsTrue(OtherStorage.IsStorageOperation("ReadDictionary"));
            Assert.IsTrue(OtherStorage.IsStorageOperation("WriteDictionary"));
        }

        [TestMethod]
        public void IsStorageOperation_FalseForUnknown()
        {
            Assert.IsFalse(OtherStorage.IsStorageOperation("Unknown"));
            Assert.IsFalse(OtherStorage.IsStorageOperation(""));
        }

        private static IOrganizationService MockServiceWithWebResource(string name, string content)
        {
            var service = Substitute.For<IOrganizationService>();
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["name"] = name;
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { wr }));
            service.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>()).Returns(wr);
            return service;
        }

        [TestMethod]
        public void ReadAppSettings_Operation_ReturnsContent()
        {
            var action = new OtherAction();
            var service = MockServiceWithWebResource(OtherSupport.AppSettingsName, "{\"ai\":{\"selectedProvider\":\"google\"}}");
            var output = action.Other(null, service, null, null, "{\"operation\":\"ReadAppSettings\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void WriteAppSettings_Operation_WritesAndReturns()
        {
            var action = new OtherAction();
            var service = MockServiceWithWebResource(OtherSupport.AppSettingsName, "{\"ai\":{}}");
            var output = action.Other(null, service, null, null, "{\"operation\":\"WriteAppSettings\",\"content\":\"new\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void ReadDictionary_Operation_ReturnsContent()
        {
            var action = new OtherAction();
            var service = MockServiceWithWebResource(OtherSupport.DictionaryName, "<dictionary sourceLcid=\"\"></dictionary>");
            var output = action.Other(null, service, null, null, "{\"operation\":\"ReadDictionary\"}");
            Assert.IsNotNull(output);
        }

        [TestMethod]
        public void WriteDictionary_Operation_WritesAndReturns()
        {
            var action = new OtherAction();
            var service = MockServiceWithWebResource(OtherSupport.DictionaryName, "<dictionary></dictionary>");
            var output = action.Other(null, service, null, null, "{\"operation\":\"WriteDictionary\",\"content\":\"<dictionary></dictionary>\"}");
            Assert.IsNotNull(output);
        }
    }
}
