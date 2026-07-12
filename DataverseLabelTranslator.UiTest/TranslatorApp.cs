using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System;
using System.Threading;

namespace DataverseLabelTranslator.UiTest
{
    public static class TranslatorApp
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

        public static void Open()
        {
            UiTestSession.Driver.Navigate().GoToUrl(UiTestSession.TranslatorAppUrl);
            ContinueSignInIfPrompted();
            WaitUntil(
                "Dataverse Label Translator toolbar was not initialized.",
                TrySwitchToDashboardContext);
            WaitUntil(
                "Dataverse solutions were not loaded.",
                () => ExecuteBoolean("var t=w2ui.grid_toolbar; var i=t.get('solutionSelect'); return !!(i && i.items && i.items.length);"));
        }

        private static void ContinueSignInIfPrompted()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                UiTestSession.Driver.SwitchTo().DefaultContent();
                if (ClickSignInInCurrentContext())
                {
                    Thread.Sleep(2000);
                    return;
                }

                foreach (var frame in UiTestSession.Driver.FindElements(By.TagName("iframe")))
                {
                    try
                    {
                        UiTestSession.Driver.SwitchTo().Frame(frame);
                        if (ClickSignInInCurrentContext())
                        {
                            Thread.Sleep(2000);
                            UiTestSession.Driver.SwitchTo().DefaultContent();
                            return;
                        }
                    }
                    catch (WebDriverException)
                    {
                    }
                    finally
                    {
                        UiTestSession.Driver.SwitchTo().DefaultContent();
                    }
                }

