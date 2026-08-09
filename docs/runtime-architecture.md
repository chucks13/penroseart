# PenroseArt Runtime Architecture

PenroseArt is a Unity-hosted C# runtime for the Penrose Wall LED installation. Unity provides the scene host, editor workflow, preview mesh, UI text, keyboard input, and platform packaging. The visual system itself is mostly plain C# classes that render into fixed-size color buffers and then send those buffers to both the Unity preview mesh and hardware output.

This document describes how the runtime is built and how it behaves. The vocabulary it uses — Cue Mark, Runway, Grid, Focus, Performer, and the rest — is defined once in [`CONTEXT.md`](../CONTEXT.md); durable decisions and their rationale live in [`docs/adr/`](adr/).

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
       ├─ planning: Director builds six track-scoped TrackCueSheet slots
       ├─ sequencing: Director hands over the on-air focus player's sheet and answers cue decisions
       ├─ execution: Switcher holds and performs the in-force sheet against its player's clock
       ├─ overlays/blending: drums, optional camera, optional PixelReceiver
       ├─ diagnostics: CueLog per-session sequencing trace file
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
5. In Synced Mode, the Director hands the `BeatManager.LiveOrder.Focus` player's current sheet to the Switcher with `Switcher.Cast(sheet)`. Cast is a handover; it does not fire a cue.
6. `Switcher.Tick()` thinks once per Grid, at the Grid's start, from the on-air Grid — the timing authority. If the next boundary carries an unfired, non-self-blend Cue Mark, its Transition fires at boundary minus Runway and the mark is permanently checked off. Marks skipped by a forward jump lapse as Missed Cues — no late firing.
7. Otherwise, anomalies — a re-crossed fired mark, a mark moving into the Effect already on the wall, or Stillness — go through one doorway: the Switcher reports a Sighting and the Director decides — ride through, or a fresh Director-cast cue. A taken cue uses the normal scheduler to land at the same closing boundary; the sheet itself never changes.
8. `Switcher.RenderAtTime(...)` renders the current Effect or active A-to-B Transition into `penrose.buffer`.
9. Filters, drums, camera, and external pixel blending may modify `penrose.buffer`.
10. The active UDP/ACN path (or the serial path, when `ENABLE_SERIAL` is defined) sends the frame to hardware.
11. `Penrose.UpdateModelColors()` applies the buffer to the Unity preview mesh and HUD/OSC status is updated.

## Sequencing model

[`docs/switching-model.md`](switching-model.md) is the single source of truth for switching behavior — the model this runtime is being rewired to match. Where the code still disagrees with the model, the model wins and the code is the defect. ADR-0009 defines the durable rule: the **Director directs** and the **Mechanical Switcher executes**.

The Director owns the decision layer: which Performer should be on stage and which Transition should move between A and B. The Switcher owns the in-force Cue Sheet and its check-offs, decides when its marks are due, and owns the in-flight mechanical execution: source Effect, target Effect, active Transition, progress, and completion. It does not own a pending or loaded-cue lifecycle.

### Standalone Mode

Standalone Mode is the intentional self-running behavior whenever no usable musical clock is present — whether nothing is connected at all, or OSC is connected but no track is playing or yet analysed. Standalone lives inside the Director (`TickStandaloneMode` → `Timer` → `RunStandaloneTimerDecision`) and starts moves through `Switcher.StartTransition`, the shared stage primitive. The sync machinery — sheets, marks, stillness — never runs in or disturbs Standalone: entering Standalone clears the sheet slots and the in-force sheet (ADR-0003). Timer expiry is not an independent sequencer; `Director.OnTimerFinished()` ignores timer completion while Synced Mode is active.

### Synced Mode

Synced Mode is active when BeatManager's usable musical clock is present (`IsSynced`); transport connectivity alone does not decide the mode. `BeatManager.LiveOrder.Focus` selects the sheet the Director hands over. The Switcher executes against the on-air beat and Grid — the timing authority; the wire guarantees on-air values equal the focus player's values in the same capture, and per-player surfaces serve song structure and sheet building. The Director never follows musical position, reads the Grid, or self-ticks a musical count.

`TrackCueSheet.Build(...)` is the single creative planning seam. When a player's complete structure generation changes, it builds one full-track plan into that player's slot, seeded by structure generation plus player number and the per-run salt (ADR-0008). The plan contains every Cue Mark with its Effect and Transition already assigned: marks sit at sensible, irregular spacing — never clumped, never metronomic — and never leave more than 64 beats (4 Grids) without a transition. Effects are dealt from bag order alone; capability is asked only of an Anchor's ride-through carrier, and no transition is written from an effect into itself. Around a Drop or Fill a capable Effect is already on the wall and no Transition's Runway or Tail crosses the moment; on a short Grid the cast Transition's Runway fits. Phrase boundaries are preferred mark positions, not mandatory transitions.

