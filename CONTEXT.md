# Penrose Simulator Context & Architecture Guide

## Overview

This project is a real-time controller for the Penrose Wall light installation. It generates generative visuals for a 900-tile Penrose model in Unity and currently outputs to LED hardware through high-speed USB serial (`SerialOut`) for S2 Mini / ESP32 boards.

The older ACN/E1.31 UDP output path still exists in `Controller.sendUDPFrame()` / `sendACN()`, but the active build path is serial because `Assets/core/Controller.cs` file-defines `ENABLE_SERIAL`.

## Core Components

### 1. Controller.cs (The Singleton Hub)

Manages the main loop, effect switching, overlays, input, and hardware output.

- **Deck System**: Ensures variety by drawing effects/transitions from the top half of a rotating deck and moving selected entries to the bottom.
- **State Machine**: Alternates between a playing state (generative effects) and a transition state (blending between effects).
- **Timing**: Defaults to 10 seconds per effect with a 2 second transition.
- **Held Effect**: A single selection that either lets the wall rotate or pins it to one effect. The **Random** state lets the Deck System rotate normally; choosing a specific effect *holds* it, suppressing both rotation and transitions until Random is chosen again. Selected from the Inspector's effect dropdown (Random is the default first entry); the `Escape` key always returns to Random. The blank effect template is never offered, since it is excluded from the runtime effect catalog.

### 2. Beat Manager (Synchronization)

Provides a global heartbeat for the installation. The current implementation is a simulated/debug beat source; future versions may poll OSC or another live synchronization source.

- **BeatData**: Shared BPM/current-beat/timing state.
- **Variants**: Supports rhythmic personalities such as every beat, alternating beats, measure start, subdivisions, and syncopation. The current code and docs disagree on the numbering of variants 4/5/6; confirm intended behavior before changing it.
- **Rhythmic Logic**: Uses an x^4 decay curve to create sharp visual kicks without making off-beat visuals too dark.
- **Propagation**: Mixers can pass rhythm to children, let children choose independently, or suppress child pulsing.

### 3. Buffer and Effect System

The runtime works on `UnityEngine.Color[]` buffers sized to `Penrose.Total == 900`.

- **Effects**: Inherit from `EffectBase` and fill their local 900-color `buffer`.
- **ScreenEffects**: Render into a rectangular virtual screen and map that image onto the irregular Penrose tile layout through precomputed interpolation weights.
- **Mixers/Wrappers**: Inherit from `MixerBase`, own child effects, and combine or transform child buffers.
- **Transitions**: Inherit from `TransitionBase` and blend between two effect buffers by effect index.
- **Penrose.cs**: Holds the physical model, tile metadata, JSON data, mesh generation, and buffer-to-mesh color mapping.

### 4. Palette System (GPalette / AnimPalette)

A shared color-management and animation system.

- **Global Coordination**: `EffectBase.APalette` is static, so all effects share a cohesive palette state.
- **Runtime Control**: `Controller` updates palette animation and can trigger global palette shifts or reloads via the `Return` key.
- **Integration**: Effects query colors using normalized positions, allowing palette details to remain separate from generative logic.

### 5. Input and Output

- **Primary output**: USB serial via `SerialOut`, using `sendSerialFrame()` to expand the 900 logical Penrose tiles through `penrose.JsonRawData.wires` into the physical LED order.
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

1. **Initialization**: `Controller` initializes `Penrose`, discovers effects/transitions/blenders through `Factory<T>`, configures UI fields, starts OSC/control helpers, and initializes serial output when enabled.
2. **Loop**: The active effect or transition draws into a 900-color buffer; overlays/blenders can modify it.
3. **Output**: The active serial path maps the Penrose buffer to physical LED order and sends frames through `SerialOut`; the legacy UDP path maps the same data into ACN/E1.31 universes.
4. **Scene update**: `Penrose.UpdateModelColors()` applies the current buffer to the Unity mesh for visualization.

## Language

> Shared glossary for **every** term used across the Penrose project — rhythm, visuals, hardware, control, tooling, anything we need a single agreed meaning for so everyone is on the same page. Not limited to beat/rhythm. Definitions describe what each concept *is*, not how it is implemented. (The sections above are an architecture guide and intentionally do carry implementation detail; this section does not.) Add a term here the moment it needs a canonical meaning.

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

