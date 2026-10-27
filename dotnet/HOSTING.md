# Hosting the pattern demos

The hosted projects adapt the console demos to Microsoft Foundry Agent Service.
Hosting is not a second implementation of a pattern. Start with the pattern's README;
use its hosted README for the exact input, output, state, and protocol contract.

## Local setup

Use .NET 10, PowerShell 7, Azure CLI and azd with the versions/extensions declared in
[`azure.yaml`](azure.yaml). Install the `azure.ai.agents` extension if missing.
Use an existing project with hosted-agent support and a compatible deployed model.
Sign in to your intended tenant yourself; Azure CLI and azd authentication are separate.

Hosts read shared user-secrets, environment variables, then command-line overrides:

| Canonical setting | Runtime fallback |
|---|---|
| `Foundry:ProjectEndpoint` / `Foundry__ProjectEndpoint` | `FOUNDRY_PROJECT_ENDPOINT` |
| `Foundry:ModelDeployment` / `Foundry__ModelDeployment` | `AZURE_AI_MODEL_DEPLOYMENT_NAME` |
| `Foundry:ModelDeployments:<alias>` | `Foundry__ModelDeployments__<alias>` |

Canonical values take precedence over fallbacks. Keep local overrides aligned with
the target project. Local console demos require an API key; most local hosts accept
the shared key or use `DefaultAzureCredential` when it is absent. Agent Harness and
Decision Models hosts always use Entra ID. Azure-hosted inference must use managed
identity, not committed keys. The local developer identity also needs inference access.

```powershell
$env:Foundry__ProjectEndpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:Foundry__ModelDeployment = '<chat-deployment>'
dotnet run --project .\dotnet\src\Chapter04.Reflection.Hosted
```

Invoke the protocol endpoint documented by the selected companion from another
terminal. Stop a temporary server with Ctrl+C. A2A and Human-in-the-Loop have distinct
protocols; do not assume every project exposes `/responses`.

## Select the deployment target

Run azd commands from `dotnet`. Reuse the existing multi-service manifest; do not
reinitialize it or overwrite unrelated services. Replace its sample Foundry project
reference with your own `azure.ai.project` service and update the selected host's
`uses` entry. Replace literal model names/aliases in that service's `env` block:
setting an azd environment variable does not override a literal manifest value.

```powershell
Set-Location .\dotnet
azd env select '<environment>'
azd env set AZURE_TENANT_ID '<tenant-id>'
azd env set AZURE_AI_PROJECT_ID '<project-arm-id>'
azd env set AZURE_AI_PROJECT_ENDPOINT 'https://<account>.services.ai.azure.com/api/projects/<project>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<chat-deployment>'
azd deploy <service> --no-prompt
azd ai agent show <service> --output json
```

If needed, create an azd environment first with `azd env new`. Discover the actual
project ARM ID rather than constructing one. These instructions do not provision
models/projects or authorize deployment or RBAC changes. Deploy only the selected
service; ask your administrator to resolve unavailable regions, hosting, or permissions.

## Runtime inference access

Deployment permissions and runtime inference permissions are separate. Before remote
invocation, resolve the **current `instance_identity.principal_id`** from the selected
agent's show output and the actual **model-hosting account ARM ID**. Check inherited
and effective role assignments at that scope.

```powershell
az role assignment list --assignee-object-id '<runtime-principal-id>' --scope '<model-hosting-account-arm-id>' --include-inherited --fill-principal-name false --output table
# Only when missing and authorized:
az role assignment create --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role 'Cognitive Services OpenAI User' --scope '<model-hosting-account-arm-id>'
azd ai agent invoke <service> '<representative request>'
```

Reuse equivalent inference access if present. Do not substitute the deploying user,
project identity or blueprint identity, broaden scope to a subscription, or bypass
missing access with an API key. Allow RBAC propagation and recheck after redeployment:
runtime identities can change. Verify an actual remote result, not only an active version.

## Shared code and packaging

[`Shared.Hosting`](src/Shared.Hosting) owns common settings/client creation and the
stateless agent adapter. The adapter accepts the latest user message, uses no business
session state, and buffers a completed workflow result for streaming delivery. It is
not suitable for durable approvals or conversational memory without a separate state
contract. Specialized hosts retain their identity, isolation and protocol controls.

The manifest uses .NET 10 code deployment with `dependencyResolution: bundled`.
azd publishes each host and its project references locally before upload. Preserve
source project paths, runtime, entry point and protocol settings. Include referenced
demo, Shared and Shared.Hosting assemblies plus central package/build configuration.
Exclude `.azure`, `.env*`, `secrets.json`, credentials and unnecessary intermediates.

## Costs, traces and cleanup

Hosting, model calls, retries, evaluations and telemetry can incur charges. Some demos
enable sensitive telemetry for synthetic examples; disable it before handling private
input, and restrict trace access/retention. A call budget is not an exact billing cap.

Remove only agent versions/resources you own through agent-scoped Foundry controls.
Never use `azd down` against a shared project or delete unrelated agents or shared models.
