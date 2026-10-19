# Chapter 15 Hosted A2A Agent

This companion exposes the Chapter 15 MAF agent through the A2A protocol. It accepts text requests, supports synchronous and server-sent-event streaming responses, and publishes an agent card. The service uses a fresh, non-persistent agent run for each request; it does not retain conversation history between requests.

## Request contract

- Input: A2A text messages (`text/plain`).
- Output: A2A text messages (`text/plain`).
- Bindings: HTTP+JSON and JSON-RPC; streaming is available through the A2A HTTP+JSON streaming operation.
- Card: standard `/.well-known/agent-card.json` with the `weather-guidance` skill.
- The model has no live weather, exchange-rate, or calendar integration. It must not claim current observations or completed actions.

## Local run

Prerequisites are the .NET 10 SDK and a Foundry chat-model deployment. Configure the shared API-key settings as described in [dotnet/README.md](../../README.md#configure-foundry-user-secrets).

The local A2A listener does not authenticate callers and AgentHost binds to all interfaces. Run it only on a trusted machine/network, and use firewall controls if needed.

Start the server:

```powershell
$env:PORT = '5090'
$env:A2A_PUBLIC_URL = 'http://localhost:5090/'
dotnet run --project .\dotnet\src\Chapter15.A2A.Hosted
```

In a second terminal, discover the card, send the notebook's synchronous JSON-RPC example, and stream the second example over HTTP+JSON:

```powershell
dotnet run --project .\dotnet\src\Chapter15.A2A -- all http://localhost:5090/
```

The client also accepts `card`, `sync`, or `stream` instead of `all`. Supply a custom request after the URL to replace that mode's sample text. Foundry's AgentHost reads its listen port from `PORT` and defaults to `8088`.

## Foundry deployment

The service is registered as `chapter15-a2a` in `dotnet/azure.yaml` and reuses the existing `mvp-playground-foundry-proj` service reference. Follow the [shared hosted setup](../../README.md#configure-and-deploy-hosted-agents) to select the reader-owned azd environment and configure its Foundry project endpoint and ARM ID. Replace the sample project reference with the selected project and configure an existing compatible chat-model deployment. The deploying identity needs permission to deploy hosted agents to that project; callers need permission to invoke the agent. The service runs as a .NET 10 hosted agent and uses bundled deployment so its Chapter 15 and Shared project references are included.

From `dotnet`, select the reader's existing azd environment and model, then deploy only this service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd env select '<existing-environment>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-model-deployment>'
azd deploy chapter15-a2a --no-prompt
azd ai agent show chapter15-a2a --output json
azd ai agent invoke chapter15-a2a --protocol a2a --new-session --new-conversation "What is the weather in Paris?"
```

The Foundry agent card describes the `weather-guidance` skill. For a deployed agent, use the Foundry-managed A2A endpoint and card; invoke it with the `azd ai agent invoke --protocol a2a` command above. The console client is for servers that publish the standard `/.well-known/agent-card.json` discovery URL. Foundry controls access to its hosted endpoint; do not expose the local server directly to untrusted networks without configuring caller authentication and authorization.

## Runtime access, cost, and cleanup

Hosted model calls use the service's managed identity, not an API key. After deployment, resolve the active `instance_identity.principal_id` and actual model-hosting Foundry account ARM ID. Check effective role assignments, including inherited grants, and ensure **Cognitive Services OpenAI User** (or verified equivalent inference access) is granted at that model-hosting account scope. Obtain RBAC-write approval before making a missing assignment, allow propagation, and recheck the runtime identity after redeployment. Deployment and runtime inference permissions are separate.

Each request incurs a model call and hosted-agent runtime cost. Sensitive MAF telemetry is enabled for this demo; prompts and responses can appear in traces, so restrict telemetry access and retention before using confidential inputs.

Remove only reader-owned experimental agents through Foundry's agent-scoped controls. Do not run `azd down` or remove a shared project or model.
