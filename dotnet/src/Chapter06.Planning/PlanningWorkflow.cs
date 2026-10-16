using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Chapter06.Planning;

public static class PlanningWorkflow
{
    public const string TelemetrySourceName = "Chapter06.Planning";

    public static Workflow Create(
        AIAgent planner,
        AIAgent writer,
        Action<string>? writeOutput = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(writer);

        var planning = new PlannerExecutor(planner, writeOutput);
        var validation = new PlanValidationExecutor(writeOutput);
        var writing = new WriterExecutor(writer, writeOutput);

        return new WorkflowBuilder(planning)
            .AddEdge(planning, validation)
            .AddEdge(validation, writing)
            .WithOutputFrom(writing)
            .Build();
    }

    public static async Task<PlanningResult> RunAsync(
        AIAgent planner,
        AIAgent writer,
        string topic,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        cancellationToken.ThrowIfCancellationRequested();

        var workflow = Create(planner, writer, writeOutput);
        await using var run = await InProcessExecution.RunAsync(
            workflow,
            new PlanningRequest(topic),
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        PlanningResult? result = null;
        foreach (var workflowEvent in run.OutgoingEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    throw new InvalidOperationException(
                        $"Planning executor '{failed.ExecutorId}' failed: {failed.Data}",
                        failed.Data as Exception);
                case WorkflowErrorEvent error:
                    throw new InvalidOperationException("The planning workflow failed.", error.Exception);
                case WorkflowOutputEvent output:
                    if (result is not null)
                    {
                        throw new InvalidOperationException("The planning workflow produced multiple final results.");
                    }

                    result = output.As<PlanningResult>()
                        ?? throw new InvalidOperationException("The planning workflow produced an invalid result.");
                    break;
            }
        }

        return result ?? throw new InvalidOperationException("The planning workflow completed without a result.");
    }

    private sealed record PlanningRequest(string Topic);
    private sealed record ProposedPlan(string Topic, PlanningPlan Plan);

    private sealed class PlannerExecutor(AIAgent planner, Action<string>? writeOutput)
        : Executor<PlanningRequest, ProposedPlan>("Planner")
    {
        public override async ValueTask<ProposedPlan> HandleAsync(
            PlanningRequest request,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await planner.RunAsync<PlanningPlan>(
                request.Topic,
                cancellationToken: cancellationToken);
            var plan = response.Result
                ?? throw new InvalidOperationException("The planner returned no structured plan.");
            writeOutput?.Invoke($"\n--- Proposed plan ---\n{string.Join(Environment.NewLine, plan.Steps?.Select((step, index) => $"{index + 1}. {step}") ?? [])}");
            return new ProposedPlan(request.Topic, plan);
        }
    }

    private sealed class PlanValidationExecutor(Action<string>? writeOutput)
        : Executor<ProposedPlan, ValidatedPlan>("PlanValidation")
    {
        public override ValueTask<ValidatedPlan> HandleAsync(
            ProposedPlan proposal,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validatedPlan = PlanningPlanValidator.Validate(proposal.Topic, proposal.Plan);
            writeOutput?.Invoke(
                $"\n--- Validated plan ({validatedPlan.Steps.Count} steps) ---\n" +
                string.Join(Environment.NewLine, validatedPlan.Steps.Select((step, index) => $"{index + 1}. {step}")));
            return ValueTask.FromResult(validatedPlan);
        }
    }

    private sealed class WriterExecutor(AIAgent writer, Action<string>? writeOutput)
        : Executor<ValidatedPlan, PlanningResult>("Writer")
    {
        public override async ValueTask<PlanningResult> HandleAsync(
            ValidatedPlan plan,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            var planText = string.Join(Environment.NewLine, plan.Steps.Select((step, index) => $"{index + 1}. {step}"));
            var response = await writer.RunAsync(
                $"""
                Topic: {plan.Topic}

                Validated plan:
                {planText}

                Write an informative, engaging summary of about 200 words that follows the validated plan.
                Return only the summary.
                """,
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new InvalidOperationException("The writer returned an empty summary.");
            }

            writeOutput?.Invoke($"\n--- Summary ---\n{response.Text}");
            return new PlanningResult(plan, response.Text);
        }
    }
}
