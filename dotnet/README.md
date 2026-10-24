# Agentic Design Patterns — .NET / Microsoft Agent Framework

Chapter demos use Microsoft Agent Framework (MAF) workflows and model deployments hosted in Microsoft Foundry. Shared configuration is stored outside the repository with .NET user-secrets.

## Prerequisites

- .NET 10 SDK
- A Microsoft Foundry project endpoint, deployed model name, and API key

## Configure Foundry user-secrets

Set the values locally; do not add them to `appsettings.json`, source files, or commits:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<your-api-key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment-name>" --project dotnet/src/Shared
```

`ModelDeployment` is the default. Add optional, named deployments for per-run selection:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:fast" "<fast-deployment-name>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:reasoning" "<reasoning-deployment-name>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:quality" "<quality-deployment-name>" --project dotnet/src/Shared
```

All chapter projects use the same `UserSecretsId`. The corresponding environment variable overrides are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`; named deployments use `Foundry__ModelDeployments__<name>`.
For the OpenAI-compatible model API, the shared client derives `https://<resource>.openai.azure.com/openai/v1/` from a standard Foundry project endpoint on `<resource>.services.ai.azure.com`.

Chapter 16 can optionally route to `fast`, `reasoning`, and `quality` model deployment aliases. Its console demo supports Google Custom Search for current-information requests; configure `GoogleCustomSearch:ApiKey` and `GoogleCustomSearch:SearchEngineId` in user-secrets or use `GOOGLE_CUSTOM_SEARCH_API_KEY` and `GOOGLE_CSE_ID` environment variables. Keep these external-service credentials out of source control. The hosted companion instead uses Foundry's Bing-backed `web_search` tool with Azure credentials and needs no external search API key.

Select a configured name when running a chapter demo; omit it to use the default:

```powershell
dotnet run --project dotnet/src/Chapter01.PromptChaining -- specs --deployment fast
dotnet run --project dotnet/src/Chapter01.PromptChaining -- all --deployment quality
```

## GitHub Actions live smoke tests

Use two separate validation layers:

- **Default CI** (`.github/workflows/dotnet-ci.yml`) builds the solution and runs deterministic tests that do not require Foundry credentials.
- **Live Foundry smoke tests** (`.github/workflows/live-foundry-tests.yml`) are manual-only and use a protected GitHub Environment instead of local user-secrets.

Create a GitHub Environment named `foundry-integration` and add these secrets:

- `FOUNDRY_PROJECT_ENDPOINT`
- `FOUNDRY_API_KEY`
- `FOUNDRY_MODEL_DEPLOYMENT`
- Optional aliases such as `FOUNDRY_MODEL_DEPLOYMENT_FAST`, `FOUNDRY_MODEL_DEPLOYMENT_REASONING`, and `FOUNDRY_MODEL_DEPLOYMENT_QUALITY`

The live workflow maps those secrets onto the existing runtime environment variables:

- `Foundry__ProjectEndpoint`
- `Foundry__ApiKey`
- `Foundry__ModelDeployment`
- `Foundry__ModelDeployments__fast`
- `Foundry__ModelDeployments__reasoning`
- `Foundry__ModelDeployments__quality`

Chapter 06's plan-and-write, Chapter 08's session-memory, Chapter 09's prompt-adaptation, Chapter 12's recovery workflows, and Chapter 19's notebook/MEAI evaluation demos are included in the live Foundry smoke tests. Chapter 06's optional Deep Research demo is not part of the smoke suite because it requires a separately deployed Deep Research model and runs as a potentially long background task. Chapter 08's Foundry-managed memory variant requires a configured embedding deployment and Entra ID, so it is an opt-in demo rather than part of the API-key smoke suite. Chapter 19's separate native cloud evaluation also requires Entra ID and creates evaluation resources, so it is run explicitly rather than by this API-key smoke suite.

That means the cloud agent and GitHub Actions jobs can invoke the chapter demos without `dotnet user-secrets`, as long as the workflow job runs inside the protected environment.

To run the live smoke tests manually:

1. Open **Actions** → **Live Foundry smoke tests**.
2. Start the workflow with an optional deployment alias if you want to target a named deployment.
3. The workflow builds the solution, runs deterministic tests, then runs live model smoke tests for Chapters 01–09, 12, and 19.

Any new test project that you add to `dotnet/AgenticDesignPatterns.slnx` is picked up by the non-live `dotnet test` step automatically. Live Foundry validation is intentionally narrower: the workflow checks out the default branch before loading protected secrets, so new demos only run there after you add coverage in `dotnet/tests/Foundry.SmokeTests` and merge that change to the default branch.

