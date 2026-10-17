# Chapter 03 - Hosted Parallelization Agent

Expose the [Chapter 03 concurrent workflow](../Chapter03.Parallelization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the fan-out/fan-in orchestration; Foundry hosts the agent and model.

## Request and response

Each request runs three independent workers followed by a synthesis call:

| Input | Workers |
|---|---|
| `topic: <text>` or unprefixed text | Summary, question generation, and key terms |
| `research: <text>` | Renewable energy, electric vehicles, and carbon capture |

The text after the prefix reaches every worker. The research mode uses model knowledge, not live search; connect a supported retrieval tool when adapting it for current research.

Each request creates a fresh workflow without persisted conversation history. The caller receives a completed synthesis rather than intermediate branch output. Workers run concurrently using buffered model completions; a failed worker fails the request rather than producing a partial synthesis.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter03.Parallelization.Hosted
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter03-parallelization --local --protocol responses 'topic: How can cities reduce emissions?'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'research: Compare sustainable technology options for a city.'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter03-parallelization --no-prompt
azd ai agent show chapter03-parallelization --output json
```

Use the returned `instance_identity.principal_id` to configure runtime model-inference access as described in the shared guide. Allow time for role propagation, then send a request:

```powershell
azd ai agent invoke chapter03-parallelization --protocol responses 'topic: Explain the history of space exploration.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 03 and Shared projects together. No Docker image or container registry is required.

## Telemetry and sensitive data

Inner agents emit spans under `Chapter03.Parallelization`. **Sensitive-data capture is enabled for this demo:** prompts, worker results, and responses may be exported when an OTLP or Application Insights exporter is configured. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

Parallel work reduces latency only when subtasks are independent and the model endpoint can handle concurrent calls. Rate limits and the slowest branch can dominate response time. Three workers plus synthesis also increase model usage compared with a single prompt.

Fan-in requires a deliberate failure policy and synthesis criteria. When extending the pattern, decide whether to fail the request, retry a branch, or return explicitly labeled partial results; preserve attribution so the synthesis does not conceal conflicting or unsupported findings.
