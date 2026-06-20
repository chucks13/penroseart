Status: ready-for-agent

## Problem Statement

PenroseArt's Synced Mode sequencing has mixed together three different concerns: Phrase Window planning, Phase Boundary timing, and active A-to-B Transition execution. That mixed model made tailed transitions capable of affecting phase planning after the Transition's musical mark had already happened. From the wall author's perspective, a Transition with a Tail can finish its visual resolution and leave the Director's Phase Anchor pointed at the wrong boundary, causing the next transition to miss or fail to cue.

The deeper problem is not only the single tail bug. The current phase mechanisms are too complicated for the musical job. The wall needs to stay in Phase with live music, respect Phrase boundaries, optionally choose some interior Phase Boundaries, and then let each started Transition execute according to its Transition Settings. Transition progress, completion, and busy state are visual execution facts; they should not become music-structure evidence or drive Phrase Window planning.

## Solution

Replace the touched Synced Mode scheduling path with the clarified musical model:

- Track Phase provides the current Phrase Window.
- A Phase is the fixed 4-bar / 16-beat timing unit inside a Phrase Window.
- The Director derives Phase Boundaries from the current Phrase Window.
- The final phrase boundary is mandatory.
- Interior Phase Boundaries are optional and selected randomly per Phrase Window.
- The Director stores and advances through Selected Phase Boundaries, not Transition-local Impact Points.
- The Director selects the next Transition and reads that Transition's Settings to learn Runway and Tail.
- Runway and Tail imply the Transition-local Impact Point and tell the Director when to start the A-to-B move so the local mark hits the Selected Phase Boundary.
- Once the Director starts the Transition through the Mechanical Switcher, that A-to-B move is fire-and-forget visual execution. Transition progress, Tail completion, and Switcher busy state must not re-anchor, re-plan, or mark a new musical event.
- Transition Settings and the Settings Editor enforce that Runway plus Tail never exceeds 12 beats, leaving room inside the 16-beat minimum cadence without adding overlap machinery to the Director.

The outcome should be a cohesive cleanup, not a narrow patch. Code vocabulary, tests, runtime scheduling, and settings validation should all teach the same model so a future maintainer or agent does not rediscover the old mixed model.

## User Stories

