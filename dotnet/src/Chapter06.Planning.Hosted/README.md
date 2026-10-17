# Chapter 06 - Hosted Planning Agent

Expose the [Chapter 06 plan-and-write workflow](../Chapter06.Planning/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF orchestrates the planner and writer; Foundry hosts the agent and model.

## Request and response

The last user message supplies the topic. A planner produces a structured 3-6 step plan, deterministic code checks its shape, and a writer uses it to produce a summary. The response contains both the plan and summary.

Each request creates fresh agents without persisted conversation history. The final response is buffered until both model calls finish; configured traces expose the intermediate stages.

This companion hosts the plan-and-write scenario. For citation-backed web research, see the separate Foundry Deep Research scenario in the console chapter.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter06.Planning.Hosted
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter06-planning --local --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter06-planning --no-prompt
azd ai agent show chapter06-planning --output json
```

Use the returned `instance_identity.principal_id` to configure runtime model-inference access as described in the shared guide. Allow time for role propagation, then send a request:

```powershell
azd ai agent invoke chapter06-planning --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 06 and Shared projects together. No Docker image or container registry is required.

## Telemetry and sensitive data

Agents emit spans under `Chapter06.Planning`. **Sensitive-data capture is enabled for this demo:** caller topics, plans, and responses may appear in exported traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

A well-formed plan can still omit constraints, choose unnecessary steps, or rely on incorrect facts. Validate domain requirements separately from plan structure and ground factual output in appropriate evidence.

Planning adds latency and model usage before execution. Use deterministic workflows for mandatory business rules, permissions, and state transitions; treat a generated plan as a proposal rather than authorization to act.
