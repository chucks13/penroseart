# Waveform System

A **Waveform** is a one-bar rhythmic brightness envelope that effects pull from instead of the old fixed `beatVariant` integers. It replaces the seven hardcoded rhythm variants with a small notation that describes *any* one-bar rhythm, plus an always-running service that turns that notation into a `[0..1]` brightness against the live beat clock.

This document is the implementer's reference. Canonical term definitions live in `CONTEXT.md` (`## Language`); the *why* is recorded in `docs/adr/0001-waveform-rhythm-model.md`. This doc carries the model, the notation, the file format, and the migration.

## What problem this solves

Today effects call:

```csharp
beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
```

where `beatVariant` is an `int` selecting one of seven hardcoded rhythms. That space is closed — you cannot ask for "half, then two quarters" or "every sixteenth on beat 4." The Waveform system opens it: an effect can request any rhythm by notation (`HQQ` at `844`), by Preset name, or by random draw from a curated Pool, and get back a shaped brightness for *right now*.

## The model

A Waveform is built by **merging Humps end-to-end in time**. Brightness is **symmetric around every beat**: full (1) on the beat, falling to 0 at the **midpoint** between that beat and the next, then rising back to 1 on the next beat. The trough is a shared boundary — the fall after one beat and the rise into the next meet at their midpoint. Humps are **concatenated, never summed** — there is no layering and no overlap. Values are **unipolar `[0..1]`**: 1 at a peak (on the beat), 0 in the trough (halfway between beats). It is an envelope, not a bipolar audio wave.

A Waveform spec is four parts:

| Part | Type | Meaning |
| --- | --- | --- |
| `sequence` | string of note-value tokens | the width of each Hump, left to right |
| `amplitude` | string of digits `0–8` | the height of each Hump, read straight across |
| `rounding` | `float [0..1]` | peak shape: sharp → dome → flat-top |
| `offset` | `float` (beats) | phase shift along the bar (`0.5` = offbeat) |

### Sequence — Hump widths

One token per Hump. Widths are musical note values, and the widths of a Waveform must sum to exactly one bar (4 beats, in 4/4):

| Token | Note value | Bar-time |
| --- | --- | --- |
| `W` | whole | 4 beats (the full bar) |
| `H` | half | 2 beats |
| `Q` | quarter | 1 beat |
| `E` | eighth | ½ beat |
| `S` | sixteenth | ¼ beat |

The sixteenth is the **fastest allowed** width. Finer rates (32nd and below) are deliberately excluded: they are musically unneeded here and a full-wall flicker/seizure hazard (a 32nd at 128 BPM strobes the whole wall at ~17 Hz).

A width slower than a quarter (`W`, `H`) is **one** Hump spanning several beats — not several beats fused. `HQQ` is three Humps: a 2-beat Hump then two 1-beat Humps.

### Amplitude — Hump heights

One digit `0–8` per Hump, mapped linearly to `[0..1]` via `digit ÷ 8`. `8` = full height (peak reaches 1). The `÷8` gives nine clean eighth-steps that land exactly on `1.0`.

Amplitude is read **1:1 straight across**, so the amplitude string sits directly beneath the sequence string as a stacked, equal-length pair:

```
HQQ
844
```

`0` makes a Hump **silent** — flat at 0 for its whole slot. This is how a beat is *skipped*; there is no separate gate, **Amplitude `0` is the gate**:

```
measure start    QQQQ / 8000
alternating      QQQQ / 8080
```

### Rounding — peak shape

A per-Waveform scalar in `[0..1]` controlling Hump shape:

- `0` — sharp/pointed peak (triangle).
- rising — the peak first rounds toward a **cosine dome**.
- higher still — a **flat top** grows: a plateau pinned at 1 around the beat, "brighter longer."

The trough between beats **always falls to 0**, at every rounding value. Rounding only reshapes the region near the peak; it never lifts the floor.

Recommended implementation: shape each Hump with a cosine dome and let `rounding` widen a clamped plateau around the peak. Exact constants are a visual-tuning detail to settle in the property drawer against live playback — the contract is only "sharp at 0, dome then flat-top as it rises, trough always 0."

### Offset — phase shift

A per-Waveform shift measured in beats, applied before evaluation. `0` leaves the Waveform on the beat; `0.5` lands it on the "&" (the **Offbeat** / Half-Step). Fractional values express swing/shuffle feel. Offset moves *when* the Humps land without changing their shape or count.

## The synthesizer

The **Waveform Synthesizer** is an always-running runtime service. The live pulse keeps a **Bar Phase** clock turning (0 on the downbeat → 1 at the next downbeat, locked to the DJ). Given any Waveform spec, the synthesizer evaluates it against the current Bar Phase and returns a brightness in `[0..1]` on demand:

```csharp
float Evaluate(Waveform wf, float barPhase);   // → [0..1]
```

Effects do not own the clock — they own (or request) a Waveform and ask for its value *now*. The synthesizer is the single forcing function: the property drawer plots exactly what `Evaluate` returns, so the visualization can never drift from runtime behavior.

