using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using DataverseLabelTranslator.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DataverseLabelTranslator.Server.Test.CustomActions.Synchronous
{
    [TestClass]
    public class SharedLibCoverageTests
    {
        private sealed class SettingsDto
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public Guid Id { get; set; }
            public List<int> Values { get; set; }
        }

        [TestMethod]
        public void DevKitJson_SerializesAndDeserializesDataverseTypes()
        {
            var accountId = Guid.NewGuid();
            var contactId = Guid.NewGuid();
            var entity = new Entity("account", accountId);
            entity["name"] = "Acme\nNorth";
            entity["amount"] = new Money(12.34m);
            entity["statuscode"] = new OptionSetValue(1);
            entity["choices"] = new OptionSetValueCollection(new[] { new OptionSetValue(2), new OptionSetValue(3) });
            entity["primarycontactid"] = new EntityReference("contact", contactId) { Name = "Ada" };
            entity["alias.name"] = new AliasedValue("contact", "fullname", "Ada");
            entity["managed"] = new BooleanManagedProperty(true) { CanBeChanged = true };
            entity.FormattedValues["statuscode"] = "Active";

            var parameters = new ParameterCollection
            {
                ["entity"] = entity,
                ["items"] = new object[] { "x", 5, true },
                ["file"] = new byte[] { 1, 2, 3 },
                ["when"] = new DateTime(2026, 7, 6, 9, 10, 11, DateTimeKind.Utc),
                ["id"] = accountId
            };
            var images = new EntityImageCollection { ["PreImage"] = entity };
            var collection = new EntityCollection(new[] { entity }) { EntityName = "account" };

            var json = DevKitJson.Serialize(new Dictionary<string, object>
            {
                ["parameters"] = parameters,
                ["images"] = images,
                ["collection"] = collection,
                ["reference"] = entity.ToEntityReference()
            });
            var compact = DevKitJson.SerializeCompact(parameters);

            var deserialized = DevKitJson.Deserialize<Dictionary<string, object>>(json);
            var deserializedCompact = DevKitJson.Deserialize<ParameterCollection>(compact);

            Assert.IsTrue(json.Contains("Entity"));
            Assert.IsTrue(compact.Contains("\"_t\""));
            Assert.IsTrue(deserialized.ContainsKey("parameters"));
            Assert.IsNotNull(deserializedCompact);
        }

        [TestMethod]
        public void DevKitJson_MapsPrimitiveCollectionsAndContexts()
        {
            var id = Guid.NewGuid();
            var dto = DevKitJson.Deserialize<SettingsDto>(
                "{\"Name\":\"Translator\",\"Count\":7,\"Id\":\"" + id + "\",\"Values\":[1,2,3]}"
            );
            var mapped = DevKitJson.MapTo<SettingsDto>(new Dictionary<string, object>
            {
                ["name"] = "Mapped",
                ["count"] = 4,
                ["id"] = id.ToString("D"),
                ["values"] = new List<object> { 8, 9 }
            });

            var context = new RemoteExecutionContext();
            var contextJson = DevKitJson.SerializeContextFull(context);
            var compactContextJson = DevKitJson.SerializeContext(context);
            var nullContextJson = DevKitJson.SerializeContext(null);

            Assert.AreEqual("Translator", dto.Name);
            Assert.AreEqual(3, dto.Values.Count);
            Assert.AreEqual("Mapped", mapped.Name);
            Assert.AreEqual(2, mapped.Values.Count);
            Assert.IsTrue(contextJson.Contains("RemoteExecutionContext"));
            Assert.IsTrue(compactContextJson.Contains("\"RC\""));
            Assert.AreEqual("null", nullContextJson);
        }

        [TestMethod]
        public void ExtensionMethods_CoverEntityHelpersAndRequests()
        {
            var id = Guid.NewGuid();
            var entity = new Entity("account", id);
            entity["name"] = "Current";
            entity["statuscode"] = new OptionSetValue(2);
            entity["amount"] = new Money(42m);
            entity["primarycontactid"] = new EntityReference("contact", Guid.NewGuid());
            entity["alias.contactid"] = new AliasedValue("contact", "contactid", Guid.NewGuid());
            entity["alias.accountid"] = new AliasedValue("account", "accountid", entity.ToEntityReference());
            entity.FormattedValues["statuscode"] = "Active";

            var update = (UpdateRequest)entity.ToUpdateRequest("update-tag");
            var create = (CreateRequest)entity.ToCreateRequest("create-tag");
            var delete = (DeleteRequest)entity.ToEntityReference().ToDeleteRequest("delete-tag");
            var upsert = (UpsertRequest)entity.ToUpsertRequest("upsert-tag");

            var target = new Entity("account");
            target.MergeAttributes(entity);
            target.AddOrUpdateAttribute("new_name", "New");
            target.RemoveAttributes("new_name");

            var samePreImage = new Entity("account", id)
            {
                ["name"] = "Current",
                ["statuscode"] = new OptionSetValue(2),
                ["amount"] = new Money(42m),
                ["primarycontactid"] = entity.GetAttributeValue<EntityReference>("primarycontactid")
            };
            var changedPreImage = new Entity("account", id) { ["name"] = "Old" };

            var parameters = new ParameterCollection { ["name"] = "value" };
            var images = new EntityImageCollection { ["PreImage"] = entity };
            var compressed = "hello world".Compress();

            Assert.AreSame(entity, update.Target);
            Assert.AreEqual("update-tag", update["tag"]);
            Assert.AreSame(entity, create.Target);
            Assert.AreEqual(id, delete.Target.Id);
            Assert.AreSame(entity, upsert.Target);
            Assert.AreEqual("Active", entity.GetFormattedValue("statuscode"));
            Assert.AreEqual("Current", target.GetAttributeValue<string>("name"));
            Assert.IsFalse(target.Contains("new_name"));
            Assert.IsFalse(entity.HasChanged("name", samePreImage));
            Assert.IsTrue(entity.HasChanged("name", changedPreImage));
            Assert.IsFalse(entity.HasChanged("statuscode", samePreImage));
            Assert.IsFalse(entity.HasChanged("amount", samePreImage));
            Assert.IsFalse(entity.HasChanged("primarycontactid", samePreImage));
            Assert.IsTrue(entity.ContainsValue("name", "statuscode"));
            Assert.IsTrue(entity.ContainsAny("missing", "name"));
            Assert.AreEqual("value", parameters.GetValue<string>("name"));
            Assert.AreSame(entity, images.GetImage("PreImage"));
            Assert.AreEqual("hello world", compressed.Decompress());
            Assert.AreNotEqual(Guid.Empty, entity.GetAliasedValue<Guid>("alias", "contactid"));
            Assert.AreEqual(id, entity.GetAliasedValue<Guid>("alias.accountid"));
        }

        [TestMethod]
        public void Date_CoversDateOnlyBehavior()
        {
            var date = new Date(2026, 7, 6);
            var next = date.AddDays(1);
            var month = date.AddMonths(1);
            var year = date.AddYears(1);
            var parsed = Date.Parse("2026-07-06", CultureInfo.InvariantCulture);
            var exact = Date.ParseExact("2026-07-06", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var exactWithStyle = Date.ParseExact(
                "2026-07-06",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );
            var exactFromFormats = Date.ParseExact(
                "2026-07-06",
                new[] { "yyyy-MM-dd" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );

            Assert.AreEqual(6, date.Day);
            Assert.AreEqual(7, date.Month);
            Assert.AreEqual(2026, date.Year);
            Assert.AreEqual(date.DayNumber, Date.FromDayNumber(date.DayNumber).DayNumber);
            Assert.AreEqual(1, next.Subtract(date).Days);
            Assert.AreEqual(8, month.Month);
            Assert.AreEqual(2027, year.Year);
            Assert.IsTrue(Date.Compare(date, next) < 0);
            Assert.IsTrue(date.CompareTo((object)next) < 0);
            Assert.IsTrue(date.CompareTo(null) > 0);
            Assert.IsTrue(date.Equals(parsed));
            Assert.IsTrue(Date.Equals(date, exact));
            Assert.AreEqual(exact, exactWithStyle);
            Assert.AreEqual(exact, exactFromFormats);
            Assert.AreEqual(DateTime.DaysInMonth(2024, 2), Date.DaysInMonth(2024, 2));
            Assert.IsTrue(Date.IsLeapYear(2024));
            Assert.AreEqual("2026-07-06", date.ToString("O"));
            Assert.AreEqual("2026-07-06", date.ToString("s"));
            Assert.AreEqual(new DateTime(2026, 7, 6, 9, 8, 7), date.ToDateTime(9, 8, 7));
            Assert.AreEqual(DateTimeKind.Utc, date.ToDateTime(9, 8, 7, DateTimeKind.Utc).Kind);
            Assert.AreEqual(date, Date.FromDateTime(new DateTime(2026, 7, 6, 12, 0, 0)));
            Assert.IsTrue(Date.TryParse("2026-07-06", out var tryParsed));
            Assert.IsTrue(Date.TryParse("2026-07-06", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tryParsedWithStyle));
            Assert.IsTrue(Date.TryParseExact("2026-07-06", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tryExact));
            Assert.IsTrue(Date.TryParseExact("2026-07-06", new[] { "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tryExactFormats));
            Assert.AreEqual(date, tryParsed);
            Assert.AreEqual(date, tryParsedWithStyle);
            Assert.AreEqual(date, tryExact);
            Assert.AreEqual(date, tryExactFormats);
            Assert.AreEqual(new DateTime(2026, 7, 6), date.ToDateTime());
        }

        [TestMethod]
        public void RegistrationAttributes_StoreConstructorAndMutableValues()
        {
            var document = new DocumentMethodAttribute(
                "WI-1",
                "Translate labels",
                DocumentMethodStage.PostSync,
                "account",
                DocumentMethodMessage.Action,
                "name,description")
            {
                WI = "WI-2",
                Description = "Updated",
                Entity = "contact",
                Fields = "fullname",
                Stage = DocumentMethodStage.Pre,
                Message = DocumentMethodMessage.Update
            };

            var step = new CrmPluginRegistrationAttribute(
                "Update",
                "account",
                StageEnum.PreOperation,
                ExecutionModeEnum.Synchronous,
                "name",
                "Account Update",
                20,
                IsolationModeEnum.Sandbox)
            {
                Id = "step-id",
                RunAs = "CallingUser",
                Unregister = true,
                DeleteAsyncOperation = false,
                Offline = true,
                Server = false,
                Action = PluginStepOperationEnum.Deactivate,
                UnSecureConfiguration = "unsecure",
                SecureConfiguration = "secure",
                Image1Name = "Pre",
                Image1Alias = "pre",
                Image1Type = ImageTypeEnum.PreImage,
                Image1Attributes = "name",
                Image2Name = "Post",
                Image2Alias = "post",
                Image2Type = ImageTypeEnum.PostImage,
                Image2Attributes = "description"
            };
            var customAction = new CrmPluginRegistrationAttribute(
                "pl_Action",
                "Label Translator",
                "Description",
                "Group",
                IsolationModeEnum.None);
            var customApi = new CrmPluginRegistrationAttribute("pl_CustomApi", "Execute", PluginType.CustomApi);

            Assert.AreEqual("WI-2", document.WI);
            Assert.AreEqual(DocumentMethodStage.Pre, document.Stage);
            Assert.AreEqual(DocumentMethodMessage.Update, document.Message);
            Assert.AreEqual("contact", document.Entity);
            Assert.AreEqual("fullname", document.Fields);
            Assert.AreEqual("step-id", step.Id);
            Assert.AreEqual("Update", step.Message);
            Assert.AreEqual("account", step.EntityLogicalName);
            Assert.AreEqual(StageEnum.PreOperation, step.Stage);
            Assert.AreEqual(ExecutionModeEnum.Synchronous, step.ExecutionMode);
            Assert.AreEqual("name", step.FilteringAttributes);
            Assert.AreEqual("Account Update", step.Name);
            Assert.AreEqual(20, step.ExecutionOrder);
            Assert.IsTrue(step.Unregister);
            Assert.IsFalse(step.DeleteAsyncOperation);
            Assert.IsTrue(step.Offline);
            Assert.IsFalse(step.Server);
            Assert.AreEqual(PluginStepOperationEnum.Deactivate, step.Action);
            Assert.AreEqual("unsecure", step.UnSecureConfiguration);
            Assert.AreEqual("secure", step.SecureConfiguration);
            Assert.AreEqual("pre", step.Image1Alias);
            Assert.AreEqual("post", step.Image2Alias);
            Assert.AreEqual("Label Translator", customAction.FriendlyName);
            Assert.AreEqual("Group", customAction.GroupName);
            Assert.AreEqual(PluginType.CustomApi, customApi.PluginType);
        }
    }
}
