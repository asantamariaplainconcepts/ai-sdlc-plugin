---
name: implement-spec-poc
description: Proof of concept — implement an epic issue's whole ticket graph on one branch/PR by fanning out implementer subagents across worktrees. Use only when explicitly invoked for the PoC; it bypasses the /ds:* lifecycle.
disable-model-invocation: true
license: MIT
compatibility: Requires gh (authenticated), git worktree support, and the repo's e2e suite.
metadata:
  author: adapted for ds-connect from mattpocock/skills — skills/in-progress/implement-spec
  source: https://github.com/mattpocock/skills/blob/main/skills/in-progress/implement-spec/SKILL.md
  status: proof-of-concept
  version: "0.2"
  changelog: v0.2 hardens v0.1 with the lessons of the first 24-ticket run (see docs/poc-implement-spec-lecciones.md)
---

Implement a spec — here, an **epic issue and its child tickets** — as a single PR on a single branch.

**Input**: the epic issue number, given at invocation.

## The model

The tickets are **not a list of steps**. They are a **task graph** with blocking
relationships, so there is always a **frontier** of tickets ready to be grabbed.
But the graph is only scaffolding: **the defects live in the unwritten mesh of
pairwise interactions between tickets** — what one ticket's deletion does to
another's forward, what a message with no author does to a read counter. No
ticket can see that mesh from inside itself; carrying it is the orchestrator's
job.

