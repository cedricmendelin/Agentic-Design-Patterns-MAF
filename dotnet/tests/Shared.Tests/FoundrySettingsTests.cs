using AgenticDesignPatterns.Shared;

namespace AgenticDesignPatterns.Shared.Tests;

public sealed class FoundrySettingsTests
{
    [Fact]
    public void OpenAIEndpoint_converts_project_endpoint_host_to_openai_host()
    {
        var settings = CreateSettings(projectEndpoint: "https://demo.services.ai.azure.com/api/projects/sample");

        Assert.Equal("https://demo.openai.azure.com/openai/v1/", settings.OpenAIEndpoint.ToString());
    }

    [Fact]
    public void ResolveModelDeployment_returns_named_alias_when_configured()
    {
        var settings = CreateSettings(modelDeployments: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fast"] = "gpt-4.1-mini"
        });

        Assert.Equal("gpt-4.1-mini", settings.ResolveModelDeployment("fast"));
    }

    [Fact]
    public void ResolveModelDeployment_throws_helpful_error_when_alias_is_missing()
    {
        var settings = CreateSettings();

        var exception = Assert.Throws<InvalidOperationException>(() => settings.ResolveModelDeployment("fast"));

        Assert.Contains("Foundry__ModelDeployments__<name>", exception.Message);
    }

    private static FoundrySettings CreateSettings(
        string projectEndpoint = "https://demo.openai.azure.com/openai/deployments/demo",
        string apiKey = "test-key",
        string modelDeployment = "gpt-4.1",
        Dictionary<string, string>? modelDeployments = null) =>
        new()
        {
            ProjectEndpoint = projectEndpoint,
            ApiKey = apiKey,
            ModelDeployment = modelDeployment,
            ModelDeployments = modelDeployments ?? []
        };
}
