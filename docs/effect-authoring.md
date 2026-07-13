# Effect Authoring Guide

Effects are plain C# classes that fill a 900-color buffer for the Penrose Wall. They are discovered automatically at runtime, so most new visuals do not need scene or prefab changes.

## Create a new effect

1. Copy `Assets/effects/EmptyEffect.cs`.
2. Rename the file and class to the new effect name.
3. Remove `[RuntimeCatalogIgnore]` from the copy.
4. Implement `Draw()`.
5. Enter Play Mode or run a compile/import check so Unity generates the new `.meta` file and compiles the class.

`EmptyEffect` itself is intentionally ignored by the runtime catalog. It exists only as a documented starter template.

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
OnNewGrid() once when the BeatManager reports a new 16-beat Grid
Draw()      every active frame
OnEnd()     not currently called
```

`OnNewGrid()` is a base hook on `EffectBase`. `UpdateTime()` forwards BeatManager's frame-coherent `Grid.Wrapped` edge exactly. Override it to re-roll a look, switch palette, or acquire a new Waveform in step with the music. An effect nested in a mixer only receives it if the mixer calls the child's `UpdateTime()`.

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
| `beatManager` | Shared read-only musical facts, edges, and stock envelopes. |
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

### 3. Shape each moment with the right envelope

- **Fill is continuous** — drive it from `BeatManager.Fill` progress so it ramps with the music. Use fast attack / slow release so even a one-beat fill slams to full and tails off cleanly:

  ```csharp
  PhraseEventInfo? fill = beatManager.Fill;
  float fillTarget = fill is { inProgress: true } ? Mathf.Clamp01(fill.Value.progress ?? 0f) : 0f;
  float fillRate = fillTarget > fillEnv ? FillAttack : FillRelease;   // e.g. 22 / 5
  fillEnv = Mathf.Lerp(fillEnv, fillTarget, 1f - Mathf.Exp(-fillRate * effectDelta));
  ```

- **Drop is a one-shot** — snap to 1 at the instant, then `SmoothStep`-decay over a BPM-derived duration (with a seconds fallback when no BPM):

  ```csharp
  dropSeconds = beatManager.Bpm is { } bpm && bpm > 0f
      ? (60f / bpm) * BeatsPerBar * DropBars : DropFallbackSeconds;
  // in Draw(): dropEnv = 1f - Mathf.SmoothStep(0f, 1f, dropElapsed / dropSeconds);
  ```

### 4. Trigger the Drop off the grid edge, once per drop

The Drop fires on beat one of the grid *inside* a drop — a discrete instant — so ride the `OnNewGrid()` hook, not a poll in `Draw()`. Latch it so it fires once per drop, and clear the latch in `Draw()` when the drop ends:

```csharp
protected override void OnNewGrid()
{
    Reroll();                                          // fresh look every grid
    if (beatManager.Drop is { inProgress: true } && !dropFlashed)
    {
        TriggerDrop();                                 // dropEnv = 1; reset decay clock
        dropFlashed = true;
    }
}
// in Draw(): if (!(beatManager.Drop is { inProgress: true })) dropFlashed = false;
```

### 5. Fold envelopes into every consequence, and expose them

One envelope can drive several visual results (scroll **and** zoom) so the gesture feels coherent: `zoom = 1 + FillZoom*fillEnv + DropZoom*dropEnv`. Make every magnitude a named, documented `const`, and surface the live envelopes on `DebugText()` (`FILL 0.83`, `DROP 0.41`) so they can be tuned on the wall instead of by guessing.

### Lift shared plumbing into the base

When the same beat-detection plumbing appears in a second effect, it belongs in `EffectBase`, not copied. The grid-downbeat edge detection now lives in `EffectBase.UpdateTime()` → `OnNewGrid()` for exactly this reason; effects just override the hook.

## Documentation expectations for new effects

Each new effect should include a class summary explaining:

- what it renders;
- which base class it uses and why;
- important data sources, such as tile geometry, shapes, palette, beat, or child effects;
- any intentional trails, persistent buffers, randomization, or performance tradeoffs.

Add comments for non-obvious math or mapping decisions. Avoid comments that merely restate assignments.
