using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestGlobalOptionSet
    {
        [TestMethod]
        public void CanSaveGlobalOptionSetDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveGlobalOptionSetDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Global Option Set Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Global Option Set");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Global Option Set Description records.");
            TranslatorApp.Load();

            TranslatorApp.GetFirstEditableParentAndChildRecordIds(out var parentRecordId, out var childRecordId);
            var token = UiTestData.CreateTimestampToken();
            var parentDescriptions = UiTestData.CreateDescriptionValues(
                UiTestData.GlobalOptionSet,
                UiTestData.ParentNode,
                token);
            var childDescriptions = UiTestData.CreateDescriptionValues(
                UiTestData.GlobalOptionSet,
                UiTestData.ChildNode,
                token);
            TranslatorApp.WriteBrowserConsole("INFO", $"Setting parent Description for record {parentRecordId}.");
            TranslatorApp.SetTranslations(parentRecordId, parentDescriptions);
            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Description for record {childRecordId}.");
            TranslatorApp.SetTranslations(childRecordId, childDescriptions);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving parent and child Descriptions.");
            TranslatorApp.SaveAndWaitForTranslations(
                parentRecordId,
                parentDescriptions,
                childRecordId,
                childDescriptions);
            TranslatorApp.WriteBrowserConsole(
                "PASS",
                "Parent and child EN, JP and VN Descriptions saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveGlobalOptionSetDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveGlobalOptionSetDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting solution PHUOC LE (PHUOCLE).");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity None and Type Global Option Set.");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Global Option Set");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Global Option Set records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Global Option Set record.");

            var childRecordId = TranslatorApp.GetFirstEditableRecordId();
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDisplayTextValues(UiTestData.GlobalOptionSet, token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Editing child record {childRecordId} with token {token}.");
            TranslatorApp.SetTranslations(childRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN child Display Text values.");
            TranslatorApp.SaveAndWaitForTranslations(childRecordId, displayTexts);
            TranslatorApp.AssertTranslations(childRecordId, displayTexts, "Saved Display Text mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Display Text values saved, reloaded and verified.");
        }
    }
}
