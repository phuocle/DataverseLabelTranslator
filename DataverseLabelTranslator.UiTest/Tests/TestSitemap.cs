using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestSitemap
    {
        [TestMethod]
        public void CanSaveSitemapDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveSitemapDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Sitemap.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Sitemap");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Sitemap records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Sitemap record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.Sitemap, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing child sitemap record {childRecordId} with token {token}.");
            TranslatorApp.SetTranslations(childRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, displayTexts);
            TranslatorApp.AssertTranslations(childRecordId, displayTexts, "Saved Sitemap Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Sitemap Display Text values saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveSitemapDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveSitemapDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Sitemap Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Sitemap");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Sitemap Description records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Sitemap record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var descriptions = UiTestData.CreateDescriptionValues(
                UiTestData.Sitemap,
                UiTestData.ChildNode,
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Description for sitemap record {childRecordId}.");
            TranslatorApp.SetTranslations(childRecordId, descriptions);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Description values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, descriptions);
            TranslatorApp.AssertTranslations(childRecordId, descriptions, "Saved Sitemap Description mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Sitemap Description values saved, reloaded and verified.");
        }
    }
}
