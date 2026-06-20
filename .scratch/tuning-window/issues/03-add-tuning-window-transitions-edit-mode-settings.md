# Add Tuning Window Transitions tab for Edit Mode settings

Status: ready-for-agent

## Parent

`.scratch/tuning-window/PRD.md`

## What to build

Add the standalone dockable Tuning Window opened from the Unity Window menu. The window should present Transitions and Effects tabs, with the Transitions tab implemented first and the Effects tab shaped as a placeholder/skeleton for the same future pattern. In Edit Mode, the Transitions tab lists the Transition catalog, lets a user select a Transition, shows that Transition's saved settings, and offers Restore Defaults.

This slice should make settings authoring usable without entering Play Mode. It should use the existing reflection catalog rather than a manual registry and should edit the real saved settings assets through Unity serialization so ordinary asset persistence, dirty handling, and restore behavior work correctly.

## Acceptance criteria

- [ ] The Tuning Window opens from `Window > Penrose > Tuning` and behaves as a normal dockable Editor window.
- [ ] The window has Transitions and Effects tabs.
- [ ] The Transitions tab lists all current catalog Transitions in Edit Mode.
- [ ] Selecting a Transition in Edit Mode shows its saved Transition Settings.
- [ ] Normal setting edits modify the real saved settings asset through Unity serialization.
- [ ] Restore Defaults is available for the selected Transition and restores the full saved settings from Code Defaults.
- [ ] Missing settings assets are created or made available through the normal editor workflow without requiring users to hunt asset files.
- [ ] The Effects tab exists only as a future-shaped skeleton and does not implement full Effect Settings.
- [ ] Unity compile succeeds; automated tests avoid brittle assertions about IMGUI layout details.

## Blocked by

- `.scratch/tuning-window/issues/02-persist-complete-transition-settings-from-code-defaults.md`