At runtime the Director hands the focus player's current sheet to the Switcher on every synced tick; `Cast(sheet)` is an idempotent handover for the same player and structure generation, and a handover changes nothing on the wall by itself. The Switcher thinks once per Grid, at the Grid's start. An unfired, non-self-blend Cue Mark at the next boundary fires at boundary minus Runway and is permanently checked off. Otherwise, a re-crossed fired mark, self-blend, or Stillness goes through the one Off-Plan doorway; a taken cue uses the normal scheduler to land at the same boundary. Marks skipped by a forward jump lapse as Missed Cues — not performed late — and once started a Transition is fire-and-forget. The loop lane may corroborate traces and diagnostics, but never selects behavior (ADR-0011).

`Switcher.BindDirector(...)` binds the decider after construction because the Director and Switcher references are mutual.

### Planned cadence and always-on Stillness

**The Director's plan-time rule** applies while `TrackCueSheet.Build(...)` walks a track's Phrase map: never leave more than 64 beats (4 Grids) without a transition, and place marks at sensible, irregular spacing — never clumped, never metronomic. There is no numeric spacing floor; the other invariant is that no two blends overlap, guaranteed by casting transitions that fit the space they are given. Both are properties of the written plan, settled before a single beat is performed.

**The Switcher's run-time rule** is Stillness — whole Grids since the last fired cue, a property of the wall, not of any sheet. It increments at every Grid-start think and resets only when a cue fires. Three still Grids make the fourth Grid's deal a take, short or not. Re-crossed fired marks, self-blends, and Stillness all use the same Off-Plan doorway, and no loop state gates it.

### Fire-and-forget, and the override contract

**Nothing changes a Transition in flight.** Once `Perform(...)` has started a move, that move plays out as it left — no re-targeting, no re-timing, no re-deciding from the clock. A pick made mid-flight applies to the next move.

The override surface — staged Next Effect / Next Transition, Hold Selected, Held Effect, and Show Now — is debugging and verification tooling for testing Performers, never show behavior; it must never degrade the show model.

Staged and held Effect/Transition choices mask the assignment when the Switcher asks `Director.DecideCue(...)` or `Director.DecideOffPlanCue(...)`; they never mutate the sheet. Both return a `CueDecision`; `Perform == false` means Hold or a ride-through, and the Switcher checks nothing off.

Show Now is the pushed counterpart to a staged pick: an operator choice starts a real Transition into the picked Effect at that instant, with the staged card and no Runway, because an off-grid interjection has no Cue Mark for an Impact Point to fly toward. There is no cut path. The plan in force is left standing — nothing is cleared or re-cast — and it resumes at its next unfired mark.

The fire math follows the **decided** Transition, not the planned one. The Grid-start think asks `Director.DecideCue(mark)` once — which is where a one-shot override is consumed — and schedules the blend at `mark.Beat - RunwayBeats` of the Transition the answer names, so an override carrying its own Runway leaves on its own beat and its Impact Point still lands on the Cue Mark. There is no peek surface: one ask, one decision, one scheduled act.

The consequence is the override's timing contract: staged overrides apply from the next think onward. An override staged before the Grid-start think that decides a mark performs that mark. An override staged after the think does not touch the already-scheduled act — fire-and-forget extends to the scheduled act — so it takes effect at a following think instead. Late staging delays an override; it never produces a rushed or off-mark hit.

### Transition timing

A Transition's `TransitionRepertoire` declares its beat timing with two authored values and one derived read:

- **`RunwayBeats`** (authored): beats before the chosen Cue Mark when the Transition must start.
- **`TailBeats`** (authored): visual resolution after the hit. `DurationBeats` is the sum; `MaxDurationBeats` caps it at 12, leaving room inside the nominal 16-beat cadence.
- **`ImpactPoint`** (derived, `RunwayBeats / DurationBeats`): where the authored Runway and Tail have already placed the visual hit inside the move, normalized to progress. It is a read of the declaration's shape, not a value anything sets or schedules by — the runtime schedules from Runway alone.

The Switcher uses Runway to decide when a Cue Mark is due and starts the Transition so its Impact lands on that mark. A late entry does not compress the Runway: a cue that cannot fly its Runway is missed instead, so the anchored start time is never behind the playhead. `Switcher.Cast(...)` only hands over a sheet and starts no Transition itself. Tail completion and Switcher progress are execution facts only, not musical scheduling inputs. Saved `Assets/transitions/Resources/TransitionSettings/*.asset` values participate in the live Transition Repertoire through `TransitionSettingsProvider`, so code defaults alone are not the full runtime truth.

