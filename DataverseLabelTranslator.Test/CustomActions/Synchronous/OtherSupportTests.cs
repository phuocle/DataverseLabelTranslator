using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OtherSupportTests
    {
        private static IOrganizationService MockAppSettingsService(string jsonContent)
        {
            var service = Substitute.For<IOrganizationService>();
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonContent));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(call =>
            {
                var q = (QueryExpression)call[0];
                if (q.EntityName == "webresource") return new EntityCollection(new[] { wr });
                return new EntityCollection();
            });
            return service;
        }

        [TestMethod]
        public void LoadAiSettings_NoWebResource_ReturnsEmpty()
        {
            var service = Substitute.For<IOrganizationService>();
            // Return an empty web resource to avoid XmlException on empty content
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { wr }));
            var settings = OtherSupport.LoadAiSettings(service);
            Assert.IsNotNull(settings);
        }

        [TestMethod]
        public void LoadAiSettings_ValidJson_ReturnsSettings()
        {
            var service = MockAppSettingsService("{\"ai\":{\"selectedProvider\":\"google\",\"providers\":{\"google\":{\"enabled\":true,\"baseUrl\":\"u\",\"apiKey\":\"k\",\"modelName\":\"g\"},\"openai\":{},\"azure\":{}}}}");
            var settings = OtherSupport.LoadAiSettings(service);
            Assert.AreEqual("google", settings.selectedProvider);
        }

        [TestMethod]
        public void LoadAiSettings_EmptyJson_ReturnsDefaults()
        {
            var service = MockAppSettingsService("{}");
            var settings = OtherSupport.LoadAiSettings(service);
            Assert.IsNotNull(settings);
        }

        [TestMethod]
        public void LoadDictionary_EmptyWebResource_ReturnsEmpty()
        {
            var service = Substitute.For<IOrganizationService>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection());
            var dict = OtherSupport.LoadDictionary(service);
            Assert.IsNotNull(dict);
        }

        [TestMethod]
        public void LoadDictionary_InvalidXml_ReturnsEmpty()
        {
            var service = Substitute.For<IOrganizationService>();
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not valid xml"));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { wr }));
            var dict = OtherSupport.LoadDictionary(service);
            Assert.IsNotNull(dict);
        }

        [TestMethod]
        public void LoadDictionary_ValidXml_ParsesEntries()
        {
            var service = Substitute.For<IOrganizationService>();
            var wr = new Entity("webresource") { Id = Guid.NewGuid() };
            var xml = "<dictionary sourceLcid=\"1033\"><entry><sourceText>Hello</sourceText><target lcid=\"1033\">Bonjour</target></entry></dictionary>";
            wr["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xml));
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection(new[] { wr }));
            var dict = OtherSupport.LoadDictionary(service);
            Assert.IsNotNull(dict);
        }

        [TestMethod]
        public void IsDictionarySourceMatch_NullDict_ReturnsFalse()
        {
            Assert.IsFalse(OtherSupport.IsDictionarySourceMatch(null, "1033"));
        }

        [TestMethod]
        public void IsDictionarySourceMatch_EmptySource_ReturnsTrue()
        {
            var dict = new OtherDictionaryModel();
            Assert.IsTrue(OtherSupport.IsDictionarySourceMatch(dict, "1033"));
        }

        [TestMethod]
        public void IsDictionarySourceMatch_MatchingSource_ReturnsTrue()
        {
            var dict = new OtherDictionaryModel { SourceLcid = "1033" };
            Assert.IsTrue(OtherSupport.IsDictionarySourceMatch(dict, "1033"));
        }

        [TestMethod]
        public void IsDictionarySourceMatch_DifferentSource_ReturnsFalse()
        {
            var dict = new OtherDictionaryModel { SourceLcid = "1033" };
            Assert.IsFalse(OtherSupport.IsDictionarySourceMatch(dict, "1043"));
        }

        [TestMethod]
        public void IsProviderConfigured_NullProvider_ReturnsFalse()
        {
            Assert.IsFalse(OtherSupport.IsProviderConfigured(null));
        }

        [TestMethod]
        public void IsProviderConfigured_Disabled_ReturnsFalse()
        {
            Assert.IsFalse(OtherSupport.IsProviderConfigured(new OtherProviderConfig()));
        }

        [TestMethod]
        public void IsProviderConfigured_MissingFields_ReturnsFalse()
        {
            Assert.IsFalse(OtherSupport.IsProviderConfigured(new OtherProviderConfig { enabled = true }));
        }

        [TestMethod]
        public void IsProviderConfigured_AllFields_ReturnsTrue()
        {
            Assert.IsTrue(OtherSupport.IsProviderConfigured(new OtherProviderConfig { enabled = true, baseUrl = "u", apiKey = "k", modelName = "m" }));
        }

        [TestMethod]
        public void GetProvider_Google_ReturnsGoogleConfig()
        {
            var settings = new OtherAiSettings();
            Assert.AreEqual(settings.providers.google, OtherSupport.GetProvider(settings, "google"));
        }

        [TestMethod]
        public void GetProvider_OpenAI_ReturnsOpenAIConfig()
        {
            var settings = new OtherAiSettings();
            Assert.AreEqual(settings.providers.openai, OtherSupport.GetProvider(settings, "openai"));
        }

        [TestMethod]
        public void GetProvider_Azure_ReturnsAzureConfig()
        {
            var settings = new OtherAiSettings();
            Assert.AreEqual(settings.providers.azure, OtherSupport.GetProvider(settings, "azure"));
        }

        [TestMethod]
        public void GetProvider_Unknown_ReturnsNull()
        {
            var settings = new OtherAiSettings();
            Assert.IsNull(OtherSupport.GetProvider(settings, "unknown"));
        }

        [TestMethod]
        public void Deserialize_ValidJson_ReturnsObject()
        {
            var obj = OtherSupport.Deserialize<OtherInput>("{\"operation\":\"test\"}");
            Assert.IsNotNull(obj);
            Assert.AreEqual("test", obj.operation);
        }

        [TestMethod]
        public void Deserialize_NullJson_Throws()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => OtherSupport.Deserialize<OtherInput>("null"));
        }

        [TestMethod]
        public void Normalize_LowercasesAndTrims()
        {
            Assert.AreEqual("abc", OtherSupport.Normalize("  ABC  "));
        }

        [TestMethod]
        public void NormalizeProvider_GoogleVariants()
        {
            Assert.AreEqual("google", OtherSupport.NormalizeProvider("gemini"));
            Assert.AreEqual("google", OtherSupport.NormalizeProvider("google-gemini"));
            Assert.AreEqual("google", OtherSupport.NormalizeProvider("GOOGLE"));
        }

        [TestMethod]
        public void NormalizeProvider_AzureVariants()
        {
            Assert.AreEqual("azure", OtherSupport.NormalizeProvider("azure-foundry"));
            Assert.AreEqual("azure", OtherSupport.NormalizeProvider("azurefoundry"));
        }

        [TestMethod]
        public void NormalizeProvider_OpenAIVariants()
        {
            Assert.AreEqual("openai", OtherSupport.NormalizeProvider("openai-compatible"));
            Assert.AreEqual("openai", OtherSupport.NormalizeProvider("openai_compatible"));
        }

        [TestMethod]
        public void NormalizeProvider_Unknown_ReturnsAsIs()
        {
            Assert.AreEqual("custom", OtherSupport.NormalizeProvider("custom"));
        }

        [TestMethod]
        public void StringValue_NullReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, OtherSupport.StringValue(null));
        }

        [TestMethod]
        public void StringValue_NonNullReturnsValue()
        {
            Assert.AreEqual("x", OtherSupport.StringValue("x"));
        }

        [TestMethod]
        public void GetString_NullReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, OtherSupport.GetString(null));
        }

        [TestMethod]
        public void GetString_ObjectToString()
        {
            Assert.AreEqual("42", OtherSupport.GetString(42));
        }

        [TestMethod]
        public void TrimTrailingSlash_RemovesTrailing()
        {
            Assert.AreEqual("path", OtherSupport.TrimTrailingSlash("path/"));
            Assert.AreEqual("path", OtherSupport.TrimTrailingSlash("path///"));
            Assert.AreEqual("path", OtherSupport.TrimTrailingSlash("path"));
        }

        [TestMethod]
        public void GetPath_DictionaryKey_ReturnsValue()
        {
            var dict = new Dictionary<string, object> { { "a", 1 } };
            Assert.AreEqual(1, OtherSupport.GetPath(dict, "a"));
        }

        [TestMethod]
        public void GetPath_DictionaryKeyMissing_ReturnsNull()
        {
            var dict = new Dictionary<string, object> { { "a", 1 } };
            Assert.IsNull(OtherSupport.GetPath(dict, "b"));
        }

        [TestMethod]
        public void GetPath_NullCurrent_ReturnsNull()
        {
            Assert.IsNull(OtherSupport.GetPath(null, "a"));
        }

        [TestMethod]
        public void GetPath_ListIndex_ReturnsValue()
        {
            var list = new List<object> { "a", "b", "c" };
            Assert.AreEqual("b", OtherSupport.GetPath(list, 1));
        }

        [TestMethod]
        public void GetPath_ListOutOfRange_ReturnsNull()
        {
            var list = new List<object> { "a" };
            Assert.IsNull(OtherSupport.GetPath(list, 5));
        }

        [TestMethod]
        public void GetPath_NestedPath_ReturnsValue()
        {
            var dict = new Dictionary<string, object> { { "a", new Dictionary<string, object> { { "b", 42 } } } };
            Assert.AreEqual(42, OtherSupport.GetPath(dict, "a", "b"));
        }

        [TestMethod]
        public void BuildLookupKey_Normalizes()
        {
            Assert.AreEqual("hello world", OtherSupport.BuildLookupKey("Hello World"));
            Assert.AreEqual("hello world", OtherSupport.BuildLookupKey("  Hello World  "));
            Assert.AreEqual("hello world", OtherSupport.BuildLookupKey("Hello&nbsp;World"));
        }

        [TestMethod]
        public void BuildLookupKey_StripsHtmlTags()
        {
            Assert.AreEqual("hello", OtherSupport.BuildLookupKey("<b>Hello</b>"));
        }
    }
}
