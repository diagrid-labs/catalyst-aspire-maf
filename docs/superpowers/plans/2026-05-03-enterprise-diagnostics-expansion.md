# Enterprise Diagnostics Workflow Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand `EnterpriseDiagnosticsWorkflow` from 3 parallel diagnostic agents to 7, add a sequential `PrioritizeDiagnosticsAgent` that ranks all results, run `SummarizeDiagnosticsAgent` afterward, and trigger bridge notification when the prioritization agent assigns any system the `IMMEDIATE` action.

**Architecture:** All 7 diagnostic result records implement a shared `IDiagnosticResult` interface (`SystemName`, `Severity`, `Notes`) so the prioritize and summarize prompts can be built from a uniform `IReadOnlyList<IDiagnosticResult>`. The fan-out stays inline and strongly typed; only the sequential aggregation steps consume the interface.

**Tech Stack:** .NET 10, Dapr Workflow (`Dapr.Workflow`), `Diagrid.AI.Microsoft.AgentFramework`, `Microsoft.Extensions.AI`, OpenAI (`gpt-4o-mini`), System.Text.Json source generators.

**Constraints (per user):**
- Do **not** run `git add` or `git commit` during implementation. The user controls all staging and commits.
- The project has **no test project**; verification is via `dotnet build` and a manual smoke test through the AppHost. Tests are out of scope (per spec).

**Spec:** `docs/superpowers/specs/2026-05-03-enterprise-diagnostics-expansion-design.md`

---

## File Structure

| File | Change |
|------|--------|
| `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsModels.cs` | Add `IDiagnosticResult`. Existing 3 records implement it. Add 4 new result records. Add `PriorityEntry`, `PrioritizationResult`. Update `DiagnosticsOutput`. |
| `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsAgentJsonContext.cs` | Add 6 `[JsonSerializable]` attributes. |
| `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Program.cs` | Register 4 new diagnostic agents + prioritize agent. Add 5 new instruction strings. Update `Summarize` instruction. |
| `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Workflows/EnterpriseDiagnosticsWorkflow.cs` | Expand fan-out 3→7. Add prioritize call. Update summarize call. Swap notification trigger. Add two private prompt-builder helpers. Update `DiagnosticsOutput` construction. |

No changes to `Tools/MetricTools.cs`, `Activities/NotifyBridgeActivity.cs`, the AppHost project, or ServiceDefaults.

---

## Task 1: Add `IDiagnosticResult` interface and apply to existing records

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsModels.cs`

- [ ] **Step 1: Add the interface and update existing 3 records**

Open `Models/DiagnosticsModels.cs`. Replace the file's contents with the version below. This adds `IDiagnosticResult` and makes `HullIntegrityResult`, `LifeSupportResult`, `WarpCoreResult` implement it via a constant `SystemName` property. Existing serialized properties are unchanged.

```csharp
using System.Text.Json.Serialization;

namespace EnterpriseDiagnostics.ApiService.Models;

public interface IDiagnosticResult
{
    string SystemName { get; }
    string Severity { get; }
    string Notes { get; }
}

public record DiagnosticsInput(string Id, string Stardate);

public record DiagnosticsOutput(
    string Stardate,
    HullIntegrityResult HullIntegrity,
    LifeSupportResult LifeSupport,
    WarpCoreResult WarpCore,
    string Summary,
    bool BridgeNotified);

public record HullIntegrityResult(
    [property: JsonPropertyName("integrityPercent")] double IntegrityPercent,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "HullIntegrity";
}

public record LifeSupportResult(
    [property: JsonPropertyName("oxygenPercent")] double OxygenPercent,
    [property: JsonPropertyName("co2Percent")] double Co2Percent,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "LifeSupport";
}

public record WarpCoreResult(
    [property: JsonPropertyName("dilithiumStability")] double DilithiumStability,
    [property: JsonPropertyName("plasmaFlowRate")] double PlasmaFlowRate,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "WarpCore";
}

public record DiagnosticsSummaryResult(
    [property: JsonPropertyName("summary")] string Summary);

