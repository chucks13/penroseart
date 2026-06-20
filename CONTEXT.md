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

**Visual Tool** (the waveform "designer" web app):
A standalone browser sketchpad for *seeing* what a Waveform's notation looks like before committing it. Purely a visualizer/design aid — it is not the authoring pipeline and the runtime does not depend on it or its exported JSON.

**Beat Pulse**:
The standard rhythmic signal: a value in `[0..1]` that peaks on the quarter-note beat and falls back before the next. It is the default/canonical Waveform — the one all others are generated from.
_Avoid_: equating it with the raw OSC scalar; the runtime regenerates a shaped pulse locally.

**Bar Phase**:
The normalized position within the current measure (0 on the downbeat, 1 at the next downbeat). The clock every Waveform is evaluated against. Derived from the live beat timing and kept locked to the DJ.
_Avoid_: "beat phase" when the whole measure is meant.

**Bar** (a.k.a. **Measure**):
Four beats — "4-on-the-floor." The unit the Director's changes align to. The whole rhythmic structure runs on **powers of four**: 4 beats to a Bar, and Phrase lengths are multiples of four Bars (16, 32, 64 beats). A Bar boundary falls every 4 beats, but the Director changes no more often than every 4 Bars (16 beats).
_Avoid_: assuming arbitrary bar lengths — the wall assumes a 4/4, powers-of-four structure.

**The One**:
The first beat of a Bar — the downbeat. **Every Director change lands on the one**, never mid-bar. Counting is musical and **1-based: there is no beat zero** — the first beat is "the one," and a countdown to a change runs "4, 3, 2, 1, change," with the change landing on the *next* one. (The internal Bar Phase clock is a 0..1 normalization used for math; the one is the musical-facing count, not a zero index.)
_Avoid_: zero-based or off-by-one counting; landing a change mid-bar; conflating the musical count with the 0..1 Bar Phase value.

**Offbeat** (a.k.a. **Half-Step**):
A Beat Pulse shifted by half a beat so it peaks on the "&". Expressed as a Waveform carrying a **Phase Offset** of half a beat; the same shaping (width, amplitude, rounding) then applies as for any Waveform.
_Avoid_: confusing "half-step" with its pitch-theory meaning (a semitone). Here it is strictly the half-beat rhythmic position, the "&" between beats.

**Phase Offset**:
A per-Waveform shift, measured in beats, that slides the whole Waveform along the Bar Phase before it is evaluated. 0 leaves it on the beat; 0.5 lands it on the "&" (the Offbeat). Fractional values express swing/shuffle feel. It moves *when* the humps land without changing their shape or count.

**Rounding** (a.k.a. sharpness):
A per-waveform scalar in `[0..1]` controlling hump shape. At 0 the peak is sharp/pointed; rising first rounds the peak toward a cosine dome, then continues to grow a **flat top** — a plateau pinned at 1 around the beat. Higher rounding keeps the wall at full brightness for longer near the beat ("brighter longer"); the trough between beats still falls to 0 at every setting.
_Avoid_: "smoothing", "easing" (overloaded); treating it as a true low-pass filter.

**Contrived Value**:
A ready-to-use value BeatManager builds from raw broadcast state — gated, normalized, smoothed, beat-synced, or otherwise shaped for effects. The counterpart of a **Raw Value**, which BeatManager passes through unchanged (BPM, track name, beat-in-bar, beat pulse). Both kinds are pulled through the same nullable queries on BeatManager: `null` is a valid, expected state meaning "this value isn't there right now" — a track may have no upcoming drop, the wire may not carry levels — and every consumer chooses its own Standalone response for that missing value. Raw transport (`BeatData`, the OSC wire) keeps `-1` sentinels internally; `null` is the public face of "not available." Shared signals are contrived once on BeatManager; per-effect seasoning (variant, enable, minimum brightness) stays on the effect side, which is the only place that knows it.
_Avoid_: "cooked" (retired term); effects reading `BeatData` directly — raw values flow through BeatManager queries too; sentinel values crossing into effect math; treating `null` as an error instead of an ordinary musical state.

**Standalone Mode / Synced Mode**:
The two intentional personalities for rhythm-aware behavior. **Synced Mode** is active whenever live OSC data is present; the wall syncs to whatever musical timing the signal currently provides. **Standalone Mode** is the self-running art behavior when no OSC data is available, and it must look fully intentional on its own. This is a preference, not a fallback: the wall prefers live data and works deliberately without it.
_Avoid_: effects that freeze, glitch, or go dark when OSC is absent; calling Standalone Mode a "fallback" or "default"; treating missing Track Phase as Standalone Mode while other OSC timing is present.

