---
name: push-session-report
description: Push one deterministic record of a command's phase — its cost and, when something was wrong, a note about it — to .telemetry/executions.jsonl. Use from an /aio:* command at a phase boundary (propose opened, implement gate passed/failed, hold applied, sync closed), and immediately whenever a step hits a missing prerequisite, a wrong instruction, or a workaround.
---

Push one structured record — one responsibility, write-only, no interpretation. Two independent
halves land in the same line: what the phase cost, and whether anything about the command itself
was wrong while running it.

## Why a skill instead of a hook

A hook only fires in Claude Code. This repo's commands are one prompt symlinked into three
runtimes (`.claude/commands/`, `.opencode/command/`, `.github/prompts/`), so a step written into
the command itself runs under any of them — the same reasoning that already keeps `/aio:*` wrapping
OpenSpec explicitly instead of expecting a plugin. The determinism is in the script the step names,
not in an event only one runtime can fire.

## Steps

1. **Run the fixed script**, passing what the calling command already knows:
   ```bash
   node .ai/skills/push-session-report/scripts/push-session-report.mjs \
     --change "<name>" --phase "<phase>" [--status ok|degraded|failed] [--note "<one line>"]
   ```
   `--status` defaults to `ok`. Passing `--note` without `--status degraded` or `--status failed` is
   refused — a finding is never buried inside a record that reads as fine.
2. **The script derives what it can mechanically** — token counts and elapsed time, summed from the
   current session's own transcript file, plus a cost estimate computed from those tokens against a
   small per-model pricing table it carries. Where no transcript can be found (a different runtime,
   or one that predates this capability), it records that plainly instead of a zero.

   The sum always starts at the transcript's first message, so a second push from the same session
   restates the first one's tokens rather than the slice since. That is why each line also carries
   `session_id`: `collect-usage` takes one figure per session instead of adding the overlap twice.
   Push as often as the command says to — the reader, not the pusher, is where that is resolved.
3. **Push the moment something is wrong, not only at the phase's normal end.** The instant a step's
   own instructions turn out to be missing a prerequisite, wrong about what's actually there, or
   force a workaround to continue, push a `--status degraded` (adapted and continued) or
   `--status failed` (could not continue as written) record with a one-line `--note` — the fact,
   not a mood. This is the same instruction Expo's own skill docs give an agent under "Submitting
   Feedback": report it so the thing you're following can be improved, rather than working around it
   silently or fixing it and saying nothing.
4. **Report what the script wrote** (or that it found no transcript) back to the caller in one
   sentence — not a summary.

## Do not

- Compute or estimate the token/duration numbers yourself — the script reads them; if it says a
  figure is missing, say that.
- Write prose into `.telemetry/executions.jsonl` — every line is what the script emits, unchanged.
- Skip the end-of-phase push because the phase "went fine" — a clean phase (`--status ok`) is
  exactly the baseline the log needs to tell a real problem from silence.
- Attach a `--note` to `--status ok`, or omit `--note` on `degraded`/`failed` — the script refuses
  both.
