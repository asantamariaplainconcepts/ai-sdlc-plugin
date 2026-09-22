# poc-05-triggers — un contrato, dos disparadores

## Why

Epic #1 (ticket POC-05, section `### POC-05 · Un contrato, dos disparadores`) asks for the sixth cut of the PoC: POC-03 established the single launch contract (`Runs.LaunchAndRecord(cwd, step)`) and POC-04 tied gates to the resulting commit. What neither has is a second way to start work. In the harness reference (837c7ca, `LabelWatcher.cs`) this divergence already exists — the watcher publishes events **without** `WorkflowId`, so a poller-started thing is distinguishable from a person-started thing everywhere *except* where it should be: the record. This cut exists to not repeat that finding: a button and a poller that start **by the same path**, produce the same record and leave the same evidence, with the trigger **recorded on the run** and the view not branching on it.

**Ticket link:** https://github.com/asantamariaplainconcepts/ai-sdlc-plugin/issues/1 (section POC-05). Umbrella story F-POC-1, use case UC-POC-1 (verify finished work), business rule BR-VERIFY-1 ("null is not zero" — absence and failure are different sentences).

**Assumptions (POC-05 owns these decisions, unattended run — recorded, each reversible):**

1. **What the poller polls** (the epic does not fully pin this) — the poller polls a **configured list of worktree paths** (config key `Watcher:Paths`, a list of absolute paths, default empty) and, for each, compares the worktree's present HEAD against the `starting_commit` of the worktree's most recent recorded run: HEAD differs ⇒ new work present ⇒ fire the launch contract for the **tests** step (the step whose panel already exists and whose gates follow a run). Recorded semantics a human must ratify: "new work" means "HEAD moved since the last recorded run", nothing about the diff's content. Reversible: a later slice can widen the predicate or make the step configurable (`Watcher:Step`).
2. **Interval, minutes not seconds** — default 300 seconds (`Watcher:IntervalSeconds`), the epic's rate-limit reasoning made concrete. This PoC's poller reads local git (cheap), but the number is recorded at the epic's suggested 5 minutes so a future GitHub-polling watcher inherits it without re-deciding.
3. **Off by default** — `Watcher:Enabled` defaults `false` under the harness's `configuration[ModeKey] ?? "disabled"` pattern: no key, no watcher, no loop, no launch. A disabled watcher starting nothing is the same silence as no watcher at all — accepted, recorded.
4. **Trigger vocabulary** — closed set of two: `button` and `poll`. Anything else is refused at the endpoint with a named sentence (declarations are checked, not guessed). The stored value rides the run row's new `trigger` column; `button` is the default when a caller does not say (the pre-POC-05 shape — a POST from the existing panel — stays valid without changes).
5. **Gates-empty-after-poll-run is a named failure, not silence** — POC-04's reading already says "gates have not run here" when a worktree declares gates but has no rows; the cut verifies this reads as **failure-adjacent** (a sentence, warn-toned) after a poller-started run, and tightens the header's checks fact tone for that state from Plain to **Warn** — an unrun gate after a run is a reviewable failure, not silence (epic check literal).
6. **Runs table gets its trigger column** — `runs` is created by POC-03 with `CREATE TABLE IF NOT EXISTS` (never migrated). This cut adds the column **defensively at startup**: `ALTER TABLE runs ADD COLUMN trigger TEXT` guarded by a pragma check (column presence), the minimal additive shape Store.cs's own convention allows — recorded, idempotent, no data touch. Old rows read as `trigger = null` = "not said", per BR-VERIFY-1.

## What Changes

- **Extended** `src/Plugin/Runs.cs` — `LaunchAndRecord(cwd, step, trigger = "button")`: the trigger rides the row; nothing else about the contract moves. The endpoint `POST /api/runs` gains a `trigger` query param validated against the closed vocabulary (`button` / `poll`), defaulting `button`.
- **Extended** `src/Plugin/Store.cs` — `runs.trigger` column (guarded `ALTER TABLE` at startup, additive, idempotent), `RunRow` gains `Trigger`, `RecordRun` writes it, `ListRuns` selects it back.
- **New** `src/Plugin/Watcher.cs` — `BackgroundService` + `PeriodicTimer`: reads `Watcher:Enabled` (default false), `Watcher:IntervalSeconds` (default 300), `Watcher:Paths` (default empty list); each tick, for each path, quick-refuses non-repositories, reads HEAD, compares against the worktree's latest recorded run's `starting_commit`, and fires `Runs.LaunchAndRecord(path, "tests", trigger: "poll")` when they differ. The config read (`WatcherConfig.Read`) and the launch decision (`Watcher.ShouldLaunch(lastStartingCommit, head)`) are pure and tested; the loop itself is hosted, not unit-tested (a fake-timer loop test is not worth its budget).
- **Extended** `src/Plugin/Program.cs` — `builder.Services.AddHostedService<Watcher>()` wiring with the config the watcher reads; `POST /api/runs` accepts `trigger`; `RunView` carries `trigger`.
- **Extended** `src/Plugin/Facts.cs` — the checks fact's "checks not run here" reading (gates declared, none run) tightens from Plain to Warn: a run with no gates recorded is a failure to look, not silence.
- **Extended** `src/web/src/steps/TestsStep.tsx` — the run button POSTs `trigger=button` explicitly; the run row does **not** render the trigger (the view does not branch on it); the gates "not run here" sentence renders with the warn ink it already has available.
- **New tests** — `WatcherTests.cs`: launch decision (HEAD moved vs not vs null), disabled-by-default config read, interval default, trigger vocabulary validation, trigger round-trip through the store (button and poll rows both recorded, both listed, trigger said), gates-empty-after-run tone. No test runs the real BackgroundService loop.

## Capabilities

### New Capabilities

- `run-triggers`: one launch contract with two triggers — the button and the poller — producing the same record and evidence; the trigger recorded on the run row and the view not branching on it; the poller off by default, minutes-not-seconds interval, and the HEAD-moved-not-run predicate it launches on.

### Modified Capabilities

None — `agent-runs` keeps its requirement text; the launch contract gains an optional third parameter and a recorded column, which is the additive shape its "runs insert, never overwrite" requirement already describes (a row with more facts, not fewer).

## Impact

- New backend file: `Watcher.cs`; new test file: `WatcherTests.cs`; extended: `Runs.cs` (one optional parameter), `Store.cs` (one column + one startup guard), `Program.cs` (hosted service + trigger param), `Facts.cs` (one tone change), `TestsStep.tsx` (one fetch line).
- Budget gate: this cut adds ≤ 150 cloc (epic allocation POC-05 ≤ 150; cumulative ≤ 1750 against the PoC total).
- No GitHub writes, no issue labels. The poller launches only through the same `Runs.LaunchAndRecord` the button uses — no second launch path exists to diverge.
