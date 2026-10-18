using System.Reflection;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Diagrid.Aspire.Hosting.Catalyst;

var builder = DistributedApplication.CreateBuilder(args);

var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

// USE_CATALYST=true runs against Diagrid Catalyst, anything else runs against the locally installed Dapr.
var useCatalyst = bool.TryParse(Environment.GetEnvironmentVariable("USE_CATALYST"), out var catalyst) && catalyst;

var apiService = builder.AddProject<Projects.EnterpriseDiagnostics_ApiService>("maf-app")
    .WithEnvironment("OPENAI_API_KEY", openAiApiKey);

if (useCatalyst)
{
    // This configures a new project in Catalyst with a managed state store for workflow state.
    // The `agent-registry` state store the ApiService writes agent metadata to is NOT created here:
    // `diagrid agent create` provisions it as a managed connection, so creating it from the AppHost
    // fails with "Resource already exists". See the README for the required pre-provisioning step.
    var catalystProject = builder.AddCatalystProject("aspire-maf", new()
    {
        EnableManagedWorkflow = true
    });

    // The apiService uses Catalyst instead of a local Dapr sidecar.
    apiService.WithCatalyst(catalystProject);
}
else
{
    builder.AddDapr();

    string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? throw new("Where am I?");

    // Valkey is the state store for workflow state (see Resources/state.yaml).
    var cachePassword = builder.AddParameter("cache-password", "state-store-123", secret: true);
    var cache = builder
        .AddValkey("cache", 16379, cachePassword)
        .WithContainerName("workflow-state")
        .WithDataVolume("workflow-state-data");

    apiService
        .WithDaprSidecar(new DaprSidecarOptions
        {
            LogLevel = "debug",
            ResourcesPaths = [Path.Join(executingPath, "Resources")],
        })
        .WaitFor(cache);

    // Diagrid Dapr Dev Dashboard (optional, must be installed locally): http://localhost:9090
    builder.AddExecutable("dapr-dev-dashboard", "diagrid-dev-dashboard", ".",
            "--port", "9090", "--no-open")
        .WithHttpEndpoint(port: 9090, isProxied: false);
}

builder.Build().Run();
