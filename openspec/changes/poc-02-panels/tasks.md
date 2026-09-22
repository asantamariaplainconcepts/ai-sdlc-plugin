## 1. Proposal reading (backend)

- [ ] 1.1 `src/Plugin/Change.cs`: `Discover(root)` walking `openspec/changes/` (skip `archive`), one level of `*.md` per change, absence named with the where-it-would-be-written path; `ReadArtifact(path)` byte-bounded with the same refusal sentences as the declared files
- [ ] 1.2 Program.cs: `GET /api/proposal?path=` (list payload with root) and `GET /api/artifact?path=&file=` (one artifact's text)

## 2. Code reading (backend)

- [ ] 2.1 `src/Plugin/Patch.cs`: pure `Parse(patchText)` → files/hunks/lines with kinds, old/new numbers, binary + rename detection, `\ No newline` tolerance
- [ ] 2.2 `Patch.Cap(files, 200)` with the hidden-lines count and the global (not per-file) counting rule
- [ ] 2.3 `Git.cs`: `Patch(cwd, basis)` porcelain read (`--unified=3 --no-color`, `core.quotepath=off`) + untracked files as synthesized all-added blocks
- [ ] 2.4 Program.cs: `GET /api/code?path=` — basis via existing `DiffBasis`, no-trunk and not-a-repository stated with remedies
- [ ] 2.5 `ReviewSteps.KnownImplemented` gains `proposal` and `code`

## 3. Panels (frontend)

- [ ] 3.1 `src/web/src/steps/ProposalStep.tsx`: change/artifact list, selection-follows-the-list, markdown via react-markdown + remark-gfm, absence sentence with the named path, oversize/unreadable artifact sentences
- [ ] 3.2 `src/web/src/steps/CodeStep.tsx`: file list + one patch, hand-drawn hunks (gutters, marker column, tints), binary rows, cut marker naming hidden lines, diff-not-asked sentences
- [ ] 3.3 `package.json`: add `react-markdown`, `remark-gfm`; App.tsx: mount both panels (one branch each) in the panel region

## 4. Tests

- [ ] 4.1 `PatchTests.cs`: kinds/numbers/classification, hunk headers, binary, rename, untracked synthesized blocks, cap at exactly 200 (no marker) and over (marker + hidden count), empty patch
- [ ] 4.2 `ChangeTests.cs`: discover present/multiple/archive-skipped, absence named with remedy path, oversize artifact refused, vanished artifact named with path
- [ ] 4.3 Absence assertions proven: positive anchor from the same fixture, then mutate (drop cap below fixture size / delete file between list and read), watch assertions fail, revert, confirm empty diff

## 5. Gate

- [ ] 5.1 `./gate.sh` green (dotnet build+test warnaserror, web tsc+vite build, openspec validate --strict)
- [ ] 5.2 cloc budget: ≤ 250 lines added this cut (tests excluded), measured with the perl cloc
- [ ] 5.3 Two-subject spot check by hand: this worktree (live change `poc-02-panels`, diff over the merge base, markdown rendering) and a path with no openspec dir (absence named)
