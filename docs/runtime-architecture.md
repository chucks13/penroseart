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
       ├─ musical state/tools: BeatManager + sibling Waveforms
       ├─ rhythm inputs: RaveOscReceiver, OSCReader
       ├─ planning: six track-scoped TrackCueSheet slots
       ├─ sequencing: Director follows BeatManager.LiveOrder focus by wire position
       ├─ execution: fire-and-forget Switcher.Cast -> transition
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
5. Create plain C# helpers such as `drums`, `PixelReceiver`, `BeatManager`, its sibling `Waveforms` acquisition surface, `Timer`, and optionally `CameraReader` / `SerialOut`.
6. Add Unity-hosted input receivers such as `OSCReader` and `RaveOscReceiver`.
7. Create the `Switcher` and `Director`; the timer callback goes to `Director.OnTimerFinished` for Standalone Mode cadence.
8. Enter the frame loop in `Controller.Update()`.

Unity calls lifecycle methods only on `MonoBehaviour` components. Most PenroseArt runtime objects are plain C# objects, so `Controller` calls their lifecycle methods manually.

## Frame loop

The active runtime frame flow is:

1. `Controller.Update()` advances local frame time and optional command systems.
2. `RaveOscReceiver.ApplyTo(beatManager)` applies the newest live Rave OSC state before any sequencing decision.
3. `BeatManager.Update()` settles the live or Standalone source, derives shared values, and captures every public value group once so the frame sees one coherent musical snapshot.
4. `Director.Tick(deltaTime)` chooses Standalone or Synced behavior and keeps one `TrackCueSheet` slot per physical player current with that player's complete structure generation.
5. In Synced Mode, the Director follows `BeatManager.LiveOrder.Focus`, reads that player's absolute beat and the on-air Grid, and looks up the corresponding planned segment.
6. When the focus position reaches the next mark's Runway start, the Director reads the current focus sheet, masks its baked Effect or Transition with any one-shot or held override, and calls `Switcher.Cast(...)` fire-and-forget.
7. If four on-air Grid starts pass without a segment change, the Director uses `TrackCueSheet.DealAt(...)` for one fresh cast at that boundary; the sheet itself does not change.
8. `Switcher.RenderAtTime(...)` renders the current Effect or active A-to-B Transition into `penrose.buffer`.
9. Filters, drums, camera, and external pixel blending may modify `penrose.buffer`.
10. The active serial path or legacy UDP/ACN path sends the frame to hardware.
11. `Penrose.UpdateModelColors()` applies the buffer to the Unity preview mesh and HUD/OSC status is updated.

## Sequencing model

ADR-0004 defines the durable rule: the **Director directs** and the **Mechanical Switcher executes**.

The Director owns the decision layer: which Performer should be on stage, which Transition should move between A and B, and when that move should be cued. The Switcher owns only the in-flight mechanical execution: source Effect, target Effect, active Transition, progress, and completion. It does not own a pending or loaded-cue lifecycle.

### Standalone Mode

Standalone Mode is the intentional self-running behavior when no live OSC source is present. The Director uses the existing `Timer` as its cadence clock, consumes staged Next Effect / Next Transition choices, and commands the Switcher. Timer expiry is not an independent sequencer; `Director.OnTimerFinished()` ignores timer completion while Synced Mode is active.

### Synced Mode

Synced Mode is active when BeatManager's usable musical clock is present (`IsSynced`); transport connectivity alone does not decide the mode. Position is wire-only: `BeatManager.LiveOrder.Focus` selects the player, that player's absolute beat locates the plan, and the on-air Grid supplies boundaries. The Director never self-ticks a musical count.

`TrackCueSheet.Build(...)` is the single creative planning seam. When a player's complete structure generation changes, it builds one full-track plan into that player's slot, seeded by structure generation plus player number. The plan contains every Cue Mark with its Effect and Transition indexes already assigned. Its seeded Effect and Transition bags, energy fit, drop/fill Anchors, ride-through versus performed-transition choice, and post-drop hold all live behind the builder. Phrase boundaries are preferred mark positions, not mandatory transitions.

At runtime the Director resolves the focus player's next mark and waits until its Transition Runway must begin. It then re-reads the current focus sheet and casts that baked assignment. Focus handover, needle-drop, and loop exit use the same lookup: when wire position resolves to a different segment or sheet, a normal cast takes over. A loop pinned inside one segment changes nothing until the starvation guard injects a fresh `DealAt(...)` cast after four on-air Grid starts.

Staged and held Effect/Transition choices mask the assignment at cast time; they never mutate the sheet. The Switcher accepts `Cast(...)` unconditionally and holds no loaded-cue, lock, rejection, or revocation lifecycle.

### Transition timing

A Transition's `TransitionRepertoire` declares its beat timing:

- **Runway**: beats before the chosen Cue Mark when the Transition must start.
- **Impact Point**: the Transition-local visual hit that lands on the selected Cue Mark.
- **Tail**: visual resolution after the Impact Point.
- **Tags**: Fill/Drop event suitability. Timing shape makes a Transition schedulable; tags make it artistically suitable for an event.

The Director uses Runway to choose the last responsible cast beat. `Switcher.Cast(...)` starts the Transition immediately from that beat-domain direction; a late cast compresses the Runway while keeping Impact on the Cue Mark. Tail completion and Switcher progress are execution facts only, not musical scheduling inputs. Saved `Assets/transitions/Resources/TransitionSettings/*.asset` values participate in the live Transition Repertoire through `TransitionSettingsProvider`, so code defaults alone are not the full runtime truth.

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

