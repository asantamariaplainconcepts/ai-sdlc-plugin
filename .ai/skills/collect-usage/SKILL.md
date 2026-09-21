---
name: collect-usage
description: Summarize a change's pushed telemetry (cost, tokens, duration, and any friction notes) from .telemetry/executions.jsonl. Use when reporting time invested for a change, e.g. from /aio:sync or /aio:review.
---

Produce one per-change usage summary from pushed telemetry — one responsibility, read-only. The
durable source is `.telemetry/executions.jsonl`, one line per `push-session-report` push — a live
dashboard is never the source, because this file is what outlives the terminal that wrote it.

## What a line carries (reference)

Each line: `change`, `phase`, `session_id`, `status` (`ok`/`degraded`/`failed`), `note` (set only on
`degraded`/`failed`), and `usage` — either `{ tokens: { input_tokens, output_tokens,
cache_creation_tokens, cache_read_tokens }, duration_ms, cost_usd_estimated }` read from that push's
session transcript, or `{ unavailable: true, reason }` when no transcript could be found (a
different runtime, or a session that predates this capability).

**A line's figures are its whole session so far, not that phase alone.** The script sums the
transcript from its first message every time, so two pushes from one session overlap — the later one
contains the earlier one. `session_id` is what makes them addable: one figure per session, summed
across sessions. This is not a rare shape. `/aio:implement` pushes twice on its own (at the gate,
then again when it marks the PR ready), and `/aio:ship` runs several phases in one session.

`duration_ms` spans the whole transcript the push found (first to last message), not agent
processing time alone — this mechanism cannot separate human reading/typing time from agent time
the way the OTel `active_time.total{type}` split this skill's first draft assumed could. Report a
session's wall-clock span, not a human/agent breakdown, and say so if asked for one.

## Steps

1. **Read `.telemetry/executions.jsonl`** and filter to lines whose `change` equals the change name.
   - Done when: every line pushed for this change is in hand (possibly none).
2. **Aggregate one figure per session, then sum those.** Group the lines carrying real usage by
   `session_id` and keep only the largest per group — its figures already include every earlier push
   from that session. Then sum `tokens` by type, `cost_usd_estimated` and `duration_ms` across the
   groups. Adding every line instead reports a change at roughly twice its cost, and the error grows
   with each extra push. Lines whose `usage.unavailable` is true are skipped but counted as phases
   with no figure — never dropped silently. A line with a null `session_id` (a runtime that exposes
   none) cannot be grouped, so count it on its own.
   - Done when: total tokens, total estimated cost, and total duration are computed from one figure
     per session, plus a count of phases whose usage was unavailable.
3. **Collect friction.** Separately, list every line with `status` `degraded` or `failed`, each with
   its `phase` and `note` verbatim.
   - Done when: the friction list for this change is in hand (possibly empty).
4. **Emit.** Return a compact summary: estimated cost, tokens, duration, how many phases had no
   figure, and the friction list — ready for `retro-entry` (the cost/time half) and its "what didn't
   work" candidates (the friction half, per its own step 1).

## When there is no telemetry

**Say so explicitly, and name it as a gap, not a footnote.** A change with zero lines in
`.telemetry/executions.jsonl` means either every phase's push step was skipped, or the change
predates this capability. Report which, if it can be told apart (a change whose `tasks.md` names
`push-session-report` steps but produced no lines is the first case; a change opened before this
capability existed is the second).

This step exists because the old wording — "if telemetry is missing, the entry says so (manual)" —
was a documented shrug, and every one of 88 prior retro entries used some version of it. Each retro
looked complete; the programme quietly lost every measurement it was built to collect. Nothing
recovers telemetry that was never pushed, so the cost of one more silent "manual" is permanent.
   - Done when: the summary — including the explicit "no telemetry" statement where that's the
     case — is handed back to the caller (`/aio:sync` or `/aio:review`).

## Do not

- Read from a live dashboard as the source of truth — `.telemetry/executions.jsonl` is what
  survives the terminal closing.
- Fabricate a figure, a human/agent split this mechanism does not produce, or a friction note that
  was not actually pushed.
- Include per-person identity in the summary beyond what the retro needs.
