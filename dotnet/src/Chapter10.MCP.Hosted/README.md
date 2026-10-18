# Chapter 10 - Hosted MCP Agent

Expose the [Chapter 10 MAF MCP agent](../Chapter10.MCP/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. Each caller request connects to a configured remote Streamable HTTP MCP server and returns a completed response.

## Request behavior

The host passes the caller's latest message to the configured MCP-enabled MAF agent. It discovers the server's tools for each request but exposes only the `greet` tool to the agent. Each request has a fresh MCP connection and no persisted MCP session or conversation state; the Responses protocol streams one buffered final result. Cancellation and MCP, model, and tool failures propagate.

The hosted companion supports remote HTTP MCP endpoints only. The console filesystem example remains separate because it starts a local stdio child process and accesses a local directory; those semantics are not silently carried into a remote host.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) for azd, Foundry project selection, model configuration, authentication, packaging, and required runtime inference access.

Configure the remote MCP endpoint for local runs:

```powershell
$env:CHAPTER10_MCP_SERVER_URL = "https://<trusted-mcp-server>/mcp"
```

The MCP server must support Streamable HTTP and be reachable from both the local process and the deployed Foundry host. The included sample server can be run locally for console tests, but Foundry cannot reach a developer's `localhost`; use a deployed endpoint for a remote invocation.

Hosted model calls use `DefaultAzureCredential` (developer identity locally, managed identity in Azure). Keep API keys out of the hosted runtime. The console MAF variant retains its separate shared user-secrets API-key path.

For azd deployment, configure `CHAPTER10_MCP_SERVER_URL` in the active azd environment and keep it synchronized with the `chapter10-mcp` service's `env` entry in `azure.yaml`:

```powershell
Set-Location .\dotnet
azd env set CHAPTER10_MCP_SERVER_URL "https://<trusted-mcp-server>/mcp"
```

Never commit tokens or endpoint credentials. Configure server authentication using an approved identity or secret store and extend the MCP transport configuration accordingly if the server requires credentials.

## Run locally

From the repository root, with endpoint and shared Foundry settings configured:

```powershell
dotnet run --project .\dotnet\src\Chapter10.MCP.Hosted
```

The server listens on port 8088 by default. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter10-mcp --local "Use the greet tool to greet Ada."
```

Keep the local host private and stop it with Ctrl+C after testing.

## Deploy and invoke

Select your own Foundry project and model using the shared guide, set the endpoint above, then run from `dotnet`:

```powershell
azd deploy chapter10-mcp --no-prompt
azd ai agent show chapter10-mcp --output json
```

Before invoking, resolve the current `instance_identity.principal_id`, confirm it has **Cognitive Services OpenAI User** (or verified equivalent inference access) at the actual model-hosting Foundry account scope, and allow RBAC propagation. The agent runtime identity may change after redeployment. Then invoke:

```powershell
azd ai agent invoke chapter10-mcp --protocol responses "Use the greet tool to greet Ada."
```

The service uses bundled .NET 10 code deployment so the Chapter 10 and Shared dependencies are published with the host. Deploy only this service; do not remove or overwrite shared Foundry resources when cleaning up an experiment. Hosting and model inference incur Azure charges.

## Telemetry and safety

The agent emits spans under `Chapter10.MCP`. **Sensitive-data capture is enabled for this demo:** prompts, tool inputs, and results can appear in traces. Use non-sensitive requests and restrict telemetry access and retention.

Treat the MCP endpoint, its tools, descriptions, and results as untrusted. Review the allowlist and access policy before deployment, use least-privilege server credentials, and require explicit approval for tools that read sensitive information or make changes. The demo's greeting server has a harmless, read-only `greet` tool; it is not a security model for arbitrary MCP servers.
