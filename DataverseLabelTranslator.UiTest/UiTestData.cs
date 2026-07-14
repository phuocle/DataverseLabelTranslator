using System;

namespace DataverseLabelTranslator.UiTest
{
    public static class UiTestData
    {
        public const string GlobalOptionSet = "GlobalOptionSet";
        public const string View = "View";
        public const string WebResource = "WebResource";
        public const string Chart = "Chart";
        public const string ParentNode = "Parent";
        public const string ChildNode = "Child";

        private const string EnglishDisplayText = "000 UiTest English {0} {1}";
        private const string JapaneseDisplayText = "000 UiTest 日本語 {0} {1}";
        private const string VietnameseDisplayText = "000 UiTest Tiếng Việt {0} {1}";
        private const string EnglishDescription = "000 UiTest Description English {0} {1} {2}";
        private const string JapaneseDescription = "000 UiTest 説明 日本語 {0} {1} {2}";
        private const string VietnameseDescription = "000 UiTest Mô tả Tiếng Việt {0} {1} {2}";

        private static readonly object SyncRoot = new object();
        private static DateTime _lastTimestamp = DateTime.MinValue;

        public static string CreateTimestampToken()
        {
            lock (SyncRoot)
            {
                var timestamp = DateTime.Now;
                if (timestamp <= _lastTimestamp)
                {
                    timestamp = _lastTimestamp.AddMilliseconds(1);
                }

                _lastTimestamp = timestamp;
                return timestamp.ToString("ddMMyyyy-HHmmssfff");
            }
        }

        public static TranslationValues CreateDisplayTextValues(string component, string token)
        {
            return new TranslationValues(
                string.Format(EnglishDisplayText, component, token),
                string.Format(JapaneseDisplayText, component, token),
                string.Format(VietnameseDisplayText, component, token));
        }

        public static TranslationValues CreateDescriptionValues(string component, string node, string token)
        {
            return new TranslationValues(
                string.Format(EnglishDescription, component, node, token),
                string.Format(JapaneseDescription, component, node, token),
                string.Format(VietnameseDescription, component, node, token));
        }
    }
}
