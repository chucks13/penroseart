# Penrose Simulator Context & Architecture Guide

## Overview

This project is a real-time controller for the Penrose Wall light installation. It generates generative visuals for a 900-tile Penrose model in Unity and currently outputs to LED hardware through high-speed USB serial (`SerialOut`) for S2 Mini / ESP32 boards.

The older ACN/E1.31 UDP output path still exists in `Controller.sendUDPFrame()` / `sendACN()`, but the active build path is serial because `Assets/core/Runtime/Controller.cs` file-defines `ENABLE_SERIAL`.

## Core Components

### 1. Controller.cs (The Singleton Hub)

Hosts the Unity frame loop, catalogs, overlays, input, preview, and hardware output. It creates the Director and Mechanical Switcher, then calls them in the correct order; it is no longer the sequencing state machine itself.

- **Deck System**: Effects/transitions are drawn from rotating decks for variety. The Director owns when those staged choices are consumed.
- **Sequencing**: In Standalone Mode, the Director uses the timer as its self-running cadence. In Synced Mode, the Director wakes once per beat on live Rave OSC / BeatManager state, keeps two Cue Sheets repaired, and casts a Cue when a Grid carrying a Cue Mark begins. The Mechanical Switcher executes the chosen move.
- **Transition Timing**: Transition Settings declare Runway and Tail. The Director chooses the Cue Mark, Effect, and Transition; the Mechanical Switcher uses that Transition's timing shape to start and progress the move so its Impact Point lands on the Cue Mark. Tail completion is visual execution, not a musical scheduling input.
- **Held Effect**: A single selection that either lets the wall rotate or pins it to one effect. The **Random** state lets the Deck System rotate normally; choosing a specific effect *holds* it, suppressing both rotation and transitions until Random is chosen again. Selected from the Inspector's effect dropdown (Random is the default first entry); the `Escape` key always returns to Random. The blank effect template is never offered, since it is excluded from the runtime effect catalog.

### 2. Beat Manager (Synchronization)

Provides one read-only musical gateway for Effects, Transitions, the Director, and debug tooling. `RaveOscReceiver` applies the latest live on-air snapshot before `BeatManager.Update()` captures the frame; without a usable live clock, the wall is deliberately in Standalone Mode.

- **Data Surface**: `BeatManager` exposes shallow, frame-coherent value groups organized by musical concept: `Timing`, `Track`, `Beats`, `Offbeats`, `Pulses`, `Phrase`, `NextPhrase`, `Drop`, `Fill`, `Energy`, `NextEnergy`, `Loop`, `Grid`, and always-present `Levels`. Individual wire values use `null` when unavailable; derived values stay beside the wire values they describe. Consumers own any previous-frame comparisons. `IsSynced` is the single mode authority.
- **Waveforms**: Controller owns one sibling `Waveforms` acquisition surface and exposes it to Effects and Transitions as `waveforms`. Performers explicitly acquire immutable, clock-bound `Waveform` values or compose a `Routine`; each held value reads its own `Envelope` or maps it through `Lerp(from, to)`.
- **Performer ownership**: BeatManager and Waveforms provide musical facts and tools, never artistic response policy. Concrete Effects and Transitions choose acquisition timing, endpoints, mapping, fallback, and any local response state; their bases do not acquire or replace musical values automatically.
- **Mixer internals**: A Mixer is one Effect publicly. It owns its child instances and may directly configure their public artistic state, including assigning `waveforms.None` to suppress a child's Waveform response; those choices stay private to the Mixer.

### 3. Buffer and Effect System

The runtime works on `UnityEngine.Color[]` buffers sized to `Penrose.Total == 900`.

- **Effects**: Inherit from `EffectBase` and fill their local 900-color `buffer`.
- **ScreenEffects**: Render into a rectangular virtual screen and map that image onto the irregular Penrose tile layout through precomputed interpolation weights.
- **Mixers/Wrappers**: Inherit from `MixerBase`, own child effects, and combine or transform child buffers.
- **Transitions**: Inherit from `TransitionBase` and blend between two effect buffers by effect index.
- **Penrose.cs**: Holds the physical model, tile metadata, layout data, mesh generation, and buffer-to-mesh color mapping. Layout comes from `Assets/StreamingAssets/penrose_layout.txt` (the pattern; fixed), wiring from `Assets/StreamingAssets/wiring_*.txt` (per art piece); both are `//`-commented text files parsed by `Assets/core/Runtime/WallData.cs`; the wiring file is selected by the `WIRING_*` define at the top of `Controller.cs` and read from StreamingAssets at startup (hand-editable next to a built player).

### 4. Palette System (GPalette / AnimPalette)

A shared color-management and animation system.

- **Global Coordination**: `EffectBase.APalette` is static, so all effects share a cohesive palette state.
- **Runtime Control**: `Controller` updates palette animation and can trigger global palette shifts or reloads via the `Return` key.
- **Integration**: Effects query colors using normalized positions, allowing palette details to remain separate from generative logic.

### 5. Input and Output

