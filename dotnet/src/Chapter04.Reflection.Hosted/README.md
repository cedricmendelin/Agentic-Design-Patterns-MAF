# Chapter 04 - Hosted Reflection Agent

This companion exposes the Chapter 04 MAF reflection workflow through Foundry Agent Service's **Responses protocol**. It hosts the shared chapter logic; it is not a separate Foundry-native reflection implementation. Version 1 was deployed on 2026-10-16 before the shared workflow graph refactor; version 2, deployed and remotely verified the same day, runs the refactored bounded generator/verifier graph.

Requests beginning with `fact-check:`, `product:`, or `code-review:` select a workflow mode. Unprefixed text is treated as product details. The `product` mode intentionally gives the writer misleading length/style guidance while the verifier enforces a hidden house style, so it typically needs at least one revision round. Hosted version 3 includes these prompts; a remote smart-mug request was accepted on round 3 of 3 on 2026-10-16. The caller's supplied text is used rather than the console samples. Every request creates fresh agents and has no persisted conversation history. The agent returns the final result after the bounded work completes; intermediate stages are available to local console output and configured traces, not streamed to the caller.

All three modes configure the same MAF `WorkflowBuilder` graph with generation, verification, decision, and completion executors. A conditional feedback edge repeats generation on `Revise` while budget remains. Every round includes verification; `Accept` stops immediately, including on the final round. The default is three rounds (at most six agent invocations, excluding provider-internal retries).

The textual response includes `Outcome: Accepted` or `Outcome: MaxRoundsReached`, rounds used, final draft, and review/reasoning. Exhaustion is a completed protocol request with an explicitly **not accepted** draft, not a protocol failure or a success claim. Malformed reviews, empty drafts, provider failures, and cancellation do not produce success-shaped results. `code-review:` now follows the caller's Python task; factorial is only the console sample.

## Prerequisites and authentication

- .NET 10 SDK, Azure CLI, Azure Developer CLI, and the `azure.ai.agents` extension.
- A Foundry project configured for hosted agents and a chat-model deployment.
- The local developer identity and deployed agent's managed identity need inference permission on the model-hosting Foundry account, such as **Cognitive Services OpenAI User**. The deploying identity separately needs agent deployment permissions.
- Before a remote invocation, resolve the deployed service's `instance_identity.principal_id` with `azd ai agent show chapter04-reflection --output json`, discover the actual model-hosting account ARM ID, and check effective/inherited role assignments. If needed and authorized, assign the inference role to that runtime identity at the account scope and allow propagation. Do not grant it to the deployer, project identity, or blueprint identity.

The host reads shared .NET user-secrets `Foundry:ProjectEndpoint`, `Foundry:ModelDeployment`, optional `Foundry:ModelDeployments`, and `Foundry:ApiKey`. The corresponding `Foundry__*` environment variables override user-secrets. When the API key is blank, `DefaultAzureCredential` uses the local developer identity and Azure managed identity in the hosted runtime. The Foundry runtime's `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` are supported as endpoint/deployment fallbacks. Never commit or print keys.

The existing `azure.yaml` service uses the shared project service and specifies `gpt-5-mini`; verify that this deployment exists in the selected Foundry project before deployment. No infrastructure provisioning is part of these code artifacts.

## Run locally

From the repository root, set shared user-secrets as described in [the .NET setup guide](../../README.md), then start the protocol server:

```powershell
dotnet run --project .\dotnet\src\Chapter04.Reflection.Hosted
```

The host listens on port 8088 by default. For local Entra ID authentication, leave `Foundry:ApiKey` blank and sign in with your developer identity. Do not expose this local server publicly.

With the service available in the existing azd project, invoke it from a second terminal:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent invoke chapter04-reflection --local --protocol responses 'fact-check: Explain why plants use photosynthesis.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'product: A mug keeps coffee hot and is controlled by a smartphone app.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'code-review: Write a Python factorial function with a docstring and negative-input validation.'
```

Each request is independent. Stop the server after local invocation.

## Deploy and invoke

`dotnet\azure.yaml` retains the existing Chapter 02 and Chapter 03 services and shared Foundry project service, and adds only `chapter04-reflection`. The new service uses bundled code deployment so its host, chapter logic, Shared reference, and runtime dependencies are published together. Do not reinitialize the multi-service manifest or run `azd down` against the shared project.

Deployment is an Azure write and is not authorized by adding these files. After selecting/confirming the target project and model, verifying that the runtime inference-role assignment is covered by the deployment approval, configuring the azd environment, and successfully invoking the local protocol endpoint, deploy only this service:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd deploy chapter04-reflection --no-prompt
azd ai agent show chapter04-reflection --output json
azd ai agent invoke chapter04-reflection --protocol responses 'product: A reusable insulated mug with app controls.'
```

After deployment, resolve the actual runtime principal and model-hosting Foundry account scope, check or assign **Cognitive Services OpenAI User** as described above, allow RBAC propagation, then verify a representative remote response. An active agent version alone is not evidence of successful inference. Deployment creates an immutable hosted version and model calls incur usage charges. Evaluation setup and execution are separate; see the generated setup below.

