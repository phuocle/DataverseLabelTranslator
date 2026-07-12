namespace DataverseLabelTranslator.UiTest
{
    public sealed class TranslationValues
    {
        public TranslationValues(object english, object japanese, object vietnamese)
        {
            English = english;
            Japanese = japanese;
            Vietnamese = vietnamese;
        }

        public object English { get; }

        public object Japanese { get; }

        public object Vietnamese { get; }
    }
}