**Director**:
The decision layer that owns *what* plays on the wall and *when* it changes — it directs, making every choice about what happens on the wall. A Performer's Repertoire and the live song structure (Track Phase, Fill, Drop, Energy, Levels) are inputs to that choice, never overrides of it. In Synced Mode, active whenever OSC data is present, it changes from musical timing — Track Phase Phrase Windows when available, otherwise the usable timing the OSC signal provides. In Standalone Mode, when no OSC data is available, it changes on the self-running timer. It decides only — it never draws a buffer or runs a transition itself; the Switcher executes its calls.
_Avoid_: "choreographer" (the earlier name, retired); giving the timer its own independent ownership of "when"; folding buffer or transition execution into the Director.

**Next Transition**:
The Transition already chosen for the Director's next A-to-B move. Selecting it early lets authoring tools show and tune what is coming before it starts, while the Director still owns the timing and phase alignment.
_Avoid_: choosing the Transition at the last moment; treating the selected next Transition as permission to bypass Runway, Tail, or Impact Point timing.

**Next Effect**:
The Effect already chosen as the destination for the Director's next A-to-B move. It lets a person know what the wall is moving toward and gives tuning tools something concrete to show before the move begins.
_Avoid_: confusing the Next Effect with the currently on-wall Effect; using an effect hold to freeze the wall when the goal is to preview or tune the next destination.

**Phase Anchor**:
The Director's current musical target for the next structural transition: a selected Phase Boundary or phrase boundary. Track Phase defines a Phrase Window; the Director may choose interior Phase Boundaries inside that window and must include the ending phrase boundary, then advances one selected impact at a time. When only beat/grid evidence is available, the anchor is the best known Phase Boundary; when Track Phase disappears, the wall can continue on the last known grid instead of snapping to an arbitrary beat-only count.
_Avoid_: treating the anchor as a new clock source; it is an interpretation of the incoming musical structure. Live Track Phase windows define the phrase boundaries and interior Phase Boundaries; inferred grid points are only for weaker evidence.

**Phase Lock**:
The Director's ongoing effort to keep Performer changes aligned to the Phase Anchor. It is not a one-time startup sync — the Director keeps reading, coasting, and re-anchoring as the musical data changes.
_Avoid_: assuming the wall is either perfectly synced or unsynced forever; phase lock is continuously maintained.

**Phase Confidence**:
How strongly the current Phase Anchor is trusted, from unknown, to beat-in-bar guess, to absolute-beat assumption, to track-end cross-check, to true Track Phase structure. Confidence describes the evidence for where the one is, not how good the visual looks.
_Avoid_: treating all beat-derived anchors as equally musical; Track Phase is stronger evidence than plain beat count.

**Coast**:
Continuing on the last known Phase Anchor when Track Phase data temporarily disappears. Coasting preserves the last musical grid until fresh phrase data returns or no anchor has ever been known.
_Avoid_: calling this a fallback; it is the deliberate Synced Mode behavior for intermittent phrase visibility.

**Re-anchor**:
Replacing the current Phase Anchor with a new one when fresh Track Phase data appears or contradicts the grid being coasted. Re-anchoring is how the wall gets back in phase after startup, data gaps, song position changes, or loop-like movement the current data can reveal.
_Avoid_: layering multiple anchors; the Director has one current phase anchor.

**Loop**:
A live repeated section of the current music. Loops are powers of four and usually preserve Phase, but they can rewind or repeat beat numbers in ways that make absolute beat progress stale. A Loop inside the same Phrase Window is a new pass through the same structure: it should keep the Phrase Window's selected Phase Boundaries and move the Director's cursor back to the next selected boundary after the current beat, not create a new Phrase Window or reroll the selection.
_Avoid_: assuming a Loop means the wall is out of phase; assuming old absolute progress remains valid after a loop rewind; treating a same-window Loop as a new Phrase Window.

**Beat Rewind**:
A substantial backward jump in the live beat count, usually a new loop pass or new track position. The Director treats a rewind of at least 16 beats as a new pass: it clears stale cue state and stops comparing future phrase targets against old absolute beat numbers. Small one- or two-beat backsteps are ignored as transport jitter. Rewind handling is not a separate scheduler — current Track Phase still supplies the phrase target.
_Avoid_: modeling loop windows, transport state machines, or speculative loop plans; the Director only needs current phrase data, a 16-beat minimum, and transition impact/tail timing.

**Performer**:
The umbrella for anything the Director can put on the wall — an Effect, Transition, or Mixer — seen as something called on stage rather than as a class. The Director casts Performers; the Switcher moves them on and off.
_Avoid_: "dancer" (the early metaphor); using "Performer" when you specifically mean an Effect vs a Transition vs a Mixer.

