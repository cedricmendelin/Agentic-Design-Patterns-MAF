namespace AgenticDesignPatterns.Shared;

public sealed record FoundrySettings
{
    public required string ProjectEndpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string ModelDeployment { get; init; }
    public Dictionary<string, string> ModelDeployments { get; init; } = [];

    public string ResolveModelDeployment(string? deploymentName = null)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return ModelDeployment;
        }

        var configuredDeployment = ModelDeployments.FirstOrDefault(
            entry => string.Equals(entry.Key, deploymentName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(configuredDeployment.Value))
        {
            var availableNames = ModelDeployments.Keys.Order(StringComparer.OrdinalIgnoreCase);
            var available = string.Join(", ", availableNames);
            throw new InvalidOperationException(
                $"Foundry model deployment '{deploymentName}' is not configured. " +
                (available.Length == 0
                    ? "Add it under Foundry:ModelDeployments in user-secrets."
                    : $"Available deployment names: {available}."));
        }

        return configuredDeployment.Value;
    }

    public Uri OpenAIEndpoint
    {
        get
        {
            if (!Uri.TryCreate(ProjectEndpoint, UriKind.Absolute, out var projectEndpoint))
            {
                throw new InvalidOperationException(
                    "Foundry:ProjectEndpoint must be a valid absolute URL.");
            }

            const string projectHostSuffix = ".services.ai.azure.com";
            const string inferenceHostSuffix = ".openai.azure.com";
            var inferenceHost = projectEndpoint.Host.EndsWith(
                projectHostSuffix,
                StringComparison.OrdinalIgnoreCase)
                ? projectEndpoint.Host[..^projectHostSuffix.Length] + inferenceHostSuffix
                : projectEndpoint.Host;

            if (!inferenceHost.EndsWith(inferenceHostSuffix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Foundry:ProjectEndpoint must use a .services.ai.azure.com or .openai.azure.com host.");
            }

            return new UriBuilder(projectEndpoint)
            {
                Scheme = Uri.UriSchemeHttps,
                Host = inferenceHost,
                Port = -1,
                Path = "/openai/v1/",
                Query = string.Empty,
                Fragment = string.Empty
            }.Uri;
        }
    }
}
