## ADDED Requirements

### Requirement: Declared review steps

The review steps of a worktree SHALL be read from a `.harness/review.json` file found from the worktree directory upward (the same search as `.harness/commands.json`), declaring an ordered `steps` array where each step carries a `key`, a `title`, and one line (`asserts`) saying what the step asserts. The file SHALL be read as JSONC (comments and trailing commas allowed) and byte-bounded before parsing. Nothing read from it SHALL be executed by its reader.

#### Scenario: Changing the file changes the wizard

- **WHEN** `.harness/review.json` declares an additional step and the reading is repeated without any code change
- **THEN** the rail draws the additional step, in the declared order

#### Scenario: JSONC tolerance

- **WHEN** the file contains `//` comments or trailing commas
- **THEN** it parses as declared, with the same JSONC semantics as `commands.json`

#### Scenario: Entries without a key or title are refused

- **WHEN** an entry in the `steps` array has a blank `key` or `title`
- **THEN** that entry is not drawn, and the rest parse

### Requirement: Absent and unreadable are named, never zero steps

A missing or unreadable `review.json` SHALL NOT draw zero steps silently. The reading SHALL state which of the two cases holds: absent — the folder declares no steps, with the remedy of declaring steps in the named `.harness/review.json` path — or unreadable — a parse failure or an over-bound file, with the problem and the file's path.

#### Scenario: No review.json

- **WHEN** the worktree directory chain has no `.harness/review.json`
- **THEN** the rail names the file absent and the path where one would be declared, and draws no step boxes

#### Scenario: Broken JSON

- **WHEN** the file exists but does not parse
- **THEN** the rail names it unreadable with the parse problem and the file's path, and draws no step boxes

#### Scenario: Oversized file refused before parsing

- **WHEN** the file exceeds the byte bound
- **THEN** the reading refuses it before parsing and states the bound it crossed

### Requirement: Progression marked against the provider

A step's mark SHALL be the presence of a `reviewed:<key>` label on the worktree's resolved issue, read through the same Octokit issue read the header already performs. The PoC SHALL NOT write labels. With no resolved issue, the marks SHALL be stated as not asked — naming why, with the same remedy the header's issue fact uses — rather than drawn as ticks or as silence.

#### Scenario: Label present

- **WHEN** the worktree's branch resolves an issue whose labels include `reviewed:code`
- **THEN** the code step draws its tick and a step with no label does not

#### Scenario: No resolved issue

- **WHEN** the worktree's branch names no digit run or the lookup refuses
- **THEN** the rail states that marks are not asked and why, and no step draws a tick

#### Scenario: Read-only provider

- **WHEN** any step's marks are read
- **THEN** nothing is written to GitHub — the tick is drawn only

### Requirement: Declared but unimplemented draws disabled

A declared step whose panel is not implemented SHALL draw disabled with the words "not implemented". It SHALL NOT disappear and SHALL NOT fake empty content. Which step keys have panels is a known set in code; the declared file is the source of what draws.

#### Scenario: Step outside the known set

- **WHEN** `review.json` declares a step whose key has no implemented panel
- **THEN** the step draws numbered and disabled, stating "not implemented", and cannot be opened

#### Scenario: Implemented step opens its panel

- **WHEN** a declared step's key is in the implemented set
- **THEN** the step draws enabled and opens a panel when chosen (the panel content itself is POC-02's scope)