**Repertoire**:
What a Performer advertises it can do, so the Director can cast it knowingly — e.g. handles Fills, is Drop-capable, responds to Energy. The Director reads a Performer's Repertoire to decide whether to have the Performer express a musical event itself or to supply the move another way; the Director always decides, and the Repertoire only tells it which options exist.
_Avoid_: "profile" / "capabilities" (earlier names); treating it as configuration the Director sets — it is the Performer's own declaration.

**A-to-B Transition**:
A Transition is a move from the current on-wall Effect (**A**) toward the destination Effect (**B**). Its visible position is described as progress from 0 to 1: 0 is fully A, 0.5 is exactly between A and B, and 1 is fully B. Once started, it is visual execution according to its Transition Settings; Runway and Tail must be non-negative and their total must not exceed 12 beats, leaving room inside the 16-beat minimum cadence without feeding back into Phrase Window or Phase Boundary decisions. `Runway=0` and `Tail=0` is a valid hard cut.
_Avoid_: treating every Transition as if its only goal is to complete on a Phase Boundary; treating transition progress, completion, or busy state as music-structure evidence.

**Transition Repertoire**:
The Director-facing declaration of the A-to-B move a Transition offers: its Runway, Tail, Shape, Intensity, and what musical moments it suits. This lets the Director cast and schedule a Transition that fits the selected Phase Boundary instead of assuming all Transitions are interchangeable.
_Avoid_: using only a generic Drop/Fill/Energy tag for Transitions; timing shape is part of what the Transition advertises; treating Repertoire as per-cue instructions or as state the Director sets.

**Transition Settings**:
Saved authoring values for a Transition's Repertoire and human-tweakable creative knobs. Settings determine the Transition's Runway and Tail, which imply its local Impact Point; the Director reads that declaration to decide when to start the A-to-B move. When settings are intentionally edited, the Settings Editor enforces non-negative Runway/Tail values whose sum is at most 12 beats, so the Director does not need compensating scheduling logic.
_Avoid_: putting pure algorithm invariants into Settings; treating every numeric literal as a setting; using the Director to compensate for invalid Transition Settings; silently mutating saved Settings just because an asset was loaded.

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
A Transition-local progress point where that Transition's main visual hit happens. The Director aligns the Impact Point to a chosen Phase Boundary, but the Impact Point is not itself Phase or Phrase structure; it only describes where the Transition should be in its own A-to-B motion when it hits its mark.
_Avoid_: treating Impact Point as a phrase boundary, Phase Boundary, or Transition Completion; assuming every Transition's main hit happens at progress 1.

**Transition Duration**:
The full length of an A-to-B Transition from start to Completion, measured in beats. Duration is derived from Runway plus Tail; both parts are non-negative, the total must not exceed 12 beats, and zero duration is a hard cut.
_Avoid_: using Duration when only the pre-impact lead time is meant; that lead time is the Runway.

**Runway**:
The lead-in before a Transition's Impact Point — how many beats before the chosen Phase Boundary the Director must start the Transition so the Impact Point reaches that boundary on time. Runway is authored directly as part of the Transition Repertoire and may be zero, in which case the Director cues on the chosen Phase Boundary.
_Avoid_: using Runway to mean the whole Transition; a Transition can continue after impact; requiring a fake one-beat Runway for hard cuts.

**Tail**:
The part of a Transition after the Impact Point. Tail is visual resolution to B after the Transition has hit its mark; it has no effect on Phrase Window, Phase Boundary, or Phase Anchor decisions.
_Avoid_: treating post-impact motion as late or wrong; treating Tail completion as a musical scheduling event.

**Transition Completion**:
The moment an A-to-B Transition has fully reached B. Completion may happen on the same beat as the Impact Point or after it, depending on the Transition Repertoire.
_Avoid_: assuming Completion is always the Director's timed musical target; the timed target is the Impact Point.

**Cue**:
The Director's directive for a change. A Cue is not the change itself — it *triggers* one: a Cue aimed at the stage triggers the Switcher to swap dancers (via a Cut, Transition, or Mixer), and a Cue aimed at the on-screen effect triggers it to respond ("respond to this fill", "play at this energy"). An effect-directed Cue follows the same nullable, preference-driven contract as the rhythm queries — the effect reads the parts it understands, uses its own defaults for anything unset (an ordinary state, never degraded), and pulls the live event data itself from the Beat Manager. A Cue carries intent, never pixel-level commands.
_Avoid_: "call" (collides with calling a Performer on stage); treating a Cue as the change itself rather than the directive that triggers it; a Cue that micromanages a Performer's internal parameters.

