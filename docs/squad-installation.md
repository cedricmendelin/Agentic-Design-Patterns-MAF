# Squad installation and design choices

This records the Windows setup prepared on 2026-11-12. The installation PR is a human-review boundary; merging, cloud rollout and first-run checks remain separate from preparing these files.

## Steps we completed

1. **Installed and initialized the original CLI.** We initially installed Squad 1.1.0 through WinGet and ran `squad init --no-workflows`. We cast and customized seven specialists instead of enabling the classic label-triggered workflows.

2. **Moved to the matching preview CLI.** The cloud package requires the 2.0 preview. We switched nvm to the already-installed Node 24.15.0, removed the WinGet copy, and installed the npm CLI globally:

   ```powershell
   nvm use 24.15.0
   winget uninstall --id bradygaster.Squad
   npm install -g @bradygaster/squad-cli@2.0.0-preview.4
   squad --version
   ```

   Node must be at least 22.5.0. Global npm packages belong to the active nvm Node version. We stopped the old Squad MCP process to release the WinGet executable, then restarted the app after upgrading.

3. **Installed the exact gh-aw compiler.**

   ```powershell
   gh extension install --pin v0.91.5 github/gh-aw
   gh aw --version
   ```

   Remove an existing mismatched gh-aw extension first. Do not substitute a newer compiler for this package.

4. **Installed the pinned workflow package.** We selected the published Squad 2.0.0-preview.4 revision, not a moving branch:

   ```powershell
   $SquadSha = 'b1f25353553c22d0bb8d42faae14ec321c50b6c7'
   gh aw add "github/squad/workflows@$SquadSha"
   ```

   We checked the package identity, accepted only the documented safe-update surface, removed only gh-aw's generated `.github\skills\agentic-workflows` router, and preserved the repository's `maf-pattern-demo` skill. The final package contains eight workflow source/lock pairs and their required runtime resources.

5. **Upgraded and reconciled the team.** `squad upgrade` refreshed local tooling but also created eleven classic workflows; we removed those newly created workflows. Upgrading alone did not make the team cloud-compatible. We preserved the seven identities and prior history, converted casting metadata to the registry/history pair format accepted by both runtimes, added the required roster sections, normalized `rai` casing, and wrote the compact GH-AW coordinator and `meet-the-squad.md`. We regenerated capabilities from the team, routes and charters. Do not blindly run `squad upgrade` over this coordinator.

6. **Checked GitHub settings without changing them.** Issues and Actions were already enabled. Default workflow permissions were already `read`, with Actions PR creation/approval disabled. The compiled Copilot engine uses `github.token`; no new secret was added. Cloud entitlement and actual execution still need a first-run check. The local MCP bridge works; its machine-specific `.mcp.json` remains unstaged.

7. **Validated and staged the intended files.** The full Cast validator, local `squad health`/`doctor`, strict compilation and staged-package verification passed. We kept only the two documented compiler warnings, ignored downloaded logs, excluded local runtime state, and left existing .NET CI and protected Foundry smoke tests unchanged.

   ```powershell
   node .github\workflows\shared\squad-install-verifier.mjs --materialize-runtime
   gh aw compile --strict
   node .github\workflows\shared\squad-install-verifier.mjs --verify-install --source-revision $SquadSha --strict-compile
   # Stage only reviewed installation and concrete Cast paths, never git add -A.
   node .github\workflows\shared\squad-install-verifier.mjs --verify-staged-install --stage-ownership --source-revision $SquadSha
   ```

## Design choices

| Specialist | Responsibility |
|------------|----------------|
| Architect | Pattern fit, architecture, requirements and acceptance criteria |
| Coder | Reusable C#/MAF pattern logic and shared clients |
| Foundry | Thin hosted adapters, Foundry integration and hosting requirements |
| Verifier | Deterministic tests and console/hosted parity |
| DevRel | Educational documentation and diagrams |
| DevOps | Builds, GitHub Actions and approved automation |
| Researcher | Verified prior art, pattern comparisons and proposal drafts |

Scribe, Ralph, Rai and Fact Checker are fixed support agents, not domain specialists. Researcher and Architect share requirements refinement; no separate Requirements Engineer is needed. Local Squad sessions do not require an issue. Cloud work uses explicit `/squad` issue comments; specialist labels identify ownership but do not themselves dispatch a workflow.

Research and plans do not authorize implementation issue creation, paid calls, cloud operations, secret access or merges. Keep automatic implementation off and retain independent human review.

## Rollout decisions still pending

- **Bootstrap:** The upstream onboarding workflow proposes a Cast PR and an initial proposals issue when the installation lands on the default branch. A pre-existing team does not suppress that proposal, but no team is replaced without merging the Cast PR. Our seven-member team is already prepared, so automatic casting is redundant. With the current PR-permission toggle, bootstrap uses a branch and tracking-issue fallback rather than opening the PR directly.
- **Retro (chosen):** Keep the original pinned workflow unchanged and disabled in GitHub Actions during normal operation. Removing its schedules would break exact source/lock verification. Once registered on the default branch, verify its disabled state as part of rollout; this flag is stored on GitHub, not in these files. For an explicitly approved manual test, enable it briefly, run it from Actions, then disable it again. Enabling also restores the six-hourly and weekly schedules, so this is not a truly manual-only workflow. Automatic implementation remains off by default. No remote disable or test has been performed yet.
- **Publishing:** The installation requires PR review and explicit human merge. Resolve bootstrap behavior and the Retro disable procedure before merging, then explicitly approve a scoped `/squad research` followed by `/squad plan` pilot. No Squad cloud run has been started during local preparation.

See [Meet the Squad](../meet-the-squad.md) for everyday usage and the [upstream enlistment guide](https://github.github.com/squad/docs/guide/gh-aw/) for the standard package contract.
