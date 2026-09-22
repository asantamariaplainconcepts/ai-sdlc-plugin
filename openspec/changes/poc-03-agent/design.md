# poc-03-agent — design

## Context

POC-00..02 built read-only readings: git porcelain, issue lookup, declared steps, proposal/code panels. POC-03 is the first cut that *starts a process* — and the epic names it as the cut that decides whether the whole approach survives (the three prior cuts "would have come out fine by construction"). The seam the epic names: "the shared process helper, and the location of the transcript's .jsonl". The empirical claude facts were verified against CLI 2.1.234 before this design (see proposal Why, seven runs in temp dirs).

## Goals / Non-Goals

**Goals:**

- Launch `claude --print --output-format json --session-id <guid>` in a worktree via Process.Start, run to completion.
- Record the run against the starting commit: session id, exit code, is_error, cost if present, transcript path.
- A failed agent leaves a record; the record says which of exit-failure vs blob-is_error happened.
- Two runs on one worktree are two rows.
- Prompt per step declared as data (`prompts` map in review.json).

**Non-Goals:**

- Streaming, chat during run, permission back-and-forth (all named in the epic's "no es").
- The gate runner (POC-04), the poller/second trigger (POC-05), tests/evidence/app panels beyond the minimal run list.

## Decisions

1. **`Agent.cs` is a static helper, not a service** — the codebase has no DI beyond `Header`/`Git`/`GitHub`/`Store` created in Program.cs; `Agent` fits as a static with one process-seam function plus pure parsers, matching `Patch.cs` (pure function over text). Alternative considered: an interface `IAgentRunner` injected for testability — rejected; the tests pin the parsers (the bug nest per POC-00's rationale), not process spawning.

2. **Run-to-completion HTTP** — POST /api/runs holds the request until exit. The epic's "wait for it to finish" is literal, and the "no es" list (no streaming, no chat) makes holding the cheapest correct shape. A fire-and-async record would need exactly the lifecycle machinery the harness got buried in. POC-05 widens with a background trigger that reuses the same record-writing function (the launch contract is `Runs.LaunchAndRecord`, a function, not an endpoint trait).

3. **Exit code AND is_error both recorded** — measured empirically: the agent's own task failure (running `false`) still exits 0 with `is_error:false`; a CLI-level API failure (429) exits 1 with `is_error:true`. "The record says which of the two happened" needs both. `exit_code` -1 additionally means "process could not start / could not be waited on" (its own sentence).

4. **Transcript location, no guessing** — rule verified: real path (symlinks resolved) with `/`, `.`, `_`, space → `-`, under `~/.claude/projects/`, file `<session-id>.jsonl`. Record `transcript_path` only when the file EXISTS after exit; otherwise null + the row's `transcript_absent` is said by the API layer as a sentence naming the rule (never a guessed path). 

5. **`runs` table, INSERT-only** — no UPDATE anywhere: re-running is a new row. Id = rowid. Keyed by session id too (unique in practice, but two runs could share a worktree + step legitimately; the rowid is the identity). Cost NULL means the provider did not give it (named on screen, never zero — null is not zero, the standing rule).

6. **Prompts as data in review.json** — `"prompts": { "<step-key>": "<prompt>" }` beside `steps`. Absent map or absent key = the step has no prompt, said with remedy. The alternative (.ai/commands/<step>.md files) was real but adds a second declared-file convention where one already exists; the epic itself suggests the map. Reversible: a later cut can widen the value to `{ "file": ... }` without breaking readers that take string.

7. **Bounded captures** — the blob's full stdout goes to `.harness/data/runs/<session-id>.json` (1MB bound, overwrite-by-session-id is safe — the session IS the run). The row keeps `result_summary` CHAR-truncated to 512. stderr bounded to 2KB in the row. Nothing read from disk is executed.

**Permission posture (the epic's pending decision, recorded):** `--permission-mode acceptEdits`, fixed in `Agent.ClaudeArguments`. Rationale: the disposable-worktree defense; strictly narrower than `bypassPermissions` (a prompt in `--print` is a denial, so anything the CLI would ask about is refused rather than allowed); a human must ratify — recorded, not silently chosen.

## Risks / Trade-offs

- [Rate limit killed my test run mid-verification] → the blob carries `is_error` + `api_error_status`; recorded as-is. The POST returns whatever happened; a 429 run is a recorded run with exit 1, not a lost one.
- [claude not on PATH on other machines] → `which claude` resolution: try `claude` from PATH first, `/opt/homebrew/bin/claude` fallback; if neither → "the agent CLI could not be found" sentence (absence named, remedy given: install it). Recorded in the run as exit -1 with stderr named.
- [Holding an HTTP request for minutes] → acceptable for this PoC (single user, `dotnet run`); Kestrel default timeouts suffice. A person can close the tab; the run still records (server-side await, not browser-side).
- [Working tree changed by the agent while we diff] → run records `starting_commit` at launch; the diff reading of later steps is POC-04's invalidation question, not this cut's.

## Migration Plan

Additive only: `runs` table via CREATE TABLE IF NOT EXISTS; no existing table touched. Rollback = drop the new file (`.harness/data/` is gitignored runtime state anyway).

## Open Questions

None blocking. Permission posture ratification is the one deliberate human-review item — written here and in the change Why, as the epic asks.
