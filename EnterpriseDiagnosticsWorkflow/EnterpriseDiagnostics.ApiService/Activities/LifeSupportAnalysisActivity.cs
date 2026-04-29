using Dapr.Workflow;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Activities;

internal sealed partial class LifeSupportAnalysisActivity(ILogger<LifeSupportAnalysisActivity> logger)
    : WorkflowActivity<AnalysisRequest, LifeSupportResult>
{
    public override Task<LifeSupportResult> RunAsync(WorkflowActivityContext context, AnalysisRequest input)
    {
        LogStart(logger, input.Stardate);

        var oxygen = 18.0 + Random.Shared.NextDouble() * 5.0;
        var co2 = Random.Shared.NextDouble() * 2.0;
        var severity = (oxygen, co2) switch
        {
            ( < 19.5, _) => "critical",
            (_, > 1.5) => "critical",
            ( < 20.5, _) => "warning",
            (_, > 1.0) => "warning",
            _ => "nominal"
        };
        var notes = severity switch
        {
            "critical" => "Atmospheric composition unsafe for crew. Emergency ventilation required.",
            "warning" => "Atmospheric mix drifting outside ideal range.",
            _ => "Atmospheric composition within crew safety parameters."
        };

        return Task.FromResult(new LifeSupportResult(
            Math.Round(oxygen, 2),
            Math.Round(co2, 2),
            severity,
            notes));
    }

    [LoggerMessage(LogLevel.Information, "Running life support analysis for stardate {Stardate}")]
    static partial void LogStart(ILogger logger, string Stardate);
}
