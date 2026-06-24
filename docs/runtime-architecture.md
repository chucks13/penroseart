# PenroseArt Runtime Architecture

PenroseArt is a Unity-hosted C# runtime for the Penrose Wall LED installation. Unity provides the scene host, editor workflow, preview mesh, UI text, keyboard input, and platform packaging. The visual system itself is mostly plain C# classes that render into fixed-size color buffers and then send those buffers to both the Unity preview mesh and hardware output.

## Runtime shape

```text
Unity scene
  └─ Controller MonoBehaviour
       ├─ Penrose model and preview mesh
       ├─ effect catalog: EffectBase[]
       ├─ transition catalog: TransitionBase[] + TransitionSettings
       ├─ blender catalog: BlenderBase[]
       ├─ rhythm inputs: RaveOscReceiver, OSCReader, BeatManager
       ├─ sequencing: Director -> On-Air Timing -> Timing Frame -> Cue Intent
       ├─ cue handoff: fire-and-forget SwitcherCueDirection
       ├─ execution: Mechanical Switcher loaded cue -> armed cue -> transition
       ├─ overlays/blending: drums, optional camera, optional PixelReceiver
       └─ outputs: serial hardware path or legacy UDP/ACN path
```

The project intentionally does **not** model every effect as a scene object. Effects, transitions, and blenders are runtime objects discovered from C# types. This keeps authoring close to creative-coding practice: copy a class, rename it, implement the frame algorithm, and let the runtime catalog discover it.

## Startup sequence

`Controller.Start()` is the main startup boundary.

1. Find and initialize the scene's `Penrose` component.
2. Discover and instantiate every non-ignored `EffectBase` subclass through `Factory<EffectBase>`.
3. Discover and instantiate every non-ignored `TransitionBase` subclass; transitions read their code defaults and saved `TransitionSettings`.
4. Discover and instantiate every non-ignored `BlenderBase` subclass.
5. Create plain C# helpers such as `drums`, `PixelReceiver`, `BeatManager`, `Timer`, and optionally `CameraReader` / `SerialOut`.
6. Add Unity-hosted input receivers such as `OSCReader` and `RaveOscReceiver`.
7. Create the `Switcher` and `Director`; the timer callback goes to `Director.OnTimerFinished` for Standalone Mode cadence.
8. Enter the frame loop in `Controller.Update()`.

Unity calls lifecycle methods only on `MonoBehaviour` components. Most PenroseArt runtime objects are plain C# objects, so `Controller` calls their lifecycle methods manually.

## Frame loop

The active runtime frame flow is:

1. `Controller.Update()` advances local frame time and optional command systems.
2. `RaveOscReceiver.ApplyTo(beatManager)` applies the newest live Rave OSC state before any sequencing decision.
3. `BeatManager.Update()` advances live or simulated rhythm state and exposes nullable rhythm queries.
4. `Director.Tick(deltaTime)` chooses Standalone, Synced, or Hold behavior.
5. In Synced Mode, `OnAirTiming.ReadFrame(...)` interprets the live beat and Track Phase state into one Director-facing `TimingFrame`.
6. `SyncedCueIntent` combines the `TimingFrame`, selected Transition Repertoire, Drop data, staged choices, and Effect Repertoire to decide Wait / Cue / BlockedByCadence and any preferred Performer casting.
7. The Director issues stage commands only: `Switcher.ShowNow(...)` or a fire-and-forget `Switcher.UpsertLoadedCue(...)` with a beat-domain `SwitcherCueDirection` and tiny `SwitcherClockSnapshot`.
8. `Switcher.RenderAtTime(...)` internally locks/starts due Loaded Cues, then renders the current Effect or active A-to-B Transition into `penrose.buffer`.
9. Filters, drums, camera, and external pixel blending may modify `penrose.buffer`.
10. The active serial path or legacy UDP/ACN path sends the frame to hardware.
11. `Penrose.UpdateModelColors()` applies the buffer to the Unity preview mesh and HUD/OSC status is updated.

## Sequencing model

ADR-0004 defines the durable rule: the **Director directs** and the **Mechanical Switcher executes**.

The Director owns the decision layer: which Performer should be on stage, which Transition should move between A and B, and when that move should be cued. The Switcher owns only the in-flight mechanical execution: source Effect, target Effect, active Transition, progress, and completion.

### Standalone Mode

Standalone Mode is the intentional self-running behavior when no live OSC source is present. The Director uses the existing `Timer` as its cadence clock, consumes staged Next Effect / Next Transition choices, and commands the Switcher. Timer expiry is not an independent sequencer; `Director.OnTimerFinished()` ignores timer completion while Synced Mode is active.

### Synced Mode

Synced Mode is active when live OSC data is present. The Director does not read raw Track Phase fields directly. Instead, it asks On-Air Timing for a `TimingFrame`.

