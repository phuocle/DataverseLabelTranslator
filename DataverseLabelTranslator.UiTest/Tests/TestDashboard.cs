using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestDashboard
    {
        [TestMethod]
        public void CanSaveDashboardDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveDashboardDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Dashboards.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Dashboards");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading flat Dashboard records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Dashboard record.");

            var parentRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.Dashboard, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing flat dashboard record {parentRecordId} with token {token}.");
            TranslatorApp.SetTranslations(parentRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN Dashboard Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(parentRecordId, displayTexts);
            TranslatorApp.AssertTranslations(parentRecordId, displayTexts, "Saved Dashboard Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Flat Dashboard Display Text values saved, reloaded and verified.");
        }
    }
}
