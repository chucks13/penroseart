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

Provides the shared rhythm state for effects and the Director. It can run from the local simulator when no live source is present, and it receives live Rave OSC on-air state through `RaveOscReceiver` before the Director ticks.

- **BeatData**: Shared BPM/current-beat/timing state plus raw Rave on-air values for Fill, Drop, Energy, Track Phase, Levels, and Pulse.
- **Nullable queries**: `BeatManagerQueries` exposes ready-to-use rhythm values where `null` means not available right now.
- **Variants**: Supports rhythmic personalities such as every beat, alternating beats, measure start, subdivisions, and syncopation. The current code and docs disagree on the numbering of variants 4/5/6; confirm intended behavior before changing it.
- **Rhythmic Logic**: Uses an x^4 decay curve to create sharp visual kicks without making off-beat visuals too dark.
- **Propagation**: Mixers can pass rhythm to children, let children choose independently, or suppress child pulsing.

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
A one-bar rhythmic brightness envelope built by **merging humps end-to-end in time** — each hump occupies its own time slot and has a width (subdivision) and a height (amplitude). Humps are never summed or layered on top of each other. Values are **unipolar `[0..1]`**: 1 at a peak (on the beat), 0 in the troughs between beats. It is an envelope, never a bipolar audio wave — there is no negative half and 0 is the trough, not a midpoint.
_Avoid_: "adding waves together" (they are concatenated in time, not summed); "true wave" / "−1 to 1" (it is unipolar); "signal", "curve".

**Hump**:
The single unit a Waveform is built from: one rise-and-fall occupying its own time slot, peaking once and returning to 0. A Waveform is an ordered run of Humps merged end-to-end. Each Hump carries a width (its subdivision / note value) and a height (its Amplitude).
_Avoid_: "cycle", "wave", "pulse" for the unit — those name the whole signal, not the piece.

**Amplitude**:
The height of a single Hump, authored as a single digit `0–8` mapping linearly to `[0..1]` via digit ÷ 8 (`8` = full height, peak reaches 1; the ÷8 gives nine clean eighth-steps that land exactly on 1.0). One digit per Hump, read straight across in order, so the amplitude string sits directly beneath the sequence string as a stacked, equal-length pair. `0` makes the Hump silent — flat at 0 for its whole slot — which is how a beat is *skipped* (e.g. "measure start" = `8000`, "alternating beats" = `8080`). There is no separate gate; Amplitude `0` is the gate.

