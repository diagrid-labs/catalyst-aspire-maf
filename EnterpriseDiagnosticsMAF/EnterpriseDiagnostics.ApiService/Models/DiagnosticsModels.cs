using System.Text.Json.Serialization;

namespace EnterpriseDiagnostics.ApiService.Models;

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
    [property: JsonPropertyName("notes")] string Notes);

public record LifeSupportResult(
    [property: JsonPropertyName("oxygenPercent")] double OxygenPercent,
    [property: JsonPropertyName("co2Percent")] double Co2Percent,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes);

public record WarpCoreResult(
    [property: JsonPropertyName("dilithiumStability")] double DilithiumStability,
    [property: JsonPropertyName("plasmaFlowRate")] double PlasmaFlowRate,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("notes")] string Notes);

public record DiagnosticsSummaryResult(
    [property: JsonPropertyName("summary")] string Summary);

public record NotifyBridgeInput(string Stardate, string Summary);
