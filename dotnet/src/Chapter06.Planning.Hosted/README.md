# Chapter 06 - Hosted Planning Agent

This companion exposes the Chapter 06 plan-and-write MAF workflow through Foundry Agent Service's **Responses protocol**. It hosts the shared planner/writer implementation; it is not a separate implementation of Foundry Deep Research.

Each caller request is processed independently. The agent uses the last user message as its topic, creates fresh planner/writer agents, validates a 3-6 step plan, and returns the plan with a summary. Conversation history is not persisted. The response is buffered until both model calls finish; intermediate stages are available in configured traces, not streamed to the caller.

Foundry Deep Research is intentionally not hosted here: it runs as a long-running background Responses task. Supporting it would require a durable job store, a polling/status contract, and explicit timeout and cancellation behavior rather than holding a normal agent request open.

## Prerequisites and authentication

- .NET 10 SDK, Azure CLI, Azure Developer CLI, and the `azure.ai.agents` extension.
- A Foundry project and a chat-model deployment.
- Local invocation uses the developer's Entra identity or shared user-secrets. Azure execution uses the hosted agent's managed identity, not an API key.
- The deploying identity needs permission to deploy an agent. The runtime identity needs **Cognitive Services OpenAI User** on the model-hosting Foundry account. These are separate permissions.
- Before remote invocation, resolve `instance_identity.principal_id` with `azd ai agent show chapter06-planning --output json`, discover the actual model-hosting account ARM ID, and check effective/inherited role assignments. Obtain explicit authorization before assigning the runtime inference role; do not grant it to the deploying user, project identity, or agent blueprint.

The host reads `Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ModelDeployments`, and `Foundry:ApiKey` from the shared user-secrets or matching `Foundry__*` environment variables. Foundry runtime values `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are supported fallbacks. Leave the API key blank in Azure so `DefaultAzureCredential` uses managed identity. Never commit or print keys.

The existing `azure.yaml` adds only the `chapter06-planning` service and uses the existing shared Foundry project service. Its model name is set to `gpt-5-mini`; verify that deployment exists in the selected project before deployment. No infrastructure provisioning is included.

## Run locally

Configure shared user-secrets as described in [the .NET setup guide](../../README.md), then start the local protocol endpoint:

```powershell
dotnet run --project .\dotnet\src\Chapter06.Planning.Hosted
```

In a second terminal, from the `dotnet` directory, invoke it locally:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter06-planning --local --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
```

The host listens on port 8088 by default. Keep the local endpoint private and stop the server after the invocation.

## Deploy and invoke

Deployment is an Azure write and is **not authorized** by creating these artifacts. Before deploying, confirm the Foundry project and model, verify the deployment identity's permissions, and obtain explicit deployment approval that includes any required runtime inference-role assignment. If authorized, deploy only the Chapter 06 service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter06-planning --no-prompt
azd ai agent show chapter06-planning --output json
```

Resolve the current `instance_identity.principal_id` and actual model-hosting account scope after deployment. Check inherited roles; if the runtime principal lacks equivalent inference access, obtain authorization before assigning **Cognitive Services OpenAI User** at that account scope. Allow propagation and verify a representative remote Responses request. An active version alone is not proof of successful model inference. Recheck after redeployment because the runtime identity can change.

Agent deployment creates a hosted version; model usage incurs charges. For cleanup, remove only the `chapter06-planning` agent/version using the agent-scoped workflow. Do not delete the shared project, model deployment, or unrelated hosted services. Evaluation setup or execution is a separate decision; neither is part of these source artifacts.

## Packaging, telemetry, and limitations

`dependencyResolution: bundled` includes the hosted project, Chapter 06 workflow, Shared project, and runtime dependencies in the .NET 10 package. The host's `.agentignore` excludes `.azure`, environment files, user-secrets, and build output. No container registry or new Azure infrastructure is added.

The MAF agents emit tracing under `Chapter06.Planning`, and sensitive-data capture is enabled for debugging and tracing. Prompts, caller topics, plans, and model responses may be present in exported traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential requests.

The host has no durable state, and its final response is buffered rather than streamed. Deep Research remains available only in the console variant because its asynchronous work needs a durable job and polling protocol.

## Verification status

The hosted agent is active as `chapter06-planning:1` in the existing Foundry project and uses the verified `gpt-5-mini` deployment. Its runtime managed identity was granted **Cognitive Services OpenAI User** at the model-hosting Foundry account scope after confirming no inherited inference role was present. After role propagation, a remote Responses invocation successfully returned a five-step plan and summary for a robotics reinforcement-learning prompt.

This is a point-in-time verification; check the active version and runtime identity again after redeployment. No evaluation suite was generated and no evaluation was run.
