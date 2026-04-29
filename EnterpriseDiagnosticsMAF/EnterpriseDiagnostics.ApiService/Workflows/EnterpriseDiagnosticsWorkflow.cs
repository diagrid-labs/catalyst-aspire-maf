using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Runtime;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseDiagnostics.ApiService.Workflows;

public sealed partial class EnterpriseDiagnosticsWorkflow : Workflow<DiagnosticsInput, DiagnosticsOutput>
{
    public override async Task<DiagnosticsOutput> RunAsync(WorkflowContext context, DiagnosticsInput input)
    {
        var logger = context.CreateReplaySafeLogger<EnterpriseDiagnosticsWorkflow>();
        LogStart(logger, context.InstanceId, input.Stardate);

        var hullAgent = context.GetAgent("HullIntegrityAgent");
        var lifeSupportAgent = context.GetAgent("LifeSupportAgent");
        var warpCoreAgent = context.GetAgent("WarpCoreAgent");
        var summarizeAgent = context.GetAgent("SummarizeDiagnosticsAgent");

        var hullTask = context.RunAgentAndDeserializeAsync<HullIntegrityResult>(
            agent: hullAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"integrityPercent\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        var lifeSupportTask = context.RunAgentAndDeserializeAsync<LifeSupportResult>(
            agent: lifeSupportAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"oxygenPercent\": number, \"co2Percent\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        var warpCoreTask = context.RunAgentAndDeserializeAsync<WarpCoreResult>(
            agent: warpCoreAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"dilithiumStability\": number, \"plasmaFlowRate\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        await Task.WhenAll(hullTask, lifeSupportTask, warpCoreTask);

        var hull = hullTask.Result
            ?? throw new InvalidOperationException("HullIntegrityAgent returned no usable response.");
        var lifeSupport = lifeSupportTask.Result
            ?? throw new InvalidOperationException("LifeSupportAgent returned no usable response.");
        var warpCore = warpCoreTask.Result
            ?? throw new InvalidOperationException("WarpCoreAgent returned no usable response.");

        var hasCritical =
            hull.Severity == "CRITICAL" ||
            lifeSupport.Severity == "CRITICAL" ||
            warpCore.Severity == "CRITICAL";

        var summaryResult = await context.RunAgentAndDeserializeAsync<DiagnosticsSummaryResult>(
            agent: summarizeAgent,
            message: $"Stardate {input.Stardate}. " +
                     $"Hull: severity={hull.Severity}, integrity={hull.IntegrityPercent}%, notes={hull.Notes}. " +
                     $"Life Support: severity={lifeSupport.Severity}, O2={lifeSupport.OxygenPercent}%, CO2={lifeSupport.Co2Percent}%, notes={lifeSupport.Notes}. " +
                     $"Warp Core: severity={warpCore.Severity}, dilithium={warpCore.DilithiumStability}%, plasma={warpCore.PlasmaFlowRate}%, notes={warpCore.Notes}. " +
                     $"Overall: {(hasCritical ? "CRITICAL" : "STABLE")}. " +
                     $"Return strict JSON: {{\"summary\": string}}.",
            logger: logger)
            ?? throw new InvalidOperationException("SummarizeDiagnosticsAgent returned no usable response.");
        var summary = summaryResult.Summary;

        var bridgeNotified = false;
        if (hasCritical)
        {
            LogCritical(logger, context.InstanceId);
            bridgeNotified = await context.CallActivityAsync<bool>(
                nameof(NotifyBridgeActivity),
                new NotifyBridgeInput(input.Stardate, summary));
        }

        return new DiagnosticsOutput(
            input.Stardate,
            hull,
            lifeSupport,
            warpCore,
            summary,
            bridgeNotified);
    }

    [LoggerMessage(LogLevel.Information, "Starting Enterprise diagnostics workflow {InstanceId} for stardate {Stardate}")]
    static partial void LogStart(ILogger logger, string InstanceId, string Stardate);

    [LoggerMessage(LogLevel.Warning, "Critical condition detected in workflow {InstanceId} - notifying bridge")]
    static partial void LogCritical(ILogger logger, string InstanceId);
}
