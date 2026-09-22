# evidence-step

## ADDED Requirements

### Requirement: The Evidence step presents recorded runs read-only

The fourth review step (`evidence`) SHALL present the runs recorded against the worktree (the `GET /api/runs` listing, most recent first) as read-only evidence: per run the session id (short), the step, the exit code and the provider error flag (two facts, never collapsed), the cost (an absent cost stated as not given, never as zero currency), the transcript path with its located/absent sentence, the started and finished timestamps, and the trigger. The panel SHALL offer no control that launches, mutates or deletes a run.

#### Scenario: Runs recorded

- **WHEN** the worktree has recorded runs and the Evidence step is opened
- **THEN** each run renders as one row carrying session id short, step, exit code, provider error, cost or its stated absence, transcript located/absent, started/finished, and trigger

#### Scenario: No run recorded

- **WHEN** the worktree has no recorded run
- **THEN** the panel says no run is recorded yet and names where a run is launched from, rather than drawing an empty list without words

#### Scenario: The listing fails

- **WHEN** the runs listing cannot be fetched
- **THEN** the panel names the failure rather than rendering stale or fabricated rows

### Requirement: A declared evidence step opens only when implemented

The `evidence` key SHALL be part of the implemented-step set this build opens; a declared step whose key is outside that set continues to draw disabled with its not-implemented sentence. `.harness/review.json` declaring `evidence` is the data that draws the rail entry; `KnownImplemented` is what makes it open.

#### Scenario: The declared evidence step opens

- **WHEN** `.harness/review.json` declares the `evidence` step in this build
- **THEN** the rail draws it enabled, and selecting it mounts the evidence panel

#### Scenario: A worktree declares a fifth step this build does not implement

- **WHEN** a worktree's own `.harness/review.json` declares a step whose key is not in the implemented set
- **THEN** the rail draws it disabled with its not-implemented sentence, unchanged by this capability
