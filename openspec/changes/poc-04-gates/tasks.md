## 1. Pure readings (test-first)

- [ ] 1.1 `Gates.Outcome` — (exit, output, problem) → pass/fail/inconclusive: exit 0 + output = pass; exit non-zero = fail; exit 0 + no output = inconclusive; problem (could not start/timeout/killed) = inconclusive; missing refutation never reaches Outcome
- [ ] 1.2 `Gates.Staleness` — recorded commit == head → current; differs → stale; head null (cannot ask) → its own answer
- [ ] 1.3 `Gates.Tail` — bounding: short text kept whole; >64 lines keeps last 64 with a cut marker; >8KB capped; stdout+stderr combined
- [ ] 1.4 `Gates.ParseRunLine` — split a run line into executable + args; `.exe` suffix on Windows is name-only knowledge, resolution happens in 1.5; over-simplified quoting recorded as assumption
- [ ] 1.5 `Gates.ResolveExecutable` — relative to the declaration's repo root, then PATH; missing ⇒ null (the caller refutes before running)
- [ ] 1.6 `Gates.LatestPerGate` — group a worktree's rows into the newest recorded outcome per command key

## 2. Store

- [ ] 2.1 `Store` gains `gate_results` (CREATE TABLE IF NOT EXISTS, additive): id/worktree_path/commit/command_key/exit_code/output_tail/finished_at/problem
- [ ] 2.2 `Store.RecordGateResult` — INSERT-only; `Store.ListGateResults(worktree)` — most recent first
- [ ] 2.3 A second run adds rows; earlier rows are still there untouched (INSERT-only, proven)

## 3. Seam + launch

- [ ] 3.1 `Agent.Launch` gains optional `timeoutMs` (absent ⇒ unchanged behavior); on timeout: kill process tree, waitForExit, return exit -1 with a problem sentence naming the timeout — one optional parameter, POC-03's call sites untouched
- [ ] 3.2 `Gates.RunDeclaredGates(cwd)` — read declaration (existing `Commands.Read`), resolve each gate (missing ⇒ refuted in the response, no row), launch each through the seam with the 10-minute timeout, read HEAD after the runs, INSERT one row per gate that ran
- [ ] 3.3 One cheap seam test: `/bin/echo hello` through `Agent.Launch` exits 0 with output; a command that cannot start returns its named problem; a short timeout kills a sleeping process (`/bin/sleep`) and records the timeout sentence — no gate.sh in tests

## 4. Endpoints + header

- [ ] 4.1 `Program.cs` — `POST /api/gates?path=` (run + return reading) and `GET /api/gates?path=` (reading only); not-a-repository / missing path degrade with named sentences like the other endpoints
- [ ] 4.2 `Header.cs` — the checks fact's outcomes now read from the store: latest outcome per declared gate, `Since = 0` when recorded commit == present HEAD, `null` when HEAD cannot be read; `Facts.Checks` text unchanged (it already renders these five readings)

## 5. Frontend + verification

- [ ] 5.1 `TestsStep.tsx` gates section: run button (POST /api/gates), per gate name/reading/commit-short-hash/stale marker/tail in a details element; "no gates declared" and "gates not run here" as named sentences
- [ ] 5.2 `gate.sh` green: dotnet build+test, web build, openspec validate; cloc added ≤ 350
- [ ] 5.3 Absence assertions proven per contract: force the populated/stale branch on, re-run, require absence assertions to fail, revert, empty diff, green again
- [ ] 5.4 One recorded end-to-end run: POST /api/gates against this worktree itself (the repo verifies itself with its own declared `./gate.sh`), staleness checked by moving HEAD in a scratch worktree — not a test, a recorded check
