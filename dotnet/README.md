# C# / Microsoft Agent Framework demos

The [root quickstart](../README.md#get-started) runs locally against one Microsoft Foundry chat deployment. This guide is the shared configuration and catalog; each pattern README owns its actual default, variants, output contract and code entry point.

## Shared setup

Install the .NET 11 SDK (11.0.100, pinned in the repository's [global.json](../global.json)) and keep the .NET 10 and ASP.NET Core 10 runtimes installed alongside it for `net10.0` tests and local hosted runs (installing the .NET 10 SDK supplies both). Configure the three settings once from the repository root. Console demos, reusable pattern projects, shared libraries, and test projects target both `net10.0` and `net11.0`; choose `--framework net11.0` for local demos. Foundry `.Hosted` and distributed entry points are single-target `net10.0` to match their unchanged `azure.yaml` runtime and startup commands. Unqualified builds and tests cover both frameworks where projects multi-target.

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ApiKey" "<resource-api-key>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment-name>" --project .\dotnet\src\Shared
dotnet run --framework net11.0 --project .\dotnet\src\PromptChaining -- specs
```

Required infrastructure: a Microsoft Foundry account/resource and project, an accessible compatible generative chat deployment and its **resource API key**. Structured-output examples require JSON-schema support; tools require function calling. The shared client derives `<resource>.openai.azure.com/openai/v1/` from `<resource>.services.ai.azure.com/api/projects/<project>`; other project-host shapes are rejected rather than guessed. `ModelDeployment` is your deployment's name, not a model-family label. The API key must authorize the actual model-hosting resource.

All projects retain the shared user-secrets ID `agentic-design-patterns-maf`. Configuration loads user-secrets then environment overrides. Keys stay outside source control; user-secrets are not encrypted production storage.

| Setting | Environment override |
|---|---|
| `Foundry:ProjectEndpoint` | `Foundry__ProjectEndpoint` |
| `Foundry:ApiKey` | `Foundry__ApiKey` |
| `Foundry:ModelDeployment` | `Foundry__ModelDeployment` |
| `Foundry:ModelDeployments:<alias>` | `Foundry__ModelDeployments__<alias>` |

Optional aliases allow explicit selection without changing the default:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:fast" "<fast-deployment-name>" --project .\dotnet\src\Shared
dotnet run --framework net11.0 --project .\dotnet\src\PromptChaining -- specs --deployment fast
```

`fast`, `reasoning` and `quality` are useful aliases, not provisioned models. ResourceOptimization documents its fallback to the default when an alias is absent. DecisionModels has separate decision deployment requirements; do not substitute them for the general chat default.

## Pattern families

Patterns live flat under `src\<PatternName>`; tests mirror them under `tests\<PatternName>.Tests`. Book chapters and families are descriptive metadata. The [root catalog](../README.md#choose-a-pattern) lists all 21 book patterns and the two repository additions; the [design guide](../docs/design-patterns.md) explains choosing and composing them.

| Family | Demos |
|---|---|
| [Flow](../docs/flow.md) | [PromptChaining](src/PromptChaining/README.md), [Routing](src/Routing/README.md), [Parallelization](src/Parallelization/README.md), [Planning](src/Planning/README.md), [Prioritization](src/Prioritization/README.md), [DecisionModels](src/DecisionModels/README.md) |
| [Reason & Improve](../docs/reason-and-improve.md) | [Reflection](src/Reflection/README.md), [LearningAdaptation](src/LearningAdaptation/README.md), [GoalSettingMonitoring](src/GoalSettingMonitoring/README.md), [Reasoning](src/Reasoning/README.md), [ExplorationDiscovery](src/ExplorationDiscovery/README.md) |
| [Knowledge & Action](../docs/knowledge-and-action.md) | [ToolUse](src/ToolUse/README.md), [Memory](src/Memory/README.md), [MCP](src/MCP/README.md), [RAG](src/RAG/README.md) |
| [Collaboration](../docs/collaboration.md) | [MultiAgent](src/MultiAgent/README.md), [HumanInTheLoop](src/HumanInTheLoop/README.md), [A2A](src/A2A/README.md) |
| [Trust & Operations](../docs/trust-and-operations.md) | [ExceptionHandling](src/ExceptionHandling/README.md), [ResourceOptimization](src/ResourceOptimization/README.md), [Guardrails](src/Guardrails/README.md), [Evaluation](src/Evaluation/README.md), [AgentHarness](src/AgentHarness/README.md) |

Begin with one named local variant. `all` can cost multiple calls or require optional features; the default path is intentionally small. Structured .NET examples are primary; original scenarios and Python-generating examples remain secondary where applicable. Generated Python is saved for inspection, never executed by the demos.

## Optional infrastructure

None of these is required for the first PromptChaining run. Open the linked guide before choosing a native or hosted variant.

| Scenario | Additional requirements / effects |
|---|---|
| [Managed memory](src/Memory/README.md) | Entra identity, embedding deployment, memory-store permissions; asynchronous updates, scoped recall and cleanup |
| [MCP filesystem](src/MCP/README.md) | Node/npx, allowlisted stdio server and confined workspace |
| [MCP HTTP](src/MCP/README.md) | Running [MCP.Server](src/MCP.Server/README.md) on loopback; native integration needs a trusted authenticated endpoint reachable by Foundry |
| [Content Safety](src/Guardrails/README.md) | Separate service endpoint/key; policy screening runs without it |
| [Google search/model selection](src/ResourceOptimization/README.md) | `GoogleCustomSearch:ApiKey` / `GoogleCustomSearch:SearchEngineId`, or `GOOGLE_CUSTOM_SEARCH_API_KEY` / `GOOGLE_CSE_ID`; model aliases optional |
| [Deep Research](src/Planning/README.md) / [native reasoning](src/Reasoning/README.md) | Compatible deployed models/features and Entra identity where required; background work can be slow and paid |
| [Decision models](src/DecisionModels/README.md) | Compatible decision deployments/aliases distinct from the generative chat deployment |
| [Native evaluation](src/Evaluation/README.md) | Entra/evaluation permissions; uploads datasets and creates remote jobs, with explicit resource cleanup; timeout is not cancellation |
| [Hosting/distributed agents](HOSTING.md) | Supported Foundry hosting, Azure CLI/azd authentication, packaging and runtime managed-identity inference permissions on the model-host account |

## Hosted-agent companions

`.Hosted` projects adapt a pattern; they are not another implementation of it. [HOSTING](HOSTING.md) owns common setup, runtime identity/RBAC, packaging, costs and cleanup. Each companion README owns its protocol, input/state/streaming contract. Most book patterns have a host; PromptChaining intentionally remains a simple console example. MultiAgent also offers a [distributed Responses crew](src/MultiAgent.Distributed/README.md), and A2A demonstrates a different protocol.

The path/assembly migration does **not** rename or redeploy existing Azure agents. Chapter-prefixed manifest service/agent identifiers may remain compatibility identifiers; local project paths and startup DLLs use pattern-only names. Reusable manifests parameterize reader-owned projects/models. Existing environments must align those values before a deliberate deployment.

Sensitive prompt/response capture is off by default. Enable it only for synthetic debugging; tracing, tool output and dataset retention still deserve access controls. A model-call budget is not an exact billing cap.

## Build and deterministic tests

Non-host demos, shared libraries, and test projects target both .NET 10 and .NET 11;
Foundry host projects remain `net10.0` only. These solution commands therefore build
and test both frameworks overall. Local console examples use `net11.0`; run or publish
a hosted project with `net10.0` explicitly (for example,
`dotnet publish .\dotnet\src\Reflection.Hosted -c Release -f net10.0`). The
`azure.yaml` manifests continue to declare `runtime: dotnet_10`.

```powershell
dotnet build .\dotnet\AgenticDesignPatterns.slnx
dotnet test .\dotnet\AgenticDesignPatterns.slnx --filter "Category!=LiveFoundry"
dotnet build .\update-conference-26\ConferenceDemos.slnx
dotnet test .\update-conference-26\ConferenceDemos.slnx
```

Test one pattern first with `dotnet test .\dotnet\tests\<PatternName>.Tests`. Deterministic tests require no Foundry secrets; they exercise output shapes, ordering, termination, errors, cancellation and identity boundaries. They do not certify live model quality or remote hosting.

## GitHub Actions live smoke tests

Default CI builds/tests both solutions without secrets and checks diagram drift. The existing **Live Foundry smoke tests** workflow is manual-only and checks out the trusted default branch. Configure its `foundry-integration` Environment with:

| GitHub Environment secret | Runtime mapping |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | `Foundry__ProjectEndpoint` |
| `FOUNDRY_API_KEY` | `Foundry__ApiKey` |
| `FOUNDRY_MODEL_DEPLOYMENT` | `Foundry__ModelDeployment` |
| `FOUNDRY_MODEL_DEPLOYMENT_FAST` (optional) | `Foundry__ModelDeployments__fast` |
| `FOUNDRY_MODEL_DEPLOYMENT_REASONING` (optional) | `Foundry__ModelDeployments__reasoning` |
| `FOUNDRY_MODEL_DEPLOYMENT_QUALITY` (optional) | `Foundry__ModelDeployments__quality` |

Select **Actions → Live Foundry smoke tests** and an optional deployment alias. The workflow runs deterministic checks before the narrower [Foundry.SmokeTests](tests/Foundry.SmokeTests) suite on .NET 11. Native managed-memory, Deep Research, native reasoning tools and native cloud evaluation remain separate opt-ins.

### Test a branch before merging

The separate **Live Foundry branch smoke tests** workflow must first be merged to the default branch. It resolves a same-repository branch name or full commit SHA once, runs deterministic tests without Foundry credentials, then requests approval to run live tests against that exact SHA. Workflow scripts come from the default-branch workflow commit, not the tested branch. Branch code is still arbitrary executable code and can read and exfiltrate credentials during the approved live job.

Before using it, a repository administrator must create the `foundry-branch-integration` Environment in **Settings → Environments**:

1. Require at least one human reviewer and restrict deployment branches to a single selected branch: the exact default branch (currently `main`), not a wildcard or tag. Disable administrator bypass where available. If the person triggering the workflow must also approve, do not enable prevention of self-review; otherwise use a separate reviewer and prevent self-review.
2. Add **environment-only** secrets `BRANCH_FOUNDRY_PROJECT_ENDPOINT`, `BRANCH_FOUNDRY_API_KEY`, and `BRANCH_FOUNDRY_MODEL_DEPLOYMENT`. Do not add these names as repository or organization secrets. Use an isolated test resource and a dedicated, minimally scoped credential with appropriate quotas and spending limits; never reuse production credentials.
3. Restrict the existing `foundry-integration` Environment to the default branch as well, and configure its own approval rules. An unprotected environment can otherwise expose its credentials to workflows modified on other branches.

The branch workflow refuses to enter its live job unless its environment has a required reviewer and exactly one default-branch deployment policy. Review the displayed commit and its changes before approving; approval is not a sandbox and cannot make untrusted code safe. Environment rules are an external security boundary and must remain enabled.

After setup, select **Actions → Live Foundry branch smoke tests**, choose the default branch as the workflow ref, and provide `test_ref`, `framework` and an optional additional smoke filter. For example:

```bash
gh workflow run live-foundry-branch-tests.yml --ref main \
  -f test_ref=my-branch -f framework=net11.0
```

Inspect the run summary and approval request for the resolved commit SHA. Branches without a `net11.0` test target may need `framework=net10.0`. A passing smoke test is not a deployment or a guarantee of model quality.