- **Primary output**: USB serial via `SerialOut`, using `sendSerialFrame()` to expand the 900 logical Penrose tiles through the Controller's wire map (flattened from the selected `Assets/StreamingAssets/wiring_*.txt` file) into the physical LED order.
- **Serial runtime support**: Standalone API compatibility is `.NET Standard 2.1`; desktop `System.IO.Ports` support is supplied by platform-specific plugin assets under `Assets/Plugins/System.IO.Ports/` for macOS, Windows, and Linux x64.
- **Fallback/legacy output**: ACN/E1.31 UDP code remains present in `sendUDPFrame()` / `sendACN()` and is used only when serial is not compiled in.
- **Control/input paths**: OSC (`OSCReader`), optional PixelReceiver blending, drum overlays, keyboard shortcuts, and optional telnet/debug paths.

## Build Symbols and Platform Notes

The project uses conditional compilation for optional output/control paths.

- `ENABLE_SERIAL`: Currently file-defined in `Controller.cs`, so serial is the active output path for the compiled controller.
- `ENABLE_TELNET`: Enables the remote command-line interface on port 23.
- `ENABLE_BLENDING`: Enables the `PixelReceiver` and dual-source frame blending logic.
- `PREP_CAPTURE`: Enables localhost pixel feedback/capture helper behavior.

Android, iOS, and WebGL serial support are not covered by the desktop `System.IO.Ports` plugin setup. If those become production targets, they need either serial-disabled builds or a platform-specific transport.

## Operational Logic

1. **Initialization**: `Controller` initializes `Penrose`, discovers effects/transitions/blenders through `Factory<T>`, configures UI fields, starts OSC/control helpers, creates the Director and Mechanical Switcher, and initializes serial output when enabled.
2. **Timing and sequencing**: Each frame applies live Rave OSC to `BeatManager`, updates `BeatManager`, then ticks the Director. The Director chooses Standalone, Synced, or Hold behavior, repairs its two Cue Sheets and casts the Cue for a beginning Grid's Cue Mark, and sends fire-and-forget cue directions to the Switcher when a stage-directed Cue should commit.
3. **Rendering**: The Switcher renders the active Effect or A-to-B Transition into a 900-color buffer; overlays/blenders can modify it.
4. **Output**: The active serial path maps the Penrose buffer to physical LED order and sends frames through `SerialOut`; the legacy UDP path maps the same data into ACN/E1.31 universes.
5. **Scene update**: `Penrose.UpdateModelColors()` applies the current buffer to the Unity mesh for visualization.

## Language

> Shared glossary for **every** term used across the Penrose project — rhythm, visuals, hardware, control, tooling, anything we need a single agreed meaning for so everyone is on the same page. Not limited to beat/rhythm. Definitions describe what each concept *is*, not how it is implemented. (The sections above are an architecture guide and intentionally do carry implementation detail; this section does not.) Add a term here the moment it needs a canonical meaning.

**Play Mode**:
Unity Editor state where the wall runtime is actually running: effects render, the Director makes choices, transitions execute, inputs may be live, and authoring tweaks are judged by watching the moving wall. Tuning changes made in Play Mode should persist when the run stops unless explicitly discarded.
_Avoid_: treating Play Mode edits as throwaway previews; confusing Play Mode with a built standalone Player.

**Edit Mode**:
Unity Editor state where the wall runtime is not running. Authoring changes made here configure the next run but are not being judged against a live moving wall.
_Avoid_: assuming Edit Mode authoring is the only durable authoring path; forcing creative tuning to happen without seeing the wall in motion.

**Tuning Window**:
The Unity authoring window used to watch and adjust Performers while the wall is running or stopped. It shows Effects and Transitions as lists, displays the selected Performer's Settings, and can steer the Director's Next Effect or Next Transition without taking timing ownership away from the Director.
_Avoid_: burying live creative tuning in ordinary Inspector fields; making a fake preview path that does not exercise the Director and Switcher.

**Hold Selected**:
A Tuning Window mode where the selected Effect or Transition remains the Director's next choice after each move completes. Turning it off returns that choice to normal random selection.
_Avoid_: confusing this with Held Effect; Hold Selected keeps the Director/Switcher path running, while Held Effect freezes rotation around one on-wall Effect.

**Waveform**:
A one-bar rhythmic brightness envelope built by **merging humps end-to-end in time** — each hump occupies its own time slot and has a width (Duration) and a height (Amplitude). Humps are never summed or layered on top of each other. Values are **unipolar `[0..1]`**: 1 at a peak (on the beat), 0 in the troughs between beats. It is an envelope, never a bipolar audio wave — there is no negative half and 0 is the trough, not a midpoint.
_Avoid_: "adding waves together" (they are concatenated in time, not summed); "true wave" / "−1 to 1" (it is unipolar); "signal", "curve".

**Hump**:
The single unit a Waveform is built from: one rise-and-fall occupying its own time slot, peaking once and returning to 0. A Waveform is an ordered run of Humps merged end-to-end. Each Hump carries a width (its Duration / note value) and a height (its Amplitude).
_Avoid_: "cycle", "wave", "pulse" for the unit — those name the whole signal, not the piece.

**Amplitude**:
The height of a single Hump, authored as a single digit `0–8` mapping linearly to `[0..1]` via digit ÷ 8 (`8` = full height, peak reaches 1; the ÷8 gives nine clean eighth-steps that land exactly on 1.0). One digit per Hump, read straight across in order, so the amplitude string sits directly beneath the sequence string as a stacked, equal-length pair. `0` makes the Hump silent — flat at 0 for its whole slot — which is how a beat is *skipped* (e.g. "measure start" = `8000`, "alternating beats" = `8080`). There is no separate gate; Amplitude `0` is the gate.

