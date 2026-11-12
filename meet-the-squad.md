# Meet the Squad

**Naming mode:** Descriptive. These seven functional names preserve the team approved for Agentic-Design-Patterns-MAF.

| Name | Role | Specialty | How to talk |
|------|------|-----------|-------------|
| Architect | Lead / Architect | Pattern fit, contracts, requirements and acceptance criteria | `squad:architect` for architecture ownership |
| Coder | Backend Developer | Runnable C#/MAF patterns, reusable logic and shared clients | `squad:coder` for implementation ownership |
| Foundry | AI Engineer | Microsoft Foundry and thin hosted-agent companions | `squad:foundry` for hosted-agent ownership |
| Verifier | Test Engineer | Deterministic tests and console/hosted contract parity | `squad:verifier` for test ownership |
| DevRel | Technical Writer | Educational documentation, catalog and diagram accuracy | `squad:devrel` for documentation ownership |
| DevOps | Infrastructure Engineer | GitHub Actions, builds and approved automation | `squad:devops` for workflow ownership |
| Researcher | AI Research Engineer | Verified agentic-pattern sources, comparisons and proposals | `squad:researcher` for research ownership |

## How to work with your Squad

Local sessions use the Squad agent and the same Cast and `.squad/routing.md`; an issue is not required for local work.

After the workflow installation is reviewed and merged to the default branch, native cloud commands arrive as GitHub issue comments. Label-based assignment uses the exact specialist labels above (recommended color `9B8FCC`). The bare `squad` label tracks work, not specialist assignment. Labels and name mentions alone do not dispatch a workflow.

Start a scoped issue with `/squad research` to request source-backed findings, then `/squad plan` to turn those findings into an implementation proposal. Use `/squad plan revise` for feedback. Planning does not authorize implementation. Acceptance, activation and implementation commands have separate permission and artifact prerequisites; do not invoke them until their actions are explicitly approved.

Researcher drafts proposals; Architect assesses fit and refines requirements. A separate Requirements Engineer is unnecessary for this team. Preserve human approval before creating implementation issues, provisioning or deploying resources, changing RBAC, accessing secrets, making paid live calls or merging.

## What happened here

The repository teaches agentic design patterns through runnable C# and Microsoft Agent Framework demos. Its .NET solution, deterministic contract tests, protected live Foundry smoke tests and educational documentation need distinct implementation, hosting, verification and teaching ownership.

Coder owns reusable pattern logic; Foundry owns hosted integration without duplicating that logic. Researcher contributes verified prior art; Architect owns fit, scope and acceptance criteria. DevOps owns workflow execution, with Foundry supplying hosting-specific requirements. This division preserves the approved seven-member design rather than accepting an unrelated generated team.

The CLI and workflow package are pinned to Squad 2.0.0-preview.4. The Cast uses the registry/history pair format understood by both; existing IDs, roles, creation times and prior casting history are preserved. The compact coordinator is shared by local and cloud sessions. Do not run a standalone coordinator upgrade over it without revalidating the cloud Cast.

The pinned package includes automatic bootstrap and retrospective triggers. Those triggers require a separate rollout decision; this Cast is not approval for unattended issue creation or paid runs. Existing protected Foundry smoke-test safeguards remain unchanged.

Cast adapted: 2026-11-12.