1. As a PenroseArt wall author, I want tailed transitions to finish visually without changing the Director's phase planning, so that Tail motion cannot desynchronize the wall from the music.
2. As a PenroseArt wall author, I want the wall to keep selecting transitions from Phrase Windows and Phase Boundaries, so that the visuals remain musically timed.
3. As a PenroseArt wall author, I want every phrase boundary to remain a mandatory transition opportunity, so that major song-structure changes are respected.
4. As a PenroseArt wall author, I want interior Phase Boundaries to be optional, so that the wall does not transition every 16 beats by default.
5. As a PenroseArt wall author, I want the Director to choose how many interior Phase Boundaries to use based on the current Phrase Window, so that longer phrases can have more possible movement than shorter phrases.
6. As a PenroseArt wall author, I want the Director to choose interior Phase Boundaries once per Phrase Window, so that the wall does not churn its plan every frame.
7. As a PenroseArt wall author, I want repeated labels to be harmless, so that scheduling follows timing facts rather than label text.
8. As a PenroseArt wall author, I want Track Phase labels to remain useful display context, so that I can understand what song section is playing without making labels the scheduling identity.
9. As a PenroseArt wall author, I want the current OSC beat and Track Phase timing fields to define the current Phrase Window, so that the Director follows the real live stream.
10. As a PenroseArt wall author, I want missing Track Phase to remain Synced Mode when other live timing is present, so that the wall does not jump into Standalone Mode just because phrase data is unavailable.
11. As a PenroseArt wall author, I want Phase to mean the 4-bar / 16-beat timing unit, so that the code and docs match the musical language.
12. As a PenroseArt wall author, I want Phrase to mean a song-structure section, so that intro, buildup, breakdown, chorus, and drop are not confused with the 16-beat Phase grid.
13. As a PenroseArt wall author, I want Phase Boundary to mean the beat where a Phase starts or ends, so that transition targets are named after the musical grid.
14. As a PenroseArt wall author, I want Selected Phase Boundary to replace Selected Impact Beat in the scheduling vocabulary, so that the selected target belongs to music, not to a Transition.
15. As a PenroseArt wall author, I want Impact Point to remain local to a Transition, so that it only describes where the Transition's main visual hit happens.
16. As a PenroseArt wall author, I want Transition Settings to determine Runway and Tail, so that each Transition declares how it should be scheduled.
17. As a PenroseArt wall author, I want Runway plus Tail to imply the Transition-local Impact Point, so that the Director does not need a second musical meaning for Impact Point.
18. As a PenroseArt wall author, I want the Director to start a Transition early enough for its local mark to hit the Selected Phase Boundary, so that different Transition timings can still land musically.
19. As a PenroseArt wall author, I want a Transition with Runway 5 and Tail 1 to be valid conceptually when within the duration limit, so that settings can express different A-to-B timings without changing phrase logic.
20. As a PenroseArt wall author, I want a Transition's Tail to be visual resolution only, so that post-hit motion never becomes a scheduling event.
21. As a PenroseArt wall author, I want Transition Completion to mean B is fully established, so that it is not mistaken for the selected musical boundary.
22. As a PenroseArt wall author, I want started Transitions to be fire-and-forget, so that the Director can continue reasoning from music rather than active visual execution.
23. As a PenroseArt wall author, I want the 16-beat minimum cadence to prevent overlapping transitions, so that the Director does not need Switcher busy-state scheduling machinery.
24. As a PenroseArt wall author, I want Transition Duration to be capped at 12 beats, so that every Transition has room to finish inside the 16-beat cadence.
25. As a PenroseArt wall author, I want invalid Transition Duration to be blocked in authoring, so that the runtime can stay simple.
26. As a PenroseArt wall author, I want the Settings Editor to prevent Runway plus Tail greater than 12, so that invalid creative settings cannot be saved or applied.
27. As a PenroseArt wall author, I want the Director to assume Transition Settings are valid, so that live runtime logic is not cluttered with compensation for invalid authoring.
28. As a PenroseArt wall author, I want loops to usually be treated as still in Phase, so that a power-of-four loop does not make the wall think the music is wrong.
29. As a PenroseArt wall author, I want a loop inside the same Phrase Window to keep the same selected Phase Boundaries, so that the plan does not reroll just because playback repeated.
30. As a PenroseArt wall author, I want a loop rewind to move the Director cursor back to the next selected boundary after the current beat, so that the current pass can continue cleanly.
31. As a PenroseArt wall author, I want the same selected Phase Boundary to be usable again on a later loop pass, so that repeated music can repeat the same visual opportunity when cadence allows.
32. As a PenroseArt wall author, I want old absolute progress state to be discarded on a substantial rewind, so that stale beat numbers from a prior pass do not block the current pass.
33. As a PenroseArt wall author, I want loop handling to stay simple, so that rare loop edge cases do not make normal sequencing worse.
34. As a PenroseArt wall author, I want no loop-window scheduler, so that the wall follows the current OSC stream instead of speculative loop modeling.
35. As a PenroseArt wall author, I want no pass-ID translation machinery, so that a few imperfect loop moments are preferred over a fragile state machine.
36. As a PenroseArt wall author, I want substantial beat rewind handling to be self-correcting, so that the Director resumes from current Track Phase facts.
37. As a PenroseArt wall author, I want small beat jitter to remain ignored, so that minor out-of-order OSC updates do not constantly reset sequencing.
38. As a PenroseArt wall author, I want the Director to rebuild selected Phase Boundaries only when the Phrase Window changes, so that the plan is stable during normal playback.
39. As a PenroseArt wall author, I want a new Phrase Window to be recognized by timing identity, so that the Director reacts to actual structure changes.
40. As a PenroseArt wall author, I want phrase labels to avoid driving scheduling identity, so that repeated phrase names do not confuse the plan.
41. As a PenroseArt wall author, I want the red tailed-transition repro to become a permanent regression test, so that this bug does not return.
42. As a PenroseArt wall author, I want the next transition to cue normally after a tailed transition completes, so that Tail motion cannot stop the show.
43. As a PenroseArt wall author, I want the Director's Phase Anchor to remain tied to the selected Phase Boundary, so that Tail completion does not retarget the anchor.
44. As a PenroseArt wall author, I want the Director's cadence mark to be the selected musical boundary, so that completion beats do not become fake musical changes.
45. As a PenroseArt wall author, I want transition progress to remain useful for rendering, so that A-to-B motion stays smooth without becoming scheduling input.
46. As a PenroseArt wall author, I want Switcher execution to remain separate from Director decisions, so that what/when stays in the Director and drawing stays in the Mechanical Switcher.
47. As a PenroseArt wall author, I want the Mechanical Switcher to execute a started move, so that it does not interpret Track Phase or choose musical targets.
48. As a PenroseArt wall author, I want Standalone Mode behavior preserved, so that the wall still self-runs intentionally when no live OSC data is present.
49. As a PenroseArt wall author, I want Synced Mode behavior preserved when OSC is present, so that live musical timing remains preferred.
50. As a PenroseArt wall author, I want Drop, Fill, and Energy behavior left alone unless directly affected, so that this cleanup does not expand into unrelated signpost work.
51. As a PenroseArt wall author, I want current Transition Settings and Code Defaults to keep working, so that authoring data is not lost.
52. As a PenroseArt wall author, I want the Settings Editor to explain or constrain invalid timing, so that I can fix settings without reading runtime errors.
53. As a PenroseArt wall author, I want future agents to see the correct vocabulary in tests and code, so that they do not reintroduce Slot or Selected Impact Beat confusion.
54. As a maintainer, I want the highest practical test seam to exercise Director synced sequencing, so that tests prove caller-visible behavior rather than private implementation details.
55. As a maintainer, I want lower-level tests for Phrase Window planning only where they protect the model, so that the test suite remains focused.
56. As a maintainer, I want Transition Settings validation tested at the settings/editor seam, so that the Director does not become responsible for invalid authoring state.
57. As a maintainer, I want the implementation to remove old mixed-model naming from the touched path, so that technical debt is not left for future agents.
58. As a maintainer, I want the implementation to avoid new queues, preemption, snapshots, or defensive schedulers, so that the normal creative runtime stays direct.
59. As a maintainer, I want the implementation to fail plainly when a real contract is violated, so that errors are visible instead of hidden by fake success.
60. As a maintainer, I want validation to include the focused regression tests and relevant broader sequencing tests, so that both the bug and surrounding model stay covered.

