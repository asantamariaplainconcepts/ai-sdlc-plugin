# gate-results Specification

## ADDED Requirements

### Requirement: The refute-at-declaration wiring is proved

The orchestration that runs declared gates over a candidate — `RunDeclaredGates` and `ReadDeclaredGates` — SHALL be covered by at least one test each over a real declared file, not only its pure helpers. A declared gate whose executable resolves nowhere SHALL be refuted at declaration time (Missing, its sentence, no launch, no store row) through the orchestration itself, proving the wiring a silent refactor could otherwise break.

#### Scenario: A declared missing binary through the orchestration

- **WHEN** `RunDeclaredGates` runs over a repository declaring a gate whose executable does not exist
- **THEN** the gate's view says Missing with its not-findable sentence, no store row exists for it, and no process was ever asked to start

#### Scenario: The reading surface refuses the same gate

- **WHEN** `ReadDeclaredGates` reads the same declaration without running anything
- **THEN** the gate's view says Missing with the same sentence, and no store row exists
