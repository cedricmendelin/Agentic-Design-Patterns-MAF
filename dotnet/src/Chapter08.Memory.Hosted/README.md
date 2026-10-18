# Chapter 08 - Hosted Foundry Memory

Exposes the [Chapter 08 Foundry-managed memory demo](../Chapter08.Memory/README.md) through the Foundry Responses protocol using `AgentHost` and `FoundryMemoryProvider`.

## Behavior and isolation

Send `Run the memory demo` (any nonempty text starts the same scripted demonstration). The agent stores the synthetic fact "Sam prefers window seats", waits for memory extraction, creates a **new MAF session**, recalls the travel preference, and deletes the temporary scope's memories after a successful run.

This is a runnable pattern demonstration, **not a personal-memory chatbot**. Caller messages and previous response history are not passed to the inner model or memory provider. Foundry's outer Responses conversation history can still retain requests and responses; that history is separate from the managed memory store. Every request generates a fresh scope, even if `Foundry:MemoryScope` is configured. Memory is shared only between the two inner sessions of that request, not between callers or invocations. An interrupted or failed run can leave synthetic memories in its isolated scope; use store retention or administrative deletion to clean those up.

A production personal-memory agent must first map authenticated callers to authorized, durable memory scopes and define consent, retention, correction, and deletion policies. A raw request header or model-selected user ID is not sufficient authorization.

## Configuration

Use the existing Foundry project configured in `dotnet/azure.yaml`. Both the developer identity and hosted runtime identity need memory-store and model permissions. No API key is packaged.

| Variable | Purpose |
|---|---|
| `Foundry__ProjectEndpoint` | Full project endpoint, resolved from `AZURE_AI_PROJECT_ENDPOINT` in the azd environment |
| `FOUNDRY_PROJECT_ENDPOINT` | Platform-provided fallback endpoint |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | Existing chat deployment |
| `AZURE_AI_EMBEDDING_DEPLOYMENT_NAME` | Existing embedding deployment |
| `AZURE_AI_MEMORY_STORE_ID` | Dedicated Chapter 08 memory-store name |

The provider creates the store if absent. Store processing and model calls incur Azure charges. Memory and its MAF provider are preview/experimental features.

## Run and deploy

From `dotnet`, configure an azd environment for your existing project as described in the [hosted deployment guide](../../README.md#configure-and-deploy-hosted-agents), then:

```powershell
azd ai agent run chapter08-memory --no-client
azd ai agent invoke chapter08-memory --local "Run the memory demo"
```

Stop the local process before deploying:

```powershell
azd deploy chapter08-memory --no-prompt
azd ai agent show chapter08-memory --output json
```

On the first deployment, grant the returned `instance_identity.principal_id` the **Foundry User** role at the containing Foundry account ARM resource scope, following the [memory authorization guidance](https://learn.microsoft.com/azure/foundry/agents/how-to/memory-usage#authorization-and-permissions). The runtime identity is separate from the developer identity; deploying does not automatically give it memory-store permissions. This role grants account-wide Foundry data access, so use an account appropriate for the demo:

```powershell
az role assignment create --assignee-object-id '<instance-principal-id>' --assignee-principal-type ServicePrincipal --role '53ca6127-db72-4b80-b1b0-d745d6d5456d' --scope '<foundry-account-arm-id>'
```

Allow RBAC propagation before invoking:

```powershell
azd ai agent invoke chapter08-memory "Run the memory demo"
```

The response transcript distinguishes storing facts, waiting for updates, recalling in a new session, and cleanup. Remote invocations can take longer than ordinary chat because memory extraction is asynchronous.
