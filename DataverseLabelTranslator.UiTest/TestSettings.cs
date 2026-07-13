using Microsoft.Dynamics365.UIAutomation.Browser;
using System;

namespace DataverseLabelTranslator.UiTest
{
    public static class TestSettings
    {
        private static readonly string Type = App.GetAppSettingOrEnvironment("BrowserType", null, null, "Chrome");
        private static readonly string RemoteType = App.GetAppSettingOrEnvironment("RemoteBrowserType", null, null, "Chrome");
        private static readonly string RemoteHubServerURL = App.GetAppSettingOrEnvironment("RemoteHubServer", null, null, "http://localhost:4444/wd/hub");

        // UITEST_HEADLESS env var overrides Headless mode (default: false).
        // The test-all-in-one.ps1 script sets UITEST_HEADLESS=true so UI tests
        // run headless in CI/batch mode. For normal interactive use, leave unset.
        private static readonly bool HeadlessMode =
            string.Equals(Environment.GetEnvironmentVariable("UITEST_HEADLESS"), "true", StringComparison.OrdinalIgnoreCase);

        public static BrowserOptions Options = new BrowserOptions
        {
            BrowserType = (BrowserType)Enum.Parse(typeof(BrowserType), Type),
            PrivateMode = false,
            FireEvents = false,
            Headless = HeadlessMode,
            DisableInfoBars = true,
            EnableAutomation = false,
            DisableExtensions = true,
            DisableFeatures = true,
            DisablePopupBlocking = true,
            DisableSettingsWindow = true,
            UserAgent = false,
            DefaultThinkTime = 2000,
            RemoteBrowserType = (BrowserType)Enum.Parse(typeof(BrowserType), RemoteType),
            RemoteHubServer = new Uri(RemoteHubServerURL),
            UCITestMode = true,
            StartMaximized = true,
            Width = 1920,
            Height = 1080
        };
    }
}