## Musical data and tools

`BeatManager` is the one read-only musical gateway for the whole application: anything needing a musical fact reads it there, which is why nothing else reads OSC directly. `RaveOscReceiver.ApplyTo(...)` applies the latest live on-air snapshot before `BeatManager.Update()` captures the frame. Without a usable live clock the wall is deliberately in Standalone Mode — a preference, not a fallback.

The Data Surface is shallow and frame-coherent: `Timing`, `Track`, `Beats`, `Offbeats`, `Pulses`, `Phrase`, `NextPhrase`, `Drop`, `Fill`, `Energy`, `NextEnergy`, `Loop`, `Grid`, `Players`, `LiveOrder`, and always-present `Levels`. Individual wire values read `null` when unavailable; derived values sit beside the wire values they describe. Consumers own any previous-frame comparisons, and `IsSynced` is the single mode authority. `docs/beat-manager.md` describes the surface; ADR-0005 governs read-only serving.

`Waveforms` is a sibling acquisition surface, not a child of BeatManager. The Controller owns one instance and exposes it to Effects and Transitions as `waveforms`. Performers acquire immutable, clock-bound `Waveform` values by Energy or compose a `Routine`; each held value reads its own `Envelope` or maps it through `Lerp(from, to)`. `Waveforms.None` is the explicit non-null value a Mixer assigns to suppress a child's response.

Both surfaces provide musical facts and tools, never artistic response policy (ADR-0007). Concrete Effects and Transitions choose acquisition timing, endpoints, mapping, fallback, and any local response state; their bases acquire nothing automatically. A Mixer is one Effect publicly: it owns its child instances and may directly configure their public artistic state, and those choices stay private to the Mixer.

## Palette

`GPalette` / `AnimPalette` is the shared color-management and animation system.

`EffectBase.APalette` is static, so every Effect shares one cohesive palette state. `Controller` updates palette animation each frame and can trigger a global palette shift or reload from the `Return` key. Effects query colors by normalized position, which keeps palette detail separate from generative logic. Palette data loads from `Assets/StreamingAssets/palettedata.txt`.

## Diagnostics: the Cue Log

`Assets/core/Runtime/CueLog.cs` is the per-run diagnostic sink for sequencing traces. It writes one session file, `penrose-<yyyyMMdd-HHmmss>.log`, under `Application.persistentDataPath/Logs`, holding one timestamped line per event.

The Cue Log owns its session file directly — there is no writer seam, no injected sink interface, and no configurable target. `CueLog.CreateForSession(logsDir)` names the file and rotates older `penrose-*.log` files away so only the newest `MaxSessionLogs` (20) survive; the file itself opens lazily on the first line, so a run that never traces leaves nothing behind. `Dispose()` flushes and closes it, called from `Controller.OnDestroy()`.

The sink owns the file, not the record format. Callers hand it a finished line, and the vocabulary of those lines belongs to whoever writes them — today `Controller.LogDirectorSwitching(Func<string> message)`, which prefixes frame and time and takes a deferred `Func<string>` so trace text is never built when no sink exists. That split is what lets the trace vocabulary change with the runtime without touching file plumbing.

Failure is always contained: any I/O error disables the sink and warns once rather than throwing, because a broken log must never take the wall down mid-performance. `Controller.cueLog` is `[NonSerialized]` and `[HideInInspector]` — it is runtime state, not authored data, and Unity cannot serialize it.

The log is strictly downstream. Nothing in the sequencing path reads it back, and its presence or absence changes no runtime behavior.

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

Standalone/manual paths can still call `Switcher.StartTransition(...)` explicitly. Synced handoff calls `Switcher.Cast(sheet)` with the focus player's Cue Sheet, then `Switcher.Tick()` starts each due move after asking the Director for its `CueDecision`. The Switcher promotes the destination Effect after the Transition completes. If an explicit transition is issued while one is still rendering, the Switcher replaces the mechanical move using the previous destination as the new source.

## Deck selection and staging

Standalone selection uses rotating integer decks.

1. Initialize a deck as `[0, 1, 2, ... count - 1]`.
2. Pick a random index from the top half of the deck.
3. Remove that entry.
4. Move it to the bottom.

This gives variety without immediate repeats while still eventually cycling through the catalog.

