# poc-03-agent — launch an agent and record what it left

## Why

Epic #1 (ticket POC-03, section `### POC-03 · Lanzar un agente y grabar lo que dejó`) asks for the fourth cut of the PoC — the one the epic itself calls "the cut that decides it": reading (POC-00..02) would have come out fine by construction, but launching an agent and recording the run is where the harness's weight actually lives. This PoC must show the seam can stay thin: resolve the prompt for a review step, launch `claude --print` in the worktree with a pinned `--session-id`, wait for exit, and record the run **against the commit it started from** — session id, exit code, cost if the provider gives it, transcript path. A run leaves a record even when the agent fails, and the record says which of the two happened; two runs on the same worktree do not overwrite each other.

**Empirical claude facts this change is built on (verified against claude CLI 2.1.234 at `/opt/homebrew/bin/claude`, real runs in temp dirs — recorded here because POC-04/05 extend them and must not re-guess):**

1. **JSON output shape** — `claude --print --output-format json --session-id <guid>` emits exactly ONE JSON blob on stdout on completion, with `session_id`, `total_cost_usd` (0 absent), `is_error`, `usage`, `num_turns`, `duration_ms`, `result`, `subtype`. No streaming, nothing else on stdout.
2. **Exit codes mean CLI-level health, not task health** — a run whose agent happily completed returned exit 0; a run where the agent internally chose to run `false` still returned exit 0 (only `is_error:true`, `terminal_reason:api_error`, exit 1 on API failure e.g. 429 weekly-limit). So the record needs BOTH the exit code AND the parsed blob; neither alone says "which of the two happened".
3. **Transcript path rule** — the `.jsonl` lives at `~/.claude/projects/<munged-cwd>/<session-id>.jsonl`, where munged-cwd is the **real path (symlinks resolved — `/var` on macOS is really `/private/var`)** with `/`, `.`, `_` and space replaced by `-` (four cases verified, including a space-and-dot directory name and the `/private` prefix). The transcript file exists even for failed runs. If the file is absent after exit, the run records transcript-absent (named, not zero) — never a guessed path.

**Assumptions (POC-03 owns these decisions, unattended run):**

