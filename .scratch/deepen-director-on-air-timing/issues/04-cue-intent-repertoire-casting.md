# Connect TimingFrame to cue intent and Repertoire-aware casting

Status: ready-for-agent

## What to build

Use the Timing Frame as the input to a clearer cue/casting path. The Director should no longer compute musical cue intent in scattered private methods or compute Drop preference only to discard it. A cue/casting decision should combine the current Timing Frame, selected Transition timing declaration, live phrase-event data such as Drop/Fill, current staged choices, and Performer Repertoire into a small Director-facing cue intent.

This slice should be narrow but complete: a Drop-aligned selected Phase Boundary can produce cue intent, Repertoire-aware deck selection can prefer a suitable next Performer when one exists, the Director can stage/use that choice without micromanaging effect internals, and tests prove the behavior through the cue/casting seam plus Director integration. Effects still own how they express Fill, Drop, Energy, and Levels; the Director casts or cues, not configures pixels.

Use Matt/codebase-design as the design gate: cue intent should be a deep module or evolved existing cue seam, not another pile of Director helper methods. Use polish as part of the slice: remove the obsolete compute-and-discard preference path and keep the interface small enough that future Fill/Energy behavior can use it without another timing redesign.

## Acceptance criteria

- [ ] Cue/casting logic consumes a Timing Frame rather than raw Track Phase or duplicated Phase Anchor fields.
- [ ] Drop-aligned timing can produce a cue intent that prefers Performers advertising Drop-capable Repertoire when such Performers are available.
- [ ] If no preferred Performer is available, deck selection remains intentional and does not fail or invent fake capabilities.
- [ ] The existing staged Next Effect / Next Transition behavior remains correct for Hold Selected and one-shot manual steering.
- [ ] Transition Repertoire remains the source of Runway/Tail timing; cue/casting does not duplicate TransitionBeatPlan arithmetic.
- [ ] Effect Repertoire remains a Performer declaration. The Director does not set effect internals or issue pixel-level commands.
- [ ] Existing EffectDeckSelection behavior is reused or deliberately deepened; deck preference rules are not duplicated across callers.
- [ ] Tests cover cue intent through the cue/casting seam rather than private Director methods.
- [ ] Director integration tests prove the cue intent affects staged/started choices in observable status or Switcher behavior.
- [ ] The obsolete Drop-preference-computed-then-discarded path is removed.
- [ ] No service layer, event bus, ScriptableObject registry, prefab architecture, or speculative adapter is introduced.
- [ ] The scoped diff is polished after behavior is green: cue, casting, Repertoire, Performer, Drop, Fill, Runway, Tail, and Impact Point vocabulary is consistent.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md`
- `.scratch/deepen-director-on-air-timing/issues/02-loop-beat-rewind-self-correction.md`
- `.scratch/deepen-director-on-air-timing/issues/03-coast-and-reanchor-recovery.md`
