# proposal-reading Specification

## MODIFIED Requirements

### Requirement: Bounded artifact reads

A single artifact SHALL be read bounded before parsing, with the same byte bound discipline as the declared files. An over-bound artifact SHALL be refused with the bound it crossed, not silently truncated. An unreadable artifact (permissions, disappeared between listing and read) SHALL be named with its path. The artifact read SHALL be confined: the caller names a worktree and a path relative to it, the resolved path SHALL sit under a change root that worktree's own live discovery listed, and any escape — an absolute path, a `..` climb, or a path outside `openspec/changes/` — SHALL be refused with a named sentence naming the listing it could have come from. A path that is relative and inside the changes tree but names no listed change root SHALL be answered with the discovery's absence sentence, not read.

#### Scenario: Artifact over the bound

- **WHEN** the chosen artifact exceeds the byte bound
- **THEN** the reader refuses it before reading and the panel states the bound crossed

#### Scenario: Artifact vanishes between listing and reading

- **WHEN** the artifact file no longer exists when its read is requested
- **THEN** the panel states that the artifact could not be read and names its path

#### Scenario: An escape attempt is refused

- **WHEN** the artifact read is asked for a path that is absolute, climbs with `..`, or resolves outside the worktree's `openspec/changes/`
- **THEN** no file is read outside the listed change roots and the refusal names the refusal

#### Scenario: A listed artifact still reads

- **WHEN** the artifact read is asked for the same path the proposal listing returned
- **THEN** the artifact reads exactly as before the confinement

### Requirement: Absence named with where it would be written

A branch with no declared change SHALL be stated as such, naming the path where a declaration would be written (`openspec/changes/<name>/proposal.md` under the worktree root). It SHALL NOT draw as an empty list without words. The absence sentence SHALL be served by the proposal reading itself (one source of truth); the panel SHALL render the served sentence rather than rebuilding it from the root.

#### Scenario: No change directory

- **WHEN** the worktree has no `openspec/` directory or no `openspec/changes/` directory
- **THEN** the panel states that the branch declares no change and names the path where one would be written

#### Scenario: Empty changes directory

- **WHEN** `openspec/changes/` exists but holds no non-archive directory
- **THEN** the panel states the same absence with the same named path

#### Scenario: The served sentence is the one drawn

- **WHEN** the proposal response carries the absence sentence
- **THEN** the panel draws that sentence, not a sentence it composed itself
