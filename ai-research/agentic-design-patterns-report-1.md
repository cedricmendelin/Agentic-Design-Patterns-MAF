# Agentic Design Patterns with Microsoft Foundry and Microsoft Agent Framework

**Research date:** September 2026
**Implementation language:** C#
**Primary reference:** Antonio Gulli, *Agentic Design Patterns*

---

# Executive Summary

Agentic systems are best understood not as “an LLM plus many agents,” but as **controlled loops around models, tools, state, context, and evidence**.

Antonio Gulli’s *Agentic Design Patterns* provides a useful catalog of recurring agentic techniques. However, the patterns operate at different architectural levels:

1. **Orchestration patterns**
   - Prompt chaining
   - Routing
   - Parallelization
   - Planning
   - Multi-agent collaboration
   - Human-in-the-loop
2. **Agent capabilities**
   - Tool use
   - Memory
   - RAG
   - MCP
   - Reasoning
   - Learning and adaptation
3. **Production controls**
   - Guardrails
   - Exception recovery
   - Evaluation
   - Resource optimization
   - Goal monitoring

For enterprise architecture and teaching purposes, the patterns can be grouped into five memorable families.

## 1. Flow

How work moves through the system:

- Prompt chaining
- Routing
- Parallelization
- Planning
- Prioritization

## 2. Reason and Improve

How agents reason about and improve their work:

- Reasoning techniques
- Reflection
- Learning and adaptation
- Goal monitoring
- Exploration and discovery

## 3. Knowledge and Action

How agents obtain information and interact with systems:

- RAG
- Memory
- Tool use
- MCP

## 4. Collaboration

How multiple actors cooperate:

- Multi-agent collaboration
- Inter-agent communication
- A2A
- Human-in-the-loop

## 5. Trust and Operations

How agentic systems become production systems:

- Guardrails
- Exception recovery
- Resource optimization
- Evaluation
- Monitoring

---

# Key Architectural Conclusion

The most important lesson is:

> **Control before autonomy.**

Start with:

```text
One Agent
   ↓
Typed Tools
   ↓
Explicit Limits
   ↓
Tracing
   ↓
Evaluation
```

Only introduce additional orchestration when the problem requires it.

A useful progression is:

```text
Single Agent
    ↓
Deterministic Workflow
    ↓
Reflection / Verification
    ↓
Routing / Parallelization
    ↓
Planner–Worker
    ↓
Dynamic Multi-Agent System
```

Multi-agent architecture should therefore be an **optimization for decomposition**, not the default architecture.

---

# Microsoft Architecture Mapping

Microsoft Agent Framework maps naturally to the pattern landscape.

## Agent level

Typical abstractions:

- `AIAgent`
- `ChatClientAgent`
- `IChatClient`
- Function tools
- MCP tools
- Context providers
- Agent sessions
- Middleware

## Workflow level

Typical abstractions:

- `WorkflowBuilder`
- Executors
- Edges
- Conditional edges
- Switch edges
- Shared workflow state
- Checkpoints
- `RequestPort`

## Multi-agent orchestration

Microsoft Agent Framework provides orchestration patterns including:

- Sequential
- Concurrent
- Handoff
- Group Chat
- Magentic

## Microsoft Foundry

Foundry contributes platform capabilities such as:

- Model deployments
- Agent Service
- Hosted Agents
- Conversations and state
- Toolbox
- MCP integration
- Identity
- Evaluation
- Tracing
- Application Insights integration
- Foundry Local

---

# The 21 Agentic Design Patterns

## 1. Prompt Chaining

### Problem

A complex task is easier to solve as a sequence of smaller transformations.

### Pattern

```text
Input
  ↓
Agent A
  ↓
Intermediate Result
  ↓
Agent B
  ↓
Intermediate Result
  ↓
Agent C
  ↓
Output
```

### Good for

- Document pipelines
- Research → draft → review
- Extraction → transformation → generation
- Structured business processes

### Risks

- Serial latency
- Error propagation
- Excessive token usage

### Microsoft Agent Framework

Use:

- Sequential orchestration
- `WorkflowBuilder`
- Typed messages between executors

---

## 2. Routing

### Problem

Different tasks require different specialists, tools, or models.

### Pattern

```text
   Request
      ↓
    Router
 ┌────┼────┐
 ↓    ↓    ↓
 A    B    C
```

### Good for

