Status: ready-for-human

## Problem Statement

PenroseArt's tailed-transition cleanup clarified that the Director should choose musical targets from Phrase Windows and Selected Phase Boundaries while the Mechanical Switcher only executes A-to-B moves. The current implementation still does not fully honor that seam. The Director still knows too much about active Transition execution: transition progress, completion, duration/tail, and mechanical switching state can leak back into decision/status behavior or next-choice staging.

From the wall author's perspective, this makes the wall harder to reason about. A Transition Tail is supposed to be visual resolution only, but if the Director waits for a Tail or tracks active transition completion, the decision matrix can appear to change only when the previous Transition finishes. That defeats the purpose of the 16-beat minimum cadence plus 12-beat maximum Transition Duration rule: cadence already prevents overlap, so the Director should not need Switcher busy checks or defensive transition-completion logic.

The deeper design problem is that the Switcher is currently too shallow as a module. Its interface exposes mechanical execution facts that callers can misuse, and the Director still carries implementation details that should live behind the Switcher seam. This refactor should deepen the Mechanical Switcher: tell it which move to execute and when it starts; after that it owns rendering, progress, Tail, and completion internally.

## Solution

Deepen the Director/Switcher seam so the Director directs and the Switcher executes.

The Director remains the musical decision layer. It reads live beat and Track Phase data, derives the current Phrase Window, selects Phase Boundaries, applies the 16-beat cadence rule, chooses the next Effect and Transition, reads the selected Transition's Repertoire only as much as needed to know when to start, and issues the start command at the correct beat.

The Mechanical Switcher becomes the deep execution module for A-to-B moves. Once it receives a start command, it owns transition progress, Tail, completion, and promotion of the destination Effect to the current Effect. Active switching state is private to the Switcher implementation. It may be visible through editor/debug presentation as "what is mechanically being rendered," but it must not be a public decision input and must not be used by the Director.

The decision matrix, Phase Anchor, Phrase Window plan, Selected Phase Boundary cursor, cadence state, Next Effect, and Next Transition must update from musical timing only. They must continue to update while a Transition Tail is still rendering. The Director should not wait for the Switcher to finish a Tail before staging the following move or reporting the current musical decision.

## User Stories

