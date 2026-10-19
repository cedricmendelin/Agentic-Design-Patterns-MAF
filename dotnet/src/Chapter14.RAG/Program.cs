using AgenticDesignPatterns.Shared;
using Chapter14.RAG;
using Microsoft.Extensions.AI;

var validModes = new HashSet<string>(
    ["rag", "foundry-files", "foundry-web", "all"],
    StringComparer.OrdinalIgnoreCase);
var mode = "rag";
string? deploymentAlias = null;
var questionParts = new List<string>();
var invalidArguments = false;
var modeSelected = false;

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
    else if (!modeSelected && validModes.Contains(args[index]))
    {
        mode = args[index];
        modeSelected = true;
    }
    else
    {
        questionParts.Add(args[index]);
    }
}

if (invalidArguments)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project dotnet/src/Chapter14.RAG " +
        "[rag|foundry-files|foundry-web|all] [--deployment <name>] [question]");
    return 1;
}

var defaultQuestion = "What did the president say about Justice Breyer?";
var question = questionParts.Count == 0 ? defaultQuestion : string.Join(' ', questionParts);

try
{
    if (mode.Equals("rag", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        var settings = FoundryChatClientFactory.LoadSettings();
        using IChatClient chatClient = FoundryChatClientFactory.CreateChatClient(settings, deploymentAlias);
        Console.WriteLine($"Model deployment: {settings.ResolveModelDeployment(deploymentAlias)}");
        Console.WriteLine($"\nUser: {question}");
        Console.WriteLine("\n--- Retrieved context ---");
        var answer = await RagAgent.RunAsync(chatClient, question, Console.WriteLine);
        Console.WriteLine("\n--- Final grounded response ---");
        Console.WriteLine(answer);
    }

    if (mode.Equals("foundry-files", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n--- Foundry-native File Search ---");
        Console.WriteLine(await FoundryNativeDemo.RunAsync("foundry-files", question, deploymentAlias));
    }

    if (mode.Equals("foundry-web", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\n--- Foundry-native Web Search ---");
        Console.WriteLine(await FoundryNativeDemo.RunAsync("foundry-web", question, deploymentAlias));
    }
}
catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

return 0;
