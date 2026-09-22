## 1. Skeleton

- [x] 1.1 Directory.Build.props + Directory.Packages.props + global.json (net10.0, Nullable, TreatWarningsAsErrors, LangVersion 14, central pins)
- [x] 1.2 src/Plugin csproj + Program.cs minimal host serving /api/header and static src/web/dist
- [x] 1.3 src/Plugin.Tests csproj (xunit 2.9.3 + Shouldly 4.3.0)
- [x] 1.4 src/web Vite scaffold (React 19.3, TS 5.7.3, pinned dep set, tsconfig, vite.config dev proxy, App.tsx screen shell)
- [x] 1.5 gate.sh + AGENTS.md + .harness/commands.json self-declaration

## 2. Pure readings (src/Plugin, tested)

- [x] 2.1 Git.cs porcelain readings: status/porcelain, rev-list counts, diff --numstat against base + working, remote url, branch, head, unmerged entries
- [x] 2.2 Seam.cs: section reader (title patterns, truncated→unreadable), parseSeam, compareSeam — harness parity
- [x] 2.3 TaskResolution.cs: keyInBranch (last digit run) + confirmation semantics
- [x] 2.4 Facts.cs: eight-fact derivation with tones, absence sentences with remedies, three-answer PR, five-reading checks
- [x] 2.5 Store.cs: SQLite open + CREATE TABLE IF NOT EXISTS (worktrees, fact_cache)
- [x] 2.6 Commands.cs: JSONC .harness/commands.json bounded read (comment skip, trailing commas)

## 3. Impure seams

- [x] 3.1 GitHub.cs: gh auth token exec + Octokit PR-for-branch, issue read, repo identity from remote url; unreachable reasons typed
- [x] 3.2 Header.cs: orchestrate reading for a path (git reads → resolution → GitHub reads → seam compare → facts DTO)

## 4. Screen

- [x] 4.1 fetch /api/header, render facts row + step-rail placeholder + issue context line

## 5. Tests (two-subject check, fixtures)

- [x] 5.1 Subject A fixtures: clean/absent — eight absences named (positive anchors + mutation proof)
- [x] 5.2 Subject B fixtures: real changes — counts, seam rows vs epic-style Seam section, tones
- [x] 5.3 Checks five readings over command outcome fixtures; PR three answers; truncated body → unreadable seam

## 6. Gate + measure

- [x] 6.1 Run gate.sh green (dotnet -warnaserror build+test, web tsc+build)
- [x] 6.2 cloc measure ≤ 400; record number in change