1. As a PenroseArt wall author, I want switching to be fire-and-forget, so that a started Transition can finish visually without affecting musical planning.
2. As a PenroseArt wall author, I want the Director to decide only from phrase, phase, cadence, and staged choices, so that mechanical Tail timing cannot change the decision matrix.
3. As a PenroseArt wall author, I want the Switcher to own active Transition progress, so that callers do not need to know how a Transition resolves after it starts.
4. As a PenroseArt wall author, I want the Switcher to own Tail completion, so that Tail completion is never treated as a musical event.
5. As a PenroseArt wall author, I want the Director to start a Transition at the correct Runway point, so that its Impact Point lands on the Selected Phase Boundary.
6. As a PenroseArt wall author, I want the Director to ignore whether the Switcher is currently rendering a Transition, so that decision timing remains tied to music.
7. As a PenroseArt wall author, I want the 16-beat cadence rule to be the overlap-prevention mechanism, so that no extra busy-state scheduling checks are needed.
8. As a PenroseArt wall author, I want non-negative Runway/Tail values with `Runway + Tail <= 12` enforced at authoring/settings seams, so that every Transition has room to finish inside the 16-beat cadence and zero/zero hard cuts remain valid.
9. As a PenroseArt wall author, I want the Director to stage the next Effect and Transition immediately after starting the current move, so that following choices are not delayed until Tail completion.
10. As a PenroseArt wall author, I want Next Effect and Next Transition to represent the next musical move, so that tuning tools show what is coming rather than what is mechanically finishing.
11. As a PenroseArt wall author, I want a Transition Tail to keep rendering while the decision matrix says WaitingForRunway or CueingTransition for the next selected boundary, so that the display matches musical truth.
12. As a PenroseArt wall author, I want the decision matrix to keep following the current Phrase Window while a Tail renders, so that Phrase changes are not missed.
13. As a PenroseArt wall author, I want Phase Anchor updates to keep using Track Phase timing during active Transition rendering, so that the wall does not fall back to weaker timing evidence.
14. As a PenroseArt wall author, I want Selected Phase Boundary planning to be independent of mechanical transition execution, so that selected boundaries remain stable and meaningful.
15. As a PenroseArt wall author, I want cadence marking to occur at the selected musical boundary, so that completion beats never become fake change beats.
16. As a PenroseArt wall author, I want the Director to read a Transition's Repertoire for scheduling only, so that visual execution details stay with the Transition/Switcher.
17. As a PenroseArt wall author, I want the Director to know Runway when it needs to decide when to start, so that it does not need to know Tail or total duration afterward.
18. As a PenroseArt wall author, I want the Switcher to know the Transition settings it needs to render, so that the Director does not compute progress each frame.
19. As a PenroseArt wall author, I want late-in-runway starts to still render correctly if supported, so that small frame/tick timing delays do not break the visual hit.
20. As a PenroseArt wall author, I want no public `is switching` concept in Director-facing code, so that future changes cannot accidentally reintroduce busy-state scheduling.
21. As a PenroseArt wall author, I want editor/debug displays to distinguish mechanical rendering from Director decisions, so that observability does not become control flow.
22. As a PenroseArt wall author, I want the Tuning Window to show active mechanical Transition as debug/status only, so that it does not imply the Director is blocked.
23. As a PenroseArt wall author, I want Hold Selected behavior to keep working, so that live tuning can still repeatedly stage a chosen Effect or Transition.
24. As a PenroseArt wall author, I want one-shot manual steering to keep working, so that a selected next move is consumed when it starts and then normal staging resumes.
25. As a PenroseArt wall author, I want Standalone Mode to keep self-running, so that the wall remains intentional without OSC data.
26. As a PenroseArt wall author, I want Synced Mode to keep following live OSC timing, so that live music remains the source of sequencing truth.
27. As a PenroseArt wall author, I want loops and substantial beat rewinds to keep using current Track Phase facts, so that loop handling stays self-correcting.
28. As a PenroseArt wall author, I want small beat jitter behavior left intact, so that out-of-order OSC updates do not churn plans.
29. As a PenroseArt wall author, I want Transition Completion to remain a mechanical fact, so that it only affects what the Switcher renders next.
30. As a PenroseArt wall author, I want Impact Point to remain Transition-local, so that it does not become a Phase Boundary or cadence marker by another name.
31. As a PenroseArt wall author, I want Selected Phase Boundary to remain music-grid vocabulary, so that transition targets stay separate from visual progress.
32. As a PenroseArt wall author, I want the Director's public status to expose musical decisions, so that it does not report Transitioning as if that were a Director decision.
33. As a PenroseArt wall author, I want the Switcher interface to be small, so that starting a move is simple and reliable.
34. As a PenroseArt wall author, I want the Switcher implementation to hide progress and completion details, so that bugs in those details are localized.
35. As a maintainer, I want the Switcher to be a deep module, so that transition execution knowledge does not leak across the Director, Controller, tests, and editor tools.
36. As a maintainer, I want the Director/Switcher seam to be hard to misuse, so that future agents do not cargo-cult busy-state checks back into scheduling.
37. As a maintainer, I want tests to prove behavior through the highest useful seam, so that refactors do not break tests that assert private execution details.
38. As a maintainer, I want focused Switcher tests only where they verify the Switcher interface itself, so that mechanical execution can be changed locally.
39. As a maintainer, I want existing Director synced-tail tests to cover decision behavior while a Tail renders, so that the original bug family cannot return.
40. As a maintainer, I want staging tests to prove next choices are restaged at transition start, so that staging does not wait for mechanical completion.
41. As a maintainer, I want no queues, preemption, snapshots, restart machinery, or scheduler layer, so that the creative runtime stays direct.
42. As a maintainer, I want errors to stay visible when transition settings violate contracts, so that invalid authoring does not produce fake success.
43. As a maintainer, I want transition authoring validation to remain the place where invalid Runway/Tail combinations are rejected, so that the Director stays simple.
44. As a maintainer, I want runtime code vocabulary to reinforce Director/Switcher separation, so that names do not smuggle old mental models back in.
45. As a maintainer, I want the completed implementation to remove old tests that asserted `IsTransitioning`, so that tests do not preserve the wrong interface.
46. As a maintainer, I want the public Switcher status to be presentation-only, so that status readouts do not become scheduling inputs.
47. As a maintainer, I want the Controller to ask the Switcher for rendered pixels, so that Controller does not calculate transition progress.
48. As a maintainer, I want the Director to remain testable without rendering pixels, so that sequencing tests stay fast and focused.
49. As a maintainer, I want the Switcher to remain testable without live OSC, so that mechanical transition execution can be validated in isolation.
50. As a maintainer, I want this deepening to replace the shallow interface rather than layer wrappers around it, so that the code gets simpler rather than more defensive.

## Implementation Decisions

