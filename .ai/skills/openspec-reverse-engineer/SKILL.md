---
name: openspec-reverse-engineer
description: Reverse-engineer shipped code into baseline OpenSpec specs under openspec/specs/. Use when a capability exists in the code but has no spec, when adopting OpenSpec on a codebase written before it, or when asked to write down what the code does today as requirements and scenarios.
---

Turn code that already ships into `openspec/specs/<capability>/spec.md` — one **baseline** spec per
capability — one responsibility. Do not propose, design or implement anything (that is
`openspec-propose` and the `/aio:*` commands), and do not create a change, a branch, a commit or a PR.

**Baseline, not proposal.** The code exists, so there is nothing to propose: no change directory, no
`## ADDED Requirements` delta, no work-item frontmatter. GitHub owns the state of the work
(`AGENTS.md`); a spec written here says what the code does today. A change that lands the ordinary way
is archived to `openspec/changes/archive/<date>-<name>/` and its delta folded into the very same
`openspec/specs/<capability>/spec.md`, so what you write must be indistinguishable in shape from a
spec that arrived that way.

**Grounded, or not written.** Every requirement names an artifact you opened — a route, a handler, a
component, a token, a config key, a command step, a test. A file name, a TODO, a comment promising
future work and a document are not behaviour.

## Steps

1. **Scope the sweep.** Take the path, module, feature or capability from the request. If nothing is
   named, ask the human which area to sweep; the whole repository is a valid answer and a long run.
   - Done when: scope is a concrete list of paths, and you have said which paths it excludes.
2. **Read what is already specified.** `openspec list --specs` for the ids, then
   `openspec show <id> --type spec` for anything that overlaps the scope.
   - Done when: for every existing capability you can say whether it covers part of the scope.
     Overlap means **amend that `spec.md`** — never a second spec saying the same thing in other words.
3. **Inventory the capabilities.** Walk the scope with the discovery map below. Name each capability
   in kebab-case: verb-object for behaviour a person invokes (`choose-a-folder`, `run-a-task`), a noun
   for a substrate or a policy (`module-boundaries`, `design-system-compliance`). Split by observable
   behaviour, not by file or class. Read
   [`references/worked-example.md`](references/worked-example.md) when a boundary or a name is unclear.
   Calibrate the size against the corpus this format was measured over — the 56 specs and 385
   requirements of the sibling `ds3d/ds-connect` project: median 6 per spec, 43 of the 56 between 2
   and 7. A capability heading past 15 requirements is two
   capabilities, unless it is a cross-cutting policy that genuinely enumerates (an end-to-end test
   charter, a deployment lane).
   - Done when: every path in scope appears exactly once — under a capability id, or under a
     *not a capability* list carrying its reason (generated, vendored, config no code reads). A path
     you did not open is not classified.
4. **Ground every requirement.** Per capability, read the code that implements it, then the tests
   that pin it, then the retro-log entries that touch it. The code gives the requirement, the tests
   give the scenarios, the retro log gives the Purpose and the why.
   - Done when: every requirement has at least one real path behind it, and every disagreement
     between code, test, doc and retro entry is written down for the report. **The code wins**; never
     resolve a disagreement silently, and never write a requirement for behaviour you could not find.
5. **Write the spec.** One file per capability at `openspec/specs/<capability>/spec.md`, in the shape
   below. English, house voice (`DESIGN.md#writing`): plain declarative sentences, real names and
   paths, specific unrounded numbers, no emoji. The Purpose says what the capability is for in product
   words, and closes with one line saying it was reverse-engineered from shipped code, so a reader knows
   it was not negotiated ahead of the work. The archive's placeholder — `TBD - created by archiving
   change …` — is not a Purpose: 55 of those 56 specs still carry it, and that is the habit this skill
   exists not to inherit.
   - Done when: the file exists, every requirement uses SHALL, every requirement carries at least one
     scenario, and every scenario names something a person or a test can observe.
6. **Validate.** `openspec validate --specs --strict`.
   - Done when: it reports zero failures for every file written. Fix the file; do not explain the
     error away.
7. **Report.** One table — capability, file, requirement and scenario counts, the paths it is
   grounded in — then the *not a capability* list, then every disagreement from step 4, then what in
   scope got no spec and why.
   - Done when: the human can check any requirement against a path without asking where it came from.

## The format

```markdown
# <capability-id> Specification

