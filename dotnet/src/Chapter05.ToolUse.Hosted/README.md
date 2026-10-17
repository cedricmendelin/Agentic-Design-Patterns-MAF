# Chapter 05 - Hosted Tool Use Agent

Expose the [Chapter 05 function-tool agent](../Chapter05.ToolUse/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF selects and invokes application-defined tools; Foundry hosts the agent and model.

## Request and response

The host accepts caller input with optional mode prefixes:

| Input | Tool |
|---|---|
| `search: <query>` | Deterministic sample information lookup |
| `calculate: <expression>` | Bounded arithmetic parser with factorial support |
| `stock: <ticker>` | Simulated stock lookup |

Search results and stock prices are sample data, not live information. The calculator evaluates supported arithmetic rather than executing generated code. The console chapter also offers separate Foundry-native web search, code interpreter, and file-search examples.

Each request creates a fresh tool-enabled agent without persisted conversation history and returns a buffered final response. Tool errors, provider failures, and cancellation propagate rather than producing successful-looking placeholders.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter05.ToolUse.Hosted
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter05-tool-use --local --protocol responses 'search: capital of France'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'calculate: (5 + 7) * 3'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'stock: AAPL'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter05-tool-use --no-prompt
azd ai agent show chapter05-tool-use --output json
```

Use the returned `instance_identity.principal_id` to configure runtime model-inference access as described in the shared guide. Allow time for role propagation, then send a request:

```powershell
azd ai agent invoke chapter05-tool-use --protocol responses 'calculate: 10 factorial'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 05 and Shared projects together. No Docker image or container registry is required.

## Telemetry and sensitive data

Agents emit spans under `Chapter05.ToolUse`. **Sensitive-data capture is enabled for this demo:** prompts, tool arguments, and responses may appear in configured traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

Model-selected tool names and arguments are untrusted input. Validate them at the tool boundary, scope credentials narrowly, and require deterministic authorization and approval for consequential writes.

Tool results are only as reliable and current as their data source. When replacing sample lookups with external services, define freshness, timeouts, error handling, and attribution explicitly. Treat retrieved content as data, not instructions that can authorize further actions.
