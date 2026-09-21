---
description: Work an approved OpenSpec change's tasks on its existing branch and PR, then hold for review.
argument-hint: <issue number or key>
---

Implement issue **$ARGUMENTS** on the branch and PR `/aio:propose` already opened — same branch, same
PR, same change. Wraps OpenSpec's apply. Ends holding.

**Two gates, in this order**

1. **Hold.** If the issue still carries `status:holding`, stop and say who has not read the proposal.
2. **WIP.** Read `wipLimit` from `.harness/config.json`, count issues at `status:in-progress`, and at
   or above the cap stop and **name the issues holding it**.

Below the cap: label `status:in-progress` **before the first implementation commit**.

**Steps**

1. Check out the change's existing branch. No new branch, no second PR.
2. `openspec status --change "<name>" --json` — everything the schema requires before implementation is
   `done`, or this is an unfinished proposal and the fix is `/aio:propose`.
3. Invoke the **`openspec-apply-change`** skill to work the tasks, committing incrementally. Do not
   squash locally — the branch is the record until the merge. **Annotate every task you close** — the
   format is below, and it is the only record of which file served which requirement.
4. Implement behind the seam the issue names. If something above it must change, that is the finding
   and it goes in the issue, not quietly into the diff.
5. Keep the spec delta true to what you built: it is what `/aio:sync` folds into `openspec/specs/`.
   Where implementation proved a requirement wrong, edit the delta here and say so in the PR.
6. Verify against reality, not against a fixture — every bug found in committed code here was found by
   running the product.
7. Run the gate, then re-validate the change:

   ```bash
   aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj
   openspec validate "<name>" --strict
   ```

   One command, no halves — `AGENTS.md#the-gate` says what it counts and what is left out of it. A
   report missing an assembly is a subset, not a success. The `.csproj` path is not optional from a
   worktree.

   Then push a report — `push-session-report` skill, `--phase implement --status ok` if the gate
   passed, or `--status failed --note "<what failed>"` if it did not.
8. If a screen moved, retake **both halves** of the record here, where the product is already running,
   and commit only the files that differ. A screen you did not touch moving is a finding for the issue.

   ```bash
   docs/design/capture.sh current                      # the 1440 set
   dotnet test src/tests/AiOrchestrator.EndToEndTests \
     --filter "FullyQualifiedName~NarrowSet" \
     --logger "console;verbosity=detailed"              # the 390 set
   ```

   `capture.sh` cannot take the narrow half — headless Chrome lays a 390px window out at 500px and
   refuses a dark shot — so the two commands are not alternatives. The detailed logger prints the
   numbers behind each capture; **a surface that no longer fits is a finding with its picture, not a
   layout you fix inside this change.** `docs/design/README.md#the-narrow-set` says what each covers.
9. Push, mark the PR ready, fill the issue's **check** line with what you observed — numbers, not
   adjectives — and apply `status:code-review` **and** `status:holding` in one edit. Push a report —
   `push-session-report` skill, `--phase implement-ready --status ok`. Say how many tasks are done.
   Stop.

**Annotate what you closed**

Ticking a task in `tasks.md` writes, under it, which requirements of this change's spec delta the
task served and which files it moved:

```
- [x] 1.2 Read the pull request in one call
      req: issue-tab/1, issue-tab/2
      files: src/worktree/pull-request.ts
```

- `req:` and `files:` each on their own continuation line, indented past the `-`, comma-separated.
- A requirement is `<capability>/<ordinal>` — the folder under this change's `specs/`, and the
  requirement's position in that file. `files:` are repository-relative paths.
- Either line may repeat under one task; the lists join. Every file under a task takes every
  requirement under it.
- **Only a closed task carries one.** A task still open has moved nothing, so what it names is a plan.
- **A task you cannot attribute is closed without a `req:` line, never with a guessed one.**
  Scaffolding, a rename and a formatting pass genuinely serve no requirement of this change, and a
  file that ends up unlabelled says exactly that — nobody declared why it is in the diff.

This is your claim about your own work. Nothing verifies it, a person reviews it like the rest of
`tasks.md`, and two screens read it: the Change tab's **Code** step labels the diff's file rows from
it, and its **Tests** step maps a test to a requirement from the same lines — a test file is a
`files:` entry like any other, so there is one format and not one per step.

**Guardrails**
- Never implement an unheld-gate proposal, and never start above the WIP cap.
- One PR per issue.
- Reuse `openspec-apply-change`; keep commits meaningful.
- **Hit a missing prerequisite, an instruction that didn't match reality, or need a workaround at
  any step above?** Push it the moment it happens, not only at steps 7 and 9:
  `push-session-report` skill, `--phase implement --status degraded` (adapted and continued) or
  `--status failed` (could not), with a one-line `--note` — the fact, not a mood.
