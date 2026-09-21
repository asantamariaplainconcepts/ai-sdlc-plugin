---
description: Append what a merged change taught to the retro log.
argument-hint: <issue number or key>
---

Issue **$ARGUMENTS** has merged. Write down what it taught.

**Only what `/aio:sync` could not see** — its entry rides in the close-out commit, before the merge.
This is for a post-merge finding, or a backfill. Where sync already wrote one, append a new dated
entry; the log is append-only.

## Read

The merged diff, the review conversation, and the backlog item's **check** line — particularly where
what was observed differs from what was expected when the item was written.

## Append one entry

Push a report first — `push-session-report` skill, `--phase review --status ok` — then invoke
`collect-usage` for this change's `.telemetry/executions.jsonl` lines. Most often there are none:
the worktree `/aio:sync` pushed from is already gone by the time this runs, so say that plainly
rather than treating an empty result as a failure. Where a line does exist, pass its summary and any
`degraded`/`failed` notes into `retro-entry` as candidate reflections, same as `/aio:sync` does.

To the retro log, in the shape the rest of it uses. An entry is worth writing when it is one of
these, and not otherwise:

- **Hit a missing prerequisite, an instruction that didn't match reality, or need a workaround while
  writing this entry?** Push it the moment it happens: `push-session-report` skill, `--phase review
  --status degraded` or `--status failed`, with a one-line `--note`.

- **A bug that the suite did not catch**, with the reason it did not. That reason is the entry; the
  bug is the example.
- **A case the design did not have**, found by running the thing.
- **A place where prior art was right and this was not**, or the reverse — including when the answer
  was that the prior art is right for its own shape and this deliberately does something else.

## What not to write

Not a summary of the change — the diff is the summary, and a paragraph restating it is a paragraph
nobody reads twice. Not a resolution ("be more careful"). Not an entry at all, if the change taught
nothing: an honest empty retro is worth more than a log of filler nobody trusts.
