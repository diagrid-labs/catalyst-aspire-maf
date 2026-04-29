using System.Text.Json.Serialization;
using EnterpriseDiagnostics.ApiService.Workflows;

namespace EnterpriseDiagnostics.ApiService.Models;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HullIntegrityResult))]
[JsonSerializable(typeof(LifeSupportResult))]
[JsonSerializable(typeof(WarpCoreResult))]
[JsonSerializable(typeof(EnterpriseDiagnosticsWorkflow.BridgeAck))]
public partial class DiagnosticsAgentJsonContext : JsonSerializerContext;
