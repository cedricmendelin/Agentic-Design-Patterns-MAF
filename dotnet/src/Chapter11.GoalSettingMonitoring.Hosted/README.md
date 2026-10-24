# Chapter 11 — Hosted Goal Setting and Monitoring

Expose the [Chapter 11 MAF goal-setting workflow](../Chapter11.GoalSettingMonitoring/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Each request generates Python code, reviews it against explicit goals, and stops when the model judge confirms success or the bounded iteration budget is exhausted.

## Request and response

Send the use case as plain text to use the sample goals and a five-iteration budget, or send a JSON object:

```json
{
  "useCase": "Write Python code to parse a CSV file and report row counts.",
  "goals": ["Use the standard library", "Handle an empty file", "Print the row count"],
  "maxIterations": 3
}
```

`goals` must contain 1–10 non-empty strings, `useCase` is limited to 2,000 characters, each goal to 300 characters, and `maxIterations` to 1–5. The response contains the goal status, number of iterations used, and final generated Python code. The host processes the caller's input; it does not replay a saved sample batch, save code to its filesystem, or execute generated code.

Requests are isolated and stateless: a host-provided response ID creates a fresh session partition even when service-to-service callers omit the platform user header. No caller history is reused to seed another request, and no candidate is promoted. The Responses streaming endpoint buffers the completed workflow result. Cancellation is passed through each model call.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select an existing Foundry project and model, configure `dotnet/azure.yaml`, and install azd agent tooling. Local model calls use a configured API key or developer identity; Azure execution uses managed identity. Keep credentials out of the deployment package.

The runtime identity needs **Cognitive Services OpenAI User** (or verified equivalent inference access) on the model-hosting Foundry account. After deployment, resolve `instance_identity.principal_id`, check effective assignments including inherited grants, assign the role at the model-hosting account scope only when authorized, and allow time for RBAC propagation. Recheck the principal after redeployment.

## Run locally

From the repository root, start the Responses server:

```powershell
dotnet run --project .\dotnet\src\Chapter11.GoalSettingMonitoring.Hosted
```

In another terminal, from `dotnet`, send a plain use case:

```powershell
azd ai agent invoke chapter11-goal-setting-monitoring --local --protocol responses "Write Python code to find the longest binary gap of a positive integer."
```

Or pass a JSON request body through an input file or supported protocol client. The response is buffered until the generation, review, and judgment calls finish. Stop the server with Ctrl+C.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter11-goal-setting-monitoring --no-prompt
azd ai agent show chapter11-goal-setting-monitoring --output json
```

Use the current agent's `instance_identity.principal_id` to verify or assign model inference access as described in the shared guide. Allow time for RBAC propagation, then invoke the request/response endpoint:

```powershell
azd ai agent invoke chapter11-goal-setting-monitoring --protocol responses "Write Python code to find the longest binary gap of a positive integer."
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 11 and Shared projects together. No Docker image or container registry is required. Deploy only this service with the service-scoped command above.

## Telemetry and sensitive data

The hosted agent enables sensitive-data capture for tracing generation, review, and judge calls. Caller use cases, goals, generated code, feedback, and model outputs may appear in exported traces. Use non-sensitive prompts, restrict telemetry access and retention, and disable sensitive capture before processing confidential requests.

## Costs and resource ownership

Each iteration makes three model calls, up to the configured maximum of five iterations. Hosting and model inference incur Azure charges; prompt and generated-code length also affect token usage.

Remove only the hosted agent and versions that you own using Foundry's agent-scoped management controls. Do not run `azd down` against a shared project or remove project infrastructure or model deployments.

## Pattern tradeoffs

The independent model reviewer and judge add useful monitoring, but they can still make correlated or incorrect judgments. The bounded loop limits cost and latency rather than guaranteeing correctness. Use deterministic tests, linters, and human review for code quality and safety before executing or adopting the generated artifact.