## Build

```powershell
dotnet build dotnet/AgenticDesignPatterns.slnx
```

## Chapter demos

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 01 | Prompt Chaining | `src/Chapter01.PromptChaining` | [Chapter 01 guide](src/Chapter01.PromptChaining/README.md) |
| 02 | Routing | `src/Chapter02.Routing` | [Chapter 02 guide](src/Chapter02.Routing/README.md) |
| 03 | Parallelization | `src/Chapter03.Parallelization` | [Chapter 03 guide](src/Chapter03.Parallelization/README.md) |
| 04 | Reflection | `src/Chapter04.Reflection` | [Chapter 04 guide](src/Chapter04.Reflection/README.md) |
| 05 | Tool Use | `src/Chapter05.ToolUse` | [Chapter 05 guide](src/Chapter05.ToolUse/README.md) |
| 06 | Planning | `src/Chapter06.Planning` | [Chapter 06 guide](src/Chapter06.Planning/README.md) |
| 07 | Multi-Agent Collaboration | `src/Chapter07.MultiAgent` | [Chapter 07 guide](src/Chapter07.MultiAgent/README.md) |
| 08 | Memory | `src/Chapter08.Memory` | [Chapter 08 guide](src/Chapter08.Memory/README.md) |
| 09 | Learning & Adaptation | `src/Chapter09.LearningAdaptation` | [Chapter 09 guide](src/Chapter09.LearningAdaptation/README.md) |
| 10 | MCP | `src/Chapter10.MCP` | [Chapter 10 guide](src/Chapter10.MCP/README.md) |
| 12 | Exception Handling and Recovery | `src/Chapter12.ExceptionHandling` | [Chapter 12 guide](src/Chapter12.ExceptionHandling/README.md) |
| 19 | Evaluation & Monitoring | `src/Chapter19.Evaluation` | [Chapter 19 guide and evaluation-tool comparison](src/Chapter19.Evaluation/README.md) |
| 16 | Resource Optimization | `src/Chapter16.ResourceOptimization` | [Chapter 16 guide](src/Chapter16.ResourceOptimization/README.md) |

## Hosted-agent companions

The hosted projects expose the same MAF chapter logic through Microsoft Foundry Agent Service's **Responses protocol**. Use them as starting points for deploying the patterns to your own Foundry project.

| Chapter | Hosted guide | Request |
|---|---|---|
| 02 | [Routing](src/Chapter02.Routing.Hosted/README.md) | Booking, information, or clarification |
| 03 | [Parallelization](src/Chapter03.Parallelization.Hosted/README.md) | `topic:` or `research:` |
| 04 | [Reflection](src/Chapter04.Reflection.Hosted/README.md) | `fact-check:`, `product:`, or `code-review:` |
| 05 | [Tool Use](src/Chapter05.ToolUse.Hosted/README.md) | `search:`, `calculate:`, or `stock:` |
| 06 | [Planning](src/Chapter06.Planning.Hosted/README.md) | Topic to plan and summarize |
| 07 | [Multi-Agent Collaboration](src/Chapter07.MultiAgent.Hosted/README.md) | `<scenario>: <task>` or a task for the default `crew` scenario |
| 07 | [Distributed crew](src/Chapter07.MultiAgent.Distributed/README.md) | Topic sent to an orchestrator that calls separately hosted researcher and writer agents |
| 08 | [Memory](src/Chapter08.Memory.Hosted/README.md) | Scripted managed-memory demo; synthetic facts and a fresh scope per request |
| 09 | [Learning & Adaptation](src/Chapter09.LearningAdaptation.Hosted/README.md) | Starting prompt instructions to evaluate and revise |
| 10 | [MCP](src/Chapter10.MCP.Hosted/README.md) | Request to the configured remote MCP server |
| 12 | [Exception Handling and Recovery](src/Chapter12.ExceptionHandling.Hosted/README.md) | Location address; bounded retry and area fallback |
| 19 | [Evaluation](src/Chapter19.Evaluation.Hosted/README.md) | Legal survey question for a structured five-criterion review |
| 16 | [Resource Optimization](src/Chapter16.ResourceOptimization.Hosted/README.md) | Request routed by intent; current-information requests use Foundry's Bing-backed `web_search` tool |

### Configure and deploy hosted agents

#### Tooling and Azure prerequisites

