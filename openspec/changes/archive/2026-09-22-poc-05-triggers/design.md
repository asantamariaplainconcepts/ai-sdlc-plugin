# Design — poc-05-triggers

## Context

POC-03 established the single launch contract `Runs.LaunchAndRecord(cwd, step)` — resolve the declared prompt, mint a session id, pin the starting commit, run claude to completion, INSERT the row. POC-04 tied gate outcomes to the resulting commit. The only trigger today is the panel's button (`POST /api/runs`). The harness reference at 837c7ca diverged exactly here: `LabelWatcher.cs` publishes label events without `WorkflowId`, so poller-started work loses its provenance at the boundary. This cut adds the second trigger while making the divergence impossible: both callers go through the same contract, and the only difference is a `trigger` value stored on the row.

Constraints inherited: budget ≤ 150 added cloc; declared config is input to be read (bounded, never executed); INSERT-only recording; vocabulary closed (Folder, Worktree, Task, Workflow, Hold, Run, Agent); porcelain-only git reads; nothing writes to GitHub; `CREATE TABLE IF NOT EXISTS` additive schema, never migrated.

## Goals / Non-Goals

**Goals:** both triggers call `LaunchAndRecord`; the run row carries `trigger` (button/poll); the view does not branch on it (wizard render identical); the poller is a `BackgroundService` + `PeriodicTimer`, off by default, minutes-not-seconds interval; the launch predicate is pure and tested; a poller-started run arriving with empty gates reads as a named failure, not silence.

**Non-Goals:** the verdict (POC-06); streaming; GitHub writes; conditional requests / rate-limit handling for a GitHub-polling watcher (the recorded git-HEAD poller is the PoC's stand-in); making the poller's step configurable (`Watcher:Step` — reversible widening recorded in the Why); any UI for watcher state.

## Decisions

1. **One contract, one extra parameter.** `LaunchAndRecord(cwd, step, trigger = "button")`. The only divergence between the two callers is the value stored on the row. `RunRow` gains `Trigger` after `Problem` (trailing position, so the existing 17-positional-arg call sites keep compiling by adding nothing); `POST /api/runs` validates the vocabulary (`button`/`poll`) and refuses anything else with a named sentence.

2. **The column, added defensively.** `runs` is born in POC-03's `CREATE TABLE IF NOT EXISTS` — existing databases have no `trigger` column and `CREATE TABLE IF NOT EXISTS` will not add one. Store's constructor gains one guarded step after the create batch: read `PRAGMA table_info(runs)`, and when `trigger` is absent, `ALTER TABLE runs ADD COLUMN trigger TEXT`. Idempotent (the pragma check is the guard, ALTER never re-runs), additive (old rows read `trigger = null` = "not said" — null is not zero). The new-database path adds the column to the CREATE statement itself so fresh databases are born with it and the ALTER stays a no-op there.

3. **The watcher, one file.** `Watcher.cs`:
   - `WatcherConfig.Read(IConfiguration)` → `(bool Enabled, int IntervalSeconds, IReadOnlyList<string> Paths)`, defaults (false, 300, []). Keys: `Watcher:Enabled`, `Watcher:IntervalSeconds`, `Watcher:Paths` — the `configuration[ModeKey] ?? "disabled"` shape's key pattern, adapted to sections.
   - `ShouldLaunch(lastStartingCommit, head)` — pure: both null → false (nothing to compare, nothing recorded — silence, not a launch); one null, one not → true (a first run is new work); both present, different → true (HEAD moved since the last recorded run); equal → false.
   - `ExecuteAsync`: disabled or interval ≤ 0 ⇒ return (the harness's `seconds <= 0` guard); otherwise `PeriodicTimer` loop, each tick: for each configured path — quick-refuse non-existent/non-repository paths silently (a configured path that stops being a worktree is not a launch and not an error worth a crash), compare `Store.ListRuns(path)` first row's `StartingCommit` against `git.Head(path)`, fire `LaunchAndRecord(path, "tests", "poll")` when `ShouldLaunch` says true. Exceptions per tick are caught and swallowed with the loop intact (a watcher that dies on one bad path stops watching everything).
   - One instance, wired in `Program.cs` via `builder.Services.AddHostedService<Watcher>()` after the store/runs are constructed — the hosted service resolves `Runs` from DI after this (a `AddSingleton` for `Runs`/`Git`/`Store` rearrangement is the minimal hosting shape; `Store` already exists as a local, so it becomes a singleton first).

4. **Gates-empty is a named failure.** POC-04's `ReadDeclaredGates` already answers each declared-but-never-run gate with reading `null` and the panel renders "not run here" — a sentence, not silence. The tightening this cut owns is the **header's checks fact** (`Facts.Checks`): "gates declared, none run" moves from `Tone.Plain` to `Tone.Warn`. A poller-started run arriving with empty gates then shows as warn — failure-adjacent, looked-at — in the header, and as "not run here" rows in the panel: both surfaces say the thing; neither goes quiet.

5. **The view does not look.** `TestsStep.tsx`'s button POSTs `trigger=button` explicitly (the default is also `button`, but the caller saying it is the contract being used); the run row renders exactly what it rendered before — no trigger text, no conditional. The wizard is indistinguishable between the two triggers, by construction: there is no code path reading `trigger` in the frontend.

## Risks / Trade-offs

- **A poller run holds the tick.** `LaunchAndRecord` runs claude to completion (minutes). While one poller run is in flight the next ticks wait — single-threaded tick execution is the recorded simplification (the harness flocks with semaphores; this PoC serializes). A run overlapping its own next tick is thereby impossible.
- **HEAD-moved predicate is content-blind.** "New work" = "starting_commit of the last run ≠ present HEAD". A rebase that lands the same content re-triggers; an empty commit re-triggers. Recorded in the Why as the reversible default; the human ratifies or widens.
- **Swallowing tick failures.** A watcher that silently survives a failing path also silently does nothing. Accepted for the PoC (the epic's death criterion is budget, not observability); ASP.NET logging is available but not counted against budget for one line if needed — actually, no: no logger, zero lines. The silence is the recorded trade-off.
