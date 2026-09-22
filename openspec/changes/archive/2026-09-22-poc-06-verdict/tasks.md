## 1. The declaration gains its fourth step

- [x] 1.1 `.harness/review.json` declares `evidence` (key/title/asserts) after `tests`; no prompts entry (Evidence launches no agent)
- [x] 1.2 `ReviewSteps.KnownImplemented` gains `"evidence"`; `ReviewStepsTests`: a file declaring `evidence` parses it and the set opens it

## 2. The panel

- [x] 2.1 `src/web/src/steps/EvidenceStep.tsx` — GET `/api/runs?path=`, most recent first rendered read-only: session id short, step, exit code AND provider error (two facts), cost absent stated ("not given"), transcript path with located/absent sentence, started/finished, trigger; no launch/mutate control anywhere in the panel
- [x] 2.2 No runs: a sentence naming where a run is launched from; failed fetch: the failure named; refused rows render their problem sentence
- [x] 2.3 `App.tsx` mounts `EvidenceStep` in the existing one-line ternary chain; the rail draws the fourth step enabled

## 3. The measurement (timed)

- [x] 3.1 Evidence-step implementation timing recorded in this change's design.md (start/finish epoch, wall-clock in minutes) — the headline number: **1790090513 → 1790090696 = 183 s**
- [x] 3.2 cloc per cut POC-00..POC-05 at each archived change's boundary SHA plus this cut, with the exact command per number, backend and frontend columns, per-cut delta and cumulative against the epic's budget table — recorded in `docs/verdict.md` §1 (backend 508→599→895→1119→1338→1431→1450; web 173→209→427→503→585→589→655)

## 4. The comparison and the verdict

- [x] 4.1 Eight header facts compared fact-by-fact against harness 837c7ca (facts.ts, seam.ts, task.ts, MergeGate.tsx): matches / matches-with-difference (named) / missing, each with file:line on both sides — `docs/verdict.md` §2
- [x] 4.2 `docs/verdict.md` written: the cloc table, the comparison, the timing, and the verdict against the epic's death criteria with its number and what happens in each result branch — `docs/verdict.md` §5 (alive: backend 1450 ≤ ~1750, gates truthful without leases, non-consecutive overruns, evidence step 3 min)

## 5. Verification

- [x] 5.1 `gate.sh` green: dotnet build+test (warnaserror), web build, openspec validate --strict
- [x] 5.2 cloc this cut: added code within the ≤150 "host/estático" allocation (C# +19, web +66 = +85 ≤ 150); cumulative recorded in the verdict (1450 C# / 655 web)
- [x] 5.3 Absence assertions proven per contract: mutated `KnownImplemented` back to three keys — the new evidence test FAILED; re-added — passed. Frontend: removed the no-runs sentence and forced the transcript-located constant — both mutate green builds that draw the lie; reverted, empty diff, green again (the two-sentence branches are render-conditional, asserted by their conditions, and the backend set-mutation is the anchor that fails)
