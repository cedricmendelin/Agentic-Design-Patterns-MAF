# Foundry + Shared Solution Setup

Create once under `dotnet/`. Verify package names/versions against nuget.org and MAF docs at implementation time (preview APIs move).

## Layout
```
dotnet/
├── AgenticDesignPatterns.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
└── src/
    ├── Shared/Shared.csproj
    │   ├── FoundrySettings.cs
    │   └── FoundryChatClientFactory.cs
    └── Chapter01.PromptChaining/...
```

## Directory.Build.props (key parts)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <!-- One secrets.json shared by every chapter -->
    <UserSecretsId>agentic-design-patterns-maf</UserSecretsId>
  </PropertyGroup>
</Project>
```

## Packages (central versions, use latest prerelease where noted)

Core (MAF + model deployment, always):
| Package | Why |
|---|---|
| `Microsoft.Agents.AI` (prerelease) | `AIAgent`, `ChatClientAgent` |
| `Microsoft.Agents.AI.OpenAI` (prerelease) | OpenAI-compatible chat client → agent |
| `Microsoft.Agents.AI.Workflows` (prerelease) | `WorkflowBuilder`, `AgentWorkflowBuilder`, executors |
| `OpenAI` | `OpenAIClient` with `ApiKeyCredential` |
| `Microsoft.Extensions.AI.OpenAI` (prerelease) | `AsIChatClient()` |
| `Microsoft.Extensions.Configuration.UserSecrets` | read `secrets.json` |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | CI override |
| `Microsoft.Extensions.Configuration.Binder` | bind `FoundrySettings` |

Foundry-native variants only (add to the chapter project, not Shared core):
| Package | Why |
|---|---|
| `Azure.AI.Projects` (prerelease) | `AIProjectClient`, Agent Service / hosted agents |
| `Microsoft.Agents.AI.Foundry` (prerelease) | Foundry agent → `AIAgent` |
| `Azure.Identity` | Agent Service APIs require Entra ID (`az login`) |

## secrets.json keys
```json
{
  "Foundry:ProjectEndpoint": "https://<resource>.services.ai.azure.com/api/projects/<project>",
  "Foundry:ApiKey": "<key from Foundry portal>",
  "Foundry:ModelDeployment": "gpt-4.1-mini",
  "Foundry:SmallModelDeployment": "<optional, for routing/SLM demos>"
}
```
Set via (user types the key; agent never asks for it in chat):
```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "<endpoint>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment>" --project dotnet/src/Shared
```
Env var override: `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, `Foundry__ModelDeployment`.

## Shared code sketch
The model is called through the resource's OpenAI-compatible v1 endpoint, derived from the project endpoint host: `https://<resource>.services.ai.azure.com/openai/v1/`.
```csharp
public sealed record FoundrySettings
{
    public required string ProjectEndpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string ModelDeployment { get; init; }
    public string? SmallModelDeployment { get; init; }

    public Uri OpenAIEndpoint => new($"https://{new Uri(ProjectEndpoint).Host}/openai/v1/");
}

public static class FoundryChatClientFactory
{
    public static FoundrySettings LoadSettings()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(FoundryChatClientFactory).Assembly)
            .AddEnvironmentVariables()
            .Build();

        return config.GetSection("Foundry").Get<FoundrySettings>()
            ?? throw new InvalidOperationException(
                "Foundry settings missing. See dotnet/README.md to configure user-secrets.");
    }

    public static IChatClient CreateChatClient(FoundrySettings s, string? deployment = null) =>
        new OpenAIClient(new ApiKeyCredential(s.ApiKey), new OpenAIClientOptions { Endpoint = s.OpenAIEndpoint })
            .GetChatClient(deployment ?? s.ModelDeployment)
            .AsIChatClient();
}
```
`AddUserSecrets(assembly)` reads the `UserSecretsId` attribute — since it is set in `Directory.Build.props`, every project resolves the same `secrets.json`.

## Agent creation pattern
```csharp
var settings = FoundryChatClientFactory.LoadSettings();
IChatClient chat = FoundryChatClientFactory.CreateChatClient(settings);

ChatClientAgent extractor = new(chat,
    name: "Extractor",
    instructions: "Extract the technical specifications from the text.");
```

## Agent run pattern (non-streaming, preferred)
```csharp
AgentResponse response = await extractor.RunAsync(inputText);
Console.WriteLine(response.Text);

// Structured output
AgentResponse<Specs> specs = await transformer.RunAsync<Specs>(response.Text);
```

## Workflow run pattern (non-streaming, preferred)
```csharp
var workflow = AgentWorkflowBuilder.BuildSequential([agentA, agentB]);
Run run = await InProcessExecution.RunAsync(workflow, messages);
foreach (WorkflowEvent evt in run.NewEvents)
{
    if (evt is WorkflowOutputEvent output)
        Console.WriteLine(output.Data);
}
```
Verify exact type/member names (`Run`, `NewEvents`, `AgentResponse`) against current MAF docs/build — preview APIs change.

## Workflow run pattern (streaming — only when required, e.g. HITL)
```csharp
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
await foreach (WorkflowEvent evt in run.WatchStreamAsync()) { /* RequestInfoEvent / WorkflowOutputEvent */ }
```
