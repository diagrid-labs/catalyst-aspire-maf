using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

public sealed partial class NotifyBridgeActivity(ILogger<NotifyBridgeActivity> logger)
    : WorkflowActivity<NotifyBridgeInput, bool>
{
    public override Task<bool> RunAsync(WorkflowActivityContext context, NotifyBridgeInput input)
    {
        LogBridgeNotified(logger, input.Stardate, input.Summary);
        return Task.FromResult(true);
    }

    [LoggerMessage(LogLevel.Information, "Bridge notified for stardate {Stardate}: {Summary}")]
    static partial void LogBridgeNotified(ILogger logger, string Stardate, string Summary);
}
