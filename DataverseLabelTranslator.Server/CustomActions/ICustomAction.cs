using Microsoft.Xrm.Sdk;

namespace DataverseLabelTranslator.Server.CustomActions
{
    internal interface ICustomAction
    {
        CustomActionOutput Execute(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json);
    }
}
