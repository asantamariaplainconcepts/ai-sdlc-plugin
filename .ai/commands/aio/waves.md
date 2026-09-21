---
description: Split ready issues into waves that fit the WIP limit and will not fight over a file, claim their branches, and say what to hand each agent.
argument-hint: <label, or issue numbers> — defaults to status:ready-for-proposal
---

Plan the waves for **$ARGUMENTS**. Write no code, and start no work.

A **wave** is the set of issues that may be in flight at once. Its size is not a preference:
`wipLimit` in [`.harness/config.json`](../../../.harness/config.json) is the cap, and the comment beside it is
the reason — *two agents at once is what one person can actually review; above that the queue moves
and the review does not*. A plan that exceeds it is a plan to stop reviewing.

## Read first

**The milestones, before anything else.** They are not a report this command writes and forgets —
they are where the plan lives between runs, so this reads them the way it expects everybody else to:

```bash
gh api repos/{owner}/{repo}/milestones --jq '.[] | "\(.title): \(.open_issues) open, \(.closed_issues) closed"'
```

Then, depending on what they say:

- **A wave with open issues** — it is in flight. **Do not re-plan.** Report which issues are left,
  who holds their branches (`git branch --list 'wave*/*'`), and stop. Re-deriving now would reshuffle
  work an agent is in the middle of, and the first it would hear of it is a conflict.
- **Every wave closed, ready issues left over** — plan the next one, numbered after the last.
- **No milestones at all** — first run; plan wave 1.
- **A milestone that exists for the wave you are about to write** — assign to it, do not create a
  second. Titles are not unique on GitHub, and two "Wave 2" is a plan nobody can read.

Then:

- The issues. Each one carries its **seam** — which module and feature folder, or which
  `BuildingBlocks/World` interface — because `/aio:refine` does not let one through without it. That
  is the file list; you do not have to guess it.
- Their **Depende de / Blocked by** lines. An issue whose blocker is still open belongs to a later
  wave, however free its files look.
- **What collides** in [`AGENTS.md`](../../../AGENTS.md) — the five rules earlier loops paid for,
  which say what fights beyond the obvious: `World.cs`, migration snapshots, the three shared
  frontend files, and moving code out of a file somebody else owns.

## Derive the waves

1. **Order by dependency.** An issue whose blocker is unfinished cannot be in the same wave as its
   blocker. Not "probably fine" — the blocked one derives its data from the other, and building it
   twice is the duplication the blocker exists to remove.

2. **Find the file collisions, and weight them by size.** Two issues that edit the same file do not
   go in one wave. **Measure it** — `wc -l` — because the cost is not the fact of a shared file but
   how much of it moves: two additions to `en.ts` merge themselves, two rewrites of a 1400-line
   screen do not. A file over ~500 lines that two issues both restructure is one issue at a time,
   whatever the dependency graph says.

3. **Fill the wave to the cap, no further.** Prefer pairing issues that touch nothing in common:
   one backend-shaped and one frontend-shaped is the easiest pair to review in an afternoon.

4. **A small issue is not free.** It still costs a branch, a PR and a review slot. Put it in a wave
   next to work that is already in its files rather than inventing a wave for it.

## Claim

**Run this from the main checkout.** It creates the worktrees; it does not need to be in one. Doing
it from inside a worktree still works — they share a `.git` — but the paths below resolve against
the repository root, so the checkout is where they land predictably.

**The branch is the claim.** Not a line in a file — a worktree cannot see another's uncommitted
edits, so two agents would take the same issue and find out days later. Branches live in the shared
`.git`, so they are visible the moment they exist:

```bash
git branch --list 'wave*/*'
git worktree add .worktrees/wave<n>-<issue> -b wave<n>/<issue>
```

**`.worktrees/`, because that is where the product itself puts them** —
`GitWorktreeManager` writes `<root>/.worktrees/<branch with / as ->` and says why: *"a worktree under
the repository root would be walked by every `ls-files` and every build in the parent"*, and the
screens were designed around that shape. A worktree somewhere else is invisible to nothing — `git
worktree list` finds it either way — but it sits apart from every worktree the app made, which is a
second convention for one thing.

One worktree per issue in the wave, named after the issue.

## Hand off

For each issue in the wave, say in one block: the issue number, its worktree path, the files it
owns, and the one sentence of context its agent will not find in the issue — usually which
neighbouring issue is in flight and what it will change underneath them.

## Record it on GitHub, and nowhere in the repository

**A milestone per wave**, and every issue in it assigned:

```bash
gh api repos/{owner}/{repo}/milestones -f title="Wave <n> · <what>" -f description="<why these, and what shapes the order>"
gh issue edit <n> --milestone "Wave <n> · <what>"
```

The description is where the planning goes — which measurement forced the order, which issue looks
deferrable and is not. Not a doc: **a file in the repository is on a branch**, so an agent in one
worktree cannot read what another wrote there until both merge, which is exactly when the warning
was needed. `AGENTS.md` says so about the claim, and it is just as true of a plan — which is why the
four documents that used to hold one are gone.

Nothing is written twice:

| Question | Answered by |
| --- | --- |
| Which files does this issue own? | The issue's **Seam** — `/aio:refine` will not pass an issue without one. |
| Which ship together, why, how much is left? | The **milestone**. |
| Who has taken it? | The **branch**. |

**If a wave needs ownership the seam does not state, fix the issue.** Do not write it into the plan:
the next agent reads the issue, and a seam that undersells its files is a refinement that let something
through — which is a finding about the issue, not a footnote about the wave.

**The milestone is also this command's memory** — that is why it is read first and why it is closed
rather than announced. Close it when its last issue merges: the next run sees a finished wave and
plans the following one. Leave it open with nothing in it and the next run reports an empty wave in
flight and refuses to move, which is the correct failure — a plan that cannot tell "done" from
"nobody started" should stop rather than guess.

## When something fails

- **A wave that will not fit under the cap** is not a bigger wave. It is two waves; say which issues
  wait and why.
- **An issue whose seam is missing or vague** goes back to `/aio:refine` rather than into a wave. You
  cannot place what you cannot locate, and guessing its files is how two agents end up in one.
- **Two issues that must edit the same file and cannot be ordered** — say so and stop. Splitting the
  file first is a change of its own, and it belongs to whichever issue was going to restructure it
  anyway, in its own wave, and said in that issue before it happens.
