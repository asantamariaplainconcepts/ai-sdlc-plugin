## 1. F1 — /api/artifact confined (blocker, test-first)

- [x] 1.1 `Change.ReadListedArtifact(worktree, file)`: `file` relative to the worktree; absolute, `..` climb, or outside-`openspec/changes` ⇒ refused with a named sentence (Path null → the endpoint's 400); inside a listed change root ⇒ the existing bounded `ReadArtifact` of the resolved path
- [x] 1.2 `Program.cs` `/api/artifact`: requires `?path=` + `?file=`; refusal (Path null + problem) → 400-style problem response naming the refusal; listed read → the same Ok body as before
- [x] 1.3 `ChangeTests`: escape attempt (`../../..`), absolute path, and outside-changes relative path all refused; a listed artifact still reads (positive anchor)
- [x] 1.4 `ProposalStep.tsx` fetches with `path` + the artifact's relative path (the listing already carries both) — the panel change is the fetch line only

## 2. F2 — refused runs persisted (major)

- [x] 2.1 `Runs.LaunchAndRecord`: the refused branches record their row through `store.RecordRun` (both refusal grounds: unknown trigger, no declared prompt); refused row keeps NO trigger and no session facts
- [x] 2.2 `WatcherTests`: `An_unknown_trigger_is_refused_and_records_no_trigger` now asserts store state through `LaunchAndRecord` (row exists, trigger null, problem the refusal sentence) instead of the fabricated-rows shape; the vacuous `The_refused_poll_and_button_rows_share_one_shape` rewrite exercises `LaunchAndRecord` on both triggers in a rowless store and asserts both rows listed
- [x] 2.3 Decision recorded in the commit message and here: persist refused rows (they answer "why did nothing happen"; the watcher's newest-row predicate reads them — an unpersisted refusal made the poller loop silently), keep no-trigger-on-refusal, fix the test to exercise the contract. Assumption for a human to ratify.

## 3. F3 — verdict budget row corrected (major)

- [x] 3.1 `docs/verdict.md` table: POC-06 row 1450 → 1431, Δ +19 → +0 (obj/ rows, not code), Total 1450 → 1431; the under-margin 300 → 319 (and the same number in §5)
- [x] 3.2 The claim that obj/ rows are absent in a clean checkout corrected to the measurement rule: exclude `bin/`/`obj/` explicitly — one honest sentence, verdict direction unaffected (under by more, not less)

## 4. F4 — gates orchestration survivor (major)

- [x] 4.1 `GatesTests`: one test of `RunDeclaredGates` over a temp repo declaring `definitely-not-a-real-binary` — asserts `Missing=true`, the not-findable sentence, and no store rows; one of `ReadDeclaredGates` over the same declaration — same refusal, no rows
- [x] 4.2 No seam extraction needed: the declared-file read is local (`FindDeclared` from the repo dir), the store is a temp `Store`, the missing executable never launches — the orchestration is testable as it stands

## 5. F5 — absence sentence served, not rebuilt (minor)

- [x] 5.1 `/api/proposal` response gains `absence` (the `Discovery.Absence` sentence, additive field)
- [x] 5.2 `ProposalStep.tsx` renders `proposal.absence` when the API served one; the root-concatenation rebuild is gone. If the panel change exceeded trivial: backend-only would be acceptable — record; (it did not)

## 6. F6 — stale prose, dead schema (minor)

- [x] 6.1 `App.tsx` placeholder: "no step selected" sentence replacing the stale POC-01 promise
- [x] 6.2 `ReviewSteps.KnownImplemented` comment: lists the four keys this build opens, not POC-02's two
- [x] 6.3 `Store.cs`: delete the dead `fact_cache` CREATE TABLE (unwritten schema; old DBs keep their table, new ones never create it); fix the "Two tables now" comment to the four that exist

## 7. Verification

- [x] 7.1 Per-commit: build + tests green before the next finding's edit
- [x] 7.2 After all fixes: `npm ci` (done once before starting), `bash gate.sh` GREEN, `openspec validate --all --strict --no-interactive` green, `git status` clean
- [x] 7.3 Mutation discipline: for each NEW absence/refusal assertion (F1 escapes, F2 refusal rows, F4 missing-gate wiring) — break once, watch fail, revert, confirm clean diff
- [x] 7.4 cloc delta of the pass recorded (guardrail ~+100): backend 1431 → 1478 hand-written C# (+47 — F1's confined read is the bulk; F6 removed 5), frontend 655 → 654 (−1: F5's render swap replaced rebuild, F6 shortened the placeholder). Tests excluded from the count, required green (141 → 152).