**Mechanical Switcher** (a.k.a. **Switcher**):
The fire-and-forget mechanism that executes the Director's stage-directed Cues. It owns the in-flight transition (which Performer is leaving, which is arriving, how far along) and realizes a swap one of three ways: a **Cut** (instant), a **Transition** (blended — looser at a phrase boundary, tightly timed to land on a Drop), or a **Mixer** (bringing in another effect, including the temporary self-reverting mix that expresses a Fill the on-screen effect can't). It moves Performers on and off the wall and never decides what or when. If a new Transition command arrives while another is still rendering, the latest command replaces the mechanical move and uses the previous destination as its new source; the Director still does not branch on Switcher busy/progress state. Kept separate from the Director so decision and mechanism stay independent.
_Avoid_: putting timing or musical decisions in the Switcher; the Director drawing buffers or running transitions itself; using Switcher progress, completion, or busy state as a scheduling input; calling it "dumb" instead of describing it as execution-only.

**Hold**:
An inspection freeze that suspends the Director so a developer can sit on one effect, watch it, and tweak its settings live — a development affordance, not normal show operation. It is not a selection input and not a second decider: while held, the Director stops advancing entirely (no rotation, no Cues, no transitions) and simply keeps the chosen Performer on screen; releasing it resumes directing. Conceptually general — the ability to halt any running thing to inspect it — though the first concrete use is holding an effect.
_Avoid_: modeling Hold as a Director selection decision, or as a path that commands the Switcher around the Director; re-asserting the held effect every frame (nothing fights it once the one decider is suspended).

**Track Phase**:
RaveSystem's name for the analyzed phrase signal: current/next phrase labels, active state, beats remaining to the phrase boundary, phrase length, and phrase count. Despite the name, Track Phase describes a **Phrase Window** in song structure; it is not the wall's **Phase**. In the current OSC stream, available Track Phase frames report active while unavailable frames report unavailable.
_Avoid_: confusing Track Phase with **Bar Phase** or **Phase**; treating phrase labels as an enum; treating unavailable Track Phase as Standalone Mode while other live timing is present.

**Phrase Window**:
The current musical section span described by Track Phase. It starts and ends at phrase boundaries, contains one or more Phases, and is usually at least 8 bars / 32 beats while often doubling or extending from there. When a new Phrase Window begins, that is the moment to derive its Phase Boundaries and choose which interior ones may receive transitions.
_Avoid_: treating a Phrase Window as a transition, a visual effect, or a clock source; choosing transition targets without reference to the current Phrase Window.

**Phase** (a.k.a. **16-Beat Phase**):
A fixed 4-bar / 16-beat timing unit inside a Phrase Window. The wall uses Phase Boundaries as its minimum switching cadence; a Phrase Window can contain several Phases, and the Director may choose some interior Phase Boundaries rather than switching at every one.
_Avoid_: using "phase" when the whole Phrase Window is meant; assuming every Phase Boundary must trigger a transition.

**Phase Boundary**:
The beat where a Phase starts or ends. A phrase boundary is always also a Phase Boundary, so the final boundary of a Phrase Window is always eligible as the mandatory final transition impact.
_Avoid_: calling every bar downbeat a Phase Boundary; a Phase Boundary is the 16-beat one, not every 4-beat bar one.

**Selected Phase Boundary**:
A Phase Boundary chosen by the Director as a transition target. The Director may choose interior Phase Boundaries and always includes the final phrase boundary; a Transition may then use its local Impact Point to hit that selected boundary at the right moment.
_Avoid_: calling this an Impact Point or treating it as Transition Completion; Impact Point is local to a Transition, while Selected Phase Boundary belongs to the music grid.

**Phase Count**:
The Director's 1-based count within the current Phase. A 4-beat Runway starts at count 13 so the Impact Point lands on the next Phase Boundary: `13, 14, 15, 16, X`.
_Avoid_: zero-based beat-zero language; using millisecond timing when beat counts are available.

**Fill**:
A short transitional phrase burst — usually four to eight beats, a measure or slightly more — between sections. Two visible sides: *upcoming* (a beat countdown to its start) and *in progress* (position through it). A Fill is expressed as a **highlight in place** — an overlay, accent, or one-shot interaction on whatever is already on screen — and never changes which effect is playing (that is a Drop's move). The Director decides how it is expressed: it has the on-screen effect highlight the Fill when that effect's Repertoire can, or it brings in a temporary mix to do so.

**Drop**:
The climactic section boundary of a track. Same two-sided visibility as a Fill: a countdown to it, then progress through it. Unlike a Fill, a Drop **can change who is on stage**. The Director decides the move: it has the on-screen effect enter a drop-state in place when that effect's Repertoire can, or it swaps Performers. Either way the move must land *on* the drop — the anticipation side (scheduling the change beats ahead so it completes exactly on the boundary) is the valuable half, and the reason a Drop transition is timed more tightly than an ordinary phrase-boundary one.

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
