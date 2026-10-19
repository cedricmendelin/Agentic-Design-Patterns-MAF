# Chapter 21 — Hosted Exploration & Discovery

Expose the [Chapter 21 MAF proposal-and-review workflow](../Chapter21.ExplorationDiscovery/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Each request is an isolated, bounded research-proposal exercise; the Foundry Deep Research console variant is not hosted.

## Request and response

The last user message supplies a research topic of at most 4,000 characters. The host returns a structured exploration proposal and three reviewer assessments. A request runs for at most two review rounds and three minutes. The host buffers the completed result into one response and does not persist conversation history or proposal state across requests.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select an existing Foundry project and model deployment, configure `dotnet/azure.yaml`, and set up authentication.

For local execution, leave `Foundry:ApiKey` unset to use `DefaultAzureCredential` with your developer identity, or use the shared user-secrets setup. Azure execution uses managed identity and must not use an API key.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter21.ExplorationDiscovery.Hosted
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter21-exploration-discovery --local --protocol responses 'Explore whether adaptive batching can reduce inference cost without harming tail latency.'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter21-exploration-discovery --no-prompt
azd ai agent show chapter21-exploration-discovery --output json
```

Use the deployed agent's `instance_identity.principal_id` and the actual model-hosting Foundry account ARM ID to check effective inference permissions, including inherited role assignments. If **Cognitive Services OpenAI User** is missing, obtain authorization before assigning it to that runtime identity at the model-hosting account scope. Allow time for RBAC propagation and recheck access before invoking:

```powershell
azd ai agent invoke chapter21-exploration-discovery --protocol responses 'Explore whether adaptive batching can reduce inference cost without harming tail latency.'
```

The service uses bundled .NET 10 code deployment, publishing the host and its Chapter 21 and Shared project references together. It does not require Docker or a container registry. Re-resolve the runtime identity after redeployment because it can change.

## Permissions, costs, and resource ownership

The deploying identity needs permission to deploy hosted agents to the selected Foundry project. The hosted runtime separately needs **Cognitive Services OpenAI User** (or verified equivalent inference access) at the model-hosting account scope. Include a role assignment in deployment authorization if the runtime identity lacks that access; deployment permission alone does not authorize an RBAC change.

Each request uses at most eight model calls across two review rounds and has a three-minute time budget. Model usage, hosting, and telemetry may incur Azure charges. To remove an experiment, delete only the Chapter 21 agent/version you own using Foundry's agent-scoped controls; do not run `azd down` against a shared project or delete shared models or project infrastructure.

## Telemetry and sensitive data

The workflow enables sensitive-data capture in its MAF telemetry. Caller topics, proposals, reviewer prompts, and responses may appear in exported traces. Use non-sensitive inputs and restrict telemetry access and retention.

## Pattern tradeoffs

The generated proposal and its reviewers can share model biases, and agreement is not proof of novelty, feasibility, or truth. Use independent evidence and qualified human review before acting on a proposal. The two-round and three-minute limits bound work but can leave unresolved reviewer concerns.
