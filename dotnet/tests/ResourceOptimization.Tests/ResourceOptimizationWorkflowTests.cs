using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Chapter16.ResourceOptimization;
using Chapter16.ResourceOptimization.Hosted;
using Chapter16.ResourceOptimization.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.ResourceOptimization.Tests;

public sealed class ResourceOptimizationWorkflowTests
{
    [Theory]
    [InlineData(
        "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen",
        OptimizationCategory.Simple)]
    [InlineData(
        "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty",
        OptimizationCategory.Reasoning)]
    public void Complexity_router_selects_route_at_twenty_word_threshold(
        string request,
        OptimizationCategory expected)
    {
        Assert.Equal(expected, ResourceOptimizationWorkflow.RouteByComplexity(request));
    }

    [Theory]
    [InlineData("{\"classification\":\"simple\"}", OptimizationCategory.Simple)]
    [InlineData("{\"classification\":\"reasoning\"}", OptimizationCategory.Reasoning)]
    [InlineData("{\"classification\":\"internet_search\"}", OptimizationCategory.InternetSearch)]
    public void Classifier_accepts_only_known_categories(string output, OptimizationCategory expected)
    {
        Assert.Equal(expected, ResourceOptimizationWorkflow.ParseClassification(output));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"classification\":\"other\"}")]
    public void Classifier_rejects_malformed_or_unknown_categories(string output)
    {
        Assert.Throws<InvalidOperationException>(
            () => ResourceOptimizationWorkflow.ParseClassification(output));
    }

    [Fact]
    public async Task Complexity_workflow_uses_selected_route_and_reports_provider_usage()
    {
        using var client = new ScriptedChatClient(
            ("A concise answer.", 5, 4));
        var modelClients = CreateModelClients(client);

        var result = await ResourceOptimizationWorkflow.RunAsync(
            "What is the capital of Australia?",
            RoutingStrategy.Complexity,
            client,
            modelClients);

        Assert.Equal(OptimizationCategory.Simple, result.Category);
        Assert.Equal("fast-deployment", result.ModelDeployment);
        Assert.Equal("A concise answer.", result.Answer);
        Assert.Equal(5, result.InputTokens);
        Assert.Equal(4, result.OutputTokens);
        Assert.Equal(9, result.TotalTokens);
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Search_route_requires_search_configuration()
    {
        using var client = new ScriptedChatClient(
            ("{\"classification\":\"internet_search\"}", 2, 1));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResourceOptimizationWorkflow.RunAsync(
                "What is the latest Australian Open schedule?",
                RoutingStrategy.IntentClassification,
                client,
                CreateModelClients(client)));

        Assert.Contains("Google Custom Search is not configured", error.ToString());
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task Google_search_client_maps_result_fields_and_limits_query_results()
    {
        var handler = new GoogleSearchHandler();
        using var httpClient = new HttpClient(handler);
        using var searchClient = new GoogleCustomSearchClient("test-key", "test-engine", httpClient);

        var results = await searchClient.SearchAsync("latest schedule");

        Assert.Equal("https://www.googleapis.com/customsearch/v1", handler.RequestUri?.GetLeftPart(UriPartial.Path));
        Assert.Contains("num=3", handler.RequestUri?.Query);
        Assert.Contains("latest%20schedule", handler.RequestUri?.Query);
        Assert.Equal(
            [new GoogleSearchResult("Result title", "Result snippet", "https://example.com/result")],
            results);
    }

    [Fact]
    public async Task Responses_protocol_returns_classification_and_final_answer()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ResourceOptimizationWorkflow.TelemetrySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var model = new ScriptedChatClient(
            ("{\"classification\":\"simple\"}", 2, 1),
            ("Canberra.", 4, 3));
        var agent = new ResourceOptimizationAgent(model, CreateModelClients(model), searchClient: null);
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing =>
            tracing.AddSource(ResourceOptimizationWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(agent);
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();
        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter16-resource-optimization",
                input = "What is the capital of Australia?",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var text = string.Concat(
                body.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            Assert.Contains("Classification: Simple", text);
            Assert.Contains("Model deployment: fast-deployment", text);
            Assert.Contains("Canberra.", text);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static IReadOnlyDictionary<OptimizationCategory, OptimizationModelClient> CreateModelClients(
        IChatClient client) =>
        new Dictionary<OptimizationCategory, OptimizationModelClient>
        {
            [OptimizationCategory.Simple] = new("fast-deployment", client),
            [OptimizationCategory.Reasoning] = new("reasoning-deployment", client),
            [OptimizationCategory.InternetSearch] = new("quality-deployment", client)
        };

    private sealed class ScriptedChatClient(params (string Text, long Input, long Output)[] responses)
        : IChatClient
    {
        private readonly Queue<(string Text, long Input, long Output)> _responses = new(responses);
        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToList());
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("A scripted model response is missing.");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response.Text))
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = response.Input,
                    OutputTokenCount = response.Output,
                    TotalTokenCount = response.Input + response.Output
                }
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The Chapter 16 workflow uses non-streaming model calls.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class GoogleSearchHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    items = new[]
                    {
                        new
                        {
                            title = "Result title",
                            snippet = "Result snippet",
                            link = "https://example.com/result"
                        }
                    }
                })
            };
            return Task.FromResult(response);
        }
    }
}
