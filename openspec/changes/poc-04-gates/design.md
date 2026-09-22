# Design — poc-04-gates

## Context

POC-00 reads the header of a change (eight facts, checks among them, no outcomes recorded); POC-03 launches an agent and records runs. What neither does is execute the declared gates. This cut adds that: run the declared `gate: true` commands from `.harness/commands.json` through the existing `Agent.Launch` process seam, capture exit code and a bounded tail, and store them against the resulting commit so that moving HEAD invalidates the previous result.

Constraints inherited: budget ≤ 350 added cloc; declared files are untrusted input (bounded, never executed by their reader); INSERT-only recording; no shared durability, no leases, no reconciliation (the epic says the seam is the process runner and the local store); vocabulary closed; porcelain-only reads.

## Goals / Non-Goals

**Goals:** the declared gates run on demand; per gate, exit + bounded tail recorded against the resulting commit; pass/fail/no concluyente with the rule written down; unrunnable gates refuted at declaration time; staleness shown when HEAD moves; the header's checks fact finally reads over recorded outcomes; the tests step panel presents it.

**Non-Goals:** two triggers (POC-05 — button + poller produce the same evidence); the verdict (POC-06); writing anything to GitHub; streaming gate output; per-command overrides of the outcome rule.

## Decisions

1. **Reuse `Agent.Launch`, extend with one optional timeout parameter.** The seam already does exit code + bounded capture + duration + "could not start" as its own answer. Gates need a kill-switch (a stuck gate would hold the HTTP request forever), so `Launch(command, args, cwd, timeoutMs = null)`: absent ⇒ current behavior, present ⇒ `WaitForExit(timeout)` then `Kill(entireProcessTree: true)` and a problem sentence naming the timeout. One optional prop, contiguous, POC-03's callers unchanged.

2. **Shell lines.** `commands.json` declares `"run": "./gate.sh"` — a line a person types, not argv. The PoC parses it simply: split on whitespace, first token is the executable (suffixed `.exe` on Windows for PATH lookup), the rest are arguments. No quoting, no pipes, no redirection — recorded as the assumption. The executable is resolved (a) as a path relative to the declaration's repo root (the directory containing `.harness/`), (b) on PATH via `Environment.GetEnvironmentVariable`. Unresolvable ⇒ refuted `missing` at declaration-read time; the harness's own vocabulary is "declared is data, absent is said".

3. **`Gates.cs`, one file.** `RunDeclaredGates(cwd)`: read the declaration (bounded, JSONC — existing `Commands.Read`), resolve each gate, launch each through the seam with a 10-minute timeout, tail-bounded stdout+stderr, read HEAD after the runs, INSERT one row per gate (plus `missing` rows are NOT inserted — refutation is a reading, not an outcome; stated in the response only). Pure derivations for the tests: `Outcome(exit, output, problem)` → `Pass`/`Fail`/`Inconclusive`, `Staleness(recordedCommit, head)` → `Current`/`Stale`.

4. **`gate_results` table** (additive, `CREATE TABLE IF NOT EXISTS`): `id, worktree_path, commit, command_key, exit_code, output_tail, finished_at, problem`. INSERT-only keyed by content — a moved HEAD makes rows stale by comparison at read time (recorded commit ≠ current HEAD), never by update or delete. List per worktree, most recent first, grouped by command for "latest per gate" presentation.

5. **Watching the budget, not the watcher.** The header (`Header.cs`) feeds `Facts.Input.Outcomes` from the store (latest outcome per declared gate, `Since = 0` when the recorded commit equals HEAD, `null` when it cannot be compared — matching POC-00's existing `CheckOutcome(int? ExitCode, int? Since)` contract exactly, so `Facts.Checks` needs no change). This is the writer POC-00's comment said would come.

6. **API.** `POST /api/gates?path=` runs and returns the reading; `GET /api/gates?path=` returns the reading without running. Response shape: `{ path, problem?, gates: [{ key, name, missing?, reading, exitCode, commit, head, stale, finishedAt, tail }] }` where `reading ∈ pass|fail|inconclusive|not-run|missing`. POST holds to completion (same contract as POC-03's runs POST — the epic's "no streaming").

7. **Frontend.** `TestsStep.tsx` gains a gates section under the run list: a run button (POST), per gate one row — name, reading with color (pass foreground / fail bad / inconclusive warn), commit short-hash recorded against, stale marker ("HEAD has moved — recorded on `abc1234`, now `def5678`"), and the tail in a collapsible `<details>`. Degradation sentences match the app style ("no gates declared — …", "gates not run here").

## Risks

- `gate.sh` of this repo takes minutes; POST /api/gates holds that long. Acceptable for a PoC whose epic asks for run-to-completion; POC-05's poller will call the same contract.
- Zombie processes on kill — mitigated with `Kill(true)` and `WaitForExit` after kill.
