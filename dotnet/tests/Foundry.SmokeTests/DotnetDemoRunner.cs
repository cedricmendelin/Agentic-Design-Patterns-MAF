using System.Diagnostics;

namespace AgenticDesignPatterns.Foundry.SmokeTests;

internal static class DotnetDemoRunner
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    public static Task<DemoRunResult> RunChapter01SpecsAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Chapter01.PromptChaining", "Chapter01.PromptChaining.csproj"),
            ["specs"]);

    public static Task<DemoRunResult> RunChapter02TravelAsync() =>
        RunAsync(
            Path.Combine(RepositoryRoot, "dotnet", "src", "Chapter02.Routing", "Chapter02.Routing.csproj"),
            ["travel"]);

    private static async Task<DemoRunResult> RunAsync(string projectPath, string[] demoArguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryRoot
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");

        foreach (var argument in demoArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var deploymentAlias = Environment.GetEnvironmentVariable("FOUNDRY_TEST_DEPLOYMENT_ALIAS");
        if (!string.IsNullOrWhiteSpace(deploymentAlias))
        {
            startInfo.ArgumentList.Add("--deployment");
            startInfo.ArgumentList.Add(deploymentAlias);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet process for Foundry smoke test.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException($"Timed out while running demo project '{projectPath}'.");
        }

        return new DemoRunResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "dotnet", "AgenticDesignPatterns.slnx");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root for Foundry smoke tests.");
    }

    internal sealed record DemoRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => $"""
            STDOUT:
            {StandardOutput}

            STDERR:
            {StandardError}
            """;
    }
}
