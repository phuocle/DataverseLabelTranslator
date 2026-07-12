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

            Assert.AreEqual("PHUOC LE (PHUOCLE)", TranslatorApp.GetSelectedSolutionText());
            Assert.AreEqual("globalOptionSet", TranslatorApp.GetSelectedType());
            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Global Option Set record.");

            var recordKey = TranslatorApp.GetFirstEditableRecordKey();
            var backup = TranslatorApp.GetTranslations(recordKey);
            var token = Guid.NewGuid().ToString("N").Substring(0, 8);
            var randomValues = new TranslationValues(
                $"UI-TEST-EN-{token}",
                $"UI-TEST-JP-{token}",
                $"UI-TEST-VN-{token}");

            try
            {
                TranslatorApp.SetTranslations(recordKey, randomValues);
                TranslatorApp.SaveAndWaitForTranslations(recordKey, randomValues);
                TranslatorApp.AssertTranslations(recordKey, randomValues, "Saved value mismatch for");
            }
            finally
            {
                TranslatorApp.RestoreTranslations(recordKey, backup);
                TranslatorApp.AssertTranslations(recordKey, backup, "Restored value mismatch for");
            }
        }
    }
}
