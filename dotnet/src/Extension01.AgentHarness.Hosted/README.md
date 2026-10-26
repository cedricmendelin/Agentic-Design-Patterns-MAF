# Hosted Agent Harness - Enterprise Extension 01

Deploy the [Agent Harness pattern](../Extension01.AgentHarness/README.md) as a Microsoft Foundry hosted agent. This project uses the official MAF `AgentHost` / `AddFoundryResponses` integration and references the console project's reusable `HarnessRunner`; it does not duplicate prompts or capture console output.

## Request Contract

Send plain-text input about the synthetic `checkout` or `identity` services to the **Responses protocol, version 2.0.0**. Input must be non-empty and at most 4,000 characters.

```json
{
  "model": "extension01-agent-harness",
  "input": "Investigate checkout latency. Inspect health and the runbook, then recommend next steps.",
  "stream": false,
  "store": false
}
```

The assistant's `output_text` is a JSON-serialized `HarnessResult`, with:

| Field | Meaning |
|---|---|
| `status` | `completed` or `incomplete`, determined by code |
| `answer` | Actual final model response; recommendations only |
| `modelCalls` | Model-client calls across the entire request |
| `todos` | `{ id, title, isComplete }` snapshots from MAF's session-backed provider |
| `evidence` | `{ service, kind, content }` receipts recorded by successful domain tools |
| `incompleteReason` | Missing work plan, unfinished todos, no named supported target, or missing health/runbook evidence for each named supported service; otherwise null |

The outer Responses status describes protocol completion; always inspect the **inner `status`** before treating an investigation as complete. Budget exhaustion, cancellation, empty model output, and provider failures propagate through the host error path rather than becoming successful synthetic answers.

## State, Interaction, and Streaming

Each request creates a fresh harness, session, todo provider, tools, evidence list, and global call budget. The adapter uses only the latest user message, so the caller must supply a self-contained task each time. Prior assistant messages and caller-supplied instructions are not treated as system authority.

The host's explicit isolation provider uses the response ID, including when Foundry does not supply a user header. Session serialization contains no application state. There is no cross-request memory, approval wait, background recovery, or conversation resume. This deliberately stateless application contract is narrower than the platform's conversation/storage capabilities; `store: false` is recommended.

Model calls are non-streaming. For a streaming Responses request, the adapter buffers the finished result and emits one final text update; it does not stream intermediate tool arguments or progress. Caller cancellation flows into the entire harness. Each request has a three-minute cooperative deadline, at most 12 model-client calls, and at most two outer todo-loop passes.

## Prerequisites

- .NET 10 SDK, PowerShell 7, Azure CLI, azd, and the versions/extensions declared in [`dotnet\azure.yaml`](../../azure.yaml).
- An existing Foundry project with hosted-agent support and a tool-capable chat-model deployment.
- Local developer inference permission for local model calls.
- Azure agent deployment permission, plus authorization to assign runtime inference access when missing.

