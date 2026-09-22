# worktree-header Specification

## Purpose
TBD - created by archiving change poc-00-header. Update Purpose after archive.
## Requirements
### Requirement: Eight header facts over a worktree

For a given worktree directory, the system SHALL answer eight facts: pull request, declared checks, changed files with `+/-` counts, ahead/behind against the base, merge conflicts, Seam comparison against the resolved issue's Seam section, whether the tree is clean, and the issue context itself (resolved task key, issue title/state). The reading SHALL match the fact semantics of harness `837c7ca`: PR has three answers (a PR, no PR, unreachable — never collapsed), declared checks distinguish undeclared from never-run from failed from stale from passed-at-commit, and the Seam distinguishes compared from no-task/no-seam from unreadable-body.

#### Scenario: Clean worktree with no issue or PR

- **WHEN** a clean worktree's header is read and the branch names no digit run and no pull request exists
- **THEN** every one of the eight facts is stated as its own absence with its remedy (e.g. "no PR", "no checks declared", "issue unresolved"), not as zero or silence

#### Scenario: Worktree with real changes

- **WHEN** a worktree with committed and uncommitted changes on a branch whose last digit run resolves to an existing issue is read
- **THEN** the facts state the changed files with added/removed counts, ahead/behind counts, merge cleanliness, the Seam rows comparing the issue's declared paths against touched paths, and the tree state

#### Scenario: Degraded GitHub access

- **WHEN** `gh auth token` yields no token or Octokit refuses the repository
- **THEN** the PR fact and the issue context are reported as unreachable with the remedy named (run `gh auth login`), not as "no pull request" or an opaque failure

### Requirement: Absence is named, never zero-filled

The reading SHALL distinguish "not present" from "could not be asked" from "asked and there is none" for every fact. `null` SHALL NOT be rendered as zero. A fact with no source is stated in its own words.

#### Scenario: No commands declared

- **WHEN** the worktree's `.harness/commands.json` declares no gate command
- **THEN** the checks fact says no checks are declared (with the file path to declare them in), which is a different sentence from "checks have not run"

#### Scenario: Truncated issue body

- **WHEN** the resolved issue's body has no Seam section but the body is marked truncated (mirrored front only)
- **THEN** the Seam fact says the body could not be read whole, not that the issue declares no seam

### Requirement: Issue resolution by branch proposal plus lookup

The system SHALL propose a task key from the branch name's last digit run (e.g. `change/275` proposes `#275`) and confirm it by an Octokit issue lookup scoped to the repository. The proposal alone SHALL NOT resolve an issue — a lookup that fails (404, no auth) refuses the proposal. A branch with no digits proposes nothing.

#### Scenario: Branch digits that do not resolve

- **WHEN** a branch named `release/24` proposes `#24` and the repository has no issue 24
- **THEN** the issue context fact reports no task resolved, not issue 24

#### Scenario: No digits in branch

- **WHEN** the branch is `fix/narrow-layout`
- **THEN** no task key is proposed and the Seam fact reports no task to compare against

### Requirement: Facts are computed by pure, testable readings

The derivation of the eight facts SHALL live in pure functions over plain data (porcelain outputs, GitHub responses as DTOs), separated from the process/IO layer, so the fact semantics are testable without GitHub or a live worktree.

#### Scenario: Fixture-driven fact derivation

- **WHEN** the fact functions are given parsed fixture data for both a clean/absent subject and a real-changes subject
- **THEN** the derived facts match the harness semantics quoted above, verified by tests covering both subjects

### Requirement: Octokit reads only

GitHub access SHALL be read-only through Octokit, authenticated with the token from `gh auth token`. The system SHALL NOT create, label, comment, or write anything on GitHub, and SHALL NOT create worktrees.

#### Scenario: Read-only surface

- **WHEN** the header is read for any worktree
- **THEN** only GitHub read endpoints are called: pull request for branch, issue by number, repository identity

