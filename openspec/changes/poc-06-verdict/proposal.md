# poc-06-verdict — the measured result of the PoC

## Why

Epic #1 (ticket POC-06, section `### POC-06 · El veredicto`) asks for the closing cut: the PoC's hypothesis ("a full agentic cycle — launch, verify, two triggers — fits in ~1.750 lines of backend without the scaffolding") has been built across POC-00..POC-05 and now must be **measured and decided, without rounding in its own favour**. Four deliverables: the cloc table measured with the epic's definition on the archived cuts' boundary SHAs; the eight header facts compared fact-by-fact against the harness reference at 837c7ca; the fourth wizard step (Evidence) implemented **timed**, because "an afternoon or not" is itself a headline number of the hypothesis; and the verdict written in `docs/verdict.md` — each death criterion answered with its number, each life branch stating what happens next.

**Ticket link:** https://github.com/asantamariaplainconcepts/ai-sdlc-plugin/issues/1 (section POC-06). Umbrella story F-POC-1, use case UC-POC-1 (verify finished work), business rule BR-VERIFY-1 (null is not zero — also this change's measurement discipline: an unmeasured claim is not a measurement).

**Assumptions (POC-06 owns these decisions, unattended run — recorded, each reversible):**

1. **Evidence step reads recorded runs, not issue attachments.** The harness's EvidencePanel (837c7ca) reads issue-body attachments; the ticket fixes this PoC's Evidence step as the runs recorded against this worktree (GET /api/runs, POC-03) with per-run session id, exit code, cost, transcript path, started/finished and trigger — read-only presentation. The ticket text is the authority here ("read-only presentation of recorded evidence"); the divergence from the harness's attachment-based step is named in the comparison and the verdict.
2. **Trigger shown in Evidence.** POC-05's Tests step deliberately does not render the trigger ("the view does not branch on it" — indistinguishable runs was that cut's acceptance). The Evidence step is the review surface that *may* look: presenting recorded evidence is exactly where provenance belongs, and no launch code branches on the value. Reversible: dropping the column from the panel changes no contract.
3. **cloc method.** `perl …/cloc` (the orchestration-provided script, v2.11), per cut, on the boundary SHA of each archived change (the archive commit), counting `src/Plugin` (C#, code) for backend and `src/web/src` (TS+TSX+CSS code) for frontend. Tests live in `src/Plugin.Tests` — outside both counted trees, excluded by construction (the epic's definition). The epic's backend budget table is read against the C# count; the frontend ≤1.200 TSX budget is read against the web count. Every number is reproducible: the command is recorded in the verdict doc.
4. **Versioning the timing.** `date +%s` before and after the Evidence-step work (spec + code + tests + gate green), recorded in this change's design.md and in docs/verdict.md. The clock covers the implementation itself, not the cloc/comparison/verdict writing — those are POC-06's other deliverables, not the fourth step's cost.

## What Changes

- **New** `docs/verdict.md` — the deliverable: cloc table per cut with commands, the eight-fact comparison with file:line on both sides, and the written verdict against the epic's death criteria with the number and the branch consequences.
- **Extended** `.harness/review.json` — the fourth step declared: `evidence` (key/title/asserts).
- **Extended** `src/Plugin/ReviewSteps.cs` — `KnownImplemented` gains `evidence`.
- **New** `src/web/src/steps/EvidenceStep.tsx` — the runs recorded against the worktree (GET /api/runs), read-only: per-run session id short, exit code, cost, transcript path, started/finished, trigger; absent cost stated, refused rows named. Mounted in one line of `App.tsx`.
- **New tests** — `ReviewStepsTests`: declared `evidence` step parses and opens. Evidence-step rendering is covered by the existing step-forwarding contract in `App.tsx` plus the panel's own absence sentences.

## Capabilities

### New Capabilities

- `evidence-step`: the fourth review step — recorded runs presented read-only as evidence, each absence named (no runs, no cost given, transcript absent).

### Modified Capabilities

- `review-steps`: THIS REPO'S OWN `.harness/review.json` gains its fourth declared step (`evidence`) — a data change the capability's "changing the file changes the wizard" requirement already describes; no requirement text changes.

## Impact

- New doc `docs/verdict.md`; one line each in `.harness/review.json`, `ReviewSteps.cs`, `App.tsx`; new `EvidenceStep.tsx`; one test addition.
- Budget gate: this cut's code (Evidence step + wiring) stays within the epic's "host, arranque y estático" ≤150 allocation — measured with cloc and recorded in the verdict (baseline POC-05: 1.431 C# + 589 web).
- No GitHub writes, no issue labels, no porting — "no es empezar a portar nada".
