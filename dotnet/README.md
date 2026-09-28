# Agentic Design Patterns — .NET / Microsoft Agent Framework

Chapter demos use Microsoft Agent Framework (MAF) workflows and model deployments hosted in Microsoft Foundry. Shared configuration is stored outside the repository with .NET user-secrets.

## Prerequisites

- .NET 10 SDK
- A Microsoft Foundry project endpoint, deployed model name, and API key

## Configure Foundry user-secrets

Set the values locally; do not add them to `appsettings.json`, source files, or commits:

```powershell
dotnet user-secrets set "Foundry:ProjectEndpoint" "https://<resource>.services.ai.azure.com/api/projects/<project>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ApiKey" "<your-api-key>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployment" "<deployment-name>" --project dotnet/src/Shared
```

`ModelDeployment` is the default. Add optional, named deployments for per-run selection:

```powershell
dotnet user-secrets set "Foundry:ModelDeployments:fast" "<fast-deployment-name>" --project dotnet/src/Shared
dotnet user-secrets set "Foundry:ModelDeployments:quality" "<quality-deployment-name>" --project dotnet/src/Shared
```

All chapter projects use the same `UserSecretsId`. The corresponding environment variable overrides are `Foundry__ProjectEndpoint`, `Foundry__ApiKey`, and `Foundry__ModelDeployment`; named deployments use `Foundry__ModelDeployments__<name>`.
For the OpenAI-compatible model API, the shared client derives `https://<resource>.openai.azure.com/openai/v1/` from a standard Foundry project endpoint on `<resource>.services.ai.azure.com`.

Select a configured name when running a chapter demo; omit it to use the default:

```powershell
dotnet run --project dotnet/src/Chapter01.PromptChaining -- specs --deployment fast
dotnet run --project dotnet/src/Chapter01.PromptChaining -- all --deployment quality
```

## Build

```powershell
dotnet build dotnet/AgenticDesignPatterns.slnx
```

## Chapter demos

| Chapter | Pattern | Project | README |
|---|---|---|---|
| 01 | Prompt Chaining | `src/Chapter01.PromptChaining` | [Chapter 01 guide](src/Chapter01.PromptChaining/README.md) |
| 02 | Routing | `src/Chapter02.Routing` | [Chapter 02 guide](src/Chapter02.Routing/README.md) |
