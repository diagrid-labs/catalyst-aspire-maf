using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Runtime;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;

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
        var shieldsAgent = context.GetAgent("ShieldsAgent");
        var weaponsAgent = context.GetAgent("WeaponsAgent");
        var navigationAgent = context.GetAgent("NavigationAgent");
        var transporterAgent = context.GetAgent("TransporterAgent");
        var prioritizeAgent = context.GetAgent("PrioritizeDiagnosticsAgent");
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

        var shieldsTask = context.RunAgentAndDeserializeAsync<ShieldsResult>(
            agent: shieldsAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"integrityPercent\": number, \"harmonicFrequency\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        var weaponsTask = context.RunAgentAndDeserializeAsync<WeaponsResult>(
            agent: weaponsAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"phaserBanksPercent\": number, \"torpedoBaysReady\": integer, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        var navigationTask = context.RunAgentAndDeserializeAsync<NavigationResult>(
            agent: navigationAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"sensorArrayPercent\": number, \"inertialDamperPercent\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        var transporterTask = context.RunAgentAndDeserializeAsync<TransporterResult>(
            agent: transporterAgent,
            message: $"Generate diagnostics for stardate {input.Stardate}. Return strict JSON: {{\"patternBufferPercent\": number, \"heisenbergCompensatorPercent\": number, \"severity\": string, \"notes\": string}}.",
            logger: logger);

        await Task.WhenAll(
            hullTask, lifeSupportTask, warpCoreTask,
            shieldsTask, weaponsTask, navigationTask, transporterTask);

        var hull = hullTask.Result
            ?? throw new InvalidOperationException("HullIntegrityAgent returned no usable response.");
        var lifeSupport = lifeSupportTask.Result
            ?? throw new InvalidOperationException("LifeSupportAgent returned no usable response.");
        var warpCore = warpCoreTask.Result
            ?? throw new InvalidOperationException("WarpCoreAgent returned no usable response.");
        var shields = shieldsTask.Result
            ?? throw new InvalidOperationException("ShieldsAgent returned no usable response.");
        var weapons = weaponsTask.Result
            ?? throw new InvalidOperationException("WeaponsAgent returned no usable response.");
        var navigation = navigationTask.Result
            ?? throw new InvalidOperationException("NavigationAgent returned no usable response.");
        var transporter = transporterTask.Result
            ?? throw new InvalidOperationException("TransporterAgent returned no usable response.");

        IReadOnlyList<IDiagnosticResult> diagnostics =
            [hull, lifeSupport, warpCore, shields, weapons, navigation, transporter];

        var prioritization = await context.RunAgentAndDeserializeAsync<PrioritizationResult>(
            agent: prioritizeAgent,
            message: BuildPrioritizationPrompt(input.Stardate, diagnostics),
            logger: logger)
            ?? throw new InvalidOperationException("PrioritizeDiagnosticsAgent returned no usable response.");

        if (prioritization.Priorities is null || prioritization.Priorities.Count != 7)
        {
            throw new InvalidOperationException(
                $"PrioritizeDiagnosticsAgent returned {prioritization.Priorities?.Count ?? 0} priorities; expected exactly 7.");
        }

        var summaryResult = await context.RunAgentAndDeserializeAsync<DiagnosticsSummaryResult>(
            agent: summarizeAgent,
            message: BuildSummaryPrompt(input.Stardate, diagnostics, prioritization.Priorities),
            logger: logger)
            ?? throw new InvalidOperationException("SummarizeDiagnosticsAgent returned no usable response.");
        var summary = summaryResult.Summary;

        var hasImmediate = prioritization.Priorities.Any(p => p.Action == "IMMEDIATE");

        var bridgeNotified = false;
        if (hasImmediate)
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
            shields,
            weapons,
            navigation,
            transporter,
            prioritization.Priorities,
            summary,
            bridgeNotified);
    }

    private static string BuildPrioritizationPrompt(
        string stardate,
        IReadOnlyList<IDiagnosticResult> diagnostics)
    {
        var lines = string.Join(
            " ",
            diagnostics.Select(d =>
                $"{d.SystemName}: severity={d.Severity}, notes={d.Notes}."));

        return $"Stardate {stardate}. {lines} " +
               "Rank these 7 systems 1-7 (1 = most urgent) and assign each an action " +
               "(IMMEDIATE, SCHEDULED, MONITOR, NONE) with a brief rationale. " +
               "Return strict JSON: {\"priorities\": [{\"system\": string, \"rank\": integer, \"action\": string, \"rationale\": string}]}.";
    }

    private static string BuildSummaryPrompt(
        string stardate,
        IReadOnlyList<IDiagnosticResult> diagnostics,
        IReadOnlyList<PriorityEntry> priorities)
    {
        var diagnosticLines = string.Join(
            " ",
            diagnostics.Select(d =>
                $"{d.SystemName}: severity={d.Severity}, notes={d.Notes}."));

        var priorityLines = string.Join(
            " ",
            priorities
                .OrderBy(p => p.Rank)
                .Select(p =>
                    $"[rank={p.Rank} system={p.System} action={p.Action} rationale={p.Rationale}]"));

        return $"Stardate {stardate}. " +
               $"Diagnostics: {diagnosticLines} " +
               $"Priorities: {priorityLines} " +
               "Return strict JSON: {\"summary\": string}.";
    }

    [LoggerMessage(LogLevel.Information, "Starting Enterprise diagnostics workflow {InstanceId} for stardate {StarDate}")]
    static partial void LogStart(ILogger logger, string instanceId, string starDate);

    [LoggerMessage(LogLevel.Warning, "Critical condition detected in workflow {InstanceId} - notifying bridge")]
    static partial void LogCritical(ILogger logger, string instanceId);
}
