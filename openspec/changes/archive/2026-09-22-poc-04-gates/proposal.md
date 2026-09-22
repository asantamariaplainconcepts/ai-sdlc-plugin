# poc-04-gates — the declared gates, tied to the commit

## Why

Epic #1 (ticket POC-04, section `### POC-04 · Las gates, atadas al commit`) asks for the fifth cut of the PoC: POC-03 launched an agent and recorded its run, and now the declared gate commands (`.harness/commands.json`, `"gate": true` — already parsed by POC-00's `Commands.cs`) must actually run over the candidate the agent left. A gate silently re-run is indistinguishable from a gate never run, so the exit code and a bounded tail of output are captured and stored **against the resulting commit**: moving HEAD invalidates the previous result and the step says so instead of showing it as valid (the staleness semantics POC-00's `Facts.CheckOutcome.Since` already models). The step presents pass / fail / **no concluyente** — "a command that started and ran zero tests is not a pass".

**Ticket link:** https://github.com/asantamariaplainconcepts/ai-sdlc-plugin/issues/1 (section POC-04). Umbrella story F-POC-1, use case UC-POC-1 (verify finished work), business rule BR-VERIFY-1 ("null is not zero" — absence and failure are different sentences).

**Assumptions (POC-04 owns these decisions, unattended run — recorded, each reversible):**

1. **Outcome rule, kept simple and written down** — per gate command: exit 0 with output = **pass**; exit non-zero = **fail**; the command could not start (refused at declaration), timed out, crashed, or produced no output at all = **no concluyente** (inconclusive). Zero output is never a pass because a gate that says nothing asserts nothing — the epic's "un comando arrancado y cero tests no es un pass" made literal. No per-command override in this cut; a later slice can widen the declaration with a `"always-silent": true` escape hatch.
2. **Refutation at declaration time** — a gate whose declared `cwd` does not exist, or whose `run` executable cannot be found on PATH (or as the declared relative path from the declaration's own directory), is refuted when the declaration is READ, not at run time. The reading surface (`GET /api/gates`) shows it as `missing` with its remedy; running it never happens. This mirrors the harness's "declared files are input, absent ≠ unreadable" discipline: the declaration itself carries the refutation.
3. **Bounded tail** — last 64 lines of stdout+stderr combined (plus a leading line if truncated), capped at 8 KB per stored row; full output is not kept (POC-03's `runs_capture` stays the only full-capture table, for agent blobs). Reviewer freedom is preserved by the re-run button.
4. **Timeout** — 10 minutes per gate command (gate.sh of this very repo takes ~1-2 min; a gate that runs longer is a gate to fix, not to wait on). On timeout the process is killed and the row is recorded as **no concluyente** (crashed/killed bucket), never a fail — a wall clock is not a judge.
5. **Dirty tree at record time** — the resulting commit is read AFTER the gates ran (the agent may have committed work meanwhile). If the tree is dirty, that is recorded in the row's problem column as a sentence rather than refusing to record: the row is still keyed by the HEAD it observed, and the staleness rule handles the rest. Reversible: a later slice can withhold the rows of a dirty tree.
6. ** staleness shape** — a recorded row is stale against a later HEAD reading when `recorded_commit != current HEAD` (the exact-commits comparison; no ahead/behind arithmetic in this cut — the harness's `since` counts exist but this PoC stores the commit itself, so equality is the honest, cheap rule recorded here).

## What Changes

- **New** `src/Plugin/Gates.cs` — `RunDeclaredGates(cwd)`: read the declared commands, refute unrunnable gates at declaration, launch each gate via the existing `Agent.Launch` seam extended minimally with a timeout, capture exit + bounded tail, read the resulting commit, INSERT the rows. Plus pure derivations: `Outcome(exit, output, problem)` → pass/fail/inconclusive, and `Staleness(recorded, head)` → current/stale.
- **Extended** `src/Plugin/Agent.cs` — `Launch` gains an optional `timeoutMs` parameter (absent ⇒ unchanged, no kill; the default call sites compile identically). One optional prop, contiguous, POC-03's contract preserved.
- **Extended** `src/Plugin/Store.cs` — new `gate_results` table (CREATE TABLE IF NOT EXISTS, additive): id, worktree_path, commit, command_key, exit_code, output_tail, finished_at, problem. INSERT-only, never updated — old rows become stale by comparison, not by deletion.
- **Extended** `src/Plugin/Header.cs` — the header's checks fact now reads over the stored gate outcomes for the worktree (feeding `Facts.Input.Outcomes` from `Store.ListGateResults`), so the eight-fact header stops saying "checks not run here" once gates have run. POC-00's reading finally gets its second writer.
- **Extended** `src/Plugin/Program.cs` — `POST /api/gates?path=` (run the declared gates over the candidate, held to completion, return the rows) and `GET /api/gates?path=` (the declared gates with their outcome, current commit, staleness marker).
- **Extended** `src/web/src/steps/TestsStep.tsx` — the tests step gains the gates presentation: per declared gate its name, pass/fail/inconclusive, the commit short-hash it was recorded against, and a stale marker when HEAD has moved since recording. A run button POSTs; degradation sentences match the app's existing style.
- **New tests** — `GatesTests.cs`: outcome derivation (exit×output → pass/fail/inconclusive), staleness (recorded commit vs current), declaration-time refutation (missing cwd / unfindable executable), bounded tail, INSERT-only store rows. One cheap seam test launches `/bin/echo` through the seam; no gate.sh run in tests.

## Capabilities

### New Capabilities

- `gate-results`: running the declared gate commands over a candidate, recording exit code and bounded tail against the resulting commit, the three readings (pass / fail / no concluyente), declaration-time refutation of unrunnable gates, and staleness of recorded outcomes against a moved HEAD.

### Modified Capabilities

None — `worktree-header` keeps its requirement text; the outcomes input it already names gains a writer, which is the shape POC-00 left open ("POC-00 records no outcomes").

## Impact

- New backend file: `Gates.cs`; new test file: `GatesTests.cs`; extended: `Agent.cs` (one optional parameter), `Store.cs` (one table), `Header.cs` (outcomes fed), `Program.cs` (two endpoints), `TestsStep.tsx` (gates presentation).
- Budget gate: this cut adds ≤ 350 cloc (epic allocation POC-04 ≤ 350; cumulative 1,450).
- No GitHub writes, no issue labels. Gates run only through POST /api/gates — a person's act (the poller is POC-05, not this).