## Purpose
What this capability is for, in one or two sentences of product language.

## Requirements
### Requirement: <what the system does, as a title>
The system SHALL … — real routes, real type names, real paths, and the ADR or retro entry that
constrains it where one does.

#### Scenario: <what is being watched>
- **WHEN** <trigger or precondition>
- **THEN** <observable outcome>
```

`## Purpose` and `## Requirements` are the only top-level sections — the corpus uses no others.
Requirements are `###`, scenarios `####`; a requirement with no scenario fails `--strict`, and two or
three scenarios per requirement is the norm (983 over 385). A scenario may pair a WHEN and a THEN in
one bullet when it is enumerating refusals — `- **WHEN** the caller has no session, **THEN** the
request is 401` — rather than spending a scenario on each.

## Discovery map

| Where | The capability there | What grounds it |
| --- | --- | --- |
| `src/modules/<Module>/…/Features/<Feature>/UseCases/*.cs` | one behaviour a caller invokes; several use cases collapse into one capability when one screen drives them | the route in `AddRoutes`, the `[Requires(...)]` access, the validator and handler in that same file |
| `…/Domain/`, `…/Persistence/` | the invariants and the module's own schema | the entity, its EF configuration, its migration |
| `src/shared/AiOrchestrator.BuildingBlocks/World/`, `src/shared/AiOrchestrator.Infrastructure/World.cs` | every reach outside this process, as a seam capability | the interface and its `AddWorld()` registration |
| `src/frontend/features/<area>/`, `src/frontend/app/routes.tsx` | one screen or one flow | the route, the components, `shared/http/queries.ts` and `contracts.ts`, the `shared/i18n/en.ts` keys |
| `src/frontend/shared/ui/`, `DESIGN.md` | the design contract as a policy capability | the kit exports and the tokens |
| `.ai/commands/aio/*.md`, `.ai/skills/*/SKILL.md` | the workflow loop | the command's steps and the label names in `.harness/config.json` |
| `.harness/config.json`, `.harness/commands.json` | a tunable the product reads | the key, plus the code that reads it |
| `src/root/AiOrchestrator.AppHost/AppHost.cs`, `CISteps/` | the gate and the inner loop | the declared `aspire do ci` steps |
| `src/shared/AiOrchestrator.ArchitectureAnalyzers/`, `src/tests/…ArchitectureTests` | the boundaries as a policy capability | the analyzer ids (MOD001–005, CQS001) and the tests |
| `docs/adr/*.md` | a decision constraining a capability | the ADR number, cited in the requirement |
| `src/tests/**` | the scenarios | test method names (`A_directory_with_no_git_is_added_and_holds_commands`) map onto WHEN/THEN |
| `docs/process/retro-log.md` | the why, and what an earlier behaviour was replaced by | the dated entry with its issue and PR number |

## Guardrails

- **Ask before touching an existing `spec.md`.** A spec is shared state; say which requirement you
  would add or change, and wait.
- **A gap is a finding, not a requirement.** Behaviour that is missing, half-wired or broken goes in
  the report so it can become an issue. Never write a SHALL for something you did not see work.
- **Never invent a tracker.** No Azure DevOps, Jira or work-item ids, and no frontmatter block for
  them: this repository tracks work in GitHub issues and nowhere else.
- **The retro log is dated, not current.** It is append-only and deliberately keeps entries that
  turned out wrong. Read it for reasoning and for what was superseded, never as evidence of today's
  behaviour.
