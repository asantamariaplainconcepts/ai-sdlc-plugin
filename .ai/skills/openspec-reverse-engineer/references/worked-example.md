# Boundaries, names, and one worked example

Read this from step 3 or 4 of [`../SKILL.md`](../SKILL.md), when a capability boundary or a name is
unclear. It shows the rules applied; it does not restate them.

## What one capability is

A capability is **behaviour whose absence a person would notice**, at the size you would write an
issue about. The boundary test: if two behaviours can be removed independently, they are two
capabilities; if describing one forces you to describe the other, they are one.

`src/modules/Workspace/…/Features/Folders/UseCases/` holds twenty use cases. The folder is not the
capability, and neither is the module:

| Too granular | Too coarse | The size that works |
| --- | --- | --- |
| one spec per use case — `add-folder`, `list-folders`, `remove-folder` each restating the same `Folder` | one spec for `workspace` — twenty behaviours under one Purpose, and no requirement anybody can check | `choose-a-folder`, `folder-commands`, `worktree-lifecycle`, `read-the-working-diff`, `read-a-file`, `open-a-terminal` |

Group by the behaviour, then confirm the grouping by opening every file you assigned to it. Four use
cases — `ChooseFolder` (`POST /api/folders/choose`), `AddFolder` (`POST /api/folders`), `ListFolders`
(`GET /api/folders`) and `RemoveFolder` (`DELETE /api/folders/{id:guid}`) — describe one thing: the
list of directories this machine works on. `SetFolderAgent` (`PUT /api/folders/{id:guid}/agent`,
`WorkspacePermissions.Configure`) does not; it configures a folder that already exists, and it can be
removed without touching any of the four.

## Naming

Kebab-case, and the grammar carries the kind:

- **Verb-object** for behaviour a person invokes: `choose-a-folder`, `run-a-task`, `read-a-file`.
- **A noun** for a substrate or a policy nobody clicks: `module-boundaries`, `design-system-compliance`,
  `repo-structure`, `the-gate`.
- Never the implementation's word when the product has its own. The vocabulary is locked at the end of
  `README.md` — Folder, Worktree, Task, Connector, Workflow, Hold, Run, Agent. A spec called
  `dbcontext-per-module` is naming a mechanism; `module-boundaries` is naming the capability.

## Worked example: `AddFolder.cs` to a requirement

**What was read.** `Features/Folders/UseCases/AddFolder.cs` (route, `[Requires]`, validator, handler);
`src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/PlainDirectoryTests.cs` (test names);
the `#98` entry in `docs/process/retro-log.md` (why a folder's commands are rows, not a file).

**The Purpose** comes from the type's own summary and the retro entry, in product words:

```markdown
## Purpose
Points the app at a directory on this machine, whether or not git has heard of it. The gate is
"what is this", not "is this a repository": a plain directory is somewhere the product can run
things, it just has no probed git fields (#98). Reverse-engineered from the shipped code, not
negotiated ahead of it.
```

**The requirement** says what the code does, with the real names in it:

```markdown
### Requirement: A directory is added by its path, probed before it is stored
The system SHALL expose `POST /api/folders` (`AddFolder.cs`), open to any caller — the one folder
operation with no folder to hold a permission on, so it declares `Access.AnyCaller` rather than a
permission. The path SHALL be non-empty and at most 1000 characters. The system SHALL canonicalise
the submitted path and every stored path before comparing them, in memory rather than in SQL, so a
hand-typed `~/code/portal` and a chooser's `/Users/me/code/portal` are one folder; a match SHALL be
refused with `FolderErrors.AlreadyAdded`. The path SHALL be probed through `IWorkspaceProbe` **before**
the row is written, and a path the probe cannot read SHALL be refused rather than stored. A directory
git cannot describe SHALL still be stored, with `IsRepository` false and the aggregate's own defaults
— no branch, no worktrees, nothing uncommitted — and only a checkout SHALL record a probe.

#### Scenario: A directory with no git is added
- **WHEN** a path that is a plain directory is submitted to `POST /api/folders`
- **THEN** the folder is created with `IsRepository` false and no probed git fields

#### Scenario: A path that is not there is refused
- **WHEN** the submitted path does not exist on this machine
- **THEN** the request is refused with the reason and no folder row is written

#### Scenario: The same checkout typed two ways is one folder
- **WHEN** a path is submitted that canonicalises to an already-stored path
- **THEN** the request is refused with `FolderErrors.AlreadyAdded` and no second row is written
```

Those three scenarios are grounded in `PlainDirectoryTests` —
`A_directory_with_no_git_is_added_and_holds_commands`, `A_path_that_is_not_there_is_refused_by_saying_so`,
`A_checkout_git_cannot_describe_is_refused_rather_than_demoted`. Read the test bodies before you keep
a scenario a test name suggested: the name says what somebody intended to pin, the body says what is
pinned.

## How a requirement reads

The 56-spec corpus the format was measured over (the sibling `ds3d/ds-connect` project) is
consistent about four things, and they are worth copying:

- **`SHALL` for what happens, `SHALL NOT` for what must not.** "The system SHALL NOT restore the
  closed case's messages" is a requirement; "should avoid" is not.
- **Bold on the decisive word**, sparingly, where a reader skimming would otherwise miss the hinge —
  probed **before** it is stored, `IsRepository` **false**.
- **Real identifiers inline.** There the requirements cite business rules and locked decisions
  (`BR-002`, `DEC-016`); here the equivalents are the ADR number (`ADR-0003`), the analyzer id
  (`MOD001`), the route, the type name, and the issue or PR a retro entry names (`#98`).
- **A requirement may name the capability that owns the rest.** Where behaviour continues in another
  spec, say so — "the file's contents are delivered by `read-a-file`" — so a reader does not read the
  boundary as a gap.

## What the sweep refuses to write

**Ungrounded.** `GetPatch.cs` exists, so it is tempting to write "the system SHALL let a person apply
a patch". The file name is not the behaviour. Open it, find the route and the handler, and write what
they do — or write nothing and report the file as unread.

**Aspirational.** A `TODO`, a commented-out branch, a half-wired button and an issue still open are not
requirements. They go in the report as findings, in the words the code used.

**Contradicted.** When a document and the code disagree, the requirement follows the code and the
report carries the disagreement with both paths. The retro log records one of these on purpose:
`CONTRIBUTING.md` and the `set-issue-status` skill name nine statuses where the code derives six.
That is a decision for a person, not something a spec resolves by picking a side.

## Not a capability

Say so, with the reason, rather than leaving the path unclassified:

- `src/frontend/styles/` — vendored and generated; edited upstream and re-imported.
- `bin/`, `obj/`, `node_modules/`, `src/frontend/pnpm-lock.yaml` — build output and lockfiles.
- `design-import/Harness.html` — the screens this was built from: input to the work, not behaviour it ships.
- `openspec/` itself — the specs are the output of this skill; they are not a capability of the product.
