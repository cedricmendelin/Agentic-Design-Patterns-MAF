using AgenticDesignPatterns.Shared;
using Chapter06.Planning;
using Microsoft.Extensions.AI;

string? demo = null;
string? deploymentAlias = null;
var invalidArguments = false;

for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--deployment", StringComparison.OrdinalIgnoreCase))
    {
        if (deploymentAlias is not null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            invalidArguments = true;
            break;
        }

        deploymentAlias = args[++index];
    }
    else if (demo is null)
    {
        demo = args[index];
    }
    else
    {
        invalidArguments = true;
        break;
    }
}

var selectedDemo = demo ?? "plan-write";
if (invalidArguments || selectedDemo is not ("plan-write" or "deep-research" or "all"))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/Chapter06.Planning [plan-write|deep-research|all] [--deployment <name>]");
    return 1;
}

try
{
    var settings = FoundryChatClientFactory.LoadSettings();
    var deployment = settings.ResolveModelDeployment(deploymentAlias);
    Console.WriteLine($"Using model deployment: {deployment}");

    if (selectedDemo is "plan-write" or "all")
    {
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        var result = await PlanningDemos.RunPlanAndWriteAsync(
            chatClient,
            writeOutput: Console.WriteLine);
        Console.WriteLine(PlanningDemos.FormatResult(result));
    }

    if (selectedDemo is "deep-research" or "all")
    {
        var researchDeployment =
            Environment.GetEnvironmentVariable("Foundry__DeepResearchModelDeployment")
            ?? Environment.GetEnvironmentVariable("FOUNDRY_DEEP_RESEARCH_MODEL_DEPLOYMENT")
            ?? "o3-deep-research-2025-06-26";
        Console.WriteLine($"Using Deep Research deployment: {researchDeployment}");
        await FoundryDeepResearchDemo.RunAsync(
            settings,
            researchDeployment,
            writeOutput: Console.WriteLine);
    }
}
catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
