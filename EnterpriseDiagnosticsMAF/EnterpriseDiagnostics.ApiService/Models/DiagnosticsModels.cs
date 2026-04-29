namespace EnterpriseDiagnostics.ApiService.Models;

public record DiagnosticsInput(string Id, string Stardate);

public record DiagnosticsOutput(
    string Stardate,
    HullIntegrityResult HullIntegrity,
    LifeSupportResult LifeSupport,
    WarpCoreResult WarpCore,
    string Summary,
    bool BridgeNotified);

public record HullIntegrityResult(double IntegrityPercent, string Severity, string Notes);

public record LifeSupportResult(double OxygenPercent, double Co2Percent, string Severity, string Notes);

public record WarpCoreResult(double DilithiumStability, double PlasmaFlowRate, string Severity, string Notes);