For safe cleanup, delete only the `chapter04-reflection` agent/version using Foundry's agent-scoped management workflow. Do not delete the shared Foundry project, model deployment, Chapter 02/03 services, or unrelated agents.

## Packaging and telemetry

The host project references `Chapter04.Reflection`, which references `Shared` and consumes the central .NET build/package files. `dependencyResolution: bundled` makes the azd extension publish the project and referenced dependencies locally for the configured .NET 10 runtime. The root `.agentignore` excludes `.env`, `.azure`, secrets, and build output; do not add user-secrets or connection strings to the package.

The MAF agents emit tracing under `Chapter04.Reflection`, and sensitive data capture is explicitly enabled. Prompts, caller requests, reviews, and model responses can appear in traces when an exporter is configured. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential requests.

## Limitations and status

- Fact checking is an LLM judgment without retrieval; do not treat it as source-verified.
- Every mode makes two agent invocations per round and stops on acceptance or after at most three rounds; no unverified rewrite is returned.
- The hosted protocol returns a buffered final result and does not retain conversation history.
- Deterministic workflow and protocol tests, bundled publish contents, and a local Responses invocation were verified during implementation.

## Verified deployment

On 2026-10-16, only `chapter04-reflection` was deployed to the existing `mvp-playground-foundry-proj` project in `swedencentral`, using the `routing-dev` azd environment and the existing `gpt-5-mini` deployment. Version 1 reported `active`. No shared infrastructure was provisioned and no Chapter 02/03 service was redeployed.

The deployed runtime identity (`instance_identity.principal_id` = `f59964ec-9500-454c-ad29-8006638c4222`) initially had no effective account-scoped inference grant. With user authorization, **Cognitive Services OpenAI User** was assigned and verified at `/subscriptions/0138edd2-3a62-4876-912f-addf6b491e8a/resourceGroups/mvp-playground-rg/providers/Microsoft.CognitiveServices/accounts/mvp-playground-foundry`. After a 30-second propagation wait, a fresh remote session successfully processed a `product:` smart-mug request and returned: "Keep your coffee hot and control it from your smartphone with a reusable smart mug." Azure execution used managed identity, not an API key. Recheck the runtime identity and effective role after redeployment because the identity can change.

Version 2 (shared workflow graph) was deployed later on 2026-10-16 to the same project and model and reports `active`. The runtime identity was unchanged (`f59964ec-…`), so the existing role assignment still applies. A remote `Write a Python function that returns the square of a number.` request completed successfully, and the verifier kept requesting revisions until the workflow stopped at its three-round limit (`Outcome: MaxRoundsReached`). This exercised the bounded loop end to end.

Evaluation-suite generation completed on 2026-10-16 for `chapter04-reflection` in `routing-dev`, using synthetic examples based on the documented public contract. No evaluations were run and the agent was not redeployed.

## Evaluation setup

The generated `chapter04-reflection-smoke` setup uses `gpt-5-mini` and contains 15 examples: five each for `fact-check:`, `product:`, and the original fixed factorial `code-review:` mode. The registered dataset is version `1.0`, and the registered evaluator is version `1` with seven dimensions. Generation requested observable final-response criteria, without internal traces or execution proofs.

Review artifacts are saved in [eval.yaml](eval.yaml), [the dataset](datasets/chapter04-reflection-smoke/chapter04-reflection-smoke_dg.jsonl), and [the rubric](evaluators/chapter04-reflection-smoke/rubric_dimensions.json). Deployment context remains owned by `dotnet\azure.yaml` and the `routing-dev` azd environment; [.foundry metadata](.foundry/agent-metadata.yaml) records generation jobs, registered dataset/evaluator references, and review status. The local azd suite configuration is not a separately verified remote Foundry suite resource.

**Review required:** the generated rubric adds case-sensitive prefix matching, rigid sentence-count constraints, and mandatory code fences that the public contract does not require. It also imposes an overly strict fact-check layout. The user explicitly chose to preserve this generated rubric unchanged for review. Do not treat its scores as an acceptance gate without resolving these mismatches. Dataset `candidate_response` values are synthetic examples, not results from invoking the deployed agent.

These artifacts also predate the shared bounded graph, common outcome formatting, and arbitrary Python task support. They, the baseline configuration, and `.foundry` metadata have been preserved unchanged during this refactor. Updating the rubric/dataset, running evaluations, and redeploying are separate authorized operations.

After generation completes and the artifacts have been reviewed, an explicitly authorized evaluation run can be started from `dotnet`:

```powershell
$env:AZURE_DEV_USER_AGENT = 'microsoft_foundry_skill'
azd ai agent eval run --agent chapter04-reflection --environment routing-dev --no-prompt
```

Evaluation runs incur additional model usage. Setup alone does not produce pass rates or establish the agent's quality.
