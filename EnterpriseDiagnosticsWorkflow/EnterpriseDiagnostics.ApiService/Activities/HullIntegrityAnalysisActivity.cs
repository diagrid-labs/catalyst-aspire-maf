using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

internal sealed partial class HullIntegrityAnalysisActivity(ILogger<HullIntegrityAnalysisActivity> logger)
    : WorkflowActivity<AnalysisRequest, HullIntegrityResult>
{
    public override Task<HullIntegrityResult> RunAsync(WorkflowActivityContext context, AnalysisRequest input)
    {
        LogStart(logger, input.Stardate);

        var integrity = Random.Shared.NextDouble() * 100.0;
        var severity = integrity switch
        {
            < 50 => "critical",
            < 80 => "warning",
            _ => "nominal"
        };
        var notes = severity switch
        {
            "critical" => "Hull breaches detected on multiple decks. Immediate repairs required.",
            "warning" => "Minor stress fractures detected on outer plating.",
            _ => "Hull plating within structural tolerances."
        };

        return Task.FromResult(new HullIntegrityResult(Math.Round(integrity, 2), severity, notes));
    }

    [LoggerMessage(LogLevel.Information, "Running hull integrity analysis for stardate {Stardate}")]
    static partial void LogStart(ILogger logger, string Stardate);
}
