# Effect and Transition Authoring Guide

Effects are plain C# classes that fill a 900-color buffer for the Penrose Wall. They are discovered automatically at runtime, so most new visuals do not need scene or prefab changes.

## Create a new effect

1. Copy `Assets/effects/EmptyEffect.cs`.
2. Rename the file and class to the new effect name.
3. Remove `[RuntimeCatalogIgnore]` from the copy.
4. Implement `Draw()`.
5. Enter Play Mode or run a compile/import check so Unity generates the new `.meta` file and compiles the class.

`EmptyEffect` itself is intentionally ignored by the runtime catalog. It exists only as a documented starter template.

[`Flock`](../Assets/effects/Flock.cs) is the advanced reference for a production music-reactive effect. Its source is organized in
reading order—signal hierarchy, artistic tuning, runtime state, lifecycle, frame pipeline, musical mappings,
and simulation—and documents why each musical source controls its particular visual consequence. Start from
`EmptyEffect`; consult `Flock` when adding Routines, calibrated levels, Standalone behavior, Fill/Drop
choreography, persistent trails, or a stateful simulation.

## Create a new transition

1. Copy `Assets/transitions/EmptyTransition.cs`.
2. Rename the file and class to the new transition name.
3. Remove `[RuntimeCatalogIgnore]` from the copy.
4. Adjust its Runway/Tail settings and implement the A-to-B blend in `Draw()`.
5. Enter Play Mode or run a compile/import check so Unity generates the new `.meta` file and compiles the class.

`EmptyTransition` is likewise ignored by the runtime catalog. Its comments explain the transition lifecycle, A-to-B progress, Runways/Tails, and the same BeatManager/Waveforms tools available to effects.

## Choose a base class

| Base class | Use when | Typical output path |
| --- | --- | --- |
| `EffectBase` | The effect works directly on Penrose tiles. | Write directly to `buffer[i]` for each tile. |
| `ScreenEffect` | The algorithm is easier in rectangular 2D coordinates. | Write to `screenBuffer`, then call `ConvertScreenBuffer(...)`. |
| `MixerBase` | The effect owns child effects and combines or transforms their buffers. | Create child effects in `OnStart()`, draw them, then combine child buffers into `buffer`. |

## Required methods

Every concrete effect must implement:

```csharp
public override string DebugText()
public override void Draw()
public override void OnEnd()
```

`OnEnd()` is currently reserved; the controller does not call it. Implement it as an empty method unless the lifecycle is changed later.

## Lifecycle

```text
Init()       once after reflection creates the catalog instance
OnStart()   whenever the effect becomes active
UpdateTime() called by Controller before Draw()
OnNewGrid() once when this Effect observes the 16-beat Grid return to one
Draw()      every active frame
OnEnd()     not currently called
```

`OnNewGrid()` is a base hook on `EffectBase`. `UpdateTime()` compares the current `Grid.Beat` with that Effect's prior observation and calls the hook when the count returns to one. Override it to re-roll a look, switch palette, or acquire a new Waveform in step with the music. An effect nested in a mixer only receives it if the mixer calls the child's `UpdateTime()`.

Use `Init()` for reusable setup that depends on `Controller.Instance`, `penrose`, or `tiles` existing.

Use `OnStart()` for per-activation state: random parameters, child effect selection, Waveform acquisition/alignment, or clearing persistent buffers.

Use `Draw()` for the frame algorithm. A valid draw writes every slot in `buffer`, unless the effect intentionally uses trails/fading and documents that behavior.

## Useful base members

| Member | Meaning |
| --- | --- |
| `buffer` | This effect's 900-color output frame. |
| `penrose` | The active Penrose model and JSON data. |
| `tiles` | Cached tile metadata from `Penrose.Tiles`. |
| `effectTime` | Seconds since the effect's randomized seed time. |
| `effectDelta` | Current frame delta time. |
| `APalette` | Shared animated palette for all effects. |
| `beatManager` | Shared read-only wire facts and derived musical values. |
| `waveforms` | Shared Waveform acquisition tools. |
| `waveform` | Public non-null artistic configuration. Effects acquire it explicitly; owners may share, replace, or assign `waveforms.None`. |

## Catalog identity

The runtime catalog is sorted by type full name. For a fixed set of classes, indexes are deterministic. Adding, removing, or renaming an effect can still shift indexes.

For debugging and force-selection, prefer names over numeric indexes. `forceEffectName` matches effect names by case-insensitive substring.

## Child effects in mixers

Mixer effects create child effects manually by calling `GetRandomEffect()`. Those children are not top-level catalog instances and are not driven by `Controller` directly.

A mixer that creates a child effect should usually call:

```csharp
child.Init();
child.OnStart();
```

After `OnStart()`, the mixer may directly configure the child. Assign the mixer's `waveform` for unison, assign `waveforms.None` to suppress child Waveform response, or leave it unchanged for independent behavior. These are ordinary object assignments, not runtime modes. If unison or suppression must persist across Grid wraps, reapply the assignment after `child.UpdateTime()` and before `child.Draw()`, because the child's Grid hook may acquire a new Waveform.

