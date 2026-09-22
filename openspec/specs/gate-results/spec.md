## Purpose

Running the declared gate commands over the candidate an agent left, recording exit code and a bounded tail against the resulting commit — so that moving HEAD invalidates the previous result and the step says so, a command that started and ran zero tests is never a pass, and a gate that cannot run is refuted when its declaration is read.

## Requirements

### Requirement: Gate commands run over the candidate and record against the resulting commit

The declared gate commands (`.harness/commands.json` with `"gate": true`) SHALL be executed in the worktree, each through the shared process seam, held to completion. For each gate SHALL be recorded: its command key, its exit code, a bounded tail of its output, the finish time, and the commit observed in the worktree AFTER the gates ran (the resulting commit — the agent may have committed meanwhile). Recording SHALL be INSERT-only: a re-run adds rows, it never edits earlier ones.

#### Scenario: Gates run and are recorded

- **WHEN** the declared gates are run over a worktree whose candidate work is committed at HEAD `H`
- **THEN** one row per declared gate exists, each carrying the exit code, the bounded tail, and `H` as the commit it was recorded against

#### Scenario: A re-run does not overwrite the earlier outcome

- **WHEN** the gates are run twice on the same worktree
- **THEN** both runs' rows exist, each with its own finish time and exit code

### Requirement: Three readings — pass, fail, no concluyente

Each recorded gate outcome SHALL be read as exactly one of three: **pass** (exit 0 and output was produced), **fail** (exit non-zero), **no concluyente** (the command could not start, timed out, was killed, crashed, or produced no output at all). A run with zero output SHALL NOT be read as a pass. The rule SHALL be applied uniformly with no per-command override.

#### Scenario: Pass

- **WHEN** a gate command exits 0 and produced output
- **THEN** the outcome is pass

#### Scenario: Fail

- **WHEN** a gate command exits non-zero
- **THEN** the outcome is fail, whatever its output says

#### Scenario: Silent success is inconclusive

- **WHEN** a gate command exits 0 and produced no output at all
- **THEN** the outcome is no concluyente, never pass — a gate that says nothing asserts nothing

#### Scenario: A killed or timed-out gate is inconclusive

- **WHEN** a gate command exceeds the timeout or is killed
- **THEN** the process is terminated, the row records the timeout, and the outcome is no concluyente — a wall clock is not a judge

### Requirement: Unrunnable gates are refuted at declaration time

When the declaration is read, a gate whose declared working directory does not exist, or whose run line's executable cannot be found (as a path relative to the declaration's own directory, or on PATH), SHALL be refuted at reading time — stated as `missing` with its remedy — and SHALL NOT be executed. Refutation happens before any run, not during it.

#### Scenario: The declared executable is missing

- **WHEN** `commands.json` declares a gate whose `run` names an executable that is neither a file that exists relative to the declaration's repo root nor on PATH
- **THEN** the reading shows that gate as missing with a sentence naming what is not findable, and no process is started for it

#### Scenario: An existing gate runs

- **WHEN** the declared `run` resolves to an executable that exists
- **THEN** the gate is launched rather than refuted

### Requirement: Moving HEAD invalidates the previous result

A recorded gate outcome SHALL be read as current only when the commit it was recorded against equals the worktree's present HEAD. When the two differ, the reading SHALL say the outcome is stale (recorded against `<short-hash>`, present HEAD is `<short-hash>`) instead of presenting it as valid.

#### Scenario: HEAD moves after the run

- **WHEN** gates were recorded against commit `A` and the worktree's HEAD is now `B`
- **THEN** the reading marks every recorded outcome as stale, naming both commits

#### Scenario: HEAD unchanged

- **WHEN** gates were recorded against commit `A` and HEAD is still `A`
- **THEN** the reading presents the outcome as current

### Requirement: Bounded tail of output

The stored output SHALL be the tail of the command's combined stdout and stderr, bounded to the last 64 lines and 8 KB, with truncation marked rather than silent.

#### Scenario: Long output is tailed

- **WHEN** a gate command produces more than 64 lines of output
- **THEN** the stored tail is the last 64 lines and records that earlier lines were cut

### Requirement: The surface presents the gates for the tests step

The gates reading SHALL be presentable per worktree: each declared gate with its name, its reading (pass / fail / no concluyente / not run / missing), the commit short-hash it was recorded against, a stale marker when HEAD has moved, and the stored tail. A trigger SHALL exist to run the gates on demand; running is a person's act.

#### Scenario: The reading for a worktree with gates run

- **WHEN** a worktree has recorded gate outcomes and HEAD is unchanged
- **THEN** the reading shows each gate's name, its reading, and the commit it is tied to

#### Scenario: Nothing has run yet

- **WHEN** a worktree declares gates but no outcome is recorded
- **THEN** the reading says the gates have not run here rather than showing zero or an empty pass
