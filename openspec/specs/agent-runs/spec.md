## Purpose

Launching the review step's agent in a worktree and recording the run against the commit it started from — declared prompts, pinned session ids, exits and costs, transcripts by the measured rule; a run is recorded whatever happened, and two runs are two rows.

## Requirements

### Requirement: Prompt resolution from the declared prompts map

The prompt a review step's run is launched with SHALL be read from an optional `prompts` map in the same `.harness/review.json` the steps are declared in, keyed by step key, the value a string. A step with no entry — or a file with no `prompts` map — SHALL be stated as having no prompt with the remedy of declaring one there; it SHALL NOT be launched with an invented prompt. Nothing read from the map SHALL be executed by its reader.

#### Scenario: A step with a declared prompt

- **WHEN** `review.json` carries a `prompts` map with the key `tests` and a run is asked for the `tests` step
- **THEN** the run is launched with exactly that string as the prompt

#### Scenario: No prompts map

- **WHEN** `review.json` declares steps but no `prompts` map
- **THEN** asking for a run of any step states that no prompt is declared for it, with the file and key named, and launches nothing

#### Scenario: The step key has no entry

- **WHEN** the `prompts` map exists but the asked step's key is absent from it
- **THEN** the response states no prompt is declared for that key and launches nothing

### Requirement: Launch runs to completion against the starting commit

Launching SHALL start the claude CLI as a process in the worktree (`--print --output-format json --session-id <guid> --permission-mode acceptEdits`), with a session id minted before launch and the commit read at launch recorded as the run's `starting_commit`. The launcher SHALL wait for the process to exit — no streaming, no chat, no permission round-trip — and the record SHALL be against the commit the run started from even though the agent may have moved HEAD meanwhile.

#### Scenario: A run starts and finishes

- **WHEN** a run with a declared prompt is launched in a repository worktree
- **THEN** the session id exists before launch, the run starts from the current HEAD, the process is waited on to exit, and a record exists with that session id and that starting commit

#### Scenario: The record is written even when the agent fails

- **WHEN** the launched process exits non-zero or its output blob says `is_error`
- **THEN** the record still exists, carrying the exit code and the parsed error flag, and the two facts (process-level failure, provider-level error) are distinguishable in the record

### Requirement: The output blob is parsed tolerantly

The run's captured stdout — one JSON blob from `--output-format json` — SHALL be parsed for session id, cost, error flag, turns and duration when present. An absent cost SHALL be recorded as absent (null, stated on read) and never zero. A blob that does not parse, or a stdout that is empty because the process never ran, SHALL be its own recorded problem sentence, not a crash.

#### Scenario: A blob with a cost

- **WHEN** the blob's `total_cost_usd` is present
- **THEN** the run row carries it as a number with the provider's precision

#### Scenario: A blob with no cost

- **WHEN** the blob parses but carries no `total_cost_usd`
- **THEN** the run row carries no cost and the reading of the row says it was not given, not that it was zero

#### Scenario: Unparseable stdout

- **WHEN** the process exited but the stdout is not a JSON blob
- **THEN** the run record exists with the exit code and a problem sentence naming that the output did not parse

### Requirement: Transcript located without guessing

The transcript location SHALL be derived from the rule `~/.claude/projects/<munged-cwd>/<session-id>.jsonl`, where munged-cwd is the worktree's real path (symlinks resolved) with `/`, `.`, `_` and space replaced by `-`. The row SHALL carry the path only when the file exists after exit; an absent transcript SHALL be recorded as transcript-absent with the derived path named — stated, never shown as though it were located, and never guessed elsewhere.

#### Scenario: The transcript lands where the rule says

- **WHEN** a run finishes and the file at the derived path exists
- **THEN** the row carries the transcript path as located

#### Scenario: No transcript

- **WHEN** a run finishes and no file exists at the derived path
- **THEN** the row records the transcript as absent and the reading names the derived path that is not there

### Requirement: Runs insert, never overwrite

Recording a run SHALL be an INSERT. Two runs on the same worktree, whatever their outcome, SHALL be two rows, distinguished by their own id and session id. No later run SHALL update an earlier row.

#### Scenario: Two runs, one worktree

- **WHEN** two runs are recorded for the same worktree and step
- **THEN** both rows exist afterwards, each with its own session id, and the listing shows both

### Requirement: Run list per worktree

A worktree's runs SHALL be listable, most recent first, each carrying session id, step, exit code, error flag, cost-or-absence, start and finish, and transcript located-or-absent.

#### Scenario: Listing runs

- **WHEN** a worktree with recorded runs is listed
- **THEN** every recorded run appears with its facts, an absent cost stated as absent
