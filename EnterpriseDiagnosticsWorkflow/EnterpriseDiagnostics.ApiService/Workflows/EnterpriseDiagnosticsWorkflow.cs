using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Workflows;

internal sealed partial class EnterpriseDiagnosticsWorkflow : Workflow<DiagnosticsInput, DiagnosticsOutput>
{
    public override async Task<DiagnosticsOutput> RunAsync(WorkflowContext context, DiagnosticsInput input)
    {
        var logger = context.CreateReplaySafeLogger<EnterpriseDiagnosticsWorkflow>();
        LogStart(logger, context.InstanceId, input.Stardate);

        var analysisRequest = new AnalysisRequest(input.Stardate);

        var hullTask = context.CallActivityAsync<HullIntegrityResult>(
            nameof(HullIntegrityAnalysisActivity),
            analysisRequest);
        var lifeSupportTask = context.CallActivityAsync<LifeSupportResult>(
            nameof(LifeSupportAnalysisActivity),
            analysisRequest);
        var warpCoreTask = context.CallActivityAsync<WarpCoreResult>(
            nameof(WarpCoreAnalysisActivity),
            analysisRequest);

        await Task.WhenAll(hullTask, lifeSupportTask, warpCoreTask);

        var hull = hullTask.Result;
        var lifeSupport = lifeSupportTask.Result;
        var warpCore = warpCoreTask.Result;

        var summary = await context.CallActivityAsync<SummaryResult>(
            nameof(SummarizeResultsActivity),
            new SummaryInput(hull, lifeSupport, warpCore));

        var bridgeNotified = false;
        if (summary.HasCritical)
        {
            LogCritical(logger, context.InstanceId);
            bridgeNotified = await context.CallActivityAsync<bool>(
                nameof(NotifyBridgeActivity),
                new BridgeNotification(input.Stardate, summary.Summary));
        }

        return new DiagnosticsOutput(
            input.Stardate,
            hull,
            lifeSupport,
            warpCore,
            summary.Summary,
            bridgeNotified);
    }

    [LoggerMessage(LogLevel.Information, "Starting Enterprise diagnostics workflow {InstanceId} for stardate {Stardate}")]
    static partial void LogStart(ILogger logger, string InstanceId, string Stardate);

    [LoggerMessage(LogLevel.Warning, "Critical condition detected in workflow {InstanceId} - notifying bridge")]
    static partial void LogCritical(ILogger logger, string InstanceId);
}