**Duration** (a.k.a. note value, the Hump's width):
How much musical time a note occupies, named by note value rather than a count. One shared ladder serves both sides of the musical vocabulary: a Hump's width **occupies** a Duration, and a notation pulse or gate **runs every** Duration. The authored range is `W` whole (the full bar), `H` half (2 beats), `Q` quarter (1 beat), `E` eighth (½ beat), `S` sixteenth (¼ beat). One token per Hump; the tokens of a Waveform, read left to right, are its widths. The sixteenth is the fastest allowed — finer rates are deliberately excluded (both musically unneeded and a full-wall flicker hazard).
_Avoid_: the retired name "Subdivision" (renamed Duration everywhere, glossary and code alike); "frequency" or per-beat counts — these are note values, and a value slower than a quarter (whole/half) spans several beats, which a per-beat count cannot express.

**Duration Pulse / Duration Gate**:
The idealized-clock members of the pulse family: signals derived from the Bar Phase clock that run every **Duration** — "pulse me every eighth." A Duration Pulse peaks on each onset and decays smoothly to 0 across its cycle; a Duration Gate is its square on-off sibling, open for the first part of each cycle (strobes, ratchets). Deliberately distinct from the other three pulse offerings: the wire's `beat_pulse` (the sender's own analyzed hit), the Offbeat pulse (contrived from measured beat-time midpoints), and Waveforms (authored dance moves). Four offerings, four purposes — all valid options for effects.
_Avoid_: folding these into Waveforms (a Duration Pulse is parametric and instant, not an authored shape); the retired name "subdivision pulses/gates"; treating the four pulse offerings as duplicates of one datum.

**Waveforms**:
The shared Waveform acquisition surface beside BeatManager. Consumers draw immutable, clock-bound values from the required Pool; the held Waveform or Routine then reads its own current `Envelope` or applies caller-chosen endpoints through `Lerp(from, to)`. `None` is the explicit non-null value a Mixer uses to suppress a child's Waveform response.
_Avoid_: index addressing in any form (a Pool position may change at any time); nullable Waveform configuration; a provider-side `Evaluate` step; one-frame Waveform "Hit" state; reaching Waveforms through a BeatManager doorway (it is a sibling, not a child); runtime Waveform substitution or a "Wall Variant Lock" inspection path.

**Preset**:
A named, saved Waveform spec in the Pool — a human/editor label for a `sequence + amplitude + rounding + offset` bundle. Runtime performers draw values by Energy rather than depending on a stable name or Pool position. The plain Beat Pulse (`QQQQ` / `8888`) is the canonical default.
_Avoid_: treating Preset names as runtime identity, as a fixed hardcoded enumeration, or as an exhaustive description of the (effectively unbounded) Waveform space.

**Pool** (the curated Preset set):
The hand-vetted collection of Presets that random selection draws from, so a random pick is always musically sensible. It is the **required runtime source of truth**, persisted as a **hand-editable text file in `StreamingAssets`** — in the spirit of `palettedata.txt`: named entries, plain notation, `//` comments, blank lines ignored, creatable by hand in any text editor. `WaveformPool` owns the shared runtime/editor codec; Waveforms loads and binds the Pool for acquisition, while the Unity editor provides the authoring UI. A missing/empty Pool, a parsed notation-invalid entry, or an unsatisfied Energy set is a configuration error and fails visibly rather than synthesizing or widening to a fallback. The seven original rhythm Presets seed the default Pool.

**Routine**:
A 16-beat choreography — exactly one Grid — containing four resolved, clock-bound single-bar Waveforms composed with `Routine.Of(w1, w2, w3, w4)`. It reads exactly like a Waveform: `Envelope` is the current Grid bar's response and `Lerp(from, to)` maps that response. Without a placed Grid, `Envelope` rests at 0 and `Lerp` returns the caller's `to` endpoint. Routine retains no acquisition settings or replacement policy; a consumer wanting different values composes another Routine whenever it chooses.
_Avoid_: "Waveform Pattern" (retired name); any Routine length other than one Grid; using "pattern" for one bar's hump sequence; treating a Routine as a separate evaluation model; putting re-roll, refresh, wrap, or lifecycle policy on Routine, Waveforms, or a base hook; gating playback on Grid State.

**Visual Tool** (the waveform "designer" web app):
A standalone browser sketchpad for *seeing* what a Waveform's notation looks like before committing it. Purely a visualizer/design aid — it is not the authoring pipeline and the runtime does not depend on it or its exported JSON.

**Beat Pulse**:
The standard rhythmic signal: a value in `[0..1]` that peaks on the quarter-note beat and falls back before the next. It is the default/canonical Waveform — the plain every-beat Preset (`QQQQ` / `8888`).
_Avoid_: equating it with the raw OSC scalar; the runtime regenerates a shaped pulse locally; "the one all others are generated from" (retired mental model — Pool Waveforms are authored, not derived from the Beat Pulse).

**Bar Phase**:
The normalized position within the current measure (0 on the downbeat, 1 at the next downbeat), exposed as `BeatManager.Timing.BarProgress`. Every Waveform is evaluated against it. `Timing.BeatProgress` is the corresponding position within the current beat. Consumers may read either value directly instead of hand-rolling a private metronome.
_Avoid_: "beat phase" when the whole measure is meant; walling the clock off as Waveforms-private; per-effect hand-rolled metronomes.

