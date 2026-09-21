---
description: The staged loop with every hold removed — refine to merge, unattended.
argument-hint: <issue number or key>
---

Take issue **$ARGUMENTS** from where it is to merged, without stopping for a person.

This is `/aio:refine` → `/aio:propose` → `/aio:implement` → `/aio:sync` with the two holds taken out.
Follow each of those commands exactly as written, **except** that you do not apply `status:holding`
and do not wait for it to be removed.

**For work whose blast radius you already know.** The holds exist because a proposal nobody read and
a diff nobody reviewed are how a change lands that nobody wanted. Removing them is a decision about
this issue, made by the person who chose this workflow — not a default.

Everything else is unchanged, and the gates are not negotiable:

- The WIP limit in `.harness/config.json` still applies, and still names the issues holding it.
- `aspire do ci` still runs before the push, and still has to pass — every test assembly and both
  frontend gates, in one command that cannot be run as three.
- It still has to pass on what is about to be merged. **Not "CI is green"**: there is no CI in this
  repository and `gh pr checks` reports zero checks, so that sentence was true of every pull request
  here from the day it was written. The gate is a command, and the report says what it counted.
- A failure still halts, applies `status:holding`, and comments the reason. **Unattended is not the
  same as unsupervised**: the first thing that goes wrong is where a person comes back in.
