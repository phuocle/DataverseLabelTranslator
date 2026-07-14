using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestFormMetadata
    {
        [TestMethod]
        public void CanSaveFormMetadataDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveFormMetadataDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Form Metadata Display Text.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Form Metadata");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Form Metadata Display Text records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Form Metadata record.");

            var formRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.FormMetadata, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting one Form Metadata Display Text row {formRecordId}.");
            TranslatorApp.SetTranslations(formRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN Form Metadata Display Text values for one row.");
            TranslatorApp.SaveAndWaitForTranslations(formRecordId, displayTexts);
            TranslatorApp.AssertTranslations(formRecordId, displayTexts, "Saved Form Metadata Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "One Display Text row saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveFormMetadataDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveFormMetadataDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Form Metadata Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Form Metadata");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Form Metadata Description records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Form Metadata record.");

            var formRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var descriptions = UiTestData.CreateDescriptionValues(
                UiTestData.FormMetadata,
                UiTestData.ChildNode,
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting one Form Metadata Description row {formRecordId}.");
            TranslatorApp.SetTranslations(formRecordId, descriptions);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN Form Metadata Description values for one row.");
            TranslatorApp.SaveAndWaitForTranslations(formRecordId, descriptions);
            TranslatorApp.AssertTranslations(formRecordId, descriptions, "Saved Form Metadata Description mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "One Description row saved, reloaded and verified.");
        }
    }
}