- Intent routing
- Specialist agents
- Model routing
- Tool routing

### Risks

Misrouting becomes a hidden failure mode.

### Microsoft implementation

Use:

- Conditional workflow edges
- Switch edges
- Handoff orchestration
- Custom router executors

---

## 3. Parallelization

### Problem

Independent work can execute concurrently.

### Pattern

```text
               ┌→ Worker A ─┐
Input → Fanout ┼→ Worker B ─┼→ Aggregate
               └→ Worker C ─┘
```

### Good for

- Research
- Document processing
- Ensemble evaluation
- Map-reduce

### Microsoft implementation

Use:

- Concurrent orchestration
- Fan-out/fan-in workflows

---

## 4. Reflection

### Problem

Initial model output may contain errors or omissions.

### Pattern

```text
Generate
   ↓
Critique
   ↓
Revise
   ↓
Validate
```

### Important limitation

Self-reflection is not independent verification.

A stronger enterprise version is:

```text
Generator
   ↓
Independent Verifier
   ↓
Acceptance Contract
   ↓
Retry / Accept
```

### Research connection

Madaan et al., *Self-Refine: Iterative Refinement with Self-Feedback*, NeurIPS 2023.

---

## 5. Tool Use

### Problem

Models cannot directly access enterprise systems or current information.

### Pattern

```text
Reason
  ↓
Select Tool
  ↓
Execute
  ↓
Observe
  ↓
Continue
```

### Research connection

Yao et al., *ReAct: Synergizing Reasoning and Acting in Language Models*, ICLR 2023.

### Microsoft implementation

- Function tools
- MCP
- Foundry Toolbox
- Approval-required tools

---

## 6. Planning

### Problem

The solution requires multiple unknown steps.

### Pattern

```text
Goal
  ↓
Plan
  ↓
Tasks
  ↓
Execute
  ↓
Monitor
  ↓
Replan
```

### Microsoft implementation

- Planner executor
- Harness todo state
- Magentic orchestration
- Workflow state

---

## 7. Multi-Agent Collaboration

### Problem

Different responsibilities benefit from separate contexts, tools, identities, or expertise.

### Microsoft implementation

- Sequential
- Concurrent
- Handoff
- Group Chat
- Magentic

### Enterprise rule

Do not create agents merely to represent personas.

Create separate agents when they have meaningfully different:

- Tools
- Permissions
- Context
- Models
- Responsibilities
- Lifecycle

---

## 8. Memory Management

Useful memory categories:

### Working memory

Current conversation and task state.

### Episodic memory

Previous experiences and outcomes.

### Semantic memory

Facts and knowledge.

### Procedural memory

Instructions and reusable procedures.

Microsoft mapping:

- `AgentSession`
- Context providers
- External stores
- Workflow state
- Agent Skills

---

## 9. Learning and Adaptation

Production systems should generally avoid uncontrolled self-modification.

Prefer:

```text
Production Runs
      ↓
Evaluation
      ↓
Curated Findings
      ↓
Reviewed Skill / Prompt / Policy Update
      ↓
Evaluation
      ↓
Deployment
```

---

## 10. MCP

Model Context Protocol standardizes interaction between agents and external capabilities.

MCP should be understood as:

> **An interoperability protocol, not an agent architecture.**

Use it for:

- Tools
- Resources
- Prompts
- Capability discovery

Enterprise controls remain necessary:

- Authentication
- Authorization
- Tool allow-lists
- Network restrictions
- Auditing

---

## 11. Goal Setting and Monitoring

Agents need explicit success criteria.

Track:

- Goal
- Subgoals
- Progress
- Remaining work
- Token budget
- Time budget
- Tool-call budget
- Completion criteria

---

## 12. Exception Handling and Recovery

Production agents require:

- Retry policies
- Timeouts
- Fallbacks
- Checkpoints
- Idempotency
- Compensation
- Human escalation

Do not simply rerun the entire agent.

---

## 13. Human-in-the-Loop

Use humans for:

- Irreversible actions
- High-risk decisions
- Ambiguous policy decisions
- Missing information
- Escalation

Microsoft Agent Framework supports approval and request/response workflow patterns.

---

## 14. RAG

RAG supplies factual context.

```text
Question
  ↓
Retrieve
  ↓
Rank
  ↓
Context
  ↓
Agent
```

RAG should not be confused with:

- Memory
- Skills
- Tools
- MCP

---

