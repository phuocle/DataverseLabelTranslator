namespace DataverseLabelTranslator.Server.CustomActions
{
    public class CustomActionOutput
    {
        public bool ok { get; set; } = true;
        public string message { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public object @object { get; set; }
    }

    public class CustomActionInput
    {
        public string type { get; set; }
        public string operation { get; set; }
    }

    public static class CustomActionTypes
    {
        public const string Loading = "Loading";
        public const string Saving = "Saving";
        public const string Publishing = "Publishing";
        public const string Published = "Published";
        public const string Other = "Other";
    }
}
