using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter05.ToolUse;
using Chapter05.ToolUse.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.ToolUse.Tests;

public sealed class ToolUseTests
{
    [Theory]
    [InlineData("(5 + 7) * 3", "36")]
    [InlineData("10!", "3628800")]
    [InlineData("-3 + 2.5 * 2", "2")]
    [InlineData("3 * (4 + 2)", "18")]
    public void Calculator_evaluates_supported_arithmetic(string expression, string expected)
    {
        var result = new ToolUseFunctions().CalculateExpression(expression);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("System.IO.File.Delete(\"important.txt\")")]
    [InlineData("28!")]
    [InlineData("(1 + 2")]
    public void Calculator_rejects_unsupported_or_invalid_expressions(string expression)
    {
        Assert.Throws<FormatException>(() => new ToolUseFunctions().CalculateExpression(expression));
    }

    [Fact]
    public void Calculator_surfaces_division_by_zero()
    {
        Assert.Throws<DivideByZeroException>(() => new ToolUseFunctions().CalculateExpression("1 / 0"));
    }

    [Fact]
    public void Search_function_preserves_sample_results_and_fallback()
    {
        var functions = new ToolUseFunctions();

        Assert.Equal("The capital of France is Paris.", functions.SearchInformation("capital of France"));
        Assert.Contains(
            "Simulated search result for 'Tell me something about dogs.'",
            functions.SearchInformation("Tell me something about dogs."));
    }

    [Fact]
    public void Stock_tool_returns_known_sample_and_rejects_unknown_tickers()
    {
        var functions = new ToolUseFunctions();

        Assert.Equal(178.15m, functions.GetStockPrice("aapl"));
        Assert.Throws<KeyNotFoundException>(() => functions.GetStockPrice("UNKNOWN"));
    }

    [Theory]
    [InlineData("search: capital of France", ToolUseScenario.Search, "capital of France")]
    [InlineData("CALCULATE: 10!", ToolUseScenario.Calculator, "10!")]
    [InlineData("stock: MSFT", ToolUseScenario.StockPrice, "MSFT")]
    public void Host_prefixes_select_a_narrow_tool_set(
        string request, ToolUseScenario expectedScenario, string expectedInput)
    {
        var (scenario, input) = ToolUseAgent.ParseRequest(request);

        Assert.Equal(expectedScenario, scenario);
        Assert.Equal(expectedInput, input);
    }

    [Fact]
    public async Task Hosted_agent_forwards_caller_request_and_returns_actual_response()
    {
        using var client = new ScriptedChatClient();
        var agent = new ToolUseHostedAgent(client);

        var response = await agent.RunAsync("calculate: caller supplied value");

        Assert.Contains("caller supplied value", response.Text);
        Assert.Contains("caller supplied value", Assert.Single(client.Requests));
    }

    [Fact]
    public async Task Responses_protocol_returns_the_completed_caller_request()
    {
        using var model = new ScriptedChatClient();
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(new ToolUseHostedAgent(model));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter05-tool-use",
                input = "search: A caller-specific question",
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
            Assert.Contains("A caller-specific question", responseText);
            Assert.Contains("A caller-specific question", Assert.Single(model.Requests));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("search: ")]
    [InlineData("calculate: ")]
    [InlineData("stock: ")]
    public async Task Hosted_agent_rejects_empty_requests(string request)
    {
        using var client = new ScriptedChatClient();

        await Assert.ThrowsAsync<ArgumentException>(() => new ToolUseHostedAgent(client).RunAsync(request));

        Assert.Empty(client.Requests);
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public IReadOnlyList<string> Requests => _requests.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(input);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Handled: {input}")));
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
