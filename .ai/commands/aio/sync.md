---
description: Archive the change on its branch, squash-merge with the gate green, and remove the worktree.
argument-hint: <issue number or key>
---

Land issue **$ARGUMENTS**. Wraps OpenSpec's archive, which runs **on the branch before the merge**, so
one squash commit carries the implementation, the synced specs, the archived change and the retro.

**Gates**

- The PR is **approved** and carries no `status:holding`.
- Every task in the change is complete (`openspec status --change "<name>" --json`) — otherwise name the
  open ones and stop.
- **`aspire do ci` passed on this machine, on what is about to be merged.** Not "the checks are green":
  `gh pr checks` reports zero checks on every PR here, so that sentence is true of a change that works
  and one that does not (`AGENTS.md#the-gate`). A red step is a stop.
- Nothing here is marked `[skip ci]` — there are no workflows to suppress, so the marker would be cargo.

**Steps**

1. **Bring the branch up to date with `origin/master` first** (`rebase-safely` for a rebase). The
   archive folds this change's deltas into the specs as they stand on the branch, so a stale branch
   folds them into out-of-date specs. Merges are sequential for this reason.
2. **Archive on the branch** — invoke `openspec-archive-change`, whose work is the CLI's:

   ```bash
   openspec archive "<name>" --yes      # add --skip-specs for a change with no delta; say that you did
   openspec validate --specs --strict
   ```

   Decline the skill's delta-sync branch: it delegates to `openspec-sync-specs`, which does not exist in
   `.ai/skills/`. `openspec archive` is what updates the main specs.
3. Push a report — `push-session-report` skill, `--phase sync --status ok` — then invoke
   `collect-usage` for this change's `.telemetry/executions.jsonl` lines (or its explicit "no
   telemetry pushed" statement if there are none) and pass that summary into `retro-entry`. Append
   the **retro entry** now, on the branch: what the change taught, not what it did — `retro-entry`
   offers this change's `degraded`/`failed` notes as candidate "what didn't work" reflections; a
   human still confirms, edits or rejects them. If the finding is a structural workflow change,
   write the ADR and link it.
4. Commit the close-out — synced specs, archived change, retro entry — and push. The PR now carries the
   final state.
5. **Squash-merge** into `master`. Check the title first: a title still describing the proposal is the
   subject `master` keeps.
6. **Close the issue.** If half is genuinely left, say which half and open the remainder as its own issue.
7. **Delete the branch in two steps, never `gh pr merge --delete-branch`** — that flag moves the local
   checkout off the branch and git refuses while another worktree holds `master`, after the merge has
   already landed:

   ```bash
   gh pr merge <pr> --squash
   git push origin --delete <branch>
   ```

8. If the merged diff touched `src/frontend/app/` or `src/frontend/features/` and nothing under
   `docs/design/current/` came with it, say so on the issue — one line, naming the screens. **Do not
   capture here**; a merge that halts for a screenshot is worse than a stale one.
9. Same test, the evidence: comment the committed screenshot pinned to the **squash-merge SHA**, never
   `master` (a window on the present, not evidence) and never the branch SHA (unreachable after step 7):

   ```bash
   SHA=$(git rev-parse HEAD)
   gh issue comment <n> --body "![<screen>, after](https://github.com/{owner}/{repo}/blob/$SHA/docs/design/current/<screen>.png?raw=1)"
   ```

10. **Close the wave's milestone if this was its last open issue** — `/aio:waves` reads milestones to
    decide what to plan next.
11. **Remove the worktree last, from the main checkout**, on the path git reports: stop the AppHost first,
    use `git worktree remove` and never a recursive delete, do **not** force it, and delete the local
    branch too (a squash-merge leaves git unable to see it as merged). Your working directory is gone
    once this runs, so name the main checkout in every later command.

**Guardrails**
- Archive and retro before the merge, so `master` lands one commit per change.
- A dirty worktree at step 11 is work that never landed: halt, hold, comment what the status showed.
- Never set the issue done before the merge completed.
- **Nothing retries.** A failure halts, applies `status:holding`, and comments the reason in the words the
  tool used. Before the merge that costs nothing; after it, say plainly that `master` has the change.
- **Hit a missing prerequisite, an instruction that didn't match reality, or need a workaround at
  any step above?** Push it the moment it happens, not only at step 3: `push-session-report` skill,
  `--phase sync --status degraded` (adapted and continued) or `--status failed` (could not), with a
  one-line `--note` — the fact, not a mood. Push this before step 11 removes the worktree —
  `.telemetry/` does not survive it.