public record NotifyBridgeInput(string Stardate, string Summary);
```

- [ ] **Step 2: Build to verify**

Run from the repo root:
```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: Build succeeds. `DiagnosticsOutput` is unchanged at this point so existing call sites still compile.

---

## Task 2: Add 4 new diagnostic result records

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsModels.cs`

- [ ] **Step 1: Append the 4 new records**

In `Models/DiagnosticsModels.cs`, immediately after the `WarpCoreResult` record block, add:

```csharp
public record ShieldsResult(
    [property: JsonPropertyName("integrityPercent")] double IntegrityPercent,
    [property: JsonPropertyName("harmonicFrequency")] double HarmonicFrequency,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "Shields";
}

public record WeaponsResult(
    [property: JsonPropertyName("phaserBanksPercent")] double PhaserBanksPercent,
    [property: JsonPropertyName("torpedoBaysReady")] int TorpedoBaysReady,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "Weapons";
}

public record NavigationResult(
    [property: JsonPropertyName("sensorArrayPercent")] double SensorArrayPercent,
    [property: JsonPropertyName("inertialDamperPercent")] double InertialDamperPercent,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "Navigation";
}

public record TransporterResult(
    [property: JsonPropertyName("patternBufferPercent")] double PatternBufferPercent,
    [property: JsonPropertyName("heisenbergCompensatorPercent")] double HeisenbergCompensatorPercent,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes) : IDiagnosticResult
{
    [JsonIgnore]
    public string SystemName => "Transporter";
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: Build succeeds.

---

## Task 3: Add prioritization records and update `DiagnosticsOutput`

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsModels.cs`

- [ ] **Step 1: Add prioritization records**

In `Models/DiagnosticsModels.cs`, immediately after the `TransporterResult` record block, add:

```csharp
public record PriorityEntry(
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("rationale")] string Rationale);

public record PrioritizationResult(
    [property: JsonPropertyName("priorities")] IReadOnlyList<PriorityEntry> Priorities);
```

- [ ] **Step 2: Update `DiagnosticsOutput`**

Replace the existing `DiagnosticsOutput` declaration with:

```csharp
public record DiagnosticsOutput(
    string Stardate,
    HullIntegrityResult HullIntegrity,
    LifeSupportResult LifeSupport,
    WarpCoreResult WarpCore,
    ShieldsResult Shields,
    WeaponsResult Weapons,
    NavigationResult Navigation,
    TransporterResult Transporter,
    IReadOnlyList<PriorityEntry> Priorities,
    string Summary,
    bool BridgeNotified);
```

- [ ] **Step 3: Build (expected to fail)**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: **FAILS** at `EnterpriseDiagnosticsWorkflow.cs:70-76` because the workflow constructs `DiagnosticsOutput` with the old 6-arg shape. This is intentional — the workflow rewrite (Task 8) will fix it. Move on to Task 4.

---

## Task 4: Update `DiagnosticsAgentJsonContext`

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsAgentJsonContext.cs`

- [ ] **Step 1: Add 6 `[JsonSerializable]` attributes**

Replace the file contents with:

```csharp
using System.Text.Json.Serialization;

namespace EnterpriseDiagnostics.ApiService.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HullIntegrityResult))]
[JsonSerializable(typeof(LifeSupportResult))]
[JsonSerializable(typeof(WarpCoreResult))]
[JsonSerializable(typeof(ShieldsResult))]
[JsonSerializable(typeof(WeaponsResult))]
[JsonSerializable(typeof(NavigationResult))]
[JsonSerializable(typeof(TransporterResult))]
[JsonSerializable(typeof(PriorityEntry))]
[JsonSerializable(typeof(PrioritizationResult))]
[JsonSerializable(typeof(DiagnosticsSummaryResult))]
public partial class DiagnosticsAgentJsonContext : JsonSerializerContext;
```

- [ ] **Step 2: Build (still expected to fail at workflow)**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: still fails at the workflow's `DiagnosticsOutput` construction. The new types compile; only the workflow lags. Proceed to Task 5.

---