**Bar** (a.k.a. **Measure**):
Four beats — "4-on-the-floor." The unit the Director's changes align to. The whole rhythmic structure runs on **powers of four**: 4 beats to a Bar, and Phrase lengths are multiples of four Bars (16, 32, 64 beats). A Bar boundary falls every 4 beats, but the Director changes no more often than every 4 Bars (16 beats).
_Avoid_: assuming arbitrary bar lengths — the wall assumes a 4/4, powers-of-four structure.

**The One**:
The first beat of a Bar — the downbeat. **Every Director change lands on the one**, never mid-bar. Counting is musical and **1-based: there is no beat zero** — the first beat is "the one," and a countdown to a change runs "4, 3, 2, 1, change," with the change landing on the *next* one. (The internal Bar Phase clock is a 0..1 normalization used for math; the one is the musical-facing count, not a zero index.)
_Avoid_: zero-based or off-by-one counting; landing a change mid-bar; conflating the musical count with the 0..1 Bar Phase value.

**Offbeat** (a.k.a. **Half-Step**):
The moment exactly midway between two beats — the "&". Four beats to a Bar means four offbeats. The wire carries nothing about the "&", so BeatManager derives four matching lanes. `Offbeats.OffBeatMs(count)` returns milliseconds until the selected 1..4 offbeat; `Offbeats.OffBeat(count)` returns its tempo-based active window.
_Avoid_: confusing "half-step" with its pitch-theory meaning (a semitone); the nearest-upcoming gate pick (retired for On Beat — both gates answer "am I on the moment" with current-slot semantics, one definition, not two); defining the offbeat as a Waveform — the position is the concept, the Waveform is one expression of it.

**On the Beat** (`OnBeat`):
Landing on the count — on the 1, 2, 3, or 4. The wire reports four triggers, each active for the first quarter of its beat interval. `Beats.OnBeat(count)` reads the selected trigger directly and `Beats.OnBeatMs(count)` reads that count's wire countdown. Counts are musical and one-based.
_Avoid_: the nearest-upcoming-count pick (retired for indirection — the current count is already named, so its gate is read directly; the earlier "reads false when a beat lands" justification was retracted against wire evidence); confusing On Beat (a gate) with the Beat Pulse (a continuous wave).

**Beat Count Countdown**:
The four wire countdowns exposed as `Beats.OnBeatMs(1..4)`. For example, at count one with a 400 ms beat they may read `0, 400, 800, 1200`. `Timing.BeatAverageMilliseconds` is the beat length; the per-count values say when each named count next lands. A consumer needing the soonest or the next count derives that tiny view locally.
_Avoid_: a zero count; conflating the countdowns with the average interval; adding a second aggregate spelling to BeatManager.

**Phase Offset**:
A per-Waveform shift, measured in beats, that slides the whole Waveform along the Bar Phase before it is evaluated. 0 leaves it on the beat; 0.5 lands it on the "&" (the Offbeat). Fractional values express swing/shuffle feel. It moves *when* the humps land without changing their shape or count.

**Rounding** (a.k.a. sharpness):
A per-waveform scalar in `[0..1]` controlling hump shape. At 0 the peak is sharp/pointed; rising first rounds the peak toward a cosine dome, then continues to grow a **flat top** — a plateau pinned at 1 around the beat. Higher rounding keeps the wall at full brightness for longer near the beat ("brighter longer"); the trough between beats still falls to 0 at every setting.
_Avoid_: "smoothing", "easing" (overloaded); treating it as a true low-pass filter.

**Contrived Value**:
A reusable value BeatManager derives from wire state, time, or multiple lanes: offbeats, progress, pulses, envelopes, energy trend, and level shaping. A **Wire Value** is passed through after sentinel translation. Both live side by side in the shallow musical group where a caller expects them; provenance never creates another navigation layer. Optional facts use `null`; `Levels` is the deliberate exception because silence and missing input both have a useful zero value while its followers fall according to their algorithms.
_Avoid_: "cooked"; effects reading the private wire snapshot directly; sentinel values crossing into effect math; separate raw/derived public trees.

**Data Surface**:
The read-only face of BeatManager through which effects, transitions, Waveforms, and other systems pull musical data. It is deliberately shallow: `Timing`, `Track`, `Beats`, `Offbeats`, `Pulses`, `Phrase`, `NextPhrase`, `Drop`, `Fill`, `Energy`, `NextEnergy`, `Loop`, `Grid`, and `Levels`. Each group places related wire facts and derived values together. Captured structs and owned collections prevent write-back; wire sentinels never cross the boundary. BeatManager exposes state and reusable math, not commands, consumer policy, or one-frame event identity.
_Avoid_: `View`/`Facts`/`Span`/`Current`/`Run` navigation; a separate raw tree; duplicate aliases; hub-owned `Started`, `Ended`, `Changed`, `Wrapped`, or gate-opened flags; color policy; dropping wire lanes because nothing reads them yet.

