# PRD: Tuning Window for transition authoring

Status: ready-for-agent

## Problem Statement

Tuning Transitions in PenroseArt is too indirect. The runtime can already sequence Effects and Transitions through the Director and Mechanical Switcher, but the Editor does not clearly show the Director's staged Next Effect and Next Transition, and the current Inspector view can read like it is showing "next" when it is actually showing the Switcher's current or last transition. A person tuning the wall needs to see what is coming, steer the Director's staged choices, and adjust Transition Settings while watching the wall move in Play Mode.

Transition defaults are also split between code and runtime behavior. Transition Repertoire values and other human-tweakable defaults should live with each Transition as Code Defaults, be copied into saved Transition Settings assets, and be editable from a dedicated authoring surface. Play Mode edits should persist after stopping unless explicitly restored. The workflow should not become a fake preview scheduler, a central registry, or a defensive state machine for low-consequence mid-transition edge cases.

## Solution

Create a standalone dockable Tuning Window opened from `Window > Penrose > Tuning`. It has Transitions and Effects tabs, with the Transitions tab implemented first and the Effects tab shaped for the same pattern later.

The Transitions tab lists the Transition catalog. In Edit Mode, selecting a Transition shows its saved Transition Settings and a Restore Defaults action. In Play Mode, the selection follows the Director's staged Next Transition. Clicking another Transition steers the real Director by setting the staged Next Transition. A Hold Selected toggle keeps the selected Transition staged after each move completes; turning it off returns to normal random staging.

The Director explicitly stages both Next Effect and Next Transition immediately after each move completes, exposes that state through its status, and consumes the staged choices when starting the next A-to-B move. This keeps the Director responsible for what/when and the Mechanical Switcher responsible only for execution.

Transition Settings use per-transition ScriptableObject assets as the persistence boundary. Each Transition keeps all human-tweakable Code Defaults near the top of its source: Transition Repertoire, timing, visual/default knobs, and external blend defaults where relevant. Editor tooling creates missing settings assets from Code Defaults, edits the real assets through Unity serialization, and restores all saved settings for a Transition from Code Defaults when requested. Runtime reads saved settings when available and falls back to Code Defaults if settings are absent.

Mid-transition edits and selections are handled simply. Selection changes steer the staged next move. Settings edits update saved authoring data and affect subsequent runtime reads. The implementation must not add queues, preemption, restart paths, transition snapshots, or defensive machinery for one-cycle oddities that naturally self-correct.

## User Stories

