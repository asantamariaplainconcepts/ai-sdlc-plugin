## MODIFIED Requirements

### Requirement: Declared but unimplemented draws disabled

A declared step whose panel is not implemented SHALL draw disabled with the words "not implemented". It SHALL NOT disappear and SHALL NOT fake empty content. Which step keys have panels is a known set in code; the declared file is the source of what draws. The known set for this cut is `proposal`, `code` and `tests`; every other declared key draws disabled.

#### Scenario: Step outside the known set

- **WHEN** `review.json` declares a step whose key is not `proposal`, `code` or `tests`
- **THEN** the step draws numbered and disabled, stating "not implemented", and cannot be opened

#### Scenario: Implemented step opens its panel

- **WHEN** a declared step's key is `proposal`, `code` or `tests`
- **THEN** the step draws enabled and opens its panel when chosen

#### Scenario: The tests step opens its run panel

- **WHEN** the rail draws the tests step declared by this repository
- **THEN** it draws enabled and opens the run launcher panel when chosen
