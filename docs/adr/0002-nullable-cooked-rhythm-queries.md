# Nullable cooked rhythm queries

**Status:** accepted

RaveSystem broadcasts 17 `/rave/onair/*` values at 60 Hz; PenroseArt parses all of them
into `BeatData`, but until now only the beat clock had consumers — energy, track phase,
drop, fill, and levels arrived, were displayed in the Inspector, and were read by nothing.
This ADR defines the consumption interface through which effects and transitions pull
**all** musical state, replacing the previous mixed idioms (4-arg `GetBeatBrightness`,
hand-rolled gates, raw `BeatData` field reads). Terms in `CONTEXT.md` (Cooked Value,
Default/Synced Mode, Color Bank, Fill, Drop, Energy, Levels, Track Phase).

This qualifies as an ADR because it sets a convention every effect and transition will be
written against, retires a tested public surface in favor of a new one, and resolves
trade-offs (nullables vs sentinels vs tester-doer) that would otherwise be re-litigated.

## Decisions worth recording

### Transport keeps sentinels; requests return nullables
`BeatData` stays plain serialized fields with `-1` sentinels — Unity's serializer does not
serialize `Nullable<T>`, and Inspector/`BeatDataDrawer` visibility is a debugging
requirement. The cooked query layer on `BeatManager` returns nullable types (`FillInfo?`,
`Color?`, `float?`); `null` means "not available right now." Sentinel-reading lives only
in the cooked layer; sentinels never cross into effect math. Rejected: tester-doer
`Has*`/neutral pairs (runtime-tolerant but the compiler enforces nothing) and nullable
fields in `BeatData` (breaks serialization). C# 9 (Unity 6000.4) supports both nullable
forms; `Assets/OSC` already compiles `#nullable enable`.

### Consumers are dual-personality, and they own the fallback
Every rhythm-aware effect/transition has a **Default Mode** (signal `null`) and a
**Synced Mode** (signal live): branch with `is { } x`, or fold inline with `?? fallback`.
The compiler forces every call site to choose its fallback — a forgotten check is a
compile error, not a silent behavior. Neutral policy belongs to the consumer (an effect
knows its own neutral color), not to BeatManager.

### Preserve the wire's tri-state (defect fix)
RaveSystem's countdown/phase states carry `active` as a tri-state: `1` = active now,
`0` = counting to the next occurrence, `-1` = unavailable. The parser currently collapses
this with `active = ReadNextInt(...) != 0` (`RaveOscPacketParser.cs:160,172`), so
**unavailable (-1) parses as active** — latent only because nothing consumed these fields.
Fix: snapshot and `BeatData` keep `int active` (-1/0/1) mirroring RaveSystem's
`TrackCountdownState`; queries map `-1 → null`, `0 → upcoming`, `1 → in progress`.

### Fill and Drop are two-phase structs
`FillInfo?` / `DropInfo?` are non-null whenever phrase data is valid and expose both
sides of the boundary: `beatsUntilStart` when upcoming, `progress` (Bar-Phase-smoothed,
not integer-stepped) when in progress, plus `lengthBeats`/`remaining`. Rejected:
active-only visibility — anticipation ("start the transition two beats early, land on
the drop") is the choreographically valuable half and is already on the wire.

### Energy is typed; Track Phase stays thin
Energy's vocabulary is closed (`Low/Mid/High`, RaveSystem `PhraseEnergy`), so it parses
**once** to an enum in the cooked layer: `EnergyInfo?` with level, next, beats-until-change,
normalized float, and direction. An unrecognized label degrades to `null`, never to a wrong
enum. Track Phase labels are an open vocabulary ("Chorus 2", "Up 1"), so `PhaseInfo?`
passes labels through and cooks only the structure (countdowns, progress). No keyword
parsing pretending the phase vocabulary is closed.

### Levels are smoothed; the Color Bank has three forms
`LevelsInfo?` delivers normalized low/mid/high with attack/release smoothing applied once
in BeatManager (tunable — flicker is the enemy, strobing is the point; settings are
test-driven). The Color Bank cooks three optional color forms: raw RGB (rhythm as
brightness), hue/saturation (rhythm as color change), palette-mediated (cohesive with the
active GPalette). Wall-wide use of raw levels can reproduce the flicker hazard that made
ADR-0001 exclude 32nd notes; the smoothing floor is the corresponding safeguard.

### One home, thin accessors, full unification
Queries live on `BeatManager` only. `EffectBase` and `TransitionBase` each carry a
one-line `beatManager` accessor (TransitionBase gains it; previously transitions had no
rhythm access at all). Rejected: re-exposing every query on the base classes (pass-through
growth) and a static `BeatManager.Instance` (second singleton, weaker test seam).

Per-effect derivations live on `EffectBase`, closing over effect-owned state:
`BeatBrightness(min = 0.5f)` and `BeatTime(intensity)`, both built on the primitive
`BeatManager.Envelope(variant)` (`float?`, null = no beat clock). Principle: **BeatManager
cooks the shared dish; the effect side adds per-effect seasoning** (variant, enable, min).

Retired after migration, not wrapped: the 4-arg `BeatManager.GetBeatBrightness`
(21 call sites passed the effect's own fields back across the seam), `GetBeatTime`
(missing enable gate), `Flock.GetBeatSpeedMultiplier`'s hand-rolled gate, and
`IsBeatActive` as the availability idiom — nullability is the availability signal.
This is deliberate unification for a live installation: one convention, no coexisting
idioms.

## Consequences

- Phase 2 walks all 27 effects and 7 transitions, re-landing every rhythm call site on
  the new surface and wiring in fill/drop/energy/Color Bank behavior per effect.
- Waveform/beat tests migrate from the 4-arg form to `Envelope` + derivations; the
  parser tri-state fix gets regression tests.
- Energy/Track Phase wire semantics are still under research on the RaveSystem side;
  the structs encode today's contract and may evolve with it.
- `BeatData` remains Inspector-visible and sentinel-based; `BeatDataDrawer` is unaffected
  except where the tri-state `active` fields change from `bool` to `int`.