- .NET 10 SDK, PowerShell 7, Azure CLI, and Azure Developer CLI (azd).
- azd version 1.35.1 or later and the `azure.ai.agents` extension version 1.0.0-beta.19 or later, as declared in `azure.yaml`.
- An existing Foundry project in a region that supports hosted agents, with hosting configured and a compatible chat-model deployment.
- Permission to deploy agents to the project and model-inference access for both your local developer identity and the hosted runtime identity.

Install the agent extension if needed:

```powershell
azd extension install azure.ai.agents
```

#### Select your project and model

All commands in this section run from `dotnet`. Sign in to your target tenant and create an azd environment, or select an existing one with `azd env select <environment>`:

```powershell
Set-Location .\dotnet
az login --tenant '<tenant-id>'
azd auth login --tenant-id '<tenant-id>'
azd env new '<environment>' --subscription '<subscription-id>' --location '<project-region>' --no-prompt
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-model-deployment>'
```

Use the actual ARM ID and endpoint of your project. The committed `azure.yaml` contains a concrete sample project reference: **replace it with your own project before deploying**. Rename the `host: azure.ai.project` service key, set its `endpoint` to your project endpoint, and update the chapter services' `uses` entries to that same key. For example:

```yaml
services:
    chapter02-routing:
        # Keep the other chapter service settings.
        uses:
            - my-foundry-project
        env:
            AZURE_AI_MODEL_DEPLOYMENT_NAME: <existing-model-deployment>
    my-foundry-project:
        host: azure.ai.project
        endpoint: https://<account>.services.ai.azure.com/api/projects/<project>
```

This is a configuration excerpt, not a replacement for the complete manifest. Each chapter service explicitly selects `gpt-5-mini` in its `env` block; replace that value with your existing model deployment name for every service you intend to use. Setting an azd environment variable alone does not replace a literal service setting. Keep the project reference and environment values consistent.

The manifest already includes the hosted services, so do not reinitialize or overwrite it for a normal deployment. Keep each service's project path, startup command, protocol, runtime, and bundled packaging settings. Do not use this directory's own `azure.yaml` as an initialization template into the same directory.

These guides target an existing project and model; they do not provision either. If hosting, regional availability, or permissions need setup, resolve those prerequisites with your Azure administrator.

#### Authentication and runtime permissions

Hosted startup reads the shared `Foundry` user-secrets and corresponding `Foundry__*` environment overrides. When canonical settings are absent, `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` provide runtime fallbacks. Make sure local secrets or overrides do not point at a different project or model.

For local Entra ID authentication, leave `Foundry:ApiKey` blank or unset it, and remove any nonempty `Foundry__ApiKey` override. The console demos require an API key; if you remove a shared key for a hosted run, configure it again before running a console demo. Azure execution should use managed identity, not an API key. Azure CLI and azd sign-in are separate from model-client configuration.

Deploy the selected service using its chapter guide, then inspect it with `azd ai agent show <service> --output json`. Use **`instance_identity.principal_id`**, not the deploying user, project identity, or agent blueprint, when configuring runtime inference access:

1. Identify the actual model-hosting Foundry account ARM ID.
2. Check effective role assignments, including inherited grants, for the runtime principal.
3. If equivalent inference access is missing, have an authorized administrator assign **Cognitive Services OpenAI User** at that account scope.
4. Allow time for RBAC propagation before sending a remote request. Recheck the principal after redeployment because the runtime identity can change.

Example commands for inspecting and assigning access:

```powershell
az role assignment list --assignee-object-id '<runtime-principal-id>' --scope '<model-hosting-account-arm-id>' --include-inherited --fill-principal-name false --output table
# Run only if the role is missing and you are authorized to assign it.
az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
```

If invocation returns `401 PermissionDenied`, check the runtime principal, account scope, model endpoint, and role propagation before retrying. Do not broaden access to the subscription or switch to API keys to bypass missing runtime permissions.

#### Packaging, costs, and resource ownership

`dependencyResolution: bundled` makes the azd extension publish the .NET 10 host and its referenced chapter/Shared projects locally before uploading. This includes central build/package settings and sibling dependencies without requiring Docker or a container registry. Keep `.azure`, environment files, user-secrets, and connection strings out of published content and source control.

Deploy only the selected chapter with `azd deploy <service> --no-prompt`; a service-scoped command avoids redeploying other chapters. Hosting and model calls incur Azure charges, and each deployment creates an immutable agent version. When removing an experiment, use Foundry's agent-scoped management controls for the resources you own. Do not run `azd down` against a shared project or delete shared models, project infrastructure, or unrelated agents.
