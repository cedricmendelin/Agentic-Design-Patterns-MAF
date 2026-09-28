# Chapter → MAF → Foundry Mapping

| # | Pattern | Notebooks (`chapter_notebooks/`) | Primary MAF primitive (C#) | Optional Foundry hook |
|---|---|---|---|---|
| 01 | Prompt Chaining | `Chapter_01_*` (Code, JSON) | `AgentWorkflowBuilder.BuildSequential`; `WorkflowBuilder` + custom `Executor` for validation gate | Tracing (App Insights), structured outputs |
| 02 | Routing | `Chapter_02_*` (ADK, LangGraph, OpenRouter) | `WorkflowBuilder` with conditional / switch edges; Handoff orchestration | Model router: SLM (Foundry Local) vs LLM deployment |
| 03 | Parallelization | `Chapter_03_*` | `AgentWorkflowBuilder.BuildConcurrent`; fan-out/fan-in edges | Multiple deployments in parallel |
| 04 | Reflection | `Chapter_04_*` | Generator + independent verifier executors in a bounded loop | Foundry evaluators as verifier |
| 05 | Tool Use | `Chapter_05_*` | `AIFunctionFactory.Create`, `ChatClientAgent` tools | Foundry Agent Service tools (Bing grounding, Code Interpreter, File Search), Toolbox |
| 06 | Planning | `Chapter_06_*` | Planner executor + worker agents; Magentic orchestration | Deep research tool in Agent Service |
| 07 | Multi-Agent | `Chapter_07_*` (Sequential, Parallel, Loop, Coordinator, AgentTool, CrewAI) | Sequential / Concurrent / Handoff / GroupChat / Magentic; `agent.AsAIFunction()` for agent-as-tool | Hosted agents |
| 08 | Memory | `Chapter_08_*` | `AgentSession` (thread), `AIContextProvider`, workflow shared state | Foundry conversations / persistent threads |
| 09 | Learning & Adaptation | `Chapter_09_*` | Eval → curated prompt/skill update loop (offline) | Foundry evaluation runs |
| 10 | MCP | `Chapter_10_*` | `ModelContextProtocol` C# SDK client → tools as `AIFunction`; C# MCP server | Foundry MCP tool integration |
| 11 | Goal Setting & Monitoring | `Chapter_11_*` | Bounded loop with typed goal/progress state and budgets | Tracing metrics |
| 12 | Exception Handling | `Chapter_12_*` | Executor retry/fallback, checkpoints | Fallback deployment |
| 13 | Human-in-the-Loop | `Chapter_13_*` | `ApprovalRequiredAIFunction`, `RequestPort`, `RequestInfoEvent` | — |
| 14 | RAG | `Chapter_14_*` | Retrieval tool / `AIContextProvider` | Azure AI Search, Agent Service File Search |
| 15 | A2A | `Chapter_15_*` | MAF A2A hosting & client (`Microsoft.Agents.AI.A2A`) | Hosted agents as A2A endpoints |
| 16 | Resource Optimization | `Chapter_16_*` | Router executor by complexity/budget; token usage from `UsageDetails` | SLM (Foundry Local) + LLM deployments |
| 17 | Reasoning | `Chapter_17_*` | ReAct via tool loop; reasoning model deployment; self-correction loop | o-series / reasoning deployments, Code Interpreter |
| 18 | Guardrails | `Chapter_18_*` | Agent/function middleware, input/output validator executors | Azure AI Content Safety, Prompt Shields |
| 19 | Evaluation | `Chapter_19_*` | LLM-as-judge agent, `Microsoft.Extensions.AI.Evaluation` | Foundry evaluations |
| 20 | Prioritization | `Chapter_20_*` | Agent proposes typed priority → deterministic scheduler | — |
| 21 | Exploration & Discovery | `Chapter_21_*` | Magentic / bounded exploration loop with budgets | Agent Service deep research |

Research sources: `ai-research/agentic-design-patterns-report-1.md` (sections by pattern number), `ai-research/enterprise-agentic-design-patterns-2026-extension-report-2.md` (enterprise hardening).
