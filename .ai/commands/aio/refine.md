---
description: Interrogate an issue to the Definition of Ready, then label it ready-for-proposal.
argument-hint: <issue number or key>
---

Take issue **$ARGUMENTS** to the Definition of Ready. Write no code.

## Read first

- The issue body and every comment on it.
- Closed issues that touched the same area. They are the archive now that `docs/backlog.md` is
  gone, and twice they have caught an issue proposing something already shipped:
  `gh issue list --state closed --search "<the area>"`.
- `ARCHITECTURE.md` for where the change would land: which module, which side of the wire.

## The Definition of Ready

An issue is ready when all five are answerable from the issue itself:

1. **What is true now**, stated as an observation rather than a complaint. A number, a path, a
   message. "The terminal pane is 80px at 560×700" is an observation; "the layout is broken" is not.
2. **What should be true instead**, and why that is better for the person using it.
3. **Which seam it lives behind.** If it changes a reach outside the process it names one of the
   `BuildingBlocks/World` interfaces; if it does not, it says which module and feature folder.
4. **The check.** How somebody else would know it is done, without asking you. A command to run, a
   screen to look at, a number to compare.
5. **What it is not.** The nearest thing this deliberately does not do.

## What to do about a gap

Comment on the issue, naming the gap and what would close it. One comment, addressed to the person
who opened it, in their words. **Never a bare rejection** — "needs more detail" tells nobody
anything, and the point of this step is that the next person does not have to guess.

When all five are answerable, add the label `status:ready-for-proposal` and stop. Do not branch, do
not open a pull request, and do not start on the change.

## Opt-in controlled Task input

Legacy issues keep the DoR above. The `Controlled Task` issue form additionally writes one
`### Task input` JSON block. It is the structured shape, not a second copy of the OpenSpec
specification. Preserve acceptance IDs when editing it; distinguish `implementation` from `epic`.
Do not add a competing prose Seam section.

`POST /api/tasks/input/validate` assesses `{ identity, body, bodyTruncated }`. Supply the current
complete body and identity `{ provider, host, repository, key }`; for GitHub cloud these are
`github`, `api.github.com`, lowercase `owner/name`, and a positive decimal issue number without
`#`. The caller must establish provider provenance and freshness; the endpoint does not fetch
or authorize anything.

Dependencies are `{ "type": "depends-on", "target": { "provider": "github", "host":
"api.github.com", "repository": "owner/name", "key": "42" } }`, or `type: "related"` for a
nonblocking reference. Prose numbers are never dependencies. Blocking references stay unresolved
until an authoritative consumer checks them; even a nonexistent reference cannot be called
satisfied by this parser. Empty arrays explicitly declare no dependencies or open decisions.

The assessment returns field-level remedies and a body digest. `valid-input` only means the
shape passes, not that a Task is ready, approved or executable. Aggregate epics do not pass
implementation assessment. Lifecycle enforcement is separate ACP-03/07 work; nothing here
changes a Hold or the current lifecycle.
