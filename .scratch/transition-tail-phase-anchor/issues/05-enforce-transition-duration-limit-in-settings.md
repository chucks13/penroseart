# Enforce Transition Duration limits at the settings/editor seam

Status: ready-for-agent

## What to build

Enforce the authoring contract that a Transition's Runway plus Tail must never exceed 12 beats. The Settings and Settings Editor path should prevent invalid creative timing from being saved, applied, or converted into the Director-facing Transition declaration. Runtime Director logic should be able to assume Transition Settings are valid and should not grow compensation, compression, overlap checks, or rescheduling logic for invalid duration.

Runway and Tail remain the authored timing language; together they imply Transition Duration and the Transition-local Impact Point.

## Acceptance criteria

- [ ] Runway plus Tail at or below 12 beats is accepted, including non-default combinations such as Runway 5 / Tail 1.
- [ ] Runway plus Tail greater than 12 beats is rejected or prevented before it reaches the Director-facing Transition declaration.
- [ ] The Settings Editor explains or constrains invalid timing so invalid values cannot be saved or applied silently.
- [ ] Tests cover both the settings contract and extracted editor validation behavior without relying on brittle IMGUI layout tests.
- [ ] Existing Transition Settings and Code Defaults continue to work, and authoring data is not discarded.
- [ ] The Director does not add compensation logic for invalid Transition Duration.

## Blocked by

None - can start immediately
