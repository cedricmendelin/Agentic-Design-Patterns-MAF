using Xunit;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

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
}
