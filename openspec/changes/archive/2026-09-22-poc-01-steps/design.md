# Design — poc-01-steps

## Context

POC-00 (archived `2026-09-22-poc-00-header`) established the skeleton this cut extends: one .NET 10 minimal API project (`src/Plugin`, one file per concern), a React 19.3 screen (`src/web/src/App.tsx`) whose step rail is a placeholder built for exactly this cut, the GitHub issue read (`GitHub.cs`) that resolves a branch's digit run to an issue, and the commands.json reader (`Commands.cs`) whose JSONC-bounding discipline this cut mirrors. The harness reference at `837c7ca` fixes the semantics of `reviewed:*` labels (`ChangeTab.tsx`: `stepMarksFromLabels`, `REVIEWED_PREFIX = "reviewed:"`). Budget: ≤ 150 cloc lines added this cut (cumulative ≤ 550).

## Goals / Non-Goals

**Goals:**

- Review steps as declared data: `.harness/review.json` beside `commands.json`, JSONC, byte-bounded, never executed by its reader.
- The rail in App.tsx draws from the declaration: numbered boxes, tick from `reviewed:<key>` presence on the resolved issue, disabled + "not implemented" for declared-but-unimplemented keys.
- Absent vs unreadable named with remedy path, mirroring POC-00's discipline.
- Marks read-only against the provider (labels carried on the existing issue read).

**Non-Goals:**

- Panel content — proposal and code panels are POC-02; this cut's implemented set is EMPTY until they land (the declared steps still draw; the distinction is per-key against the known set).
- Writing labels (no GitHub writes, same as POC-00).
- A visual step editor (epic: "no es un editor visual de pasos").
- Any sqlite schema change — progression is GitHub labels.

## Decisions

1. **Shapes (later tickets extend these — recorded as the contract):**
   - `.harness/review.json` schema: `{ "steps": [ { "key": string, "title": string, "asserts": string } ] }` — mirrors `commands.json`'s shape (`commands` array, flat entries). `asserts` is the one line saying what the step asserts (epic requires it). Ordered; duplicates of a key keep first declaration. Reaction to the file's absence/unreadability travels in the same Read-shaped structure Commands uses (Absent / Unreadable / Problem) — one vocabulary for both declared files.
   - `src/Plugin/ReviewSteps.cs`: static class like Commands. `Read(path)` → `ReadResult` (Steps, Absent, Unreadable, Problem, same field names Commands established); `KnownImplemented` (static `IReadOnlySet<string>`, EMPTY this cut — POC-02 adds `proposal`, `code`; tests adds value later); `Marked(key, labels)` → bool (`reviewed:<key>` present, case-insensitive); `MarksNotAskedReason(...)` — the sentence the rail draws when no issue is resolved. All pure — testable without network.
   - `GitHub.cs` `IssueRead` gains `IReadOnlyList<string> Labels` — additive (the record gains one positional field; both construction sites updated; the seamed test sources pass empty).
   - `GET /api/steps?path=...` payload: `{ path, steps: [{ key, title, asserts, implemented, marked }], problem, marksNotAsked, issueKey, issueTitle }`. `problem` null when the file read fine; `marksNotAsked` non-null names why marks cannot be read (no issue resolved / lookup refused / not a repository). The rail draws itself entirely from this payload.
   - Panel mounting: unchanged from POC-00's contract — one step panel = one component file, mounted in one line of App.tsx. This cut mounts none; selecting an implemented step is not yet possible because the set is empty. The rail keeps the selection state (a step id) so POC-02 only adds the panel branch.
2. **Endpoint separate from /api/header.** The header answers eight facts with its own cadence; steps are a different reading that shares only the git/issue inputs. Growing the header payload would re-run the porcelain reads the rail does not need. Cost: one endpoint (~25 lines including issue resolution shared with Header via a small shared method).
3. **Implemented-ness is code, not data.** A declared file that could also declare "implemented" would let a file claim a panel that does not exist — the epic's disabled-with-words step exists precisely because the file cannot know what code ships. The known set lives in `ReviewSteps.KnownImplemented`; a declared step outside it draws disabled. Alternative rejected: inferring implemented-ness from the presence of a component — frontend-only inference that the API could not vouch for.
4. **Marks semantics (harness parity, read-only):** harness `stepMarksFromLabels` filters `reviewed:`-prefixed labels to its known steps; ours match against the declared keys — the same rule over declared data (the file is the source of what draws). Case-insensitive label compare (vendor compares that way). No issue resolved → `marksNotAsked` sentence names the reason and remedy, same words the header's issue fact uses. We do NOT write labels — harness's tick button is out (nothing writes to GitHub in this PoC).
5. **File discovery** shares Commands' upward search, extracted to one place: a declared-file finder used by both readers (nearest `.harness/<name>` from the worktree up, falling back to `<cwd>/.harness/<name>` so the remedy path is stable). Alternative rejected: duplicating the loop in ReviewSteps — two copies of a walk that POC-02..05 will keep needing.

## Risks / Trade-offs

- [Label casing/spelling drift] → case-insensitive compare + `reviewed:` prefix strictness; marks only ever read presence, never inferred.
- [Two declared files, two vocabularies] → same Read discipline (bounded, JSONC, absent-vs-unreadable) enforced by tests mirroring CommandsTests, one per case, with positive anchors.
- [issue read cost doubles (header + steps endpoints)] → one request each on manual read actions only; no polling in this cut (POC-05 owns cadence). Acceptable at PoC scale.
- [Budget 150] → ReviewSteps.cs expected ~70 lines CS, App.tsx rail region ~40 TS lines, endpoint ~25. Measured with cloc at the end; the known set being empty this cut keeps the rail rendering minimal.

## Migration Plan

New file + additive edits only; rollback is checkout of the parent commit. The `GitHub.IssueRead` positional-field change is source-compatible within this repo (three construction sites, all updated in the same change).

## Open Questions

None for this cut. The implemented set being empty (panels arrive with POC-02) is recorded in the Why, not left implicit.