**Standalone Mode / Synced Mode**:
The two intentional personalities for rhythm-aware behavior. The dividing line is a single authority — whether a usable musical clock (the running 4-count) is present. **Synced Mode** is active whenever that clock is present; the wall syncs to whatever musical timing the signal currently provides. **Standalone Mode** is the self-running art behavior whenever the clock is absent — whether no OSC is connected at all, or OSC is connected but no track is playing or yet analysed — and it must look fully intentional on its own. One shared flag — spelled `IsSynced`, its only name — decides this for every consumer (effects, Director, HUD, Grid floor) so they can never disagree about which mode the wall is in. This is a preference, not a fallback: the wall prefers a live clock and works deliberately without one.
_Avoid_: deciding mode from transport connectivity or tempo instead of the running 4-count; multiple consumers each re-deriving the mode; `IsActive` (retired alias of `IsSynced`); effects that freeze, glitch, or go dark when the clock is absent; calling Standalone Mode a "fallback" or "default"; treating missing Track Phase (clock still running) as Standalone Mode.

**Director**:
The decision layer that owns *what* plays on the wall. Its whole job in Synced Mode: keep the current and next Phrases' Cue Sheets in existence, Cast the Cue when a Grid carrying a Cue Mark begins, and hand that Cue to the Switcher fire-and-forget. It wakes only on a new beat, reads musical truth only from BeatManager, and holds no state beyond its Cue Sheets.
_Avoid_: "choreographer" (the earlier name, retired); per-frame decision loops; reading OSC directly instead of BeatManager; mirroring Switcher state or remembering past decisions; making the Director draw buffers, run transitions, or own transition start/progress mechanics.

**Cue Sheet**:
A per-Phrase index of Cue Marks generated once from the Phrase's announced beat length: marks sit on Grid Boundaries, consecutive marks (and the run-in to the first) are at least 16 and at most 64 beats apart, and the final phrase boundary always carries one. Layout within those constraints is a creative roll — random today, energy-weighted later. Marks are empty: no Effect or Transition is chosen until Cast.
_Avoid_: putting Effect or Transition choices in the Cue Sheet; treating it as a queue of loaded Cues; rerolling just because same-length phrase timing shifted.

**Cue Mark**:
A beat position on a Cue Sheet where a stage-directed Cue should musically land. A Cue Mark belongs to Phrase structure; marks include selected 16-beat Grid Boundaries and the mandatory final phrase boundary. Fill/Drop state does not create or move Cue Marks; it only informs which Effect and Transition the Director should cast for an existing Cue Mark.
_Avoid_: calling a Cue Mark an Impact Point, Transition start, Transition Completion, or Selected Grid Boundary when speaking about the Phrase plan.

**Cast**:
Choosing the Effect and Transition for a Cue Mark's Cue. Casting happens lazily — when the Grid that loads the Cue begins, not when the Cue Sheet is built — so it reads the freshest wire truth. A Fill on this Grid or a Drop on the next Grid makes Fill/Drop-capable Repertoire *preferred*, never mandatory; a mandate would collapse variety onto the same few capable Performers.
_Avoid_: casting at Cue Sheet build time; consulting energy or other wire lanes when casting (Performers and Transitions read those themselves); treating the Fill/Drop preference as a hard filter.

**Loaded Cue**:
The Switcher-held stage-directed Cue for the next Cue Mark: which destination Performer, which Transition, and which musical mark the move should hit. It is mutable only inside the Switcher before its Lock Point; the Director sends cue directions fire-and-forget and does not inspect Loaded Cue state.
_Avoid_: loading multiple future Cues; treating a Loaded Cue as Switcher transition progress; putting pixel-level Effect commands in it.

**Armed Cue**:
A Loaded Cue that has crossed its Lock Point and is committed to Switcher execution. Phrase changes may still be observed, but they no longer change the armed Transition, destination Performer, or target Cue Mark.
_Avoid_: treating an Armed Cue as a Cue Sheet entry, a Director decision still in flux, or a multi-item queue.

**Lock Point**:
The beat after which a Loaded Cue can no longer change. The Lock Point depends on the chosen Transition's Runway so the Switcher has one committed beat before it must trigger the Transition.
_Avoid_: one global lock beat for every Transition; locking at the Impact Point; putting transition start math into the Cue Sheet.

**Next Transition**:
The Transition already chosen for the Director's next cue command. Selecting it early lets authoring tools show and tune what is coming before it starts, while the Switcher still honors that Transition's Runway, Tail, and Impact Point when the Cue is loaded and armed.
_Avoid_: choosing the Transition at the last moment; treating the selected next Transition as permission to bypass Runway, Tail, or Impact Point timing.

**Next Effect**:
The Effect already chosen as the destination for the Director's next A-to-B move. It lets a person know what the wall is moving toward and gives tuning tools something concrete to show before the move begins.
_Avoid_: confusing the Next Effect with the currently on-wall Effect; using an effect hold to freeze the wall when the goal is to preview or tune the next destination.

