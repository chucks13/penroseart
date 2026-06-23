Status: accepted

## Problem Statement

PenroseArt's Director is supposed to be the one decision layer that chooses what plays on the wall and when it changes. The Mechanical Switcher should only execute those choices. The current Director/Switcher architecture is pointed in the right direction, but the Director still carries too much low-level musical interpretation and too much future cue/casting work in one place.

From the wall author's perspective, this makes Synced Mode harder to trust and harder to evolve. The Director needs to stay aligned to live RaveSystem timing, keep Phase distinct from Phrase, preserve a selected Phase Boundary plan through loops and data gaps, and eventually cast Effects and Transitions based on Repertoire. Today those ideas are scattered across Director fields, raw rhythm-query reads, helper calls, status projection, cue decisions, logging, and staging. A future cleanup of cue intent or Repertoire would have to revisit the same knot unless the timing model is cut into the right shape now.

The team does not need compatibility shims, staged migration machinery, or a low-risk minimal patch. The desired outcome is a hard-cut refactor to the right internal patterns for this small creative runtime: one clear Director-facing timing seam first, then cue/casting and status cleanup on top of that seam, without preserving old internal pathways just because they existed.

## Solution

Reshape the Director around a deeper in-process on-air timing module and use that as the first durable piece of the final Director architecture.

The Director remains the small public directing module. It owns the high-level loop: Standalone Mode, Synced Mode, Hold, staged Next Effect, staged Next Transition, and issuing stage-directed commands to the Mechanical Switcher. It should not interpret raw Track Phase fields, own selected Phase Boundary cursor mechanics, or carry loop/coast/re-anchor policy directly.

Introduce an on-air timing module whose interface returns a single timing frame for the current synced tick. That timing frame should tell the Director the current beat, whether a Phase Anchor is available, the selected Phase Boundary being targeted, confidence, source/reason, Phase reading, Phrase Window identity when available, and whether the frame represents normal Track Phase, coasting, a substantial rewind, or a re-anchor. The implementation can reuse the existing PhaseClock, PhraseWindow, SelectedPhaseBoundaryPlan, ChangeCadence, and related pure modules, but those details should sit behind the timing seam rather than being reassembled by the Director.

This is not a compatibility extraction. The old Director-owned timing fields and methods should be removed from the Director as the timing module takes ownership. The Director should consume the timing frame and proceed to cue decisions, staging, status projection, and Switcher commands.

After the timing seam lands, the same model should support the next Director deepening pass: cue intent and casting. Cue planning should consume timing frames, transition timing declarations, live phrase events such as Drop and Fill, and Performer Repertoire to produce a small cue direction that the Director inserts or updates in the Switcher. The Switcher then owns Loaded Cue mutability, Lock Point, arming, and transition execution. That follow-on work should not require redesigning on-air timing again.

Documentation should be refreshed after the code shape lands so runtime architecture, ADR vocabulary, tests, and code all describe the same model: Director decides, On-Air Timing interprets live musical structure, Cue Planning/Casting chooses the move, and the Mechanical Switcher executes.

## User Stories

