# Chapter 19 - Hosted Legal Survey Judge

This companion exposes [Chapter 19's shared MAF judge](../Chapter19.Evaluation/README.md) using the official Foundry **Responses protocol** host. It follows the [official .NET hello-world hosting sample](https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/hello-world), while keeping the console demos and their API-key configuration separate.

## Request contract

POST a legal survey question as `input` to `/responses`. The final assistant output is JSON containing `overall_score` (integer 1-5), `rationale`, `detailed_feedback` (at least five named-criterion feedback strings), `concerns` (an array), and `recommended_action`.

Each call judges only the **latest user message** with a fresh MAF judge call. No conversation history, prior judgments, notebook batches, cloud evaluation jobs, or caller-supplied system instructions are passed into the judge. Responses infrastructure can retain protocol history, but that history does not influence grading; use `store: false` for independent calls. There is no durable application-level memory.

`stream: false` returns the completed judgment. SSE requests buffer the judgment and then emit one assistant update, preserving complete JSON rather than streaming partial fields. Model errors, cancellation, and invalid output propagate instead of producing a success-shaped fallback.

This endpoint reviews survey methodology; it does not certify legal accuracy, approve a survey for use, or implement Foundry's custom endpoint-evaluator payload schema.

## Local setup and invocation

Prerequisites: .NET 10, a Foundry project, a structured-output-capable chat deployment, and a developer identity with model-inference access. Sign in manually using `az login --tenant '<tenant-id>'`.

From the repository root:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<model-deployment>'
$env:AZURE_TENANT_ID = '<tenant-id>'
dotnet run --project dotnet\src\Chapter19.Evaluation.Hosted
```

The host always uses `DefaultAzureCredential`: developer credentials locally and managed identity in Azure. It never uses the shared console API key. Shared user-secrets `Foundry:ProjectEndpoint` and `Foundry:ModelDeployment` are local fallbacks; the runtime variables above take precedence.

In another PowerShell terminal:

```powershell
Invoke-RestMethod 'http://localhost:8088/readiness'
$body = @{
    input = 'How clear are the data-retention requirements in the privacy notice you received? (Very unclear, Unclear, Neither, Clear, Very clear)'
    stream = $false
    store = $false
} | ConvertTo-Json
$response = Invoke-RestMethod 'http://localhost:8088/responses' -Method Post -ContentType 'application/json' -Body $body
$response.output.content.text
```

The official host configures its listening port (8088 by default); `PORT` can override it.

## Tracing and data handling

The client enables sensitive OpenTelemetry data for the `Chapter19.Evaluation` source so debugging can inspect the rubric, question, and judgment. Foundry injects `APPLICATIONINSIGHTS_CONNECTION_STRING` when telemetry is configured; set your own connection string for local export if needed. Treat exported content as sensitive: use synthetic questions, restrict access and retention, and disable `EnableSensitiveData` in `Program.cs` before sending confidential surveys without an approved logging policy. `store: false` is not a telemetry-redaction switch.

## Packaging

The existing multi-service [`dotnet/azure.yaml`](../../azure.yaml) includes `chapter19-evaluation` with `.NET 10` code deployment and `dependencyResolution: bundled`. azd publishes the source project and bundles the resulting runtime dependencies, including the chapter and Shared assemblies. Do not upload only the host source folder: sibling project references and central build/package settings are needed during local publish.

To inspect a package without deploying:

```powershell
dotnet publish dotnet\src\Chapter19.Evaluation.Hosted -c Release -o dotnet\publish\chapter19-evaluation
Get-ChildItem dotnet\publish\chapter19-evaluation
```

Keep `.azure`, environment files, user-secrets, and source build directories out of source uploads. The repository ignores those paths, and bundled deployment uploads published output rather than local secrets or azd state.

## Deploy and invoke

Use the [shared hosted setup](../../README.md#configure-and-deploy-hosted-agents) for Azure CLI/azd sign-in, extension installation, project selection, deployment permissions, and configuration. Choose **your own** project reference in the existing manifest without replacing unrelated services. Set `AZURE_AI_MODEL_DEPLOYMENT_NAME` in the selected azd environment; this service's manifest uses that variable rather than a literal model name.

From `dotnet`:

```powershell
azd env select '<environment>'
azd env set AZURE_AI_MODEL_DEPLOYMENT_NAME '<model-deployment>'
azd deploy chapter19-evaluation --no-prompt
azd ai agent show chapter19-evaluation --output json
```

Deploy only `chapter19-evaluation`. These instructions target an existing project and deployment; they do not provision a model or a new Foundry account. Creating the source and manifest is not permission to deploy them.

### Required runtime inference access

Deployment permission is separate from runtime inference permission. Resolve the current hosted version's **`instance_identity.principal_id`** with `azd ai agent show`, and discover the ARM ID of the account actually hosting the model. Check inherited role assignments before making changes. If missing and RBAC writes are approved, grant **Cognitive Services OpenAI User** (`5e0bd9bd-7b93-4f28-af87-19fc36ad61bd`) to that runtime principal at the model-hosting **account** scope:

```powershell
az role assignment list --subscription '<subscription-id>' --scope '<model-hosting-account-arm-id>' --include-inherited --fill-principal-name false --query "[?principalId=='<runtime-principal-id>']" --output json
# Only with explicit RBAC-write authorization and when equivalent inference access is absent:
az role assignment create --subscription '<subscription-id>' --assignee-object-id '<runtime-principal-id>' --assignee-principal-type ServicePrincipal --role '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd' --scope '<model-hosting-account-arm-id>'
```

Do not substitute the deploying user, project identity, or agent blueprint. Allow RBAC propagation and re-resolve the principal after redeployment. An active version alone is not proof that the model call works.

Once access is effective:

```powershell
azd ai agent invoke chapter19-evaluation 'What are your thoughts on legal tech?' --new-session
```

Inspect the actual structured judgment. Consult `azd ai agent invoke --help` for the installed extension's session/stream options. Run hosted evaluation suites separately and deliberately; this endpoint does not start a batch eval or optimize prompts as a side effect.

## Costs

Each request makes one structured-output model call. Model tokens, hosted compute, and any enabled telemetry/storage are billable. The separate console Foundry evaluation command incurs its own evaluation/model costs. Keep request volume and telemetry retention bounded; these examples do not supply a pricing estimate.

## Cleanup

Use `azd ai agent delete --help` for your installed extension and target only the `chapter19-evaluation` agent and versions you own. Inspect the selected environment and target before confirming deletion. Do not run broad `azd down` or delete a shared Foundry project, model, account, or another chapter's agent.
