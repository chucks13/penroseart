# Editor surfaces never originate runtime state

Status: accepted

Dashboards, custom inspectors, property drawers, and other Unity Editor surfaces are downstream mirrors: they display what the runtime core is and are rewritten to follow whatever the core becomes. No runtime member, state, or feature may be created, preserved, or shaped because an editor surface wants to show or control it — an editor-surface need is never a design driver for the core. The Effect **Hold** is a Unity Editor observation affordance: it freezes one effect on the wall through `Controller.heldEffect`, honored by the Director. It does not justify waveform substitution or other inspection state inside musical providers.

## Amendment — 2026-07-13: inspection scope corrected

The previously accepted Waveform Hold is removed. It put editor observation policy inside `Waveforms`, changed every caller's acquisition and evaluation semantics, and forced Routine construction to bypass that substitution. Hunter clarified that Hold means keeping an Effect running so it can be watched and tuned; it is not a musical runtime behavior. The historical provenance below remains useful, but its conclusion that Waveform Hold was a sanctioned second runtime feature is superseded.

## Why this is an ADR and not just an instruction

The agents-file rule ("custom property drawers and inspectors are downstream debug views; runtime code must not be preserved just to keep them fed") was violated in the most instructive way possible: an agent built "two-way wall control" into a dashboard drawer (`ffcdb107`), a later agent extracted that editor-born policy *into* `BeatManager` and coined a domain term for it (`9f1a5c29`, which also wrote the "Wall Variant Lock" glossary entry), and a still later design session carried it forward as settled semantics. The result was public runtime state (`activeVariant`, `LockVariant`, `ReleaseToAuto`, `ResolveDisplayVariant`) whose only non-test caller was the editor control itself (`Assets/Editor/Rhythm/WallVariantControl.cs`) — a runtime "feature" no effect ever consumed and no human ever decided to have. On discovery (2026-07-11, beat-data-interface effort) its existence was put to the human for the first time, and Hunter initially kept it as an inspection affordance — renamed the **Waveform Hold**, the proposed second use of Hold. The 2026-07-13 amendment supersedes that conclusion after its cross-cutting runtime cost became clear. The direction-of-authority lesson remains: **runtime → editor, never editor → runtime.**

## Consequences

- Deleting or reshaping a runtime member never requires preserving anything for an editor surface; the surface is updated to follow, or it breaks and is fixed.
- A proposal that adds runtime state and whose only consumer is an editor surface is rejected by construction.
- Wanting a new operator control starts as a runtime design conversation ("should the wall have this behavior?"), not as an editor widget that needs a backing field.
