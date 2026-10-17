# Chapter 04 - Hosted Reflection Agent

Expose the [Chapter 04 reflection workflow](../Chapter04.Reflection/README.md) through Microsoft Foundry Agent Service's **Responses protocol**. MAF provides the bounded generator/verifier graph; Foundry hosts the agent and model.

## Request and response

| Input | Behavior |
|---|---|
| `fact-check: <topic>` | Generate a paragraph and review its accuracy and qualifications |
| `product: <details>` or unprefixed text | Write and refine product copy against house-style criteria |
| `code-review: <Python task>` | Generate Python code and review it against the supplied requirements |

The product scenario deliberately gives the writer misleading length/style guidance while the verifier enforces a hidden house style, making the feedback loop visible.

Every round includes generation and review. Acceptance ends the workflow immediately; otherwise feedback returns to the generator while budget remains. The default budget is three rounds, or at most six agent invocations excluding provider retries.

The final response includes the draft, review, rounds used, and `Outcome: Accepted` or `Outcome: MaxRoundsReached`. Budget exhaustion returns an explicitly **not accepted** draft. Malformed reviews, empty drafts, provider failures, and cancellation fail the request.

Each request creates fresh agents without persisted conversation history. The caller receives the final result after the workflow completes; intermediate stages are available in configured traces.

## Prerequisites and configuration

Follow the [shared hosted-agent setup](../../README.md#configure-and-deploy-hosted-agents) to install tooling, select your Foundry project and model, configure `azure.yaml`, and set up authentication and permissions.

The host reads shared `Foundry` user-secrets and `Foundry__*` environment overrides. A blank or absent API key selects `DefaultAzureCredential`: your developer identity locally and managed identity in Azure. Keep API keys out of hosted configuration.

## Run locally

From the repository root:

```powershell
dotnet run --project .\dotnet\src\Chapter04.Reflection.Hosted
```

The local server listens on port 8088 by default; keep it private. In a second terminal, from `dotnet`:

```powershell
azd ai agent invoke chapter04-reflection --local --protocol responses 'fact-check: Explain why plants use photosynthesis.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'product: A mug keeps coffee hot and is controlled by a smartphone app.'
azd ai agent invoke chapter04-reflection --local --protocol responses 'code-review: Write a Python factorial function with a docstring and negative-input validation.'
```

Stop the server with Ctrl+C when finished.

## Deploy and invoke

After completing the shared setup, run from `dotnet`:

```powershell
azd deploy chapter04-reflection --no-prompt
azd ai agent show chapter04-reflection --output json
```

Use the returned `instance_identity.principal_id` to configure runtime model-inference access as described in the shared guide. Allow time for role propagation, then send a request:

```powershell
azd ai agent invoke chapter04-reflection --protocol responses 'product: A reusable insulated mug with app controls.'
```

The service uses bundled .NET 10 code deployment, publishing the host and referenced Chapter 04 and Shared projects together. No Docker image or container registry is required.

## Telemetry and sensitive data

Agents emit spans under `Chapter04.Reflection`. **Sensitive-data capture is enabled for this demo:** prompts, drafts, reviews, and responses may appear in traces when an exporter is configured. Use non-sensitive inputs, restrict telemetry access and retention, and disable sensitive capture before handling confidential data.

## Adapting evaluation examples

[eval.yaml](eval.yaml), the [sample dataset](datasets/chapter04-reflection-smoke/chapter04-reflection-smoke_dg.jsonl), and the [rubric](evaluators/chapter04-reflection-smoke/rubric_dimensions.json) are starting points for a task-specific evaluation. Align examples and scoring with your request contract, outcome fields, acceptance criteria, and Python tasks before use. Review prefix matching, sentence-count rules, and code-format requirements rather than assuming they are requirements of the reflection pattern. Synthetic candidate responses are examples, not measured agent outputs.

Use your own project, model, dataset, and evaluator references when configuring evaluations. Evaluation runs incur additional model usage.

## Pattern tradeoffs

A model-based reviewer can reinforce the generator's errors; a separate role is not independent evidence. Ground factual claims in trusted sources, run generated code through deterministic checks, and use human review for consequential decisions.

Revision loops add latency and cost without guaranteeing improvement. Keep a bounded budget and distinguish reviewer acceptance from budget exhaustion.
