using DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Test.CustomActions.Synchronous
{
    /// <summary>
    /// Unit tests for EasyTranslator models and shared base-class helpers.
    /// A test-only subclass exposes the protected static helpers of
    /// <see cref="EasyTranslatorAdapterBase"/> so they can be exercised directly.
    /// </summary>
    [TestClass]
    public class EasyTranslatorModelsCoverageTests
    {
        private class BaseProxy : EasyTranslatorAdapterBase
        {
            public static string CallBuildGridKey(params string[] parts) => BuildGridKey(parts);

            public static string[] CallParseGridKey(string gridKey, string type, int min)
                => ParseGridKey(gridKey, type, min);

            public static List<EasyTranslatorLabelChange> CallGetValidLabelChanges(EasyTranslatorChangedRowInput row)
                => GetValidLabelChanges(row);

            public static Guid? CallParseSolutionId(string solutionId, string label)
                => ParseSolutionId(solutionId, label);

            public static int? CallGetOptionValue(Entity entity, string attr) => GetOptionValue(entity, attr);

            public static bool? CallGetManagedBooleanValue(Entity entity, string attr)
                => GetManagedBooleanValue(entity, attr);

            public static bool CallIsDescriptionComponent(string component) => IsDescriptionComponent(component);
            public static bool CallIsDisplayTextComponent(string component) => IsDisplayTextComponent(component);

            public static List<Entity> CallToList(EntityCollection collection) => ToList(collection);

            public static void CallAddLabelValues(EasyTranslatorGridRowOutput row, Label label)
                => AddLabelValues(row, label);

            public static void CallAddPublishTarget(EasyTranslatorSaveOutput output, HashSet<string> seen, string kind, string id)
                => AddPublishTarget(output, seen, kind, id);

            public static List<string> CallGetPublishTargetIds(EasyTranslatorPublishInput input, string kind, bool requireGuid)
                => GetPublishTargetIds(input, kind, requireGuid);

            public static EasyTranslatorSaveOutput CallToPublishOutput(string kind, List<string> ids)
                => ToPublishOutput(kind, ids);

            // Unused interface methods to satisfy no compile errors (base is abstract, not interface)
        }

        [TestMethod]
        public void GridRow_GetSetProperties_RoundTrip()
        {
            var row = new EasyTranslatorGridRowOutput();
            row.Recid = "a";
            row.GridKey = "k";
            row.SchemaName = "s";
            row.RowType = "t";
            row.IsEditable = true;
            row.IsTranslatable = false;
            row.Ai = new EasyTranslatorAiOutput { include = true, location = "loc" };
            row.Children = null;
            row.Children = new List<EasyTranslatorGridRowOutput>();

            Assert.AreEqual("a", row.Recid);
            Assert.AreEqual("k", row.GridKey);
            Assert.AreEqual("s", row.SchemaName);
            Assert.AreEqual("t", row.RowType);
        }

        [TestMethod]
        public void GridRow_SetLanguageValue_IgnoresEmptyCode()
        {
            var row = new EasyTranslatorGridRowOutput();
            row.SetLanguageValue(" ", "value");
            Assert.IsFalse(row.ContainsKey(" "));
            row.SetLanguageValue("1033", null);
            Assert.AreEqual(string.Empty, row["1033"]);
            row.SetLanguageValue("1041", "ja");
            Assert.AreEqual("ja", row["1041"]);
        }

        [TestMethod]
        public void GridRow_GetString_ReturnsEmptyForMissingOrNUll()
        {
            var row = new EasyTranslatorGridRowOutput();
            Assert.AreEqual(string.Empty, row.Recid);
            row["recid"] = null;
            Assert.AreEqual(string.Empty, row.Recid);
            row["recid"] = 42;
            Assert.AreEqual("42", row.Recid);
        }

        [TestMethod]
        public void GridRow_ChildrenSetter_SkipsEmptyOrNull()
        {
            var row = new EasyTranslatorGridRowOutput();
            row.Children = null;
            Assert.IsFalse(row.ContainsKey("children"));
            row.Children = new List<EasyTranslatorGridRowOutput>();
            Assert.IsFalse(row.ContainsKey("children"));
            row.Children = new List<EasyTranslatorGridRowOutput> { new EasyTranslatorGridRowOutput { Recid = "c" } };
            Assert.IsTrue(row.ContainsKey("children"));
        }

        [TestMethod]
        public void BuildGridKey_EncodesAndJoins()
        {
            var key = BaseProxy.CallBuildGridKey("a b", "c|d", "e");
            Assert.AreEqual("a%20b|c%7Cd|e", key);
        }

        [TestMethod]
        public void ParseGridKey_DecodesAndValidatesType()
        {
            var key = BaseProxy.CallBuildGridKey("forms", Guid.NewGuid().ToString("D"), "label");
            var parts = BaseProxy.CallParseGridKey(key, "forms", 3);
            Assert.AreEqual(3, parts.Length);
            Assert.AreEqual("forms", parts[0]);
        }

        [TestMethod]
        public void ParseGridKey_ThrowsWhenEmpty()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => BaseProxy.CallParseGridKey("", "forms", 2),
                "gridKey is required");
        }

        [TestMethod]
        public void ParseGridKey_ThrowsWhenTypeMismatch()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => BaseProxy.CallParseGridKey("other|x", "forms", 1),
                "forms");
        }

        [TestMethod]
        public void ParseGridKey_ThrowsWhenTooFewParts()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => BaseProxy.CallParseGridKey("forms|x", "forms", 5),
                "forms");
        }

        [TestMethod]
        public void GetValidLabelChanges_FiltersNonNumericKeys()
        {
            var row = new EasyTranslatorChangedRowInput
            {
                gridKey = "k",
                changes = new Dictionary<string, string>
                {
                    { "1033", "en" },
                    { "bad", "x" },
                    { "1041", "ja" }
                }
            };
            var changes = BaseProxy.CallGetValidLabelChanges(row);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual("en", changes[0].Label);
        }

        [TestMethod]
        public void GetValidLabelChanges_NullRow_ReturnsEmpty()
        {
            Assert.AreEqual(0, BaseProxy.CallGetValidLabelChanges(null).Count);
        }

        [TestMethod]
        public void GetValidLabelChanges_NullChangesMap_ReturnsEmpty()
        {
            var row = new EasyTranslatorChangedRowInput { gridKey = "k", changes = null };
            Assert.AreEqual(0, BaseProxy.CallGetValidLabelChanges(row).Count);
        }

        [TestMethod]
        public void ParseSolutionId_AllOrEmpty_ReturnsNull()
        {
            Assert.IsNull(BaseProxy.CallParseSolutionId("all", "x"));
            Assert.IsNull(BaseProxy.CallParseSolutionId("", "x"));
            Assert.IsNull(BaseProxy.CallParseSolutionId(null, "x"));
        }

        [TestMethod]
        public void ParseSolutionId_InvalidGuid_Throws()
        {
            AdapterTestHelpers.ExpectException<InvalidPluginExecutionException>(
                () => BaseProxy.CallParseSolutionId("nope", "x"),
                "GUID");
        }

        [TestMethod]
        public void ParseSolutionId_ValidGuid_ReturnsValue()
        {
            var id = Guid.NewGuid();
            Assert.AreEqual(id, BaseProxy.CallParseSolutionId(id.ToString("D"), "x"));
        }

        [TestMethod]
        public void GetOptionValue_HandlesOptionSetIntMissingAndNull()
        {
            Assert.IsNull(BaseProxy.CallGetOptionValue(null, "x"));
            var entity = new Entity("account");
            Assert.IsNull(BaseProxy.CallGetOptionValue(entity, "missing"));
            entity["statecode"] = new OptionSetValue(2);
            Assert.AreEqual(2, BaseProxy.CallGetOptionValue(entity, "statecode"));
            entity["rawint"] = 5;
            Assert.AreEqual(5, BaseProxy.CallGetOptionValue(entity, "rawint"));
            entity["bad"] = "string";
            Assert.IsNull(BaseProxy.CallGetOptionValue(entity, "bad"));
        }

        [TestMethod]
        public void GetManagedBooleanValue_HandlesTypes()
        {
            Assert.IsNull(BaseProxy.CallGetManagedBooleanValue(null, "x"));
            var entity = new Entity("account");
            Assert.IsNull(BaseProxy.CallGetManagedBooleanValue(entity, "missing"));
            entity["m"] = new BooleanManagedProperty(true);
            Assert.AreEqual(true, BaseProxy.CallGetManagedBooleanValue(entity, "m"));
            entity["b"] = false;
            Assert.AreEqual(false, BaseProxy.CallGetManagedBooleanValue(entity, "b"));
            entity["bad"] = "string";
            Assert.IsNull(BaseProxy.CallGetManagedBooleanValue(entity, "bad"));
        }

        [TestMethod]
        public void IsComponent_Helpers_CompareCaseInsensitive()
        {
            Assert.IsTrue(BaseProxy.CallIsDescriptionComponent("description"));
            Assert.IsTrue(BaseProxy.CallIsDescriptionComponent("Description"));
            Assert.IsFalse(BaseProxy.CallIsDescriptionComponent("DisplayText"));
            Assert.IsTrue(BaseProxy.CallIsDisplayTextComponent("DisplayText"));
            Assert.IsTrue(BaseProxy.CallIsDisplayTextComponent("displaytext"));
            Assert.IsFalse(BaseProxy.CallIsDisplayTextComponent("Description"));
        }

        [TestMethod]
        public void ToList_HandlesNullAndEntities()
        {
            Assert.AreEqual(0, BaseProxy.CallToList(null).Count);
            var e = new Entity("account", Guid.NewGuid());
            var collection = new EntityCollection(new[] { e });
            Assert.AreEqual(1, BaseProxy.CallToList(collection).Count);
        }

        [TestMethod]
        public void AddLabelValues_SkipsNullsAndEmpty()
        {
            var row = new EasyTranslatorGridRowOutput();
            BaseProxy.CallAddLabelValues(row, null);
            Assert.AreEqual(0, row.Count);

            var label = new Label();
            label.LocalizedLabels.Add(new LocalizedLabel("en", 1033));
            label.LocalizedLabels.Add(null);
            BaseProxy.CallAddLabelValues(row, label);
            Assert.AreEqual("en", row["1033"]);
        }

        [TestMethod]
        public void AddPublishTarget_SkipsInvalidAndDuplicates()
        {
            var output = new EasyTranslatorSaveOutput();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            BaseProxy.CallAddPublishTarget(output, seen, "", "id");
            BaseProxy.CallAddPublishTarget(output, seen, "entity", "");
            Assert.AreEqual(0, output.publishTargets.Count);

            BaseProxy.CallAddPublishTarget(output, seen, "entity", "account");
            BaseProxy.CallAddPublishTarget(output, seen, "entity", "account");
            Assert.AreEqual(1, output.publishTargets.Count);
            Assert.IsTrue(output.changed);
        }

        [TestMethod]
        public void AddPublishTarget_NullOutput_DoesNotThrow()
        {
            BaseProxy.CallAddPublishTarget(null, new HashSet<string>(), "entity", "account");
        }

        [TestMethod]
        public void GetPublishTargetIds_FiltersByKindAndGuidAndDuplicates()
        {
            var input = new EasyTranslatorPublishInput
            {
                publishTargets = new List<EasyTranslatorPublishTarget>
                {
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    new EasyTranslatorPublishTarget { kind = "other", id = "contact" },
                    new EasyTranslatorPublishTarget { kind = "entity", id = "account" },
                    null
                }
            };
            var ids = BaseProxy.CallGetPublishTargetIds(input, "entity", false);
            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("account", ids[0]);

            var guidIds = BaseProxy.CallGetPublishTargetIds(input, "entity", true);
            // "account" is not a guid, so it's filtered when requireGuid=true
            Assert.AreEqual(0, guidIds.Count);
        }

        [TestMethod]
        public void GetPublishTargetIds_NullInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, BaseProxy.CallGetPublishTargetIds(null, "entity", false).Count);
        }

        [TestMethod]
        public void ToPublishOutput_AddsAndDedupes()
        {
            var output = BaseProxy.CallToPublishOutput("entity", new List<string> { "a", "a", "b" });
            Assert.AreEqual(2, output.publishTargets.Count);
            Assert.IsTrue(output.changed);

            Assert.AreEqual(0, BaseProxy.CallToPublishOutput("entity", null).publishTargets.Count);
        }

        [TestMethod]
        public void SaveOutput_Defaults()
        {
            var output = new EasyTranslatorSaveOutput();
            Assert.AreEqual(0, output.changedRowCount);
            Assert.IsFalse(output.changed);
            Assert.IsNotNull(output.publishTargets);
        }
    }
}
