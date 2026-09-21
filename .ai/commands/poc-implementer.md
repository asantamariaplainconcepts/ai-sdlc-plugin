---
name: "PoC: Implementer"
description: Take one ticket end to end on the current branch — OpenSpec change, implementation, archive + spec sync. Runs inside an implement-spec-poc implementer subagent. v0.2, hardened with the first run's lessons.
category: Workflow
tags: [workflow, poc, openspec, implement-spec]
---

Take **one ticket** from end to end on the branch you are already on: OpenSpec change, implementation, archive + spec sync — all committed here, then pushed. This is the per-ticket worker of the **`implement-spec-poc`** skill.

**Input — it arrives in your spawn prompt, not from a human**

Your orchestrator hands you two layers. **Pointers** — your ticket issue number, the epic issue URL, the exploration notes path, the PR branch name, and the SHAs already merged. **Coordination summary** — shapes merged siblings established, debts assigned to you, in-flight siblings and where you will collide, your allocated shared-counter values. Treat the summary as a claim, not a fact: **verify it against the branch before building on it** (read the archived changes and specs the pointers name; the orchestrator gets details wrong). If the ticket number is missing, stop and report it — you run in the background with nobody to ask.

**Precondition — check before the first commit, don't assume it**

You must already be in your **own worktree on your own branch**. Your orchestrator sets this up (`isolation: "worktree"`); this command never creates it.

```bash
git rev-parse --git-dir --git-common-dir --abbrev-ref HEAD
```

Stop and report if the two git dirs are **equal** (you are in the primary checkout, not a linked worktree) or if the branch is `main` or the shared PR branch. Do not try to move yourself — a wrong starting point means the fan-out is misconfigured, and committing from here would push onto a branch you don't own.

**Steps**

1. Invoke **`read-issue`** for your ticket. Read it, the pointers you were given, and the archived changes of every merged sibling whose shapes you were told to extend. Nothing else.
2. Invoke **`openspec-ff-change`** to create the change and loop the artifact graph (`openspec status` → `openspec instructions`) until it is apply-ready — it batches the whole sequence with no per-file approval. Derive the change name from your ticket, and seed the ticket link and its UC/BR/actor IDs into the Why. **Prefer a brand-new capability spec over modifying a shared one, and ADDED requirements over MODIFIED** — a new file cannot conflict with 20 sibling archives. Commit.
3. Invoke **`openspec-apply-change`** to work the change's tasks, committing incrementally so the branch keeps its narrative. **Extend the shapes merged siblings established; never reinvent or duplicate them** — one error code per fact, one reading of a shared trace, one seam per hot file (at most one optional prop, contiguous, absent ⇒ unchanged behaviour; zero additions beat clever ones).
4. Run the repo's checks as `AGENTS.md` prescribes and fix what you broke. Do not hand over a red branch. **For every absence assertion you write** (`queryX(...).toBeNull()` and kin), give it a **positive anchor from the same render**, then prove it: force the loading branch on, re-run, require your absence assertions to FAIL, revert, confirm an empty diff, re-run green. A survivor is not vindicated until you can show *why* it survived. Reading the code is a prediction; the mutation is the evidence.
5. Fetch the PR branch and rebase your branch onto its tip. Resolve every conflict yourself: you wrote your side, so read the other side's ticket before choosing, and never resolve by blindly taking one side. **A clean rebase is not evidence of a correct one** — non-overlapping hunks merge silently into code that does not compile, and a sibling's new required field on a shared type breaks every test factory with zero conflicts. So after ANY rebase, even silent: typecheck FIRST, then re-run step 4 in full. **Regenerate generated artifacts rather than merging them** — delete yours and regenerate against the rebased model, including embedded copies (a migration's own embedded snapshot can be stale even when the main snapshot merges clean). Archiving on a stale branch is what makes spec conflicts, so this comes first.
6. Invoke **`openspec-archive-change`** to fold the delta specs into `openspec/specs/` and archive the change — **non-interactively** (its `--yes`-style flag; a prompt aborts an unattended run). When your delta MODIFIED a shared spec, verify the fold by **counting requirements before and after** — headers match textually, counts catch a silently dropped sibling requirement. Commit as `chore(openspec): close out <name> — archive + sync`.
7. Push your branch and report, as pointers only: branch name, head SHA, change name, **the shapes you established that later tickets must extend**, every assumption you invented (recorded in the change's Why), every debt you are leaving (addressed to the orchestrator's ledger), and anything you had to leave undone.

**Guardrails**
- **You cannot ask anyone anything.** You run unattended in the background, so `AskUserQuestion` only hangs you — and that overrides every skill you invoke that offers to pause, ask, or confirm (`openspec-ff-change`'s unclear-context branch, `openspec-apply-change`'s pauses). Decide, write the assumption into the change's Why, and continue. When a decision is genuinely balanced, **prefer the reversible direction** — the one a later slice can widen, rather than one that withdraws something already seen. If you genuinely cannot proceed, abort the ticket and report why — never wait.
- **Never touch a GitHub issue.** No labels, no comments, no closing, no checklist ticks — the epic and its tickets are read-only source of truth. The orchestrator owns the epic checklist.
- **Stay inside your ticket — with one bounded exception.** Something else looks broken? Report it in step 7; don't fix it. The exception is a **red shared branch**: carry the fix when the correct value is **determined elsewhere** (a formatter's output, a required field whose value the sibling's own type or spec fixes) — in its own commit, naming whose file and why you touched it, called out in your report. Report only when the fix must be **decided here** (a failing assertion, two tickets disagreeing on behaviour, anything modelling- or security-shaped). Never resolve another ticket's semantics to get your own gates green, and never silence a gate — no loosened pins, no skipped tests, no escape-hatch markers. A pinned count in a sibling's test that your change grows is **tightened to the new count, never loosened**.
- **You own only your own branch.** Never push, merge, or rebase the PR branch — the orchestrator is its single writer. Rebasing *onto* it (step 5) is not writing it. Verify that at the start (see the precondition) rather than trusting the setup. Expect the tip to move more than once while you work; each move is one more round of step 5, not a reason to skip it.
- **Never call `gh pr`.** This command neither creates nor touches a pull request.
- Reuse the `openspec-*` skills; don't re-implement artifact generation, application, or archiving here.
- **Use your allocated shared-counter values exactly** — never "the next free one" — and report an allocation you did not spend; most go unused, and the orchestrator tracks the free list.
