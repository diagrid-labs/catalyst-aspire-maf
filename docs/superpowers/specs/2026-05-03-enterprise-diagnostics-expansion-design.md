# Enterprise Diagnostics Workflow Expansion — Design

**Date:** 2026-05-03
**Status:** Approved for planning
**Target file:** `EnterpriseDiagnosticsMAF/EnterpriseDiagnostics.ApiService/Workflows/EnterpriseDiagnosticsWorkflow.cs`

## Goal

Expand the `EnterpriseDiagnosticsWorkflow` from 3 parallel diagnostic agents to 7, then add a sequential `PrioritizeDiagnosticsAgent` that consumes all 7 results and produces a ranked action plan. The existing `SummarizeDiagnosticsAgent` runs after prioritization. The bridge-notification trigger moves from severity-based to prioritization-driven.

## Current state

- Three diagnostic agents (`HullIntegrityAgent`, `LifeSupportAgent`, `WarpCoreAgent`) run in parallel, each calling the `GetRandomPercentage` tool once and returning strict JSON.
- After fan-out, `SummarizeDiagnosticsAgent` produces a one-paragraph summary.
- If any of the 3 results has `severity == "CRITICAL"`, `NotifyBridgeActivity` is invoked.

## Target state

```
[Hull] [LifeSupport] [WarpCore] [Shields] [Weapons] [Navigation] [Transporter]   (parallel fan-out)
                                  │
                                  ▼
                       PrioritizeDiagnosticsAgent                                 (sequential)
                                  │
                                  ▼
                       SummarizeDiagnosticsAgent                                  (sequential)
                                  │
                                  ▼
                  if any priority.action == "IMMEDIATE"
                       → NotifyBridgeActivity
```

## Section 1 — Models

**New common interface** (added to `Models/DiagnosticsModels.cs`):

```csharp
public interface IDiagnosticResult
{
    string SystemName { get; }
    string Severity { get; }
    string Notes { get; }
}
```

The interface members are non-serialized (no `[JsonPropertyName]`). `SystemName` is implemented as a hardcoded constant per record.

**Existing 3 records** (`HullIntegrityResult`, `LifeSupportResult`, `WarpCoreResult`) gain `: IDiagnosticResult` and a `SystemName` constant property:

```csharp
public record HullIntegrityResult(...) : IDiagnosticResult
{
    public string SystemName => "HullIntegrity";
}
```

The existing `Severity` and `Notes` properties already match the interface.

**Four new result records** (same `IDiagnosticResult` pattern, two metrics each):

| Record | Primary metric (from tool) | Secondary metric (LLM) | CRITICAL threshold |
|--------|----------------------------|------------------------|--------------------|
| `ShieldsResult` | `integrityPercent` | `harmonicFrequency` (GHz, double) | `< 40` |
| `WeaponsResult` | `phaserBanksPercent` | `torpedoBaysReady` (int 0-10) | `< 30` |
| `NavigationResult` | `sensorArrayPercent` | `inertialDamperPercent` | `< 50` |
| `TransporterResult` | `patternBufferPercent` | `heisenbergCompensatorPercent` | `< 60` |

All follow the existing JSON shape: `{ <primary>, <secondary>, severity, notes }`.

**Prioritization output**:

```csharp
public record PriorityEntry(
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("action")] string Action,        // IMMEDIATE | SCHEDULED | MONITOR | NONE
    [property: JsonPropertyName("rationale")] string Rationale);

public record PrioritizationResult(
    [property: JsonPropertyName("priorities")] IReadOnlyList<PriorityEntry> Priorities);
```

**Updated `DiagnosticsOutput`**:

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

**`DiagnosticsAgentJsonContext`** gets six new `[JsonSerializable]` attributes: `ShieldsResult`, `WeaponsResult`, `NavigationResult`, `TransporterResult`, `PriorityEntry`, `PrioritizationResult`.

## Section 2 — Agent registration & instructions

**`Program.cs`** registers four new diagnostic agents and one new sequential agent. The four diagnostic agents reuse the same `diagnosticsTools` array:

```csharp
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
```

`PrioritizeDiagnosticsAgent` has **no tools** — pure reasoning.

**Instruction strings** added to `AgentInstructions`. The four diagnostic prompts follow the existing template (call `GetRandomPercentage` once for the primary metric, generate the secondary metric in character, apply CRITICAL threshold rule, return strict JSON).

**Prioritize instruction:**
> You are the USS Enterprise diagnostics prioritization officer. Given diagnostic readings from 7 ship subsystems (each with severity and notes), produce a ranked action plan. Assign each system a rank 1-7 (1 = most urgent) and one of these actions: IMMEDIATE (requires bridge notification now), SCHEDULED (address within current shift), MONITOR (watch for change), NONE (no action). Consider cross-system interactions — e.g., shields down + warp-core unstable is more urgent than either alone. Always respond with strict JSON only — no prose, no code fences. Schema: `{"priorities": [{"system": string, "rank": int, "action": "IMMEDIATE"|"SCHEDULED"|"MONITOR"|"NONE", "rationale": string}]}`. Return exactly 7 entries, ranked 1 through 7.

