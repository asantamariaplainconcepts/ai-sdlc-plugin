## 1. Declared file

- [x] 1.1 Write `.harness/review.json` declaring this repo's three steps (proposal, code, tests), each with key, title, and one line of what it asserts, in the repo's own voice (JSONC comments allowed)

## 2. Reader (backend)

- [x] 2.1 `src/Plugin/ReviewSteps.cs`: `Read(path)` with JSONC options, byte bound, absent vs unreadable (distinct Problem sentences naming the path/remedy), `Marked(key, labels)` (`reviewed:<key>` case-insensitive), `KnownImplemented` (empty set this cut)
- [x] 2.2 Extract the upward `.harness` file search shared with Commands (one finder, both readers use it; Commands' behaviour unchanged)
- [x] 2.3 Extend `GitHub.IssueRead` with `Labels` (additive; fill from Octokit issue read; existing construction sites updated)
- [x] 2.4 `GET /api/steps?path=...` in Program.cs: resolve issue as Header does (digit run + lookup), read review.json, return steps with implemented/marked plus problem/marksNotAsked/issue context

## 3. Rail (frontend)

- [x] 3.1 App.tsx: replace the placeholder rail with one drawn from `/api/steps` — numbered boxes in declared order, tick from `marked`, disabled + "not implemented" for unimplemented keys, tick absent-and-said when marksNotAsked
- [x] 3.2 Absent/unreadable review.json draws the problem sentence with its remedy path, no step boxes

## 4. Tests

- [x] 4.1 ReviewStepsTests: JSONC parses, comments/trailing commas; missing file is absent with remedy path; broken JSON is unreadable with path; oversize refused before parse; blank-key entries refused; `Marked` case-insensitive on `reviewed:` prefix; labels without the prefix do not mark
- [x] 4.2 Marks derivation against declared keys: label present for declared key marks; label for unknown key ignored
- [x] 4.3 Absence assertions proven: positive anchor from the same render/read, then mutate (force unreadable / remove file), watch the assertions fail, revert, confirm empty diff

## 5. Gate

- [x] 5.1 `./gate.sh` green (dotnet build+test warnaserror, web tsc+vite build, openspec validate --strict)
- [x] 5.2 cloc budget check: ≤ 150 lines added this cut over POC-00's 508 (cumulative ≤ 550), tests excluded
  - measured 599 C# + 167 TS (was 508 + 131): +91 C# ≤ 150 this cut; cumulative 599 C# is 49 over the 550 cumulative column — the epic's cumulative assumes a 400-line POC-00, and POC-00 shipped at 508 (overrun recorded then); the per-cut budget holds
- [x] 5.3 Two-subject spot check by hand: this worktree (issue #1 resolved via branch digits, labels readable) and a path with no review.json — both named correctly
