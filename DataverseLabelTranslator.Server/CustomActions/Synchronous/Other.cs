using Microsoft.Xrm.Sdk;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous
{
    public class OtherAction : ICustomAction
    {
        public object Loading(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("Other Loading operation is not supported.");
        }

        public object Saving(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("Other Saving operation is not supported.");
        }

        public object Publishing(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("Other Publishing operation is not supported.");
        }

        public object Published(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            throw new InvalidPluginExecutionException("Other Published operation is not supported.");
        }

        public object Other(IPluginExecutionContext context, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing, string json)
        {
            var input = OtherSupport.Deserialize<CustomActionInput>(json);
            var operation = OtherSupport.Normalize(input.operation);

            if (operation == OtherProvider.NormalizedOperationName)
            {
                return new OtherProvider().Execute(serviceAdmin);
            }

            if (operation == OtherTranslate.NormalizedOperationName)
            {
                return new OtherTranslate().Execute(serviceAdmin, json);
            }

            if (OtherStorage.IsStorageOperation(input.operation))
            {
                return new OtherStorage().Execute(serviceAdmin, json);
            }

            throw new InvalidPluginExecutionException("Other operation is required.");
        }
    }
}
