# Chapter 17 — Hosted Reasoning Agent

Expose the [Chapter 17 MAF reasoning demos](../Chapter17.Reasoning/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host reuses the console project's reasoning logic and handles one caller-provided request at a time.

## Request and response

| Request | Behavior |
|---|---|
| `cot: <question>` or unprefixed text | Answer with a concise user-facing rationale; do not return private chain-of-thought. |
| `self-correction: <requirements and draft>` | Generate, review, and revise within a three-round budget; enforce the 150-character limit deterministically. |
| `react: <factorial request>` | Call the bounded factorial tool (integers 0 through 20) and return the result. |
| `deep-search: <question>` | Plan up to three queries, reflect, and synthesize using model knowledge only; no live web access is claimed. |

The host processes the latest user message, returns the completed result, and retains no conversation or workflow state. Responses streaming is supported by buffering the completed reasoning result into one final update. Intermediate stages are available in configured traces.

## Prerequisites and configuration

Follow the [shared hosted-agent guide](../../README.md#configure-and-deploy-hosted-agents) to select a Foundry project and chat model, configure the `chapter17-reasoning` service in `azure.yaml`, and install the hosted-agent tooling. The manifest contains a sample project reference; replace it with the ARM ID and endpoint of a project you own before deployment.

The hosted process uses a local API key only in the Development environment when the shared user-secret is present; otherwise it authenticates with Entra ID through `DefaultAzureCredential`. In Azure it always uses managed identity and ignores API-key configuration. The separate console demo continues using API-key user-secrets or `Foundry__*` environment variables.

Before remote invocation, ensure the current deployed runtime identity has model inference access:

1. Read `instance_identity.principal_id` from `azd ai agent show chapter17-reasoning --output json`.
2. Resolve the actual model-hosting Foundry account ARM ID and check effective role assignments, including inherited grants.
3. If equivalent access is absent and the assignment is approved, assign **Cognitive Services OpenAI User** to that runtime principal at the model-hosting account scope.
4. Allow for RBAC propagation, then verify access with a representative request. Recheck the principal after redeployment.

The deploying identity also needs permission to create or update hosted agents in the selected project. Deployment permissions and runtime inference permissions are separate.

## Run locally

Use the shared Foundry endpoint/model settings. Development launches use the shared API-key user-secret when configured; otherwise sign in with Entra ID. From the repository root:

```powershell
az login --tenant '<tenant-id>'
dotnet run --project .\dotnet\src\Chapter17.Reasoning.Hosted
```

The project listens on the development URLs in `Properties/launchSettings.json`. To run through the same Responses protocol as Foundry, from `dotnet`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'cot: Explain a topic supplied by the caller.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'self-correction: Write a short post about a new eco-friendly product.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'react: Calculate 10 factorial.'
azd ai agent invoke chapter17-reasoning --local --protocol responses 'deep-search: Summarize a topic from model knowledge.'
```

Stop the local host with Ctrl+C when finished. Keep the local endpoint private.

## Deploy and invoke

Complete the shared setup and replace the sample `uses` project in `azure.yaml` with your own Foundry project. Configure the model deployment in that service, then run from `dotnet`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter17-reasoning --no-prompt
azd ai agent show chapter17-reasoning --output json
```

After verifying or assigning the runtime inference role as described above and allowing propagation:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter17-reasoning --protocol responses 'react: Calculate 10 factorial.'
```

The service uses bundled .NET 10 code deployment; azd publishes the host and referenced Chapter 17 and Shared projects before upload. No Docker image or container registry is required. Hosting and model/tool calls may incur Azure charges. When removing an experiment, delete only the hosted agent you own using Foundry's agent-scoped management controls; do not run `azd down` against shared project infrastructure or remove unrelated agents.

## Telemetry and sensitive data

The hosted service emits spans for `Chapter17.Reasoning`. **Sensitive-data capture is enabled** for the MAF agents, so prompts, drafts, research notes, tool inputs, and responses may appear in traces when an exporter is configured. Use non-sensitive inputs, restrict access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

Reasoning, reflection, and model-knowledge research can produce plausible but incorrect results. A reviewer is not independent evidence, and this hosted agent does not provide live search. Use the chapter's Foundry-native Web Search mode for current, source-backed research. Keep tool permissions narrow, preserve deterministic input checks, and cap model-call budgets to control latency and cost.
