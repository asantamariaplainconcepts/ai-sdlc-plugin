# poc-shell Specification

## Purpose
TBD - created by archiving change poc-00-header. Update Purpose after archive.
## Requirements
### Requirement: Single gate command

The repository SHALL provide `gate.sh` runnable from the repo root as the one verification command. It SHALL fail on any warning on both sides: `dotnet build` and `dotnet test` with `-warnaserror` over the solution, and the web gates via `npm run` scripts (`tsc --noEmit` and `vite build`).

#### Scenario: Red either side

- **WHEN** either the .NET build/test or the web typecheck/build fails or warns
- **THEN** `gate.sh` exits non-zero

### Requirement: Self-declared commands

The repository SHALL declare its own gate command in `.harness/commands.json` with `"gate": true`, using the same declaration vocabulary as the harness (`key`, `name`, `run`, `gate`). The file is read with JSONC semantics (comments allowed, trailing commas allowed).

#### Scenario: Header reads this repo's own declaration

- **WHEN** the header capability reads this repository's own worktree
- **THEN** the declared-checks fact finds the gate command declared in `.harness/commands.json` and reports its last recorded outcome per the checks semantics

### Requirement: Budgeted surface

The manually written code in `src/Plugin` and `src/web` SHALL stay within the epic's per-cut budget (`cloc`-counted, non-blank, non-comment, tests excluded from count but required by the gate). POC-00's budget is 400 lines.

#### Scenario: Budget not exceeded

- **WHEN** `cloc` counts `src/Plugin` + `src/web` (excluding test projects)
- **THEN** the count is ≤ 400 for this cut, and the tests exist and pass under the gate

