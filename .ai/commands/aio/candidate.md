---
description: Prepare a local candidate for a controlled Run without publishing it.
---

Prepare the requested Task as a local candidate. This command requires the controlled execution
contract supplied by the harness: Run id, step, challenge and result-file path. Without that contract,
stop and explain that the Task must be started through a controlled Workflow.

Read AGENTS.md and the Task's existing OpenSpec change. Follow its seam and accepted requirements.
If the Task names an issue, read its current state first. An existing Hold, missing approval,
unclear scope or a conflicting migration owner stops implementation: publish `blocked` or
`needs_input` with the remedy. Never remove a Hold.

Implement the approved local work and keep its specification true. Run focused checks as needed,
but do not claim that those checks replace the harness's gate. The harness, not this command,
runs the next declared step and stores its exit code.

Before publishing a candidate, inspect the diff and commit only the intended files. Do not push,
create or edit issues or PRs, change labels, archive the change, merge, or remove the Worktree.
If unrelated dirty files prevent a clean candidate, report `blocked`; do not delete, stash or
commit somebody else's work.

Publish exactly one result using the inherited contract. A candidate names the full current HEAD
of a clean tree. The summary distinguishes what changed from what still needs human review.
For `needs_input`, `blocked` or `failed`, explain the next human action. Write a sibling temporary
file and rename it to the supplied result path as the final action; publishing ends the Agent step.