**Grid State**:
How much to trust where the one sits on the 16-beat Grid this frame, expressed as the three `GridState` values (the wire's `state`): **Locked** (the one is trusted), **Coasting** (the last good offset is held), and **Disputed** (a fresh offset disagrees with the held one). Effects read `BeatManager.Grid.State`; position may still be absent in the wire's partial coasting shape. Losing the clock makes the group's nullable facts read null; it is not a fourth state. BeatManager types the wire vocabulary without renaming it.
_Avoid_: describing Grid State as a five-level evidence ladder (retired); treating Coasting or Disputed as off-grid.

**Loop**:
A live repeated section of the current music, surfaced display-only as `BeatManager.Loop`. Loops are powers of four and usually preserve Grid, but they can rewind or repeat beat numbers so absolute beat progress goes stale. The Director keeps no loop-specific machinery: a loop's backward jump is just another grid-reading move: when it re-presents a Grid whose Cue Mark equals the Loaded Cue's, the Switcher answers "kept" and the cue rides unchanged; when it re-presents a different mark, the Switcher replaces the loaded cue if it is unlocked, or the locked cue rides. Sheets are keyed to their Phrase announcement (label and length), so a loop within the same Phrase never re-rolls a Cue Sheet.
_Avoid_: assuming a Loop means the wall is out of phase; assuming old absolute progress remains valid after a loop rewind; modeling a loop as its own scheduler or a Director cursor.

**Performer**:
The umbrella for anything the Director can put on the wall — an Effect, Transition, or Mixer — seen as something called on stage rather than as a class. The Director casts Performers; the Switcher moves them on and off.
_Avoid_: "dancer" (the early metaphor); using "Performer" when you specifically mean an Effect vs a Transition vs a Mixer.

**Repertoire**:
What a Performer advertises it can do, so the Director can cast it knowingly: handles Fills, handles Drops, or neither. Repertoire says what event moments a Performer can support; it does not say what BeatManager data the Performer reads while rendering. The Director always decides, and Repertoire only tells it which options exist.
_Avoid_: "profile" / "capabilities" (earlier names); treating it as configuration the Director sets — it is the Performer's own declaration.

**Mixer**:
An Effect that owns and combines child Effects. To everything outside it a Mixer is one ordinary Effect; inside it may directly configure any public child state, while suppress/unison/passive behavior remains the Mixer's choice rather than a system-wide policy.
_Avoid_: special-casing Mixers in casting or switching; treating independent child behavior as an isolation boundary; prescribing one child policy for every Mixer.

**A-to-B Transition**:
A Transition is a move from the current on-wall Effect (**A**) toward the destination Effect (**B**). Its visible position is described as progress from 0 to 1: 0 is fully A, 0.5 is exactly between A and B, and 1 is fully B. Once started, it is visual execution according to its Transition Settings; Runway and Tail must be non-negative and their total must not exceed 12 beats, leaving room inside the 16-beat minimum cadence without feeding back into Grid Boundary decisions. `Runway=0` and `Tail=0` is a valid hard cut.
_Avoid_: treating every Transition as if its only goal is to complete on a Cue Mark; treating transition progress, completion, or busy state as music-structure evidence.

**Transition Repertoire**:
The declaration of the A-to-B move a Transition offers: its Runway, Tail, Shape, Intensity, and Fill/Drop event suitability. This lets the Director cast a fitting Transition while the Switcher uses the Transition's own timing shape to execute an Armed Cue. Matching Fill/Drop tags make a Transition artistically suitable; Runway/Tail make it schedulable.
_Avoid_: treating timing length alone as Fill/Drop support; treating Repertoire as per-cue instructions or as state the Director sets; making the Director micromanage transition progress.

**Transition Settings**:
Saved authoring values for a Transition's Repertoire and human-tweakable creative knobs. Settings determine the Transition's Fill/Drop tags, Runway, and Tail, which imply its local Impact Point; the Switcher uses that declaration to execute an Armed Cue without compensating scheduling logic. Saved `TransitionSettings` assets are part of the live Transition Repertoire, not just editor tuning notes.
_Avoid_: putting pure algorithm invariants into Settings; treating every numeric literal as a setting; using the Director to compensate for invalid Transition Settings;

**Code Defaults**:
The transition-authored baseline values used to create or restore Transition Settings. Code Defaults live with the Transition's source so changing a Transition and changing its intended defaults stay together.
_Avoid_: making saved Settings the only source of truth; forcing artists to hunt generated asset files just to return to the Transition's intended defaults.

**Transition Shape**:
The broad visual family of an A-to-B Transition — e.g. Blend, Channel Blend, Directional Wipe, Index Wipe, Dissolve, Iris, or Noise. Shape helps the Director avoid treating two same-duration Transitions as equivalent when they read very differently on the wall.
_Avoid_: using Shape to describe musical timing; timing is Runway/Tail/Impact Point.

**Transition Intensity**:
How forcefully a Transition reads as a musical move: Subtle, Medium, or High. Intensity is a casting hint for ordinary phrase motion versus bigger events such as Drops or high-energy changes.
_Avoid_: treating Intensity as brightness or audio level; it describes the visual force of the Transition itself.

**Impact Point**:
A Transition-local progress point where that Transition's main visual hit happens — authoring vocabulary for transition designers, not a runtime concept. The Switcher's private Runway/Tail math makes the hit land on the Cue Mark; the runtime knows only Cue Marks, Runway, and Tail.
_Avoid_: naming any runtime type, field, or parameter after the Impact Point; treating it as a phrase boundary, Cue Mark, Grid Boundary, or Transition Completion; assuming every Transition's main hit happens at progress 1.

**Transition Duration**:
The full length of an A-to-B Transition from start to Completion, measured in beats. Duration is derived from Runway plus Tail; both parts are non-negative, the total must not exceed 12 beats, and zero duration is a hard cut.
_Avoid_: using Duration when only the pre-impact lead time is meant; that lead time is the Runway.

**Runway**:
The lead-in before a Transition's Impact Point — how many beats before the Armed Cue's Cue Mark the Switcher must start the Transition so the Impact Point reaches that mark on time. Runway is authored directly as part of the Transition Repertoire and may be zero, in which case the Transition hits immediately on the Cue Mark.
_Avoid_: using Runway to mean the whole Transition; a Transition can continue after impact; requiring a fake one-beat Runway for hard cuts.

**Tail**:
The part of a Transition after the Impact Point. Tail is visual resolution to B after the Transition has hit its mark; it has no effect on Phrase, Cue Sheet, Cue Mark, or Grid Boundary decisions.
_Avoid_: treating post-impact motion as late or wrong; treating Tail completion as a musical scheduling event.

**Transition Completion**:
The moment an A-to-B Transition has fully reached B. Completion may happen on the same beat as the Impact Point or after it, depending on the Transition Repertoire.
_Avoid_: assuming Completion is always the Director's timed musical target; the timed target is the Impact Point.

**Cue**:
The Director's directive for a musical change. A Cue is not the change itself — it *triggers* one: a stage-directed Cue arms the Switcher to swap Performers at a Cue Mark, and an effect-directed Cue tells the on-screen effect to respond ("respond to this fill", "play at this energy"). A Cue carries intent, never pixel-level commands.
_Avoid_: "call" (collides with calling a Performer on stage); treating a Cue as the change itself rather than the directive that triggers it; a Cue that micromanages a Performer's internal parameters.

**Mechanical Switcher** (a.k.a. **Switcher**):
The fire-and-forget mechanism that executes the Director's armed stage-directed Cues. It owns the Armed Cue, transition start/progress/completion, and the in-flight move between leaving and arriving Performers; it uses Runway and Tail to make the Transition's Impact Point land on the Cue Mark, but never chooses the Cue Sheet, Cue Mark, Performer, or Transition.
_Avoid_: putting musical or casting decisions in the Switcher; making it read Track Phase/Phrase data; the Director drawing buffers or running transitions itself; using Switcher progress, completion, or busy state as a scheduling input; calling it "dumb" instead of describing it as execution-only.

**Hold**:
An inspection freeze that suspends the Director so a developer can sit on one effect, watch it, and tweak its settings live — a Unity Editor development affordance, not normal show operation. It is not a selection input and not a second decider: while held, the Director stops advancing entirely (no rotation, no Cues, no transitions) and simply keeps the chosen Performer on screen; releasing it resumes directing.
_Avoid_: modeling Hold as a Director selection decision, or as a path that commands the Switcher around the Director; re-asserting the held effect every frame (nothing fights it once the one decider is suspended).

**Track Phase**:
RaveSystem's name for the analyzed phrase signal: current/next phrase labels, active state, beats remaining to the phrase boundary or upcoming phrase start, phrase length, and phrase count. Despite the name, Track Phase describes a **Phrase** in song structure; it is not the wall's **Grid**. In the current OSC stream, `active=1` describes the current Phrase, `active=0` can describe an upcoming Phrase, and `active=-1` means unavailable.
_Avoid_: confusing Track Phase with **Bar Phase** or the wall's **Grid**; treating phrase labels as an enum; treating unavailable Track Phase as Standalone Mode while other live timing is present.

**Phrase**:
The current musical section span described by Track Phase. It starts and ends at phrase boundaries, contains one or more Grids, and is usually at least 8 bars / 32 beats while often doubling or extending from there. Its announced beat length is the source for its Cue Sheet.
_Avoid_: treating a Phrase as a transition, a visual effect, or a clock source; choosing Cue Marks without reference to the current Phrase.

**Grid** (a.k.a. **16-Beat Grid**):
A fixed 4-bar / 16-beat timing unit inside a Phrase; a new Grid begins when the 16-count wraps back to the One. The wall uses Grid Boundaries as its minimum switching cadence; a Phrase can contain several Grids, and the Director may choose some interior Grid Boundaries rather than switching at every one. "Grid" here is the wall's own cyclic 16-beat unit — *not* RaveSystem's **Beat Grid** (the analyzed per-beat → time map), which the wall does not use under this name.
The Grid is the wall's main timing source: Phrase events (Fills, Drops) may drift off Grid, but the wall always follows the Grid.
_Avoid_: using "grid" when the whole Phrase is meant; assuming every Grid Boundary must trigger a transition; conflating the wall's Grid with RaveSystem's per-beat Beat Grid.

**Grid Boundary**:
The beat where a Grid starts or ends. A phrase boundary is always also a Grid Boundary, so the final boundary of a Phrase is always eligible as the mandatory final Cue Mark.
_Avoid_: calling every bar downbeat a Grid Boundary; a Grid Boundary is the 16-beat one, not every 4-beat bar one.

**Selected Grid Boundary**:
A Grid Boundary chosen as a transition target by the current implementation. In domain language, prefer **Cue Mark** for the Phrase-level plan; the important concept is that the mark belongs to the Grid and a Transition's local Impact Point hits it.
_Avoid_: using Selected Grid Boundary as the canonical name for Cue Sheet items; calling it an Impact Point or treating it as Transition Completion.

**Grid Beat**:
The wall's 1-based grid beat within the current Grid (the wire's `beat`, 1..16). A 4-beat Runway begins at grid beat 13 so the Impact Point lands on the next Grid Boundary: `13, 14, 15, 16, X`.
_Avoid_: zero-based beat-zero language; using millisecond timing when beat counts are available.

