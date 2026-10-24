# Chapter 14 - Hosted RAG Agent

Expose the [Chapter 14 local-corpus RAG agent](../Chapter14.RAG/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. The host uses the same MAF retrieval tool and sample corpus as the console demo.

## Request and response

Send a plain-text question, such as `What did the president say about Justice Breyer?`. Each request searches the bundled sample corpus, retrieves up to five matching chunks, and returns a buffered answer with source identifiers. The host creates a fresh, stateless run per request; caller conversation history is not persisted. The Responses protocol can stream, but this demo returns one completed update after retrieval and generation finish.

## Prerequisites and configuration

Use .NET 10, a Foundry project with a deployed chat model, and the Azure Developer CLI (`azd`) with the `azure.ai.agents` extension. Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to select your own project/model, configure authentication, and grant deployment permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. If no API key is configured, it uses `DefaultAzureCredential` locally and the hosted agent's managed identity in Azure. Never include API keys in the hosted package.

## Run locally

From the repository root, start the protocol host:

```powershell
dotnet run --project dotnet/src/Chapter14.RAG.Hosted
```

From a second terminal, in `dotnet`, invoke the Responses endpoint with a caller-supplied question:

```powershell
azd ai agent invoke chapter14-rag --local --protocol responses "What did the address say about the economy?"
```

Stop the host with Ctrl+C when finished.

## Deploy and invoke

After configuring the shared `azure.yaml` project for your own Foundry project and model, run from `dotnet`:

```powershell
azd deploy chapter14-rag --no-prompt
azd ai agent show chapter14-rag --output json
```

Deployment permissions do not grant model inference automatically. Resolve the active instance's `instance_identity.principal_id` from `azd ai agent show`, then verify or assign **Cognitive Services OpenAI User** at the actual model-hosting Foundry account scope. Allow RBAC propagation and recheck the current identity after redeployment; see [runtime inference setup](../../README.md#configure-and-deploy-hosted-agents).

Once inference access is effective, invoke the deployed agent:

```powershell
azd ai agent invoke chapter14-rag --protocol responses "What did the president say about Justice Breyer?"
```

The service uses bundled .NET 10 deployment, so the host and referenced Chapter 14 / Shared projects are published together. It does not provision a vector store or external search connection. Model inference and hosted compute incur charges; costs depend on usage, model, and hosting configuration.

To remove only this reader-owned hosted agent, run `azd ai agent delete chapter14-rag` from the configured `dotnet` project. Do not run `azd down` on this shared multi-service project.

## Telemetry and sensitive data

The MAF agent emits spans under `Chapter14.RAG`. **Sensitive-data capture is enabled for this demo:** caller questions, retrieved passages, and model responses may appear in configured traces. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Pattern tradeoffs

The bundled sample corpus is small and uses deterministic lexical ranking; it is a reproducible teaching example, not a semantic-search service. Replace it with a permission-aware index and evaluate retrieval recall, source coverage, citation correctness, answer groundedness, latency, and cost before using the pattern with production data.
