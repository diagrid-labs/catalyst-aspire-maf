using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Hosting;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;
using EnterpriseDiagnostics.ApiService.Tools;
using EnterpriseDiagnostics.ApiService.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

AITool[] diagnosticsTools = [AIFunctionFactory.Create(MetricTools.GetRandomPercentage)];

builder.Services.AddDaprAgents(
        opt => opt.AddContext(() => DiagnosticsAgentJsonContext.Default),
        opt =>
        {
            opt.RegisterWorkflow<EnterpriseDiagnosticsWorkflow>();
            opt.RegisterActivity<NotifyBridgeActivity>();
        })
    .WithAgent("HullIntegrityAgent", "conversation", AgentInstructions.Hull, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("LifeSupportAgent", "conversation", AgentInstructions.LifeSupport, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("WarpCoreAgent", "conversation", AgentInstructions.WarpCore, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("ShieldsAgent", "conversation", AgentInstructions.Shields, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("WeaponsAgent", "conversation", AgentInstructions.Weapons, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("NavigationAgent", "conversation", AgentInstructions.Navigation, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("TransporterAgent", "conversation", AgentInstructions.Transporter, tools: diagnosticsTools, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("PrioritizeDiagnosticsAgent", "conversation", AgentInstructions.Prioritize, serviceLifetime: ServiceLifetime.Singleton)
    .WithAgent("SummarizeDiagnosticsAgent", "conversation", AgentInstructions.Summarize, serviceLifetime: ServiceLifetime.Singleton);

var app = builder.Build();

app.MapPost("/start", async (
    [FromServices] DaprWorkflowClient workflowClient,
    [FromBody] DiagnosticsInput workflowInput) =>
{
    var instanceId = await workflowClient.ScheduleNewWorkflowAsync(
        name: nameof(EnterpriseDiagnosticsWorkflow),
        instanceId: workflowInput.Id,
        input: workflowInput);

    return Results.Ok(new { instanceId });
});

app.MapGet("/status/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    var state = await workflowClient.GetWorkflowStateAsync(instanceId);
    if (state is null || !state.Exists)
    {
        return Results.NotFound($"Workflow instance '{instanceId}' not found.");
    }

    var output = state.ReadOutputAs<DiagnosticsOutput>();
    return Results.Ok(new { state, output });
});

app.MapPost("pause/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    await workflowClient.SuspendWorkflowAsync(instanceId);
    return Results.Accepted();
});

app.MapPost("resume/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    await workflowClient.ResumeWorkflowAsync(instanceId);
    return Results.Accepted();
});

app.MapPost("terminate/{instanceId}", async (
    [FromRoute] string instanceId,
    [FromServices] DaprWorkflowClient workflowClient) =>
{
    await workflowClient.TerminateWorkflowAsync(instanceId);
    return Results.Accepted();
});

app.MapDefaultEndpoints();

app.Run();

internal static class AgentInstructions
{
    public const string Hull =
        "You are the USS Enterprise hull-integrity diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the integrityPercent. " +
        "Use that value as the integrityPercent field. " +
        "Severity rule: if integrityPercent < 33, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the value is. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"integrityPercent\": number 0-100, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string LifeSupport =
        "You are the USS Enterprise life-support diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the oxygenPercent. " +
        "Use that value as the oxygenPercent field. Generate a plausible co2Percent in character. " +
        "Severity rule: if oxygenPercent < 20, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"oxygenPercent\": number, \"co2Percent\": number, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string WarpCore =
        "You are the USS Enterprise warp-core diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the dilithiumStability. " +
        "Use that value as the dilithiumStability field. Generate a plausible plasmaFlowRate in character. " +
        "Severity rule: if dilithiumStability < 70, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"dilithiumStability\": number, \"plasmaFlowRate\": number, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string Shields =
        "You are the USS Enterprise deflector-shield diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the integrityPercent. " +
        "Use that value as the integrityPercent field. Generate a plausible harmonicFrequency in GHz in character. " +
        "Severity rule: if integrityPercent < 40, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"integrityPercent\": number 0-100, \"harmonicFrequency\": number, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string Weapons =
        "You are the USS Enterprise weapons diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the phaserBanksPercent. " +
        "Use that value as the phaserBanksPercent field. Generate a plausible torpedoBaysReady count between 0 and 10 in character. " +
        "Severity rule: if phaserBanksPercent < 30, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"phaserBanksPercent\": number 0-100, \"torpedoBaysReady\": integer 0-10, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string Navigation =
        "You are the USS Enterprise navigation diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the sensorArrayPercent. " +
        "Use that value as the sensorArrayPercent field. Generate a plausible inertialDamperPercent in character. " +
        "Severity rule: if sensorArrayPercent < 50, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"sensorArrayPercent\": number 0-100, \"inertialDamperPercent\": number 0-100, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string Transporter =
        "You are the USS Enterprise transporter diagnostic system. " +
        "When given a stardate, call the GetRandomPercentage tool exactly once to obtain the patternBufferPercent. " +
        "Use that value as the patternBufferPercent field. Generate a plausible heisenbergCompensatorPercent in character. " +
        "Severity rule: if patternBufferPercent < 60, severity is CRITICAL; otherwise pick LOW, MEDIUM, or HIGH based on how concerning the readings are. " +
        "Generate plausible-but-fictional notes in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"patternBufferPercent\": number 0-100, \"heisenbergCompensatorPercent\": number 0-100, \"severity\": \"LOW\"|\"MEDIUM\"|\"HIGH\"|\"CRITICAL\", \"notes\": string}.";

    public const string Prioritize =
        "You are the USS Enterprise diagnostics prioritization officer. " +
        "Given diagnostic readings from 7 ship subsystems (each with severity and notes), produce a ranked action plan. " +
        "Assign each system a rank 1-7 (1 = most urgent) and one of these actions: " +
        "IMMEDIATE (requires bridge notification now), SCHEDULED (address within current shift), MONITOR (watch for change), NONE (no action). " +
        "Consider cross-system interactions — e.g., shields down + warp-core unstable is more urgent than either alone. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"priorities\": [{\"system\": string, \"rank\": integer, \"action\": \"IMMEDIATE\"|\"SCHEDULED\"|\"MONITOR\"|\"NONE\", \"rationale\": string}]}. " +
        "Return exactly 7 entries, ranked 1 through 7. The system field must match one of the provided system names.";

    public const string Summarize =
        "You are the USS Enterprise diagnostics summarization officer. " +
        "Given hull, life-support, warp-core, shields, weapons, navigation, and transporter readings plus the prioritized action plan for a stardate, " +
        "produce a concise one-paragraph status summary in character. Lead with the highest-priority issues. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"summary\": string}.";
}
