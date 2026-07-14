using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class TestEntityMetadata
    {
        [TestMethod]
        public void CanSaveEntityMetadataDisplayText()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveEntityMetadataDisplayText));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity Metadata Display Text.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Entity Metadata");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Entity Metadata Display Text records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Entity Metadata record.");

            var displayTextRecordId = TranslatorApp.GetEditableRecordIdBySchemaName("Display Text");
            var collectionNameRecordId = TranslatorApp.GetEditableRecordIdBySchemaName("Collection Name");
            var token = UiTestData.CreateTimestampToken();
            var displayTexts = UiTestData.CreateDescriptionValues(
                UiTestData.EntityMetadata,
                "DisplayText",
                token);
            var collectionNames = UiTestData.CreateDescriptionValues(
                UiTestData.EntityMetadata,
                "CollectionName",
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Display Text for entity metadata record {displayTextRecordId}.");
            TranslatorApp.SetTranslations(displayTextRecordId, displayTexts);
            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Collection Name for entity metadata record {collectionNameRecordId}.");
            TranslatorApp.SetTranslations(collectionNameRecordId, collectionNames);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN Entity Metadata Display Text and Collection Name values.");
            TranslatorApp.SaveAndWaitForTranslations(
                displayTextRecordId,
                displayTexts,
                collectionNameRecordId,
                collectionNames);
            TranslatorApp.AssertTranslations(displayTextRecordId, displayTexts, "Saved Entity Metadata Display Text mismatch for");
            TranslatorApp.AssertTranslations(collectionNameRecordId, collectionNames, "Saved Entity Metadata Collection Name mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Display Text and Collection Name values saved, reloaded and verified.");
        }

        [TestMethod]
        public void CanSaveEntityMetadataDescription()
        {
            TranslatorApp.Open();
            TranslatorApp.ClearBrowserConsole(nameof(CanSaveEntityMetadataDescription));
            TranslatorApp.WriteBrowserConsole("PASS", "Translator App opened and navigation completed.");
            TranslatorApp.WriteBrowserConsole("INFO", "Selecting Entity Metadata Description.");
            TranslatorApp.SelectSolution("PHUOC LE (PHUOCLE)");
            TranslatorApp.SelectEntity("None");
            TranslatorApp.SelectType("Entity Metadata");
            TranslatorApp.SelectComponent("Description");
            TranslatorApp.WriteBrowserConsole("INFO", "Loading Entity Metadata Description records.");
            TranslatorApp.Load();

            Assert.IsTrue(TranslatorApp.GetRecordCount() > 0, "Expected at least one Entity Metadata record.");

            var descriptionRecordId = TranslatorApp.GetEditableRecordIdBySchemaName("Description");
            var collectionNameRecordId = TranslatorApp.GetEditableRecordIdBySchemaName("Collection Name");
            var token = UiTestData.CreateTimestampToken();
            var descriptions = UiTestData.CreateDescriptionValues(
                UiTestData.EntityMetadata,
                "Description",
                token);
            var collectionNames = UiTestData.CreateDescriptionValues(
                UiTestData.EntityMetadata,
                "CollectionName",
                token);

            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Description for entity metadata record {descriptionRecordId}.");
            TranslatorApp.SetTranslations(descriptionRecordId, descriptions);
            TranslatorApp.WriteBrowserConsole("INFO", $"Setting child Collection Name for entity metadata record {collectionNameRecordId}.");
            TranslatorApp.SetTranslations(collectionNameRecordId, collectionNames);
            TranslatorApp.WriteBrowserConsole("INFO", "Saving EN, JP and VN Entity Metadata Description and Collection Name values.");
            TranslatorApp.SaveAndWaitForTranslations(
                descriptionRecordId,
                descriptions,
                collectionNameRecordId,
                collectionNames);
            TranslatorApp.AssertTranslations(descriptionRecordId, descriptions, "Saved Entity Metadata Description mismatch for");
            TranslatorApp.AssertTranslations(collectionNameRecordId, collectionNames, "Saved Entity Metadata Collection Name mismatch for");
            TranslatorApp.WriteBrowserConsole("PASS", "Child Description and Collection Name values saved, reloaded and verified.");
        }
    }
}