**Subdivision** (a.k.a. note value, the Hump's width):
How much bar-time one Hump occupies, named by musical note value rather than a count. The authored range is `W` whole (the full bar), `H` half (2 beats), `Q` quarter (1 beat), `E` eighth (½ beat), `S` sixteenth (¼ beat). One token per Hump; the tokens of a Waveform, read left to right, are its widths. The sixteenth is the fastest allowed — finer rates are deliberately excluded (both musically unneeded and a full-wall flicker hazard).
_Avoid_: "frequency" or "subdivisions-per-beat counts" — widths are note values, and a value slower than a quarter (whole/half) is one Hump spanning several beats, which a per-beat count cannot express.

**Waveform Synthesizer**:
The always-running runtime service effects pull from. The live pulse keeps a Bar Phase clock turning; given any Waveform spec, the synthesizer evaluates it against the current Bar Phase and hands back a brightness in `[0..1]` on demand. Effects do not own the clock — they own (or request) a Waveform and ask for its value *now*. The Waveform spec is the request; it can be typed inline in effect code, named as a Preset, or chosen at random.

**Preset**:
A named, saved Waveform spec — a convenience handle for a `sequence + amplitude + rounding + offset` bundle so it can be referred to by name instead of retyping the notation. Presets are *optional* for any single lookup: the synthesizer works on any spec, inline or named. The plain Beat Pulse (`QQQQ` / `8888`) is the canonical default.
_Avoid_: treating Presets as the only way to get a Waveform, as a fixed hardcoded set, or as an exhaustive enumeration of the (effectively unbounded) space.

**Pool** (the curated Preset set):
The hand-vetted collection of Presets that random selection draws from, so a random pick is always musically sensible. It is the **runtime source of truth** for available Presets, persisted as a **hand-editable text file in `StreamingAssets`** — in the spirit of `palettedata.txt`: named entries, plain notation, `//` comments, blank lines ignored, creatable by hand in any text editor. It is **read** at runtime by raw C# in `BeatManager` (same `StreamReader` + hand-rolled parse pattern the palettes use) and can also be **authored** in the Unity Inspector via the Waveform property drawer. A file — not a scene-serialized field — so the Editor-only authoring side and the runtime synth side stay decoupled, and a human is a first-class author alongside the drawer. The 7 legacy variants seed the default Pool. Inline specs bypass the Pool entirely.

**Wall Variant Lock** (a.k.a. **Auto** when released):
The wall-wide override that pins every effect to a single Pool Preset, so the whole installation pulses to one chosen rhythm instead of each effect rolling its own. Its released state is **Auto**: each effect picks its own variant as it starts. Engaging the lock fixes future effect starts *and* retargets the effect already on screen, so the change is immediate; releasing it returns the wall to Auto. Surfaced two-way in the Beat Manager dashboard's Waveform selector.
_Avoid_: conflating the lock (wall-wide, persists across effect changes) with an effect's own per-instance variant choice.

**Waveform Pattern** (future):
A choreographed routine assembled from single-bar Waveforms placed in sequence — bars composed into a multi-bar dance routine. Today Waveforms are single-bar only and the Pool holds individual bars; Patterns are the planned next step.
_Avoid_: treating today's single-bar Waveform as the final form; using "pattern" for one bar's hump sequence.

**Visual Tool** (the waveform "designer" web app):
A standalone browser sketchpad for *seeing* what a Waveform's notation looks like before committing it. Purely a visualizer/design aid — it is not the authoring pipeline and the runtime does not depend on it or its exported JSON.

**Beat Pulse**:
The standard rhythmic signal: a value in `[0..1]` that peaks on the quarter-note beat and falls back before the next. It is the default/canonical Waveform — the one all others are generated from.
_Avoid_: equating it with the raw OSC scalar; the runtime regenerates a shaped pulse locally.

**Bar Phase**:
The normalized position within the current measure (0 on the downbeat, 1 at the next downbeat). The clock every Waveform is evaluated against — owned and turned by the Waveform Synthesizer, derived from the live beat timing and kept locked to the DJ — and offered on the Data Surface like everything else, along with **Beat Fraction**, its sub-beat half (position within the current beat, 0..1). Consumers are never constrained: an effect wanting raw musical position reads the clock instead of hand-rolling a private metronome from wire facts.
_Avoid_: "beat phase" when the whole measure is meant; walling the clock off as synthesizer-private; per-effect hand-rolled metronomes.

**Bar** (a.k.a. **Measure**):
Four beats — "4-on-the-floor." The unit the Director's changes align to. The whole rhythmic structure runs on **powers of four**: 4 beats to a Bar, and Phrase lengths are multiples of four Bars (16, 32, 64 beats). A Bar boundary falls every 4 beats, but the Director changes no more often than every 4 Bars (16 beats).
_Avoid_: assuming arbitrary bar lengths — the wall assumes a 4/4, powers-of-four structure.

**The One**:
The first beat of a Bar — the downbeat. **Every Director change lands on the one**, never mid-bar. Counting is musical and **1-based: there is no beat zero** — the first beat is "the one," and a countdown to a change runs "4, 3, 2, 1, change," with the change landing on the *next* one. (The internal Bar Phase clock is a 0..1 normalization used for math; the one is the musical-facing count, not a zero index.)
_Avoid_: zero-based or off-by-one counting; landing a change mid-bar; conflating the musical count with the 0..1 Bar Phase value.

**Offbeat** (a.k.a. **Half-Step**):
A Beat Pulse shifted by half a beat so it peaks on the "&". Expressed as a Waveform carrying a **Phase Offset** of half a beat; the same shaping (width, amplitude, rounding) then applies as for any Waveform.
_Avoid_: confusing "half-step" with its pitch-theory meaning (a semitone). Here it is strictly the half-beat rhythmic position, the "&" between beats.

**On the Beat** (`OnBeat`):
Landing on the count — on the 1, 2, 3, or 4. The wire reports this per count as four gates; a count's gate is open for the first quarter of the beat interval. The Data Surface serves one contrived convenience, **On Beat**: is the *current* count's gate open right now. Watching specific counts (the 2 and the 4 for the snare) reads the wire's four gates directly.
_Avoid_: the nearest-upcoming-count pick (retired — it reads false at the instant a beat lands); confusing On Beat (a gate) with the Beat Pulse (a continuous wave).

**Next Beat** (`NextBeatMs`):
The live countdown to the next beat hit, whatever its count — the soonest of the wire's four per-count countdowns, running to zero on the hit and resetting. Not the beat's *length*: the average beat interval (`beat_avg_ms`, ≈ 60000 ÷ BPM) says how long a beat is; Next Beat says when the next one lands.
_Avoid_: conflating the countdown with the average interval; keeping a second spelling of the countdown.

**Phase Offset**:
A per-Waveform shift, measured in beats, that slides the whole Waveform along the Bar Phase before it is evaluated. 0 leaves it on the beat; 0.5 lands it on the "&" (the Offbeat). Fractional values express swing/shuffle feel. It moves *when* the humps land without changing their shape or count.

**Rounding** (a.k.a. sharpness):
A per-waveform scalar in `[0..1]` controlling hump shape. At 0 the peak is sharp/pointed; rising first rounds the peak toward a cosine dome, then continues to grow a **flat top** — a plateau pinned at 1 around the beat. Higher rounding keeps the wall at full brightness for longer near the beat ("brighter longer"); the trough between beats still falls to 0 at every setting.
_Avoid_: "smoothing", "easing" (overloaded); treating it as a true low-pass filter.

**Contrived Value**:
A ready-to-use value BeatManager builds from raw broadcast state — gated, normalized, smoothed, beat-synced, or otherwise shaped for effects. The counterpart of a **Raw Value**, which BeatManager passes through unchanged (BPM, track name, beat-in-bar, beat pulse). The test: a value is contrived when it is built from **more than a single piece of wire data** — a wire value plus anything else (another lane, local state, time). A member that re-serves one wire value unchanged is not contrived — it is a duplication, and a single-lane fact lives exactly once, on the raw wire view. Translating wire sentinels to `null` is serving, not contriving. Both kinds are pulled through the same nullable queries on BeatManager: `null` is a valid, expected state meaning "this value isn't there right now" — a track may have no upcoming drop, the wire may not carry levels — and every consumer chooses its own Standalone response for that missing value. Raw transport (`BeatData`, the OSC wire) keeps `-1` sentinels internally; `null` is the public face of "not available." Shared signals are contrived once on BeatManager; per-effect seasoning (variant, enable, minimum brightness) stays on the effect side, which is the only place that knows it.
_Avoid_: "cooked" (retired term); effects reading `BeatData` directly — raw values flow through BeatManager queries too; sentinel values crossing into effect math; treating `null` as an error instead of an ordinary musical state.

**Data Surface**:
The read-only face of BeatManager through which every consumer — effects, transitions, the Waveform Synthesizer, any future system — pulls musical data. Contrived Values are organized by musical concept (clock, events, energy, levels, …); Raw Values live exactly once, on the raw wire view — the first-class offering through which every wire fact stays pullable by name. Uniformly nullable — `null` is the only public spelling of "not available"; wire sentinels never cross it. Reads cannot write: data flows one way, from the wire through BeatManager outward. The few inbound knobs (transport feed, Wall Variant Lock, tuning) sit apart from the Data Surface; they are control, not data.
_Avoid_: re-serving a single-lane wire fact as a named query (duplication); mingling control knobs among data reads; treating the surface as restricting what a consumer may read or combine; merging similar-looking offerings without proof they are duplicates; surfacing transport/wire liveness as an offering (`IsLiveSource`, retired) — connectivity is internal plumbing that debug views mirror.

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
How much to trust where the one sits on the 16-beat Grid this frame, expressed as the three `GridState` values (the wire's `state`): **Locked** (offset freshly anchored or steadily dead-reckoned — the one is trusted), **Coasting** (no fresh anchor, e.g. Track Phase dropped out, so the last good offset is held), and **Disputed** (a freshly derived offset disagreed with the held one, kept pending the next clean re-latch). Effects read it as `BeatManager.Grid.State`; all three are *on-grid* readings with a valid Grid Beat — they differ only in trust. State is about the evidence for where the one is, not how good the visual looks. Losing the clock is **not** a low-trust value — it is a Standalone Mode exit, surfaced as a null `BeatManager.Grid`, not a fourth state. Wire vocabulary is law at the surface: this lane keeps RaveSystem's own `state` words; the BeatManager boundary types and validates them, never re-words them.
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
An Effect that combines multiple child Effects so more than one plays on screen at once. To everything outside it — Director, Switcher, casting — a Mixer is just another Effect: it declares its own Repertoire and owns what its children see and do.
_Avoid_: special-casing Mixers in casting or switching; letting child effects speak for themselves past the Mixer.

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
An inspection freeze that suspends the Director so a developer can sit on one effect, watch it, and tweak its settings live — a development affordance, not normal show operation. It is not a selection input and not a second decider: while held, the Director stops advancing entirely (no rotation, no Cues, no transitions) and simply keeps the chosen Performer on screen; releasing it resumes directing. Conceptually general — the ability to halt any running thing to inspect it — though the first concrete use is holding an effect.
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

**`BeatManager.Grid` (`GridInfo`)**:
The effect-facing read of the live **Grid**: a nullable `GridInfo { State, Beat, Progress }` (null = not on a grid). `State` is the **Grid State** `GridState`; `Beat` is the 1..16 **Grid Beat**; `Progress` is the 0..1 position through the 16-beat Grid. The Grid reading is decoded from the wire by RaveSystem OSC and boundaried into BeatManager; effects read only this facade, never the Switching layer or the Director. `GridInfo` is deliberately named to stand clear of the phrase-side `PhraseInfo` (the **Track Phase** read); these were once the one-letter twin `PhaseInfo`/`PhraseInfo`, and renaming the cyclic side to **Grid** is what resolved that collision.
_Avoid_: reaching into `Director` from an effect; treating a null `Grid` as an error rather than "not on a grid right now".

**Fill**:
A one-to-four-beat musical section at the end of a Phrase, described by BeatManager's Fill state. A Phrase's end usually lines up with a Grid Boundary, but not always. Two visible sides: *upcoming* (a beat countdown to its start) and *in progress* (position through it). The Director only uses Fill state to cast Effects and Transitions whose Repertoire says they support Fill for the relevant Cue. The selected Effect or Transition owns how it renders the Fill from BeatManager data.

**Drop**:
The climactic section of a track. A Drop is its own Phrase, and support for it lands at that Phrase's beginning. Same two-sided visibility as a Fill: a countdown to it, then progress through it. Unlike a Fill, a Drop **can change who is on stage**. The Director decides the move: it has the on-screen effect enter a drop-state in place when that effect's Repertoire can, or it swaps Performers. Either way the move must land *on* the drop — the anticipation side (scheduling the change beats ahead so it completes exactly on the boundary) is the valuable half, and the reason a Drop transition is timed more tightly than an ordinary phrase-boundary one.

**Phrase Event View** (`PhraseEventView`):
The canonical display model of a phrase event (a **Fill** or a **Drop**): its status chip, meter fill, one-line readout, and a Now/Soon/Idle state, all derived from the phrase-event query in one place so every surface — the Beat Manager dashboard today, any telnet/OSC/debug readout later — presents a Fill or Drop the same way. It is the presentation counterpart of the Fill/Drop *data*: what a phrase event **is** stays separate from how it **reads**.
_Avoid_: duplicating "how a Fill/Drop reads" per surface; folding the chip *color* (an editor concern) into the view — color is the caller's decision, the view only classifies the state.

**Rhythm Text** (`RhythmText`):
The shared text vocabulary for the nullable beat/count values of the rhythm queries (**Phrase Event View**, **Energy**, **Track Phase**) — a beat count reads as "16b", a plain count as its number, and **`null` reads as "—"**. One vocabulary so every rhythm-query readout speaks the same way, keeping the **Contrived Value** rule that "null means not-available" visible in the UI.
_Avoid_: re-deriving the "—"-for-null formatting per row; treating "—" as an error rather than the ordinary absent state.

**Energy**:
The track's current intensity as a closed three-step vocabulary — Low, Mid, High — with the next level and a beat countdown to the change. Direction (rising/falling/steady) follows from comparing current and next; "rising, change in 8 beats" is the build-up signal.
_Avoid_: treating Energy labels as open text; confusing Energy (phrase-level intensity) with Levels (instantaneous audio bands).

**Levels**:
The live low/mid/high audio band magnitudes, normalized — each band carries its own rhythm. Delivered smoothed (tunable): flicker (unintentional jitter) is the enemy; strobing (intentional rhythm) is the point.

**Color Bank**:
The set of beat-synced colors contrived from the Levels for effects to pull from — or ignore. Three forms: raw RGB (bands as channel brightness, black to bright — rhythm as brightness), hue/saturation (rhythm as color change), and palette-mediated (bands choose positions within the active palette, keeping the wall's look cohesive).
_Avoid_: treating the Bank as mandatory; bypassing the palette system without meaning to.