## Task 5: Add agent instruction strings and update `Summarize`

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Program.cs`

- [ ] **Step 1: Append 5 new instruction constants**

In the `AgentInstructions` static class at the bottom of `Program.cs`, immediately after the `WarpCore` constant and before the `Summarize` constant, add five new constants:

```csharp
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
```

- [ ] **Step 2: Replace the `Summarize` instruction**

Find the existing `public const string Summarize = ...` block in `AgentInstructions` and replace it with:

```csharp
    public const string Summarize =
        "You are the USS Enterprise diagnostics summarization officer. " +
        "Given hull, life-support, warp-core, shields, weapons, navigation, and transporter readings plus the prioritized action plan for a stardate, " +
        "produce a concise one-paragraph status summary in character. Lead with the highest-priority issues. " +
        "Always respond with strict JSON only — no prose, no code fences. " +
        "Schema: {\"summary\": string}.";
```

- [ ] **Step 3: Build (still expected to fail at workflow)**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: still fails only at the workflow's `DiagnosticsOutput` construction. New constants compile.

---

## Task 6: Register 5 new agents in `Program.cs`

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Program.cs`

- [ ] **Step 1: Add five `.WithAgent(...)` chains**

Find the existing `AddDaprAgents(...)` chain. After the `SummarizeDiagnosticsAgent` registration (`name: "SummarizeDiagnosticsAgent"`), append five new chained `.WithAgent(...)` calls so the full chain reads:

```csharp
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
        .AsAIAgent(instructions: AgentInstructions.Shields, name: "ShieldsAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Weapons, name: "WeaponsAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Navigation, name: "NavigationAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Transporter, name: "TransporterAgent", tools: diagnosticsTools))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Prioritize, name: "PrioritizeDiagnosticsAgent"))
    .WithAgent(sp => sp.GetRequiredService<IChatClient>()
        .AsAIAgent(instructions: AgentInstructions.Summarize, name: "SummarizeDiagnosticsAgent"));
```

Note: `PrioritizeDiagnosticsAgent` does **not** receive `tools:` — it has no tool access. Order matters only for readability; runtime resolution is by name.

- [ ] **Step 2: Build (still expected to fail at workflow)**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: still fails only at the workflow.

---

## Task 7: Add prompt-builder helpers to the workflow

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Workflows/EnterpriseDiagnosticsWorkflow.cs`

- [ ] **Step 1: Add two private static helpers**

In `EnterpriseDiagnosticsWorkflow.cs`, immediately after the `LogCritical` `[LoggerMessage]` declaration (the last member of the class) and before the closing `}` of the class, add:

```csharp
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
```

These helpers depend on `System.Linq` (already implicitly available via global usings in modern .NET projects) and on `IDiagnosticResult` / `PriorityEntry` defined earlier.

- [ ] **Step 2: Add `using System.Linq;` if global usings do not already cover it**

Open `EnterpriseDiagnosticsWorkflow.cs`. The current `using` block is:
```csharp
using Dapr.Workflow;
using Diagrid.AI.Microsoft.AgentFramework.Runtime;
using EnterpriseDiagnostics.ApiService.Activities;
using EnterpriseDiagnostics.ApiService.Models;
```
.NET 10 SDK enables implicit usings by default for ASP.NET Core projects (`<ImplicitUsings>enable</ImplicitUsings>` brings in `System.Linq`). If the build in Step 3 errors with "The name 'Select' does not exist", explicitly add `using System.Linq;` at the top.

- [ ] **Step 3: Build (still expected to fail at the `RunAsync` body)**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: still fails only at `EnterpriseDiagnosticsWorkflow.RunAsync` (the `DiagnosticsOutput` constructor mismatch). The two new helpers compile cleanly.

---

## Task 8: Rewrite `RunAsync` — 7-fan-out, prioritize, summarize, action-based notification

**Files:**
- Modify: `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Workflows/EnterpriseDiagnosticsWorkflow.cs`

This task replaces the entire body of `RunAsync` plus the immediately following null-check / summarize / notify section. The two helpers from Task 7 and the `[LoggerMessage]` declarations stay where they are.

- [ ] **Step 1: Replace the `RunAsync` method body**

In `EnterpriseDiagnosticsWorkflow.cs`, replace the existing `RunAsync` method (lines 10-77 in the current file) with:

```csharp
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
```

- [ ] **Step 2: Build to verify the project compiles**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/EnterpriseDiagnostics.ApiService.csproj
```
Expected: **Build succeeds.** No errors, no warnings related to the new code. If a `System.Linq` error appears, add `using System.Linq;` at the top of `EnterpriseDiagnosticsWorkflow.cs` (see Task 7 Step 2 note).

