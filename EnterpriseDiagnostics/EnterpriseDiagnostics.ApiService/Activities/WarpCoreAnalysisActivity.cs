using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

internal sealed partial class WarpCoreAnalysisActivity(ILogger<WarpCoreAnalysisActivity> logger)
    : WorkflowActivity<AnalysisRequest, WarpCoreResult>
{
    public override Task<WarpCoreResult> RunAsync(WorkflowActivityContext context, AnalysisRequest input)
    {
        LogStart(logger, input.Stardate);

        var dilithium = 60.0 + Random.Shared.NextDouble() * 40.0;
        var plasma = 70.0 + Random.Shared.NextDouble() * 40.0;
        var severity = (dilithium, plasma) switch
        {
            ( < 70.0, _) => "critical",
            (_, < 80.0 or > 105.0) => "critical",
            ( < 85.0, _) => "warning",
            (_, < 90.0 or > 100.0) => "warning",
            _ => "nominal"
        };
        var notes = severity switch
        {
            "critical" => "Warp core instability detected. Risk of breach. Eject if necessary.",
            "warning" => "Warp core operating outside optimal range.",
            _ => "Warp core operating within nominal parameters."
        };

        return Task.FromResult(new WarpCoreResult(
            Math.Round(dilithium, 2),
            Math.Round(plasma, 2),
            severity,
            notes));
    }

    [LoggerMessage(LogLevel.Information, "Running warp core analysis for stardate {Stardate}")]
    static partial void LogStart(ILogger logger, string Stardate);
}
