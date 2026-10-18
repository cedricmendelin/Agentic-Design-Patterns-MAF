# Chapter 09 — Hosted Learning and Adaptation

Expose the [Chapter 09 MAF prompt adaptation workflow](../Chapter09.LearningAdaptation/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The service scores a caller's starting instructions against a fixed benchmark and returns a revised prompt for review.

## Request and response

Send the initial prompt instructions as the last user message, up to 4,000 characters. Each request runs one bounded adaptation iteration against three support-policy cases and returns the baseline score, best score, whether the prompt improved, and the best instructions for review. The host processes caller input; it does not replay a saved sample batch.

Requests have fresh, isolated state. Conversation history is not used to seed another run, no candidate is written to disk or deployed, and streaming buffers the completed result until the workflow finishes. The response is a proposal, not approval to replace a live prompt.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select an existing Foundry project and model, configure `dotnet/azure.yaml`, and install azd agent tooling. Model calls locally use a configured API key or developer identity; Azure execution uses managed identity. Keep credentials out of the deployment package.

The runtime identity needs **Cognitive Services OpenAI User** (or verified equivalent inference access) on the model-hosting Foundry account. After deployment, resolve `instance_identity.principal_id`, check effective assignments including inherited grants, assign the role at the model-hosting account scope only when authorized, and allow time for RBAC propagation. Recheck the principal after redeployment.

## Run locally

From the repository root, start the Responses server:

```powershell
dotnet run --project .\dotnet\src\Chapter09.LearningAdaptation.Hosted
```

In another terminal, from `dotnet`, send caller-supplied instructions:

```powershell
azd ai agent invoke chapter09-learning-adaptation --local --protocol responses 'You are a helpful support assistant. Answer customer questions briefly.'
```

The response is buffered until the baseline, curator, and candidate evaluation calls complete. Stop the server with Ctrl+C.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter09-learning-adaptation --no-prompt
azd ai agent show chapter09-learning-adaptation --output json
```

Use the current agent's `instance_identity.principal_id` to verify or assign model inference access as described in the shared guide. Allow for role propagation, then invoke the request/response endpoint:

```powershell
azd ai agent invoke chapter09-learning-adaptation --protocol responses 'You are a helpful support assistant. Answer customer questions briefly.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 09 and Shared projects together. No Docker image or container registry is required. Deploy only this service with the service-scoped command above.

## Telemetry and sensitive data

The hosted agent enables sensitive-data capture for tracing the candidate and curator calls. Caller instructions, benchmark inputs, model outputs, and the proposed prompt may be present in exported traces. Use non-sensitive prompts, restrict telemetry access and retention, and disable sensitive capture before processing confidential instructions.

## Costs and resource ownership

Each request evaluates up to three baseline cases, uses one curator call, and evaluates up to three candidate cases. Model usage grows with the number and length of requests. Hosted compute and inference incur Azure charges.

Remove only the hosted agent and versions that you own using Foundry's agent-scoped management controls. Do not run `azd down` against a shared project or remove project infrastructure or model deployments.

## Pattern tradeoffs

The fixed phrase evaluator is transparent and inexpensive but does not measure full semantic correctness; a candidate can satisfy required phrases while still giving a poor answer. The prompt is not automatically adopted, and a proposal that improves these benchmark cases still needs representative regression checks, human review, and a controlled rollout.