## 15. Inter-Agent Communication / A2A

A2A enables independently deployed agents to communicate.

Like MCP:

> **A2A is a protocol boundary, not an orchestration architecture.**

Use when agents have independent:

- Deployment
- Ownership
- Identity
- Lifecycle

---

## 16. Resource-Aware Optimization

Agents should operate within explicit budgets:

- Tokens
- Cost
- Latency
- Tool calls
- Iterations
- Parallel workers
- Wall-clock time

This becomes especially important for autonomous loops.

---

## 17. Reasoning Techniques

Examples include:

- ReAct
- Decomposition
- Plan-and-execute
- Verifier-guided reasoning

The important production principle is not exposing chain-of-thought but designing an observable execution structure.

---

## 18. Guardrails and Safety

Guardrails should exist at multiple boundaries:

```text
Input
  ↓
Policy
  ↓
Agent
  ↓
Tool Selection
  ↓
Authorization
  ↓
Execution
  ↓
Output Validation
```

Do not rely solely on prompt instructions.

---

## 19. Evaluation and Monitoring

Evaluate both:

### Outcome

Was the answer correct?

### Trajectory

Did the agent:

- choose appropriate tools?
- use correct arguments?
- follow policy?
- retry unnecessarily?
- exceed budgets?
- reach the correct stop condition?

Microsoft mapping:

- OpenTelemetry
- Foundry evaluation
- Application Insights
- Agent Framework evaluators

---

## 20. Prioritization

Agents may propose priorities, but deterministic code should generally schedule work.

```text
Agent proposes priority
        ↓
Typed Priority
        ↓
Business Rules
        ↓
Scheduler
```

---

## 21. Exploration and Discovery

Useful for:

- Research
- Investigation
- Unknown solution spaces

Always bound exploration by:

- Time
- Cost
- Tool calls
- Domains
- Permissions
- Stop conditions

---

# Newer Patterns

## Manager-Led / Magentic Orchestration

A manager maintains:

- Task ledger
- Progress ledger
- Specialist assignments
- Replanning state

Pattern:

```text
Manager
  ↓
Plan
  ↓
Delegate
  ↓
Workers
  ↓
Observe
  ↓
Update Ledger
  ↓
Replan
```

Research connection:

Fourney et al., *Magentic-One: A Generalist Multi-Agent System for Solving Complex Tasks*, 2024.

---

# Context Engineering

Context should be treated as a managed resource.

Useful categories:

```text
Goal
Plan
Current State
Trusted Evidence
Recent Tool Results
Errors
Open Questions
```

Remove or compact:

- obsolete tool output
- duplicate evidence
- old reasoning
- irrelevant conversation

---

# Agent Skills

Skills represent reusable procedural knowledge.

A useful distinction:

| Concept | Purpose |
|---|---|
| Prompt | Immediate instructions |
| RAG | Retrieve facts |
| Memory | Remember experience |
| Tool | Perform an action |
| MCP | Transport/discover capabilities |
| Skill | Teach a reusable procedure |

Skills should be:

- Versioned
- Tested
- Reviewed
- Governed
- Discoverable
- Progressively loaded

---

# Durable Agents

Long-running agents require:

- Checkpoints
- Resume
- Timers
- External events
- Human waits
- Correlation IDs
- Idempotency
- Durable state

This is especially important for enterprise workflows lasting minutes, hours, or days.

---

# Hybrid SLM + LLM Architecture

A particularly promising pattern is heterogeneous model routing.

## SLM tasks

Use smaller/local models for:

- Classification
- Extraction
- Formatting
- Structured transformation
- Chunk processing
- Tool argument generation

## LLM tasks

Use larger models for:

- Planning
- Ambiguous reasoning
- Cross-document synthesis
- Novel exceptions
- Complex judgment

## SLM-first fallback

```text
Task
  ↓
 SLM
  ↓
Verifier
  ├─ Pass → Result
  └─ Fail → LLM
```

## Predictive routing

```text
Task
  ↓
Router
  ├─ Simple  → SLM
  └─ Complex → LLM
```

## Map-reduce

```text
     Documents
         ↓
      Chunks
 ┌───────┼───────┐
 ↓       ↓       ↓
SLM     SLM     SLM
 └───────┼───────┘
         ↓
     Cloud LLM
         ↓
     Synthesis
```

Filip W.’s .NET Day Switzerland material demonstrates several of these architectures using Microsoft Agent Framework and Foundry Local.

