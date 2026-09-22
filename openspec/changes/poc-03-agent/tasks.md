## 1. Backend readings (pure, test-first)

- [ ] 1.1 `Agent.ParseResult` — parse the JSON blob (session, cost, is_error, turns, duration, result summary) from fixture texts matching the verified 2.1.234 output shape: full blob, cost-absent blob, unparseable stdout, empty stdout; absent cost is null, never zero
- [ ] 1.2 `Agent.TranscriptPath` — derive `~/.claude/projects/<munged-cwd>/<session-id>.jsonl` from cwd fixtures (incl. a path whose munging crosses `.`, `_`, space; symlink resolution via `Path.GetFullPath` + realpath handling) with a located/absent answer
- [ ] 1.3 `Agent.ClaudeArguments` — the argument list (print, output-format json, session-id, permission-mode acceptEdits, prompt) as a pure function over (prompt, sessionId)
- [ ] 1.4 `ReviewSteps.PromptFor` — read the `prompts` map from the declared file: present key, absent map, absent key — each its own sentence with remedy (extend the existing reader, one optional shape property)

## 2. Run recording

- [ ] 2.1 `Store` gains the `runs` table (CREATE TABLE IF NOT EXISTS, additive): id/session_id/worktree_path/step/prompt/starting_commit/started_at/finished_at/exit_code/is_error/cost_usd/num_turns/duration_ms/transcript_path/result_summary/stderr_tail
- [ ] 2.2 `Runs.Record` — INSERT-only; two runs on one worktree are two rows (test with a temp db, assert both selected afterwards)
- [ ] 2.3 `Runs.List` — the worktree's rows, most recent first, all recorded columns

## 3. Launch + endpoints

- [ ] 3.1 `Agent.Launch` — Process.Start of a command with args and cwd; exit code, bounded stdout (1MB), bounded stderr (2KB), duration; a command that cannot start is exit -1 with a named problem
- [ ] 3.2 `Runs.LaunchAndRecord` — resolve prompt (named absence ⇒ no launch), mint session id, read HEAD, launch claude (PATH then /opt/homebrew/bin/claude fallback, absent CLI = recorded sentence), parse blob, locate transcript, record; one function both POC-05 triggers will call
- [ ] 3.3 `Program.cs` — `POST /api/runs?path=&step=` (runs to completion, returns the row) and `GET /api/runs?path=` (list); POST refuses unknown step keys and named prompt absence with 200-and-problem, matching the app's degradation style

## 4. Declared data + frontend

- [ ] 4.1 `.harness/review.json` gains the `prompts` map with a `tests` prompt (this repo verifies itself)
- [ ] 4.2 `TestsStep.tsx` — run launcher panel: launch button named-disabled when no prompt, run list (session abbreviated, exit code, is_error, cost with its absence stated, transcript located-or-absent), mounted in App.tsx in one line; `ReviewSteps.KnownImplemented` gains `tests`
- [ ] 4.3 Build (`gate.sh`): dotnet tests + web build green, cloc ≤ 300 added

## 5. Verification

- [ ] 5.1 Absence assertions proven: force the loading/populated branch on (fail runs-list empty, fail transcript absent), require absence assertions to fail, revert, empty diff, green again
- [ ] 5.2 One real end-to-end run against this worktree itself through POST /api/runs (the repo verifies itself), recorded in .harness/data — not a test, a recorded check
