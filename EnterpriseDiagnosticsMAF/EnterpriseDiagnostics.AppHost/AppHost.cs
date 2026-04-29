using Diagrid.Aspire.Hosting.Catalyst;

var builder = DistributedApplication.CreateBuilder(args);

var openAiApiKey = builder.Configuration["OPENAI_API_KEY"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");

// This configures a new project in Catalyst with a managed state store for workflow state.
var catalystProject = builder.AddCatalystProject("aspire-wf", new()
{
    EnableManagedWorkflow = true,
});

// The apiService will not use a Dapr sidecar anymore but will use the Catalyst.
builder.AddProject<Projects.EnterpriseDiagnostics_ApiService>("wf-app")
    .WithCatalyst(catalystProject)
    .WithEnvironment("OPENAI_API_KEY", openAiApiKey);

builder.Build().Run();
