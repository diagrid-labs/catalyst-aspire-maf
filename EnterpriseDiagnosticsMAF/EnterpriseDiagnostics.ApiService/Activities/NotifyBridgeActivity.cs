using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;

namespace EnterpriseDiagnostics.ApiService.Activities;

public sealed partial class NotifyBridgeActivity(ILogger<NotifyBridgeActivity> logger)
    : WorkflowActivity<NotifyBridgeInput, bool>
{
    public override Task<bool> RunAsync(WorkflowActivityContext context, NotifyBridgeInput input)
    {
        LogBridgeNotified(input.Stardate, input.Summary);
        return Task.FromResult(true);
    }

    [LoggerMessage(LogLevel.Information, "Bridge notified for stardate {Stardate}: {Summary}")]
    partial void LogBridgeNotified(string stardate, string summary);
}
