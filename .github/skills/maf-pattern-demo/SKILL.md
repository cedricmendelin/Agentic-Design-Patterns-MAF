---
name: maf-pattern-demo
description: 'Port an Agentic Design Pattern chapter (Antonio Gulli book) from the Python notebooks in chapter_notebooks/ to a runnable C# .NET demo using Microsoft Agent Framework (MAF) and Microsoft Foundry. Use when: implement chapter, port notebook to C#, create MAF demo, prompt chaining / routing / parallelization / reflection / tool use / planning / multi-agent / memory / MCP / HITL / RAG / A2A / guardrails / evaluation pattern in .NET, add chapter README.'
argument-hint: 'Chapter number or pattern name, e.g. "01" or "Routing"'
---

# MAF Agentic Design Pattern Demo

Turns one book chapter into a self-contained C# console project inside the shared `dotnet/` solution, backed by a Microsoft Foundry model deployment, plus a chapter README.

## When to Use
- "Implement Chapter NN with MAF / .NET"
- "Port the <pattern> notebook to C#"
- "Add a Foundry-backed demo for <pattern>"

## Inputs
- Chapter number (01–21) or pattern name. Resolve via [pattern mapping](./references/pattern-mapping.md).

## Procedure

### 1. Research (read-only)
1. Read **all** notebooks matching `chapter_notebooks/Chapter_<NN>_*.ipynb`. Note: scenario, sample inputs, prompts, expected output, framework-specific tricks.
2. Read the matching sections in `ai-research/agentic-design-patterns-report-1.md` (pattern #NN: problem, risks, MAF mapping) and scan `ai-research/enterprise-agentic-design-patterns-2026-extension-report-2.md` for related enterprise patterns (Bounded Agentic Island, Evaluator-Contract, Safe-Action Gateway, etc.).
3. Check the row in [pattern mapping](./references/pattern-mapping.md) for the MAF primitive and Foundry hook.
4. If MAF API details are uncertain, fetch current docs at `https://learn.microsoft.com/agent-framework/` (C# pivot). MAF packages are preview — **never guess signatures; verify via docs or build errors**.

### 2. Ensure shared solution exists (first run only)
If `dotnet/AgenticDesignPatterns.slnx` (or `.sln`) is missing, create it per [Foundry setup](./references/foundry-setup.md):
- `dotnet/Directory.Build.props` — net10.0, nullable, implicit usings, **shared `UserSecretsId`**.
- `dotnet/Directory.Packages.props` — central package versions.
- `dotnet/src/Shared/` — `FoundrySettings` + `FoundryChatClientFactory` reading `secrets.json` via `Microsoft.Extensions.Configuration.UserSecrets`.
- `dotnet/README.md` — one-time secrets setup.

Auth: Foundry project endpoint + API key stored in `secrets.json` (user-secrets). Never commit secrets, never put keys in `appsettings.json`, never echo keys to console or chat.

### 3. Create the chapter project
Path: `dotnet/src/Chapter<NN>.<PatternName>/` (e.g. `Chapter01.PromptChaining`). Reference `Shared`, add to solution.

Structure:
```
Chapter<NN>.<PatternName>/
├── Chapter<NN>.<PatternName>.csproj
├── Program.cs              # menu/args: runs each demo variant
├── Demos/<Variant>Demo.cs  # one file per variant
├── Models/                 # typed records for structured outputs (if any)
└── README.md               # from ./assets/chapter-readme-template.md
```

### 4. Implement demo variants

| Variant | Required | Purpose |
|---|---|---|
| **MAF + model deployment** | Always | Port of the notebook scenario (same inputs, prompts, expected output) using MAF and the Foundry model deployment via API key. One demo per distinct notebook idea; merge duplicates. |
| **Foundry-native** | Only if Foundry already offers the pattern (e.g. Agent Service tools, hosted agents, Foundry workflows, evaluations, File Search/RAG) | Same scenario implemented with the Foundry capability — typically a **hosted agent** (or Agent Service agent) invoked from MAF — to show both worlds side by side. Skip and state why in the README if Foundry adds nothing. |

Use `ai-research` to shape prompts, naming and README content — not to add extra hardening variants.

Rules:
- Use the MAF primitive from the mapping table (e.g. `AgentWorkflowBuilder.BuildSequential`, `WorkflowBuilder` + executors, `ChatClientAgent`, `AIFunctionFactory`, `ApprovalRequiredAIFunction`).
- Agents are created from the shared `IChatClient`; one `ChatClientAgent` per responsibility with a clear `name`.
- Prefer structured output (`RunAsync<T>` / JSON schema) over "parse the string" when the notebook produces JSON.
- Deterministic code decides correctness (validation, routing rules, termination); the model decides judgment.
- **Prefer non-streaming APIs** for readability: `agent.RunAsync(...)` / `RunAsync<T>(...)` and `InProcessExecution.RunAsync(workflow, input)` then read `run.NewEvents` / `WorkflowOutputEvent`. Use streaming (`RunStreamingAsync`, `WatchStreamAsync`) only when the pattern requires it (e.g. HITL `RequestInfoEvent` loops, or no non-streaming API exists) and say why in a one-line comment.
- Print each step's intermediate output with a header so the pattern is visible when run.
- The MAF variant must run with only `secrets.json` (endpoint, key, deployment). The Foundry-native variant may require extra setup (e.g. Entra ID / `az login`, deployed hosted agent); document it in the README and fail with a clear message if not configured.
- For hosted agents, load the `microsoft-foundry` skill for scaffolding/deploy steps rather than improvising.
- No framework-irrelevant extras: no DI hosts, logging frameworks, or tests unless the pattern needs them.

### 5. Write the chapter README
Copy [README template](./assets/chapter-readme-template.md) and fill **every** section: pattern summary + diagram (mermaid), notebook → C# mapping, setup/run, **when to use / when not**, 3–5 real-world scenarios, Foundry extensions, limitations.

### 6. Validate
1. `dotnet build dotnet/AgenticDesignPatterns.slnx` — must be warning-free for new code.
2. If secrets are configured (`dotnet user-secrets list --project dotnet/src/Shared`), run `dotnet run --project dotnet/src/Chapter<NN>.<PatternName>` and confirm each variant produces sensible output.
3. If no secrets: stop and ask the user to set endpoint, deployment and API key via `dotnet user-secrets` themselves (never request the key in chat).
4. Update the root chapter table in `dotnet/README.md`.

## Completion Checklist
- [ ] All chapter notebooks reviewed; each distinct idea has a MAF demo
- [ ] Foundry-native variant built, or README explains why it is not applicable
- [ ] Builds cleanly; ran against Foundry (or user told why not)
- [ ] README has when-to-use / when-not / real-world scenarios
- [ ] No secrets in repo; `secrets.json` only via user-secrets
