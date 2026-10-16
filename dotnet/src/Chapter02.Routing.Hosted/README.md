# Chapter 02 - Hosted Routing Agent

This companion exposes the existing Chapter 02 MAF workflow through the Foundry Agent Service **Responses protocol**. It is a deployment of the same custom routing code, not a separate Foundry-native routing primitive. No Prompt Chaining agent is included.

Each request runs the shared `RoutingWorkflow`: a coordinator selects BOOKER, INFO, or UNCLEAR, then the workflow returns a specialist answer or a deterministic clarification. The original request reaches the chosen specialist. Bookings are always simulated. Only the final answer is returned; console demo headers and router diagnostics are not included in the response.

## Prerequisites and authentication

- .NET 10 SDK, PowerShell 7, Azure CLI, Azure Developer CLI, and its `azure.ai.agents` extension.
- An existing Foundry project that supports hosted agents and an existing chat-completions model deployment.
- Authenticate Azure CLI and azd yourself in the target tenant. Deployment authentication is separate from the console demo's API key.
- The local developer identity and deployed agent's managed identity need model inference permissions on the Foundry account, for example **Cognitive Services OpenAI User**. The deploying identity also needs project/agent deployment permissions. Request any missing role assignment from an administrator; do not put API keys in the hosted deployment.

The host reads the shared .NET user-secrets `Foundry` section, including `ProjectEndpoint`, `ApiKey`, `ModelDeployment`, and the `ModelDeployments` alias dictionary. Environment variables such as `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, `Foundry__ModelDeployment`, and `Foundry__ModelDeployments__reasoning` override user-secrets. An empty or absent `ApiKey` uses `DefaultAzureCredential` (developer identity locally, managed identity in Azure); a nonempty key uses the existing shared API-key client. Never commit or print keys.

If canonical settings are absent, the host accepts `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` supplied by the Foundry runtime/azd. Canonical `Foundry` values take precedence over these fallback variables. `AZURE_TENANT_ID` selects the developer's directory when needed. The deployment manifest still selects `gpt-5-mini` for Azure; the local secrets default can independently select `gpt-5.4-mini`.

## Run locally

From the repository root:

```powershell
# Uses the shared Foundry user-secrets, with ModelDeployment as the default.
dotnet run --project .\dotnet\src\Chapter02.Routing.Hosted
dotnet run --project .\dotnet\src\Chapter02.Routing.Hosted -- --deployment reasoning
```

Use `dotnet user-secrets` with `--project .\dotnet\src\Shared` to manage the same secrets as the console demos. For example, `Foundry:ModelDeployments:reasoning` maps the `reasoning` alias to a deployed model; unknown aliases fail explicitly. A blank API key is valid for this host (the API-key-only console demos still require a key).

To override the default without editing secrets:

```powershell
$env:Foundry__ModelDeployment = 'gpt-5.4-mini'
dotnet run --project .\dotnet\src\Chapter02.Routing.Hosted
```

The official `AgentHost` configures the protocol, health checks, and port (default 8088). Local traffic can use HTTP; use the authenticated Foundry endpoint for remote access. Do not expose the local server publicly.

Once the azd project is initialized, you can instead run it through azd from `dotnet`:

```powershell
azd ai agent run chapter02-routing --no-client
```

In another terminal in `dotnet`, invoke the local server:

```powershell
azd ai agent invoke chapter02-routing --local --protocol responses 'Book me a hotel in Paris.'
```

Stop the local server before deploying. Additional representative requests are `What is the highest mountain in the world?` and `Can you help me with this?`.

## Telemetry and sensitive data

The host instruments the shared chat client and each executed inner agent with OpenTelemetry under the `Chapter02.Routing` activity source. It registers that source with the existing `AgentHost` tracing provider rather than creating a second provider. BOOKER and INFO requests emit two named agent spans (`CoordinatorRouter`, then `Booker` or `Info`), each containing its own child model-call span. UNCLEAR emits only the router agent and its model call.

**`EnableSensitiveData = true` is enabled explicitly for this demo. Prompts, user messages, router decisions, and model responses can be stored in telemetry.** Use non-sensitive sample data only, restrict access and retention on the telemetry backend, and disable the setting on both the chat-client and agent instrumentation before using confidential or production data. Content can appear at both levels; a parent agent span and its child model span represent the same call, not two different calls.

The hosting library configures exporters automatically: `OTEL_EXPORTER_OTLP_ENDPOINT` selects an OTLP collector (for example, a local dashboard), while `APPLICATIONINSIGHTS_CONNECTION_STRING` enables Azure Monitor export to the Application Insights resource associated with the Foundry project. Instrumentation alone does not select a backend. If neither is configured by your environment/runtime, configure one before expecting these spans in a trace viewer. Never commit connection strings.

Restart the local host after rebuilding, and redeploy to enable this instrumentation in Foundry. Existing processes and previously deployed versions do not pick up source edits; historical requests cannot acquire the missing internal spans retroactively.

### View local traces with Aspire Dashboard

The standalone azd Agent Inspector can send chat requests without implementing the tracing collector/query operations used by its Tracing tab. Enabling sensitive data does not provide that missing backend. Use Inspector for chat and a separate OTLP viewer for local spans; this does not require changing the deployed agent or provisioning Azure resources.

With the Aspire CLI installed (`aspire dashboard run` is available in version 13.3.0), start a standalone dashboard in one terminal:

```powershell
aspire dashboard run --frontend-url http://127.0.0.1:18888 --otlp-grpc-url http://127.0.0.1:4317 --otlp-http-url http://127.0.0.1:4318
```

Open the login URL printed by Aspire. Keep browser authentication enabled because prompts and responses are captured. All endpoints above bind to loopback; do not expose the collector or dashboard on a public interface. See the [standalone dashboard guide](https://aspire.dev/dashboard/standalone/) for installation and configuration.

In a second terminal, from `dotnet`, configure the exporter **before** starting the agent:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://127.0.0.1:4317'
$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'grpc'
$env:OTEL_SERVICE_NAME = 'chapter02-routing-local'
azd ai agent run chapter02-routing
```