On-Air Timing owns the musical interpretation seam:

- converts `BeatManager` rhythm queries into `OnAirTimingInput`;
- resolves the 16-beat Phase reading through `PhaseClock`;
- derives active and upcoming `PhraseWindow` values from Track Phase;
- owns `CueSheet` planning and cursor advancement for current/upcoming Phrase Windows;
- corrects pass-local cue/cadence state after substantial Beat Rewinds;
- coasts when Track Phase disappears after a structural anchor;
- reports re-anchor when fresh structural Track Phase replaces a coasted or weaker target.

The returned `TimingFrame` is the Director-facing interface: current beat, Phase reading, Phase Anchor availability/confidence, Cue Mark, Phrase Window identity when known, source/reason, Beat Rewind, pass-local state correction, Coast, and Re-anchor.

Cue/casting then happens from domain facts, not raw OSC fields. `SyncedCueIntent` reads the `TimingFrame`, Transition Repertoire Runway/Tail, Drop data, staged choice, current Performer, deck, and Effect Repertoire. A Drop-aligned cue may reserve a preferred Drop-capable Performer through `EffectDeckSelection`, unless manual or held staging says to preserve the current staged choice. When `SyncedCueIntent` returns Cue, the Director sends one `SwitcherCueDirection` and records pass-local Cue Mark consumption from its own command; it does not inspect Switcher Loaded/Locked/Started state.

### Transition timing

A Transition's `TransitionRepertoire` declares its beat timing:

- **Runway**: beats before the chosen Cue Mark when the Transition must start.
- **Impact Point**: the Transition-local visual hit that lands on the selected Cue Mark.
- **Tail**: visual resolution after the Impact Point.

`TransitionBeatPlan.FromCueMark(...)` is the shared beat-domain Runway/Tail math. The Director chooses the Cue Mark, destination Performer, and Transition; the Switcher derives Loaded Cue Lock Point, start, progress, and completion so the Impact Point lands on that Cue Mark. Tail completion and Switcher progress are execution facts only; they are not musical scheduling inputs and do not redefine the next Phrase Window or Phase Anchor.

## Catalog discovery and indexing

`Factory<T>` builds catalogs by reflecting over the assembly that contains `T`.

A type appears in the catalog when it is:

- a concrete class;
- not abstract;
- a subclass of the catalog base type; and
- not marked with `[RuntimeCatalogIgnore]`.

The resulting types are sorted by `Type.FullName` using ordinal comparison. This makes indexes deterministic for a fixed set of classes.

Indexes are still not permanent IDs. Adding, removing, or renaming an effect can move later sorted indexes. Use name-based controls for debugging and operator workflows when possible.

## Effect lifecycle

Each top-level effect has one catalog instance.

```text
Init()        once after creation
OnStart()    every time the effect becomes active
UpdateTime() every active frame before Draw()
Draw()       every active frame
OnEnd()      reserved, but Controller does not currently call it
```

`EffectBase.Init()` connects the effect to `Controller.Instance`, the active `Penrose` model, tile metadata, and a `Color[] buffer` sized to `Penrose.Total`.

`EffectBase.Draw()` implementations write one frame into that local `buffer`. The Switcher returns the active Effect or Transition buffer to the Controller, which makes it the current `penrose.buffer` for overlays, output, and preview.

## Transition execution lifecycle

Transitions blend two effect indexes:

- `A`: source effect index;
- `B`: destination effect index;
- `V`: progress from `0` to `1`;
- `D`: remaining progress, `1 - V`.

Standalone/manual paths can still call `Switcher.StartTransition(...)` explicitly. Synced cue handoff uses `Switcher.UpsertLoadedCue(...)`: the Switcher holds one Loaded Cue, derives its Transition-specific Lock Point from Runway, ignores conflicting updates after lock, starts due cues from `RenderAtTime(...)`, and promotes the destination Effect after the Transition completes. If an explicit transition is issued while one is still rendering, the Switcher replaces the mechanical move using the previous destination as the new source.

## Deck selection and staging

Effects and transitions use rotating integer decks.

1. Initialize a deck as `[0, 1, 2, ... count - 1]`.
2. Pick a random index from the top half of the deck.
3. Remove that entry.
4. Move it to the bottom.

This gives variety without immediate repeats while still eventually cycling through the catalog. Repertoire-aware casting uses the same deck rules through `EffectDeckSelection`, so a preferred Drop-capable Performer is still reserved by rotating its card rather than by bypassing the deck.

The Director keeps staged **Next Effect** and **Next Transition** choices so the Tuning Window can show what is coming. Manual staging is one-shot by default. Hold Selected keeps the staged choice after each move while still allowing the Director/Switcher path to run.

## Held Effect override

`heldEffect` is the active inspection freeze. The `-1` Random sentinel lets the Director rotate normally. Any non-negative catalog index holds that Effect on the wall and suspends Director rotation until Random is chosen again or Escape resets it.

