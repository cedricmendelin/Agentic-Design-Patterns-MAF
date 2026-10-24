# Chapter 20 — Hosted Prioritization

Expose the [Chapter 20 MAF prioritization workflow](../Chapter20.Prioritization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Each request contains one or more task descriptions and returns a validated priority queue.

## Request and response

Send one task description as plain text, or send a JSON object such as `{"tasks":["Urgent: restore sign-in","Review the release notes"]}` for a batch of up to 20 tasks. The agent assigns request-local IDs, asks the MAF priority proposer for one priority and assignee per task, validates each response, and returns the queue sorted by P0, P1, then P2. Tasks with equal priority retain arrival order. Available workers are `Worker A`, `Worker B`, and `Review Team`; the proposer recommends `Worker A` when no assignee is specified.

Requests are isolated: prior conversation messages and task state are not reused, and IDs restart for each request. The completed schedule is buffered into a single response after one structured model call per task. A malformed proposal is retried once, then returned as an explicit error. The service processes caller input rather than replaying the console's sample tasks.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select an existing Foundry project and model, configure `dotnet/azure.yaml`, and install azd agent tooling. The deploying identity needs permission to deploy hosted agents to the selected project. Model calls locally use shared user-secrets or a developer identity with model-inference access; Azure execution uses managed identity. Do not package API keys or local secrets.

The hosted runtime needs **Cognitive Services OpenAI User** (or verified equivalent inference access) on the model-hosting Foundry account. After deployment, resolve `instance_identity.principal_id`, check effective assignments including inherited grants, and assign the role at the model-hosting account scope only when authorized. Allow time for RBAC propagation and recheck the principal after redeployment.

## Run locally

From the repository root, start the Responses server:

```powershell
dotnet run --project .\dotnet\src\Chapter20.Prioritization.Hosted
```

In another terminal, from `dotnet`, send a caller-provided task batch:

```powershell
azd ai agent invoke chapter20-prioritization --local --protocol responses '{"tasks":["Urgent: restore sign-in for affected customers.","Review the marketing website content."]}'
```

Stop the local server with Ctrl+C after the invocation.

## Deploy and invoke

After completing shared project and model setup, run from `dotnet`:

```powershell
azd deploy chapter20-prioritization --no-prompt
azd ai agent show chapter20-prioritization --output json
```

Use the current agent's `instance_identity.principal_id` to verify or assign model inference access as described in the shared guide. Allow for role propagation, then invoke the Responses endpoint:

```powershell
azd ai agent invoke chapter20-prioritization --protocol responses '{"tasks":["Urgent: restore sign-in for affected customers.","Review the marketing website content."]}'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 20 and Shared projects together. No Docker image or container registry is needed. Deploy only this service with the service-scoped command.

## Telemetry and sensitive data

The hosted agent enables sensitive-data capture for traces of the model request and response. Task descriptions, priority proposals, and assignee details may be present in exported telemetry. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before processing confidential tasks.

## Costs and resource ownership

Each request uses one model call per task, with at most one retry for a malformed proposal, and is limited to 20 tasks. Hosted compute and model inference incur Azure charges. Remove only the hosted agent and versions you own using Foundry's agent-scoped management controls. Do not run `azd down` against a shared project or delete shared project infrastructure or model deployments.

## Pattern tradeoffs

Model proposals can misinterpret urgency or assignment, so queue order is deterministic only after proposal validation; it cannot make the proposal itself authoritative. FIFO tie-breaking is transparent but does not account for deadlines, dependencies, worker capacity, or service-level agreements. Add durable storage and deterministic policy checks when those constraints matter.
