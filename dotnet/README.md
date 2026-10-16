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
dotnet user-secrets set "Foundry:ModelDeployments:quality" "<quality-deployment-name>" --project dotnet/src/Shared
```

All chapter projects use the same `UserSecretsId`. The corresponding environment variable overrides are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`; named deployments use `Foundry__ModelDeployments__<name>`.
For the OpenAI-compatible model API, the shared client derives `https://<resource>.openai.azure.com/openai/v1/` from a standard Foundry project endpoint on `<resource>.services.ai.azure.com`.

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
- Optional aliases such as `FOUNDRY_MODEL_DEPLOYMENT_FAST` and `FOUNDRY_MODEL_DEPLOYMENT_QUALITY`

The live workflow maps those secrets onto the existing runtime environment variables:

- `Foundry__ProjectEndpoint`
- `Foundry__ApiKey`
- `Foundry__ModelDeployment`
- `Foundry__ModelDeployments__fast`
- `Foundry__ModelDeployments__quality`

Chapter 06's plan-and-write workflow is included in the live Foundry smoke tests. Its optional Deep Research demo is not part of the smoke suite because it requires a separately deployed Deep Research model and runs as a potentially long background task.

That means the cloud agent and GitHub Actions jobs can invoke the chapter demos without `dotnet user-secrets`, as long as the workflow job runs inside the protected environment.

To run the live smoke tests manually:

1. Open **Actions** → **Live Foundry smoke tests**.
2. Start the workflow with an optional deployment alias if you want to target a named deployment.
3. The workflow builds the solution, runs deterministic tests, then runs smoke tests for Chapters 01–06.

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
| 06 | Planning | `src/Chapter06.Planning` | [Chapter 06 guide](src/Chapter06.Planning/README.md) |

## Hosted-agent companions

The [Routing hosted companion](src/Chapter02.Routing.Hosted/README.md) exposes the same Chapter 02 workflow through Foundry Agent Service's Responses protocol. `azure.yaml` configures .NET 10 bundled code deployment; the azd agent extension publishes referenced projects locally before uploading, without Docker or ACR. Hosted version 3 was deployed to `mvp-playground-foundry-proj` with `gpt-5-mini` and remotely verified on 2026-10-16.

The console demos keep their existing API-key configuration. Hosted startup reads the same `Foundry` user-secrets and `Foundry__*` environment overrides, supports `--deployment <alias>`, and uses Entra ID / managed identity when `ApiKey` is blank. Foundry runtime variables remain supported as fallbacks, and azd deployment authentication is separate. Follow the hosted guide for prerequisites, deployment status, local invocation, deployment, and cleanup. No Prompt Chaining hosted companion is included.

The [Parallelization hosted companion](src/Chapter03.Parallelization.Hosted/README.md) exposes the Chapter 03 MAF workflow through the same Responses protocol and bundled .NET 10 deployment. Hosted version 1 was deployed to the existing `mvp-playground-foundry-proj` project and remotely verified on 2026-10-16.

The [Reflection hosted companion](src/Chapter04.Reflection.Hosted/README.md) exposes the Chapter 04 MAF workflow through the Responses protocol and bundled .NET 10 deployment. Local code now uses one shared generator/verifier workflow graph for all three modes, with early acceptance and a three-round ceiling. Hosted version 3, which runs this graph with the stricter product verifier, was deployed to the existing `mvp-playground-foundry-proj` project with `gpt-5-mini` and remotely verified using managed identity on 2026-10-16. Its evaluation setup contains 15 generated cases and a seven-dimension rubric, preserved unchanged for review against the changed contract; no evaluation run has been started.

The [Planning hosted companion](src/Chapter06.Planning.Hosted/README.md) exposes Chapter 06's bounded MAF planner/writer workflow through the Responses protocol. It uses bundled .NET 10 deployment and creates fresh agents per request. Version 1 was deployed to the existing `mvp-playground-foundry-proj` project with `gpt-5-mini` and remotely verified using managed identity on 2026-10-16. The separate Deep Research path is not included in this host because its long-running background response requires an explicit job/polling lifecycle.
