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
Draw()      every active frame
OnEnd()     not currently called
```

Use `Init()` for reusable setup that depends on `Controller.Instance`, `penrose`, or `tiles` existing.

Use `OnStart()` for per-activation state: random parameters, child effect selection, beat variant alignment, or clearing persistent buffers.

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
| `beatManager` / `beat` | Global beat state and helper methods. |
| `beatVariant` | Rhythmic personality selected in `OnStart()`. |
| `beatEnable` | Whether the effect should react to beat brightness. |

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

Then, inside `Draw()`:

```csharp
child.UpdateTime();
child.Draw();
```

`MixerBase.GetRandomEffect()` avoids returning other mixers, preventing recursive mixer trees.

## Packed Penrose shape arrays

Several effects use packed shape lists from `penrose.JsonRawData.shapes`, especially `TileShapes`, `Petals`, `AnimateLoops`, `ShapeGlitch`, `Mirror`, and `kscope`.

The common format is:

```text
shape[0] = number of tile indexes in this shape
shape[1..shape[0]] = tile indexes or pointers, depending on the source list
```

Check the consuming effect before reusing a shape list. Some lists are direct tile lists; others are lists of indexes into another shape collection.

## Documentation expectations for new effects

Each new effect should include a class summary explaining:

- what it renders;
- which base class it uses and why;
- important data sources, such as tile geometry, shapes, palette, beat, or child effects;
- any intentional trails, persistent buffers, randomization, or performance tradeoffs.

Add comments for non-obvious math or mapping decisions. Avoid comments that merely restate assignments.
