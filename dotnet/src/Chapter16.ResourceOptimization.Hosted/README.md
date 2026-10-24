# Chapter 16 - Hosted Resource Optimization Agent

Expose the [Chapter 16 MAF workflow](../Chapter16.ResourceOptimization/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host classifies each incoming request, selects a configured model deployment, and searches current information only when the classifier selects that route.

## Request contract and state

Send the user's question as the Responses `input` text. Each request is independent: the host uses only the latest user message, creates a fresh MAF workflow, and does not persist conversation history. The completed response reports the classification, selected deployment, provider-reported token usage, and actual model answer. The host buffers a completed workflow result before returning it; it does not stream intermediate router decisions.

When a request is classified as `internet_search`, the quality-route agent uses Foundry's built-in Bing-backed `web_search` tool through the project Responses API. Source URLs from citation annotations are included in the completed answer. No Google API key, search engine ID, Bing resource, or project connection is required. Search/network errors are propagated to the caller rather than falling back to model memory.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select a Foundry project and model, configure `azure.yaml`, and prepare authentication and permissions. Configure the model settings using shared user-secrets for local development or `Foundry__*` environment variables for deployment.

Optional model aliases provide per-route resource choices:

| Alias | Route |
|---|---|
| `fast` | `simple` |
| `reasoning` | `reasoning` |
| `quality` | `internet_search` |

An unset alias falls back to `Foundry:ModelDeployment`. If a single default deployment is used, all routes therefore share it.

For live web lookup, use a quality-route model deployment that supports Foundry `web_search` and ensure web search is permitted by the subscription administrator. The project Responses API uses `DefaultAzureCredential`: sign in with Azure CLI for local runs; the hosted runtime uses its managed identity in Azure. A model API key alone is not sufficient for this route. The console demo retains Google Custom Search solely to match the original notebook; those credentials are not used by this host.

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

The second request exercises Foundry web search using Azure credentials, without external search credentials.

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

The deploying identity needs permission to deploy hosted agents to the selected Foundry project. The hosted agent needs model inference access at the model-hosting account scope, including Responses API access for web search. Foundry model calls and Bing-backed web search incur usage charges. The bundled deployment excludes user-secrets and local environment files.

Web search is a preview feature governed by the [Grounding with Bing terms](https://www.microsoft.com/bing/apis/grounding-legal-enterprise) and [Microsoft Privacy Statement](https://go.microsoft.com/fwlink/?LinkId=521839). The Microsoft Data Protection Addendum does not apply to data sent to Bing grounding, and data can leave compliance and geographic boundaries. Use non-sensitive sample prompts. See the [Foundry web search documentation](https://learn.microsoft.com/azure/ai-foundry/agents/how-to/tools/web-search?view=foundry) for administrator controls and supported models.

When removing an experiment, use agent-scoped controls for the agent resources you own. Do not delete the shared project, its model deployments, or unrelated agents.

## Telemetry and sensitive data

MAF spans use `Chapter16.ResourceOptimization`. Sensitive-data capture is enabled for this demo, so prompts, search results, and model responses may be exported when an OpenTelemetry listener is configured. Use non-sensitive sample requests, restrict telemetry access and retention, and disable sensitive capture before handling confidential information.

## Pattern tradeoffs

Classification adds a model call, latency, and possible misrouting. A missing route-specific deployment silently uses the documented default deployment, so compare route quality and cost before relying on model tiers. Search results can be stale or incomplete, and external requests add cost and availability dependencies. Keep explicit budgets and authorization checks outside the model's classification decision.