- Treat the Director as the sole musical decision module. It owns Phrase Window interpretation, Selected Phase Boundary planning, cadence, mode selection, next-choice staging, and the decision to start an A-to-B move.
- Treat the Mechanical Switcher as the sole mechanical execution module. It owns active Transition rendering, progress, Tail, completion, and promotion of B to the current Effect.
- Remove public Director-facing switching-state concepts. A public boolean that means "the Switcher is transitioning" should not exist as a scheduling seam.
- Remove `Transitioning` as a Director decision concept. Active mechanical rendering is not a Director decision.
- Keep mechanical stage presentation available for editor/debug display, but express it as presentation state, not as a value the Director branches on.
- Change the Switcher interface so callers start a move and then stop managing it. The Switcher should receive enough timing context at start to render the Transition correctly, including any progress offset needed when the cue starts slightly late inside the Runway.
- The Director may read Runway from the selected Transition's Repertoire to decide when to start. After start, the Director should not own Tail, duration, progress, or completion.
- The Director should stage the next Effect and next Transition when a move starts, using the destination Effect as the current selection context, not waiting until mechanical completion.
- The Director should continue to refresh Phase Anchor, Phrase Window, Selected Phase Boundary cursor, cadence status, and decision status every synced tick regardless of whether the Switcher is mechanically rendering a Tail.
- The 16-beat minimum cadence plus the `Runway >= 0`, `Tail >= 0`, `Runway + Tail <= 12` settings contract are the overlap-prevention mechanism. Do not add Switcher busy-state checks, overlap guards, queues, preemption, snapshots, restart machinery, or a defensive scheduler.
- Standalone Mode should also avoid using Switcher busy state as a decision input. Its self-running cadence can decide when to start a move while the Switcher owns completion internally.
- Hold and Show Now are explicit override commands. They may cancel or replace mechanical execution because they are user/developer override actions, not ordinary musical scheduling.
- Preserve the existing reflection-based Effect/Transition catalog. Do not introduce registries, ScriptableObject routing layers, or service-style orchestration.
- Keep Transition Settings validation at the authoring/settings seam, and apply it when values are intentionally modified rather than when an existing asset is merely loaded. The runtime should assume Repertoire timing is valid.
- Prefer replacing the shallow Director/Switcher interaction over layering new wrappers or flags around the old shape.

## Testing Decisions

- Good tests should verify observable behavior through public interfaces, not private fields or old public execution flags. Tests should survive internal changes to how the Switcher tracks mechanical execution.
- The highest-value seam is the Director/Switcher integration seam: feed beat/Track Phase state into the Director, tick sequencing, and observe Director status plus Switcher presentation state. This proves the user-visible behavior without testing private implementation details.
- A focused Switcher seam is also justified because Switcher is becoming the deep mechanical execution module. Switcher tests should start a move, render/advance it through the public interface, and assert observable stage/result behavior without exposing internal `is switching` state.
- Existing synced-tail tests are prior art for Director sequencing. Extend them so they prove the decision matrix follows the next Selected Phase Boundary while a previous Tail is still rendering.
- Existing staging tests are prior art for Next Effect / Next Transition behavior. Update them so they prove consumed choices are restaged at transition start, not at completion.
- Existing Transition Settings and Transition Repertoire tests remain the right seam for Runway/Tail validity. Do not move those validation responsibilities into Director tests.
- Avoid tests that merely assert the absence or presence of a particular field/property. The behavior to prove is that no Director decision or staging output changes based on active mechanical rendering.
- Tests should use project glossary terms: Director, Mechanical Switcher, A-to-B Transition, Runway, Tail, Impact Point, Selected Phase Boundary, Phrase Window, Phase Boundary, cadence, Next Effect, and Next Transition.
- Run focused Director synced-tail tests, Director staging tests, Switcher tests, and Transition timing/settings tests first, then Unity compile, then the broader Unity test suite.

## Out of Scope

- Changing RaveSystem OSC message shape, ports, URLs, or payload semantics.
- Changing the Penrose wall's hardware output, serial protocol, UDP/E1.31 path, PixelReceiver behavior, drum overlay, camera overlay, or telnet behavior.
- Adding queues, preemption, transition snapshots, restart paths, overlap schedulers, pass IDs, loop-window models, or historical playback trackers.
- Replacing the Director/Switcher architecture with a new sequencing subsystem.
- Replacing reflection discovery for Effects, Transitions, Mixers, or Blenders.
- Adding prefab-heavy Unity composition, dependency-injection frameworks, event buses, or service layers.
- Retuning visual transition algorithms except where a test fixture needs a Transition with a known Repertoire.
- Weakening the 16-beat cadence or `Runway >= 0`, `Tail >= 0`, `Runway + Tail <= 12` Transition Duration contract.
- Making the Director compensate for invalid Transition Settings.
- Heavy Editor UI testing. Editor/tooling changes should be covered through extracted logic or runtime status where practical.

## Further Notes

- Implemented on `fix/transition-tail-phase-anchor`: the Director fires `StartTransition` and immediately returns to musical planning; the Switcher owns rendering, progress, Tail completion, B promotion, and last-command-wins replacement.
- Switcher status remains available for inspector/HUD observability, but Director/runtime decision modules do not use it as a busy/progress scheduling input.
- Transition timing validation is limited to the real authoring contract: `Runway >= 0`, `Tail >= 0`, and `Runway + Tail <= 12`. Zero/zero is a supported hard cut.
- Existing Transition Settings assets are not silently constrained on load; constraint/repair happens when values are intentionally modified through editor utility paths.
- The previous tailed-transition work remains useful, especially the Phrase Window / Selected Phase Boundary model and authoring-side Transition Duration validation. This deepening round built on that model rather than reopening phase/phrase vocabulary.
