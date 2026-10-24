# Chapter 12 - Hosted Exception Handling and Recovery

Expose the [Chapter 12 recovery workflow](../Chapter12.ExceptionHandling/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF controls bounded retries, request-local checkpoints, fallback selection, and response generation.

## Request and response

Submit a location address as the user message. The host uses fresh workflow state for each request and persists no conversation history. It buffers the completed workflow and returns one final response; provider errors and cancellation propagate as request failures.

The sample index supports `1600 Amphitheatre Parkway, Mountain View, CA` and `1 Market Street, San Francisco, CA`. Prefix an address with `transient:` to simulate one transient precise-lookup failure and retry, or `primary-unavailable:` to retry the unavailable precise provider and then exercise the city-level fallback. These are simulation controls, not production request flags or a live geocoding service.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter12.ExceptionHandling.Hosted
```

The local server listens on port 8088 by default; keep it private. In another terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter12-exception-handling --local --protocol responses '1600 Amphitheatre Parkway, Mountain View, CA'
azd ai agent invoke chapter12-exception-handling --local --protocol responses 'primary-unavailable: 1 Market Street, San Francisco, CA'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing shared hosted setup, run from `dotnet`:

```powershell
azd deploy chapter12-exception-handling --no-prompt
azd ai agent show chapter12-exception-handling --output json
```

Use the returned `instance_identity.principal_id` to verify model-inference access at the actual model-hosting Foundry account scope. If the runtime identity lacks equivalent access, an authorized administrator must assign **Cognitive Services OpenAI User** there. Allow time for role-assignment propagation and recheck the runtime identity after redeployment before invoking:

```powershell
azd ai agent invoke chapter12-exception-handling --protocol responses 'transient: 1600 Amphitheatre Parkway, Mountain View, CA'
```

The service uses bundled .NET 10 code deployment, publishing this host and its referenced Chapter 12 and Shared projects together. No Docker image or container registry is required. Keep `.azure`, environment files, user-secrets, and build output out of published content.

Deploying and invoking hosted agents and model inference incur Azure charges. Remove only agents and other resources that you own; do not remove shared project infrastructure or unrelated agents.

## Telemetry and sensitive data

Agent spans use `Chapter12.ExceptionHandling`. **Sensitive-data capture is enabled for this demo:** prompts, sample addresses, intermediate results, and responses may appear in traces when an exporter is configured. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential addresses.

## Pattern tradeoffs

Retries are limited to a side-effect-free lookup activity and a fixed attempt budget. Real operations need explicit transient-error classification, timeouts, idempotency controls, and observability. Area fallback is less precise than an exact lookup and must be labeled as such. Checkpoints in this example are request-local; restart recovery requires a durable store or workflow service.
