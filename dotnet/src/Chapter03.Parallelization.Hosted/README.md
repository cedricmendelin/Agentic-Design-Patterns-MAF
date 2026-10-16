# Chapter 03 - Hosted Parallelization Agent

This companion exposes the Chapter 03 MAF concurrent workflow through Foundry Agent Service's **Responses protocol**. It hosts the shared Chapter 03 logic; it is not a separate Foundry-native parallelization primitive. Version 1 was deployed and remotely verified on 2026-10-16.

Each call runs three independent model tasks and a synthesis call. Requests beginning with `research:` select the sustainable-technology researchers; requests beginning with `topic:` (or unprefixed requests) select summary, question-generation, and key-term workers. The input after the prefix is passed to every parallel worker. Each request builds a fresh workflow; no session history is persisted. The caller receives the completed synthesis, not intermediate console output or streamed branch tokens.

The sustainable-technology branch is not grounded by Google Search. Its prompts explicitly prohibit claims of live search or verified current findings. Add a supported retrieval/search integration before using this mode for current research.

## Prerequisites and authentication

- .NET 10 SDK, Azure CLI, Azure Developer CLI, and the `azure.ai.agents` extension.
- An existing Foundry project configured for hosted agents and a chat-completions model deployment.
- Authenticate Azure CLI and azd yourself in the target tenant. Deployment authentication is separate from the console demo's API key.
- The local developer identity and deployed agent's managed identity need model inference permissions on the Foundry account, for example **Cognitive Services OpenAI User**. The deploying identity also needs project/agent deployment permissions. Agent deployment does not necessarily grant inference access: use the deployed agent's `instance_identity.principal_id` from `azd ai agent show` for the role assignment and allow time for RBAC propagation. Do not put API keys in the hosted deployment.

The host reads shared .NET user-secrets `Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ModelDeployments`, and `Foundry:ApiKey`. Environment variables `Foundry__ProjectEndpoint`, `Foundry__ModelDeployment`, `Foundry__ModelDeployments__<alias>`, and `Foundry__ApiKey` override user-secrets. A blank or absent API key uses `DefaultAzureCredential` (developer identity locally, managed identity in Azure); a nonempty key uses the shared API-key client. Never commit or print keys.

If canonical settings are absent, the host accepts `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` from the Foundry runtime/azd. `AZURE_TENANT_ID` selects the developer's directory when needed. The committed `azure.yaml` selects `gpt-5-mini`; ensure that deployment exists in the selected project before deploying.

## Run locally

From the repository root, configure the same shared user-secrets as the console demo, then start the protocol server:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment>" --project .\dotnet\src\Shared
dotnet user-secrets set "Foundry:ApiKey" "<key>" --project .\dotnet\src\Shared
dotnet run --project .\dotnet\src\Chapter03.Parallelization.Hosted
```

For local Entra ID authentication, leave `Foundry:ApiKey` blank and sign in with your developer identity. Do not expose the local server publicly. The host listens on port 8088 by default.

After setting up the azd project, a local invocation can be sent from a second terminal:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'topic: How can cities reduce emissions?'
azd ai agent invoke chapter03-parallelization --local --protocol responses 'research: Compare sustainable technology options for a city.'
```

Stop the server after local invocation. Responses are stateless: follow-up requests do not inherit earlier turns.

## Initialize and deploy

`dotnet\azure.yaml` now contains both Chapter 02 and Chapter 03 hosted services and retains the existing Foundry project service. Do not overwrite the shared manifest or initialize from this directory as an overlapping template. Resolve the existing Foundry project ARM ID and deployment with your administrator/team before continuing; no Azure resources are created by this repository change.