The command opens Inspector for chat. Send `Book me a hotel in Paris.`, then open **Traces** in Aspire Dashboard at `http://127.0.0.1:18888`. The request contains `CoordinatorRouter` and `Booker` agent spans, each with a child `chat` span containing the prompt and response. Allow a few seconds for batched export. An INFO request has the router and Info specialist; an UNCLEAR request has only the router call.

For direct `dotnet run`, set the same variables in that process's terminal before launching the hosted project. A host started from Visual Studio or another terminal does not inherit these changes. Stop any old agent first and restart it with a rebuild; restarting Inspector alone does not reconfigure or rebuild the agent. Do not use `azd env set` or put this localhost endpoint in `azure.yaml`, since that would also affect deployment configuration.

Stop the agent and dashboard with Ctrl+C in their respective terminals. The environment variables above apply only to the agent terminal; close it when finished. The standalone dashboard's default in-memory history is lost when it stops.

## Initialize and deploy

Follow the current Microsoft Foundry skill for environment verification and azd initialization. The committed manifest is `dotnet\azure.yaml`; it contains the Routing agent and a reference to the existing Foundry project. It does not declare new model deployments.

From `dotnet`, create an azd environment if none exists, then initialize the existing source against an **existing Azure-resolved project ARM ID**. Do not adopt `.\azure.yaml` as a template from its own directory: the CLI attempts to copy the project into an overlapping source directory.

```powershell
azd env new routing-dev --subscription '<subscription-id>' --location '<project-region>' --no-prompt
azd ai agent init --no-prompt --force --src .\src\Chapter02.Routing.Hosted --agent-name chapter02-routing --project-id '<project-arm-id>' --model-deployment '<existing-deployment>' --protocol responses --deploy-mode code --runtime dotnet_10 --entry-point Chapter02.Routing.Hosted.dll --dep-resolution bundled
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-deployment>'
```

