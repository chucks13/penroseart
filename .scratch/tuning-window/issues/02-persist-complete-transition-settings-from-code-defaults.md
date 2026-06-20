# Persist complete Transition Settings from Code Defaults

Status: ready-for-agent

## Parent

`.scratch/tuning-window/PRD.md`

## What to build

Create the full Transition Settings contract for all current Transitions. Each Transition should keep its human-tweakable Code Defaults together near the top of its source, and those defaults should cover the complete authoring surface for that Transition: Transition Repertoire, timing, visual/default knobs, and external blend defaults where they are artistic controls. Saved per-transition settings assets become the editable authoring copy of those defaults.

Runtime should read saved Transition Settings when available and fall back truthfully to Code Defaults when a settings asset is absent. Editor-side support should create missing settings assets from Code Defaults and restore a selected Transition's entire saved settings back to its Code Defaults. This is a complete current-Transitions pass, not a repertoire-only migration.

## Acceptance criteria

- [ ] Every current Transition has its human-tweakable Code Defaults grouped near the top of its source.
- [ ] Every current Transition has saved Transition Settings coverage for all relevant human-tweakable defaults, not only Repertoire.
- [ ] Algorithm invariants and non-creative implementation constants remain in code rather than becoming settings.
- [ ] Transition Repertoire, timing, visual/default knobs, and relevant external blend defaults are represented in saved settings where they are artistic controls.
- [ ] Missing settings assets can be auto-created from Code Defaults without a manual registry.
- [ ] Restore Defaults copies the full set of Code Defaults for a Transition back into its saved settings.
- [ ] Runtime reads saved settings when available and falls back to Code Defaults when absent.
- [ ] Play Mode edits are not defeated by freezing effective settings values at catalog setup.
- [ ] Focused tests cover settings creation from Code Defaults, effective runtime settings, and full Restore Defaults behavior where practical.

## Blocked by

None - can start immediately
