## 1. The declaration gains its fourth step

- [ ] 1.1 `.harness/review.json` declares `evidence` (key/title/asserts) after `tests`; no prompts entry (Evidence launches no agent)
- [ ] 1.2 `ReviewSteps.KnownImplemented` gains `"evidence"`; `ReviewStepsTests`: a file declaring `evidence` parses it and the set opens it

## 2. The panel

- [ ] 2.1 `src/web/src/steps/EvidenceStep.tsx` — GET `/api/runs?path=`, most recent first rendered read-only: session id short, step, exit code AND provider error (two facts), cost absent stated ("not given"), transcript path with located/absent sentence, started/finished, trigger; no launch/mutate control anywhere in the panel
- [ ] 2.2 No runs: a sentence naming where a run is launched from; failed fetch: the failure named; refused rows render their problem sentence
- [ ] 2.3 `App.tsx` mounts `EvidenceStep` in the existing one-line ternary chain; the rail draws the fourth step enabled

## 3. The measurement (timed)

- [ ] 3.1 Evidence-step implementation timing recorded in this change's design.md (start/finish epoch, wall-clock in minutes) — the headline number
- [ ] 3.2 cloc per cut POC-00..POC-05 at each archived change's boundary SHA plus this cut, with the exact command per number, backend and frontend columns, per-cut delta and cumulative against the epic's budget table

## 4. The comparison and the verdict

- [ ] 4.1 Eight header facts compared fact-by-fact against harness 837c7ca (facts.ts, seam.ts, task.ts, MergeGate.tsx): matches / matches-with-difference (named) / missing, each with file:line on both sides
- [ ] 4.2 `docs/verdict.md` written: the cloc table, the comparison, the timing, and the verdict against the epic's death criteria with its number and what happens in each result branch (alive → thin + order: fifth step, substrate ACP-before-herdr, multi-writer deferral in writing; dead → ACP-07 + multi-writer deferral as two issues there)

## 5. Verification

- [ ] 5.1 `gate.sh` green: dotnet build+test (warnaserror), web build, openspec validate --strict
- [ ] 5.2 cloc this cut: added code within the ≤150 "host/estático" allocation; cumulative recorded in the verdict
- [ ] 5.3 Absence assertions proven per contract: force the no-runs and failed-fetch branches on, re-run, require the assertions to fail, revert, empty diff, green again
