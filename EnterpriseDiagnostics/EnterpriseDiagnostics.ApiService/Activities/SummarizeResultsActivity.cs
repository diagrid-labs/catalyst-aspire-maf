using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

internal sealed partial class SummarizeResultsActivity(ILogger<SummarizeResultsActivity> logger)
    : WorkflowActivity<SummaryInput, SummaryResult>
{
    public override Task<SummaryResult> RunAsync(WorkflowActivityContext context, SummaryInput input)
    {
        LogStart(logger);

        var hasCritical =
            input.HullIntegrity.Severity == "critical" ||
            input.LifeSupport.Severity == "critical" ||
            input.WarpCore.Severity == "critical";

        var summary =
            $"Hull: {input.HullIntegrity.Severity} ({input.HullIntegrity.IntegrityPercent}%). " +
            $"Life Support: {input.LifeSupport.Severity} (O2 {input.LifeSupport.OxygenPercent}%, CO2 {input.LifeSupport.Co2Percent}%). " +
            $"Warp Core: {input.WarpCore.Severity} (Dilithium {input.WarpCore.DilithiumStability}%, Plasma {input.WarpCore.PlasmaFlowRate}%). " +
            $"Overall status: {(hasCritical ? "CRITICAL" : "STABLE")}.";

        return Task.FromResult(new SummaryResult(summary, hasCritical));
    }

    [LoggerMessage(LogLevel.Information, "Summarizing diagnostic results")]
    static partial void LogStart(ILogger logger);
}
