# Work Routing

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture and requirements | Architect | Distinct contracts, proposal assessment, scope, acceptance criteria |
| C#/MAF pattern implementation | Coder | Console demos, reusable logic, shared libraries |
| Foundry and hosted agents | Foundry | .Hosted adapters, manifests, containers, runtime configuration |
| Tests and contract verification | Verifier | Deterministic tests, failure paths, console/hosted parity |
| Documentation and diagrams | DevRel | Pattern READMEs, family guides, catalog and diagram accuracy |
| GitHub Actions and build automation | DevOps | CI/CD workflows, scripts, approved issue automation |
| Agentic design patterns research | Researcher | Verified sources, prior-art comparisons, gap proposals |

## Issue assignment

The bare `squad` label tracks Squad work; it is not specialist assignment and does not itself start a native workflow. The specialist labels below identify the accountable member. Labels and name mentions alone do not dispatch cloud work; use the documented `/squad` commands with their authorization and artifact prerequisites.

| Label | Member |
|-------|--------|
| `squad:architect` | Architect |
| `squad:coder` | Coder |
| `squad:foundry` | Foundry |
| `squad:verifier` | Verifier |
| `squad:devrel` | DevRel |
| `squad:devops` | DevOps |
| `squad:researcher` | Researcher |

Labels are definitions until created on GitHub. Do not enable watchers or classic label-triggered workflows as part of this Cast.

## Ownership and review boundaries

- Select one accountable specialist and add only bounded collaboration required by the work.
- Coder owns reusable logic and shared Foundry clients/settings; Foundry reviews the Foundry-specific behavior and owns thin hosted adapters.
- Foundry defines hosting requirements; DevOps owns the executing workflow. Neither responsibility grants cloud-operation authorization.
- Verifier checks behavioral acceptance criteria and console/hosted contract parity. Architect reviews design changes; DevRel reviews educational clarity.
- Researcher drafts source-backed proposals; Architect assesses fit and refines scope. Explicit user approval precedes implementation issue creation.
- Scribe, Ralph, Rai and Fact Checker are mandatory support agents, not routing destinations.
- Team review procedures do not configure GitHub branch protection or authorize a merge.
