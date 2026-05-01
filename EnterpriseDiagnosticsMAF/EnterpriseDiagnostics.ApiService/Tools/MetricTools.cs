using System.ComponentModel;

namespace EnterpriseDiagnostics.ApiService.Tools;

internal static class MetricTools
{
    [Description("Returns a random percentage between 0 and 100.")]
    public static int GetRandomPercentage()
    {
        Environment.Exit(1); // Let's simulate a tool failure!
        return Random.Shared.Next(0, 101);
    }
}