## Implementation Decisions

- Keep the Director as the module that chooses what plays and which Selected Phase Boundary to target. Keep the Mechanical Switcher as the execution module for started A-to-B moves.
- Refine the Director/Switcher seam: the Director decides music-derived targets and starts the move; active Transition progress and completion must not feed back into Phrase Window, Phase Boundary, Phase Anchor, or cadence decisions.
- Use Track Phase timing fields plus the current beat to derive the Phrase Window. The scheduling identity is the exact timing identity of the Phrase Window: start beat, end beat, and length. Phrase labels remain display/context only.
- Build the Phrase Window's selected Phase Boundary plan when entering a new Phrase Window. Always include the final phrase boundary. Randomly choose zero or more eligible interior Phase Boundaries based on the current Phrase Window.
- Retain the current idea of a selected-boundary list per Phrase Window, but rename the domain/code vocabulary away from Slot and Selected Impact Beat. The selected music target is a Selected Phase Boundary.
- Replace fuzzy Phrase Window matching with exact timing identity for normal phrase changes. Do not let small tolerance checks hide real phrase changes or loop movement.
- On a same-window Loop or substantial beat rewind, keep the current Phrase Window's selected Phase Boundaries. Reset the plan cursor to the next selected boundary after the current beat and clear stale pass-local cadence state when it points into the old pass.
- Do not model loop bounds, loop windows, loop pass IDs, or historical playback passes. Loop handling should be a small reset based on current OSC facts.
- Preserve the rule that small beat backsteps are jitter/out-of-order updates and should not reset the plan.
- Treat the 16-beat minimum cadence as the normal overlap prevention mechanism. The Director should not need Switcher busy-state scheduling machinery to avoid overlapping transitions.
- Transition Settings declare Runway and Tail. Those values imply the Transition-local Impact Point and are exposed through the Transition's Director-facing declaration.
- The Director uses the selected Transition's declared Runway and Tail to compute when to start the A-to-B move so the Transition-local Impact Point hits the Selected Phase Boundary.
- Once the Director starts an A-to-B Transition through the Mechanical Switcher, the move is fire-and-forget visual execution according to Transition Settings. Tail completion is not a musical event.
- Do not mark cadence or Phase Anchor state from Transition Completion. Cadence should be marked from the Selected Phase Boundary being targeted, not from when B becomes fully established.
- Do not re-plan Phrase Windows or Selected Phase Boundaries because a Transition is in progress or completes.
- Enforce Transition Duration at the settings/editor layer. Runway plus Tail must never exceed 12 beats.
- The Director should assume Transition Settings are valid. It should not compress, overlap-check, reschedule, or compensate for invalid Transition Duration.
- The Settings Editor should prevent saving or applying invalid Runway plus Tail values. The settings contract should also reject invalid values where settings are converted into the Director-facing declaration.
- Keep Standalone Mode intentional and preserve its existing behavior unless it directly depends on the renamed Transition Settings contract.
- Keep Synced Mode active whenever live OSC data is present. Missing Track Phase while other live timing is present is not Standalone Mode.
- Use Serena rename/reference tooling for vocabulary cleanup so references remain consistent. Do not manually sweep names with broad text edits.
- Update runtime observability terms so displayed Director state refers to Selected Phase Boundaries rather than Selected Impact Beats where applicable.
- Update documentation and tests as part of the same slice. This PRD explicitly rejects a partial patch that fixes the red test while leaving the old mixed model in the touched path.
- This PRD refines the existing sequencing decision: the Director still directs and the Mechanical Switcher still executes, but started Transition execution must not remain a source of musical scheduling facts.

