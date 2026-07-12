using Microsoft.Dynamics365.UIAutomation.Api.UCI;
using Microsoft.Dynamics365.UIAutomation.Browser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System;

[assembly: DoNotParallelize]

namespace DataverseLabelTranslator.UiTest
{
    [TestClass]
    public sealed class UiTestSession
    {
        public const string TranslatorAppUrl = "https://contoso-pl-xrm-quick-edit-dev.crm5.dynamics.com/main.aspx?appid=5f5c9906-b158-f111-a825-7ced8db48acd&forceUCI=1&pagetype=webresource&webresourceName=pl_%2Fhtml%2FApp.html#";

        public static XrmApp XrmApp { get; private set; }

        public static WebClient Client { get; private set; }

        public static IWebDriver Driver => Client.Browser.Driver;

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            var username = App.GetAppSettingOrEnvironment("UiTestUserName", "UserName", "DEVKIT_USERNAME").ToSecureString();
            var password = App.GetAppSettingOrEnvironment("UiTestPassword", "Password", "DEVKIT_PASSWORD").ToSecureString();
            var xrmUri = new Uri(App.GetAppSettingOrEnvironment("Url", "DEVKIT_URL"));
            Client = new WebClient(TestSettings.Options);

            Client.Browser.Options.UCIPerformanceMode = false;
            XrmApp = new XrmApp(Client);
            XrmApp.OnlineLogin.Login(xrmUri, username, password);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            XrmApp?.Dispose();
            XrmApp = null;
            Client = null;
        }
    }
}
