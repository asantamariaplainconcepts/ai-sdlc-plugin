# run-triggers Specification

## MODIFIED Requirements

### Requirement: The trigger is recorded on the run

The run row SHALL carry the trigger it was launched with. A trigger that is not recorded (a row older than the column, or a caller that said nothing) SHALL be read as absent — said as "not said", never defaulted on read, never zero. A refused launch (unknown trigger, no declared prompt) SHALL also be a recorded row: the refusal is evidence — it answers "why did nothing happen", and the watcher's newest-row predicate reads it rather than looping on the same unanswered question. The refused row SHALL store no trigger (the column stays `button`/`poll`/not-said; the refused value is named in the problem sentence, never stored) and no session facts (no exit, no cost, no transcript) — the launch never happened, and the row says that rather than pretending a run did.

#### Scenario: A poller run is distinguishable in the record

- **WHEN** a poller-launched run and a button-launched run are recorded for the same worktree
- **THEN** both rows exist, each carrying its own trigger, and the listing shows both

#### Scenario: An old row carries no trigger

- **WHEN** a run recorded before the trigger column exists is listed
- **THEN** its trigger reads as not said, not as button and not as poll

#### Scenario: A refused launch is recorded, not silently dropped

- **WHEN** a launch is refused (unknown trigger or no declared prompt)
- **THEN** a row exists afterwards carrying the refusal's problem sentence, no trigger, and no session facts — and the worktree's listing shows it as the newest row

#### Scenario: A recorded refusal settles the watcher

- **WHEN** the watcher's HEAD-moved predicate would fire again after a refused launch
- **THEN** the newest row's presence is what the next tick compares against — a refusal is an answer, not silence