**`BeatManager.Grid`**:
The live sixteen-beat Grid as one shallow value group: nullable `State`, `Beat`, `Bar`, and `Progress`, plus `Build()` and `Decay()` conveniences. It exposes no `Current` wrapper or `Wrapped` event flag. A consumer that needs a boundary retains its own prior `Grid.Beat` and observes the count return to one. Effects read BeatManager, never the Director.
_Avoid_: reaching into `Director` from an effect; `Grid.Current`; hub-owned wrap identity; treating absent grid facts as an error.

**Fill**:
A one-to-four-beat musical section at the end of a Phrase, described by `BeatManager.Fill`. The wire's one lane changes meaning with `Active`: `CountBeats` is beats remaining while active and beats until the fill while inactive; `LengthBeats` is the current or upcoming length. BeatManager keeps those raw facts and adds `BeatsRemaining`, `BeatsUntil`, `Progress`, `Build()`, and `Decay()` for readability. The selected Effect or Transition owns how it responds.

**Drop**:
The climactic section of a track. A Drop is its own Phrase, and support for it lands at that Phrase's beginning. `BeatManager.Drop` has the same direct shape as Fill: `Active`, raw `CountBeats`/`LengthBeats`/`Remaining`, readable `BeatsRemaining` or `BeatsUntil`, `Progress`, `Build()`, and `Decay()`. There is no separate “next drop” wire lane; the same lane describes the current or upcoming drop according to `Active`.