1. As a PenroseArt wall author, I want the Director to stay focused on deciding what plays and when it changes, so that the main sequencing code reads like show direction rather than signal parsing.
2. As a PenroseArt wall author, I want Synced Mode timing interpretation to live behind one clear seam, so that live musical timing bugs are fixed in one place.
3. As a PenroseArt wall author, I want Phase to consistently mean the fixed 16-beat / 4-bar unit, so that transition cadence is not confused with song sections.
4. As a PenroseArt wall author, I want Phrase to consistently mean a song-structure window, so that Track Phase data is interpreted as musical structure rather than a clock name.
5. As a PenroseArt wall author, I want Track Phase fields to be interpreted by the timing module, so that the Director does not know raw OSC query details.
6. As a PenroseArt wall author, I want the current Phrase Window to be derived from on-air timing, so that the wall follows the actual current song section.
7. As a PenroseArt wall author, I want a Phase Window's selected Phase Boundaries to be planned once per Phrase Window, so that the wall does not reroll musical targets every frame.
8. As a PenroseArt wall author, I want the final phrase boundary to remain mandatory, so that major song-structure changes remain visible opportunities for movement.
9. As a PenroseArt wall author, I want interior Phase Boundaries to remain optional, so that the wall can breathe instead of changing every 16 beats.
10. As a PenroseArt wall author, I want the Director to receive the currently selected Phase Boundary, so that it can schedule transitions without owning the cursor details.
11. As a PenroseArt wall author, I want the selected Phase Boundary cursor to live with the timing interpretation, so that loop and rewind behavior is local to the musical grid.
12. As a PenroseArt wall author, I want a same-window Loop rewind to keep the selected Phase Boundary plan, so that repeated music can repeat the same visual opportunities.
13. As a PenroseArt wall author, I want a same-window Loop rewind to move the cursor back to the next selected Phase Boundary after the current beat, so that the current pass self-corrects.
14. As a PenroseArt wall author, I want substantial beat rewinds to clear stale pass-local cue state where appropriate, so that old absolute beat positions do not block the new pass.
15. As a PenroseArt wall author, I want small one- or two-beat backsteps to remain treated as jitter, so that ordinary OSC wobble does not reset the show plan.
16. As a PenroseArt wall author, I want Track Phase disappearance to coast on the last known Phase Anchor, so that temporary data gaps do not make the wall snap to arbitrary timing.
17. As a PenroseArt wall author, I want coasting to remain Synced Mode behavior, so that missing Track Phase is not mistaken for Standalone Mode when other live timing is present.
18. As a PenroseArt wall author, I want the wall to re-anchor when fresh Track Phase returns, so that it can recover from gaps, startup ambiguity, or changed song position.
19. As a PenroseArt wall author, I want the timing frame to say why a target was chosen, so that debugging can distinguish Track Phase, selected Phase Boundary, grid, coast, re-anchor, and unlocked states.
20. As a PenroseArt wall author, I want Phase Confidence to remain visible, so that stronger structural evidence is distinguishable from weaker beat-grid evidence.
21. As a PenroseArt wall author, I want the Director status to report musical timing facts from the timing frame, so that HUD and inspector readouts match the actual decision model.
22. As a PenroseArt wall author, I want Standalone Mode to remain intentional and simple, so that the wall still self-runs when no live OSC source is present.
23. As a PenroseArt wall author, I want Synced Mode to remain preferred whenever live OSC timing is present, so that the wall follows the DJ when possible.
24. As a PenroseArt wall author, I want Hold to remain an inspection freeze, so that I can stop the Director and inspect an Effect without confusing sequencing state.
25. As a PenroseArt wall author, I want Show Now to remain an explicit override, so that manual inspection can still replace whatever is mechanically rendering.
26. As a PenroseArt wall author, I want the Mechanical Switcher to remain execution-only, so that timing interpretation does not leak into transition rendering.
27. As a PenroseArt wall author, I want active transition progress and Tail completion to stay out of timing interpretation, so that visual execution does not become musical evidence.
28. As a PenroseArt wall author, I want the Switcher to use the inserted cue's Transition Runway to decide when to lock and start, so that the Transition-local Impact Point lands on the selected Cue Mark without the Director owning execution timing.
29. As a PenroseArt wall author, I want Tail to remain visual resolution only, so that post-impact motion does not change the next musical target.
30. As a PenroseArt wall author, I want transition timing declarations to stay reusable by cue planning, so that timing and casting can be improved without duplicating rules.
31. As a PenroseArt wall author, I want Drop preference to eventually affect casting, so that the Director can choose Performers that can express a Drop.
32. As a PenroseArt wall author, I want Fill preference to eventually affect expression or temporary mixing, so that fills can be highlighted without changing the main Effect when appropriate.
33. As a PenroseArt wall author, I want Energy and Levels to remain Effect-expression inputs, so that the Director does not micromanage pixel behavior.
34. As a PenroseArt wall author, I want Performer Repertoire to remain a declaration from the Performer, so that the Director casts from capabilities instead of configuring internals.
35. As a PenroseArt wall author, I want cue intent to be a small value derived from timing and live events, so that the Director can choose a stage move without spreading event logic everywhere.
36. As a PenroseArt wall author, I want Next Effect and Next Transition to keep representing the next musical move, so that tuning tools show what is coming.
37. As a PenroseArt wall author, I want Hold Selected behavior to keep working after the refactor, so that live tuning can keep a chosen Performer staged.
38. As a PenroseArt wall author, I want one-shot manual steering to keep working after the refactor, so that selected choices can be consumed and normal staging resumes.
39. As a PenroseArt wall author, I want the Director's public interface to stay small, so that the rest of the runtime has one obvious way to drive sequencing.
40. As a PenroseArt wall author, I want the Director implementation to be internally deeper, so that timing, cue planning, casting, and status are not tangled together.
41. As a PenroseArt wall author, I want the timing module interface to be stable enough for cue planning, so that the team does not need to redo the timing seam in the next pass.
42. As a PenroseArt wall author, I want old internal timing pathways removed, so that future work does not accidentally choose the stale path.
43. As a PenroseArt wall author, I want no compatibility bridge between old and new Director timing internals, so that this small project does not carry migration debt.
44. As a PenroseArt wall author, I want tests to name the musical concepts directly, so that future agents do not reintroduce Phase/Phrase confusion.
45. As a PenroseArt wall author, I want loop behavior to be tested cheaply at the timing seam, so that confidence does not require heavy Unity GameObject setup.
46. As a PenroseArt wall author, I want coasting behavior to be tested cheaply at the timing seam, so that intermittent Track Phase gaps are covered.
47. As a PenroseArt wall author, I want re-anchor behavior to be tested cheaply at the timing seam, so that recovery from fresh phrase data is covered.
48. As a PenroseArt wall author, I want Director-level tests to prove the Director consumes timing frames correctly, so that the integration remains covered without private-field assertions.
49. As a PenroseArt wall author, I want Controller-level tests to remain wiring tests, so that they do not become the only way to verify timing policy.
50. As a maintainer, I want PhaseClock to remain a focused pure module, so that Phase reading behavior stays independently understandable.
51. As a maintainer, I want PhraseWindow to remain a focused pure module, so that Phrase Window derivation and boundaries stay independently understandable.
52. As a maintainer, I want SelectedPhaseBoundaryPlan to remain a focused pure module or be absorbed only if the new timing module gives more leverage, so that useful tested behavior is not flattened.
53. As a maintainer, I want SyncedCueIntent to remain focused on cue timing and casting, so that cue decisions do not drift back into the Director body.
54. As a maintainer, I want ChangeCadence to remain a clear rule, so that minimum-change cadence is not scattered across timing, cue, and status code.
55. As a maintainer, I want EffectDeckSelection to keep owning deck preference behavior, so that casting work does not duplicate deck selection rules.
56. As a maintainer, I want the deletion test to pass for the timing module, so that deleting it would force the same complexity back into multiple Director methods.
57. As a maintainer, I want the timing module to be a deep module rather than a pass-through wrapper, so that it hides meaningful behavior behind a small interface.
58. As a maintainer, I want only earned seams, so that this refactor does not introduce speculative adapters for a single in-process runtime.
59. As a maintainer, I want the module names to use project domain language, so that the code remains AI-navigable and musician-readable.
60. As a maintainer, I want test seams to match caller seams, so that tests exercise behavior the same way the Director will use it.
61. As a maintainer, I want no service layer, dependency-injection framework, or event bus, so that the creative runtime stays direct.
62. As a maintainer, I want no prefab-heavy Unity architecture, so that the core plain-C# runtime remains the product.
63. As a maintainer, I want no new OSC protocol behavior, so that this refactor stays inside PenroseArt's interpretation of existing data.
64. As a maintainer, I want no hardware output changes, so that serial and preview behavior are not affected by Director cleanup.
65. As a maintainer, I want focused validation for timing and Director behavior first, so that failures identify the seam that broke.
66. As a maintainer, I want broad Unity validation after the focused slice, so that runtime integration remains safe.
67. As a maintainer, I want runtime architecture docs updated after the code shape lands, so that future work starts from the current Director model.
68. As a maintainer, I want ADR vocabulary respected, so that the Director/Switcher decision remains durable.
69. As a maintainer, I want the implementation to replace the wrong shape, so that simplicity means the cleanest sound design rather than the smallest diff.
70. As a maintainer, I want the final diff polished after the hard-cut refactor, so that the new shape is clear and not just moved code.

