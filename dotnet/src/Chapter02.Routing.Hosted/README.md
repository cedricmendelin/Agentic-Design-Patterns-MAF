# Chapter 02 - Hosted Routing Agent

Expose the [Chapter 02 routing workflow](../Chapter02.Routing/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the routing orchestration; Foundry hosts the agent and model.

## Request and response

A coordinator selects BOOKER, INFO, or UNCLEAR. The original request reaches the chosen specialist, or the workflow returns a clarification. Bookings are simulated; no reservations are made.

The caller receives the final answer without console headers or router diagnostics. Each request uses the latest user message independently, with no persisted conversation history. Responses are buffered until the workflow completes.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter02.Routing.Hosted
# Optional: select an alias from Foundry:ModelDeployments.
dotnet run --project .\dotnet\src\Chapter02.Routing.Hosted -- --deployment reasoning
```

Run only one command at a time. The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter02-routing --local --protocol responses 'Book me a hotel in Paris.'
azd ai agent invoke chapter02-routing --local --protocol responses 'What is the highest mountain in the world?'
azd ai agent invoke chapter02-routing --local --protocol responses 'Can you help me with this?'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter02-routing --no-prompt
azd ai agent show chapter02-routing --output json
```

Use the returned `instance_identity.principal_id` to configure runtime model-inference access as described in the shared guide. Allow time for role propagation, then send a request:

```powershell
azd ai agent invoke chapter02-routing --protocol responses 'Book me a hotel in Paris.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 02 and Shared projects together. No Docker image or container registry is required.

## Telemetry and sensitive data

The `Chapter02.Routing` activity source records `CoordinatorRouter` and the selected `Booker` or `Info` agent, with child model-call spans. UNCLEAR requests run only the coordinator. Instrumentation is registered with the host's tracing provider.

**Sensitive-data capture is enabled for this demo.** Prompts, user messages, routing decisions, and responses may appear in traces. Use non-sensitive sample data, restrict telemetry access and retention, and disable sensitive capture on both chat-client and agent instrumentation before handling confidential data.

`OTEL_EXPORTER_OTLP_ENDPOINT` selects an OTLP collector; `APPLICATIONINSIGHTS_CONNECTION_STRING` enables Azure Monitor export. Configure an exporter to view spans, and keep connection strings out of source control.

### View local traces with Aspire Dashboard

Use Agent Inspector for chat and an OTLP viewer for local spans. With the Aspire CLI installed, start a standalone dashboard:

```powershell
aspire dashboard run --frontend-url http://127.0.0.1:18888 --otlp-grpc-url http://127.0.0.1:4317 --otlp-http-url http://127.0.0.1:4318
```

Open the login URL printed by Aspire and keep authentication enabled. See the [standalone dashboard guide](https://aspire.dev/dashboard/standalone/) for installation and configuration.

In a second terminal, from `dotnet`, set the exporter before starting the agent:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://127.0.0.1:4317'
$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'grpc'
$env:OTEL_SERVICE_NAME = 'chapter02-routing-local'
azd ai agent run chapter02-routing
```

Send a booking request in Inspector, then open **Traces** at `http://127.0.0.1:18888`. Allow a few seconds for batched export. For direct `dotnet run`, set the same variables in the terminal that starts the host. Keep these loopback settings local rather than adding them to `azure.yaml` or azd environment state.

Stop the agent and dashboard with Ctrl+C in their respective terminals. The dashboard's default in-memory history is lost when it stops.

## Pattern tradeoffs

Routing decisions are model judgments and can misclassify ambiguous requests. Use explicit route contracts, a clarification path, and representative labeled examples when adapting the pattern to a new domain. Put authorization and consequential actions behind deterministic controls rather than trusting the selected route.
