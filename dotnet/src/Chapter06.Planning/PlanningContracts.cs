namespace Chapter06.Planning;

public sealed record PlanningPlan
{
    public List<string>? Steps { get; init; }
}

public sealed record ValidatedPlan(string Topic, IReadOnlyList<string> Steps);

public sealed record PlanningResult(ValidatedPlan Plan, string Summary);

public static class PlanningPlanValidator
{
    public const int MinimumSteps = 3;
    public const int MaximumSteps = 7;
    public const int MaximumStepLength = 200;

    public static ValidatedPlan Validate(string topic, PlanningPlan? plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (plan?.Steps is not { Count: >= MinimumSteps and <= MaximumSteps } steps)
        {
            throw new InvalidOperationException(
                $"The planner must return between {MinimumSteps} and {MaximumSteps} steps.");
        }

        if (steps.Any(step => string.IsNullOrWhiteSpace(step) || step.Length > MaximumStepLength))
        {
            throw new InvalidOperationException(
                $"Every plan step must contain 1 to {MaximumStepLength} characters.");
        }

        var normalizedSteps = steps.Select(step => step.Trim()).ToArray();
        if (normalizedSteps.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedSteps.Length)
        {
            throw new InvalidOperationException("The plan must not contain duplicate steps.");
        }

        return new ValidatedPlan(topic, normalizedSteps);
    }
}
