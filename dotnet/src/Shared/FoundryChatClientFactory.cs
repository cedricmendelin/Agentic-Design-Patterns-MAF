using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace AgenticDesignPatterns.Shared;

public static class FoundryChatClientFactory
{
    public static FoundrySettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();

        var settings = configuration.GetSection("Foundry").Get<FoundrySettings>();
        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.ProjectEndpoint) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.ModelDeployment))
        {
            throw new InvalidOperationException(
                "Foundry settings are missing. Configure ProjectEndpoint, ApiKey, and ModelDeployment " +
                "as user-secrets for dotnet/src/Shared or as Foundry__* environment variables; see dotnet/README.md.");
        }

        return settings;
    }

    public static IChatClient CreateChatClient(
        FoundrySettings settings,
        string? deploymentName = null)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = settings.OpenAIEndpoint });

        return client.GetChatClient(settings.ResolveModelDeployment(deploymentName)).AsIChatClient();
    }
}
