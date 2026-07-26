# Rhythm Quickstart — BeatManager, Waveforms, and Routines

A friendly guide to making a Penrose effect dance. This is the "how do I use it"
companion to the deeper references:

- [`beat-manager.md`](beat-manager.md) — every musical value BeatManager exposes.
- [`waveform-system.md`](waveform-system.md) — the Waveform model, notation, and Pool file format.
- [`effect-authoring.md`](effect-authoring.md) — the full effect/transition authoring guide.

## The big picture

Three pieces work together:

1. **BeatManager** — the read-only musical data hub. When RaveSystem is broadcasting
   OSC (UDP 7000), it knows the tempo, beat position, phrase, fills, drops, energy,
   and audio levels of whatever the DJ is playing, refreshed once per frame.
2. **Waveforms** — a pool of one-bar rhythmic envelopes (think "brightness patterns")
   you can grab and hold. A held Waveform plays itself against the live beat.
3. **Routine** — four Waveforms strung across the 16-beat Grid, so your effect can
   have a four-bar choreography instead of repeating one bar forever.

Two rules shape everything:

- **Everything is read-only.** BeatManager never tells your effect what to do.
  Your effect reads values and owns every artistic decision.
- **No beat is a normal state.** Without live OSC there is no beat (Standalone).
  Musical values become `null`, Waveform envelopes rest at 0, and `Lerp(from, to)`
  returns `to` — so your effect keeps a steady, intentional look instead of glitching.

## Reading the beat

Every effect gets `beatManager`. Its values live in small named groups:

```csharp
beatManager.IsSynced              // do we have the live 1-2-3-4 count this frame?

beatManager.Timing.Bpm            // float? — null when no live tempo
beatManager.Timing.BarProgress    // float? — 0 on the downbeat → 1 at the next

beatManager.Grid.Bar              // int? — 1..4 within the shared 16-beat Grid
beatManager.Grid.Progress         // float? — 0..1 through the whole Grid

beatManager.Fill.Active           // bool — is a fill happening right now?
beatManager.Drop.Active           // bool — is a drop happening right now?
beatManager.Energy.Level          // Energy? — Low, Mid, or High
beatManager.Levels.Smoothed       // audio bands: .Low .Mid .High .Average ...
```

Three kinds of values, three rest states:

- **Yes/no questions** (`Drop.Active`, `Fill.Active`, `Pulses.On(...)`) are plain
  bools. No music or no data simply reads **false**.
- **Pulse envelopes** (`Pulses.Beat`, `Pulses.Every(...)`) are plain floats that
  rest at **0**, like `waveform.Envelope`.
- **Facts** (BPM, `Grid.Bar`, progress values, phrase names, `Energy.Level`) are
  nullable — `null` means "the wire doesn't know right now." It's a normal
  musical state (a track may simply have no drop data), not an error.

### Dealing with nullable facts

The compiler won't let you use a `float?` directly — that's deliberate: it forces
every effect to choose its no-music look instead of glitching. Each call site is
one extra token:

```csharp
// Pick a fallback with ??
float progress = beatManager.Grid.Progress ?? 0f;
float bpm      = beatManager.Timing.Bpm ?? 120f;
Energy energy  = beatManager.Energy.Level ?? Energy.Mid;

// Or branch when you want a different code path with no beat:
if (beatManager.Timing.BarProgress is { } barPhase)
    ScrollWith(barPhase);     // live: barPhase is a plain float here
else
    ScrollSteadily();         // Standalone: your explicit no-music behavior
```

Bools need none of this — read them directly:

```csharp
if (beatManager.Drop.Active)      // "a drop is happening right now"
    ...
```

One subtlety: `Drop.Active == false` covers both "a drop is coming" and "no drop
data at all." When that difference matters (landing a look *on* the drop), the
nullable counts carry it — `Drop.BeatsUntil` is non-null only while a real drop
is upcoming, counting down to it.

