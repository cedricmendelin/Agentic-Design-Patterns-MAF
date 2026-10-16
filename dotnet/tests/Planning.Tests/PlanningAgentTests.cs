using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter06.Planning;
using Chapter06.Planning.Hosted;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using Xunit;

namespace AgenticDesignPatterns.Planning.Tests;

public sealed class PlanningAgentTests
{
    private static readonly string ValidPlan = JsonSerializer.Serialize(new PlanningPlan
    {
        Steps = ["Define reinforcement learning", "Explain how agents learn", "Describe applications"]
    });

    [Fact]
    public async Task Workflow_validates_the_structured_plan_before_writing()
    {
        using var client = new ScriptedChatClient([ValidPlan, "A concise summary of reinforcement learning."]);
        var (planner, writer) = PlanningDemos.CreateAgents(client);
        var stages = new List<string>();

        var result = await PlanningWorkflow.RunAsync(planner, writer, "Caller topic.", stages.Add);

        Assert.Equal(2, client.Requests.Count);
        Assert.Contains("Caller topic.", client.Requests[0].Input);
        Assert.Contains("Caller topic.", client.Requests[1].Input);
        Assert.Contains("Define reinforcement learning", client.Requests[1].Input);
        Assert.Equal(3, result.Plan.Steps.Count);
        Assert.Equal("A concise summary of reinforcement learning.", result.Summary);
        Assert.Contains(stages, stage => stage.Contains("Validated plan", StringComparison.Ordinal));
        Assert.Contains(stages, stage => stage.Contains("--- Summary ---", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_plan_fails_before_the_writer_is_called()
    {
        using var client = new ScriptedChatClient(
        [
            JsonSerializer.Serialize(new PlanningPlan { Steps = ["Only one", "Only two"] }),
            "This must not be returned."
        ]);
        var (planner, writer) = PlanningDemos.CreateAgents(client);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            PlanningWorkflow.RunAsync(planner, writer, "A task with an invalid plan."));

        Assert.Contains("between 3 and 7 steps", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(client.Requests);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("too long")]
    public void Validator_rejects_malformed_plan_steps(string failure)
    {
        var steps = failure switch
        {
            "duplicate" => new List<string> { "Same", "Same", "Third" },
            "empty" => new List<string> { "First", " ", "Third" },
            _ => new List<string> { "First", "Second", new string('x', PlanningPlanValidator.MaximumStepLength + 1) }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PlanningPlanValidator.Validate("Topic", new PlanningPlan { Steps = steps }));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task Responses_protocol_uses_caller_input_and_returns_the_completed_plan_and_summary()
    {
        using var model = new ScriptedChatClient([ValidPlan, "Caller-specific final summary."]);
        using var chatClient = model.AsBuilder()
            .UseOpenTelemetry(
                sourceName: PlanningWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.ConfigureTracing(tracing => tracing.AddSource(PlanningWorkflow.TelemetrySourceName));
        builder.Services.AddFoundryResponses(new PlanningAgent(chatClient));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter06-planning",
                input = "Summarize caller-specific topic.",
                stream = false,
                store = false
            });
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
            var responseText = string.Concat(
                body.RootElement.GetProperty("output").EnumerateArray()
                    .SelectMany(item => item.GetProperty("content").EnumerateArray())
                    .Where(item => item.GetProperty("type").GetString() == "output_text")
                    .Select(item => item.GetProperty("text").GetString()));
            Assert.Contains("Caller-specific final summary.", responseText);
            Assert.Equal(2, model.Requests.Count);
            Assert.All(model.Requests, request =>
                Assert.Contains("caller-specific topic", request.Input, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed record ModelRequest(string Input);

    private sealed class ScriptedChatClient(IEnumerable<string> responses) : IChatClient
    {
        private readonly ConcurrentQueue<string> _responses = new(responses);
        private readonly ConcurrentQueue<ModelRequest> _requests = new();

        public IReadOnlyList<ModelRequest> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(new ModelRequest(input));
            if (!_responses.TryDequeue(out var text))
            {
                throw new InvalidOperationException("No scripted response remains.");
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                yield return new ChatResponseUpdate(message.Role, message.Text);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