See the [shared hosted setup](../../README.md#configure-and-deploy-hosted-agents). Do not reinitialize the existing multi-service manifest or deploy unrelated chapter services.

## Configuration and Authentication

For local runs, the host reads endpoint/model values from shared user-secrets, overridden by environment variables:

```powershell
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:Foundry__ModelDeployment = '<existing-model-deployment>'
```

`FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are fallback runtime names. Canonical `Foundry__*` values win, so keep any local overrides aligned with the intended target.

The host **never reads or uses the API key**, even when one is configured in shared user-secrets for console demos. Locally it uses `DefaultAzureCredential`; in the Foundry hosting environment it uses system-assigned `ManagedIdentityCredential`. Keep the console API-key path unchanged. Azure CLI and azd sign-in are separate from model-client configuration.

## Run and Invoke Locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Extension01.AgentHarness.Hosted
```

The host listens on port 8088 by default. From a second PowerShell terminal:

```powershell
$body = @{
    model = 'extension01-agent-harness'
    input = 'Investigate checkout latency. Check its health and runbook, then recommend next steps.'
    stream = $false
    store = $false
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:8088/responses' -ContentType 'application/json' -Body $body
```

Or invoke through azd from `dotnet`, selecting the service explicitly:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke extension01-agent-harness --local --protocol responses 'Investigate identity authentication latency.'
```

Stop the local server with Ctrl+C when finished. Deterministic tests exercise the real harness and the local HTTP Responses endpoint using a scripted model:

```powershell
dotnet test .\dotnet\tests\AgentHarness.Tests
```

## Configure and Deploy to Your Project

Follow the shared setup to select your own azd environment and existing Foundry project. Replace the manifest's sample `azure.ai.project` service reference and this service's `uses` entry with your project; do not assume the sample project is yours.

From `dotnet`, set reader-owned deployment values:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd env select '<your-environment>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-model-deployment>'
azd deploy extension01-agent-harness --no-prompt
azd ai agent show extension01-agent-harness --output json
```

The service uses environment substitution for endpoint/model values. It does not provision a Foundry project or model. Deployment incurs charges and creates an immutable agent version; perform it only in a project you have selected and are authorized to change.

### Required Runtime Inference Access

Before remote invocation, read **`instance_identity.principal_id`** from the current agent's `show` output and discover the actual model-hosting Foundry account ARM ID. Do not use the deploying user, project identity, or blueprint identity.

```powershell
az role assignment list --assignee-object-id '<runtime-principal-id>' --scope '<model-hosting-account-arm-id>' --include-inherited --fill-principal-name false --output table
# Only when inference access is missing and the role assignment is authorized:
az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
```

That role is **Cognitive Services OpenAI User**. Equivalent existing inference access can be reused. Keep scope at the actual model-hosting account, allow for RBAC propagation, and repeat the check after redeployment because runtime identities can change. Do not bypass missing access with an API key or broader subscription roles.

After access is effective, invoke a representative request:

```powershell
azd ai agent invoke extension01-agent-harness --protocol responses 'Investigate checkout latency. Check health and the runbook and recommend next steps.'
```

An active agent version alone does not establish readiness; verify a remote response and its inner completion/evidence contract.

## Packaging

The multi-service manifest selects `.NET 10` code deployment with `dependencyResolution: bundled`. azd publishes the host and its referenced `Extension01.AgentHarness` and `Shared` assemblies before upload, using the central build/package settings. Keep `project` pointing to the source project, not to an already published folder.

`.agentignore` excludes environment files, `.azure`, user-secrets, and source build output. Do not copy local secrets into publish output. To inspect a package independently:

```powershell
dotnet publish .\dotnet\src\Extension01.AgentHarness.Hosted -c Release -o '<scratch-publish-directory>'
```

The output should include `Extension01.AgentHarness.Hosted.dll`, `Extension01.AgentHarness.dll`, `AgenticDesignPatterns.Shared.dll`, `Microsoft.Agents.AI.Harness.dll`, and the host's `.deps.json` and `.runtimeconfig.json`. It must not contain `.azure`, `.env`, or `secrets.json`.

## Telemetry, Cost, and Ownership

The deployment manifest enables `Harness__EnableSensitiveTelemetry` for this synthetic teaching scenario. With an exporter configured, prompts/responses can appear in traces. Restrict access and retention; set this value to `false` before handling confidential requests. Local sensitive capture is off by default. The registered source is `Extension01.AgentHarness`; attach your chosen exporter to retain traces.

Hosting, model calls, context compaction, and retries have costs. A model-call cap is not a billing cap, and provider retries occur within a counted client call.

Remove only agent versions/resources you own using Foundry's agent-scoped controls. Never use `azd down` to clean up an experiment in a shared project, and do not delete shared models or unrelated chapter agents.

## Pattern Tradeoffs

The harness makes operational boundaries explicit but does not make model judgments deterministic. A recorded plan can be poor, or completed todos can accompany an inaccurate recommendation. Add independent evaluation for semantic correctness, and explicit authorization/idempotency before introducing write tools. Durable state requires a separate storage and resume design.

## References

- [Agent Harness guide and pattern combinations](../Extension01.AgentHarness/README.md)
- [Official Foundry MAF local-tools sample](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/local-tools)
- [Foundry hosted agents](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents)
