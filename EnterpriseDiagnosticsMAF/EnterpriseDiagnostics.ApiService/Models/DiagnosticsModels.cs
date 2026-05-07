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
    ShieldsResult Shields,
    WeaponsResult Weapons,
    NavigationResult Navigation,
    TransporterResult Transporter,
    IReadOnlyList<PriorityEntry> Priorities,
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

public record PriorityEntry(
    [property: JsonPropertyName("system")] string System,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("rationale")] string Rationale);

public record PrioritizationResult(
    [property: JsonPropertyName("priorities")] IReadOnlyList<PriorityEntry> Priorities);

public record DiagnosticsSummaryResult(
    [property: JsonPropertyName("summary")] string Summary);

public record NotifyBridgeInput(string Stardate, string Summary);
