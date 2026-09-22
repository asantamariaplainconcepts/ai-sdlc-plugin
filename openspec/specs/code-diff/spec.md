# code-diff Specification

## Purpose
TBD - created by archiving change poc-02-panels. Update Purpose after archive.
## Requirements
### Requirement: The diff as parsed data

The Code step SHALL present the change's diff from the merge base of the branch and its trunk (the same basis the header's counts use), parsed into per-file hunks with lines classified as `added`, `removed`, or `context`, each carrying its old and new line numbers where they exist. The parse SHALL be a pure function over the patch text and SHALL NOT depend on a diff library.

#### Scenario: A file with added, removed, and context lines

- **WHEN** the patch for a touched file contains all three line kinds
- **THEN** each line is classified with its kind, its old and new numbers, and its text

#### Scenario: Binary file

- **WHEN** a touched file is binary
- **THEN** the file is marked binary and no line rows are drawn for it

#### Scenario: A file git has never seen

- **WHEN** the branch contains a file git has never been told about (untracked)
- **THEN** the file appears with all its lines marked added, rather than not appearing

### Requirement: Added and removed separated

The rendering SHALL draw added and removed lines distinguishably (tint and marker both, not color alone), with old/new gutters and the marker column the harness's patch view uses. Files SHALL be grouped one per file, hunks under their header.

#### Scenario: Reading a hunk

- **WHEN** a hunk with mixed added and removed lines renders
- **THEN** the removed lines show in the old-number gutter with a removal marker, the added lines in the new-number gutter with an addition marker, and the context lines in both

### Requirement: The two-hundred-line cap

The reading SHALL present at most 200 diff body lines (added, removed, and context together, counted across the whole change in order). A diff exceeding the cap SHALL be cut with a visible marker at the cut point naming how many lines are hidden, rather than being dumped entire.

#### Scenario: A diff over the cap

- **WHEN** the change's diff contains more than 200 body lines
- **THEN** the panel draws exactly the first 200 and a marker naming the count of hidden lines

#### Scenario: A diff at the cap

- **WHEN** the change's diff contains exactly 200 body lines
- **THEN** the panel draws it entire with no cut marker

### Requirement: A diff that cannot be asked

A worktree with no trunk to diff against SHALL state that (with the same remedy the header's base fact uses), not an empty diff. A worktree that is not a repository SHALL state that.

#### Scenario: No trunk

- **WHEN** the worktree's branch has no upstream/trunk to merge-base against
- **THEN** the Code panel states that the diff is not asked because there is no trunk, and names the remedy

#### Scenario: Not a repository

- **WHEN** the pointed path is not a git repository
- **THEN** the Code panel states that and does not draw a diff