1. **Prompt resolution — a `prompts` map in `.harness/review.json`** (the epic's suggested reversible convention): one string per step key beside the existing `steps` array; absent = the step has no prompt and says so ("no prompt is declared for this step — add one under \"prompts\" in .harness/review.json"). The prompt is data, read by the same bounded JSONC reader, never executed by its reader. This PoC declares a `tests` step prompt: run the gate and report.
2. **Permission posture** (the epic's named pending decision) — `--permission-mode acceptEdits` on the command line, fixed for now, recorded here for a human to ratify. The defense is the disposable-worktree one: the agent edits files in a worktree this PoC already reads read-only; `acceptEdits` still refuses anything that leaves the filesystem (bash commands that would prompt still prompt — and in `--print` mode a prompt is a denial), so it is strictly narrower than `bypassPermissions`, which was rejected. A later slice can widen it to a declared setting; the flag is one argument in one place.
3. **Run record shape (POC-04/05 extend)** — a `runs` sqlite table keyed by rowid autoincrement (session id is ALSO unique in practice but two runs could theoretically share a worktree+moment; the id is the rowid — INSERT only, never UPDATE), columns: `id, session_id, worktree_path, step, prompt (resolved, what was sent), starting_commit, started_at, finished_at, exit_code, is_error, cost_usd (nullable — absent stays NULL, named on screen), num_turns, duration_ms, transcript_path (nullable, absent = named), result_summary (bounded tail of the blob's result text), stdout_path (bounded capture file for the full blob)` — no streaming, no partials.
4. **Launch contract** — `POST /api/runs?path=&step=` resolves the prompt for the step, mints a session id (Guid), reads `git rev-parse HEAD` as the starting commit, launches `claude` in the worktree, **waits to completion** (this cut is run-to-completion by design — the epic's "no es" list excludes streaming; the HTTP request holds), records the run, and returns the run row as JSON. `GET /api/runs?path=` lists a worktree's runs (id, step, session, exit, is_error, cost, started_at, transcript presence). The returned record IS the launch contract POC-05's two triggers will both call.
5. **Agent helper shape** — a new `Agent.cs` static class with one seam: `Launch(command, args, cwd)` → `AgentRunResult (exit code, bounded stdout, bounded stderr, duration ms)` via Process.Start, plus pure parsers: `ParseResult(stdout)` → the run fields from the JSON blob (tolerant: missing cost stays null, unparseable blob is its own problem sentence), `TranscriptPath(cwd, sessionId)` → real-path-munged location + existence flag. No test launches real claude (tests are pure-parser fixtures, per standing policy).
6. **Stdout capture on disk, bounded in the row** — the full blob can be huge; the DB row keeps a bounded summary, the full blob goes to `.harness/data/runs/<session-id>.json` (bounded to 1MB, not executed).
7. **Frontend: minimal, backend-heavy cut** — the Tests step rail entry becomes implemented, its panel is a run launcher + run list: a button that POSTs and shows the recorded run (a one-line indicator suffices on success/failure naming exit code + is_error + cost-or-absent + transcript-located-or-absent). No streaming display, no chat, no permission UI.

## What Changes

- **New** `src/Plugin/Agent.cs` — the shared process helper + three pure readings: `Launch` (Process.Start — command, args, cwd, exit code, bounded stdout/stderr, duration), `ParseResult` (JSON blob → session/cost/isError/turns/duration/result summary, tolerant of absence), `TranscriptPath` (real-path munging rule + existence). This is the epic's named seam ("el helper de proceso compartido, y la localización del .jsonl").
- **New** `src/Plugin/Runs.cs` — run recording: `runs` table (Store extended additively), INSERT-only record + list per worktree, prompt resolution reusing `ReviewSteps.FindDeclared` for the same `.harness/review.json` read.
- **Extended** `src/Plugin/ReviewSteps.cs` — the declared-file reading gains the `prompts` map (one optional property; absent ⇒ each step has no prompt, said with its remedy). Bounded, JSONC, as before.
- **Extended** `src/Plugin/Program.rs (Program.cs)** — `POST /api/runs?path=&step=` (launch + record to completion + return row) and `GET /api/runs?path=` (list).
- **Extended** `.harness/review.json` — this repo's own declaration gains a `prompts` map with a `tests` step prompt (run the gate; report).
- **Extended** `src/Plugin/ReviewSteps.cs KnownImplemented` + `src/web/src/steps/TestsStep.tsx` — the tests step's panel (one file, one mount line in App.tsx): launch button (disabled + named when no prompt declared), run list with each run's exit/is_error/cost-absent-named/transcript-absent-named.
- **New** tests: `AgentParseTests.cs` (blob fixtures from the verified output shape — present, error, missing-cost, unparseable) + `RunsTests.cs` (record survives agent failure — INSERT with exit 1/is_error true and exit 0/is_error false both recorded, distinct; two runs same worktree not overwritten; prompt resolution absent = named; transcript path derivation from cwd fixtures).

## Capabilities

### New Capabilities

- `agent-runs`: launching the claude CLI in a worktree for a review step — prompt resolution from the declared `prompts` map (absent = named), pinned session id, recorded run against the starting commit with exit code, is_error, cost-if-present, turns, duration, transcript location (absent = named, never guessed); a run is recorded even when the agent fails; two runs on one worktree are two rows.

### Modified Capabilities

None — `review-steps` gains the `prompts` map reading and the `tests` implemented key, which is POC-02's established shape (the set it names shrinks; the requirement text is unchanged; a MODIFIED delta records it exactly as poc-02 did for proposal/code).

## Impact

- New backend files: `Agent.cs`, `Runs.cs`; new test files: `AgentParseTests.cs`, `RunsTests.cs`; extended: `Store.cs` (one table), `ReviewSteps.cs` (prompts map + one key), `Program.cs` (two endpoints), `.harness/review.json`, `App.tsx` (one mount line), new `TestsStep.tsx`.
- Budget gate: this cut adds ≤ 300 cloc (epic allocation POC-03 ≤ 300; cumulative 1,100).
- No GitHub writes. `gh` untouched. Run launch happens only through POST /api/runs (a person's act — the poller is POC-05, not this).