Hold is not a second sequencer and does not command around the Director. It exists so a developer can inspect and tune one Effect live.

## Buffer flow

```text
Switcher-rendered Effect or Transition buffer
  -> penrose.buffer
  -> optional filter/drum/camera/pixel-source blending
  -> serial or UDP hardware output
  -> Penrose.UpdateModelColors() for Unity preview
```

`Penrose.Total` is 900 logical tiles. The active serial path expands those logical tiles through the JSON `wires` map into physical LED order before sending packets to the S2 Mini / ESP32 boards.

## Major subsystems

| Area | Main files | Responsibility |
| --- | --- | --- |
| Runtime hub | `Assets/core/Runtime/Controller.cs` | Unity host for catalogs, lifecycle, input routing, output routing, overlays, preview update, and the per-frame call order. |
| Geometry/model | `Assets/core/Runtime/Penrose.cs` | JSON data, tile metadata, Unity mesh generation, buffer-to-mesh colors. |
| Sequencing decision | `Assets/core/Switching/Director.cs` | Standalone/Synced/Hold decision layer, staged choices, cue issuing, and read-only sequencing status. |
| Timing interpretation | `Assets/core/Switching/OnAirTiming.cs`, `Assets/core/Switching/PhaseClock.cs`, `Assets/core/Switching/PhraseWindow.cs`, `Assets/core/Switching/CueSheet.cs`, `Assets/core/Switching/ChangeCadence.cs` | Convert live beat/Track Phase facts into a Director-facing Timing Frame and current Cue Mark. |
| Cue/casting | `Assets/core/Switching/SyncedCueIntent.cs`, `Assets/core/Transitions/TransitionBeatPlan.cs`, `Assets/core/Switching/EffectDeckSelection.cs`, `Assets/core/Effects/Repertoire.cs` | Decide whether a Synced cue command should fire and which Performer should be cast. |
| Mechanical execution | `Assets/core/Switching/Switcher.cs` | ShowNow/StartTransition/RenderAtTime execution, Switcher-held Loaded Cue scheduling, and active A-to-B progress. |
| Effects | `Assets/core/Effects/EffectBase.cs`, `Assets/effects/*.cs` | Generate 900-tile frames and express their own Repertoire from BeatManager data. |
| Screen effects | `Assets/core/Effects/ScreenEffect.cs` | Map rectangular screen buffers onto the Penrose tile layout. |
| Mixers/wrappers | `Assets/core/Effects/MixerBase.cs`, mixer effects | Own child effects and combine/transform their buffers. |
| Transitions/settings | `Assets/core/Transitions/TransitionBase.cs`, `Assets/core/Transitions/TransitionSettings*.cs`, `Assets/transitions/*.cs` | Blend effect A to effect B and declare Runway/Tail/Shape/Intensity defaults and saved tuning. |
| External blenders | `Assets/core/Blending/BlenderBase.cs`, `Assets/blenders/*.cs` | Mix incoming pixel-source data with the native Penrose buffer. |
| Palette | `Assets/core/helpers/GPalette.cs` | Global palette sampling and animated palette transitions. |
| Rhythm queries | `Assets/core/Rhythm/BeatManager.cs`, `Assets/core/Rhythm/BeatManagerQueries.cs`, `Assets/core/Rhythm/PhraseEventView.cs`, `Assets/core/Rhythm/RhythmText.cs`, `Assets/core/Rhythm/Waveform.cs`, `Assets/core/Rhythm/WaveformPool.cs` | Live/simulated beat state, nullable OSC-derived rhythm-query values, current phrase-event display helpers under review, waveform evaluation, and waveform pool loading. |
| Rave OSC | `Assets/core/IO/RaveOscReceiver.cs`, `Assets/OSC/Rave/*.cs`, `Assets/OSCReader.cs` | Receive/apply RaveSystem on-air state into BeatManager before Director ticks. |
| Drum overlay | `Assets/core/ReactiveInputs/drums.cs` | Drum/ring overlay triggers and drawing. |
| Serial output | `Assets/core/Hardware/SerialOut.cs` | USB serial discovery and frame output for S2 Mini / ESP32 boards. |
| Legacy UDP output | `Assets/core/Runtime/Controller.cs` (`sendUDPFrame`, `sendACN`) | E1.31/ACN output path retained for non-serial builds. |

## Known architectural pressure points

These are documented facts, not requests to change behavior during documentation work.

- `Controller` owns many responsibilities and is the primary future refactor target.
- Several numeric scene fields still depend on catalog indexes; name-based controls are safer.
- `OnEnd()` exists on effects/transitions but is not called by the current controller.
- `Controller - nova.cs` is inactive reference code under `#if false` and references missing/incompatible concepts.
- Optional telnet code is inactive by default and should be revisited before re-enabling.