And you often don't need the null at all: `Build()`/`Decay()` return plain
floats, and `waveform.Lerp(from, to)` already folds the no-beat state into your
`to` endpoint. Reach for those before hand-rolling null checks.

Three helpers turn raw values into visuals:

```csharp
// Build/Decay: 0→1 ramps over a musical duration (Phrase, Energy, Grid directly;
// Drop and Fill through their In/Before spans). Windows are whole beats.
float tension = beatManager.Fill.In.Build();        // rises 0→1 across the fill
float flash   = beatManager.Drop.In.Decay(16);      // 1→0 over the first 16 beats of the drop
float slowing = beatManager.Drop.Before.Decay(8);   // 1→0 across the last 8 beats before it lands

// Lerp: map a normalized amount into your artistic range.
float brightness = beatManager.Drop.In.Build().Lerp(0.25f, 1f);

// Remap: convert one range to another.
float glow = beatManager.Levels.Smoothed.Average.Remap(0.1f, 0.8f, 0f, 1f, clamp: true);
```

BeatManager has **no event flags** ("drop just started"). If you need an onset,
remember last frame's value yourself:

```csharp
bool dropActive = beatManager.Drop.Active;
if (dropActive && !previousDropActive)
    TriggerDropHit();
previousDropActive = dropActive;
```

## Waveforms

A **Waveform** is a one-bar brightness envelope: 1 on a beat, falling to 0 in the
trough between beats. You don't build them in code — you grab one from the
hand-curated Pool and hold it:

```csharp
public override void OnStart()
{
    base.OnStart();
    waveform = waveforms.Random();                        // anything from the Pool
    // or filter by intensity:
    waveform = waveforms.Random(Energy.Low, Energy.Mid);  // calmer shapes only
}
```

A held Waveform plays itself — no per-frame bookkeeping:

```csharp
public override void Draw()
{
    float envelope   = waveform.Envelope;         // raw 0..1, rests at 0 with no beat
    float brightness = waveform.Lerp(0.35f, 1f);  // maps trough→peak; returns 1f with no beat
    ...
}
```

`Lerp(from, to)` is the workhorse: `from` is your look at the trough, `to` at the
peak — **and `to` is also the Standalone fallback**, so choose it as the look you
want when there's no music.

### Changing with the music

The shared Grid is a rolling 16-beat (four-bar) cycle. When it wraps, every effect
gets a hook — the natural place to re-roll your rhythm so the effect doesn't feel
frozen:

```csharp
protected override void OnNewGrid()
{
    waveform = waveforms.Random();
}
```

Skip the override if the effect should keep its Waveform for its whole run.

### What's in the Pool

The Pool is a plain text file at `Assets/StreamingAssets/penrose_waveforms.txt`,
one Preset per line:

```
DEFINE_WAVEFORM(beat pulse)    { QQQQ | 8888 | 0.3 | 0   }
DEFINE_WAVEFORM(offbeat)       { QQQQ | 8888 | 0.3 | 0.5 }
DEFINE_WAVEFORM(measure start) { QQQQ | 8000 | 0.3 | 0   }
```

The four fields:

| Field | Meaning |
| --- | --- |
| sequence | one note-value token per hump: `W` whole, `H` half, `Q` quarter, `E` eighth, `S` sixteenth — widths must sum to one bar |
| amplitude | one digit `0–8` per hump (height ÷ 8); `0` = silent, which is how a beat is skipped |
| rounding | peak shape `0..1`: sharp triangle → cosine dome → flat top |
| offset | phase shift in beats; `0.5` lands on the offbeat "&" |

So `QQQQ / 8080` is "hit beats 1 and 3." Each Waveform's `Energy` (Low/Mid/High)
is derived automatically from how busy the notation is — never authored by hand.

Edit the Pool with **Window ▸ Penrose ▸ Waveform Pool Editor** in Unity, which
previews shapes as you type. (You can hand-edit the file, but an editor save
rewrites it canonically — comments and formatting are not preserved.) A malformed
Pool entry fails loudly at startup rather than silently playing a fallback.

