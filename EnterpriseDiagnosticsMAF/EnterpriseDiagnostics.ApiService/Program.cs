using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Hosting;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;
using EnterpriseDiagnostics.ApiService.Tools;
using EnterpriseDiagnostics.ApiService.Workflows;
using OpenAI;

const string Model = "gpt-4o-mini";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

builder.Services.AddSingleton<IChatClient>(_ =>
    new OpenAIClient(apiKey).GetChatClient(Model).AsIChatClient());

AITool[] diagnosticsTools = [AIFunctionFactory.Create(MetricTools.GetRandomPercentage)];

builder.Services.AddDaprAgents(
        opt => opt.AddContext(() => DiagnosticsAgentJsonContext.Default),
        opt =>
        {
            opt.RegisterWorkflow<EnterpriseDiagnosticsWorkflow>();
            opt.RegisterActivity<NotifyBridgeActivity>();
        })
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Hull, name: "HullIntegrityAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.LifeSupport, name: "LifeSupportAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.WarpCore, name: "WarpCoreAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Summarize, name: "SummarizeDiagnosticsAgent"));

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

    public const string Summarize =
        "You are the USS Enterprise diagnostics summarization officer. " +
        "Given hull, life-support, and warp-core readings for a stardate, produce a concise one-paragraph status summary in character. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"summary\": string}.";
}