---

# Research Evidence

## ReAct

**Yao et al.**
*ReAct: Synergizing Reasoning and Acting in Language Models.*
ICLR, 2023.

### Research question

Can reasoning and external actions be interleaved to improve language-model task performance?

### Method

Evaluated on:

- HotpotQA
- FEVER
- ALFWorld
- WebShop

### Findings

- Tool interaction reduces unsupported reasoning.
- Observations allow models to correct assumptions.
- ReAct performs particularly well on interactive tasks.
- Reasoning and acting complement each other.

### Evidence quality

Strong benchmark evidence, but results remain model-, prompt-, and environment-dependent.

---

## Self-Refine

**Madaan et al.**
*Self-Refine: Iterative Refinement with Self-Feedback.*
NeurIPS, 2023.

### Research question

Can an LLM improve its own output through iterative feedback?

### Findings

Iterative critique and revision improved performance across multiple tasks.

### Limitation

The same model may reproduce its original blind spots.

Enterprise systems should therefore prefer independent verification where possible.

---

## Reflexion

**Shinn et al.**
*Reflexion: Language Agents with Verbal Reinforcement Learning.*
NeurIPS, 2023.

### Pattern

```text
Actor
  ↓
Evaluator
  ↓
Reflection
  ↓
Episodic Memory
  ↓
Next Attempt
```

### Key insight

Agents can use textual feedback from previous attempts as episodic memory.

---

## Magentic-One

**Fourney et al.**
*Magentic-One: A Generalist Multi-Agent System for Solving Complex Tasks.*
Microsoft Research, 2024.

### Architecture

Manager-led multi-agent orchestration with:

- Planning
- Delegation
- Progress tracking
- Replanning
- Specialist agents

### Evidence

Promising benchmark results across general agent tasks.

### Confidence

Moderate.

Multi-agent systems remain substantially more expensive and operationally complex than deterministic workflows.

---

# Cross-Paper Synthesis

## Strong consensus

External tools and observations improve grounding.

Explicit feedback and validation improve reliability.

Bounded iteration is preferable to unconstrained autonomous loops.

## Moderate consensus

Task decomposition and specialization help when responsibilities genuinely differ.

## Still unresolved

Whether multi-agent role-playing itself provides enough benefit to justify:

- Additional tokens
- Additional latency
- Coordination complexity
- Failure modes

---

# Enterprise Architecture Recommendation

The default architecture should be:

```text
User / Event
    ↓
Deterministic Workflow
    ↓
Agentic Decision
    ↓
Typed Result
    ↓
Policy / Validation
    ↓
Tool / Action
    ↓
Checkpoint
    ↓
Evaluation
```

Not:

```text
User
  ↓
Autonomous Agent
  ↓
Everything
```

---

# Recommended Talk Narrative

## Demo 1 — Tool-Using Agent

Show:

- One agent
- Two typed tools
- ReAct-style loop
- Trace

## Demo 2 — Draft / Review / Revise

Show:

```text
Research
  ↓
Draft
  ↓
Critic
  ↓
Revision
```

Add:

- Structured state
- Maximum iterations
- Acceptance criteria

## Demo 3 — Hybrid SLM / LLM

Show:

```text
Request
  ↓
Router
  ├─ SLM
  └─ LLM
```

Then demonstrate escalation.

## Demo 4 — Magentic

Use as the advanced finale.

Compare:

- Number of model calls
- Latency
- Cost
- Trace complexity

against the deterministic workflow.

---

# Recommended Reading

1. Antonio Gulli — *Agentic Design Patterns*
2. Yao et al. — *ReAct*
3. Shinn et al. — *Reflexion*
4. Madaan et al. — *Self-Refine*
5. Fourney et al. — *Magentic-One*
6. Microsoft Agent Framework documentation and C# samples
7. Microsoft Foundry Agent Service documentation
8. Filip W.’s hybrid Agent Framework samples

---

# Final Takeaway

The most useful mental model is:

> **Agentic design is not primarily about creating more agents. It is about deciding where probabilistic reasoning belongs inside a controlled software architecture.**

The strongest systems combine:

- deterministic workflows
- bounded agent loops
- typed tools
- explicit context
- governed memory
- verification
- human approval
- durable state
- observability
- evaluation
- least privilege

The model provides intelligence.

The surrounding architecture provides reliability.