## Implementation Decisions

- Make this a hard-cut internal refactor. Do not preserve old Director-owned timing pathways behind compatibility wrappers, duplicate fields, or migration-style shims.
- Keep the Director as the single external directing module. Its interface should remain small: tick the Director, issue explicit overrides, stage next choices, expose read-only status, and command the Mechanical Switcher.
- Introduce one new primary seam for on-air timing interpretation. The ideal number of new seams for the first slice is one.
- The on-air timing module should accept a small snapshot of live timing inputs and the minimal decision state it legitimately needs, rather than taking the whole Controller as its interface.
- The on-air timing module should return a timing frame. The timing frame is the Director-facing contract for Synced Mode timing.
- The timing frame should include current beat, Phase reading, Phase Anchor availability, Phase Confidence, selected Phase Boundary, beats until selected boundary, target source/reason, Phrase Window identity when available, and state markers for coasting, rewind, re-anchor, and unlocked cases.
- The timing module owns raw Track Phase interpretation, PhaseInput construction, PhaseClock resolution, PhraseWindow derivation, selected Phase Boundary plan identity, selected boundary cursor advancement, same-window rewind handling, coasting, and re-anchor policy.
- Existing pure modules should be reused behind the timing seam where they still earn their keep. The refactor should not flatten useful modules just to reduce file count.
- If an existing pure module becomes a shallow pass-through after the new timing module is built, it may be absorbed as part of the same hard-cut cleanup. Preserve behavior, not arbitrary file boundaries.
- The Director should not read raw Track Phase fields after the timing seam is in place.
- The Director should not own selected boundary arrays, phrase identity fields, or boundary cursor indexes after the timing seam is in place.
- The Director should not own beat rewind detection except as part of feeding the timing module the current synced beat sequence, if the final interface requires that.
- The Director should consume the timing frame to build status, configure the next cue direction, and insert/update that cue in the Switcher while the Switcher still reports it is mutable.
- Timing source/reason strings or enums should be domain-facing and stable enough for tests and status. Prefer a small closed vocabulary over ad hoc log text.
- The timing frame should be designed so future cue planning can consume it directly. Do not make the first slice return only the fields needed by today's exact Director implementation if cue/casting will immediately need richer timing context.
- Keep the Mechanical Switcher execution-only. The timing module must not inspect Switcher progress, completion, or busy state.
- Keep Transition Repertoire as the source of Runway and Tail for transition timing. The timing module chooses the Cue Mark; the Director combines that target with Performer/Transition casting to form cue direction, while the Switcher derives Lock Point, start, impact, and completion from the selected Transition Repertoire.
- Keep the 16-beat minimum cadence rule explicit and shared through the existing cadence module or a small timing/cue input. Do not duplicate cadence arithmetic in multiple Director methods.
- Keep Standalone Mode separate from on-air timing. Standalone Mode can continue to use the self-running timer and staged choices without routing through the Synced Mode timing frame.
- Keep Hold as a Director suspension/inspection concept. Holding should not cause the timing module to fake new anchors or stage choices.
- Keep Show Now as an explicit override. It may reset timing-related planning because it is a manual/developer action, not ordinary musical scheduling.
- Defer cue/casting implementation until the timing frame is established, but shape the timing frame so cue/casting does not require another timing redesign.
- The follow-on cue/casting module should consume timing frame, live phrase-event data, current staged choices, transition timing declarations, and Performer Repertoire to produce a cue direction for the Switcher-held lifecycle.
- Cue intent should express what the Director wants to do, not pixel-level instructions or effect internals.
- Drop preference currently being computed and discarded should become a cue/casting concern in the follow-on pass, not a one-off patch inside transition start.
- Effect expression remains with Effects. The Director may cast or send a cue, but Effects decide how to express Fill, Drop, Energy, or Levels when their Repertoire says they can.
- Do not introduce adapters for hypothetical alternate timing sources. There is one in-process runtime and one current interpretation path.
- Do not introduce service-layer, event-bus, dependency-injection, or framework machinery.
- Keep code and tests using the project glossary: Director, Mechanical Switcher, On-Air Timing, Timing Frame, Phase, Phrase Window, Phase Boundary, Selected Phase Boundary, Phase Anchor, Phase Lock, Phase Confidence, Coast, Re-anchor, Loop, Beat Rewind, Runway, Tail, Impact Point, Cue, Repertoire, and Performer.
- Refresh runtime architecture documentation after the implementation lands so the docs do not keep reviving the old timer-transition model.

