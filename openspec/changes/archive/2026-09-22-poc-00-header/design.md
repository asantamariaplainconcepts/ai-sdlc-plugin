# Design — poc-00-header

## Context

First cut of the PoC described in epic #1. Nothing exists yet: no `src/`, no gate, no AGENTS.md. The harness reference is pinned at `837c7ca` (read-only worktree); its fact semantics are the contract this header must match. Budget: ≤ 400 cloc lines in `src/Plugin` + `src/web` (non-blank, non-comment, tests excluded from count, required by gate). Frontend ≤ 1200 TSX total across the PoC.

## Goals / Non-Goals

**Goals:**

- Eight-fact header reading over a given worktree directory, served by a minimal API, drawn by a React screen.
- Fact semantics matching harness `837c7ca` exactly (see decisions).
- Repo skeleton: central package management, Directory.Build.props, gate.sh, AGENTS.md, self-declared commands.json, SQLite store created at startup.
- Tests over the pure parsers covering the two-subject check.

**Non-Goals:**

- Review steps as data (`review.json`) — POC-01.
- Proposal/Code panels — POC-02.
- Agent launch, runs table — POC-03.
- Creating worktrees, writing to GitHub, merge verdicts, scoring.

## Decisions

1. **Shapes (later tickets extend these, recorded here as the contract):**
   - `src/Plugin/AiSdlcPlugin.csproj` — single .NET 10 web project, minimal API. `Directory.Build.props` at root: `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>14</LangVersion>`, `<InvariantGlobalization>true</InvariantGlobalization>`. `Directory.Packages.props` central pins: `Microsoft.Data.Sqlite 10.0.9`, `Octokit 14.0.0` (+ test packages xunit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.7.0, Shouldly 4.3.0).
   - File layout: `Program.cs` (host + endpoints), `Git.cs` (porcelain execution/parsing), `Header.cs` (reading + DTOs), `Facts.cs` (pure fact derivation), `Seam.cs` (pure seam parse/compare), `TaskResolution.cs` (branch key), `GitHub.cs` (Octokit reads + auth), `Store.cs` (SQLite, `CREATE TABLE IF NOT EXISTS`), `Commands.cs` (JSONC commands.json reading). One file per concern.
   - `src/web` — Vite + React 19.3 + TS 5.7.3, deps exactly: `react`, `react-dom`, `lucide-react`, dev: `@types/react`, `@types/react-dom`, `@vitejs/plugin-react`, `typescript`, `vite`. No router, no signalr, no react-query. Screen file `src/App.tsx` renders header + step-rail placeholder; each later step is one panel component file mounted in one line.
   - SQLite: `.harness/data/poc.db` (gitignored), `worktrees` + `fact_cache` tables created at startup.

2. **Fact semantics (from harness `837c7ca` source, not screenshots):**
   - PR: three answers — `{pullRequest}` (number, state, isDraft, title), both-null = "asked, there is none", `unreachable` (reason + vendor detail) = "could not ask". Reasons: NotSignedIn, CliMissing, NoGitHubRemote, NotAskable, TimedOut, other→general failure sentence.
   - Changed: `{files, added, removed}` from `git diff --numstat` + `git status --porcelain` unmerged handling; a pending read is "reading…", never zero.
   - Base (ahead/behind): `git rev-list --left-right --count`; null (no upstream/base) is its own "unknown" sentence, not zero. behind > 0 → warn tone.
   - Merge: `git merge-tree` (write) unavailable; uses `git merge --no-commit --no-ff` dry path? No — **read-only constraint**: conflicts are read from `git status --porcelain` unmerged entries (`DD`, `AU`, `UD`, `UA`, `DU`, `AA`, `UU`); clean-tree check is `git status --porcelain` empty plus `git diff --cached --name-only` for staged. Merge fact compares working-tree conflicts only; `mergesClean: null` when ahead of nothing (no base) — named unknown.
   - Checks: from `.harness/commands.json` `gate: true` entries + stored last outcomes (Store). Five readings: undeclared / declared-never-ran / failed (worst outranks) / stale (outcome recorded on a commit that is not HEAD) / passed naming the short hash.
   - Seam: pure `parseSeam` (section `## Seam`-titled by regex `/seam/i`, bullet-first-backtick, `looksLikePath`, suffix/dir coverage) + `compareSeam` (declared-touched / declared-untouched / undeclared rows). Three readings: rows / null (no task or no section) / unreadable (truncated body, section ran to end).
   - Tree: `git status --porcelain` empty → clean; else the probe's own count sentence, warn tone.
   - Issue context: branch-proposed key (last digits) confirmed by Octokit issue read in the repo resolved from `git remote get-url origin`; fact names the issue number/title/state or the refusal.

3. **Serving**: `Program.cs` minimal API — `GET /api/header?path=<abs>` runs the reading and returns the facts JSON; static files from `src/web/dist`; dev proxy in Vite config only. single `dotnet run` serves everything.

4. **Read-only git**: only porcelain/diff/rev-list/log/remote-get-url — never merge, never worktree-create. Conflict presence is from unmerged index entries (a merge in progress or rebase conflicts leave those; otherwise fact names "not mergeable" unknown when no base to compare).

5. **Two-subject check (documented for human reproduction):**
   - Subject A (clean, all-absent): a fresh worktree of this repo on a branch with no digits (e.g. `main`) — expect the eight absences named.
   - Subject B (real changes): this PoC's own working worktree at `/var/folders/mq/qq109xk14c14mktfg3ysl9x40000gn/T/opencode/wt-poc-00` mid-implementation (uncommitted changes) — expect files>0 with `+/-`, seam compared against epic #1's Seam section, tree dirty warning.
   - Unit tests encode both subjects as fixtures; `git -C <repo> worktree add` is NOT run by tests (no worktree creation).
   - To reproduce by hand: `dotnet run` and open the screen, or `curl "localhost:<port>/api/header?path=$(pwd)"`.

6. **Auth**: `gh auth token` exec'd once; absence → `unreachable: NotSignedIn` with remedy "run gh auth login". No PAT env var: the gh seam removes the secret seam.

## Risks / Trade-offs

- [merge-tree simulation vs real merge] → Mitigated: conflicts read from index; state documented; harness parity is on the drawn sentence set (clean / n conflicts paths / unknown), which the index read satisfies for the check's subjects.
- [Octokit rate limits] → read is one PR + one issue per header request; no polling in this cut.
- [budget 400] → facts split into pure files with tests carrying the fixture weight; measured with cloc at the end, and the seam is included — if measured over budget, the epic pre-authorizes 7-of-8 with the exclusion stated — current measurement says it fits.
- [cloc unavailable via brew] → perl cloc at `/var/folders/mq/qq109xk14c14mktfg3ysl9x40000gn/T/opencode/cloc` per orchestration notes.

## Migration Plan

New files only; nothing to roll back. `git checkout` of the branch before this change is the rollback.

## Open Questions

None for this cut — patterns for POC-01..06 are the shapes in decision 1.