Communication with subagents is therefore two-layered. **Content flows through
context pointers** — the epic issue, the ticket numbers, the exploration notes
directory, previous commit SHAs, archived changes readable on the branch. Never
paste content a subagent can read for itself through a pointer. **Coordination
state flows as explicit orchestrator summary** — shapes a merged sibling
established, obligations addressed to this ticket, warnings about in-flight
siblings, allocated values from shared counters. Mark it as summary: the
orchestrator gets details wrong, and a subagent must verify a summarized claim
against the branch before building on it (the best catches of the first run
came from workers checking the orchestrator's claims).

Implementer subagents run **in the background** for maximum concurrency, each
in its **own worktree on its own branch**. The worktree is not only conflict
isolation — it is the **crash-recovery unit**: if the session dies mid-run,
every agent's committed and uncommitted work survives in its worktree, and the
agent resumes from its own transcript. Budget for a rebase tax after any
interruption; never trade the isolation away for it.

**Nobody is watching.** This runs unattended, so no step may wait on a human —
not you, not a subagent. Where an invoked skill offers to pause, ask, or
confirm, decide instead and record what you assumed. Any CLI a subagent invokes
must run non-interactively (pass its `--yes`-style flag); a prompt aborts an
unattended run.

**The child tickets are read-only. The epic checklist is the one thing you
write.** Never edit a child ticket — no labels, no comments, no closing, not
even to mark progress. Progress lives in exactly one place: the checklist in
the epic, and **you tick it yourself** — never a subagent. Ticking rewrites the
whole epic body, so a single writer is what keeps two concurrent ticks from
clobbering each other.

## Steps

1. **Read the spec and the related issues — and gate on readiness.**

   Read each child ticket the epic lists. Read enough to derive the task graph:
   which tickets are independent, and which block others (shared schema, shared
   component, shared endpoint). Write the graph down — frontier plus blocked
   set — before spawning anything.

   **Definition-of-Ready gate:** a one-line ticket with no acceptance criteria
   cannot be handed to an unattended implementer as-is. If most tickets are
   bare captures, the exploration step (2) MUST produce a per-ticket criteria
   substitute from the product docs, or you stop and say the epic is not ready.
   When the run proceeds on substitutes, the deliverable to review is the
   **assumptions ledger**, not the code — say so in the PR body.

   **Open decisions get one owner.** Any product decision the tickets leave
   open is assigned to exactly one ticket in its brief. Two siblings deciding
   the same open question in parallel is a conflict you caused. When a decision
   is genuinely balanced, instruct the owner to prefer **the reversible
   direction** and to record the choice as an assumption a human must ratify.

2. **(Optional) Exploration subagent.**

   If the tickets need codebase or external-docs exploration, run one
   exploration subagent first. It must save markdown notes to a directory
   **outside the repo** that every later subagent can read — including a ranked
   **fan-out hazards** note: the files N agents will collide on, generated
   artifacts that must be regenerated rather than merged, and shared counters
   (enum ordinals, event ids) that need central allocation. Use the session
   scratchpad. Announce the notes path; it becomes a context pointer.

   **Pre-allocate shared counters yourself** — a deterministic value per ticket
   (e.g. `base + ticket-offset`), handed out in the brief. Expect most
   allocations to go unused (agents often find an existing mechanism covers
   them); the allocation buys collision-freedom, not a prediction of need.

3. **Create the branch and a draft PR.**

   Branch off `main` (the repo's default branch). Open the PR as a draft, based
   on `main`, and mark it as closing **the epic only** — `Closes #<epic>`.
   Never put a closing keyword on a child ticket: merging would close every one
   of them. List the ones this PR covers in the body as backticked refs
   (`` `#123` ``) — those neither close a ticket nor post a cross-reference on
   it. The PR title should be the epic issue title.

4. **Fan out implementer subagents — one per frontier ticket.**

   Each implementer gets its **own worktree and its own branch**: spawn it with
   `isolation: "worktree"` and the tool creates one for it — don't prepare them
   yourself. Use the `poc-implementer` command in every implementer subagent to
   implement its ticket, commit, and push **its own branch** — never the PR
   branch. Each must follow `AGENTS.md`.

   Its brief carries the pointers (epic URL, ticket number, notes path, PR
   branch name, merged SHAs) **plus the coordination summary**: the shapes
   already-merged siblings established that this ticket must extend rather than
   reinvent, any debt from the ledger (step 6) assigned to it, which siblings
   are in flight and where they will collide, its allocated counter values, and
   the standing policies below. Sizing the wave: launching every ready ticket
   maximizes the rebase tax on whichever finishes last; launching too few
   starves the run. Prefer the ready tickets with the most tickets blocked
   behind them.

5. **Fast-forward each completed ticket onto the PR branch.**

   The implementer already rebased onto the PR branch tip and resolved its own
   conflicts — it wrote that code, so it is the right agent for it. So this is
   a fast-forward, and you are the PR branch's single writer. If the ff is
   refused the branch moved underneath it: send that ticket back for one more
   rebase, don't resolve it here.

   **Merge order is a priority queue, not arrival order.** First-come-first-
   served systematically starves the branch touching the most contended
   surface — it finishes correct work and is invalidated by every sibling
   merge. When several branches are ready or near-ready: merge the one with the
   **largest contended surface first**, and **freeze the tip** (queue other
   finishers) for any branch already invalidated twice, until it lands.

   **Gate the shared branch after every fast-forward.** Every implementer's
   branch being green does not make the integration green: run at least the
   cheap gates (formatter check, typecheck) on the PR branch after each merge —
   and keep your own checkout as current as the workers keep theirs (a stale
   dependency tree in the orchestrator's checkout produces false failures, and
   a false failure can "fix" a branch that was never broken).

   Once the merge lands, **tick that ticket in the epic checklist yourself**:
   read the epic's body, flip that ticket's `- [ ]` to `- [x]`, and write the
   body back (`gh issue edit <epic> --body-file -`). This is the only issue
   write this skill performs.

6. **Recompute the frontier and walk the debt ledger.**

   A merged ticket may unblock others. **Recompute the whole ready set against
   the full graph every time** — never refill incrementally from "what did this
   merge unblock": a ticket whose single dependency merged early falls out of
   an incremental refill forever and nobody notices until the end.

   **Keep a debt ledger.** When an implementer reports something outside its
   scope — a missing row in an exhaustive table, a stale comment, a gap it
   couldn't test — record it and **assign it an owner**: a specific later
   ticket whose brief will carry it, or the fix pass. Scope discipline works
   exactly as designed — nine agents will correctly decline to fix another
   ticket's file — so an unowned debt survives to the end *because* the rules
   were followed.

   Spawn implementers for every newly ready ticket per step 4's sizing; do not
   wait for the current batch to drain.

7. **Verification pass, then one fix pass. Never the same agent.**

   With all tickets merged, run one **read-only verification subagent** on the
   PR branch: the full gate set, the e2e suite, and — because a pre-existing
   suite almost certainly covers none of the epic's new journeys — it must
   **write and run new journeys** for the epic's headline flows, capturing
   evidence per step. Forbidding it to fix anything is what makes it find
   things: the best instrument for an intermittent failure is **diffing a
   passing trace against a failing one** (the signal that differs is the cause;
   an error present in both runs is a red herring).

   Two audits are mandatory, because they catch what green gates cannot:
   - **The mutation audit.** Every absence assertion in the epic's tests
     (`queryX(...).toBeNull()` and kin) passes for free when the component sits
     in its loading state. Force the loading branch on, re-run, and require the
     absence assertions to FAIL; a survivor is not vindicated until the
     verifier shows *why* it survived. Reading the code and reasoning about it
     is a prediction; the mutation is the evidence.
   - **Coverage-requirement check.** Confirm the testing spec actually requires
     journeys for this epic's capability — an absent requirement is how N
     tickets land with zero coverage and nobody notices.

   Hand every finding — plus the accumulated ledger — to a **single ordinary
   subagent** (not `poc-implementer`, which only knows how to take a ticket
   from change to archive) to fix in one pass on a branch off the PR branch, in
   separately-labelled commits, with an explicit DO-NOT-FIX list for anything
   needing a human or a product decision. An intermittent fix must be proven at
   the mechanism (N repeat runs showing the passing signature), not by a single
   green run.

8. **Mark the PR ready for review** with `gh pr ready <pr>`. Not
   `mark-pr-ready` — it proceeds only on a human confirmation this run has
   nobody to give. Write the PR body for the review the run actually needs:
   lead with the decisions and assumptions to ratify, the known-red items that
   predate the branch, and what was reported-not-fixed.

9. **Clean up every implementer worktree and branch**, local and remote — but
   only after verifying each ticket branch is an ancestor of the PR branch.

## Standing policies (repeat these in every brief)

- **A clean rebase is not evidence of a correct one.** Non-overlapping hunks
  merge silently into code that does not compile, and one ticket can silently
  turn another ticket's tests vacuous. After ANY rebase, even conflict-free:
  typecheck first (a new required field on a shared type breaks every test
  factory with zero conflicts), then the full gate set.
- **Generated artifacts are regenerated, never hand-merged** — including their
  embedded copies (a migration's own embedded snapshot can be stale even when
  the main snapshot merges clean, which corrupts the *next* generation).
- **Carry/report for a red shared branch.** Carry the fix when the correct
  value is **determined elsewhere** (a formatter's output, a required field
  whose value the sibling's own type or spec fixes) — own commit, naming whose
  file and why. Report when it must be **decided here**. Never silence a gate:
  no loosened pins, no skipped tests, no escape-hatch markers.
- **Prefer a new capability spec over modifying a shared one, and ADDED over
  MODIFIED.** On a fold into a shared spec, verify the requirement **count**
  before and after — headers match textually, counts catch a silent loss.
- **Hot shared files survive by convention**: the first ticket to touch one
  extracts the seam; every later ticket adds at most one optional prop,
  contiguous, absent ⇒ unchanged behaviour. Zero additions beat clever ones.

## Report

Report the PoC outcome, not just the PR link:

- Task graph as derived, and how it changed once tickets landed.
- Peak concurrency vs. total tickets — did the frontier serialize?
- **Rebase rounds per merge** and their dominant cause (queue policy vs.
  in-flight concurrency).
- Conflicts per merge, **split textual vs. semantic** — a zero-conflict count
  hides the expensive ones.
- **Vacuous tests found by the mutation audit**, and unused counter
  allocations.
- Scope drift: any subagent that reached outside its ticket, and whether it
  was forward (into shared substrate) or sideways.
- Whether the pointer/summary split held, and every place the orchestrator's
  summary was wrong and a worker corrected it.
- The assumptions ledger: every decision an implementer invented where a skill
  would have asked a human, flagged for ratification.