## Testing Decisions

- Good tests verify caller-visible behavior through the highest useful seam. They should not assert private fields, exact log text, or internal collection indexes unless those are part of the module's interface.
- The primary new testing seam is the on-air timing module. It should be testable without Unity GameObjects, Controller singleton reflection, Switcher rendering, or Unity time.
- On-air timing tests should feed timing snapshots and observe timing frames. They should prove musical interpretation behavior directly.
- Add tests proving Track Phase data produces a structural timing frame with the expected Phrase Window identity and selected Phase Boundary.
- Add tests proving a same-window substantial rewind keeps the selected Phase Boundary plan and rewinds the cursor to the next selected boundary after the current beat.
- Add tests proving the same selected Phase Boundary can become eligible again on a later loop pass when cadence allows.
- Add tests proving small beat backsteps are treated as jitter and do not reset the selected boundary plan.
- Add tests proving Track Phase disappearance coasts on the last known anchor when a prior anchor exists.
- Add tests proving no prior anchor plus unavailable Track Phase produces an unlocked timing frame rather than a fake target.
- Add tests proving fresh Track Phase after coasting re-anchors the timing frame to structural phrase data.
- Add tests proving a contradictory fresh Phrase Window replaces the coasted anchor instead of layering anchors.
- Add tests proving source/reason values distinguish structural Track Phase, selected Phase Boundary, phase-clock grid, coasting, re-anchor, rewind, and unlocked cases where relevant.
- Preserve existing PhaseClock tests for low-level Phase reading behavior unless the new timing module absorbs that interface entirely.
- Preserve existing PhraseWindow tests for Phrase Window derivation and Phase Boundary enumeration unless the new timing module absorbs that interface entirely.
- Preserve or update SelectedPhaseBoundaryPlan tests where they still express the reusable behavior of selecting interior boundaries and always including the phrase boundary.
- Keep Director synced tests, but move loop/coast/re-anchor policy coverage down to the timing seam where possible.
- Director tests should prove the Director consumes timing frames correctly: status reflects timing, cue decisions use the selected Cue Mark, and the Director inserts/updates the expected cue direction without computing Switcher lock/start/progress timing.
- Existing Director staging tests remain prior art for Next Effect / Next Transition behavior and should be updated only where the refactor changes how timing/cue inputs reach staging.
- Existing Switcher tests remain the right seam for mechanical execution. Do not test active transition progress through the timing module.
- Existing TransitionBeatPlan and SyncedCueIntent tests remain useful for cue timing and casting unless they are deliberately evolved into a higher cue-planning seam.
- When cue/casting work starts, add tests at the cue-planning seam rather than testing Drop preference by reading private Director methods.
- Test names should use project domain vocabulary and avoid generic words that hide Phase/Phrase distinctions.
- Random selected-boundary behavior should be tested with deterministic random delegates or controlled seeds through a public test seam, not by depending on incidental global random state where avoidable.
- Run the focused timing, Director, Switcher, transition timing, transition settings, and BeatManager integration test slice after implementation.
- Run the documented Unity compile/test wrapper after the focused tests.
- Validation should include a polish pass over the scoped diff after behavior is green.

