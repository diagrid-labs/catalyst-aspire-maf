using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

internal sealed partial class NotifyBridgeActivity(ILogger<NotifyBridgeActivity> logger)
    : WorkflowActivity<BridgeNotification, bool>
{
    public override Task<bool> RunAsync(WorkflowActivityContext context, BridgeNotification input)
    {
        LogNotify(logger, input.Stardate, input.Message);

        // TODO: integrate with the actual bridge communications system

        return Task.FromResult(true);
    }

    [LoggerMessage(LogLevel.Warning, "RED ALERT to Bridge (stardate {Stardate}): {Message}")]
    static partial void LogNotify(ILogger logger, string Stardate, string Message);
}