Bar Phase is the boss. A malformed Waveform (widths that don't sum to a bar, an amplitude string shorter/longer than the sequence) cannot run away — it is still evaluated against one bounded bar. **Malformation is logged at load time and otherwise tolerated; nothing is silently substituted.** (We do not fall back to the Beat Pulse; the worst case is one odd-looking bar, which is self-correcting on the next downbeat.)

## Requesting a Waveform

Three ways, in increasing reuse:

1. **Inline** — typed in effect code: `Waveform.Parse("HQQ", "844")`. Bypasses the Pool entirely. When rounding/offset are omitted, an inline spec **defaults to the Beat Pulse's shaping** (Beat Pulse rounding, offset `0`) — so an inline spec with no extras behaves like the plain pulse with custom widths/heights.
2. **By Preset name** — a named, saved spec from the Pool, referred to instead of retyping notation.
3. **Random** — drawn from the curated Pool, so a random pick is always musically sensible.

The plain **Beat Pulse** (`QQQQ` / `8888`) is the canonical default and the origin point: every other Waveform is "the pulse, but with these deltas."

## The Pool file

The **Pool** is the hand-vetted set of Presets that random selection draws from. It is the runtime source of truth for available Presets, persisted as a **hand-editable text file in `StreamingAssets`**, in the spirit of `palettedata.txt`.

### Format

One Preset per line, palette-style macro:

```
DEFINE_WAVEFORM(name){ sequence | amplitude | rounding | offset }
```

```
// penrose_waveforms.txt — Waveform Pool
// DEFINE_WAVEFORM(name){ sequence | amplitude | rounding | offset }

DEFINE_WAVEFORM(beat pulse)    { QQQQ | 8888 | 0.3 | 0   }
DEFINE_WAVEFORM(offbeat)       { QQQQ | 8888 | 0.3 | 0.5 }
DEFINE_WAVEFORM(measure start) { QQQQ | 8000 | 0.3 | 0   }
DEFINE_WAVEFORM(alternating)   { QQQQ | 8080 | 0.2 | 0   }
DEFINE_WAVEFORM(four on floor) { HQQ  | 844  | 0.5 | 0   }
```

Conventions, matching the palette files: named entries, `//` line comments, blank lines ignored, brace-delimited data. The seven legacy variants seed the default Pool.

### Reading (runtime)

Read by **raw C# in `BeatManager`**, using the same `StreamReader` + hand-rolled parse pattern `GPalette`/`Penrose` already use:

```csharp
var sr = new StreamReader(Application.streamingAssetsPath + "/penrose_waveforms.txt");
// split each DEFINE_WAVEFORM(name){ ... } line on '{', '}', '|'
```

This keeps the runtime synth side independent of any Editor code.

### Writing (Editor)

The **property drawer owns the file.** It parses what it can on load and, on save, **rewrites the whole file canonically** from its in-memory list of records. There is no comment-preservation and no append-merge: drawer save is a full canonical rewrite. Hand-editing is the bootstrap/fallback path; the drawer is the primary editor. Anything in the file that is not a `DEFINE_WAVEFORM` record (comments, blank lines, hand formatting) is **not preserved** across a drawer save.

This single-owner rule is why the format is dead simple to parse and serialize — see the ADR for the trade-off.

## Property drawer visualization

The BeatManager dashboard drawer (`Assets/Editor/BeatManagerDrawer.cs`, which absorbed the original `BeatDataDrawer`) carries the Waveform view. Layout (the "Option C" design):

- **Left ¾** — the full one-bar Waveform rendered statically from its notation: the merged Hump envelope across the bar, beat gridlines behind it.
- **Right ¼** — the **live playhead**: a marker tracking the current Bar Phase across the same envelope, showing where "now" sits and the brightness being emitted.

The drawer animates live in Play Mode via `ControllerEditor` (`RequiresConstantRepaint() => Application.isPlaying`). Curves are drawn with `Handles.DrawAAPolyLine`; the existing rect/glyph dashboard (header badge, beat dot columns, pulse meters, countdown chips) stays.

## Migration

The change is deliberately small at the call sites.

- `EffectBase` already carries `beatEnable` and `beatVariant`; effects already default to a random rhythm (`beatVariant = beatManager.GetRandomVariant()` in `EffectBase` init). That seam stays — `GetRandomVariant` becomes "draw a Waveform from the Pool."
- `BeatManager.GetBeatBrightness(...)` keeps its signature shape; its **internals** swap from the seven-way variant switch to `Evaluate(waveform, barPhase)`. The ~18 effects that call it need no change if the variant selector resolves to a Waveform inside `BeatManager`.
- `GetRandomVariant` / `GetRandomVariantChill` remain the selection seam, and are the natural home for future energy/mood-filtered selection (deferred — OSC `energy_state` is not wired into selection yet).

## Out of scope (for now)

- OSC `energy_state` / direction filtering of random selection (the incoming data isn't finalized).
- The `energy` integer from the web designer's JSON export — it does nothing to the curve and is dropped from the Pool format.
- The web "designer" app (`waveforms/`) is a **Visual Tool** only: a sketchpad for seeing notation before committing it. The runtime does not depend on it or its exported JSON.