1. As a PenroseArt author, I want a dedicated Tuning Window, so that I can tune the wall without burying the workflow in the Controller Inspector.
2. As a PenroseArt author, I want to open the Tuning Window from the Unity Window menu, so that it behaves like a normal dockable Editor tool.
3. As a PenroseArt author, I want Transitions and Effects tabs, so that the authoring model can grow consistently from Transitions to Effects.
4. As a PenroseArt author, I want the Transitions tab implemented first, so that the highest-value live tuning workflow is available before expanding to Effects.
5. As a PenroseArt author, I want to see every Transition in the runtime catalog, so that I can select and tune any available A-to-B move.
6. As a PenroseArt author, I want the Transition list to work in Edit Mode, so that I can configure the next run without entering Play Mode.
7. As a PenroseArt author, I want selecting a Transition in Edit Mode to show its saved Transition Settings, so that I can adjust its defaults directly.
8. As a PenroseArt author, I want selecting a Transition in Play Mode to steer the Director's Next Transition, so that I can audition a specific Transition through the real runtime path.
9. As a PenroseArt author, I want the list selection in Play Mode to follow the Director's staged Next Transition, so that I always know what is coming next.
10. As a PenroseArt author, I want to see the current on-wall Effect separately from the Next Effect, so that I do not confuse the destination with what is currently playing.
11. As a PenroseArt author, I want to see the current or active Transition separately from the Next Transition, so that the tuning surface does not misrepresent Switcher state as Director state.
12. As a PenroseArt author, I want the Director to stage the Next Effect early, so that authoring tools can show the destination before the move starts.
13. As a PenroseArt author, I want the Director to stage the Next Transition early, so that authoring tools can show and steer the move before it starts.
14. As a PenroseArt author, I want the Director to stage both next choices after each move completes, so that there is always a concrete upcoming A-to-B move to inspect.
15. As a PenroseArt author, I want clicking a Transition without Hold Selected to be a one-shot steering action, so that normal random staging can resume naturally.
16. As a PenroseArt author, I want Hold Selected for Transitions, so that I can repeatedly audition one Transition while the Director and Mechanical Switcher continue running.
17. As a PenroseArt author, I want turning Hold Selected off to resume normal random Transition staging, so that tuning does not permanently alter selection behavior.
18. As a PenroseArt author, I want Hold Selected to be distinct from Held Effect, so that choosing a next Transition does not freeze the wall on one Effect.
19. As a PenroseArt author, I want the Tuning Window to steer the Director rather than run its own preview scheduler, so that the behavior I tune is the real wall behavior.
20. As a PenroseArt author, I want the Mechanical Switcher to remain execution-only, so that timing and selection decisions stay in the Director.
21. As a PenroseArt author, I want Transition Settings to persist after Play Mode stops, so that live tuning is not throwaway preview work.
22. As a PenroseArt author, I want normal settings edits to modify the real saved settings asset, so that Edit Mode and Play Mode share the same authoring data.
23. As a PenroseArt author, I want missing Transition Settings assets to be auto-created from Code Defaults, so that adding or checking out a Transition does not require asset hunting.
24. As a PenroseArt author, I want Restore Defaults for each Transition, so that I can intentionally return saved settings to the Transition's authored baseline.
25. As a PenroseArt author, I want Code Defaults to live with the Transition source, so that the algorithm and its intended baseline stay together.
26. As a PenroseArt author, I want Transition Settings to include all human-tweakable defaults, so that the workflow is complete rather than only tuning Repertoire.
27. As a PenroseArt author, I want Transition Repertoire fields to be editable as settings, so that Runway, Tail, Shape, Intensity, and tags can be tuned while watching the wall.
28. As a PenroseArt author, I want visual/default knobs to be editable as settings when they are artistic choices, so that the tuning window covers the actual creative defaults.
29. As a PenroseArt author, I want implementation invariants to remain in code, so that the settings surface does not become cluttered with values that are not meaningful creative controls.
30. As a PenroseArt author, I want runtime behavior to fall back to Code Defaults if saved settings are absent, so that a broken or incomplete checkout still runs truthfully.
31. As a PenroseArt author, I want settings changes to influence future decisions without complex mid-transition handling, so that tuning remains responsive and the ordinary path stays simple.
32. As a PenroseArt author, I want no queueing, preemption, restart, or snapshot logic for mid-transition edits, so that rare edge cases do not make normal sequencing worse.
33. As a PenroseArt author, I want one in-flight transition to be allowed to look slightly odd while I am actively tuning, so that the system can stay simple and self-correct on the next cycle.
34. As a PenroseArt author, I want the Controller Inspector to stop implying that the Switcher's current transition is the Director's next transition, so that runtime observability is accurate.
35. As a PenroseArt author, I want Play Mode tuning to use the same Director/Switcher path as standalone sequencing, so that the Editor does not hide integration bugs.
36. As a future Effects-tuning author, I want the Effects tab to follow the same selection and Hold Selected concepts, so that Effects can later gain the same workflow without a separate design.
37. As a maintainer, I want the implementation to avoid a central manual registry, so that the existing reflection catalog remains the source of available Performers.
38. As a maintainer, I want settings persistence to be small and Unity-friendly, so that saved authoring data can be edited and saved in both Edit Mode and Play Mode.
39. As a maintainer, I want tests at the Director and settings contract seams, so that behavior is verified without brittle Editor layout assertions.
40. As a maintainer, I want compile and focused behavior validation, so that the Tuning Window is safe to use without creating heavy UI test infrastructure.

## Implementation Decisions

