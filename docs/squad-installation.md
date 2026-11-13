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

## Rollout and approval gates

- **Bootstrap (chosen):** Keep the upstream onboarding workflow enabled and review its generated proposal. It proposes a Cast PR and an initial proposals issue when the installation lands on the default branch. A pre-existing team does not suppress that proposal, but no team is replaced without merging the Cast PR. Our seven-member team is already prepared, so review any proposed recasting against the approved identities and ownership rather than accepting it automatically. With the current PR-permission toggle, bootstrap uses a branch and tracking-issue fallback rather than opening the PR directly. Bootstrap is registered and active.
- **Retro (chosen):** Keep the original pinned workflow unchanged and disabled in GitHub Actions during normal operation. Removing its schedules would break exact source/lock verification. Retro is not yet registered, so its disabled state cannot be established before merge. Immediately after an explicitly approved merge registers it on the default branch, run the commands below and confirm `disabled_manually` before considering rollout complete. This flag is stored on GitHub, not in these files; until it is confirmed, automatic scheduled runs remain possible. For an explicitly approved manual test, enable it briefly, run it from Actions, then disable it again and recheck the state. Enabling also restores the six-hourly and weekly schedules, so this is not a truly manual-only workflow. Automatic implementation remains off by default. No remote disable or test has been performed yet.

  ```powershell
  gh workflow disable squad-retro.lock.yml --repo cedricmendelin/Agentic-Design-Patterns-MAF
  gh api repos/cedricmendelin/Agentic-Design-Patterns-MAF/actions/workflows/squad-retro.lock.yml --jq .state
  ```

- **Publishing:** The installation requires PR review and explicit human merge. Keep Bootstrap's proposal review separate from approval of implementation work. Complete the post-merge Retro disable step before an explicitly approved, scoped `/squad research` followed by `/squad plan` pilot. No Squad cloud run has been started during local preparation.

See [Meet the Squad](../meet-the-squad.md) for everyday usage and the [upstream enlistment guide](https://github.github.com/squad/docs/guide/gh-aw/) for the standard package contract.

## Research and planning runtime repair

The November 12 research and plan pilots restored their inline skills correctly,
but the pinned gh-aw `v0.91.5` SDK driver did not pass `skillDirectories` into
`createSession`. The agent saw only built-in skills and returned
`Skill not found: squad-research` (and the equivalent planning errors). This was
not a missing repository skill or an authentication failure. Some research runs
reported a `noop` and ended green without delivering an artifact.

### Research evidence and alternatives

- [Research run 43313575145](https://github.com/cedricmendelin/Agentic-Design-Patterns-MAF/actions/runs/43313575145):
  the activation artifact contains `squad-research/SKILL.md` and the planning
  skills; the restore step copies them into the workspace. The agent session
  records `Skill not found: squad-research` and a `noop`, despite a successful
  run conclusion.
- [Plan run 43316814969](https://github.com/cedricmendelin/Agentic-Design-Patterns-MAF/actions/runs/43316814969):
  the command guard rejects `missing_tool` for open plan mode, and the conclusion
  reports `missing_terminal_safe_output`. A missing skill is not evidence that
  authentication or the command router failed.
- [Immutable gh-aw SDK session implementation](https://github.com/github/gh-aw/blob/8e829d4c667c50eaa5c54b61ae6bdf8b2b118e9f/actions/setup/js/copilot_sdk_session.cjs):
  `createSession` receives neither `skillDirectories` nor session-level
  `workingDirectory`. Setting the client working directory alone did not expose
  project skills in these observed sessions.

Copying more skills into the repository does not fix a session that never loads
them. Patching the downloaded SDK driver would couple this repository to upstream
runtime internals. Upgrading the entire compiler/package would widen the change.
Selecting the existing CLI path is the narrower workaround; it does not claim
to repair the upstream SDK implementation.

### Repair and validation boundary

The repository's `squad.md` now sets `engine.copilot-sdk: false`, selecting the
supported CLI path. Copilot CLI `1.0.78` discovers all four required research,
plan, planning-policy and planning-ontology skills from the restored activation
artifact. The compiler, CLI, action and workflow package pins remain unchanged;
authorization, evidence and safe-output guards remain enabled. A missing mode
skill or planning prerequisite must use `report_incomplete`, not `noop`.
The native `web-fetch` declaration is removed because the compiler disallows it
in offline BYOK CLI mode. Research instead fetches reachable primary sources
with `curl --fail --location` through the existing Bash tool, under the same
network policy. Unreachable sources still require an explicit disclosure.

The compiled CLI invocation uses `--allow-all-tools` rather than the SDK's
per-tool permission handler. This is the compiler's standard CLI behavior, not
identical tool permissions: review that tradeoff before merging. Network
isolation, credential exclusion, command authorization, threat detection and
safe-output validation remain in the compiled workflow.

Local checks used the checksum-verified Copilot `1.0.78` release with
`--no-auto-update skill list` against the downloaded activation artifact,
confirming discovery of all four required skills. Strict compilation with
gh-aw `v0.91.5` succeeds and removes the SDK driver invocation. This proves
discovery and compilation, not a successful live research or planning run.

This is an intentional local deviation from the installed upstream workflow;
the package ownership hashes remain the original provenance, not a claim that
the customized source is byte-identical. Exact package verification will report
this drift, and reinstalling the package can overwrite the repair. Recompile
with the pinned compiler after changes:

```bash
gh aw compile squad --strict
```

After deployment, pilot `/squad research` on one backlog issue, confirm its
bot-authored research artifact and lifecycle update, then run `/squad plan` and
confirm its plan and lifecycle update. A green run or historical binding check
alone does not prove completion. Do not activate implementation as part of this
pilot.
