using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestRelationship
    {
        [TestMethod]
        public void CanSaveRelationshipDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveRelationshipDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Relationships.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Relationships");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Relationships records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Relationship record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.Relationship, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing child relationship record {childRecordId} with token {token}.");
            TranslatorApp.SetTranslations(childRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, displayTexts);
            TranslatorApp.AssertTranslations(childRecordId, displayTexts, "Saved Relationship Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Relationship Display Text values saved, reloaded and verified.");
        }
    }
}
