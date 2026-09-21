## 1. Skeleton

- [ ] 1.1 Directory.Build.props + Directory.Packages.props + global.json (net10.0, Nullable, TreatWarningsAsErrors, LangVersion 14, central pins)
- [ ] 1.2 src/Plugin csproj + Program.cs minimal host serving /api/header and static src/web/dist
- [ ] 1.3 src/Plugin.Tests csproj (xunit 2.9.3 + Shouldly 4.3.0)
- [ ] 1.4 src/web Vite scaffold (React 19.3, TS 5.7.3, pinned dep set, tsconfig, vite.config dev proxy, App.tsx screen shell)
- [ ] 1.5 gate.sh + AGENTS.md + .harness/commands.json self-declaration

## 2. Pure readings (src/Plugin, tested)

- [ ] 2.1 Git.cs porcelain readings: status/porcelain, rev-list counts, diff --numstat against base + working, remote url, branch, head, unmerged entries
- [ ] 2.2 Seam.cs: section reader (title patterns, truncated→unreadable), parseSeam, compareSeam — harness parity
- [ ] 2.3 TaskResolution.cs: keyInBranch (last digit run) + confirmation semantics
- [ ] 2.4 Facts.cs: eight-fact derivation with tones, absence sentences with remedies, three-answer PR, five-reading checks
- [ ] 2.5 Store.cs: SQLite open + CREATE TABLE IF NOT EXISTS (worktrees, fact_cache)
- [ ] 2.6 Commands.cs: JSONC .harness/commands.json bounded read (comment skip, trailing commas)

## 3. Impure seams

- [ ] 3.1 GitHub.cs: gh auth token exec + Octokit PR-for-branch, issue read, repo identity from remote url; unreachable reasons typed
- [ ] 3.2 Header.cs: orchestrate reading for a path (git reads → resolution → GitHub reads → seam compare → facts DTO)

## 4. Screen

- [ ] 4.1 fetch /api/header, render facts row + step-rail placeholder + issue context line

## 5. Tests (two-subject check, fixtures)

- [ ] 5.1 Subject A fixtures: clean/absent — eight absences named (positive anchors + mutation proof)
- [ ] 5.2 Subject B fixtures: real changes — counts, seam rows vs epic-style Seam section, tones
- [ ] 5.3 Checks five readings over command outcome fixtures; PR three answers; truncated body → unreadable seam

## 6. Gate + measure

- [ ] 6.1 Run gate.sh green (dotnet -warnaserror build+test, web tsc+build)
- [ ] 6.2 cloc measure ≤ 400; record number in change