**Summarize instruction (updated):**
> You are the USS Enterprise diagnostics summarization officer. Given hull, life-support, warp-core, shields, weapons, navigation, and transporter readings plus the prioritized action plan for a stardate, produce a concise one-paragraph status summary in character. Lead with the highest-priority issues. Always respond with strict JSON only — no prose, no code fences. Schema: `{"summary": string}`.

## Section 3 — Workflow shape

`EnterpriseDiagnosticsWorkflow.RunAsync`:

1. Resolve all 7 diagnostic agents + `PrioritizeDiagnosticsAgent` + `SummarizeDiagnosticsAgent` via `context.GetAgent`.
2. Fan out 7 `RunAgentAndDeserializeAsync` calls in parallel; `await Task.WhenAll(...)`.
3. Unwrap each result with the existing null-throw pattern.
4. Build `IReadOnlyList<IDiagnosticResult> diagnostics = [hull, lifeSupport, warpCore, shields, weapons, navigation, transporter];`.
5. Sequentially call `PrioritizeDiagnosticsAgent` with a prompt built from `diagnostics`.
6. Sequentially call `SummarizeDiagnosticsAgent` with a prompt built from `diagnostics` + `prioritization.Priorities`.
7. Compute `hasImmediate`; conditionally call `NotifyBridgeActivity` (Section 4).
8. Return updated `DiagnosticsOutput`.

**Prompt builders** are private static helpers on the workflow class:

- `BuildPrioritizationPrompt(string stardate, IReadOnlyList<IDiagnosticResult> diagnostics)` — iterates `diagnostics` and emits one summary line per system (`"{SystemName}: severity={Severity}, notes={Notes}"`) followed by the JSON schema reminder. This is where the `IDiagnosticResult` interface earns its keep.
- `BuildSummaryPrompt(string stardate, IReadOnlyList<IDiagnosticResult> diagnostics, IReadOnlyList<PriorityEntry> priorities)` — same per-system loop, plus a priorities block (`"Priorities: [rank=1 system=X action=IMMEDIATE rationale=...] ..."`) and the existing summary JSON schema reminder.

The 7 diagnostic-agent prompts stay inline in the workflow body (matching existing style — each prompt is system-specific and short).

## Section 4 — Notification trigger & error handling

```csharp
var hasImmediate = prioritization.Priorities.Any(p => p.Action == "IMMEDIATE");

var bridgeNotified = false;
if (hasImmediate)
{
    LogCritical(logger, context.InstanceId);
    bridgeNotified = await context.CallActivityAsync<bool>(
        nameof(NotifyBridgeActivity),
        new NotifyBridgeInput(input.Stardate, summary));
}
```

The existing `LogCritical` log message stays — an IMMEDIATE action is the new "critical" signal. `NotifyBridgeActivity` and `NotifyBridgeInput` are unchanged.

**Validation of prioritization output:**
- If `prioritization.Priorities` is null or `Count != 7` → throw `InvalidOperationException` (consistent with the existing null-throw pattern on every agent result).
- Unknown `Action` values do not trigger bridge notification — only `Action == "IMMEDIATE"` does. The raw action string is preserved as-is in the returned `DiagnosticsOutput`; we do not normalize, drop, or replace entries.
- Rank duplicates or gaps are not validated — they don't affect the IMMEDIATE trigger.
- The `System` field returned by the agent is not validated against `SystemName` constants. The prioritize prompt is built from `IDiagnosticResult.SystemName` values, so the agent has the canonical names in context.

**No retry logic added.** Workflow-level retries remain Dapr's responsibility, matching existing behavior.

## Files changing

| File | Change |
|------|--------|
| `Models/DiagnosticsModels.cs` | Add `IDiagnosticResult`; 4 new result records; `PriorityEntry`, `PrioritizationResult`. Existing 3 records implement the interface. Update `DiagnosticsOutput`. |
| `Models/DiagnosticsAgentJsonContext.cs` | Add 6 `[JsonSerializable]` attributes. |
| `Program.cs` | Register 4 new diagnostic agents + prioritize agent. Add 5 instruction strings (Shields, Weapons, Navigation, Transporter, Prioritize). Update Summarize instruction. |
| `Workflows/EnterpriseDiagnosticsWorkflow.cs` | Expand fan-out from 3 to 7. Add prioritize call. Update summarize call to receive the priorities. Swap notification trigger from severity-based to action-based. Add two private prompt-builder helpers. Update `DiagnosticsOutput` construction. |

No changes to `Tools/MetricTools.cs`, `Activities/NotifyBridgeActivity.cs`, the AppHost project, or the ServiceDefaults project.

## Out of scope

- No new tools (all 4 new agents reuse `GetRandomPercentage`).
- No retry/circuit-breaker behavior beyond what Dapr already provides.
- No changes to the public HTTP endpoints (`/start`, `/status`, etc.).
- No tests added (the project currently has no test project; introducing one is a separate effort).
