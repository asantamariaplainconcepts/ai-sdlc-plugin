# poc-fix-pass — the read-only verifier's findings, fixed

## Why

A read-only verifier walked the PoC at the end of POC-06 and produced six findings (F1–F6): one blocker (the artifact endpoint read arbitrary files), two majors (refused runs documented as recorded but never persisted; the verdict's budget row measured through `obj/`), one major coverage gap (the gates orchestration's refute-at-declaration wiring had zero tests), and two minors (the proposal absence sentence rebuilt in the panel rather than served; stale prose and one dead table). This change fixes all six on `poc/fix-pass`, one commit per finding, each tagged `fix(poc): F<n>`. Nothing here fixes what the verifier marked DO-NOT-FIX: F7 (no-trunk `changed` zero vs "diff not asked" — harness parity product call), evidence-step timing re-verification, F1's API shape beyond the minimal confinement, budget automation in `gate.sh`, or the frontend test framework.

**Ticket link:** https://github.com/asantamariaplainconcepts/ai-sdlc-plugin/issues/1. Umbrella story F-POC-1, use case UC-POC-1 (verify finished work), business rule BR-VERIFY-1 ("null is not zero" — absence and refusal are different sentences, and a refusal that is not recorded is a silence).

## What Changes

- **Fixed** `src/Plugin/Program.cs` + `src/Plugin/Change.cs` (F1) — `/api/artifact` no longer reads an arbitrary `file` path from the caller: it requires the worktree `path` plus a path relative to it, rejects absolute/`..`/outside-`openspec/changes` inputs with a named 400-style problem, and reads only inside a change root that worktree's own proposal discovery listed. New confinement tests (escape refused, absolute refused, listed artifact still read).
- **Fixed** `src/Plugin/Runs.cs` (F2) — a refused launch is now **persisted**: `LaunchAndRecord` records the refused row through the store (it answers "why did nothing happen", and the watcher's HEAD-moved predicate reads the newest row whatever it says, so an unpersisted refusal made the poller loop silently forever). The trigger-vocabulary rule is kept unchanged — the refused row still stores NO trigger; the refused value is named in the problem sentence. `WatcherTests` now exercises `LaunchAndRecord` for refusals and asserts store state instead of fabricating rows.
- **Fixed** `docs/verdict.md` (F3) — the POC-06 budget row corrected: clean re-measure (excluding `obj/`) is 1431, not 1450 (+19 was `obj/` build artifacts, not code); the claim that `obj/` rows are absent in a clean checkout is corrected to the measurement rule — exclude `bin/`/`obj/` explicitly.
- **Added** one gates orchestration test (F4) — `RunDeclaredGates` over a temp repo declaring a missing binary: `Missing=true`, no store rows. The refute-at-declaration wiring (missing ⇒ no row) and the reading surface's same refusal are now covered, not just the pure helpers.
- **Fixed** the proposal absence duplication (F5) — `/api/proposal` serializes the discovery's absence sentence (additive `absence` field); `ProposalStep.tsx` renders the API's sentence instead of rebuilding it from the root. One source of truth.
- **Fixed** stale prose and dead schema (F6) — `App.tsx`'s placeholder says what it is now (no step selected), `ReviewSteps.KnownImplemented` comment updated to the four keys that exist, `Store.cs`'s "Two tables now" corrected, and the never-used `fact_cache` CREATE TABLE removed (dead schema — removing a `CREATE TABLE IF NOT EXISTS` entry is safe for existing DBs: the table remains in old files, never created in new ones).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `proposal-reading`: the bounded artifact read gains its confinement requirement — the endpoint serves only paths the proposal discovery listed, under the worktree's own `openspec/changes/`; every escape is refused with a named sentence. The absence sentence is serialized in the proposal response (the panel no longer rebuilds it).
- `run-triggers`: the refused-launch requirement is corrected to what now holds — a refused launch is recorded as a row (problem sentence, no trigger, no session facts), so the watcher's newest-row predicate answers rather than looping silently.
- `agent-runs`: the "runs insert, never overwrite" requirement already describes the shape; the refused row is a row (recorded whatever happened), which the capability text now names explicitly rather than implying only launched runs.

## Impact

- Backend: `Change.cs` (+confined read), `Runs.cs` (refused rows persisted — removed the store-less `Refused` path's divergence), `Program.cs` (endpoint signatures), `Store.cs` (dead `fact_cache` removed, comment fixed). Frontend: `ProposalStep.tsx` (renders the served sentence), `App.tsx` (one sentence). Docs: `docs/verdict.md` (one row + one claim corrected).
- Tests: `ChangeTests.cs` (confinement), `WatcherTests.cs` (refused rows persisted via `LaunchAndRecord`), `GatesTests.cs` (orchestration survivor).
- Budget guardrail: this pass fixes by removal/hardening; net cloc delta stays within ~+100 (recorded per commit; measured before push).
- No GitHub writes, no issue labels, no new endpoints beyond the confinement the fix itself is.
