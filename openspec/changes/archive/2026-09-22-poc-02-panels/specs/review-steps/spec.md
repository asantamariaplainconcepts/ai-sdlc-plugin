# review-steps Specification

## MODIFIED Requirements

### Requirement: Declared but unimplemented draws disabled

A declared step whose panel is not implemented SHALL draw disabled with the words "not implemented". It SHALL NOT disappear and SHALL NOT fake empty content. Which step keys have panels is a known set in code; the declared file is the source of what draws. The known set for this cut is `proposal` and `code`; every other declared key draws disabled.

#### Scenario: Step outside the known set

- **WHEN** `review.json` declares a step whose key is not `proposal` or `code`
- **THEN** the step draws numbered and disabled, stating "not implemented", and cannot be opened

#### Scenario: Implemented step opens its panel

- **WHEN** a declared step's key is `proposal` or `code`
- **THEN** the step draws enabled and opens its panel when chosen

#### Scenario: The tests step stays unimplemented

- **WHEN** the rail draws the tests step declared by this repository
- **THEN** it draws disabled with the words "not implemented", the same as any other key outside the set
