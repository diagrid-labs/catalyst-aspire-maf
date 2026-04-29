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

public record HullIntegrityResult(double IntegrityPercent, DiagnosticsSeverity Severity, string Notes);

public record LifeSupportResult(double OxygenPercent, double Co2Percent, DiagnosticsSeverity Severity, string Notes);

public record WarpCoreResult(double DilithiumStability, double PlasmaFlowRate, DiagnosticsSeverity Severity, string Notes);

public record DiagnosticsSummaryResult(string Summary);

public record NotifyBridgeInput(string Stardate, string Summary);

[JsonConverter(typeof(JsonStringEnumConverter<DiagnosticsSeverity>))]
public enum DiagnosticsSeverity
{
    LOW,
    MEDIUM,
    HIGH,
    CRITICAL
}
