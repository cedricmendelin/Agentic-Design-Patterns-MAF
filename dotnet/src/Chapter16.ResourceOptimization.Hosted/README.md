# Chapter 16 - Hosted Resource Optimization Agent

Expose the [Chapter 16 MAF workflow](../Chapter16.ResourceOptimization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host classifies each incoming request, selects a configured model deployment, and searches current information only when the classifier selects that route.

## Request contract and state

Send the user's question as the Responses `input` text. Each request is independent: the host uses only the latest user message, creates a fresh MAF workflow, and does not persist conversation history. The completed response reports the classification, selected deployment, provider-reported token usage, and actual model answer. The host buffers a completed workflow result before returning it; it does not stream intermediate router decisions.

When a request is classified as `internet_search`, both Google Custom Search settings must be present. Otherwise the request fails with an explicit configuration error rather than answering current-event questions from model memory. Search/network errors are propagated to the caller.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select a Foundry project and model, configure `azure.yaml`, and prepare authentication and permissions. Configure the model settings using shared user-secrets for local development or `Foundry__*` environment variables for deployment.

Optional model aliases provide per-route resource choices:

| Alias | Route |
|---|---|
| `fast` | `simple` |
| `reasoning` | `reasoning` |
| `quality` | `internet_search` |

An unset alias falls back to `Foundry:ModelDeployment`. If a single default deployment is used, all routes therefore share it.

For live web lookup, configure both values in shared user-secrets for local runs:

```powershell
dotnet user-secrets set "GoogleCustomSearch:ApiKey" "<google-custom-search-api-key>" --project .\dotnet\src\Shared
dotnet user-secrets set "GoogleCustomSearch:SearchEngineId" "<programmable-search-engine-id>" --project .\dotnet\src\Shared
```

For deployment, supply `GOOGLE_CUSTOM_SEARCH_API_KEY` and `GOOGLE_CSE_ID` as protected azd environment values when needed:

```powershell
Set-Location .\dotnet
azd env set GOOGLE_CUSTOM_SEARCH_API_KEY '<google-custom-search-api-key>'
azd env set GOOGLE_CSE_ID '<programmable-search-engine-id>'
```

Never commit either value. The hosted runtime uses managed identity for Foundry inference; Google Custom Search credentials are separate external-service credentials.

To use route-specific deployments in Azure, set any desired aliases in the azd environment before deployment. These names match the service environment entries in `azure.yaml`:

```powershell
azd env set FOUNDRY_MODEL_DEPLOYMENT_FAST '<fast-deployment-name>'
azd env set FOUNDRY_MODEL_DEPLOYMENT_REASONING '<reasoning-deployment-name>'
azd env set FOUNDRY_MODEL_DEPLOYMENT_QUALITY '<quality-deployment-name>'
```

Unset aliases fall back to `AZURE_AI_MODEL_DEPLOYMENT_NAME`.

## Run locally and invoke

From the repository root, start the local protocol endpoint:

```powershell
dotnet run --project .\dotnet\src\Chapter16.ResourceOptimization.Hosted
```

In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter16-resource-optimization --local --protocol responses 'What is the capital of Australia?'
azd ai agent invoke chapter16-resource-optimization --local --protocol responses 'When does the Australian Open 2026 start?'
```

The second request needs Google Custom Search configured.

## Deploy and invoke

After completing the shared setup, deploy only this service:

```powershell
Set-Location .\dotnet
azd deploy chapter16-resource-optimization --no-prompt
azd ai agent show chapter16-resource-optimization --output json
```

The code deployment bundles the host and referenced Chapter 16 and Shared projects. Then use the returned `instance_identity.principal_id` to check for effective **Cognitive Services OpenAI User** access at the actual model-hosting Foundry account scope. Assign that role to the runtime identity only when missing and authorized; allow for RBAC propagation and recheck the identity after redeployment. Deployment permissions and runtime inference permissions are separate.

Invoke the deployed agent after runtime access is ready:

```powershell
azd ai agent invoke chapter16-resource-optimization --protocol responses 'Explain why model routing can reduce cost.'
```

## Permissions, packaging, and cost

The deploying identity needs permission to deploy hosted agents to the selected Foundry project. The hosted agent needs model inference access at the model-hosting account scope and outbound access to Google Custom Search when that route is used. Google Search API and Foundry model calls may incur usage charges. The bundled deployment excludes user-secrets and local environment files; keep any runtime Google API values in protected deployment configuration.

When removing an experiment, use agent-scoped controls for the agent resources you own. Do not delete the shared project, its model deployments, or unrelated agents.

## Telemetry and sensitive data

MAF spans use `Chapter16.ResourceOptimization`. Sensitive-data capture is enabled for this demo, so prompts, search results, and model responses may be exported when an OpenTelemetry listener is configured. Use non-sensitive sample requests, restrict telemetry access and retention, and disable sensitive capture before handling confidential information.

## Pattern tradeoffs

Classification adds a model call, latency, and possible misrouting. A missing route-specific deployment silently uses the documented default deployment, so compare route quality and cost before relying on model tiers. Search results can be stale or incomplete, and external requests add cost and availability dependencies. Keep explicit budgets and authorization checks outside the model's classification decision.
