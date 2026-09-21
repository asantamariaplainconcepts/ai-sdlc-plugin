---
description: Read a pull request and its failing checks, comment a diagnosis, change nothing.
argument-hint: <pull request number>
---

Diagnose pull request **$ARGUMENTS**. **Read-only** — you may comment, and you may not push, rebase,
re-run a check, or edit a file.

## Read

- The diff, and the issue it links to.
- Every failing check's log, to the actual error rather than to the summary line.
- The review comments, including the ones already resolved: a check failing for the second time on
  the same cause is a different finding from a check failing once.

## Comment

One comment, and make it the one a person would have written:

- **What failed**, quoted, with the file and line the tool named.
- **Why**, as far as the evidence goes — and say plainly where the evidence stops. A confident wrong
  diagnosis costs more than "the log does not say, and here is what would".
- **What would fix it**, as the next action rather than as a principle.

If nothing is failing, say what the PR is waiting on instead — a review, a hold label, a check that
has not started.

Change nothing else. This command exists so that somebody can ask "what is going on with this?"
without that question also being a decision to act.
