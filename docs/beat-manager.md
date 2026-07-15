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

Drop and Fill each arrive as one wire lane. `Active` is a plain bool: true while the event is happening, false otherwise — false covers both "upcoming" and "no data," and the nullable counts say which (`BeatsUntil` is non-null only while a real event is upcoming). `Active` determines whether `CountBeats` means beats remaining or beats until the upcoming event; BeatManager preserves the raw count and also gives it the readable name:

```csharp
beatManager.Drop.Active;
beatManager.Drop.CountBeats;
beatManager.Drop.LengthBeats;
beatManager.Drop.Remaining;
beatManager.Drop.BeatsRemaining;
beatManager.Drop.BeatsUntil;
beatManager.Drop.Progress;
```

`Fill` has the same shape. There is no separate next-drop or next-fill lane.

Energy uses the closed `Low`/`Mid`/`High` vocabulary. `Energy` holds the current run and its derived `Trend`; `NextEnergy` holds the wire's explicitly named next run.

Phrase, Drop, Fill, Energy, and Grid expose `Build()` and `Decay()`. With no argument they cover the full duration. A shorter beat duration completes early and holds its endpoint:

```csharp
float fullBuild = beatManager.Drop.Build();
float fastBuild = beatManager.Drop.Build(16);
float fastDecay = beatManager.Drop.Decay(16);
```

These normalized values use the same two scalar helpers as the rest of the runtime. `Lerp` turns a normalized
amount into a useful range; `Remap` converts one range to another and clamps only when requested:

```csharp
float brightness = beatManager.Drop.Build().Lerp(0.25f, 1f);
float energy = beatManager.Levels.Smoothed.Average.Remap(0.1f, 0.8f, 0f, 1f, clamp: true);
```

`Loop` exposes all loop wire fields, including `SizeNumerator` and `SizeDenominator`, plus `NominalSizeBeats`. `Grid` exposes nullable `State`, `Beat`, `Bar`, and `Progress` plus its build/decay conveniences.

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