## Routines

A **Routine** is four Waveforms, one per bar of the 16-beat Grid. It reads exactly
like a Waveform — same `Envelope`, same `Lerp` — but the current Grid bar picks
which shape plays:

```csharp
private Routine routine;

public override void OnStart()
{
    base.OnStart();
    routine = Routine.Of(
        waveforms.Random(Energy.Mid),
        waveforms.Random(Energy.Low),
        waveforms.Random(Energy.Mid),
        waveforms.Random(Energy.High));
}

public override void Draw()
{
    float brightness = routine.Lerp(0.35f, 1f);
    ...
}
```

That's the whole API — compose four acquired Waveforms with `Routine.Of(...)`,
then read it. Want a different choreography? Compose a new one (usually in
`OnNewGrid()`). Two constraints: all four Waveforms must come from `waveforms`,
and `waveforms.None` can't be placed in a Routine.

A nice pattern from `Flock.cs`: pick the four Energy tiers from the track's
current energy, so the choreography matches the music's intensity:

```csharp
Energy energy = beatManager.Energy.Level ?? Energy.Mid;
Energy[] recipe = energy switch
{
    Energy.Low  => new[] { Energy.Low,  Energy.Low, Energy.Low, Energy.Mid },
    Energy.Mid  => new[] { Energy.Mid,  Energy.Mid, Energy.Mid, Energy.Low },
    Energy.High => new[] { Energy.High, Energy.Mid, Energy.Mid, Energy.Low },
};
routine = Routine.Of(
    waveforms.Random(recipe[0]),
    waveforms.Random(recipe[1]),
    waveforms.Random(recipe[2]),
    waveforms.Random(recipe[3]));
```

## A complete minimal effect

```csharp
using UnityEngine;

public class HeartbeatWash : EffectBase
{
    public override void OnStart()
    {
        base.OnStart();
        waveform = waveforms.Random(Energy.Low, Energy.Mid);
    }

    protected override void OnNewGrid()
    {
        waveform = waveforms.Random(Energy.Low, Energy.Mid);
    }

    public override void Draw()
    {
        // Pulses 0.4→1 with the beat; holds steady at 1 (the `to` endpoint) with no music.
        float brightness = waveform.Lerp(0.4f, 1f);

        // Push toward white while a fill builds tension.
        float whiten = beatManager.Fill.In.Build() * 0.5f;

        Color color = Color.Lerp(Color.blue, Color.white, whiten) * brightness;
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = color;
    }
}
```

Start from `Assets/effects/EmptyEffect.cs` (copy, rename, drop
`[RuntimeCatalogIgnore]`), and look at `Assets/effects/Flock.cs` for the fully
worked example: Routines, energy recipes, audio levels, and Standalone behavior.

## Cheat sheet

| I want... | Use |
| --- | --- |
| Pulse with the beat | `waveform.Lerp(from, to)` |
| A rhythm that varies over four bars | `Routine.Of(...)` + `routine.Lerp(from, to)` |
| Calmer / busier rhythms | `waveforms.Random(Energy.Low)` / `(Energy.High)` |
| Change rhythm as the music moves | re-acquire in `OnNewGrid()` |
| Tension rising through a fill or drop | `beatManager.Fill.In.Build()` / `Drop.In.Build()` |
| A flash that fades after the drop | `beatManager.Drop.In.Decay(beats)` |
| Slow into an approaching drop | `beatManager.Drop.Before.Decay(8)` (rests at 1 — safe to multiply) |
| Charge up as a drop approaches | `beatManager.Drop.Before.Build(8)` |
| React to actual audio loudness | `beatManager.Levels.Smoothed` / `.Peak` |
| Know if there's a live beat at all | `beatManager.IsSynced` |
| Handle a nullable fact | `?? fallback`, or `is { } x` to branch (bools and pulses are never null) |
| A sane look with no music | pick your `to` endpoint — it's the fallback |
