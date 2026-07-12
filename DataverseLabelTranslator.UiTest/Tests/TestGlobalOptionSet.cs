using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestGlobalOptionSet
    {
        [TestMethod]
        public void CanSaveAndRestoreGlobalOptionSetTranslations()
        {
            TranslatorApp.Open();
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Global Option Set");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Global Option Set record.");

            var recordId = TranslatorApp.GetFirstEditableRecordId();
            var backup = TranslatorApp.GetTranslations(recordId);
            var token = Guid.NewGuid().ToString("N").Substring(0, 8);
            var randomValues = new TranslationValues(
                $"UI-TEST-EN-{token}",
                $"UI-TEST-JP-{token}",
                $"UI-TEST-VN-{token}");

            try
            {
                TranslatorApp.SetTranslations(recordId, randomValues);
                TranslatorApp.SaveAndWaitForTranslations(recordId, randomValues);
                TranslatorApp.AssertTranslations(recordId, randomValues, "Saved value mismatch for");
            }
            finally
            {
                TranslatorApp.RestoreTranslations(recordId, backup);
                TranslatorApp.AssertTranslations(recordId, backup, "Restored value mismatch for");
            }
        }
    }
}
