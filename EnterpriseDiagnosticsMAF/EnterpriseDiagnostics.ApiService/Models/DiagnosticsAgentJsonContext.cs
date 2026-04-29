using System.Text.Json.Serialization;

namespace EnterpriseDiagnostics.ApiService.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HullIntegrityResult))]
[JsonSerializable(typeof(LifeSupportResult))]
[JsonSerializable(typeof(WarpCoreResult))]
[JsonSerializable(typeof(DiagnosticsSummaryResult))]
[JsonSerializable(typeof(DiagnosticsSeverity))]
public partial class DiagnosticsAgentJsonContext : JsonSerializerContext;