- Build a standalone dockable Tuning Window with Transitions and Effects tabs. Implement Transitions first; keep Effects shaped for the same pattern but do not build full Effect Settings in this PRD.
- Preserve the Director/Switcher seam. The Director owns timing, selection, Next Effect, Next Transition, and Hold Selected behavior. The Mechanical Switcher remains execution-only and does not choose what or when.
- Add explicit staged state for Next Effect and Next Transition. Staged values are visible through Director status and are consumed when the Director starts the next A-to-B move.
- Stage both next choices immediately after startup and after each move completes. The tuning surface should never have to infer next choices from Switcher current/last state.
- Add a small runtime-facing Director interface for setting staged Next Effect, setting staged Next Transition, and toggling Hold Selected modes. The Tuning Window calls this interface in Play Mode.
- Manual selection without Hold Selected is one-shot. After that move completes, normal random staging resumes.
- Hold Selected keeps the selected Effect or Transition as the staged next choice after each move completes. It does not freeze the wall and does not replace Held Effect.
- Do not implement a fake preview scheduler. All Play Mode steering must exercise the real Director and Mechanical Switcher path.
- Represent Transition Settings as per-transition ScriptableObject assets. These assets are the saved authoring values edited by the Tuning Window and read by runtime when available.
- Keep Code Defaults with each Transition source and move all human-tweakable defaults to the top of each Transition. This includes Transition Repertoire, timing, visual/default knobs, and external blend defaults where they are artistic controls.
- Do not move algorithm invariants or non-creative implementation constants into settings. Settings are for human-tweakable defaults, not every numeric literal.
- Restore Defaults copies all Code Defaults for the selected Transition into the saved settings asset. It is not limited to Repertoire.
- Editor tooling creates missing Transition Settings assets from Code Defaults. Normal authoring does not require users to create, delete, or hunt for asset files.
- Runtime may cache asset references, but should not freeze effective settings values at catalog setup if Play Mode edits are expected to affect future decisions.
- Runtime falls back to Code Defaults when a settings asset is missing. This is a truthful fallback for missing authoring data, not a hidden degradation path for invalid runtime state.
- Settings should be edited through Unity serialization so standard dirty handling and Undo expectations are respected where practical.
- Save dirty settings assets after Restore Defaults and when Play Mode exits, so Play Mode tuning persists unless explicitly restored.
- Controller/runtime observability should distinguish Current Effect, Next Effect, current/active Transition, and Next Transition.
- Avoid defensive mid-transition complexity. If the selected Transition or settings change while a transition is already in flight, do not add queues, preemption, restarts, transition snapshots, or special state machines. The changed values steer future reads and future staged moves.
- Keep the implementation direct. Avoid speculative generic settings hierarchies unless multiple real settings shapes require them.
- Keep the existing reflection catalog as the source of available Effects and Transitions. Do not add a manual registry for the Tuning Window.

## Testing Decisions

- Good tests verify caller-visible behavior through the highest available seam. They should prove what the Director stages, what settings produce, and what Restore Defaults does. They should not assert private fields, IMGUI layout details, or the absence of old implementation structure.
- Test the Director staging seam: initial staged choices, restaging after completion, manual staged selection, one-shot selection behavior, and Hold Selected behavior.
- Test the settings contract seam: Code Defaults create settings, saved settings produce the effective Transition Repertoire and artistic knobs, and Restore Defaults restores all defaults for the selected Transition.
- Test runtime observability enough to prove Director status exposes Next Effect and Next Transition distinctly from current Switcher state.
- Prefer focused Edit Mode tests for pure Director and settings behavior. Avoid heavy UI automation for the Editor window.
- Validate the Tuning Window with Unity compile and manual Editor review. Play Mode review should be done only when safe because the runtime can interact with serial output, UDP listeners, OSC input, and other live systems.
- Prior art exists in tests for Transition Repertoire, Synced Cue Decision, Effect Deck Selection, Synced Transition Plan, Transition Beat Plan, Directional Wipe behavior, and Beat Manager integration. Follow their style for focused behavior tests.
- If Editor utility logic is factored out of the window, test that utility through its public behavior rather than through the rendered UI.

## Out of Scope

- Full Effect Settings implementation is out of scope. The Effects tab may exist as a placeholder or skeleton for the same future pattern.
- Replacing the buffer/effect architecture is out of scope.
- Replacing reflection-based catalog discovery with a manual registry is out of scope.
- Reworking the Mechanical Switcher beyond the minimal changes needed to consume staged Director choices is out of scope.
- Adding queueing, preemption, restart, snapshot, or defensive mid-transition state machinery is out of scope.
- Tuning every numeric literal is out of scope. Only human-tweakable artistic defaults belong in settings.
- Creating heavy automated UI tests for Editor layout is out of scope.
- Changing hardware output modes, OSC messages, serial protocol behavior, or PixelReceiver behavior is out of scope.
- Creating or revising ADRs is out of scope unless implementation uncovers a new hard-to-reverse architectural decision.

## Further Notes

- This PRD follows the glossary terms: Tuning Window, Hold Selected, Director, Mechanical Switcher, Next Effect, Next Transition, Transition Repertoire, Transition Settings, Code Defaults, Play Mode, and Edit Mode.
- The design aligns with the sequencing ADR: the Director directs, the Mechanical Switcher executes, and editor tooling belongs in the editor layer while core sequencing remains plain runtime code.
- The implementation should be complete for current Transitions. A repertoire-only first pass would leave the authoring workflow incomplete and is not the desired scope.
- Simplicity means the cleanest sound design, not the smallest patch and not extra safeguards for rare low-consequence events.
