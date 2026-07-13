# Waveform System

A **Waveform** is a one-bar rhythmic brightness envelope described by compact musical notation. Runtime values are acquired from `Waveforms`, bound to the shared musical clock, and read directly through `Envelope` or `Lerp(from, to)`.

This document is the implementer's reference. Canonical term definitions live in `CONTEXT.md` (`## Language`); the *why* is recorded in `docs/adr/0001-waveform-rhythm-model.md`. This doc carries the model, the notation, the file format, and the migration.

## What problem this solves

An Effect may need a pulse on every beat, a single measure-start accent, alternating hits, or a shape such as "half, then two quarters." Encoding those choices as a closed set would make each new dance move a provider change.

```csharp
waveform = waveforms.Random(Energy.Low, Energy.Mid);
float brightness = waveform.Lerp(0.5f, 1f);
```

The curated Pool can carry any valid notation. Waveforms owns Pool selection and clock binding; the concrete Effect or Transition owns when it acquires, which Energy set it requests, and how the held envelope changes the art.

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

## Waveforms

**Waveforms** is the shared acquisition surface beside BeatManager. BeatManager owns the live **Bar Phase** clock (0 on the downbeat → 1 at the next downbeat, locked to the DJ); every acquired Waveform is bound to that source and plays itself:

```csharp
var waveform = waveforms.Random(Energy.Low);
float envelope = waveform.Envelope;
float brightness = waveform.Lerp(0.5f, 1f);
```

Effects and transitions own the Waveform values they acquire and choose every artistic endpoint. `Envelope` rests at `0` without a live Bar Phase; `Lerp(from, to)` returns `to` in that state, making the effect's Standalone response explicit without nullable branching. Mixers use `waveforms.None` when they intentionally suppress a child's Waveform response. There is no provider-side `Evaluate`, one-frame `Hit`, or per-frame Waveforms lifecycle.

The clock-independent shape kernel is `Waveform.Sample(barPhase)`. Runtime playback and the editor plot both call it, so visualization and playback cannot drift. A value created directly with `Waveform.Parse` is an authoring/kernel value; attempting clock-driven playback from it fails until it has been acquired and bound through `Waveforms`.

## Routines

A Routine is four already-resolved Waveforms spanning one 16-beat Grid. Callers use the same Waveform acquisition operations they already know, then compose the result directly:

```csharp
var routine = Routine.Of(
    waveforms.Random(Energy.Mid),
    waveforms.Random(Energy.Low),
    waveforms.Random(Energy.High),
    waveforms.Random(Energy.Low));

float envelope = routine.Envelope;
float brightness = routine.Lerp(0.5f, 1f);
```

There is no Routine-specific acquisition language, resolver, evaluation service, or replacement policy. Routine reads exactly like Waveform; the current Grid bar selects its shape. Without a placed Grid, `Envelope` rests at `0` and `Lerp(from, to)` returns `to`. The caller composes another value when it wants different bars.

Bar Phase is the boss. The pure parser logs malformed notation and `Sample` remains bounded to one bar for editor inspection. Runtime Pool construction is stricter: a missing/empty Pool or parsed notation-invalid entry throws before effects start. Nothing silently substitutes the Beat Pulse or widens an unsatisfied Energy request.

## Requesting a Waveform

Runtime performers draw from the curated Pool:

```csharp
waveform = waveforms.Random();                    // whole Pool
waveform = waveforms.Random(Energy.Low);          // one Energy tier
waveform = waveforms.Random(Energy.Low, Energy.Mid);
```

No match is a configuration error and throws. Pool positions and Preset names are authoring details, not runtime identities. `Waveform.Parse(...)` remains the shared pure parser for the Pool codec, editor previews, tests, and direct `Sample(barPhase)` inspection; it is not a second runtime acquisition path.

The plain **Beat Pulse** (`QQQQ` / `8888`) is the canonical default Preset.

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

Conventions, matching the palette files: named entries, `//` line comments, blank lines ignored, brace-delimited data. The seven original rhythm Presets seed the default Pool.

### Reading (runtime)

Read through the shared `WaveformPool` codec. Waveforms uses the same parser as the editor:

```csharp
var entries = WaveformPool.Parse(WaveformPool.ReadFileOrEmpty());
```

This keeps runtime Pool parsing independent of Editor code.

### Writing (Editor)

The **Waveform Pool editor owns file writes.** It parses what it can on load and, on save, **rewrites the whole file canonically** from its in-memory list of records. There is no comment-preservation and no append-merge: editor save is a full canonical rewrite. Hand-editing is the bootstrap/fallback path; the editor window is the primary UI. Anything in the file that is not a `DEFINE_WAVEFORM` record (comments, blank lines, hand formatting) is **not preserved** across an editor save.

This single-owner rule is why the format is dead simple to parse and serialize — see the ADR for the trade-off.

## Property drawer visualization

The BeatManager dashboard drawer (`Assets/Editor/Rhythm/BeatManagerDrawer.cs`, which absorbed the original `BeatDataDrawer`) carries the Waveform view. Layout (the "Option C" design):

- **Left ¾** — the full one-bar Waveform rendered statically from its notation: the merged Hump envelope across the bar, beat gridlines behind it.
- **Right ¼** — the **live playhead**: a marker tracking the current Bar Phase across the same envelope, showing where "now" sits and the brightness being emitted.

The drawer animates live in Play Mode via `ControllerEditor` (`RequiresConstantRepaint() => Application.isPlaying`). Curves are drawn with `Handles.DrawAAPolyLine`; the existing rect/glyph dashboard (header badge, beat dot columns, pulse meters, countdown chips) stays.

## Migration

The landed runtime is a hard cut to value ownership:

- Controller owns `BeatManager` and its sibling `Waveforms` surface. `EffectBase` and `TransitionBase` expose both roots to concrete Performers.
- Acquisition is explicit through `waveforms.Random(...)`; a held `Waveform` is immutable and bound to the shared clock.
- Each concrete Effect or Transition chooses acquisition timing, reads `Envelope` or supplies the endpoints to `Lerp(from, to)`, and owns its Standalone response and any local state. Routines use the identical playback spelling.
- Authoring bases provide access and neutral configuration only. They do not acquire, replace, refresh, or map Waveforms automatically.
- A Mixer is one Effect publicly. It configures its privately owned child Effects directly and assigns `waveforms.None` when it intentionally suppresses a child's response.
- `Routine.Of(...)` composes four already-acquired values directly; it owns no resolver, lifecycle, or replacement policy.

## Out of scope (for now)

- Provider-side automatic filtering from live Energy direction. Performers choose the Energy tiers they request.
- The `energy` integer from the web designer's JSON export — it does nothing to the curve and is dropped from the Pool format.
- The web "designer" app (`waveforms/`) is a **Visual Tool** only: a sketchpad for seeing notation before committing it. The runtime does not depend on it or its exported JSON.
