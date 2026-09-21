---
description: Branch, write the OpenSpec change, open a draft PR, and hold for a person.
argument-hint: <issue number or key>
---

Turn issue **$ARGUMENTS** into an **OpenSpec change** on its own branch, opened as a draft PR for a
person to read. Wraps OpenSpec — never call `/opsx:*` by hand. Ends holding.

**Gate:** refuse if the issue does not carry `status:ready-for-proposal`, and name `/aio:refine` as
the fix.

**Steps**

1. Fetch, then branch `change/<issue>` from current `origin/master`. A stale local ref is the one
   mistake here that survives review.
2. Read the capabilities this touches — `openspec list --specs`, then `openspec show <id> --type spec`.
   The delta goes **against an existing capability** wherever one covers the ground; a new id is for
   behaviour with no home.
3. Invoke the **`openspec-propose`** skill for the artifacts (proposal, design, spec deltas, tasks),
   seeded with the issue: number, link, intent, its **Seam**, and the prior art in the seam you are
   opening (`Prior art:` comments in `BuildingBlocks/World`).
4. `openspec validate "<name>" --strict` and `openspec status --change "<name>"`.
5. Commit the artifacts and push. Write no implementation — that is `/aio:implement`'s.
6. Open a **draft** PR against `master`, titled after the issue, with `Closes #<n>` and the change name.
   Then push a report — `push-session-report` skill, `--phase propose --status ok`.
7. Apply `status:ready-for-implementation` **and** `status:holding` in the same `gh issue edit`, and
   put the change name on the issue.
8. Say what waits on whom, in one line. Stop.

**Guardrails**
- Reuse `openspec-propose`; do not re-implement artifact generation here.
- The PR is a draft on purpose — it is the spec-review gate.
- `tasks.md` is one issue's own plan on its own branch, which is why it may carry checkboxes where
  `AGENTS.md` forbids them. Coordination stays in the issue, the label and `git branch --list`.
- Never remove your own hold, and never continue into `/aio:implement`.
- **Hit a missing prerequisite, an instruction that didn't match reality, or need a workaround at
  any step above?** Push it the moment it happens, not only at step 6: `push-session-report` skill,
  `--phase propose --status degraded` (adapted and continued) or `--status failed` (could not),
  with a one-line `--note` — the fact, not a mood.
