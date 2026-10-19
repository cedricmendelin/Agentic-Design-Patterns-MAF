using AgenticDesignPatterns.Shared;
using Azure.AI.OpenAI;
using Azure.Identity;
using Chapter16.ResourceOptimization;
using Chapter16.ResourceOptimization.Hosted;
using Chapter16.ResourceOptimization.Models;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

var builder = AgentHost.CreateBuilder(args);
builder.WebApplicationBuilder.Configuration
    .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var settings = HostedFoundryConfiguration.LoadSettings(builder.Configuration);
var deploymentAlias = builder.Configuration["deployment"];
var defaultDeployment = settings.ResolveModelDeployment(deploymentAlias);
var routeDeployments = Enum.GetValues<OptimizationCategory>()
    .ToDictionary(category => category, category => ResolveRouteDeployment(settings, category, defaultDeployment));
var deployments = routeDeployments.Values
    .Append(defaultDeployment)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToDictionary(
        deployment => deployment,
        deployment => CreateModelClient(settings, deployment),
        StringComparer.OrdinalIgnoreCase);
var routeClients = routeDeployments.ToDictionary(
    entry => entry.Key,
    entry => new OptimizationModelClient(entry.Value, deployments[entry.Value]));
using var searchClient = GoogleCustomSearchClient.FromConfiguration(builder.Configuration);

builder.ConfigureTracing(tracing => tracing.AddSource(ResourceOptimizationWorkflow.TelemetrySourceName));
builder.Services.AddFoundryResponses(
    new ResourceOptimizationAgent(deployments[defaultDeployment], routeClients, searchClient));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();

static IChatClient CreateModelClient(FoundrySettings settings, string deployment)
{
    if (!string.IsNullOrWhiteSpace(settings.ApiKey))
    {
        return FoundryChatClientFactory.CreateChatClient(
            settings with { ModelDeployment = deployment, ModelDeployments = [] });
    }

    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
    });
    return new AzureOpenAIClient(
            new Uri(settings.OpenAIEndpoint.GetLeftPart(UriPartial.Authority)),
            credential)
        .GetChatClient(deployment)
        .AsIChatClient();
}

static string ResolveRouteDeployment(
    FoundrySettings settings,
    OptimizationCategory category,
    string fallbackDeployment)
{
    var alias = category switch
    {
        OptimizationCategory.Simple => "fast",
        OptimizationCategory.Reasoning => "reasoning",
        OptimizationCategory.InternetSearch => "quality",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
    return settings.ModelDeployments.TryGetValue(alias, out var deployment) &&
        !string.IsNullOrWhiteSpace(deployment)
            ? deployment
            : fallbackDeployment;
}
