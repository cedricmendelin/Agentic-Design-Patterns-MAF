using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Azure.AI.AgentServer.Invocations;
using Microsoft.Agents.AI;
using Chapter13.HumanInTheLoop;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace Chapter13.HumanInTheLoop.Hosted;

public sealed class CustomerSupportInvocationHandler(IChatClient chatClient) : InvocationHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override async Task HandleAsync(
        HttpRequest request,
        HttpResponse response,
        InvocationContext context,
        CancellationToken cancellationToken)
    {
        CustomerSupportInvocationRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<CustomerSupportInvocationRequest>(
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status400BadRequest,
                "Request body must be a JSON object containing either a task or an approval decision.",
                cancellationToken);
            return;
        }

        if (body is null)
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status400BadRequest,
                "Request body is required.",
                cancellationToken);
            return;
        }

        var hasTask = body.Task is not null;
        var hasDecision = body.Decision is not null;
        if (hasTask == hasDecision)
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status400BadRequest,
                "Provide exactly one of 'task' or 'decision'.",
                cancellationToken);
            return;
        }

        if (hasTask && string.IsNullOrWhiteSpace(body.Task))
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status400BadRequest,
                "The task cannot be empty.",
                cancellationToken);
            return;
        }

        var sessionId = context.SessionId;
        var ownerId = context.PlatformContext.UserIdKey;
        var sessionLock = SupportSessionStore.GetLock(sessionId);
        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            var stored = SupportSessionStore.Load(sessionId);
            if (stored is not null &&
                !string.Equals(stored.OwnerId, ownerId, StringComparison.Ordinal))
            {
                await WriteErrorAsync(
                    response,
                    StatusCodes.Status404NotFound,
                    "No session found for this caller.",
                    cancellationToken);
                return;
            }

            if (hasTask)
            {
                await StartSupportRequestAsync(
                    body,
                    stored,
                    sessionId,
                    ownerId,
                    context.InvocationId,
                    response,
                    cancellationToken);
                return;
            }

            await ResolveApprovalAsync(
                body,
                stored,
                sessionId,
                ownerId,
                context.InvocationId,
                response,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status400BadRequest,
                exception.Message,
                cancellationToken);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    private async Task StartSupportRequestAsync(
        CustomerSupportInvocationRequest body,
        SupportInvocationSession? previousSession,
        string sessionId,
        string? ownerId,
        string invocationId,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (previousSession?.Status == "awaiting_approval")
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status409Conflict,
                "This session has a pending approval. Resolve it before submitting another task.",
                cancellationToken);
            return;
        }

        var task = body.Task!;
        if (task.Length > 4000)
        {
            throw new ArgumentException("The task cannot exceed 4000 characters.");
        }

        var customer = body.Customer ?? new SupportCustomer();
        customer.Validate();
        var agent = CustomerSupportAgent.Create(chatClient, customer);
        var agentSession = await agent.CreateSessionAsync(cancellationToken);
        var agentResponse = await agent.RunAsync(task, agentSession, cancellationToken: cancellationToken);
        await SaveResponseAsync(
            agent,
            agentSession,
            agentResponse,
            customer,
            task,
            sessionId,
            ownerId,
            invocationId,
            response,
            cancellationToken);
    }

    private async Task ResolveApprovalAsync(
        CustomerSupportInvocationRequest body,
        SupportInvocationSession? stored,
        string sessionId,
        string? ownerId,
        string invocationId,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (stored is null || stored.Status != "awaiting_approval" || stored.PendingApproval is null)
        {
            await WriteErrorAsync(
                response,
                StatusCodes.Status409Conflict,
                "This session has no pending approval.",
                cancellationToken);
            return;
        }

        var decision = body.Decision!.Trim().ToLowerInvariant();
        if (decision is not ("approve" or "reject"))
        {
            throw new ArgumentException("Decision must be 'approve' or 'reject'.");
        }

        if (!string.Equals(body.RequestId, stored.PendingApproval.RequestId, StringComparison.Ordinal))
        {
            throw new ArgumentException("RequestId must match the pending approval request.");
        }

        var agent = CustomerSupportAgent.Create(chatClient, stored.Customer);
        var agentSession = await agent.DeserializeSessionAsync(stored.AgentSession, cancellationToken: cancellationToken);
        var approvalRequest = RestoreApprovalRequest(stored.PendingApproval);
        var approvalMessage = new ChatMessage(
            ChatRole.User,
            [approvalRequest.CreateResponse(decision == "approve")]);
        var agentResponse = await agent.RunAsync(
            approvalMessage,
            agentSession,
            cancellationToken: cancellationToken);

        await SaveResponseAsync(
            agent,
            agentSession,
            agentResponse,
            stored.Customer,
            stored.Task,
            sessionId,
            ownerId,
            invocationId,
            response,
            cancellationToken);
    }

    private static async Task SaveResponseAsync(
        AIAgent agent,
        AgentSession agentSession,
        AgentResponse agentResponse,
        SupportCustomer customer,
        string task,
        string sessionId,
        string? ownerId,
        string invocationId,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var pendingApprovals = agentResponse.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
            .ToArray();
        if (pendingApprovals.Length > 1)
        {
            throw new InvalidOperationException(
                "The hosted support demo can pause for one approval at a time.");
        }

        var serializedSession = await agent.SerializeSessionAsync(
            agentSession,
            cancellationToken: cancellationToken);
        var session = new SupportInvocationSession(
            sessionId,
            ownerId,
            pendingApprovals.Length == 1 ? "awaiting_approval" : "completed",
            task,
            customer,
            serializedSession,
            pendingApprovals.Length == 1 ? SaveApprovalRequest(pendingApprovals[0]) : null,
            agentResponse.Text);
        SupportSessionStore.Save(session);

        if (session.PendingApproval is { } pending)
        {
            await response.WriteAsJsonAsync(
                new
                {
                    session_id = sessionId,
                    invocation_id = invocationId,
                    status = session.Status,
                    assistant_message = agentResponse.Text,
                    approval = new
                    {
                        request_id = pending.RequestId,
                        tool_name = pending.Name,
                        arguments = pending.Arguments
                    }
                },
                JsonOptions,
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(agentResponse.Text))
        {
            throw new InvalidOperationException("The support agent returned an empty response.");
        }

        await response.WriteAsJsonAsync(
            new
            {
                session_id = sessionId,
                invocation_id = invocationId,
                status = session.Status,
                final_output = agentResponse.Text
            },
            JsonOptions,
            cancellationToken);
    }

    private static PendingApproval SaveApprovalRequest(ToolApprovalRequestContent request)
    {
        if (request.ToolCall is not FunctionCallContent functionCall)
        {
            throw new InvalidOperationException("The support agent requested approval for an unsupported tool call.");
        }

        return new PendingApproval(
            request.RequestId,
            functionCall.CallId,
            functionCall.Name,
            (functionCall.Arguments ?? new Dictionary<string, object?>()).ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions),
                StringComparer.Ordinal));
    }

    private static ToolApprovalRequestContent RestoreApprovalRequest(PendingApproval approval)
    {
        var arguments = approval.Arguments.ToDictionary(
            pair => pair.Key,
            pair => (object?)ConvertJsonValue(pair.Value),
            StringComparer.Ordinal);
        return new ToolApprovalRequestContent(
            approval.RequestId,
            new FunctionCallContent(approval.CallId, approval.Name, arguments));
    }

    private static object? ConvertJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException("The pending tool arguments contain an unsupported JSON value.")
        };

    private static Task WriteErrorAsync(
        HttpResponse response,
        int statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(
            new { error = message },
            JsonOptions,
            cancellationToken);
    }

    public sealed record CustomerSupportInvocationRequest
    {
        public string? Task { get; init; }
        public SupportCustomer? Customer { get; init; }
        public string? Decision { get; init; }
        public string? RequestId { get; init; }
    }

    private sealed record PendingApproval(
        string RequestId,
        string CallId,
        string Name,
        Dictionary<string, JsonElement> Arguments);

    private sealed record SupportInvocationSession(
        string SessionId,
        string? OwnerId,
        string Status,
        string Task,
        SupportCustomer Customer,
        JsonElement AgentSession,
        PendingApproval? PendingApproval,
        string? FinalOutput);

    private static class SupportSessionStore
    {
        private static readonly string StateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".chapter13-human-in-the-loop");
        private static readonly Dictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);
        private static readonly object LocksSync = new();

        public static SemaphoreSlim GetLock(string sessionId)
        {
            lock (LocksSync)
            {
                if (!Locks.TryGetValue(sessionId, out var sessionLock))
                {
                    sessionLock = new SemaphoreSlim(1, 1);
                    Locks.Add(sessionId, sessionLock);
                }

                return sessionLock;
            }
        }

        public static SupportInvocationSession? Load(string sessionId)
        {
            var path = GetPath(sessionId);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SupportInvocationSession>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize support session '{sessionId}'.");
        }

        public static void Save(SupportInvocationSession session)
        {
            Directory.CreateDirectory(StateDirectory);
            var path = GetPath(session.SessionId);
            var temporaryPath = path + ".tmp";
            var json = JsonSerializer.Serialize(session, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }

        private static string GetPath(string sessionId)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))
                .ToLowerInvariant();
            return Path.Combine(StateDirectory, $"session-{hash}.json");
        }
    }
}
