using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Chapter06.Planning;

public static class PlanningDemos
{
    public const string SampleTopic = "The importance of Reinforcement Learning in AI";

    public static (AIAgent Planner, AIAgent Writer) CreateAgents(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        var planner = new ChatClientAgent(
            chatClient,
            """
            You are an expert technical writer and content strategist.
            Create an ordered, concise plan for a summary of the requested topic.
            Return 3 to 7 distinct, actionable points in the structured plan format.
            """,
            name: "ArticlePlanner")
            .AsBuilder()
            .UseOpenTelemetry(
                PlanningWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        var writer = new ChatClientAgent(
            chatClient,
            """
            You are an expert technical writer.
            Write a concise, engaging, well-structured summary of about 200 words.
            Follow the supplied validated plan and do not add unrelated sections.
            """,
            name: "ArticleWriter")
            .AsBuilder()
            .UseOpenTelemetry(
                PlanningWorkflow.TelemetrySourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
        return (planner, writer);
    }

    public static async Task<PlanningResult> RunPlanAndWriteAsync(
        IChatClient chatClient,
        string topic = SampleTopic,
        Action<string>? writeOutput = null,
        CancellationToken cancellationToken = default)
    {
        var (planner, writer) = CreateAgents(chatClient);
        return await PlanningWorkflow.RunAsync(planner, writer, topic, writeOutput, cancellationToken);
    }

    public static string FormatResult(PlanningResult result) =>
        $"""
        --- Final plan and summary ---
        Plan:
        {string.Join(Environment.NewLine, result.Plan.Steps.Select((step, index) => $"{index + 1}. {step}"))}

        Summary:
        {result.Summary}
        """;
}
