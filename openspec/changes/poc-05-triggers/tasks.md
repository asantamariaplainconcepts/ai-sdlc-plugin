## 1. Store + row carry the trigger (test-first)

- [x] 1.1 `RunRow` gains `Trigger` (trailing position); `Store` CREATE adds `trigger TEXT` to `runs` and the constructor gains the guarded `ALTER TABLE runs ADD COLUMN trigger TEXT` (pragma-checked, idempotent over an existing POC-03 database — the migration an old file needs, proved)
- [x] 1.2 `Store.RecordRun` writes it; `Store.ListRuns` selects it back; a row recorded with `poll` and one with `button` both list, each with its own trigger
- [x] 1.3 An old-shape row (trigger not said) reads as null — not button, not poll, not empty string

## 2. The one contract takes a trigger

- [x] 2.1 `Runs.LaunchAndRecord(cwd, step, trigger = "button")` — the value rides the row, nothing else about the contract moves; trigger vocabulary (`button`/`poll`) validated before a session id is minted, unknown values refused with a named sentence
- [x] 2.2 `POST /api/runs` gains `?trigger=` (validated, default button); `RunView` carries `trigger`
- [x] 2.3 `TestsStep.tsx` run button POSTs `trigger=button` explicitly; the run row renders no trigger fact (the view does not branch on it — absence of code is the acceptance)

## 3. The watcher

- [x] 3.1 `WatcherConfig.Read(IConfiguration)` — Enabled default false, IntervalSeconds default 300, Paths default empty list; tests over a memory configuration prove all three defaults and their configured overrides
- [x] 3.2 `Watcher.ShouldLaunch(lastStartingCommit, head)` — pure: equal ⇒ false; differ ⇒ true; no recorded run (null last) ⇒ true; unreadable HEAD (null now) ⇒ false — compared against nothing, launched nothing
- [x] 3.3 `Watcher` BackgroundService: `ExecuteAsync` returns immediately when disabled or interval ≤ 0; otherwise `PeriodicTimer(IntervalSeconds)` — per tick, per path: skip non-existent/non-repository paths, compare `ListRuns(path)` first row's `StartingCommit` against `git.Head(path)`, `ShouldLaunch` ⇒ `Runs.LaunchAndRecord(path, "tests", "poll")`; per-path try/catch keeps one bad path from stopping the others; wired in `Program.cs` via `AddHostedService` + the singletons the DI resolution needs
- [x] 3.4 No test runs the real loop — the decision, the config and the refusal paths are the tested surface (recorded in the design)

## 4. Gates empty is failure, not silence

- [x] 4.1 `Facts.Checks` "gates declared, none run" tone: Plain → Warn (a run with no gates recorded is a failure to look, not quiet); `FactsTests` pinned tone tightened
- [x] 4.2 `TestsStep.tsx` "not run here" gate reading renders with the warn ink it has — verified, not changed, if already warn

## 5. Verification

- [x] 5.1 `gate.sh` green: dotnet build+test (warnaserror), web build, openspec validate --strict
- [x] 5.2 cloc budget: added ≤ 150 over the 1933 baseline (tests excluded from the count but required green)
- [x] 5.3 Absence assertions proven per contract: force the disabled-watcher/empty-gates branch on, re-run, require the absence assertions to fail, revert, empty diff, green again
- [x] 5.4 The two-trigger evidence, recorded: a `poll`-triggered `LaunchAndRecord` refused-and-recorded run against a temp worktree through the store (button and poll rows side by side in one listing — same shape, different trigger) — not a test, a recorded check
