# Chapter 06 - Hosted Planning Agent

This companion exposes the Chapter 06 MAF plan-and-write workflow through Foundry Agent Service's **Responses protocol**. It shares the console workflow, including deterministic plan validation, and is not a separate Foundry-native planning implementation.

Each request uses the caller's latest user message as the topic, runs one planner call followed by validation and one writer call, and returns the completed plan and summary. Invalid plans fail before the writer runs. Each request creates an isolated workflow; no conversation state is persisted. The Responses host buffers the result rather than streaming intermediate workflow stages.

## Prerequisites and authentication

- .NET 10 SDK, Azure CLI, Azure Developer CLI, and the `azure.ai.agents` extension.
- An existing Foundry project configured for hosted agents and an available chat-completions model deployment.
- Authenticate Azure CLI and azd yourself in the target tenant. Deployment authentication is separate from the console API key.
- The local developer identity and deployed runtime managed identity require model inference access on the actual model-hosting Foundry account, such as **Cognitive Services OpenAI User**. The deploying identity separately requires Foundry project/agent deployment permissions.
- Before remote invocation, resolve `instance_identity.principal_id` with `azd ai agent show chapter06-planning --output json`, discover the actual model-hosting account ARM ID, and inspect effective/inherited role assignments. If missing and approved, grant Cognitive Services OpenAI User to that runtime principal at the model-hosting account scope. Do not use the deploying user, project identity, or blueprint identity; allow RBAC propagation and recheck after redeployment.

The host reads shared user-secrets `Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ModelDeployments`, and `Foundry:ApiKey`. Corresponding `Foundry__*` environment variables override user-secrets. A blank API key uses `DefaultAzureCredential` locally and managed identity in Azure. Foundry runtime variables `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are supported as endpoint/deployment fallbacks. Never commit or print keys.

## Run locally

From the repository root, configure shared user-secrets as described in the [dotnet setup guide](../../README.md), then start the protocol server:

```powershell
dotnet run --project .\dotnet\src\Chapter06.Planning.Hosted
```

The host listens on port 8088 by default. Do not expose the local server publicly. For local Entra ID authentication, leave `Foundry:ApiKey` blank and sign in using your developer identity.

Once the existing azd environment has been configured to include this service, invoke a caller-provided topic from another terminal:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter06-planning --local --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
```

Stop the server after local invocation. Requests are stateless and do not inherit earlier turns.

## Deploy and invoke

The shared `dotnet/azure.yaml` retains the existing chapter services and Foundry project service, adding only `chapter06-planning`. Do not reinitialize or overwrite the shared multi-service manifest. Deployment is an Azure write and is not authorized merely by these artifacts. Confirm the target project/model and explicitly approve any required runtime RBAC assignment before deploying.

After selecting the existing project and deployment, configuring the azd environment, and successfully invoking the local endpoint, deploy only this service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter06-planning --no-prompt
azd ai agent show chapter06-planning --output json
azd ai agent invoke chapter06-planning --protocol responses 'Plan and summarize the role of reinforcement learning in robotics.'
```

Before the remote invocation, verify the active version and runtime identity, check inference access at the actual model-hosting account scope, assign **Cognitive Services OpenAI User** only when approved and missing, and allow for propagation. An active version alone is not a successful smoke test. Hosted requests make two model calls and incur corresponding usage costs. Evaluation-suite setup is deferred and no evaluation has been run.

For cleanup, remove only this chapter's hosted agent/version using the agent-scoped Foundry workflow. Do not run `azd down` against the shared project or delete its model deployment, other chapter agents, or unrelated services.

## Packaging and telemetry

The host references `Chapter06.Planning`, which references Shared and the central build/package configuration. The service uses bundled .NET 10 deployment, which publishes the project and referenced assemblies locally before upload. No Docker image or ACR is required. `.agentignore` excludes `.azure`, environment files, secrets, and build artifacts; never include local user-secrets in the package.

The planner and writer emit MAF tracing under `Chapter06.Planning` with sensitive-data capture enabled. Prompts, caller input, plans, and model responses can be exported when tracing is configured. Use non-sensitive inputs and restrict telemetry access and retention.

## Validation and status

The deterministic test project exercises plan validation, the shared workflow, and a local Responses endpoint using a scripted chat client. A live Foundry smoke test is included in `dotnet/tests/Foundry.SmokeTests` for the plan-and-write console path.

No deployment or remote invocation was performed during this code change. The issue description reports a version 1 deployment to `mvp-playground-foundry-proj` using `gpt-5-mini` and a successful remote smoke test after granting the hosted runtime identity Cognitive Services OpenAI User at the model-hosting account scope. Treat that as the reported prior deployment; re-resolve the runtime identity and verify inference access and a representative response after deploying this implementation.

## Limitations

- The host implements plan-and-write only. Deep Research is a separate console demo using the Responses API and is not part of this hosted agent.
- Plans are checked for structure, not factual or semantic correctness. No proposed step is executed or authorized.
- Requests return a buffered final plan and summary and do not persist conversation state or stream intermediate stages.
- The workflow is bounded to planner → validator → writer; it has no checkpoints, task execution, monitoring, or replanning.
- No evaluation suite is configured or run.
