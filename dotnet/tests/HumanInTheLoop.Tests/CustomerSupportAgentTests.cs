using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Chapter13.HumanInTheLoop;
using Microsoft.Extensions.AI;
using Xunit;

namespace AgenticDesignPatterns.HumanInTheLoop.Tests;

public sealed class CustomerSupportAgentTests
{
    [Fact]
    public async Task Escalation_waits_for_explicit_approval_before_running_tool()
    {
        using var chatClient = new EscalationChatClient();
        var customer = new SupportCustomer
        {
            Name = "Sam",
            Tier = "premium",
            SupportHistory = ["Previous pairing issue."]
        };
        var agent = CustomerSupportAgent.Create(chatClient, customer);
        var session = await agent.CreateSessionAsync();

        var pending = await agent.RunAsync("Escalate this complex issue.", session);
        var approval = Assert.Single(pending.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>());

        Assert.Equal("escalate_to_human", Assert.IsType<FunctionCallContent>(approval.ToolCall).Name);
        Assert.Empty(chatClient.FunctionResults);

        var final = await agent.RunAsync(
            new ChatMessage(ChatRole.User, [approval.CreateResponse(approved: true)]),
            session);

        Assert.Contains("Escalated hardware issue to a human specialist.", chatClient.FunctionResults);
        Assert.Contains("Escalation was completed.", final.Text);
    }

    [Fact]
    public void Customer_profile_rejects_oversized_and_unbounded_lists()
    {
        var tooLong = new SupportCustomer { Name = new string('x', 121) };
        var tooManyHistoryItems = new SupportCustomer
        {
            SupportHistory = Enumerable.Repeat("issue", 11).ToList()
        };

        Assert.Throws<ArgumentException>(tooLong.Validate);
        Assert.Throws<ArgumentException>(tooManyHistoryItems.Validate);
    }

    private sealed class EscalationChatClient : IChatClient
    {
        private readonly ConcurrentQueue<string> _functionResults = new();
        private int _requests;

        public IReadOnlyList<string> FunctionResults => _functionResults.ToArray();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _requests) == 1)
            {
                Assert.Contains(
                    "escalate_to_human",
                    options?.Tools?.Select(tool => tool.Name) ?? []);
                return Task.FromResult(new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new FunctionCallContent(
                                "escalation-1",
                                "escalate_to_human",
                                new Dictionary<string, object?> { ["issueType"] = "hardware issue" })
                        ])));
            }

            var result = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Single();
            _functionResults.Enqueue(Convert.ToString(result.Result) ?? string.Empty);
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Escalation was completed.")));
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