## Testing Decisions

- Good tests should verify caller-visible behavior through public seams. They should prove the Director's synced sequencing behavior from beat/Track Phase inputs through observable Director/Switcher state. They should not assert private fields, specific log strings, or that old names no longer exist.
- The highest-value test seam is Director synced sequencing. Tests should set live beat and Track Phase state, tick the Director, observe the planned/cued behavior, and verify that Tail completion does not alter musical scheduling.
- Keep and expand the tailed-transition regression test. It should prove that a Transition with Tail can complete visually while the next Selected Phase Boundary remains derived from Track Phase/Phrase Window timing.
- Add or update tests for Phrase Window planning only where they express the domain rule: derive Phase Boundaries from a Phrase Window, include the final phrase boundary, and choose interior Phase Boundaries as optional targets.
- Add loop/rewind behavior tests at the Director seam when practical. A same-window rewind should keep the selected Phase Boundary plan, reset the cursor to the next boundary after the current beat, and avoid stale cadence blocking.
- Add cadence tests for loop reset behavior only at the level needed to prove self-correction. Do not create a large loop scheduler test matrix.
- Add tests proving Transition Completion does not mark a new musical change beat or re-anchor the Director. The selected Phase Boundary should remain the cadence/musical marker.
- Add tests proving the Director can cue the next transition after a tailed transition completes.
- Add tests for Transition Settings validation: Runway plus Tail at or below 12 is valid, and greater than 12 is rejected or prevented at the settings contract/editor utility seam.
- Add tests for the Settings Editor validation behavior through extracted editor utility logic if direct editor UI testing would be brittle. Avoid heavy IMGUI layout tests.
- Update existing lower-level tests whose names or assertions use retired vocabulary. The behavior should stay the same where only names change.
- Existing prior art includes focused tests around PhaseClock, PhraseWindow, PhraseImpactPlan, ChangeCadence, TransitionBeatPlan, SyncedCueDecision, SyncedTransitionPlan, TransitionRepertoire, TransitionSettings, and BeatManager integration. Follow their focused Edit Mode style.
- Run the focused Director/phase/transition tests before broad validation. Then run Unity compile and the relevant broader Unity test suite.
- Test names and assertions should use the glossary terms: Phrase Window, Phase, Phase Boundary, Selected Phase Boundary, Impact Point, Runway, Tail, Transition Duration, Director, and Mechanical Switcher.

## Out of Scope

- Changing the RaveSystem OSC URL shape or payload order is out of scope. PenroseArt should consume the current OSC fields as they exist.
- Fixing the upstream RaveSystem active-state bug is out of scope. The current PenroseArt behavior should treat available Track Phase frames according to the current stream reality.
- Building a loop scheduler, loop-window model, pass-ID system, or historical playback tracker is out of scope.
- Handling every rare DJ edge case perfectly is out of scope. Simple self-correction is preferred over complex defensive machinery.
- Reworking player arbitration for multiple simultaneous DJ players is out of scope unless current on-air OSC fields already present one focused Track Phase stream.
- Changing Drop, Fill, Energy, Levels, or effect-directed Cue behavior is out of scope except where naming or tests must stay consistent.
- Replacing the Director/Switcher architecture is out of scope. This PRD refines their seam; it does not introduce a new sequencing subsystem.
- Adding queues, preemption, restart paths, transition snapshots, or fake schedulers is out of scope.
- Changing hardware output modes, serial protocol behavior, E1.31/UDP behavior, PixelReceiver behavior, or OSC message publishing is out of scope.
- Replacing the effect/transition reflection catalog with a manual registry is out of scope.
- Creating a new prefab-heavy Unity architecture or broad service layer is out of scope.
- Building heavy automated Editor UI tests is out of scope.
- Tuning unrelated transition visual settings is out of scope except where Settings Editor validation needs to enforce Runway plus Tail duration.

## Further Notes

- The current red repro shows a tailed transition leaving the Director's Phase Anchor at the weaker inferred grid boundary rather than the Track Phase-derived boundary after Tail completion.
- Current code already contains useful pieces of the desired model: Track Phase can derive a Phrase Window, a phrase plan can choose interior targets and include the final boundary, and Transition Settings already expose Runway and Tail through a Director-facing declaration.
- Current code also contains the old mixed model that this PRD should remove from the touched path: fuzzy Phrase Window matching, Slot/Selected Impact Beat vocabulary, Director gating on Switcher transition state, and transition completion marking cadence.
- Simplicity here means the direct musical model, not the smallest diff. A patch that only changes one condition and leaves the mixed model in place is not the desired outcome.
- If implementation reveals that the existing sequencing ADR needs wording updates, update it separately using the project's ADR format rather than burying the decision only in code.
