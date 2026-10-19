using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chapter14.RAG;
using Chapter14.RAG.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.RAG.Tests;

public sealed class RagTests
{
    [Fact]
    public void Retrieval_ranks_the_relevant_source_for_the_notebook_questions()
    {
        var knowledgeBase = new RagKnowledgeBase();

        Assert.Equal(
            "SOTU-2022-BREYER",
            knowledgeBase.Retrieve("What did the president say about Justice Breyer?").First().DocumentId);
        Assert.Equal(
            "SOTU-2022-ECONOMY",
            knowledgeBase.Retrieve("What did the address say about the economy?").First().DocumentId);
    }

    [Fact]
    public void Retrieval_returns_no_context_for_unmatched_queries()
    {
        var knowledgeBase = new RagKnowledgeBase();

        Assert.Empty(knowledgeBase.Retrieve("unrelated astrophysics vocabulary"));
        Assert.Contains("No relevant information", knowledgeBase.Search("unrelated astrophysics vocabulary"));
    }

    [Fact]
    public async Task Agent_calls_retrieval_with_the_caller_question_and_returns_its_answer()
    {
        using var client = new ScriptedChatClient("What did the address say about the economy?");
        var response = await RagAgent.RunAsync(client, "What did the address say about the economy?");

        Assert.Contains("[SOTU-2022-ECONOMY]", response);
        Assert.Contains("What did the address say about the economy?", client.Requests);
        Assert.Contains("SOTU-2022-ECONOMY", client.FunctionResults.Single());
    }

    [Fact]
    public async Task Hosted_responses_protocol_returns_a_grounded_answer_for_the_caller_input()
    {
        using var model = new ScriptedChatClient("What did the address say about the economy?");
        var builder = AgentHost.CreateBuilder([]);
        builder.WebApplicationBuilder.WebHost.UseTestServer();
        builder.Services.AddFoundryResponses(new RagHostedAgent(model));
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        await app.StartAsync();

        try
        {
            using var http = app.GetTestClient();
            using var response = await http.PostAsJsonAsync("/responses", new
            {
                model = "chapter14-rag",
                input = "What did the address say about the economy?",
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
            Assert.Contains("[SOTU-2022-ECONOMY]", responseText);
            Assert.Contains("What did the address say about the economy?", model.Requests);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Agent_rejects_an_empty_question_before_calling_the_model()
    {
        using var client = new ScriptedChatClient("unused");

        await Assert.ThrowsAsync<ArgumentException>(() => RagAgent.RunAsync(client, " "));

        Assert.Empty(client.Requests);
    }

    private sealed class ScriptedChatClient(string searchQuery) : IChatClient
    {
        private readonly ConcurrentQueue<string> _requests = new();
        private readonly ConcurrentQueue<string> _functionResults = new();

        public IReadOnlyList<string> Requests => _requests.ToArray();
        public IReadOnlyList<string> FunctionResults => _functionResults.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messageList = messages.ToArray();
            var userInput = messageList.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
            _requests.Enqueue(userInput);

            if (_requests.Count == 1)
            {
                var searchTool = Assert.Single(options?.Tools ?? []);
                Assert.Equal("search_knowledge_base", searchTool.Name);
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "call-1",
                                searchTool.Name,
                                new Dictionary<string, object?> { ["query"] = searchQuery })
                        ])));
            }

            var functionResult = messageList
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Single();
            var resultText = Convert.ToString(functionResult.Result, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            _functionResults.Enqueue(resultText);
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "The address focused on lowering family costs [SOTU-2022-ECONOMY].")));
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
