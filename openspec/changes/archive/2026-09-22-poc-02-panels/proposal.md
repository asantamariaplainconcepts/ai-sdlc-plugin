# poc-02-panels — the Proposal and Code step panels

## Why

Epic #1 (ticket POC-02, section `### POC-02 · Proposal y Code`) asks for the third cut of the PoC: the first two review steps stop being "not implemented" rail entries and become panels. The Proposal step reads what the branch declares as its change — the `openspec/changes/<name>/` directory on the current worktree's checked-out branch, read live from the working tree, not `git show`. The Code step presents the diff as a diff: added/removed separated, per-file grouping, drawn by hand (no diff library, ~150 lines pinned by the epic), with a visible cap at 200 presented lines. Both steps join `ReviewSteps.Known Implemented` so the rail stops drawing them disabled.

**Assumptions (POC-02 owns these decisions, unattended run):**

1. **Proposal discovery** — the worktree's `openspec/changes/` directory read from disk (`Directory.EnumerateDirectories`, bounded), skipping `archive/`. A branch with no live change directory says so and names where one would be written (`openspec/changes/<name>/proposal.md` under the worktree root) — absence named with remedy, the POC-00 discipline. When several live changes exist, all are drawn the same and neither is marked as the one (harness `ProposalStep` parity — which change a branch is about is not a fact on disk).
2. **Proposal artifacts** — `proposal.md` renders first (it is the step's namesake); every `*.md` directly under the change directory is selectable. Specs deltas under `specs/` are listed as rows but not fetched this cut (debt: POC-03+ may widen). One artifact at a time renders, one file per read, byte-bounded.
3. **Markdown rendering** — `react-markdown` + `remark-gfm`, both pinned by the epic for exactly this step ("react-markdown + remark-gfm are yours to add"). No sanitization config beyond defaults; content is the repo's own declaration, same trust level as the file in an editor.
4. **Endpoint shape** — one endpoint per step, mirroring `/api/steps`: `GET /api/proposal?path=` returns the change list with artifact names, and `GET /api/code?path=` returns per-file hunks. A single `/api/step/<key>` was rejected: the two readings share no shape (a directory list vs a parsed patch), and one endpoint with a union payload costs the frontend a discriminator it does not need.
5. **Patch source** — `git diff --unified=3 <merge-base>...HEAD` porcelain text, plus `git diff --unified=3` (same basis) covers committed work; untracked files ride along as all-added rows (a new file is invisible to git diff until staged — POC-00's numstat rule, same reasoning). Parse is a pure function over the patch text.
6. **The 200-line cap** counts presented diff body lines (added+removed+context) across the whole change, not per file; the cut marker names how many lines are hidden. Epic wording: "a diff over 200 lines is cut with a visible marker, not dumped" — the marker is at the cut point, the count is named.
7. **Merge-base reuse** — the Code step reuses `Git.DiffBasis(cwd, trunk)` (POC-00's reading) rather than inventing a second basis rule.

## What Changes

- **New** `src/Plugin/Change.cs` — the proposal reading: discover live `openspec/changes/*` on disk under the worktree, list their `*.md` artifacts, read one artifact bounded. Absence (no changes dir, no live change) is its own sentence naming where one would be written.
- **New** `src/Plugin/Patch.cs` — pure patch parsing: unified-diff text → per-file hunks → lines `{kind: added|removed|context, oldLine, newLine, text}`, binary detection, the 200-line cap with `cutAfter` + hidden count.
- **Extended** `src/Plugin/Git.cs` — one porcelain read added: `Patch(cwd, basis)` returning `git -c core.quotepath=off diff --unified=3 --no-color` text against the basis (plus an untracked-file pass for new files).
- **Extended** `src/Plugin/ReviewSteps.cs` — `KnownImplemented` gains `proposal` and `code` (the epic's explicit instruction — the rail stops drawing them disabled).
- **Extended** `src/Plugin/Program.cs` — two endpoints: `GET /api/proposal?path=`, `GET /api/code?path=`.
- **New** `src/web/src/steps/ProposalStep.tsx` + `src/web/src/steps/CodeStep.tsx` — one panel file per step (POC-00 convention), mounted in App.tsx's panel region in one branch each.
- **New** web deps `react-markdown`, `remark-gfm` (epic pins them for the Proposal step).
- **New** tests `ChangeTests.cs` (discovery: present/multiple/absent-named-with-remedy) and `PatchTests.cs` (parse: added/removed/context split, hunk headers, rename shapes, binary, cap behaviour with hidden count) — pure functions, fixtures only.

## Capabilities

### New Capabilities

- `proposal-reading`: what a branch declares as its change — live `openspec/changes/*` discovery from the worktree's working tree, artifact listing, bounded single-artifact reads, absence named with the path where a declaration would be written.
- `code-diff`: the change's diff as data — parsed unified diff with per-file hunks and line kinds, untracked new files as all-added rows, the 200-presented-line cap with a visible cut marker naming the hidden count.

### Modified Capabilities

- `review-steps`: the requirement "Declared but unimplemented draws disabled" changes scope — `proposal` and `code` leave the unimplemented set (the known set now contains them; tests/evidence remain unimplemented until later cuts). The requirement's text itself is unchanged; the set it names shrinks. A MODIFIED delta records the two keys becoming implemented.

## Impact

- New backend files: `Change.cs`, `Patch.cs`; new test files: `ChangeTests.cs`, `PatchTests.cs`.
- Extended in place: `Git.cs` (one read), `ReviewSteps.cs` (two keys), `Program.cs` (two endpoints), `App.tsx` (panel branches), `package.json` (+react-markdown, +remark-gfm), `Directory.Packages.props` (nothing new — C# side needs no packages).
- Budget gate: this cut adds ≤ 250 cloc lines (epic allocation POC-02 ≤ 250; cumulative column 800 assumes a 400-line POC-00 — POC-00's recorded overrun carries forward; the per-cut budget is what this change holds).
- Marks stay POC-01's (read-only `reviewed:*` labels). No sqlite change. No GitHub writes. Not this ticket: writing marks, tests/evidence/app panels, agent launch, gate runner.
