# poc-00-header — header capability + PoC skeleton

## Why

Epic #1 (ticket POC-00, section `### POC-00 · Observar un worktree y dibujar su cabecera`) asks for the first cut of the PoC: a reading that answers, for a given worktree directory, the eight header facts the harness `837c7ca` shows — pull request, declared checks, changed files `+/-`, ahead/behind, merge conflicts, Seam comparison against the resolved issue's Seam section, and clean tree — plus the issue context itself. Each absent fact must be named with its remedy, not folded into zero.

This change also stands up the skeleton the rest of the PoC builds on: `src/Plugin` (.NET 10 minimal API, single project), `src/web` (React 19.3 + Vite 6 + TS 5.7.3), `AGENTS.md`, `gate.sh`, and the self-declared `.harness/commands.json`.

**Assumptions (POC-00 owns these decisions, recorded per orchestration notes):**

1. **Resolved-issue direction** — the task key a branch proposes is its last digit run (like harness `taskFor`/`keyInBranch` in `837c7ca`), confirmed by GitHub issue lookup via Octokit. Branch digits alone never resolve an issue; the lookup's failure refuses the proposal.
2. **Subject #2 for the two-subject check** is this repo's own second worktree `/var/folders/qq109xk14c14mktfg3ysl9x40000gn/T/opencode/wt-poc-00` once this branch carries real changes (the PoC verifies itself as first subject — a clean worktree with no issue/PR shows the eight absences). Practically: fixtures + tests exercise both, and `design.md` documents the human reproduction.
3. **tests location** — `src/Plugin.Tests` (sibling project, not `tests/`), so `dotnet test` finds it next to the source it covers.
4. **web gate** — `tsc --noEmit` + `vite build` via `npm run` scripts (no eslint config yet; recorded as debt for the orchestrator if lint is wanted).
5. Seam is IN scope (8 of 8, not the pre-authorized 7-of-8 fallback) as measured below.

## What Changes

- **New** `src/Plugin` — .NET 10 minimal API host: git porcelain reading (`status`, `rev-list` counts, `diff --numstat`, `log`), Octokit reads (PR for branch, issue body by number, `gh auth token`), SQLite store (`CREATE TABLE IF NOT EXISTS` at startup), JSONC commands reading, one screen API (`GET /api/header?path=...`).
- **New** `src/web` — Vite + React screen: header (facts row) + step-rail placeholder, plain fetch.
- **New** capability `worktree-header` — the eight-fact reading and its degradation contract.
- **New** capability `poc-shell` — repo skeleton conventions: central packages, gate.sh, commands.json self-declaration.
- **New** `AGENTS.md`, `gate.sh`, `.harness/commands.json` replaced with this repo's own gates.

## Capabilities

### New Capabilities

- `worktree-header` — the eight facts over a worktree, absence named with remedy, fact semantics matching harness `837c7ca` (PR three answers, checks five readings, seam three readings, null ≠ zero, unset ≠ absent).
- `poc-shell` — repository skeleton self-checking: what gate.sh runs, what commands.json declares, how the repo verifies itself.

### Modified Capabilities

None — first change in the repo.

## Impact

- New files only: `src/Plugin/**`, `src/Plugin.Tests/**`, `src/web/**`, `AGENTS.md`, `gate.sh`, `.harness/commands.json`, `Directory.Build.props`, `Directory.Packages.props`.
- Budget gate: `src/Plugin` + `src/web` ≤ 400 cloc lines (non-blank, non-comment; tests excluded from count but required by gate).
- Vocab: Folder, Worktree, Task, Workflow, Hold, Run, Agent — same words as harness.