Standalone/manual paths can still call `Switcher.StartTransition(...)` explicitly. Synced handoff calls `Switcher.Cast(...)` with the Cue Mark, target Effect, Transition, and current beat clock. Cast starts the move unconditionally and the Switcher promotes the destination Effect after the Transition completes. If an explicit transition is issued while one is still rendering, the Switcher replaces the mechanical move using the previous destination as the new source.

## Deck selection and staging

Standalone selection uses rotating integer decks.

1. Initialize a deck as `[0, 1, 2, ... count - 1]`.
2. Pick a random index from the top half of the deck.
3. Remove that entry.
4. Move it to the bottom.

This gives variety without immediate repeats while still eventually cycling through the catalog.

Synced selection is separate: `TrackCueSheet.Build(...)` deals seeded shuffle bags over the complete Effect and Transition catalogs and bakes the results into the plan. Drop/fill Anchors scan those bags for capable performers and choose either ride-through or a performed Transition. `TrackCueSheet.DealAt(...)` provides a deterministic fresh deal only for the starvation guard.

The Director keeps staged **Next Effect** and **Next Transition** choices as override masks. `SetNextEffect(...)` and `SetNextTransition(...)` replace exactly the next planned assignment; their Hold variants keep replacing that side on later casts. Releasing a hold returns to the unchanged plan.

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
| Geometry/model | `Assets/core/Runtime/Penrose.cs` | Layout data, tile metadata, Unity mesh generation, buffer-to-mesh colors. |
| Wall data files | `Assets/core/Runtime/WallData.cs` | `LayoutData`/`WiringData` contracts for the `Assets/StreamingAssets/` text files; layout is fixed, wiring is selected per art piece on the Controller. |
| Sequencing decision | `Assets/core/Switching/Director.cs` | Standalone cadence plus Synced focus following: maintains six track-sheet slots, resolves the focus player's wire position, applies override masks, casts at Runway start, and guards against segment starvation. |
| Track Cue Sheets | `Assets/core/Switching/TrackCueSheet.cs` | Pure full-track plan builder with baked Effect/Transition assignments, seeded bags, drop/fill Anchors, ride-through/performed-transition treatment, post-drop hold, and deterministic starvation deals. |
| Cue/casting | `Assets/core/Switching/Deck.cs`, `Assets/core/Effects/Repertoire.cs` | Rotating Standalone decks plus the capability and timing declarations consumed by track-sheet planning and Switcher execution. |
| Mechanical execution | `Assets/core/Switching/Switcher.cs` | Fire-and-forget Cast, ShowNow/StartTransition/RenderAtTime execution, and active A-to-B progress; no loaded-cue or lock lifecycle. |
| Effects | `Assets/core/Effects/EffectBase.cs`, `Assets/effects/*.cs` | Generate 900-tile frames; concrete Effects own Repertoire, Waveform acquisition, and every artistic mapping from shared musical facts/tools. |
| Screen effects | `Assets/core/Effects/ScreenEffect.cs` | Map rectangular screen buffers onto the Penrose tile layout. |
| Mixers/wrappers | `Assets/core/Effects/MixerBase.cs`, mixer effects | Remain one Effect publicly; privately own/configure child Effects and combine or transform their buffers. |
| Transitions/settings | `Assets/core/Transitions/TransitionBase.cs`, `Assets/core/Transitions/TransitionSettings*.cs`, `Assets/transitions/*.cs` | Blend effect A to effect B; concrete Transitions own musical response while settings declare Runway/Tail/Shape/Intensity defaults and saved tuning. |
| External blenders | `Assets/core/Blending/BlenderBase.cs`, `Assets/blenders/*.cs` | Mix incoming pixel-source data with the native Penrose buffer. |
| Palette | `Assets/core/helpers/GPalette.cs` | Global palette sampling and animated palette transitions. |
| Rhythm Data Surface | `Assets/core/Rhythm/BeatManager.cs`, `LiveOrderValues.cs`, other `*Values.cs`, `StockEnvelopes.cs`, `Duration.cs` | One live/Standalone musical gateway exposing shallow, frame-coherent wire values and derived musical values, including the live-order focus used by Synced Mode. |
| Waveform tools | `Assets/core/Rhythm/Waveforms.cs`, `Waveform.cs`, `WaveformPool.cs`, `Routine.cs` | Sibling acquisition surface, immutable clock-bound values, Pool loading/codec, and direct four-bar choreography composition. |
| Rave OSC | `Assets/core/IO/RaveOscReceiver.cs`, `Assets/OSC/Rave/*.cs`, `Assets/OSCReader.cs` | Receive/apply RaveSystem on-air state into BeatManager before Director ticks. |
| Drum overlay | `Assets/core/ReactiveInputs/drums.cs` | Drum/ring overlay triggers and drawing. |
| Serial output | `Assets/core/Hardware/SerialOut.cs` | USB serial discovery and frame output for S2 Mini / ESP32 boards. |
| Legacy UDP output | `Assets/core/Runtime/Controller.cs` (`sendUDPFrame`, `sendACN`) | E1.31/ACN output path retained for non-serial builds. |

## Known architectural pressure points

These are documented facts, not requests to change behavior during documentation work.

- `Controller` owns many responsibilities and is the primary future refactor target.
- Several numeric scene fields still depend on catalog indexes; name-based controls are safer.
- `OnEnd()` exists on effects/transitions but is not called by the current controller.
- Track-scoped Cue Sheet visualization is not present; a replacement for the deleted Live Timeline is a planned follow-up.
- `Controller - nova.cs` is inactive reference code under `#if false` and references missing/incompatible concepts.
- Optional telnet code is inactive by default and should be revisited before re-enabling.
