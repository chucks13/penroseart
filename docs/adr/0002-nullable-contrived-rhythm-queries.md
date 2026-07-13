# Nullable contrived rhythm queries

RaveSystem broadcasts 17 `/rave/onair/*` values at 60 Hz; PenroseArt parses all of them
into `BeatData`, but until now only the beat clock had consumers — energy, track phase,
drop, fill, and levels arrived, were displayed in the Inspector, and were read by nothing.
This ADR defines the consumption interface through which effects and transitions pull
**all** musical state, replacing the previous mixed idioms (4-arg `GetBeatBrightness`,
hand-rolled gates, raw `BeatData` field reads). Terms in `CONTEXT.md` (Contrived Value,
Standalone/Synced Mode, Color Bank, Fill, Drop, Energy, Levels, Track Phase).

## Transport keeps sentinels; requests return nullables
`BeatData` stays plain serialized fields with `-1` sentinels — Unity's serializer does not
serialize `Nullable<T>`, and Inspector visibility is a debugging
requirement. The query layer on `BeatManager` returns nullable types (`PhraseEventInfo?`,
`Color?`, `float?`); `null` means "not available right now" — a valid, expected musical
state (a track may simply have no upcoming drop), never an error. Sentinel-reading lives
only in the query layer; sentinels never cross into effect math. Rejected: tester-doer
`Has*`/neutral pairs (runtime-tolerant but the compiler enforces nothing) and nullable
fields in `BeatData` (breaks serialization). C# 9 (Unity 6000.4) supports both nullable
forms; `Assets/OSC` already compiles `#nullable enable`.

## Consumers are dual-personality, and they own the fallback
Every rhythm-aware effect/transition has a **Standalone response** (that signal is `null`) and a
**Synced Mode** path (that signal is live): branch with `is { } x`, or fold inline with `?? standalone`.
The compiler forces every call site to choose its fallback — a forgotten check is a
compile error, not a silent behavior. Neutral policy belongs to the consumer (an effect
knows its own neutral color), not to BeatManager.

## Preserve the wire's tri-state (defect fix)
RaveSystem's countdown/phase states carry `active` as a tri-state: `1` = active now,
`0` = counting to the next occurrence, `-1` = unavailable. The parser currently collapses
this with `active = ReadNextInt(...) != 0` (`RaveOscPacketParser.cs:160,172`), so
**unavailable (-1) parses as active** — latent only because nothing consumed these fields.
Fix: snapshot and `BeatData` keep `int active` (-1/0/1) mirroring RaveSystem's
`TrackCountdownState`; queries map `-1 → null`, `0 → upcoming`, `1 → in progress`.

## Fill and Drop share one two-phase shape
`Fill` and `Drop` each return a `PhraseEventInfo?` — one struct shape, two instances —
non-null whenever the wire state is available. `inProgress` is the only state flag;
everything else is values: `beatsUntilStart`/`msUntilStart`/`anticipation` while
upcoming, `beatsUntilEnd`/`progress` (Bar-Phase-smoothed, not integer-stepped) while in
progress, plus `lengthBeats`/`remaining`. `anticipation` fills 0→1 over the last
32 beats before the start (counting up to something fills); `msUntilStart` is contrived
from `beat_avg_ms` exactly as the OSC schema delegates to clients. "No more left in this
track" is not a third state: it is ordinary values (`beatsUntilStart == null`,
`remaining == 0`) — zero is a number, null means no value. Rejected: active-only
visibility — anticipation ("start the transition two beats early, land on the drop") is
the choreographically valuable half and is already on the wire. Also rejected: an
upcoming/in-progress/spent status enum — the nulls and counts already carry it.

## Energy is typed; Track Phase stays thin
Energy's vocabulary is closed (`Low/Mid/High`, RaveSystem `PhraseEnergy`), so it parses
**once** to an enum in the query layer: `EnergyInfo?` with level, next, beats-until-change,
normalized float, direction, and the same-energy run's progress/length/changes-remaining.
An unrecognized label degrades to `null`, never to a wrong
enum. Track Phase labels are an open vocabulary ("Chorus 2", "Up 1"), so `PhaseInfo?`
passes labels through and contrives only the structure (countdowns, progress). No keyword
parsing pretending the phase vocabulary is closed.

## Levels are smoothed; the Color Bank has three forms
`LevelsInfo?` delivers normalized low/mid/high with attack/release smoothing applied once
in BeatManager (tunable — flicker is the enemy, strobing is the point; settings are
test-driven). The Color Bank contrives three optional color forms: raw RGB (rhythm as
brightness), hue/saturation (rhythm as color change), palette-mediated (cohesive with the
active GPalette). Wall-wide use of raw levels can reproduce the flicker hazard that made
ADR-0001 exclude 32nd notes; the smoothing floor is the corresponding safeguard.

### Amendment (2026-07-11, Hunter, beat-data-interface ticket 07)

