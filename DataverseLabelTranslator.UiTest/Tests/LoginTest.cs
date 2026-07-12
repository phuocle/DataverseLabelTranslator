using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataverseLabelTranslator.UiTest.Tests
{
    [TestClass]
    public class LoginTest
    {
        [TestMethod]
        public void CanLoginAndOpenTranslatorApp()
        {
            TranslatorApp.Open();

            StringAssert.Contains(UiTestSession.Driver.Url, "appid=5f5c9906-b158-f111-a825-7ced8db48acd");
        }
    }
}
