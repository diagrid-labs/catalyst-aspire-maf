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