This section is superseded; the rest of the ADR stands. The smoothing floor is
**retracted**: the flicker hazard was this ADR's inference by analogy with ADR-0001's
32nd-note exclusion, never an observed fact — the wire updates at ~60 Hz and the app
renders at 60 fps, so effects handle whatever arrives. Levels now serve **three forms of
one triple** — Normalized (the raw wire fact, per ADR-0013's floor), Smoothed (this ADR's
smoothing, keeping its attack/release knobs), Peak (tempo-based fall-off, fixed policy) —
and the Color Bank's three fixed forms became **parameterized mappings** (RGB/HSV/palette)
whose component sources are effect-chosen knobs, defaulting to this section's wirings.
The palette is caller-supplied; BeatManager's reach into `EffectBase.APalette` is deleted.
Details: CONTEXT.md (Levels, Color Bank).

## One home, thin accessors, full unification
Queries live on `BeatManager` only. `EffectBase` and `TransitionBase` each carry a
one-line `beatManager` accessor (TransitionBase gains it; previously transitions had no
rhythm access at all). Rejected: re-exposing every query on the base classes (pass-through
growth) and a static `BeatManager.Instance` (second singleton, weaker test seam).

Per-effect derivations live on `EffectBase`, closing over effect-owned state:
`BeatBrightness(min = 0.5f)` and `BeatTime(intensity)`, both built on the primitive
`BeatManager.Envelope(variant)` (`float?`, null = no beat clock). Principle: **BeatManager
contrives the shared signals; the effect side adds per-effect seasoning** (variant, enable, min).

Retired after migration, not wrapped: the 4-arg `BeatManager.GetBeatBrightness`
(21 call sites passed the effect's own fields back across the seam), `GetBeatTime`
(missing enable gate), `Flock.GetBeatSpeedMultiplier`'s hand-rolled gate, and
`IsBeatActive` as the availability idiom — nullability is the availability signal.
This is deliberate unification for a live installation: one convention, no coexisting
idioms.

## Raw values flow through the same seam

`BeatData` is transport only: exactly what the source (live OSC or the simulator) said,
wire-shaped with sentinels, serialized for Inspector debugging. Locally derived stored
state (the offbeat gate/pulse machinery, `active`, `currentBeat`) moves out of `BeatData`
into BeatManager-owned state, beside the levels smoothing. Raw values effects may want
(BPM, track, beat-in-bar, beat pulse) are exposed as nullable passthrough queries on
`BeatManager`, so effects pull any field — raw or contrived — through one surface and
sentinels still never cross the seam. Effects never read `BeatData` directly. Rejected:
letting effects read `BeatData` for raw fields — it re-opens the sentinel leak and couples
effects to the wire shape.

## Consequences

- Phase 2 walks all 27 effects and 7 transitions, re-landing every rhythm call site on
  the new surface and wiring in fill/drop/energy/Color Bank behavior per effect.
- Waveform/beat tests migrate from the 4-arg form to `Envelope` + derivations; the
  parser tri-state fix gets regression tests.
- Energy/Track Phase wire semantics are still under research on the RaveSystem side;
  the structs encode today's contract and may evolve with it.
- `BeatData` remains Inspector-visible and sentinel-based, as the raw foldout of the
  single unified BeatManager dashboard drawer (one drawer for raw and contrived values).

### Amendment (2026-07-11, Hunter, beat-data-interface effort)

Three further sections are superseded by the BeatManager Data Surface redesign. The core
contract of this ADR stands and is generalized application-wide by ADR-0012: `null` is
the ordinary spelling of "not available," sentinels never cross the surface, and every
consumer owns its Standalone response.

- **"Fill and Drop share one two-phase shape"**: `PhraseEventInfo?` is replaced by the
  uniform span view — every genuine Span (Fill, Drop, Phrase, Grid, Energy run) serves
  the same shape: nullable facts, Started/Ended Edges, Build/Decay Stock Envelopes
  (ADR-0015). Loop is flat playback state outside the Span family: no progress, Edges,
  or Stock Envelopes. The anticipation side survives as next-occurrence countdowns
  served beside each Span; the Energy and Track Phase structs (`EnergyInfo?`,
  `PhaseInfo?`) reshape into the Energy and Phrase doorway views the same way.
- **"One home, thin accessors"**: musical data lives on two sibling read-only surfaces —
  BeatManager (the Data Surface, organized as concept doorways) and Waveforms (its
  own root, the base `waveforms` property beside `beatManager`) — both under ADR-0012.
  Effects and transitions receive the same two live roots, but neither base holds a
  Waveform or Routine or owns automatic acquisition, replacement, or response policy;
  concrete Performers own those artistic decisions under ADR-0017.
- **"Raw values flow through the same seam" / Consequences**: raw facts still flow
  through the surface (that ruling stands, hardened by ADR-0013), but the public mutable
  `beatData` field goes private and is no longer anyone's Inspector contract — dashboards
  mirror the core downstream (ADR-0016), never read transport state through a public
  field.