Initialization is one-time for an environment: inspect existing state rather than blindly rerunning it or creating a second suffixed service. The `--force` option replaces the existing agent service configuration; review the resulting manifest and preserve the startup command, description, and intended model. The current extension may warn that it cannot parse `TargetFramework` because this repository inherits it from `Directory.Build.props`; the bundled publish resolves it correctly. Never run `azd down` against a shared Foundry project.

Before deployment, ensure the active azd environment points to the intended project. No `azd provision` is necessary when the existing project already supports hosted agents and no infrastructure changes are required. Unsupported regions, missing hosting setup, or insufficient permissions require explicit resolution rather than silently creating another project.

```powershell
azd deploy chapter02-routing --no-prompt
azd ai agent show --output json
azd ai agent invoke chapter02-routing --protocol responses 'Book me a hotel in Paris.'
```

Each deploy creates an immutable agent version. The service key and agent name are both `chapter02-routing`. Deployment and invocation incur Azure/model charges.

### Why bundled code deployment?

The host references the Routing console project, which references Shared and inherits central build/package files. Uploading only the host's source folder for a remote build would omit those dependencies. For .NET `dependencyResolution: bundled`, the azd agent extension runs `dotnet publish` locally for `linux-x64` and ZIPs the published output. This resolves the sibling references and central build/package files before uploading, with all required assemblies and runtime configuration beside `Chapter02.Routing.Hosted.dll`. No custom packaging hook, Docker image, or ACR is required.

You can inspect an equivalent local publish from `dotnet`:

```powershell
dotnet publish .\src\Chapter02.Routing.Hosted -c Release -r linux-x64 --self-contained false -o .\publish\chapter02-routing
```

The publish directory and `.azure` environment state are ignored by Git. The ZIP contains published application artifacts, not the source repository or local user-secrets. Do not add environment files or credentials as publish content.

## Verification and deployment status

The approved target for this change is `mvp-playground-foundry-proj` in the Visual Studio Enterprise Subscription - MVP subscription, using the existing `gpt-5-mini` deployment. The model is selected in `azure.yaml`, not hardcoded in host code.

Deployment succeeded in the local azd environment `routing-dev`. Foundry reports `chapter02-routing` version `2` as **active**, with the Responses protocol, bundled .NET 10 runtime, and `gpt-5-mini`. This version includes the sensitive-content telemetry instrumentation described above. A version-pinned remote booking request succeeded; telemetry ingestion and visibility in the Foundry trace viewer have not been independently verified.

The first remote request failed in `RouterExecutor` with HTTP 401 `PermissionDenied`: the agent's managed identity, principal `a19063d7-ece2-468e-887c-97f809809530`, lacked model-inference access. Assigning **Cognitive Services OpenAI User** on the `mvp-playground-foundry` account resolved it after Azure RBAC propagation. A fresh hosted session successfully answered `Book me a hotel in Paris.` with a simulated booking confirmation and explicitly stated that no real reservation was made. The deployed workflow is operationally verified.

`azd ai agent doctor` passed all 11 applicable checks; it did not detect the initial runtime inference-permission failure. Always inspect the invocation result as well as the active version status. Newly assigned inference roles may take several minutes to propagate; retry in a fresh hosted session before changing permissions again. Optional evaluation-suite generation remains deferred; do not interpret the existing console live smoke tests as hosted-agent deployment coverage.

## Limitations and cleanup

- Requests are independent and route the latest user message. Session serialization intentionally stores no conversation history; follow-ups requiring earlier context are not supported.
- Router decisions are model judgments; unknown route tokens retain the original demo's clarification fallback.
- Streaming callers receive one completed final result rather than intermediate model tokens.
- Cancellation propagates through both workflow and model calls. Failed workflows fail the request rather than returning a success-shaped placeholder.
- The hosted server adds required hosting infrastructure, not new booking tools or a replacement implementation of the routing pattern.
- For cleanup, stop/delete only this agent's explicitly identified deployment/version using the current Foundry management workflow. Do not delete the shared project, its models, or unrelated agents. Local published output can be regenerated.
