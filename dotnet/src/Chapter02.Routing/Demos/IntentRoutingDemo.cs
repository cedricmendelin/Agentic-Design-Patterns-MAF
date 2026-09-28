using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Chapter02.Routing.Models;

namespace Chapter02.Routing.Demos;

internal static class IntentRoutingDemo
{
    private static readonly (string Label, string Request)[] SampleRequests =
    [
        ("Booking", "Book me a hotel in Paris."),
        ("Information", "What is the highest mountain in the world?"),
        ("Unclear", "Can you help me with this?"),
        ("Booking", "Find flights to Tokyo next month.")
    ];

    public static async Task RunAsync(IChatClient chatClient)
    {
        var routerAgent = new ChatClientAgent(
            chatClient,
            """
            You are the main coordinator. Your only task is to analyze incoming user requests and decide which specialist should handle them.
            - For any requests related to booking flights or hotels, respond with BOOKER.
            - For all other general information questions, respond with INFO.
            - If the request is unclear or doesn't fit either category, respond with UNCLEAR.
            After the route token, add a short rationale on the next line using the format REASON: <why>.
            Do not answer the user's request directly.
            """,
            name: "CoordinatorRouter");

        var bookerAgent = new ChatClientAgent(
            chatClient,
            """
            You are the Booker specialist from a routing demo.
            The demo simulates bookings rather than completing real reservations.
            For each request, briefly confirm the travel booking task, mention the destination or itinerary you extracted, and state that the booking action was simulated.
            Keep the response to 2 sentences maximum.
            """,
            name: "Booker");

        var infoAgent = new ChatClientAgent(
            chatClient,
            """
            You are the Info specialist from a routing demo.
            Answer general information requests clearly and concisely in 1-2 sentences.
            If the user asks for factual information, provide the answer directly.
            """,
            name: "Info");

        var routerExecutor = new RouterExecutor(routerAgent);
        var bookingExecutor = new SpecialistExecutor(
            "BookerExecutor",
            RoutingDecision.Booker,
            bookerAgent,
            header: "Booker specialist output");
        var infoExecutor = new SpecialistExecutor(
            "InfoExecutor",
            RoutingDecision.Info,
            infoAgent,
            header: "Info specialist output");
        var clarificationExecutor = new ClarificationExecutor();

        var workflow = new WorkflowBuilder(routerExecutor)
            .AddEdge(routerExecutor, bookingExecutor, route => route is RoutedRequest { Decision: RoutingDecision.Booker })
            .AddEdge(routerExecutor, infoExecutor, route => route is RoutedRequest { Decision: RoutingDecision.Info })
            .AddEdge(routerExecutor, clarificationExecutor, route => route is RoutedRequest { Decision: RoutingDecision.Unclear })
            .WithOutputFrom(bookingExecutor, infoExecutor, clarificationExecutor)
            .Build();

        foreach (var (label, request) in SampleRequests)
        {
            Console.WriteLine($"\n>>> Sample route: {label}");
            Console.WriteLine($">>> User request: {request}");

            await using var run = await InProcessExecution.RunAsync(workflow, new ChatMessage(ChatRole.User, request));

            string? finalOutput = null;
            foreach (var workflowEvent in run.OutgoingEvents)
            {
                if (workflowEvent is WorkflowOutputEvent outputEvent)
                {
                    finalOutput = outputEvent.As<string>();
                }
                else if (workflowEvent is ExecutorFailedEvent failedEvent)
                {
                    var details = failedEvent.Data is Exception exception
                        ? DescribeException(exception)
                        : "No exception details were provided.";
                    throw new InvalidOperationException(
                        $"Workflow executor '{failedEvent.ExecutorId}' failed: {details}");
                }
                else if (workflowEvent is WorkflowErrorEvent errorEvent)
                {
                    throw new InvalidOperationException(
                        errorEvent.Exception is null
                            ? "The workflow reported an error without exception details."
                            : $"The workflow failed: {DescribeException(errorEvent.Exception)}");
                }
            }

            if (string.IsNullOrWhiteSpace(finalOutput))
            {
                throw new InvalidOperationException("The routing workflow completed without a final output.");
            }

            Console.WriteLine("\n--- Final routed response ---");
            Console.WriteLine(finalOutput);
        }
    }

    private static string DescribeException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" --> ", messages);
    }

    private sealed class RouterExecutor(ChatClientAgent routerAgent) : Executor<ChatMessage, RoutedRequest>("RouterExecutor")
    {
        public override async ValueTask<RoutedRequest> HandleAsync(
            ChatMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await routerAgent.RunAsync(message, cancellationToken: cancellationToken);
            var (decision, rationale) = ParseDecision(response.Text);

            Console.WriteLine("\n--- Coordinator router decision ---");
            Console.WriteLine($"Raw output: {response.Text}");
            Console.WriteLine($"Route: {decision}");
            Console.WriteLine($"Reason: {rationale}");

            return new RoutedRequest(message.Text ?? string.Empty, decision, response.Text, rationale);
        }

        private static (RoutingDecision Decision, string Rationale) ParseDecision(string rawOutput)
        {
            var lines = rawOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var routeToken = lines.FirstOrDefault() ?? string.Empty;
            var rationale = lines
                .Skip(1)
                .FirstOrDefault(line => line.StartsWith("REASON:", StringComparison.OrdinalIgnoreCase));

            var normalizedRoute = routeToken.Trim().TrimEnd('.', ':').ToUpperInvariant();
            var decision = normalizedRoute switch
            {
                "BOOKER" => RoutingDecision.Booker,
                "INFO" => RoutingDecision.Info,
                _ => RoutingDecision.Unclear
            };

            var parsedRationale = rationale is null
                ? "No rationale provided."
                : rationale["REASON:".Length..].Trim();

            return (decision, parsedRationale);
        }
    }

    private sealed class SpecialistExecutor(
        string executorId,
        RoutingDecision expectedDecision,
        ChatClientAgent agent,
        string header) : Executor<RoutedRequest, string>(executorId)
    {
        public override async ValueTask<string> HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            if (message.Decision != expectedDecision)
            {
                throw new InvalidOperationException(
                    $"{executorId} cannot handle decision '{message.Decision}'.");
            }

            var response = await agent.RunAsync(message.OriginalRequest, cancellationToken: cancellationToken);

            Console.WriteLine($"\n--- {header} ---");
            Console.WriteLine(response.Text);

            return response.Text;
        }
    }

    [YieldsOutput(typeof(string))]
    private sealed class ClarificationExecutor() : Executor<RoutedRequest>("ClarificationExecutor")
    {
        public override async ValueTask HandleAsync(
            RoutedRequest message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            const string clarification = "Coordinator could not delegate the request confidently. Please clarify whether you need travel booking help or general information.";

            Console.WriteLine("\n--- Clarification fallback ---");
            Console.WriteLine(clarification);

            await context.YieldOutputAsync(clarification, cancellationToken);
        }
    }
}
