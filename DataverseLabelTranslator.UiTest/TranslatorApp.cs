using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using System;
using System.Linq;
using System.Threading;

namespace DataverseLabelTranslator.UiTest
{
    public static class TranslatorApp
    {
        public const string BrowserConsoleFilterKey = "DLT_UI_TEST";

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);
        private static bool isTranslatorAppOpened;

        public static void Open()
        {
            if (isTranslatorAppOpened && TrySwitchToDashboardContext())
            {
                return;
            }

            if (isTranslatorAppOpened)
            {
                WaitUntil(
                    "Dataverse Label Translator toolbar was not initialized.",
                    TrySwitchToDashboardContext);
                return;
            }

            UiTestSession.Driver.Navigate().GoToUrl(UiTestSession.TranslatorAppUrl);
            ContinueSignInIfPrompted();
            CollapseSitemapIfExpanded();
            WaitUntil(
                "Dataverse Label Translator toolbar was not initialized.",
                TrySwitchToDashboardContext);
            isTranslatorAppOpened = true;
        }

        private static void CollapseSitemapIfExpanded()
        {
            UiTestSession.Driver.SwitchTo().DefaultContent();

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var button = FindSitemapButtonInCurrentContext();
                if (button != null)
                {
                    ClickDomElement(button);
                    Thread.Sleep(500);
                    UiTestSession.Driver.SwitchTo().DefaultContent();
                    return;
                }

                Thread.Sleep(500);
            }
        }

        private static IWebElement FindSitemapButtonInCurrentContext()
        {
            var buttons = UiTestSession.Driver.FindElements(
                By.XPath(
                    "//button[" +
                    "contains(translate(@aria-label,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sitemap') or " +
                    "contains(translate(@aria-label,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'site map') or " +
                    "contains(translate(@title,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sitemap') or " +
                    "contains(translate(@title,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'site map') or " +
                    "contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sitemap')" +
                    "] | //*[@role='button' and (" +
                    "contains(translate(@aria-label,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sitemap') or " +
                    "contains(translate(@aria-label,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'site map') or " +
                    "contains(translate(@title,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'sitemap') or " +
                    "contains(translate(@title,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'site map')" +
                    ")]"));

            return buttons.FirstOrDefault(button =>
            {
                try
                {
                    return button.Displayed && button.Enabled;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });
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
            return FindVisibleElements(By.Id("tb_grid_toolbar_item_solutionSelect")).Count > 0;
        }

        public static void SelectSolution(string solutionName)
        {
            SelectToolbarMenuItem("solutionSelect", solutionName);
            WaitForToolbarText("solutionSelect", "PHUOC LE");
            WaitUntil(
                "Entity selector did not become ready after selecting the solution.",
                () =>
                    !FindToolbarButton("entitySelect").GetAttribute("class").Contains("disabled") &&
                    FindVisibleElements(By.CssSelector(".w2ui-lock")).Count == 0,
                TimeSpan.FromSeconds(30));
        }

        public static void SelectType(string typeText)
        {
            SelectToolbarMenuItem("type", typeText);
            WaitForToolbarText("type", typeText);
        }

        public static void SelectEntity(string entityText)
        {
            SelectToolbarMenuItem("entitySelect", entityText);
            WaitForToolbarText("entitySelect", entityText);
        }

        public static void SelectComponent(string componentText)
        {
            SelectToolbarMenuItem("component", componentText);
            WaitForToolbarText("component", componentText);
        }

        public static void Load()
        {
            ClickToolbarCommand("load");
            WaitUntil(
                "No records were loaded.",
                () =>
                    FindVisibleElements(By.CssSelector(".w2ui-lock")).Count == 0 &&
                    FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-'])")).Count > 0,
                DefaultTimeout);
            ExpandAllRecordsIfAvailable();
        }

        public static long GetRecordCount()
        {
            return FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]")).Count;
        }

        public static string CaptureDomDiagnostics()
        {
            return Convert.ToString(Execute(
                "var ids=['solutionSelect','entitySelect','type','load','w2ui-save'];" +
                "var toolbar=ids.map(function(id){var e=document.getElementById('tb_grid_toolbar_item_'+id);return e?e.outerHTML:'';}).join('\\n');" +
                "var headers=Array.from(document.querySelectorAll('.w2ui-grid-columns td')).slice(0,8).map(function(e){return e.outerHTML;}).join('\\n');" +
                "var row=document.querySelector('.w2ui-grid-records tr[recid]');" +
                "return 'TOOLBAR\\n'+toolbar+'\\nHEADERS\\n'+headers+'\\nROW\\n'+(row?row.outerHTML:'');"));
        }

        public static string CaptureToolbarMenuDiagnostics(string toolbarId)
        {
            ClickDomElement(FindToolbarButton(toolbarId));
            Thread.Sleep(500);
            return Convert.ToString(Execute(
                "return Array.from(document.querySelectorAll('.w2ui-overlay')).map(function(e){return e.outerHTML;}).join('\\n');"));
        }

        public static void ClearBrowserConsole(string testName)
        {
            Execute(
                "console.clear();" +
                "console.log('%c['+arguments[0]+'] START%c '+arguments[1]," +
                "'background:#2563eb;color:white;font-weight:bold;padding:2px 6px;border-radius:3px'," +
                "'color:#2563eb;font-weight:bold');",
                BrowserConsoleFilterKey,
                testName);
        }

        public static void WriteBrowserConsole(string level, string message)
        {
            Execute(
                "var level=String(arguments[0]||'INFO').toUpperCase();" +
                "var colors={INFO:'#0369a1',PASS:'#15803d',ERROR:'#b91c1c',RESTORE:'#7e22ce'};" +
                "var color=colors[level]||'#374151';" +
                "console.log('%c['+arguments[2]+'] '+level+'%c '+arguments[1]," +
                "'background:'+color+';color:white;font-weight:bold;padding:2px 6px;border-radius:3px'," +
                "'color:'+color+';font-weight:bold');",
                level,
                message,
                BrowserConsoleFilterKey);
        }

        public static string GetFirstEditableRecordId()
        {
            ExpandAllRecords();
            var row = WaitForVisibleElement(
                By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)"),
                "No editable Global Option Set row was visible.");
            return row.GetAttribute("recid");
        }

        public static string GetFirstEditableParentRecordId()
        {
            ExpandAllRecords();
            var rows = FindVisibleElements(
                By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)"));
            var parent = rows.FirstOrDefault(row => row.GetAttribute("recid").Split(':').Length == 2);

            Assert.IsNotNull(parent, "No editable Global Option Set parent row was visible.");
            return parent.GetAttribute("recid");
        }

        public static string GetFirstEditableChildRecordId(string parentRecordId)
        {
            ExpandAllRecords(force: true);
            var prefix = parentRecordId + ":";
            IWebElement child = null;
            WaitUntil(
                $"No editable child row was visible for parent '{parentRecordId}'.\n" + CaptureGridDiagnostics(),
                () =>
                {
                    child = FindVisibleElements(
                            By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)"))
                        .FirstOrDefault(row => row.GetAttribute("recid").StartsWith(prefix, StringComparison.Ordinal));
                    return child != null;
                },
                DefaultTimeout);

            Assert.IsNotNull(child, $"No editable child row was visible for parent '{parentRecordId}'.");
            return child.GetAttribute("recid");
        }

        public static string GetEditableRecordIdBySchemaName(string schemaName)
        {
            string foundRecordId = null;

            WaitUntil(
                $"No editable row with schema name '{schemaName}' was visible after expanding records.\n" + CaptureGridDiagnostics(),
                () =>
                {
                    ExpandAllRecords(force: true);
                    foundRecordId = Convert.ToString(Execute(
                        "var schemaName=String(arguments[0]||'');" +
                        "var grid=window.w2ui&&w2ui.grid;" +
                        "if(!grid||!Array.isArray(grid.records)) return '';" +
                        "var stack=grid.records.slice();" +
                        "while(stack.length){" +
                        "  var record=stack.shift();" +
                        "  if(!record) continue;" +
                        "  var children=record.w2ui&&Array.isArray(record.w2ui.children)?record.w2ui.children:[];" +
                        "  for(var i=0;i<children.length;i++) stack.push(children[i]);" +
                        "  if(record.schemaName===schemaName&&record.isEditable!==false) return String(record.recid||'');" +
                        "}" +
                        "return '';",
                        schemaName));
                    if (string.IsNullOrWhiteSpace(foundRecordId)) return false;

                    return FindVisibleElements(
                            By.XPath($"//div[contains(@class,'w2ui-grid-records')]//tr[@recid={ToXPathLiteral(foundRecordId)} and not(contains(@class,'w2ui-no-edit'))]"))
                        .Count > 0;
                },
                DefaultTimeout);

            return foundRecordId;
        }

        public static void GetFirstEditableParentAndChildRecordIds(out string parentRecordId, out string childRecordId)
        {
            string foundParentRecordId = null;
            string foundChildRecordId = null;

            WaitUntil(
                "No editable parent/child row pair was visible after expanding records.\n" + CaptureGridDiagnostics(),
                () =>
                {
                    ExpandAllRecords(force: true);
                    var rows = FindVisibleElements(
                        By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)"));
                    var rowIds = rows
                        .Select(row => row.GetAttribute("recid"))
                        .Where(recid => !string.IsNullOrWhiteSpace(recid))
                        .ToList();
                    var child = rowIds.FirstOrDefault(recid => recid.Split(':').Length > 2);
                    if (child == null) return false;

                    var parts = child.Split(':');
                    var parent = string.Join(":", parts.Take(2));
                    if (!rowIds.Contains(parent)) return false;

                    foundParentRecordId = parent;
                    foundChildRecordId = child;
                    return true;
                },
                DefaultTimeout);

            parentRecordId = foundParentRecordId;
            childRecordId = foundChildRecordId;
        }

        public static TranslationValues GetTranslations(string recordId)
        {
            return new TranslationValues(
                ReadCellEditorValue(recordId, "1"),
                ReadCellEditorValue(recordId, "2"),
                ReadCellEditorValue(recordId, "3"));
        }

        public static void SetTranslations(string recordId, TranslationValues values)
        {
            EditCell(recordId, "1", values.English);
            EditCell(recordId, "2", values.Japanese);
            EditCell(recordId, "3", values.Vietnamese);
        }

        public static void SaveAndWaitForTranslations(string recordId, TranslationValues expected)
        {
            var previousCell = FindCell(recordId, "1");
            ClickToolbarCommand("w2ui-save");
            WaitUntil(
                "The grid did not reload after Save.",
                () => IsStale(previousCell),
                TimeSpan.FromMinutes(2));
            WaitForVisibleElement(
                By.CssSelector(".w2ui-grid-records tr[recid]"),
                "Global Option Set rows were not displayed after Save.",
                TimeSpan.FromMinutes(2));
            ExpandAllRecords();
            WaitUntil(
                "The translations were not reloaded with the expected values.",
                () => TryGetVisibleTranslations(recordId, out var actual) && TranslationValuesEqual(actual, expected),
                TimeSpan.FromSeconds(15));
        }

        public static void RestoreTranslations(string recordId, TranslationValues backup)
        {
            ExpandAllRecords();
            if (TryGetVisibleTranslations(recordId, out var actual) && TranslationValuesEqual(actual, backup)) return;

            SetTranslations(recordId, backup);
            SaveAndWaitForTranslations(recordId, backup);
        }

        public static void AssertTranslations(string recordId, TranslationValues expected, string message)
        {
            ExpandAllRecords();
            var actual = GetVisibleTranslations(recordId);

            Assert.AreEqual(NormalizeValue(expected.English), NormalizeValue(actual.English), $"{message} English (1033).");
            Assert.AreEqual(NormalizeValue(expected.Japanese), NormalizeValue(actual.Japanese), $"{message} Japanese (1041).");
            Assert.AreEqual(NormalizeValue(expected.Vietnamese), NormalizeValue(actual.Vietnamese), $"{message} Vietnamese (1066).");
        }

        public static void SaveAndWaitForTranslations(
            string parentRecordId,
            TranslationValues expectedParent,
            string childRecordId,
            TranslationValues expectedChild)
        {
            var previousCell = FindCell(parentRecordId, "1");
            ClickToolbarCommand("w2ui-save");
            WaitUntil(
                "The grid did not reload after saving parent and child translations.",
                () => IsStale(previousCell),
                TimeSpan.FromMinutes(2));
            WaitForVisibleElement(
                By.CssSelector(".w2ui-grid-records tr[recid]"),
                "Global Option Set rows were not displayed after Save.",
                TimeSpan.FromMinutes(2));
            ExpandAllRecords(force: true);
            WaitUntil(
                "Parent and child translations were not reloaded with the expected values.",
                () =>
                    TryGetVisibleTranslations(parentRecordId, out var actualParent) &&
                    TranslationValuesEqual(actualParent, expectedParent) &&
                    TryGetVisibleTranslations(childRecordId, out var actualChild) &&
                    TranslationValuesEqual(actualChild, expectedChild),
                TimeSpan.FromSeconds(15));
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

        private static void SelectToolbarMenuItem(string toolbarId, string visibleText)
        {
            ClickDomElement(FindToolbarButton(toolbarId));
            var normalizedText = visibleText.ToLowerInvariant();
            var item = WaitForVisibleElement(
                By.XPath(
                    "//*[contains(@class,'w2ui-overlay')]" +
                    "//*[contains(@class,'w2ui-menu-item') and " +
                    $"contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),{ToXPathLiteral(normalizedText)})]"),
                $"Toolbar option '{visibleText}' was not displayed.",
                TimeSpan.FromSeconds(15));
            ClickDomElement(item);
        }

        private static void WaitForToolbarText(string toolbarId, string expectedText)
        {
            WaitUntil(
                $"Toolbar '{toolbarId}' did not display '{expectedText}'.",
                () => FindToolbarButton(toolbarId).Text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0,
                TimeSpan.FromSeconds(15));
        }

        private static IWebElement FindToolbarButton(string toolbarId)
        {
            return WaitForVisibleElement(
                By.Id($"tb_grid_toolbar_item_{toolbarId}"),
                $"Toolbar button '{toolbarId}' was not visible.");
        }

        private static void ClickToolbarCommand(string toolbarId)
        {
            var button = TryWaitForVisibleElement(By.Id($"tb_grid_toolbar_item_{toolbarId}"), TimeSpan.FromSeconds(3));
            if (button != null)
            {
                ClickDomElement(button);
                return;
            }

            WaitUntil(
                $"Toolbar command '{toolbarId}' was not ready.\n" + CaptureDomDiagnostics(),
                () => Convert.ToBoolean(Execute(
                    "var toolbar=window.w2ui&&w2ui.grid_toolbar;" +
                    "var item=toolbar&&toolbar.get&&toolbar.get(arguments[0]);" +
                    "return !!(toolbar&&item&&!item.disabled&&typeof toolbar.click==='function');",
                    toolbarId)),
                DefaultTimeout);
            Execute(
                "var toolbar=window.w2ui&&w2ui.grid_toolbar;" +
                "if (toolbar&&typeof toolbar.click==='function') toolbar.click(arguments[0]);",
                toolbarId);
        }

        private static void ClickDomElement(IWebElement element)
        {
            ((IJavaScriptExecutor)UiTestSession.Driver).ExecuteScript("arguments[0].click();", element);
        }

        private static void ExpandAllRecords(bool force = false)
        {
            if (!force && FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)")).Count > 0) return;
            ExpandAllRecordsIfAvailable(force);
            WaitForVisibleElement(
                By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)"),
                "No editable row appeared after expanding records.",
                DefaultTimeout);
        }

        private static void ExpandAllRecordsIfAvailable(bool force = false)
        {
            if (!force && FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)")).Count > 0) return;

            WaitUntil(
                "Grid rows were not ready before expanding records.",
                () =>
                    FindVisibleElements(By.CssSelector(".w2ui-lock")).Count == 0 &&
                    FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-'])")).Count > 0,
                DefaultTimeout);

            var toggle = FindToolbarButton("toggle");
            ClickUserElement(toggle);
            var item = TryWaitForVisibleElement(
                By.XPath(
                    "//*[contains(@class,'w2ui-overlay')]" +
                    "//*[contains(@class,'w2ui-menu-item') and " +
                    "contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'expand all records')]"),
                TimeSpan.FromSeconds(3));
            if (item == null)
            {
                CloseToolbarOverlay();
                InvokeExpandAllToolbarCommand();
            }
            else
            {
                try
                {
                    ClickUserElement(item);
                }
                catch (WebDriverException)
                {
                    InvokeExpandAllToolbarCommand();
                }
            }

            if (!WaitUntilOrFalse(
                    () =>
                        FindVisibleElements(By.CssSelector(".w2ui-lock")).Count == 0 &&
                        FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)")).Count > 0,
                    TimeSpan.FromSeconds(10)))
            {
                ExpandAllGridRowsInRuntime();
            }

            if (!WaitUntilOrFalse(
                    () =>
                        FindVisibleElements(By.CssSelector(".w2ui-lock")).Count == 0 &&
                        FindVisibleElements(By.CssSelector(".w2ui-grid-records tr[recid]:not([recid='-none-']):not(.w2ui-no-edit)")).Count > 0,
                    DefaultTimeout))
            {
                Assert.Fail("Expand all records did not complete.\n" + CaptureGridDiagnostics());
            }
        }

        private static void InvokeExpandAllToolbarCommand()
        {
            Execute(
                "if (window.w2ui && w2ui.grid_toolbar && typeof w2ui.grid_toolbar.click === 'function') {" +
                "  w2ui.grid_toolbar.click('toggle:expandAll');" +
                "  w2ui.grid_toolbar.click('expandAll');" +
                "}");
        }

        private static void ExpandAllGridRowsInRuntime()
        {
            Execute(
                "var grid=window.w2ui&&w2ui.grid;" +
                "if(grid&&Array.isArray(grid.records)){" +
                "  var flat=[];" +
                "  function append(record){" +
                "    if(!record) return;" +
                "    flat.push(record);" +
                "    var state=record.w2ui=record.w2ui||{};" +
                "    var children=Array.isArray(state.children)?state.children:[];" +
                "    state.expanded=children.length>0;" +
                "    for(var i=0;i<children.length;i++){" +
                "      var child=children[i];" +
                "      child.w2ui=child.w2ui||{};" +
                "      child.w2ui.parent_recid=record.recid;" +
                "      append(child);" +
                "    }" +
                "  }" +
                "  var roots=[];" +
                "  for(var r=0;r<grid.records.length;r++){" +
                "    var record=grid.records[r];" +
                "    if(!record||!record.recid||record.recid==='-none-') continue;" +
                "    if(record.w2ui&&record.w2ui.parent_recid) continue;" +
                "    roots.push(record);" +
                "  }" +
                "  for(var root=0;root<roots.length;root++) append(roots[root]);" +
                "  if(flat.length>0){" +
                "    grid.records=flat;" +
                "    grid.total=flat.length;" +
                "    if(grid.last) grid.last.idCache={};" +
                "    if(grid.searchData&&grid.searchData.length>0&&typeof grid.localSearch==='function') grid.localSearch(true);" +
                "    if(typeof grid.refresh==='function') grid.refresh();" +
                "  }" +
                "}");
        }

        private static void ClickUserElement(IWebElement element)
        {
            try
            {
                new Actions(UiTestSession.Driver).MoveToElement(element).Click().Perform();
            }
            catch (WebDriverException)
            {
                try
                {
                    ClickDomElement(element);
                }
                catch (WebDriverException)
                {
                    throw;
                }
            }
        }

        private static void CloseToolbarOverlay()
        {
            Execute("document.body.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));");
        }

        private static object ReadCellEditorValue(string recordId, string column)
        {
            var input = OpenCellEditor(recordId, column);
            var value = input.GetAttribute("value") ?? string.Empty;
            input.SendKeys(Keys.Escape);
            WaitUntil("The cell editor did not close.", () => FindCell(recordId, column).FindElements(By.TagName("input")).Count == 0);
            return value;
        }

        private static void EditCell(string recordId, string column, object value)
        {
            var input = OpenCellEditor(recordId, column);
            input.SendKeys(Keys.Control + "a");
            input.SendKeys(NormalizeValue(value));
            input.SendKeys(Keys.Enter);
            CloseAnyCellEditor();
        }

        private static IWebElement OpenCellEditor(string recordId, string column)
        {
            CloseAnyCellEditor();
            var cell = FindCell(recordId, column);
            ((IJavaScriptExecutor)UiTestSession.Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'center'});", cell);
            ClickUserElement(cell);
            try
            {
                new Actions(UiTestSession.Driver).MoveToElement(cell).DoubleClick().Perform();
            }
            catch (WebDriverException)
            {
            }

            var editorSelector = By.XPath(
                $"//tr[@recid={ToXPathLiteral(recordId)}]/td[@col={ToXPathLiteral(column)}]//input | " +
                $"//tr[@recid={ToXPathLiteral(recordId)}]/td[@col={ToXPathLiteral(column)}]//textarea");
            var editor = TryWaitForVisibleElement(editorSelector, TimeSpan.FromSeconds(3));
            if (editor != null) return editor;

            cell = FindCell(recordId, column);
            ((IJavaScriptExecutor)UiTestSession.Driver).ExecuteScript(
                "arguments[0].dispatchEvent(new MouseEvent('mousedown',{bubbles:true,cancelable:true,view:window}));" +
                "arguments[0].dispatchEvent(new MouseEvent('mouseup',{bubbles:true,cancelable:true,view:window}));" +
                "arguments[0].dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window}));" +
                "arguments[0].dispatchEvent(new MouseEvent('dblclick',{bubbles:true,cancelable:true,view:window}));",
                cell);
            editor = TryWaitForVisibleElement(editorSelector, TimeSpan.FromSeconds(3));
            if (editor != null) return editor;

            cell = FindCell(recordId, column);
            try
            {
                new Actions(UiTestSession.Driver).MoveToElement(cell).Click().SendKeys(Keys.F2).Perform();
            }
            catch (WebDriverException)
            {
                cell.SendKeys(Keys.F2);
            }
            editor = TryWaitForVisibleElement(editorSelector, TimeSpan.FromSeconds(3));
            if (editor != null) return editor;

            var openedWithGridApi = Convert.ToBoolean(Execute(
                "var recordId=String(arguments[0]||'');" +
                "var column=parseInt(arguments[1],10);" +
                "var grid=window.w2ui&&w2ui.grid;" +
                "if(!grid||!Number.isFinite(column)||!grid.columns||!grid.columns[column]) return false;" +
                "var index=typeof grid.get==='function'?grid.get(recordId,true):-1;" +
                "if(index==null||index<0) return false;" +
                "if(typeof grid.focus==='function') grid.focus();" +
                "if(typeof grid.scrollIntoView==='function') grid.scrollIntoView(index,column,true);" +
                "if(typeof grid.selectNone==='function') grid.selectNone(true);" +
                "if(typeof grid.select==='function') grid.select({recid:recordId,column:column});" +
                "if(typeof grid.editField!=='function') return false;" +
                "grid.editField(recordId,column,null,{type:'keydown',keyCode:13,preventDefault:function(){},stopPropagation:function(){}});" +
                "return true;",
                recordId,
                column));
            if (openedWithGridApi)
            {
                editor = TryWaitForVisibleElement(editorSelector, TimeSpan.FromSeconds(5));
                if (editor != null) return editor;
            }

            cell = FindCell(recordId, column);
            try
            {
                new Actions(UiTestSession.Driver).MoveToElement(cell).Click().SendKeys(Keys.Enter).Perform();
            }
            catch (WebDriverException)
            {
                cell.SendKeys(Keys.Enter);
            }
            return WaitForVisibleElement(
                editorSelector,
                $"Cell editor did not open for record '{recordId}', column '{column}'.\n" + CaptureCellDiagnostics(recordId, column),
                TimeSpan.FromSeconds(15));
        }

        private static string CaptureCellDiagnostics(string recordId, string column)
        {
            return Convert.ToString(Execute(
                "var row=document.querySelector('.w2ui-grid-records tr[recid='+JSON.stringify(arguments[0])+']');" +
                "var cell=row?row.querySelector('td[col='+JSON.stringify(arguments[1])+']'):null;" +
                "return JSON.stringify({row:row?{recid:row.getAttribute('recid'),className:row.className,text:row.innerText}:null,cell:cell?{className:cell.className,html:cell.outerHTML}:null,active:document.activeElement?document.activeElement.outerHTML:null});",
                recordId,
                column));
        }

        private static void CloseAnyCellEditor()
        {
            var editors = FindVisibleElements(By.CssSelector(".w2ui-grid-records input, .w2ui-grid-records textarea"));
            if (editors.Count > 0) editors[0].SendKeys(Keys.Escape);
            WaitUntil(
                "The active grid editor did not close.",
                () => FindVisibleElements(By.CssSelector(".w2ui-grid-records input, .w2ui-grid-records textarea")).Count == 0,
                TimeSpan.FromSeconds(15));
        }

        private static IWebElement FindCell(string recordId, string column)
        {
            return WaitForVisibleElement(
                By.XPath($"//div[contains(@class,'w2ui-grid-records')]//tr[@recid={ToXPathLiteral(recordId)}]/td[@col={ToXPathLiteral(column)}]"),
                $"Cell was not visible for record '{recordId}', column '{column}'.");
        }

        private static TranslationValues GetVisibleTranslations(string recordId)
        {
            return new TranslationValues(
                NormalizeCellText(FindCell(recordId, "1").Text),
                NormalizeCellText(FindCell(recordId, "2").Text),
                NormalizeCellText(FindCell(recordId, "3").Text));
        }

        private static bool TryGetVisibleTranslations(string recordId, out TranslationValues values)
        {
            values = null;
            try
            {
                values = GetVisibleTranslations(recordId);
                return true;
            }
            catch (WebDriverException)
            {
                return false;
            }
            catch (AssertFailedException)
            {
                return false;
            }
        }

        private static string NormalizeCellText(string value)
        {
            return value == "-" ? string.Empty : value ?? string.Empty;
        }

        private static bool IsStale(IWebElement element)
        {
            try
            {
                _ = element.Enabled;
                return false;
            }
            catch (StaleElementReferenceException)
            {
                return true;
            }
        }

        private static IWebElement WaitForVisibleElement(By selector, string timeoutMessage, TimeSpan? timeout = null)
        {
            IWebElement result = null;
            WaitUntil(
                timeoutMessage,
                () =>
                {
                    result = FindVisibleElements(selector).FirstOrDefault();
                    return result != null;
                },
                timeout);
            return result;
        }

        private static IWebElement TryWaitForVisibleElement(By selector, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                var element = FindVisibleElements(selector).FirstOrDefault();
                if (element != null) return element;
                Thread.Sleep(200);
            }

            return null;
        }

        private static bool WaitUntilOrFalse(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (condition()) return true;
                }
                catch (WebDriverException)
                {
                }

                Thread.Sleep(500);
            }

            return false;
        }

        private static string CaptureGridDiagnostics()
        {
            return Convert.ToString(Execute(
                "var rows=Array.from(document.querySelectorAll('.w2ui-grid-records tr[recid]')).slice(0,12).map(function(row){" +
                "return {recid:row.getAttribute('recid'), className:row.className, text:row.innerText.slice(0,180)};" +
                "});" +
                "var overlays=Array.from(document.querySelectorAll('.w2ui-overlay')).map(function(e){return e.innerText.slice(0,300);});" +
                "var toggle=document.getElementById('tb_grid_toolbar_item_toggle');" +
                "return JSON.stringify({rowCount:document.querySelectorAll('.w2ui-grid-records tr[recid]').length,editableCount:document.querySelectorAll('.w2ui-grid-records tr[recid]:not([recid=\"-none-\"]):not(.w2ui-no-edit)').length,rows:rows,overlays:overlays,toggle:toggle?toggle.outerHTML:null});"));
        }

        private static System.Collections.Generic.List<IWebElement> FindVisibleElements(By selector)
        {
            return UiTestSession.Driver
                .FindElements(selector)
                .Where(element =>
                {
                    try
                    {
                        return element.Displayed;
                    }
                    catch (StaleElementReferenceException)
                    {
                        return false;
                    }
                })
                .ToList();
        }

        private static string ToXPathLiteral(string value)
        {
            if (!value.Contains("'")) return $"'{value}'";
            if (!value.Contains("\"")) return $"\"{value}\"";
            return "concat('" + value.Replace("'", "',\"'\",'") + "')";
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

        private static object Execute(string script, params object[] arguments)
        {
            return ((IJavaScriptExecutor)UiTestSession.Driver).ExecuteScript(script, arguments);
        }
    }
}
