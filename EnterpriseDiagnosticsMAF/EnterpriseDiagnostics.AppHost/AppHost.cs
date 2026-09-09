using Diagrid.Aspire.Hosting.Catalyst;

var builder = DistributedApplication.CreateBuilder(args);

var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

// This configures a new project in Catalyst with a managed state store for workflow state.
// The `agent-registry` state store the ApiService writes agent metadata to is NOT created here:
// `diagrid agent create` provisions it as a managed connection, so creating it from the AppHost
// fails with "Resource already exists". See the README for the required pre-provisioning step.
var catalystProject = builder.AddCatalystProject("aspire-maf", new()
{
    EnableManagedWorkflow = true
});

// The apiService will not use a Dapr sidecar anymore but will use the Catalyst.
builder.AddProject<Projects.EnterpriseDiagnostics_ApiService>("maf-app")
    .WithCatalyst(catalystProject)
    .WithEnvironment("OPENAI_API_KEY", openAiApiKey);

builder.Build().Run();