**Phrase Event View** (`PhraseEventView`):
The editor-side display model of a phrase event (a **Fill** or a **Drop**): its status chip, meter fill, one-line readout, and a Now/Soon/Idle state, derived from the direct Fill or Drop values. It never feeds state back into BeatManager.
_Avoid_: duplicating "how a Fill/Drop reads" per surface; folding the chip *color* (an editor concern) into the view — color is the caller's decision, the view only classifies the state.

**Rhythm Text** (`RhythmText`):
The shared text vocabulary for nullable beat/count facts on the Data Surface (**Phrase Event View**, **Energy**, **Track Phase**) — a beat count reads as "16b", a plain count as its number, and **`null` reads as "—"**. One vocabulary so every rhythm readout speaks the same way, keeping the **Contrived Value** rule that "null means not-available" visible in the UI.
_Avoid_: re-deriving the "—"-for-null formatting per row; treating "—" as an error rather than the ordinary absent state.

**Energy**:
Intensity on one closed three-step ladder — Low, Mid, High. `BeatManager.Energy` exposes the current wire level, countdown, length, progress, derived `Trend`, and `Build()`/`Decay()`. The explicitly named next wire lane lives separately at `BeatManager.NextEnergy`. A **Waveform's** Energy is derived from its shape — how many peaks it has and how tightly they pack — computed from the notation itself.
_Avoid_: treating Energy labels as open text; confusing Energy (phrase-level intensity) with Levels (instantaneous audio bands); "Medium" (the middle tier is **Mid**); storing a Waveform's Energy in the Pool file or a per-entry label (it is a pure function of the notation); per-subject ladders or extra tiers.

**Span**:
An ordinary description of music with a duration, not a public BeatManager interface or wrapper type. Phrase, Drop, Fill, Energy, and Grid expose their facts directly and each offers the same readable `Build()` and `Decay()` convenience where useful.
_Avoid_: `SpanView`, `.Span`, `.Current`, or forcing unlike wire shapes through one generic public type.

**Edge**:
A consumer-local comparison when a system needs to know that a value changed or a moment began. BeatManager exposes current immutable state, not one-frame `Started`, `Ended`, `Changed`, `Wrapped`, or gate-opened booleans. The consumer retains the prior value whose change matters to its own behavior. A real event system may be added later if actual cross-consumer event delivery is required.
_Avoid_: manufacturing events in BeatManager merely because a caller could compare two values; confusing a frame flag with durable event delivery.

**Stock Envelope**:
A direct convenience on Phrase, Drop, Fill, Energy, and Grid. **Build** rises from zero to one; **Decay** falls from one to zero. With no argument the window is the value's full length. `Build(16)` or `Decay(16)` completes during the first sixteen beats and then holds its endpoint. The methods rest at zero when their duration is unavailable or inactive.
_Avoid_: a generic public envelope hierarchy; naming curves after artistic gestures; treating the convenience as the only sanctioned response.

**Levels**:
The live low/mid/high audio band triple in three forms: **Normalized** (wire values), **Smoothed** (attack/release follower), and **Peak** (instant rise with tempo-based fall). `Levels` is never null. When the wire lane is unavailable, Normalized becomes zero immediately while Smoothed and Peak fall toward zero according to their algorithms. Every form has the same `Low`, `Mid`, `High`, `Average`, `Strongest`, `StrongestBand`, `Centroid`, and `Dominance` reads.
_Avoid_: nullable Levels; unequal capabilities between forms; treating track-relative levels as absolute loudness meters.

**Color Bank**:
Retired. Color mapping is artistic policy owned by the Effect or Transition using the level data. BeatManager exposes musical values only.
_Avoid_: `Rgb()`, `Hsv()`, palette reads, or configurable color-source abstractions on `LevelBands`.
