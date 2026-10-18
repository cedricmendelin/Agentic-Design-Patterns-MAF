using Xunit;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

[Trait("Category", "LiveFoundry")]
public sealed class FoundrySmokeTests
{
    [Fact(Timeout = 240000)]
    public async Task Chapter01_specs_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter01SpecsAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Final output ---", result.StandardOutput);
        Assert.True(result.StandardOutput.Contains("\"cpu\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
        Assert.True(result.StandardOutput.Contains("\"memory\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
        Assert.True(result.StandardOutput.Contains("\"storage\"", StringComparison.OrdinalIgnoreCase), result.CombinedOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter02_travel_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter02TravelAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Coordinator router decision ---", result.StandardOutput);
        Assert.Contains("--- Booker specialist output ---", result.StandardOutput);
        Assert.Contains("--- Info specialist output ---", result.StandardOutput);
        Assert.Contains("--- Clarification fallback ---", result.StandardOutput);
        Assert.Contains("--- Final routed response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter03_topic_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter03TopicAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Topic summary (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Related questions (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Key terms (parallel branch) ---", result.StandardOutput);
        Assert.Contains("--- Final synthesized response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter04_fact_check_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter04FactCheckAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Generated draft (round 1 of 3) ---", result.StandardOutput);
        Assert.Contains("--- Independent verification ---", result.StandardOutput);
        Assert.Contains("--- Final result ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter05_search_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter05SearchAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.True(
            result.StandardOutput.Split("Tool called: search_information", StringSplitOptions.None).Length >= 4,
            result.CombinedOutput);
        Assert.Contains("--- Final agent response ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter06_plan_and_write_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter06PlanWriteAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Plan ---", result.StandardOutput);
        Assert.Contains("--- Summary ---", result.StandardOutput);
        Assert.Contains("### Plan", result.StandardOutput);
        Assert.Contains("### Summary", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter07_crew_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter07CrewAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Crew research task ---", result.StandardOutput);
        Assert.Contains("--- Crew writing task ---", result.StandardOutput);
        Assert.Contains("--- Final output ---", result.StandardOutput);
    }

    [Fact(Timeout = 240000)]
    public async Task Chapter09_learning_adaptation_demo_runs_with_live_foundry_configuration()
    {
        var result = await DotnetDemoRunner.RunChapter09LearningAdaptationAsync();

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("--- Baseline evaluation ---", result.StandardOutput);
        Assert.True(
            result.StandardOutput.Contains("Candidate score:", StringComparison.Ordinal) ||
            result.StandardOutput.Contains(
                "The starting prompt already passes the benchmark; no update is recommended.",
                StringComparison.Ordinal),
            result.CombinedOutput);
        Assert.Contains("--- Adaptation result ---", result.StandardOutput);
        Assert.Contains("Best-scoring prompt:", result.StandardOutput);
    }
}
