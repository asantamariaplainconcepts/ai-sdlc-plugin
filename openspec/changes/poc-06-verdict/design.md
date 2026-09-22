# Design — poc-06-verdict

## Context

POC-00..05 are archived; the PoC has a header (eight facts), three wizard panels, an agent launcher recording runs against starting commits, gates tied to resulting commits, and two triggers through one contract. This cut closes the epic: measure, compare, decide. The reference harness is read-only at `/var/folders/mq/.../harness-837c7ca`; the cloc script is `perl /var/folders/mq/.../opencode/cloc` (v2.11).

Constraint inherited from every prior cut: declared files are untrusted input; the vocabulary is closed; nothing writes to GitHub; backend budget counted with cloc (tests excluded from the count, required by the gate).

## Timing protocol (the headline measurement)

`date +%s` captured at the start of the Evidence-step work (implementation only — spec drafting, cloc table, comparison and verdict are this cut's other deliverables, not the fourth step's cost): **1790092863 → recorded below when finished**. The epic's question is "is an afternoon or not"; the honest number is wall-clock from first code-touching commit to gate-green, including the thinking in between, excluding the verdict writing. If the step turns ugly past two hours, STOP and report the overrun — the timing IS the finding.

- START (epoch): 1790092863 (2026-09-22 17:34 CEST) — captured before opening EvidenceStep.tsx
- FINISH (epoch): recorded in tasks.md when the gate is green
- Wall-clock: the difference, stated in minutes and in "afternoon or not" terms

## Decisions

1. **Evidence = recorded runs, read-only.** The panel fetches `GET /api/runs?path=` (POC-03's listing, most recent first) and renders each row: session id short (8), step, exit code AND provider error (two facts, as TestsStep established), cost (absent = "not given — the provider said nothing about it", never $0.00), transcript path with located/absent sentence, started/finished timestamps, and trigger. No button, no POST, no mutation — evidence is what the record holds. The no-runs case is a sentence ("no run recorded yet — launch one from the step that runs"), not a blank panel; the failed-fetch case names the failure.
2. **The declaration gains its fourth entry.** `.harness/review.json` adds `{"key": "evidence", "title": "Evidence", "asserts": "the runs recorded against this worktree say what happened, and their exits and costs are on the table"}`; `KnownImplemented` gains `"evidence"`; `App.tsx` mounts `EvidenceStep` in the same one-line ternary chain. The prompts map gains nothing — Evidence launches no agent; a future prompt for it would ride the existing contract unchanged.
3. **Trigger rendered here, deliberately.** TestsStep hides it (POC-05's acceptance: the run-launching view is indistinguishable between triggers). EvidenceStep shows it: one more recorded fact among recorded facts, read after the work, not branched on by any code path. The distinction — provenance is reviewable, launching is not — is recorded in the proposal's assumptions.
4. **cloc and comparison are readings, not code.** The verdict doc cites the exact command per number (`perl …/cloc <tree>` at the archive SHA's worktree) and file:line on both sides for each of the eight facts. No new backend code serves the comparison; the table is written by reading both trees.
5. **docs/verdict.md is the deliverable, the change is its wrapper.** The doc holds: the per-cut and cumulative table; the eight-fact semantic comparison; the Evidence-step timing; the verdict with its number and both result branches (alive → permission to thin + order: fifth step pattern, substrate ACP-before-herdr, multi-writer block deferred in writing; dead → ACP-07 + multi-writer deferral as two issues in the harness epic, this archives with its measurement).

## Risks / Trade-offs

- **The Evidence step is cheaper than the harness's.** The harness's panel reads attachments out of issue bodies (381 lines + evidence.ts parser); this one reads the local runs table (~90 lines). That is the honest comparison to state: this PoC's evidence surface is thinner than the reference's, and the verdict says so rather than claiming parity.
- **Frontend budget pressure.** 589 + ~95 web lines ≈ 684 against the epic's ≤1.200 TSX ceiling — inside, with no room for complacency; recorded either way.
- **Timing measurement is person-dependent.** Wall-clock of one implementer is not a repeatable benchmark; it is the epic's own chosen instrument ("esa tarde se cronometra"), so it is recorded as measured, methodology stated.
