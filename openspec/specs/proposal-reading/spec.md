# proposal-reading Specification

## Purpose
TBD - created by archiving change poc-02-panels. Update Purpose after archive.
## Requirements
### Requirement: Live change discovery from the worktree

The declared change of a worktree SHALL be discovered by reading `openspec/changes/` live from the worktree's working tree (not `git show`), skipping the `archive` directory. Each live change directory SHALL be listed by name with its top-level Markdown artifacts (`*.md` directly under it). The reading SHALL NOT attempt to decide which of several live changes the branch is about.

#### Scenario: One live change

- **WHEN** the worktree's `openspec/changes/` holds exactly one non-archive directory containing `proposal.md` and `design.md`
- **THEN** the reading lists that change with both artifacts, and the panel opens with `proposal.md` rendered

#### Scenario: Several live changes

- **WHEN** `openspec/changes/` holds more than one non-archive directory
- **THEN** the panel lists all of them drawn the same way, names the count, and marks none of them as *the* change

#### Scenario: Archive is skipped

- **WHEN** `openspec/changes/archive/` is the only entry
- **THEN** the reading treats the branch as declaring no live change (not as declaring the archive)

### Requirement: Absence named with where it would be written

A branch with no declared change SHALL be stated as such, naming the path where a declaration would be written (`openspec/changes/<name>/proposal.md` under the worktree root). It SHALL NOT draw as an empty list without words.

#### Scenario: No change directory

- **WHEN** the worktree has no `openspec/` directory or no `openspec/changes/` directory
- **THEN** the panel states that the branch declares no change and names the path where one would be written

#### Scenario: Empty changes directory

- **WHEN** `openspec/changes/` exists but holds no non-archive directory
- **THEN** the panel states the same absence with the same named path

### Requirement: Bounded artifact reads

A single artifact SHALL be read bounded before parsing, with the same byte bound discipline as the declared files. An over-bound artifact SHALL be refused with the bound it crossed, not silently truncated. An unreadable artifact (permissions, disappeared between listing and read) SHALL be named with its path.

#### Scenario: Artifact over the bound

- **WHEN** the chosen artifact exceeds the byte bound
- **THEN** the reader refuses it before reading and the panel states the bound crossed

#### Scenario: Artifact vanishes between listing and reading

- **WHEN** the artifact file no longer exists when its read is requested
- **THEN** the panel states that the artifact could not be read and names its path

### Requirement: Markdown rendered as markdown

The Proposal step SHALL render a Markdown artifact as Markdown (headings, lists, tables) rather than as raw text. Rendering SHALL happen client-side from the artifact's raw text; no server-side markdown interpretation SHALL occur.

#### Scenario: A proposal with headings and a table

- **WHEN** the selected artifact contains `##` headings and a GFM table
- **THEN** the panel renders the headings at their levels and the table as a table