Synced selection is separate: `TrackCueSheet.Build(...)` deals seeded shuffle bags over the complete Effect and Transition catalogs and bakes the results into the plan. A drop/fill Anchor scans the Effect bag for a capable ride-through carrier — the Effect that must already be on the wall for the moment — and the moment itself is cleared of Transitions. Every Transition is dealt to fit the space it is given: its Runway inside the free interval behind the mark, its Tail short of the next Anchor moment and of the next mark's smallest possible Runway, so no two blends can overlap and no blend can cross a landing — including across short Grids, where there is no fixed spacing floor to fall back on. `TrackCueSheet.DealOffPlanCueAt(...)` provides a deterministic fresh deal for the Off-Plan doorway, excluding whatever the wall already holds.

Each bag is dealt top-card and reshuffled once it empties. The reshuffle keeps the card just dealt off the top of the new permutation, because the seam between two passes is the one place a fair bag would otherwise deal the same card twice running — and an Effect dealt twice running bakes a Transition from a card to itself, which moves nothing. Filtered deals — a ride-through carrier's capability scan, a Transition's fit scan — dig past the top card and prefer any match that is not the card just dealt; an encore from the discard pile hands a spent card out again only when the remaining cards hold no match.

The Director keeps staged **Next Effect** and **Next Transition** choices as override masks. `SetNextEffect(...)` and `SetNextTransition(...)` replace exactly the next performed assignment; their Hold variants keep replacing that side on later decisions. Releasing a hold returns to the unchanged plan.

## Held Effect override

`Controller.heldEffect` is the whole control: the `-1` Random sentinel lets the wall rotate normally, and any non-negative catalog index holds that Effect until Random is chosen again. `Controller.TryGetHeldEffectIndex(...)` is the single read, and an out-of-range index degrades to Random rather than throwing. Three input surfaces write that one field — the shared `Effect / Hold` dropdown (Controller inspector and Tuning Window), Escape as the quick release, and the TouchOSC grid, whose cell press holds and whose release button returns to Random.

Hold stops switching along both drive paths, because the two modes drive the wall differently:

- **Synced pulls.** `Director.Decide(...)` — the choke point under both `DecideCue(...)` and `DecideOffPlanCue(...)` — returns `CueDecision.Frozen` while held, so the Switcher performs nothing and checks nothing off. A mark passed under a hold stays unfired and simply lapses. Plan maintenance in `TickSyncedMode` is untouched: sheets keep building and handing over, so a release resumes against a current plan.
- **Standalone pushes.** There is no sheet to refuse — the Director's own `standaloneTimer` cues the Switcher — so `TickStandaloneMode(...)` returns before `standaloneTimer.Update(...)` while held. The clock stops rather than rewinding, and `DirectorStatus.IsStandaloneCadenceFrozen` reports that state and only that state.

`Director.ApplyHold()` engages the selection: it starts a Show Now toward the held Effect unless `Switcher.TransitionTargetEffectIndex` already matches, so a move already in flight toward that destination is left to land.

Hold is not a second sequencer and does not command around the Director. It exists so a developer can inspect and tune one Effect live.

## Buffer flow

```text
Switcher-rendered Effect or Transition buffer
  -> penrose.buffer
  -> optional filter/drum/camera/pixel-source blending
  -> UDP or serial hardware output
  -> Penrose.UpdateModelColors() for Unity preview
```

`Penrose.Total` is 900 logical tiles. Both hardware output paths expand those logical tiles through the Controller's flattened wire map into physical LED order — the UDP path into E1.31/ACN universes, the serial path into packets for the S2 Mini / ESP32 boards.

## Wall data files

The physical wall is described by two `//`-commented plain-text files in `Assets/StreamingAssets/`, parsed through the `LayoutData` / `WiringData` contracts in `Assets/core/Runtime/WallData.cs`:

- `penrose_layout.txt` — the Penrose pattern itself. Fixed; the same for every art piece.
- `wiring_*.txt` — the LED wiring order, which differs per art piece. Selected by the `WIRING_*` define at the top of `Controller.cs` (`WIRING_6X5` or `WIRING_ORIGINAL`; omitting both is a compile error), and read from StreamingAssets at startup so it stays hand-editable next to a built player.

## Output paths and build symbols

The project uses conditional compilation for optional output and control paths.

| Symbol | Effect |
| --- | --- |
| `ENABLE_SERIAL` | File-defined at the top of `Controller.cs`; when defined, makes USB serial the active output path. Currently commented out, so the compiled output is UDP/E1.31. |
| `ENABLE_TELNET` | Enables the remote command-line interface on port 23. Inactive by default; revisit before re-enabling. |
| `ENABLE_BLENDING` | Enables `PixelReceiver` and dual-source frame blending. |
| `PREP_CAPTURE` | Enables localhost pixel feedback/capture helper behavior and a synthetic blend source for testing. |

