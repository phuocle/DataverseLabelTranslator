using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestView
    {
        [TestMethod]
        public void CanSaveViewDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveViewDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Views Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Views");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Views Description records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one View record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var descriptions = UiTestData.CreateDescriptionValues(
                UiTestData.View,
                UiTestData.ChildNode,
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Description for view record {childRecordId}.");
            TranslatorApp.SetTranslations(childRecordId, descriptions);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Description values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, descriptions);
            TranslatorApp.AssertTranslations(childRecordId, descriptions, "Saved View Description mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Description values saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveViewDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveViewDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Views.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Views");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Views records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one View record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.View, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing child view record {childRecordId} with token {token}.");
            TranslatorApp.SetTranslations(childRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, displayTexts);
            TranslatorApp.AssertTranslations(childRecordId, displayTexts, "Saved View Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Display Text values saved, reloaded and verified.");
        }
    }
}
