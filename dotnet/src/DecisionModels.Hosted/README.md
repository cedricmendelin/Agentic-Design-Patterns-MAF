# Decision Models - Foundry hosted companion

Hosts the reusable [decision-model workflow](../DecisionModels/README.md) with the official Microsoft Agent Framework Foundry Responses integration. Hosting is a deployment option, not a different decision algorithm. The implementation follows the official [C# Agent Framework Responses sample](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/hello-world).

## Request and state contract

Send one plain-text support request as the latest user message. The provider is deployment configuration (`Decisions:Provider` = `microsoft` or `openai`), not something caller text can override. Nontext input is rejected.

Each call executes the three typed decisions, applies deterministic policy, and optionally generates a specialist draft. The final assistant text is JSON containing `Decision` (original input, full distributions, usage, disposition, priority) and `Response` (draft or review requirement). Errors and caller cancellation propagate; refusals are not converted into approval.

Only the latest user message is assessed. Earlier history and sessions do not carry business state; each call is independent. There is no persistent ticket queue, approval transaction, or memory. A review result tells the caller what is required; it does not mean a reviewer was notified. Requests never issue refunds or modify systems.

The Responses protocol can request streaming; the agent emits the **completed** result rather than partial, unchecked generation. This is buffered delivery, not interactive approval or token-by-token reasoning.

## Configuration and local invocation

Requires .NET 10, a Foundry account/project, Microsoft-Decision-1 or gpt-6-luna deployed for decisions, and a separate generative chat deployment. See [model setup and aliases](../DecisionModels/README.md#setup-and-run).

The host loads endpoint/model settings from shared user-secrets and `Foundry__*` overrides. It **ignores shared API keys**, including environment overrides; local inference uses `DefaultAzureCredential`, and Azure runtime inference uses managed identity. Sign in yourself with `az login` in the target tenant, and ensure the developer identity has inference access on the model-hosting account.

```powershell
$env:Foundry__ProjectEndpoint = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$env:Foundry__ModelDeployment = "<generative-chat-deployment>"
$env:Foundry__ModelDeployments__decision = "<microsoft-decision-deployment>"
$env:Foundry__ModelDeployments__openaiDecision = "<gpt-6-luna-deployment>"
$env:Decisions__Provider = "microsoft"
dotnet run --project dotnet\src\DecisionModels.Hosted -- --urls http://localhost:8088
```

From another PowerShell terminal:

```powershell
$body = @{
    model = "decision-models"
    input = "Checkout is down for every customer."
    stream = $false
    store = $false
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:8088/responses" -ContentType "application/json" -Body $body
```

The `/responses` path is the local host endpoint; remote invocation goes through Foundry Agent Service. `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are endpoint/chat-model fallbacks when canonical settings are absent. Only the selected decision alias is required.

## Packaging and deployment

Follow [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) for tools, authentication, existing-project selection, and the multi-service manifest. The `decision-models` service is already registered in `dotnet\azure.yaml`; do not reinitialize the manifest or overwrite other services.

Before deploying, replace the manifest's sample Foundry project reference and selected service environment values with your own project and deployments. Set `Foundry__ProjectEndpoint` and `Foundry__ModelDeployment` explicitly if shared canonical settings would otherwise override the runtime fallbacks. Configure only the provider/aliases you intend to use. No model or project is provisioned by this guide.

The service uses azd .NET code deployment, `dotnet_10`, and `dependencyResolution: bundled`. Publishing resolves the sibling demo and Shared references and central package/build files locally; the uploaded application includes the decisions library and runtime dependencies without requiring a container registry.

```powershell
dotnet publish dotnet\src\DecisionModels.Hosted -c Release -o "<publish-folder>"
Set-Location dotnet
azd deploy decision-models --no-prompt
azd ai agent show decision-models --output json
```

Keep `.azure`, `.env*`, user-secrets, credentials, and unnecessary build intermediates outside the upload. The included `.agentignore` excludes these sources. Never publish a repository root as if it were the compiled host.

Deployment needs permission to create/update hosted agents in the chosen project. Runtime inference permissions are separate: resolve the **current** `instance_identity.principal_id` from `azd ai agent show`, resolve the actual model-hosting Foundry account ARM ID, and inspect inherited/effective role assignments there. With explicit RBAC-write authorization, assign **Cognitive Services OpenAI User** (or a verified equivalent inference role) to that runtime identity at the model-hosting **account** scope if missing. If models use different accounts, verify each scope separately.

Do not substitute the deploying user, project identity, or blueprint identity, broaden the role to subscription scope, or work around missing runtime permissions with an API key. Allow for RBAC propagation and recheck after redeployment because runtime identities can change. Verify both an active version and a representative inference response.

```powershell
azd ai agent invoke decision-models "Checkout is down for every customer."
```

Invoke the selected service only. Do not deploy all chapter services as a side effect of trying this companion. Creating deployment artifacts or running locally is not permission to provision resources, deploy an agent, or change RBAC.

## Tracing, evaluation, and costs

`DecisionModels` activities record decision metadata. Specialist MAF tracing has sensitive content enabled for debugging: prompts and drafts can enter telemetry. Use synthetic requests and restrict telemetry access/retention; disable sensitive capture before handling private data.

For production, assess labeled team classifications, ordinal severity, refund detection, calibration, abstention, refusal behavior, and end-to-end review outcomes separately. Hosted execution does not itself establish model accuracy. Evaluation suites and additional live evaluation calls are explicit, separately billed work.

Each request makes three decision calls plus at most one generative call. Hosting and telemetry can incur charges even when review prevents generation. Model deployment capacity, SKU, residency, and pricing should be assessed independently from host compute.

## Safe cleanup

Remove only agent versions/resources you own using agent-scoped Foundry management. Do not use `azd down` against a shared project or delete shared model deployments, infrastructure, or unrelated agents. Stop a temporary local host after use.