**Visual Tool** (the waveform "designer" web app):
A standalone browser sketchpad for *seeing* what a Waveform's notation looks like before committing it. Purely a visualizer/design aid — it is not the authoring pipeline and the runtime does not depend on it or its exported JSON.

**Beat Pulse**:
The standard rhythmic signal: a value in `[0..1]` that peaks on the quarter-note beat and falls back before the next. It is the default/canonical Waveform — the one all others are generated from.
_Avoid_: equating it with the raw OSC scalar; the runtime regenerates a shaped pulse locally.

**Bar Phase**:
The normalized position within the current measure (0 on the downbeat, 1 at the next downbeat). The clock every Waveform is evaluated against. Derived from the live beat timing and kept locked to the DJ.
_Avoid_: "beat phase" when the whole measure is meant.

**Offbeat** (a.k.a. **Half-Step**):
A Beat Pulse shifted by half a beat so it peaks on the "&". Expressed as a Waveform carrying a **Phase Offset** of half a beat; the same shaping (width, amplitude, rounding) then applies as for any Waveform.
_Avoid_: confusing "half-step" with its pitch-theory meaning (a semitone). Here it is strictly the half-beat rhythmic position, the "&" between beats.

**Phase Offset**:
A per-Waveform shift, measured in beats, that slides the whole Waveform along the Bar Phase before it is evaluated. 0 leaves it on the beat; 0.5 lands it on the "&" (the Offbeat). Fractional values express swing/shuffle feel. It moves *when* the humps land without changing their shape or count.

**Rounding** (a.k.a. sharpness):
A per-waveform scalar in `[0..1]` controlling hump shape. At 0 the peak is sharp/pointed; rising first rounds the peak toward a cosine dome, then continues to grow a **flat top** — a plateau pinned at 1 around the beat. Higher rounding keeps the wall at full brightness for longer near the beat ("brighter longer"); the trough between beats still falls to 0 at every setting.
_Avoid_: "smoothing", "easing" (overloaded); treating it as a true low-pass filter.

**Contrived Value**:
A ready-to-use value BeatManager builds from raw broadcast state — gated, normalized, smoothed, beat-synced, or otherwise shaped for effects. The counterpart of a **Raw Value**, which BeatManager passes through unchanged (BPM, track name, beat-in-bar, beat pulse). Both kinds are pulled through the same nullable queries on BeatManager: `null` is a valid, expected state meaning "this value isn't there right now" — a track may have no upcoming drop, the wire may not carry levels — and every consumer chooses its own fallback. Raw transport (`BeatData`, the OSC wire) keeps `-1` sentinels internally; `null` is the public face of "not available." Shared signals are contrived once on BeatManager; per-effect seasoning (variant, enable, minimum brightness) stays on the effect side, which is the only place that knows it.
_Avoid_: "cooked" (retired term); effects reading `BeatData` directly — raw values flow through BeatManager queries too; sentinel values crossing into effect math; treating `null` as an error instead of an ordinary musical state.

**Default Mode / Synced Mode**:
The two personalities every rhythm-aware effect or transition has. Default Mode is its way of working when a requested signal is unavailable (`null`) — the effect must look intentional on its own. Synced Mode is its way of working when the signal is live. Branch once per frame (`is { } x`) for dual-personality behavior, or fold inline (`?? fallback`) for simple modulation.
_Avoid_: effects that freeze, glitch, or go dark when data is absent.

**Track Phase**:
The named phrase position within the current track as analyzed by RaveSystem — "Intro", "Break", "Drop", "Chorus 2" — with current/next labels and beat countdowns to the boundary. An **open vocabulary**: labels are track-dependent names, never a closed set to parse against.
_Avoid_: confusing with **Bar Phase** (position within one measure); treating the labels as an enum.

**Fill**:
A short transitional phrase burst — usually four to eight beats, a measure or slightly more — between sections. Two visible sides: *upcoming* (a beat countdown to its start) and *in progress* (position through it). Fill-only behavior — overlays, quick effect switches, one-shot interactions — is a first-class visual move.

**Drop**:
The climactic section boundary of a track. Same two-sided visibility as a Fill: a countdown to it, then progress through it. The anticipation side (landing a transition *on* the drop, beats ahead) is the choreographically valuable half.

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
