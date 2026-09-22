# agent-runs Specification

## MODIFIED Requirements

### Requirement: Runs insert, never overwrite

Recording a run SHALL be an INSERT. Two runs on the same worktree, whatever their outcome, SHALL be two rows, distinguished by their own id and session id. No later run SHALL update an earlier row. A row exists whatever happened — launched, failed, or refused before launch — each its own row.

#### Scenario: Two runs, one worktree

- **WHEN** two runs are recorded for the same worktree and step
- **THEN** both rows exist afterwards, each with its own session id, and the listing shows both

#### Scenario: A refused launch leaves its row

- **WHEN** a launch is asked with an unknown trigger or a step with no declared prompt
- **THEN** no process launches, and the refusal is recorded as its own row so the listing names why nothing ran
