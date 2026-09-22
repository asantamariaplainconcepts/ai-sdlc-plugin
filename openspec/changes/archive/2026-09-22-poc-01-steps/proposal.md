# poc-01-steps — review steps as declared data

## Why

Epic #1 (ticket POC-01, section `### POC-01 · Los pasos de review son datos, no código`) asks for the second cut of the PoC: the review steps stop being code and become a declared file — `.harness/review.json`, beside `commands.json` — with key, title, and one line saying what each step asserts. In the harness (fixed `837c7ca`) the five steps are hardcoded, unlike commands, workflows and lifecycle; this cut removes that difference for this PoC. The PoC boots declaring three: proposal, code, tests.

Progression stays marked against the provider, as today: the harness marks step completion via `reviewed:<step>` labels on the resolved issue, so the step mark reads `reviewed:<key>` presence in the issue's labels. This PoC writes nothing to GitHub — the tick is drawn only.

**Assumptions (POC-01 owns these decisions, unattended run):**

1. **review.json shape** — a top-level `"steps"` array of `{ key, title, asserts }` (mirrors `commands.json`'s `commands` shape: same vocabulary style, one addition — `asserts` is the one line saying what the step asserts, which the epic requires). The file is JSONC, byte-bounded before parse, never executed by its reader.
2. **Implemented-ness is a static known-set in code** (`ReviewSteps.Implemented`: proposal and tests keys are NOT implemented in this cut — actually none are until POC-02 lands the first panels). A declared step whose key is outside the known set draws disabled saying "not implemented", never disappears, never fakes empty.
3. **Marks come from the resolved issue's labels**, read through the same Octokit issue read POC-00 already does (`GitHub.Issue`) — extended additively with a labels array. No issue resolved → marks are absent and stated (each step shows "asked of the issue, none resolved"), not zero-ticked.
4. **Endpoint shape** — `GET /api/steps?path=...` returns `{ steps: [{key,title,asserts,implemented,marked}], problem, issue }`. A separate endpoint rather than growing the header payload: the steps are a different reading with a different cadence (they come from the same issue read, but the rail re-reads without dragging the eight facts along).
5. **Panel mounting** — App.tsx's rail placeholder region is extended in place (it was built as the placeholder for exactly this). Each implemented step's panel lands as its own component file mounted in one line (POC-02 onward). No visual step editor.

## What Changes

- **New** `.harness/review.json` — this repo declares its own three steps: proposal, code, tests.
- **New** `src/Plugin/ReviewSteps.cs` — JSONC reader (bounded, absent vs unreadable named with remedy path) + pure mark derivation (`reviewed:<key>` presence over a labels array) + the static known-implemented set.
- **Extended** `src/Plugin/GitHub.cs` — `IssueRead` carries `Labels` (additive; the issue read POC-00 already does).
- **Extended** `src/Plugin/Program.cs` — one new endpoint `GET /api/steps`.
- **Extended** `src/web/src/App.tsx` — the step-rail placeholder becomes a rail drawn from the declared steps: numbered boxes, tick from `reviewed:*` presence, disabled + "not implemented" for declared-but-unimplemented, absent/unreadable file named with its remedy.
- **New** capability `review-steps` — steps-as-data reading and its degradation contract.

## Capabilities

### New Capabilities

- `review-steps`: the review steps of a worktree, read from its declared `.harness/review.json` — key, title, one-line assertion per step; implementation status against a known set; marks from `reviewed:<step>` labels on the resolved issue; absence named with remedy.

### Modified Capabilities

None — `worktree-header` is untouched (its eight facts do not change). The GitHub issue read gains labels, but that is an implementation detail of the same read, not a requirement change.

## Impact

- New files: `.harness/review.json`, `src/Plugin/ReviewSteps.cs`, `src/Plugin.Tests/ReviewStepsTests.cs`, this repo's own review.json declaration.
- Extended in place: `src/Plugin/GitHub.cs` (one field + its fill), `src/Plugin/Program.cs` (one endpoint), `src/web/src/App.tsx` (the rail region).
- Budget gate: this cut adds ≤ 150 cloc lines over POC-00's 508 (epic allocation POC-01 ≤ 150; cumulative ≤ 550).
- No schema change (progression is marked against GitHub labels, not sqlite — per orchestration notes).
