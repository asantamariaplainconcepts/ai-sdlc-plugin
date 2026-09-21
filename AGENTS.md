# AGENTS.md — ai-sdlc-plugin (PoC)

A PoC of a review facilitator: it observes worktrees, draws the header of a change (eight
facts), and — in later cuts — walks a review over it. It reviews finished work; it does not
accompany in-progress agent runs. The reference for all fact semantics is the harness at
`837c7ca`.

## Layout

- `src/Plugin/` — one .NET 10 minimal-API project: `Program.cs` (host + endpoints), `Git.cs`
  (porcelain execution), `Seam.cs` + `TaskResolution.cs` + `Facts.cs` (pure readings),
  `Commands.cs` (JSONC declared commands), `GitHub.cs` (Octokit + `gh auth token`),
  `Store.cs` (raw SQLite, `CREATE TABLE IF NOT EXISTS` at startup), `Header.cs` (orchestration).
- `src/Plugin.Tests/` — xunit + Shouldly over the pure parsers (fixtures, no network).
- `src/web/` — Vite + React 19.3 + TS 5.7.3, one screen. Later review steps are one panel
  component file each, mounted in one line of `App.tsx`.
- `gate.sh` — the one verification command. Run it before reporting work done.
- `.harness/commands.json` — this repo's own declared gates (JSONC).

## Rules that later cuts inherit

- **Null is not zero.** Every absent fact is its own sentence with its remedy; "could not be
  asked" is a different answer from "there is none".
- The vocabulary is closed: Folder, Worktree, Task, Workflow, Hold, Run, Agent.
- Declared files (`.harness/*.json`) are untrusted input: bounded before parsing, never
  executed by their reader.
- Backend budget is counted with cloc (`src/Plugin` + `src/web`, non-blank, non-comment,
  tests excluded from the count but required by the gate). POC-00 ≤ 400.
- Central package versions live in `Directory.Packages.props`; add packages there, never
  inline versions in csproj.
- Git reads are porcelain only; nothing here creates worktrees or writes to GitHub.
- GitHub reads authenticate with `gh auth token` — no PAT env vars.

## Check commands

```bash
./gate.sh                    # everything below, warnaserror on both sides
dotnet test src/Plugin.Tests/AiSdlcPlugin.Tests.csproj
(cd src/web && npm run build)
```
