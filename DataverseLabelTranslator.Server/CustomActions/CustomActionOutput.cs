namespace DataverseLabelTranslator.Server.CustomActions
{
    internal class CustomActionOutput
    {
        public bool ok { get; set; } = true;
        public string message { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public object @object { get; set; }
    }

    internal class CustomActionInput
    {
        public string type { get; set; }
    }

    internal static class CustomActionTypes
    {
        public const string Loading = "Loading";
        public const string Saving = "Saving";
        public const string Publishing = "Publishing";
        public const string Published = "Published";
    }
}
