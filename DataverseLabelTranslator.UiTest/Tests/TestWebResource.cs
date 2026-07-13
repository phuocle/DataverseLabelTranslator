using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestWebResource
    {
        [TestMethod]
        public void CanSaveWebResourceDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveWebResourceDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Web Resources Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Web Resources");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Web Resources Description records.");
            TranslatorApp.Load();

            var parentRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var descriptions = UiTestData.CreateDescriptionValues(
                UiTestData.WebResource,
                UiTestData.ParentNode,
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting parent Description for record {parentRecordId}.");
            TranslatorApp.SetTranslations(parentRecordId, descriptions);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN parent Description values.");
            TranslatorApp.SaveAndWaitForTranslations(parentRecordId, descriptions);
            TranslatorApp.AssertTranslations(parentRecordId, descriptions, "Saved Web Resource Description mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Parent Description values saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveWebResourceDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveWebResourceDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Web Resources.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Web Resources");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Web Resources records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Web Resource record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.WebResource, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing child record {childRecordId} with token {token}.");
            TranslatorApp.SetTranslations(childRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, displayTexts);
            TranslatorApp.AssertTranslations(childRecordId, displayTexts, "Saved Web Resource Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Display Text values saved, reloaded and verified.");
        }
    }
}