## Out of Scope

- Changing RaveSystem OSC message shape, ports, URL paths, or payload semantics.
- Changing the generic OSC library boundary or adding Penrose-specific policy to generic OSC code.
- Changing serial output, S2 Mini protocol behavior, UDP/E1.31 behavior, PixelReceiver behavior, drum overlay behavior, camera overlay behavior, telnet behavior, or hardware output modes.
- Replacing the Director/Switcher architecture with a new sequencing subsystem.
- Replacing reflection discovery for Effects, Transitions, Mixers, or Blenders.
- Adding prefab-heavy Unity composition, ScriptableObject registries, dependency-injection frameworks, event buses, or service layers.
- Building alternate timing-source adapters or a general scheduler framework.
- Building loop windows, pass IDs, playback history, transport state machines, or speculative loop plans.
- Guaranteeing perfect handling for every possible DJ transport edge case. The goal is simple self-correction from current on-air facts.
- Retuning visual transition algorithms or effect visuals.
- Implementing full cue/casting/Repertoire behavior in the first timing slice. The timing frame should support it, but the first implementation should harden timing ownership first.
- Making Effects advertise new Repertoire as part of the timing slice, unless a tiny test Performer needs declared Repertoire as a fixture.
- Changing Transition Settings contracts except where timing/cue tests need to keep existing Runway/Tail behavior aligned.
- Heavy Editor UI testing.
- Play Mode validation unless explicitly approved, because Play Mode can touch local ports, hardware, and generated image lists.

## Further Notes

- This PRD intentionally treats "small project" as permission to cut to the right shape, not as permission to keep bad internal compatibility paths.
- The first implementation should start with timing because that is the root seam for Phase/Phrase correctness, loop handling, coasting, re-anchor, and future cue planning.
- The timing slice should be designed as the first step of the final Director architecture, not as a temporary extraction that will be replaced during cue/casting work.
- The prior Director/Switcher work and tailed-transition fixes remain valuable. This work deepens the next layer rather than reopening the Mechanical Switcher execution contract.
- Runtime architecture documentation was refreshed after the code shape landed so it describes Director, On-Air Timing, Cue Intent, and Mechanical Switcher responsibilities together.
- Issue 07 clarification: the Director configures one cue direction and inserts/updates it in the Switcher; the Switcher holds the Loaded Cue, derives Lock Point from Runway, refuses updates after lock, and owns Armed Cue execution. Do not implement a Director-held LoadedCue plus Director-called ArmCue seam.
