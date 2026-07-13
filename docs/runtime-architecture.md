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
       ├─ sequencing: Director (wire-change reducer) -> two Cue Sheets -> cast Cue
       ├─ cue handoff: fire-and-forget SwitcherCueDirection
       ├─ execution: Mechanical Switcher loaded cue -> locked cue -> transition
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
3. `BeatManager.Update()` settles the live or Standalone source, derives shared signals, and captures every concept doorway once so the frame sees one coherent Data Surface.
4. `Director.Tick(deltaTime)` chooses Standalone, Synced, or Hold behavior.
5. In Synced Mode, the Director wakes only on a new beat; it reads Grid and Phrase truth from `BeatManager` (wire-decoded) and repairs its two Cue Sheets by invariant.
6. When a Grid carrying a Cue Mark begins, the Director casts a Cue lazily — a Fill on this Grid or a Drop on the next Grid makes capable Repertoire *preferred*, never required — reading the freshest wire truth.
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

Synced Mode is active when BeatManager's usable musical clock is present (`IsSynced`); transport connectivity alone does not decide the mode. Grid and Phrase truth come from the wire (RaveSystem OSC schema v2) decoded into BeatManager's concept doorways; the Director does not re-derive timing from arithmetic. The Director is a wire-change reducer (ADR-0011): it wakes once per new beat — nothing in the decision path runs per frame — and does three things.

On each wake the Director:

- **repairs two Cue Sheets by invariant** (`RepairSheets`): it holds exactly a current and a next `CueSheet`, keyed to the announced **label and length** read from `BeatManager` — nothing else. It watches the phrase_state lane by expectation: the expected countdown wrap is the turnover (next promotes to current and the emptied slot refills), and an unexpected label/length change rebuilds only the affected sheet. A sheet captures its absolute anchor once, at build or shift, so timing wobble on an unchanged announcement is absorbed against that anchor and cannot re-roll a sheet — only a changed announcement can;
- **casts a Cue lazily** (`CastOnNewGrid`) when a Grid carrying a Cue Mark begins, reading the freshest wire truth. A Fill on this Grid or a Drop on the next Grid makes capable Repertoire *preferred*, never required; energy and every other wire lane are Performer/Transition inputs read from `BeatManager` by the Performers themselves, not Director casting inputs;
- **offers the Cue fire-and-forget** to the Switcher (`UpsertLoadedCue`) and acts on its one answer. The Director makes no keep/recast decision of its own: identity on the seam is the Cue Mark alone, so a same-mark offer the Switcher answers *kept* rides the loaded cue unchanged, a *rejected* offer touches nothing, and only a *loaded* answer pulls the peeked deck cards and re-stages.

The Director records no verdicts and holds no decision memory. It hands each Cue to the Switcher fire-and-forget and never mirrors commitment state; the Switcher alone owns commitment.

### Transition timing

A Transition's `TransitionRepertoire` declares its beat timing:

- **Runway**: beats before the chosen Cue Mark when the Transition must start.
- **Impact Point**: the Transition-local visual hit that lands on the selected Cue Mark.
- **Tail**: visual resolution after the Impact Point.
- **Tags**: Fill/Drop event suitability. Timing shape makes a Transition schedulable; tags make it artistically suitable for an event.

Runway/Tail/Lock arithmetic is private Switcher math (`Switcher.ProjectCueWindow` / `LockPointBeatFor`), not a shared public type. The Director chooses the Cue Mark, destination Performer, and Transition; the Switcher derives the Loaded Cue Lock Point, start, progress, and completion so the Transition's impact lands on that Cue Mark. Tail completion and Switcher progress are execution facts only; they are not musical scheduling inputs. Saved `Assets/transitions/Resources/TransitionSettings/*.asset` values participate in the live Transition Repertoire through `TransitionSettingsProvider`, so code defaults alone are not the full runtime truth.

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

