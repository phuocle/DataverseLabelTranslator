using DataverseLabelTranslator.Server.CustomActions.Synchronous;
using DataverseLabelTranslator.Shared;
using Microsoft.Xrm.Sdk;
using System;

namespace DataverseLabelTranslator.Server.CustomActions
{
    [CrmPluginRegistration("pl_DataverseLabelTranslatorCustomAction", "none", StageEnum.PostOperation, ExecutionModeEnum.Synchronous, "", "DataverseLabelTranslator.Server.CustomActions.PostDataverseLabelTranslatorCustomActionSynchronous", 1, IsolationModeEnum.Sandbox, PluginType = PluginType.CustomAction)]
    public class PostDataverseLabelTranslatorCustomActionSynchronous : IPlugin
    {
        /*
        InputParameters:
            f         System.String - require
            input     System.String
        OutputParameters:
            output    System.String - require
        */

        //private readonly string unSecureConfiguration = null;
        //private readonly string secureConfiguration = null;
        //public PostDataverseLabelTranslatorCustomActionSynchronous(string unSecureConfiguration, string secureConfiguration)
        //{
        //    this.unSecureConfiguration = unSecureConfiguration;
        //    this.secureConfiguration = secureConfiguration;
        //}

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (!int.Equals(context.Stage, (int)StageEnum.PostOperation)) throw new InvalidPluginExecutionException("Stage does not equals PostOperation");
            if (!string.Equals(context.MessageName, "pl_DataverseLabelTranslatorCustomAction", StringComparison.OrdinalIgnoreCase)) throw new InvalidPluginExecutionException("MessageName does not equals pl_DataverseLabelTranslatorCustomAction");
            if (!string.Equals(context.PrimaryEntityName, "none", StringComparison.OrdinalIgnoreCase)) throw new InvalidPluginExecutionException("PrimaryEntityName does not equals none");
            if (!int.Equals(context.Mode, (int)ExecutionModeEnum.Synchronous)) throw new InvalidPluginExecutionException("Execution does not equals Synchronous");
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var serviceAdmin = serviceFactory.CreateOrganizationService(null);
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            tracing?.DebugContext(context);

            var outputs = ExecuteCustomAction(context, serviceFactory, serviceAdmin, service, tracing);
            foreach (var output in outputs)
            {
                if (context.OutputParameters.Contains(output.Key))
                {
                    context.OutputParameters[output.Key] = output.Value;
                }
            }
        }

        private ParameterCollection ExecuteCustomAction(IPluginExecutionContext context, IOrganizationServiceFactory serviceFactory, IOrganizationService serviceAdmin, IOrganizationService service, ITracingService tracing)
        {
            var outputs = new ParameterCollection();
            var f = context.InputParameters.Contains("f") ? context.InputParameters["f"] as string : null;
            var input = context.InputParameters.Contains("input") ? context.InputParameters["input"] as string : null;
            if (string.IsNullOrWhiteSpace(f))
            {
                throw new InvalidPluginExecutionException("Missing required parameter: f");
            }

            var action = default(ICustomAction);
            switch (f)
            {
                case ActionNames.Dashboard:
                    action = new Dashboard();
                    break;
                case ActionNames.GlobalOptionSet:
                    action = new GlobalOptionSet();
                    break;
                case ActionNames.WebResource:
                    action = new WebResource();
                    break;
                default:
                    throw new InvalidPluginExecutionException($"Unsupported action: {f}");
            }

            var inputType = DevKitJson.Deserialize<CustomActionInput>(input);
            if (inputType == null || string.IsNullOrWhiteSpace(inputType.type))
            {
                throw new InvalidPluginExecutionException("Missing required input type.");
            }

            object phaseOutput;
            switch (inputType.type)
            {
                case CustomActionTypes.Loading:
                    phaseOutput = action.Loading(context, serviceAdmin, service, tracing, input);
                    break;
                case CustomActionTypes.Saving:
                    phaseOutput = action.Saving(context, serviceAdmin, service, tracing, input);
                    break;
                case CustomActionTypes.Publishing:
                    phaseOutput = action.Publishing(context, serviceAdmin, service, tracing, input);
                    break;
                case CustomActionTypes.Published:
                    phaseOutput = action.Published(context, serviceAdmin, service, tracing, input);
                    break;
                case CustomActionTypes.Other:
                    phaseOutput = action.Other(context, serviceAdmin, service, tracing, input);
                    break;
                default:
                    throw new InvalidPluginExecutionException($"Unsupported custom action type: {inputType.type}");
            }

            var output = new CustomActionOutput
            {
                ok = true,
                type = inputType.type,
                @object = phaseOutput
            };

            outputs.Add("output", DevKitJson.Serialize(output));
            return outputs;
        }
    }
}