- [ ] **Step 3: Build the AppHost**

```bash
dotnet build EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.AppHost/EnterpriseDiagnostics.AppHost.csproj
```
Expected: Build succeeds.

---

## Task 9: Smoke test via AppHost

**Files:** none (manual verification)

This task confirms the workflow runs end-to-end against OpenAI. Requires `OPENAI_API_KEY` set in the environment.

- [ ] **Step 1: Start the AppHost**

In a terminal:
```bash
cd EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.AppHost
dotnet run
```
Expected: Aspire dashboard URL is printed; the `apiservice` resource starts and reaches "Running" state. Note the API service base URL from the dashboard (or the console output).

- [ ] **Step 2: Trigger the workflow**

In a second terminal, with `<API_BASE>` set to the API service URL printed by Aspire:
```bash
curl -X POST "<API_BASE>/start" \
  -H "Content-Type: application/json" \
  -d '{"id":"smoke-1","stardate":"49827.5"}'
```
Expected: HTTP 200 with `{"instanceId":"smoke-1"}`.

- [ ] **Step 3: Poll status until completed**

```bash
curl "<API_BASE>/status/smoke-1"
```
Repeat every few seconds until `state.runtimeStatus` is `Completed`. The workflow runs 7 agent calls in parallel followed by 2 sequential calls, so expect ~10-30 seconds total depending on OpenAI latency.

- [ ] **Step 4: Verify the output shape**

Inspect the `output` field in the response. Confirm:
- All 7 result fields present: `hullIntegrity`, `lifeSupport`, `warpCore`, `shields`, `weapons`, `navigation`, `transporter`.
- `priorities` is an array of 7 entries, each with `system`, `rank`, `action`, `rationale`.
- `summary` is a non-empty string.
- `bridgeNotified` is `true` iff at least one priority entry has `action == "IMMEDIATE"`.

- [ ] **Step 5: Verify bridge-notification path on a critical run**

Re-run Steps 2-4 a few times with different `id` values. With seven agents each rolling random percentages against varied CRITICAL thresholds (40, 30, 50, 60), at least one run should produce an `IMMEDIATE` priority. When it does, confirm:
- Server logs show `"Critical condition detected in workflow ... - notifying bridge"`.
- Server logs show `"Bridge notified for stardate ..."` from `NotifyBridgeActivity`.
- Output `bridgeNotified` is `true`.

If 5 runs go by with no `IMMEDIATE`, that's still acceptable — the LLM has discretion. To force the path, temporarily lower one threshold in `AgentInstructions` (e.g., set `Hull` threshold to `< 99`) and re-run; revert after verifying.

- [ ] **Step 6: Stop the AppHost**

`Ctrl+C` in the AppHost terminal.

---

## Task 10: Hand off changed files to user for review and commit

**Files:** none

- [ ] **Step 1: Summarize the changes**

Per user instruction, do **not** run `git add` or `git commit`. Instead, run `git status` and `git diff --stat` to surface what changed, and report:

```bash
git -C /Users/marcduiker/dev/diagrid-labs/catalyst-aspire-maf status
git -C /Users/marcduiker/dev/diagrid-labs/catalyst-aspire-maf diff --stat
```

Expected files modified:
- `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsModels.cs`
- `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Models/DiagnosticsAgentJsonContext.cs`
- `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Program.cs`
- `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Workflows/EnterpriseDiagnosticsWorkflow.cs`

Plus new untracked files in `docs/superpowers/` (the spec and this plan).

Tell the user the implementation is complete and verified, and that staging/committing is theirs to do.