Standalone/manual paths can still call `Switcher.StartTransition(...)` explicitly. Synced cue handoff uses `Switcher.UpsertLoadedCue(...)`, whose identity is the Cue Mark alone: an offer at the same Cue Mark is a **keep** (the loaded cue rides unchanged and is never re-flavored), a different-mark offer replaces the loaded cue when it can still commit and is not locked, and otherwise the offer is rejected. The Switcher answers in one call — kept, loaded, or rejected — so the Director never mirrors commitment. The Switcher holds one Loaded Cue, derives its Transition-specific Lock Point from Runway, starts due cues from `RenderAtTime(...)`, and promotes the destination Effect after the Transition completes. If an explicit transition is issued while one is still rendering, the Switcher replaces the mechanical move using the previous destination as the new source.

## Deck selection and staging

Effects and transitions use rotating integer decks.

1. Initialize a deck as `[0, 1, 2, ... count - 1]`.
2. Pick a random index from the top half of the deck.
3. Remove that entry.
4. Move it to the bottom.

This gives variety without immediate repeats while still eventually cycling through the catalog. Repertoire-aware casting uses the same deck rules, so a preferred Fill/Drop-capable Effect or Transition is still reserved by rotating its card rather than by bypassing the deck. Transition event casting first inspects candidates without mutation, then rotates the selected transition only when a cue command is sent.

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
| Geometry/model | `Assets/core/Runtime/Penrose.cs` | Layout data, tile metadata, Unity mesh generation, buffer-to-mesh colors. |
| Wall data files | `Assets/core/Runtime/WallData.cs` | `LayoutData`/`WiringData` contracts for the `Assets/StreamingAssets/` text files; layout is fixed, wiring is selected per art piece on the Controller. |
| Sequencing decision | `Assets/core/Switching/Director.cs` | Wire-change reducer (ADR-0011): Standalone/Synced/Hold decision layer, two Cue Sheets repaired by invariant each new-beat wake, lazy preference-based casting, staged choices, and fire-and-forget cue handoff. Holds no decision memory. |
| Cue Sheets | `Assets/core/Switching/CueSheet.cs` | Index of Cue Marks over an announced phrase length: marks on Grid Boundaries, gaps of one to four Grids, phrase end always marked; layout is an announcement-seeded random roll. Grid and Phrase truth themselves come from the wire via `BeatManager`. |
| Cue/casting | `Assets/core/Switching/Deck.cs`, `Assets/core/Effects/Repertoire.cs` | Rotating card decks and Effect/Transition Repertoire behind lazy, preference-based casting when a Grid carrying a Cue Mark begins; cards are pulled only on Switcher acceptance. |
| Mechanical execution | `Assets/core/Switching/Switcher.cs` | ShowNow/StartTransition/RenderAtTime execution, sole owner of cue commitment (one beat-domain lock; private runway/tail/lock math; loading a cue returns accepted-or-not), Switcher-held Loaded Cue scheduling, and active A-to-B progress. |
| Effects | `Assets/core/Effects/EffectBase.cs`, `Assets/effects/*.cs` | Generate 900-tile frames; concrete Effects own Repertoire, Waveform acquisition, and every artistic mapping from shared musical facts/tools. |
| Screen effects | `Assets/core/Effects/ScreenEffect.cs` | Map rectangular screen buffers onto the Penrose tile layout. |
| Mixers/wrappers | `Assets/core/Effects/MixerBase.cs`, mixer effects | Remain one Effect publicly; privately own/configure child Effects and combine or transform their buffers. |
| Transitions/settings | `Assets/core/Transitions/TransitionBase.cs`, `Assets/core/Transitions/TransitionSettings*.cs`, `Assets/transitions/*.cs` | Blend effect A to effect B; concrete Transitions own musical response while settings declare Runway/Tail/Shape/Intensity defaults and saved tuning. |
| External blenders | `Assets/core/Blending/BlenderBase.cs`, `Assets/blenders/*.cs` | Mix incoming pixel-source data with the native Penrose buffer. |
| Palette | `Assets/core/helpers/GPalette.cs` | Global palette sampling and animated palette transitions. |
| Rhythm Data Surface | `Assets/core/Rhythm/BeatManager.cs`, `*Doorway.cs`, `SpanView.cs`, `Edges.cs`, `Duration.cs` | One live/Standalone musical gateway, split by concept into frame-coherent nullable facts, total Edges, pulses, and Stock Envelopes. |
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
- `Controller - nova.cs` is inactive reference code under `#if false` and references missing/incompatible concepts.
- Optional telnet code is inactive by default and should be revisited before re-enabling.
