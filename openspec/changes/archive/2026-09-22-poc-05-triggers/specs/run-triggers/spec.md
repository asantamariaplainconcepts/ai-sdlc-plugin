## ADDED Requirements

### Requirement: One launch contract carries its trigger

The launch contract SHALL be the same code path for every trigger: `LaunchAndRecord(cwd, step, trigger)`. The trigger vocabulary SHALL be closed — `button` and `poll` — and anything else SHALL be refused with a named sentence before a session id is minted. A caller that does not say a trigger SHALL be recorded as `button`, the shape every pre-existing caller had.

#### Scenario: The button starts a run

- **WHEN** a run is launched with trigger `button`
- **THEN** the row is recorded through the same contract as every other launch, carrying trigger `button`

#### Scenario: The poller starts a run

- **WHEN** a run is launched with trigger `poll`
- **THEN** the row is recorded through the same contract as the button's, and the two rows differ in no column except the trigger

#### Scenario: An unknown trigger is refused

- **WHEN** a launch is asked with a trigger outside the closed vocabulary
- **THEN** no run launches and the answer names the two triggers it could have been

### Requirement: The trigger is recorded on the run

The run row SHALL carry the trigger it was launched with. A trigger that is not recorded (a row older than the column, or a caller that said nothing) SHALL be read as absent — said as "not said", never defaulted on read, never zero.

#### Scenario: A poller run is distinguishable in the record

- **WHEN** a poller-launched run and a button-launched run are recorded for the same worktree
- **THEN** both rows exist, each carrying its own trigger, and the listing shows both

#### Scenario: An old row carries no trigger

- **WHEN** a run recorded before the trigger column exists is listed
- **THEN** its trigger reads as not said, not as button and not as poll

### Requirement: The view does not branch on the trigger

The wizard's rendering of a run SHALL NOT read the trigger. A run started by the poller and a run started by the button SHALL be indistinguishable in the wizard — the possibility of divergence lives in the record, and nowhere else.

#### Scenario: Two runs, one rendering

- **WHEN** the wizard lists a poller-started run and a button-started run of the same step
- **THEN** the two rows render through the same code with the same facts, and no element of the interface says which trigger started which

### Requirement: The poller is off by default with a minutes-scale interval

A periodic watcher SHALL exist in-process (BackgroundService + PeriodicTimer), reading its shape from configuration: `Watcher:Enabled` (default false), `Watcher:IntervalSeconds` (default 300), `Watcher:Paths` (default empty). Disabled or non-positive interval SHALL mean the loop never ticks — indistinguishable from no watcher at all. The interval default SHALL be minutes-scale, honoring the rate-limit reasoning a GitHub-bound successor inherits.

#### Scenario: No configuration, no ticks

- **WHEN** the host starts with no watcher configuration present
- **THEN** nothing launches, no loop is scheduled, and the app serves as before

#### Scenario: The interval config is read

- **WHEN** the configuration declares `Watcher:Enabled` true with no interval
- **THEN** the watcher ticks at the default interval — 300 seconds, five minutes

### Requirement: The poller launches on HEAD moved since the last run

The watcher's predicate SHALL be: for each configured worktree path, the worktree's present HEAD compared against the starting commit of the most recently recorded run — differ ⇒ launch the launch contract for the tests step with trigger `poll`; equal ⇒ nothing. A path that is not a directory, or not a repository, SHALL be skipped without a launch and without an error. No run recorded at all for the path ⇒ a first run launches.

#### Scenario: HEAD moved since the last run

- **WHEN** the last recorded run for a configured path started from commit A, and the path's HEAD is now B
- **THEN** the watcher launches the tests step through the same contract as the button, with trigger `poll`

#### Scenario: HEAD unchanged since the last run

- **WHEN** the last recorded run for a configured path started from the commit the path is still on
- **THEN** the watcher launches nothing that tick

#### Scenario: A configured path is not a worktree

- **WHEN** a configured path does not exist or describes no git repository
- **THEN** that path is skipped, no run launches for it, and the tick survives to the next path

#### Scenario: No run ever recorded

- **WHEN** a configured path is a repository whose worktree has no recorded run
- **THEN** the watcher launches the tests step — a first run is new work

### Requirement: Empty gates after a run read as failure, not silence

A run — however triggered — arriving with no recorded gate outcomes SHALL be shown as a named failing reading, never as quiet success. The header's checks fact SHALL read gates-declared-but-none-run with the warn tone; the panel SHALL say per gate that it has not run here.

#### Scenario: A poller run with empty gates

- **WHEN** a poller-started run has finished and the worktree's gate outcomes are still unrecorded
- **THEN** the header's checks fact reads as warn — not plain — naming that gates have not run, and the panel says so per gate rather than rendering quiet success

#### Scenario: Gates that have run read as before

- **WHEN** gate outcomes are recorded against the present HEAD
- **THEN** their readings render with the tones POC-04 established, unchanged by this cut
