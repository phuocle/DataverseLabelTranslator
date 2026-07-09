using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using NSubstitute;
using System;
using System.Reflection;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    [TestClass]
    public class OtherTranslateTests
    {
        [TestMethod]
        public void Execute_Throws_WhenProviderMissing()
        {
            var service = Substitute.For<IOrganizationService>();
            var instance = (OtherTranslate)Activator.CreateInstance(typeof(OtherTranslate), nonPublic: true);
            var json = "{\"provider\":\"\",\"fromLanguage\":\"en\",\"toLanguage\":\"de\",\"items\":[{\"text\":\"Hello\"}]}";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => instance.Execute(service, json),
                "provider is required");
        }

        [TestMethod]
        public void Execute_Throws_WhenLanguagesMissing()
        {
            var service = Substitute.For<IOrganizationService>();
            var instance = (OtherTranslate)Activator.CreateInstance(typeof(OtherTranslate), nonPublic: true);
            var json = "{\"provider\":\"openai\",\"fromLanguage\":\"\",\"toLanguage\":\"\",\"items\":[{\"text\":\"Hello\"}]}";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => instance.Execute(service, json),
                "languages are required");
        }

        [TestMethod]
        public void Execute_Throws_WhenLanguagesSame()
        {
            var service = Substitute.For<IOrganizationService>();
            var instance = (OtherTranslate)Activator.CreateInstance(typeof(OtherTranslate), nonPublic: true);
            var json = "{\"provider\":\"openai\",\"fromLanguage\":\"en\",\"toLanguage\":\"en\",\"items\":[{\"text\":\"Hello\"}]}";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => instance.Execute(service, json),
                "must be different");
        }

        [TestMethod]
        public void Execute_Throws_WhenItemsEmpty()
        {
            var service = Substitute.For<IOrganizationService>();
            var instance = (OtherTranslate)Activator.CreateInstance(typeof(OtherTranslate), nonPublic: true);
            var json = "{\"provider\":\"openai\",\"fromLanguage\":\"en\",\"toLanguage\":\"de\",\"items\":[]}";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => instance.Execute(service, json),
                "phrases are required");
        }

        [TestMethod]
        public void Execute_Throws_WhenItemsBlank()
        {
            var service = Substitute.For<IOrganizationService>();
            var instance = (OtherTranslate)Activator.CreateInstance(typeof(OtherTranslate), nonPublic: true);
            var json = "{\"provider\":\"openai\",\"fromLanguage\":\"en\",\"toLanguage\":\"de\",\"items\":[{\"text\":\"\"}]}";
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => instance.Execute(service, json),
                "phrases are required");
        }

        [TestMethod]
        public void NormalizeTranslateItems_Empty_ReturnsEmpty()
        {
            var m = typeof(OtherTranslate).GetMethod("NormalizeTranslateItems", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m);
            var input = new OtherInput();
            var result = (System.Collections.Generic.List<OtherTranslateItemInput>)m.Invoke(null, new object[] { input });
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void NormalizeTranslateItems_WithItems_ReturnsNormalized()
        {
            var m = typeof(OtherTranslate).GetMethod("NormalizeTranslateItems", BindingFlags.NonPublic | BindingFlags.Static);
            var input = new OtherInput
            {
                items = new System.Collections.Generic.List<OtherTranslateItemInput>
                {
                    new OtherTranslateItemInput { text = "Hello" },
                    new OtherTranslateItemInput { text = "World" }
                }
            };
            var result = (System.Collections.Generic.List<OtherTranslateItemInput>)m.Invoke(null, new object[] { input });
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void NormalizeTranslateItems_WithPhrases_ReturnsNormalized()
        {
            var m = typeof(OtherTranslate).GetMethod("NormalizeTranslateItems", BindingFlags.NonPublic | BindingFlags.Static);
            var input = new OtherInput
            {
                phrases = new System.Collections.Generic.List<string> { "Hello", "World" }
            };
            var result = (System.Collections.Generic.List<OtherTranslateItemInput>)m.Invoke(null, new object[] { input });
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Hello", result[0].text);
        }

        [TestMethod]
        public void BuildPrompt_EmptyCustomPrompt()
        {
            var m = typeof(OtherTranslate).GetMethod("BuildPrompt", BindingFlags.NonPublic | BindingFlags.Static);
            var prompt = (string)m.Invoke(null, new object[] { "en", "de", 2, "" });
            Assert.IsTrue(prompt.Contains("Translate"));
            Assert.IsTrue(prompt.Contains("en"));
            Assert.IsTrue(prompt.Contains("de"));
            Assert.IsTrue(prompt.Contains("2"));
        }

        [TestMethod]
        public void BuildPrompt_WithCustomPrompt()
        {
            var m = typeof(OtherTranslate).GetMethod("BuildPrompt", BindingFlags.NonPublic | BindingFlags.Static);
            var prompt = (string)m.Invoke(null, new object[] { "en", "de", 3, "Keep formal" });
            Assert.IsTrue(prompt.Contains("Keep formal"));
        }

        [TestMethod]
        public void StripCodeFence_PlainText_ReturnsAsIs()
        {
            var m = typeof(OtherTranslate).GetMethod("StripCodeFence", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)m.Invoke(null, new object[] { "[\"a\",\"b\"]" });
            Assert.AreEqual("[\"a\",\"b\"]", result);
        }

        [TestMethod]
        public void StripCodeFence_WithFences_Strips()
        {
            var m = typeof(OtherTranslate).GetMethod("StripCodeFence", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)m.Invoke(null, new object[] { "```json\n[\"a\",\"b\"]\n```" });
            Assert.IsTrue(result.Contains("[\"a\",\"b\"]"));
        }

        [TestMethod]
        public void ParseTranslationArray_ValidJson_ReturnsList()
        {
            var m = typeof(OtherTranslate).GetMethod("ParseTranslationArray", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (System.Collections.Generic.List<string>)m.Invoke(null, new object[] { "[\"a\",\"b\"]" });
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void ParseTranslationArray_WithFences_ReturnsList()
        {
            var m = typeof(OtherTranslate).GetMethod("ParseTranslationArray", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (System.Collections.Generic.List<string>)m.Invoke(null, new object[] { "```json\n[\"x\",\"y\"]\n```" });
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void ParseTranslationArray_Invalid_Throws()
        {
            var m = typeof(OtherTranslate).GetMethod("ParseTranslationArray", BindingFlags.NonPublic | BindingFlags.Static);
            AdapterTestHelpers.ExpectException<System.Reflection.TargetInvocationException>(
                () => m.Invoke(null, new object[] { "not-json" }));
        }

        [TestMethod]
        public void BuildOpenAiChatUrl_Empty_Throws()
        {
            var m = typeof(OtherTranslate).GetMethod("BuildOpenAiChatUrl", BindingFlags.NonPublic | BindingFlags.Static);
            AdapterTestHelpers.ExpectException<System.Reflection.TargetInvocationException>(
                () => m.Invoke(null, new object[] { "" }));
        }

        [TestMethod]
        public void BuildOpenAiChatUrl_NoSuffix_Appends()
        {
            var m = typeof(OtherTranslate).GetMethod("BuildOpenAiChatUrl", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)m.Invoke(null, new object[] { "https://api.openai.com/v1" });
            Assert.IsTrue(result.EndsWith("/chat/completions"));
        }

        [TestMethod]
        public void BuildOpenAiChatUrl_WithSuffix_Keeps()
        {
            var m = typeof(OtherTranslate).GetMethod("BuildOpenAiChatUrl", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (string)m.Invoke(null, new object[] { "https://api.openai.com/v1/chat/completions" });
            Assert.AreEqual("https://api.openai.com/v1/chat/completions", result);
        }
    }
}
