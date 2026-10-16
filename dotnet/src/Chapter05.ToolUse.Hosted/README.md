# Chapter 05 - Hosted Tool Use Agent

This companion exposes the Chapter 05 MAF function-tool agent through Foundry Agent Service's **Responses protocol**. It hosts the existing in-process function tools; it is not a Foundry-native reimplementation. The host accepts caller input and supports optional `search:`, `calculate:`, and `stock:` prefixes. Each request creates a fresh tool-enabled agent and returns a buffered final response. It does not retain conversation history.

## Prerequisites and authentication

- .NET 10 SDK, Azure CLI, Azure Developer CLI, and the `azure.ai.agents` extension.
- A Microsoft Foundry project endpoint and chat-model deployment.
- The local developer identity and deployed agent's managed identity need model-inference permission on the model-hosting Foundry account, such as **Cognitive Services OpenAI User**. The deployment identity separately needs Foundry agent deployment permissions.
- Before any remote invocation, resolve `instance_identity.principal_id` from `azd ai agent show chapter05-tool-use --output json`, discover the actual model-hosting account ARM ID, and check effective/inherited role assignments. If needed and authorized, assign the inference role to that runtime identity at the account scope and allow propagation. Do not grant it to the deployer, project identity, or blueprint identity.

The host reads shared user-secrets `Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ModelDeployments`, and `Foundry:ApiKey`. Corresponding `Foundry__*` environment variables override user-secrets. When `ApiKey` is blank, local Entra ID authentication uses `DefaultAzureCredential` and Azure uses managed identity. Never commit or print keys.

## Run locally

From the repository root, configure shared user-secrets as described in [the .NET setup guide](../../README.md), then start the Responses server:

```powershell
dotnet run --project .\dotnet\src\Chapter05.ToolUse.Hosted
```

The host listens on port 8088 by default. In another terminal, invoke each supported mode with a caller-supplied request:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'search: capital of France'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'calculate: (5 + 7) * 3'
azd ai agent invoke chapter05-tool-use --local --protocol responses 'stock: MSFT'
```

Each request is independent. Stop the server after local invocation.

## Deploy and invoke

`dotnet\azure.yaml` adds only the `chapter05-tool-use` service and preserves the existing Chapter 02-04 services and shared Foundry project service. The service uses bundled .NET 10 code deployment so the host, Chapter 05 project, Shared reference, and runtime dependencies are published together. Do not reinitialize the multi-service manifest or run `azd down` against the shared project.

Deployment is an Azure write and is not authorized by adding these files. After selecting the target project/model, confirming that required runtime inference RBAC is included in deployment approval, configuring the azd environment, and successfully invoking the local protocol endpoint, deploy only this service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter05-tool-use --no-prompt
azd ai agent show chapter05-tool-use --output json
azd ai agent invoke chapter05-tool-use --protocol responses 'calculate: 10 factorial'
```

After deployment, resolve the runtime principal and actual model-hosting account scope, check or assign **Cognitive Services OpenAI User** as described above, allow for RBAC propagation, and verify a representative remote response. An active version alone does not prove model inference works. Deployment creates an immutable hosted version and model calls incur usage charges. No Azure resources have been deployed by adding these code artifacts.

For safe cleanup, remove only the `chapter05-tool-use` agent/version using the Foundry agent-scoped management workflow. Do not delete the shared Foundry project, model deployment, or unrelated agents.

## Packaging, telemetry, and limitations

`dependencyResolution: bundled` publishes the host and referenced projects locally. The host's `.agentignore` excludes `.azure`, environment files, secrets, and build output. MAF tracing uses `Chapter05.ToolUse` and explicitly enables sensitive data capture; prompts, tool arguments, and responses can therefore appear in configured traces. Use non-sensitive inputs and restrict telemetry access and retention.

The hosted tool set uses deterministic sample search and stock data, plus a bounded arithmetic parser; it does not perform live web search, retrieve real-time stock prices, or execute arbitrary generated code. Tool errors, provider failures, and cancellation propagate rather than being converted into successful-looking responses. The Responses endpoint returns a completed result rather than streaming tool progress.

## Deployment status

Version 1 is active in the `mvp-playground-foundry-proj` project, using the `gpt-5-mini` deployment. The hosted runtime identity was granted **Cognitive Services OpenAI User** at the model-hosting account scope (`mvp-playground-foundry`); after a 20-second propagation wait, the remote Responses invocation `calculate: 10 factorial` returned `3628800`. Evaluation-suite generation was offered after deployment and deferred.

Each future deployment creates a new immutable version and may incur model usage charges. Confirm the target project/model and deployment authorization before deploying elsewhere. Recheck the current runtime identity's effective inference access after redeployment because its principal may change.
