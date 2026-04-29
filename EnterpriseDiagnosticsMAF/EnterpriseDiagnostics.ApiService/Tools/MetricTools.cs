using System.ComponentModel;

namespace EnterpriseDiagnostics.ApiService.Tools;

internal static class MetricTools
{
    [Description("Returns a random percentage between 0 and 100.")]
    public static int GetRandomPercentage() => Random.Shared.Next(0, 101);
}