                Thread.Sleep(500);
            }
        }

        private static bool ClickSignInInCurrentContext()
        {
            var buttons = UiTestSession.Driver.FindElements(
                By.XPath("//button[normalize-space(.)='Sign in'] | //*[@role='button' and normalize-space(.)='Sign in']"));

            foreach (var button in buttons)
            {
                if (!button.Displayed || !button.Enabled) continue;
                button.Click();
                return true;
            }

            return false;
        }

        private static bool TrySwitchToDashboardContext()
        {
            UiTestSession.Driver.SwitchTo().DefaultContent();
            if (HasDashboardToolbar()) return true;

            foreach (var frame in UiTestSession.Driver.FindElements(By.TagName("iframe")))
            {
                try
                {
                    UiTestSession.Driver.SwitchTo().Frame(frame);
                    if (HasDashboardToolbar()) return true;
                }
                catch (WebDriverException)
                {
                }

                UiTestSession.Driver.SwitchTo().DefaultContent();
            }

            return false;
        }

        private static bool HasDashboardToolbar()
        {
            return ExecuteBoolean("return !!(window.w2ui && w2ui.grid_toolbar && w2ui.grid_toolbar.get('solutionSelect'));");
        }

        public static void SelectSolution(string solutionName)
        {
            var available = ExecuteBoolean(
                "var t=w2ui.grid_toolbar;" +
                "var i=t.get('solutionSelect');" +
                "var normalize=function(v){return (v||'').toLowerCase().replace(/[^a-z0-9]/g,'');};" +
                "var n=normalize(arguments[0]);" +
                "var x=i.items.find(function(v){" +
                "return normalize(v.text).indexOf(n)!==-1;" +
                "});" +
                "if(!x){return false;}" +
                "t.click('solutionSelect:'+x.id);" +
                "return true;",
                solutionName);

            Assert.IsTrue(
                available,
                $"Solution '{solutionName}' was not available in the toolbar. Available solutions: {GetAvailableSolutionTexts()}");
            WaitForGridUnlock("The app did not finish selecting the solution.");
        }

        public static void SelectType(string typeText)
        {
            var available = ExecuteBoolean(
                "var t=w2ui.grid_toolbar;" +
                "var i=t.get('type');" +
                "var text=arguments[0].toLowerCase();" +
                "var x=i.items.find(function(v){return (v.text||'').toLowerCase()===text;});" +
                "if(!x){return false;}" +
                "t.click('type:'+x.id);" +
                "return true;",
                typeText);

            Assert.IsTrue(available, $"Translation type '{typeText}' was not available.");
            WaitUntil(
                $"Translation type '{typeText}' was not selected.",
                () => ExecuteBoolean(
                    "var i=w2ui.grid_toolbar.get('type');" +
                    "var text=arguments[0].toLowerCase();" +
                    "var x=i.items.find(function(v){return (v.text||'').toLowerCase()===text;});" +
                    "return !!x && i.selected===x.id;",
                    typeText),
                TimeSpan.FromSeconds(15));
        }

        public static void SelectEntity(string entityText)
        {
            var available = ExecuteBoolean(
                "var t=w2ui.grid_toolbar;" +
                "var i=t.get('entitySelect');" +
                "var text=arguments[0].toLowerCase();" +
                "var x=i.items.find(function(v){return (v.text||'').toLowerCase()===text;});" +
                "if(!x){return false;}" +
                "t.click('entitySelect:'+x.id);" +
                "return true;",
                entityText);

            Assert.IsTrue(available, $"Entity '{entityText}' was not available.");
            WaitUntil(
                $"Entity '{entityText}' was not selected.",
                () => ExecuteBoolean(
                    "var i=w2ui.grid_toolbar.get('entitySelect');" +
                    "var text=arguments[0].toLowerCase();" +
                    "var x=i.items.find(function(v){return (v.text||'').toLowerCase()===text;});" +
                    "return !!x && i.selected===x.id;",
                    entityText),
                TimeSpan.FromSeconds(15));

            if (string.Equals(entityText, "None", StringComparison.OrdinalIgnoreCase))
            {
                WaitUntil(
                    "Global Option Set did not become available after selecting Entity None.",
                    () => ExecuteBoolean(
                        "var i=w2ui.grid_toolbar.get('type:globalOptionSet');" +
                        "return !!i && i.hidden!==true;"),
                    TimeSpan.FromSeconds(15));
            }
        }

        public static void Load()
        {
            Execute("w2ui.grid_toolbar.click('load');");
            WaitUntil(
                "No Global Option Set records were loaded.",
                () => ExecuteBoolean(
                    "var g=w2ui.grid;" +
                    "var b=w2ui.grid_toolbar.get('load');" +
                    "return !!g && !g.locked && !(b && b.disabled) && g.records && g.records.length>0;"),
                TimeSpan.FromSeconds(15));
        }

        public static string GetSelectedSolutionText()
        {
            return Convert.ToString(Execute(
                "var t=w2ui.grid_toolbar;" +
                "var i=t.get('solutionSelect');" +
                "var x=i.items.find(function(v){return v.id===i.selected;});" +
                "return x ? x.text : '';"));
        }

        private static string GetAvailableSolutionTexts()
        {
            return Convert.ToString(Execute(
                "var i=w2ui.grid_toolbar.get('solutionSelect');" +
                "return i.items.map(function(v){return v.text||'';}).filter(Boolean).join(', ');"));
        }

        public static string GetSelectedType()
        {
            return Convert.ToString(Execute("return w2ui.grid_toolbar.get('type').selected;"));
        }

        public static long GetRecordCount()
        {
            return Convert.ToInt64(Execute("return w2ui.grid.records.length;"));
        }

        public static string GetFirstEditableRecordKey()
        {
            var recordKey = Convert.ToString(Execute(
                "var records=DataverseLabelTranslator.GetAllRecords();" +
                "var record=records.find(function(r){" +
                "return !!r.gridKey && r.w2ui && r.w2ui.editable!==false;" +
                "});" +
                "return record ? String(record.gridKey) : '';"));

            Assert.IsFalse(string.IsNullOrWhiteSpace(recordKey), "No editable Global Option Set record was available.");
            return recordKey;
        }

        public static TranslationValues GetTranslations(string recordKey)
        {
            return new TranslationValues(
                GetTranslation(recordKey, "1033"),
                GetTranslation(recordKey, "1041"),
                GetTranslation(recordKey, "1066"));
        }

        public static void SetTranslations(string recordKey, TranslationValues values)
        {
            var changed = ExecuteBoolean(
                "var records=DataverseLabelTranslator.GetAllRecords();" +
                "var key=String(arguments[0]);" +
                "var record=records.find(function(r){return String(r.gridKey)===key;});" +
                "if(!record){return false;}" +
                "DataverseLabelTranslator.ApplyGridChangeValue(record,'1033',arguments[1]);" +
                "DataverseLabelTranslator.ApplyGridChangeValue(record,'1041',arguments[2]);" +
                "DataverseLabelTranslator.ApplyGridChangeValue(record,'1066',arguments[3]);" +
                "DataverseLabelTranslator.RefreshGridRow(record.recid);" +
                "DataverseLabelTranslator.SetSaveButtonDisabled(!DataverseLabelTranslator.HasPendingChanges());" +
                "return DataverseLabelTranslator.HasPendingChanges();",
                recordKey,
                values.English,
                values.Japanese,
                values.Vietnamese);

            Assert.IsTrue(changed, "The requested translations did not create any pending changes.");
        }

        public static void SaveAndWaitForTranslations(string recordKey, TranslationValues expected)
        {
            Execute("w2ui.grid.toolbar.click('w2ui-save');");
            WaitUntil(
                "The translations were not saved and reloaded with the expected values.",
                () =>
                    !HasPendingChanges() &&
                    TranslationValuesEqual(GetTranslations(recordKey), expected),
                TimeSpan.FromMinutes(2));
        }

        public static void RestoreTranslations(string recordKey, TranslationValues backup)
        {
            if (TranslationValuesEqual(GetTranslations(recordKey), backup) && !HasPendingChanges()) return;

            SetTranslations(recordKey, backup);
            SaveAndWaitForTranslations(recordKey, backup);
        }

        public static void AssertTranslations(string recordKey, TranslationValues expected, string message)
        {
            var actual = GetTranslations(recordKey);

            Assert.AreEqual(NormalizeValue(expected.English), NormalizeValue(actual.English), $"{message} English (1033).");
            Assert.AreEqual(NormalizeValue(expected.Japanese), NormalizeValue(actual.Japanese), $"{message} Japanese (1041).");
            Assert.AreEqual(NormalizeValue(expected.Vietnamese), NormalizeValue(actual.Vietnamese), $"{message} Vietnamese (1066).");
        }

        private static object GetTranslation(string recordKey, string languageCode)
        {
            return Execute(
                "var records=DataverseLabelTranslator.GetAllRecords();" +
                "var key=String(arguments[0]);" +
                "var record=records.find(function(r){return String(r.gridKey)===key;});" +
                "if(!record){throw new Error('Global Option Set record not found: '+key);}" +
                "return Object.prototype.hasOwnProperty.call(record,arguments[1]) ? record[arguments[1]] : null;",
                recordKey,
                languageCode);
        }

        private static bool HasPendingChanges()
        {
            return ExecuteBoolean("return DataverseLabelTranslator.HasPendingChanges();");
        }

        private static bool TranslationValuesEqual(TranslationValues left, TranslationValues right)
        {
            return
                string.Equals(NormalizeValue(left.English), NormalizeValue(right.English), StringComparison.Ordinal) &&
                string.Equals(NormalizeValue(left.Japanese), NormalizeValue(right.Japanese), StringComparison.Ordinal) &&
                string.Equals(NormalizeValue(left.Vietnamese), NormalizeValue(right.Vietnamese), StringComparison.Ordinal);
        }

        private static string NormalizeValue(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static void WaitForGridUnlock(string timeoutMessage)
        {
            WaitUntil(
                timeoutMessage,
                () => ExecuteBoolean("return !!w2ui.grid && !w2ui.grid.locked;"));
        }

        private static void WaitUntil(string timeoutMessage, Func<bool> condition, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow.Add(timeout ?? DefaultTimeout);
            Exception lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (condition()) return;
                }
                catch (WebDriverException error)
                {
                    lastError = error;
                }

                Thread.Sleep(500);
            }

            Assert.Fail(lastError == null ? timeoutMessage : $"{timeoutMessage} Last WebDriver error: {lastError.Message}");
        }

        private static bool ExecuteBoolean(string script, params object[] arguments)
        {
            return Convert.ToBoolean(Execute(script, arguments));
        }

        private static object Execute(string script, params object[] arguments)
        {
            return ((IJavaScriptExecutor)UiTestSession.Driver).ExecuteScript(script, arguments);
        }
    }
}
