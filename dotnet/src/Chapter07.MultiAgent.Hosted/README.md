# Chapter 07 - Hosted Multi-Agent Agent

Expose the [Chapter 07 multi-agent scenarios](../Chapter07.MultiAgent/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host reuses the MAF orchestration and accepts one caller-provided scenario per request.

This companion is one hosted process with in-process MAF agents. For independent specialist services and an orchestrator making remote calls, see the [distributed crew companion](../Chapter07.MultiAgent.Distributed/README.md).

## Request and response

Send a user message containing `<scenario>: <task>` where the scenario is `sequential`, `parallel`, `loop`, `coordinator`, `agent-tool`, or `crew`. A message without a recognized scenario prefix runs `crew`. The response is the final result from that scenario.

Each request constructs a fresh run and does not persist conversation history. The final result is buffered until the selected workflow completes. Agent spans use the `Chapter07.MultiAgent` telemetry source, with sensitive-data capture enabled; caller text and intermediate results can appear in exported traces.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

For `azd ai agent run`, make sure the local azd environment or the service's untracked `.env` file supplies `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME`. Keep those values aligned with the Foundry settings read by the host.

## Run locally

From `dotnet`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent run chapter07-multi-agent --no-client
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter07-multi-agent --local --protocol responses 'parallel: Analyze the impact of an extreme heat event.'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter07-multi-agent --no-prompt
azd ai agent show chapter07-multi-agent --output json
```

Use the returned `instance_identity.principal_id` to verify model-inference access as described in the shared guide. The hosted runtime needs **Cognitive Services OpenAI User** (or equivalent verified inference access) at the actual model-hosting Foundry account scope. Allow time for RBAC propagation, then invoke a representative request:

```powershell
azd ai agent invoke chapter07-multi-agent --protocol responses 'crew: Explain how teams can use AI agents to analyze a product launch.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 07 and Shared projects together. No Docker image or container registry is required. Deploy only the selected service, and remove only agent resources that you own; do not tear down shared project infrastructure.

## Telemetry and sensitive data

Agents emit spans under `Chapter07.MultiAgent`. **Sensitive-data capture is enabled for this demo:** caller tasks, specialist outputs, and final responses may appear in exported traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

Multi-agent orchestration can improve decomposition when responsibilities are distinct, but increases latency, token consumption, and the number of handoffs that can lose context or propagate errors. Parallel specialist agreement is not evidence of correctness. Keep loops bounded and use deterministic checks for mandatory policy and completion rules.