Waveform response reads from the value itself. Use `waveform.Envelope` for a raw `[0..1]` lift, or `waveform.Lerp(from, to)` when the rhythm maps between two artistic endpoints. Without live placement, `Envelope` is 0 and `Lerp` returns `to`, preserving the effect's explicit Standalone response without nullable syntax.

Then, inside `Draw()`:

```csharp
child.UpdateTime();
child.Draw();
```

`MixerBase.GetRandomEffect()` avoids returning other mixers, preventing recursive mixer trees.

## Packed Penrose shape arrays

Several effects use packed shape lists from `penrose.Layout.shapes`, especially `TileShapes`, `Petals`, `AnimateLoops`, `ShapeGlitch`, `Mirror`, and `kscope`.

The common format is:

```text
shape[0] = number of tile indexes in this shape
shape[1..shape[0]] = tile indexes or pointers, depending on the source list
```

Check the consuming effect before reusing a shape list. Some lists are direct tile lists; others are lists of indexes into another shape collection.

## Reacting to musical structure (Fill and Drop)

A **Fill** is the build that leads up to a change; a **Drop** is the impact at the change. An effect can express both. The pattern below is the one used by `Tunnel`, and it generalizes to any effect with a continuous phase/motion term.

### 1. Advertise the capability

Override `Repertoire` so the Director can cast this effect into those moments:

```csharp
public override Repertoire Repertoire => Repertoire.HandlesFill | Repertoire.HandlesDrop;
```

### 2. Find the motion term, and never scale `effectTime`

Identify the one accumulator that drives the look (for `Tunnel`: `phase = i*density + effectTime*speed + distance*mix`). `effectTime` is seeded with a large random offset (0–14400s), so multiplying `effectTime*speed` to "speed up" teleports the phase. Instead, keep a **separate bounded accumulator** per response and integrate a rate into it each frame:

```csharp
fillScroll = Mathf.Repeat(fillScroll + speed * FillRush * fillEnv * effectDelta, 1f);
dropScroll = Mathf.Repeat(dropScroll - speed * DropRush * dropEnv * effectDelta, 1f);
// phase = (... + fillScroll + dropScroll + ...) % 1f
```

`Mathf.Repeat(…, 1f)` keeps each accumulator in `[0,1)` so it never drifts. Fill adds (`+`), Drop subtracts (`-`) — make the two motions **opposite** so the Drop reads as an inversion of the Fill, not just "more of it."

### 3. Read direct structure values and envelopes

Fill and Drop each keep their raw countdown fields beside readable interpretations (`Active`, `BeatsRemaining`, `BeatsUntil`, and `Progress`). Their `Build()` and `Decay()` conveniences rest at zero when the event is inactive. `Tunnel` uses them directly:

```csharp
fillEnv = beatManager.Fill.Build();
dropEnv = beatManager.Drop.Decay(DropBars * 4f);
```

Read `Active`, `CountBeats`, `LengthBeats`, and `Remaining` when the Effect needs wire facts. If an Effect needs an onset, retain its own prior `Active` value and compare locally; BeatManager deliberately exposes state, not one-frame event flags.

### 4. Acquire Waveforms explicitly

The base exposes the sibling `waveforms` root and neutral public `waveform` configuration, but it performs no acquisition. A concrete Effect chooses when and what to draw:

```csharp
public override void OnStart()
{
    waveform = waveforms.Random(Energy.Low, Energy.Mid);
}

protected override void OnNewGrid()
{
    waveform = waveforms.Random(Energy.Low, Energy.Mid);
}

// In Draw():
float brightness = waveform.Lerp(BeatBrightnessFloor, 1f);
```

Transitions follow the same ownership rule: expose their own public artistic configuration when it should be tunable, acquire explicitly in their concrete lifecycle, and choose their own `Envelope`/`Lerp` mapping.

### 5. Fold envelopes into every consequence, and expose them

One envelope can drive several visual results (scroll **and** zoom) so the gesture feels coherent: `zoom = 1 + FillZoom*fillEnv + DropZoom*dropEnv`. Make every magnitude a named, documented `const`, and surface the live envelopes on `DebugText()` (`FILL 0.83`, `DROP 0.41`) so they can be tuned on the wall instead of by guessing.

### Keep artistic policy in the Performer

BeatManager and Waveforms provide shared musical facts, Edges, Stock Envelopes, and acquisition tools. The concrete Effect or Transition owns how those inputs affect color, motion, timing, fallback, and local state. Do not add automatic acquisition, replacement, or response policy to an authoring base. The existing `EffectBase.UpdateTime()` → `OnNewGrid()` hook is a narrow shared seam for the captured Grid wrap Edge; overriding it remains a concrete Effect decision.

A Mixer is still one Effect to the rest of the runtime. It privately owns its child Effects and can directly set their public artistic state, including sharing a held Waveform or assigning `waveforms.None`; it does not publish child policy as a second runtime system.

## Documentation expectations for new effects

Each new effect should include a class summary explaining:

- what it renders;
- which base class it uses and why;
- important data sources, such as tile geometry, shapes, palette, beat, or child effects;
- any intentional trails, persistent buffers, randomization, or performance tradeoffs.

Add comments for non-obvious math or mapping decisions. Avoid comments that merely restate assignments.
