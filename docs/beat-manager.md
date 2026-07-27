# BeatManager musical data

`BeatManager` is the application's read-only musical-data hub. `RaveOscReceiver` feeds it the latest [OSC on-air snapshot](osc-client-contract.md); once per frame it captures immutable wire facts and reusable derived values. It does not command effects, choose responses, or publish one-frame event flags.

## Interface at a glance

```csharp
beatManager.IsSynced

beatManager.Timing
beatManager.Track
beatManager.Beats
beatManager.Offbeats
beatManager.Pulses
beatManager.Phrase
beatManager.NextPhrase
beatManager.Drop
beatManager.Fill
beatManager.Intro          // and Up, Down, Verse, Bridge, Chorus, Outro
beatManager.Energy
beatManager.NextEnergy
beatManager.Loop
beatManager.Grid
beatManager.Levels
```

All groups are captured structs with getters only. Snapshot-owned arrays and the live-player collection are copied before exposure. Optional wire facts (tempo, positions, counts, lengths, names) use nullable values. Boolean questions and pulses are total responses: flags such as `Drop.Active` rest at false and pulse envelopes rest at zero when their signal is unavailable. `Levels` is always present.

`IsSynced` means the current frame has the wire's usable 1-through-4 beat count. It does not hide other valid wire facts: for example, BPM and absolute track position remain readable while that count is temporarily unavailable. Only calculations that require the running count, such as beat progress and subdivision pulses, wait for synchronization.

## Timing, beats, and pulses

`Timing` groups tempo and position:

```csharp
Timing.Bpm
Timing.BeatAverageMilliseconds
Timing.Beat
Timing.TotalBeats
Timing.Bar
Timing.BeatInBar
Timing.NextBarMilliseconds
Timing.BeatProgress
Timing.BarProgress
```

The wire supplies four one-based beat-count lanes. Read them by musical count:

```csharp
beatManager.Beats.OnBeatMs(1); // milliseconds until count 1 next lands
beatManager.Beats.OnBeat(3);   // count 3's wire trigger
```

`OnBeat(count)` is active only during the first quarter of that beat interval. `Offbeats` mirrors the same interface with tempo-derived midpoints:

```csharp
beatManager.Offbeats.OffBeatMs(1);
beatManager.Offbeats.OffBeat(3);
```

`Pulses.Beat` is the wire beat pulse and `Pulses.OffBeat` is derived. Musical subdivisions are tempo-based:

```csharp
float pulse = beatManager.Pulses.Every(Duration.Sixteenth); // 1 → 0 every sixteenth; rests at 0
bool on = beatManager.Pulses.On(Duration.Eighth);           // first quarter is active
bool wider = beatManager.Pulses.On(Duration.Eighth, activeFor: 0.5f);
```

## Musical structure

Current and explicitly named next wire lanes stay separate:

```csharp
beatManager.Phrase.Name;
beatManager.Phrase.BeatsRemaining;
beatManager.Phrase.LengthBeats;
beatManager.Phrase.Irregular;
beatManager.Phrase.Progress;

beatManager.NextPhrase.Name;
beatManager.NextPhrase.BeatsUntil;
beatManager.NextPhrase.LengthBeats;
```

Drop and Fill each arrive as one wire lane. `Active` is a plain bool: true while the event is happening, false otherwise — false covers both "upcoming" and "no data," and the nullable counts say which. The wire's context-dependent count is served only under its readable names: `BeatsRemaining` is non-null while active, `BeatsUntil` while a real event is upcoming.

```csharp
beatManager.Drop.Active;
beatManager.Drop.LengthBeats;
beatManager.Drop.Remaining;
beatManager.Drop.BeatsRemaining;
beatManager.Drop.BeatsUntil;
beatManager.Drop.Progress;
```

`Fill` has the same shape. There is no separate next-drop or next-fill lane.

Energy uses the closed `Low`/`Mid`/`High` vocabulary. `Energy` holds the current run and its derived `Trend`; `NextEnergy` holds the wire's explicitly named next run.

Phrase, Energy, and Grid expose `Build()` and `Decay()` directly. Drop, Fill, and the seven Song Structure phrase handles reach theirs through two spans: `In`, through the active piece, and `Before`, approaching the next one across a window you name. Windows are counts of whole beats; with no argument `In` covers the event's own length, and a shorter window completes early and holds its endpoint:

```csharp
float fullBuild = beatManager.Drop.In.Build();
float fastBuild = beatManager.Drop.In.Build(16);
float fastDecay = beatManager.Drop.In.Decay(16);
float slowdown  = beatManager.Drop.Before.Decay(8);   // 1 far off → continuously falling to 0 at the drop
float charge    = beatManager.Drop.Before.Build(8);   // 0 far off → rising as the drop nears
```

Every envelope is total. `Before` requires its window (it has no length of its own) and rests as if the event were infinitely far: `Before.Decay` reads 1, everything else reads 0. That holds when no such event is coming, while one is already running, and in Standalone Mode — so a speed multiplier written against `Before.Decay` never needs a null check and never freezes the effect. Both spans use the same contract: a continuous normalized position shaped linearly, with `Build` equal to that position and `Decay` equal to one minus it. `In` reads continuous elapsed beats; `Before` reads the whole-beat countdown minus the current intra-beat fraction, so a countdown of 8 is 8.0 on the beat and 7.5 halfway through it. When no intra-beat fraction is available, both naturally rest on the captured whole-beat position.

These normalized values use the same two scalar helpers as the rest of the runtime. `Lerp` turns a normalized
amount into a useful range; `Remap` converts one range to another and clamps only when requested:

```csharp
float brightness = beatManager.Drop.In.Build().Lerp(0.25f, 1f);
float energy = beatManager.Levels.Smoothed.Average.Remap(0.1f, 0.8f, 0f, 1f, clamp: true);
```

`Loop` exposes all loop wire fields, including `SizeNumerator` and `SizeDenominator`, plus `NominalSizeBeats`. `Grid` exposes nullable `State`, `Beat`, `Bar`, and `Progress` plus its build/decay conveniences.

## Song Structure phrase handles

The seven typed phrase kinds of the [Song Structure](osc-client-contract.md#per-player-values) — `Intro`, `Up`, `Down`, `Verse`, `Bridge`, `Chorus`, and `Outro` — are handles carrying the same two spans, so a Performer can shape itself to song sections rather than only to drops and fills. The wire's `unknown` kind gets no handle, and structure `drop` phrases feed no handle: `Drop` and `Fill` keep their single source in the on-air event lanes.

```csharp
float bloom = beatManager.Chorus.Before.Build(32);  // rise into the next chorus
float fade  = beatManager.Outro.Before.Decay(64);   // wind down toward the outro
float shape = beatManager.Up.In.Decay(5);           // respond inside the current up section
```

They read the Focus deck — the first live player, followed with no damping, so a deck swap re-reads the new structure the same frame — through its live structure cursor, which counts only while its generation matches the held structure. Everything is positional, computed from where the cursor sits in the phrase list rather than from time accumulated across frames, so a Loop rewinding into a phrase re-enters its `In` span and a needle-drop reads correctly. `Before` targets the *next ordinal occurrence* of the kind: during a chorus, `Chorus.Before` means the following chorus, so both spans of one handle can be live in the same frame. All seven rest — zero, and `Before.Decay` at one — when no structure is held, while its phrase list is still assembling from chunks, when the cursor covers no phrase or belongs to another generation, when the kind never occurs again, whenever the wire reports no running beat count, and in Standalone Mode.

## Levels

```csharp
beatManager.Levels.Normalized
beatManager.Levels.Smoothed
beatManager.Levels.Peak
```

Every form is a `LevelBands` value with `Low`, `Mid`, `High`, `Average`, `Strongest`, `StrongestBand`, `Centroid`, and `Dominance`. When the wire levels are unavailable, Normalized becomes zero immediately; Smoothed and Peak fall toward zero according to their algorithms. Color mapping belongs to the effect consuming these values.

## Consumer-owned change detection

BeatManager exposes current state, not `Started`, `Ended`, `Changed`, `Wrapped`, or gate-opened frame flags. A consumer that needs an onset stores the prior value it cares about:

```csharp
bool dropActive = beatManager.Drop.Active;
if (dropActive && !previousDropActive)
{
    TriggerDropHit();
}
previousDropActive = dropActive;
```

This keeps BeatManager freely readable and leaves event meaning with the system that uses it. A true event mechanism can be added later if durable or cross-consumer delivery becomes a demonstrated need.