Use the selected existing project and a model deployment already present in it. From `dotnet`, create/select an azd environment and initialize only this agent if needed; inspect the current environment and manifest first:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd env new <environment> --subscription '<subscription-id>' --location '<project-region>' --no-prompt
azd ai agent init --no-prompt --src .\src\Chapter03.Parallelization.Hosted --agent-name chapter03-parallelization --project-id '<project-arm-id>' --model-deployment '<existing-deployment>' --protocol responses --deploy-mode code --runtime dotnet_10 --entry-point Chapter03.Parallelization.Hosted.dll --dep-resolution bundled
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<existing-deployment>'
```

The chapter service already exists in `azure.yaml`; do not blindly rerun initialization or allow it to replace other service definitions. Verify any CLI changes and keep the Chapter 02 service and Foundry project service intact. If the selected Foundry project does not support hosted agents or the deployment identity lacks permissions, stop for administrator setup rather than creating a new project implicitly.

After local protocol invocation succeeds and the existing target is confirmed, deployment can be performed explicitly:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter03-parallelization --no-prompt
azd ai agent show --output json
azd ai agent invoke chapter03-parallelization --protocol responses 'topic: Explain the history of space exploration.'
```

Each deployment creates an immutable agent version. Remote inference incurs model usage charges: each request makes three concurrent worker calls and one synthesis call. Cleanup must target only this agent/version using Foundry's agent management workflow. Do not run `azd down` against the shared project or delete model deployments, Chapter 02, or unrelated agents.

### Bundled package contents

The host references the Chapter 03 console project, which references Shared and inherits central build/package files. Remote source-only build uploads would omit sibling references. The `dependencyResolution: bundled` configuration makes the azd agent extension publish the .NET project locally for `linux-x64` and upload its published output, including the referenced assemblies and runtime configuration next to `Chapter03.Parallelization.Hosted.dll`. No Docker image or ACR is required. The root `.agentignore` excludes `.azure`, environment files, secrets, and build artifacts; never add local user-secrets or connection strings to publish content.

## Telemetry and sensitive data

Each inner MAF agent is instrumented under `Chapter03.Parallelization`, and `EnableSensitiveData = true` is explicit. Prompts, caller messages, worker results, and model responses can be exported when an OTLP or Application Insights exporter is configured. Use non-sensitive sample data, restrict telemetry access and retention, and disable sensitive capture before handling confidential inputs. Tracing instrumentation is registered with the hosted `AgentHost`; it does not itself configure an exporter.

## Verification and deployment status

The companion is covered by deterministic tests that exercise the Responses endpoint using a scripted model client. The tests validate caller-input propagation and final-result output without making Azure requests. A real local Responses request was also verified against Foundry.

On 2026-10-16, `chapter03-parallelization` version 1 was deployed to the existing `mvp-playground-foundry-proj` project using the `routing-dev` azd environment and `gpt-5-mini`. The version reported `active`, and a remote request (`topic: Explain two practical ways cities can reduce emissions.`) returned a completed synthesis. The initial invocation failed with `401 PermissionDenied`; assigning **Cognitive Services OpenAI User** to the agent runtime identity at the Foundry account scope and allowing RBAC propagation resolved it. No project provisioning or Chapter 02 redeployment was performed.

The generated `smoke-core` evaluation completed against version 1 on 2026-10-16: **15 cases, 9 passed, 6 failed, 0 errored**. Per-item results are saved under `.foundry/results/routing-dev/`, with run references and failure clusters in `.foundry/agent-metadata.yaml`. Two failed cases recorded no response text or output items; the evaluation export contains no sample error explaining why. The other four failures were driven partly by generated rubric requirements for provenance maps, internal concurrency timestamps, and timeout-policy artifacts that this host does not expose; additional findings included malformed CSV and unsupported numerical claims. This is an exploratory baseline, not a production-quality acceptance result.

The generated `eval.yaml`, dataset, and rubric are available for review. From `dotnet`, a future run can be started with `azd ai agent eval run --agent chapter03-parallelization --environment routing-dev --no-prompt`. Review and align the rubric with the documented request/response contract before relying on its pass rate. Evaluation incurs additional model usage.

## Limitations

- Calls are isolated and stateless; conversation context is not persisted across requests.
- The protocol receives one final synthesis response. Intermediate results are available in traces, not streamed to callers.
- A failed parallel worker fails the request; no partial-results policy is applied.
- The sustainable-technology mode lacks live search/grounding and must not be treated as current research.
