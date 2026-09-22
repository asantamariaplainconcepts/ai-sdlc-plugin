# Design — poc-02-panels

## Context

POC-01 (archived `2026-09-22-poc-01-steps`) established the rail that draws declared steps and a `KnownImplemented` set that is empty; every step draws disabled. This cut lands the first two panels. The shapes to extend are: `ReviewSteps.cs` (reader + known set), `Git.cs` (porcelain reads, `DiffBasis`), `Program.cs`'s endpoint style (`/api/steps` DTO with problem-style absences), and App.tsx's panel region (built as a one-branch-per-step placeholder). The harness reference at `837c7ca` fixes the panel semantics: `ProposalStep.tsx` (artifact list + reader, every absence a sentence, several changes drawn the same) and `CodeStep.tsx`/`PatchView.tsx` (hunk list with tints, truncated-patch note). Budget: ≤ 250 cloc lines added this cut.

## Goals / Non-Goals

**Goals:**

- Proposal panel: live `openspec/changes/*` discovery from the worktree, artifact listing, markdown rendering of one artifact at a time (react-markdown + remark-gfm).
- Code panel: parsed unified diff as data, per-file grouping, hand-drawn rendering with added/removed separated, 200-line cap with visible cut marker naming the hidden count.
- Both step keys join `KnownImplemented`; the rail stops drawing them disabled.
- Absence discipline everywhere: no declared change → say so + name where it would be written; no diff basis → say so; binary → say so.

**Non-Goals:**

- Marks writing, tests/evidence/app step panels (later cuts), agent launch, gate runner.
- Diff annotations/claims (`tasks.md` labels — harness has them; epic scope says diff-only).
- Editing proposal files or commenting on the diff ("no es: edición, ni comentarios sobre el diff").

## Decisions

1. **Shapes (later tickets extend these — recorded as the contract):**
   - `src/Plugin/Change.cs`: static class, pure + IO-separated. `Discover(root)` → `ChangeRead { Root, Changes: [{ Name, Path, Artifacts: [{Name, Path}] }], Problem? }` — walks the worktree's `openspec/changes/` (skypsy `archive`), one level of `*.md` per change. `ReadArtifact(path)` → `{ Path, Text, Problem? }`, byte-bounded before read (same MaxBytes discipline). No markdown knowledge server-side — the pipeline ships raw and renders client-side.
   - `src/Plugin/Patch.cs`: static pure class. `Parse(string patchText)` → `PatchRead { Files: [{ Path, RenamedFrom?, IsBinary, Hunks: [{ Header, Lines: [{ Kind, OldLine?, NewLine?, Text }] }] }], Problem? }`; `Cap(files, 200)` → same shape + `CutAfter` (the global line index where drawing stopped) + `HiddenLinesCount`. Kind enum `added|removed|context`.
   - `Git.cs` + one method: `Patch(cwd, basis, untracked)` — `git -c core.quotepath=off diff --unified=3 --no-color <basis>` text; for each untracked file a synthesized `--- /dev/null` / `+++ b/<path>` all-added block (reads each untracked file's text, bounded). Untracked already ride numstat in POC-00 for the same reason: git diff does not see never-staged files.
   - `ReviewSteps.KnownImplemented` = `{ "proposal", "code" }`.
   - `GET /api/proposal?path=` → `{ path, problem?, changes: [{ name, path, artifacts: [{ name, path }] }] }`; `GET /api/artifact?path=&file=` → `{ text, problem? }` (separate so the list renders without dragging markdown bodies). `GET /api/code?path=` → `{ path, problem?, basis, files (patch-shaped), cutAfter?, hiddenLinesCount? }`.
   - `src/web/src/steps/ProposalStep.tsx` — fetches proposal, lists changes+artifacts (chosen fallback-first, harness's selection-follows-the-list), fetches artifact text, renders via `ReactMarkdown` + `remarkGfm`. Absence sentence drawn as a Note, not a blank.
   - `src/web/src/steps/CodeStep.tsx` — fetches code, one file at a time (list left, patch right — simplified harness layout, no narrow-container breakpoint this cut), draws hunks: header bar, lines with old/new gutters and `+`/`−` marker column, tinted backgrounds (color-mix on existing CSS vars). Cut marker: a row at the cut point naming hidden lines.
   - App.tsx panel region: `step === "proposal"` → `<ProposalStep path={path} />`, `step === "code"` → `<CodeStep path={path} />` — one line each, the POC-00 convention.
2. **Discovery semantics** mirror the harness `ProposalStep` reading: `openspec/changes` under the *worktree*, live (working tree, not `git show`) — the epic says "the harness reads the working tree's openspec dir". Zero live changes → `{ problem: null, changes: [] }` + frontend sentence "no change declared here — it would be written at `<root>/openspec/changes/<name>/proposal.md`" (absence named with remedy, both from the same payload: root travels in the response). Several changes → all drawn, note naming the count, neither marked as *the* one.
3. **Markdown client-side.** Shipping the artifact list separately from artifact text keeps the payload small and the render streamed-by-choice; react-markdown renders untrusted-markdown the way an editor renders untrusted-text (the file is repo content, the same trust level as `git show`). Props pinned exactly: `react-markdown@^10`, `remark-gfm@^4` (latest majors as of this cut; lockfile is the pin).
4. **Code endpoint does not need the issue read.** Marks and rail stay `/api/steps`; `/api/code` is a pure git read (basis + patch + untracked) — no GitHub round-trip, so the panel opens fast and offline. `problem` covers: not a repository, no trunk to diff against (named, with the fetch remedy — same words as the header's base fact).
5. **Cap counts diff body lines globally** (added+removed+context across all files, in order). At 200 the parse stops and the DTO names `hiddenLinesCount` — "a diff over 200 lines is cut with a visible marker, not dumped". The marker draws at the file boundary where counting stopped, naming the count. Alternative rejected: per-file caps (a 199-line single file would read as complete while hiding 400 more).
6. **Testing:** `PatchTests` over fixture patch text (hunk header split, `\ No newline at end of file` tolerance, binary `Bin` detection, rename headers, cap at exactly 200 + hidden count, empty patch). `ChangeTests` with temp directories (discover: present/multiple/nested-archive-skipped; absent named with remedy path; oversize artifact refused). Absence assertions carry positive anchors per repo policy; mutation-proof for at least: unreadable artifact (force a directory where the file reader walks), cap hidden-count (drop cap to a number below the fixture, assert the marker appears).

## Risks / Trade-offs

- [react-markdown bundle weight] → accepted: epic pins it for this exact step; harness keeps it behind Prose boundary, we accept the single-page cost (one screen).
- [hand-rolled patch parser vs library] → required by the epic ("Sin dependencia de librería de diff"). Risk contained: edge cases encoded as fixtures; `-z` numstat already has its own parser in POC-00 — the same tolerance for unusual paths (`core.quotepath=off` + tab-separated headers only).
- [budget 250] → Change.cs ~60, Patch.cs ~90, Git addition ~20, endpoints ~35 CS; two step files ~180 TS total; package.json additions. Measured with cloc at the end; if over, first cut is CodeStep's file-list trimming (list becomes a `<select>`), recorded here if taken.
- [untracked file text can be huge] → bounded read (MaxBytes per file, same constant family as other declared-file reads; a refused read draws "over the bound", not a truncated body).

## Migration Plan

New files + additive edits; rollback is `git checkout` of the parent commit. `KnownImplemented` growing is the only behavioural change to shipping code (rail entries enable).

## Open Questions

None blocking. Specs-delta rendering (proposal reading `specs/**`) deferred — recorded in proposal assumptions as debt for a later cut.
