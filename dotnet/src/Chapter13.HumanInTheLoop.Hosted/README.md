# Chapter 13 - Hosted Human-in-the-Loop Support Agent

This project hosts the [Chapter 13 MAF customer-support agent](../Chapter13.HumanInTheLoop/README.md) through Foundry's **Invocations protocol**. It preserves the approval pause: an escalation request returns a pending approval, and the caller resumes the same session with an approve or reject decision.

## Request contract and state

Start a request with a task and optional customer context:

```json
{
  "task": "My headphones still disconnect after troubleshooting. Escalate this complex issue.",
  "customer": {
    "name": "Sam",
    "tier": "premium",
    "recentPurchases": ["Wireless headphones"],
    "supportHistory": ["Previously contacted support about Bluetooth pairing."]
  }
}
```

The response is either `status: "completed"` with `final_output`, or `status: "awaiting_approval"` with an `approval` (one request) or `approvals` (multiple requests), each containing the MAF request ID, tool name, and arguments. If a task retry arrives for a session with a pending approval and identical task and customer context, the host replays the saved proposal without rerunning the model. This lets the caller recover if the first response was lost. A different task still receives a conflict until the pending approvals are resolved.

For one pending approval, send the decision in the same `agent_session_id`:

```json
{
  "decision": "approve",
  "requestId": "<request_id from the pending response>"
}
```

Use `"reject"` to deny the tool call. When multiple approvals are pending, send a decision for each request in one invocation:

```json
{
  "approvals": [
    { "decision": "approve", "requestId": "<first request_id>" },
    { "decision": "reject", "requestId": "<second request_id>" }
  ]
}
```

The server requires each pending request ID exactly once and rejects stale or mismatched IDs. A new task can reuse a session only after its pending approvals have been resolved.

Each Foundry platform user gets an isolated session partition, including both the storage path and in-process lock. A hosted invocation without the platform-injected user identity is rejected. Locally, where platform identity is not present, the protocol session ID is the caller boundary. MAF's serialized agent session and pending approvals are written atomically under the host user's home directory so a later invocation can resume after process restart. The file store and in-process lock are a single-instance sample design, not a distributed store: use encrypted shared persistence and distributed locking for multiple replicas, set retention/deletion policies, and protect customer/profile data in production.

## Prerequisites and configuration

- .NET 10 SDK, PowerShell 7, Azure CLI, and azd with the `azure.ai.agents` extension.
- An existing Foundry project with hosted-agent support and a deployed chat model.
- Local developer credentials with model inference access, or the hosted runtime identity with **Cognitive Services OpenAI User** on the actual model-hosting Foundry account.
- Follow [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select the reader's project/model and configure authentication. The console project separately uses shared user-secrets and an API key.

The hosted process uses `DefaultAzureCredential` (developer identity locally and managed identity in Azure); it does not use or require the console API key. Its telemetry enables sensitive-data capture for debugging, including customer prompts and tool arguments. Use synthetic data and restrict telemetry access and retention.

## Run locally

From the repository root, start the protocol server:

```powershell
$env:Foundry__ProjectEndpoint = "https://<account>.services.ai.azure.com/api/projects/<project>"
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = "<model-deployment>"
dotnet run --project .\dotnet\src\Chapter13.HumanInTheLoop.Hosted
```

The server listens on port 8088 by default. In another PowerShell terminal, use the same session ID for the approval round trip:

```powershell
$sessionId = "support-demo-001"
$first = Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType "application/json" `
  -Body '{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}'
$first

$requestId = $first.approval.request_id
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:8088/invocations?agent_session_id=$sessionId" `
  -ContentType "application/json" `
  -Body (@{ decision = "approve"; requestId = $requestId } | ConvertTo-Json)
```

Local Entra ID auth uses the signed-in developer identity. Configure a local user-secrets API key only for running the separate console project.

## Deploy and invoke

Use the service name `chapter13-human-in-the-loop` from the `dotnet` directory. Configure your own project and model as described in the shared hosted-agent guide; the committed manifest reuses its existing project service and must not be deployed with its sample project reference unchanged.

```powershell
Set-Location .\dotnet
$env:AZURE_DEV_USER_AGENT = "microsoft_foundry_skill"
azd deploy chapter13-human-in-the-loop --no-prompt
azd ai agent show chapter13-human-in-the-loop --output json
```

Before remote invocation, resolve the deployed agent's current `instance_identity.principal_id`, identify the actual model-hosting Foundry account ARM ID, and check effective role assignments including inherited access. If needed and authorized, grant **Cognitive Services OpenAI User** at that account scope to that runtime identity, allow for RBAC propagation, and recheck after redeployment. Deployment permissions and runtime inference access are separate.

The `InvocationsServer` exposes `POST /invocations`; the Foundry platform supplies invocation authentication and the user identity key. Save the following request as `support-request.json` and invoke it:

```powershell
@'
{"task":"My headphones still disconnect after troubleshooting. Escalate this complex issue.","customer":{"name":"Sam","tier":"premium","recentPurchases":["Wireless headphones"],"supportHistory":[]}}
'@ | Set-Content -NoNewline .\support-request.json
azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\support-request.json
```

Copy `approval.request_id` from the response into `approval.json` and invoke again with the same session:

```powershell
@'
{"decision":"approve","requestId":"<request_id from the pending response>"}
'@ | Set-Content -NoNewline .\approval.json
azd ai agent invoke chapter13-human-in-the-loop --protocol invocations --session-id support-demo -f .\approval.json
```

Change `"approve"` to `"reject"` to deny the escalation. When a response contains an `approvals` array, send an array of decisions as shown in the request contract. Deploy only this service. Hosting and model inference incur Azure charges. Remove only the reader-owned agent using Foundry's agent-scoped management controls; do not remove shared project resources.

## Packaging and extension

The service uses `dependencyResolution: bundled`, so azd publishes this host and its Chapter 13 and Shared project references before upload. Keep `.azure`, local environment files, user-secrets, and generated state out of source control and published output.

For production, replace the simulated ticket and escalation tools with least-privilege integrations, retain a human review checkpoint, record an auditable decision, and replace the sample file store with encrypted durable storage and distributed coordination.