**Active output** is ACN/E1.31 UDP through `Controller.sendUDPFrame()` / `sendACN()`, targeting the destination IP from the Controller's `IP` field / UI input. **Serial output** through `SerialOut` (`sendSerialFrame()` sends wire-mapped physical LED order to the S2 Mini / ESP32 boards) compiles in only when `ENABLE_SERIAL` is defined; it is currently disabled after issues with serial in use.

Standalone API compatibility is intentionally `.NET Standard 2.1`; desktop `System.IO.Ports` support comes from platform-specific plugin assets under `Assets/Plugins/System.IO.Ports/` for macOS, Windows, and Linux x64. Android, iOS, and WebGL are not covered by that plugin setup — if they become production targets they need either serial-disabled builds or a platform-specific USB serial transport.

Control and input paths are OSC (`OSCReader`, `RaveOscReceiver`), optional `PixelReceiver` blending, drum overlays, keyboard shortcuts, and the optional telnet/debug path.

## Major subsystems

| Area | Main files | Responsibility |
| --- | --- | --- |
| Runtime hub | `Assets/core/Runtime/Controller.cs` | Unity host for catalogs, lifecycle, input routing, output routing, overlays, preview update, and the per-frame call order. |
| Geometry/model | `Assets/core/Runtime/Penrose.cs` | Layout data, tile metadata, Unity mesh generation, buffer-to-mesh colors. |
| Wall data files | `Assets/core/Runtime/WallData.cs` | `LayoutData`/`WiringData` contracts for the `Assets/StreamingAssets/` text files; layout is fixed, wiring is selected per art piece on the Controller. |
| Sequencing decision | `Assets/core/Switching/Director.cs` | Standalone cadence plus Synced planning and decisions: maintains six track-sheet slots, hands over the focus player's sheet, and answers the due-mark question and the one anomaly doorway (`DecideOffPlanCue`: a re-crossed fired mark, a self-blend mark, or Stillness — ride through or a fresh dealt cue, never the on-wall Effect or the one being moved toward) with override-aware `CueDecision` values, remembering nothing between asks. |
| Track Cue Sheets | `Assets/core/Switching/TrackCueSheet.cs` | Pure full-track plan builder with baked Effect/Transition assignments, seeded bags, drop/fill Anchor casting and clearance, and deterministic off-plan deals. |
| Cue/casting | `Assets/core/Switching/Deck.cs`, `Assets/core/Effects/Repertoire.cs` | Rotating Standalone decks plus the capability and timing declarations consumed by track-sheet planning and Switcher execution. |
| Mechanical execution | `Assets/core/Switching/Switcher.cs` | Holds the handed-over sheet and its permanent check-offs; thinks once per Grid at Grid start from the on-air beat and Grid, gives an unfired non-self-blend planned Cue priority, owns Runway/Impact/Tail timing and always-on Grid-counted Stillness, and reports every anomaly through the Director's one Off-Plan doorway. |
| Effects | `Assets/core/Effects/EffectBase.cs`, `Assets/effects/*.cs` | Generate 900-tile frames; concrete Effects own Repertoire, Waveform acquisition, and every artistic mapping from shared musical facts/tools. |
| Screen effects | `Assets/core/Effects/ScreenEffect.cs` | Map rectangular screen buffers onto the Penrose tile layout. |
| Mixers/wrappers | `Assets/core/Effects/MixerBase.cs`, mixer effects | Remain one Effect publicly; privately own/configure child Effects and combine or transform their buffers. |
| Transitions/settings | `Assets/core/Transitions/TransitionBase.cs`, `Assets/core/Transitions/TransitionSettings*.cs`, `Assets/transitions/*.cs` | Blend effect A to effect B; concrete Transitions own musical response while settings declare Runway/Tail/Shape/Intensity defaults and saved tuning. |
| External blenders | `Assets/core/Blending/BlenderBase.cs`, `Assets/blenders/*.cs` | Mix incoming pixel-source data with the native Penrose buffer. |
| Palette | `Assets/core/helpers/GPalette.cs` | Global palette sampling and animated palette transitions. |
| Sequencing diagnostics | `Assets/core/Runtime/CueLog.cs` | Per-session trace file: owns naming, rotation, lazy open, and contained failure; callers own the record vocabulary. |
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
- `TrackCueSheet.cs` and `Director.cs` are both large enough to be the next reading-cost problem after `Controller`.
- `Controller - nova.cs` is inactive reference code under `#if false` and references missing/incompatible concepts.
- Optional telnet code is inactive by default and should be revisited before re-enabling.
