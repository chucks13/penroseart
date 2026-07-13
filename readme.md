# PenroseArt

PenroseArt is a Unity-hosted C# runtime for the Penrose Wall LED installation. It renders generative visuals for a 900-tile Penrose model, previews them in Unity, and outputs the same buffer to LED hardware.

Unity is the host and simulator. The effect system itself is mostly plain C# so visuals can be authored by copying and editing effect classes rather than wiring new scene objects.

## Start here

- [`docs/runtime-architecture.md`](docs/runtime-architecture.md) — how the Controller, catalogs, buffers, transitions, inputs, and outputs fit together.
- [`docs/effect-authoring.md`](docs/effect-authoring.md) — how to create effects, use the lifecycle, and work with buffers.
- [`docs/code-map.md`](docs/code-map.md) — file-by-file map of the project-authored runtime code.
- [`CONTEXT.md`](CONTEXT.md) — operational project context and platform/output notes.
- [`Assets/core/Hardware/S2_MINI_PROTOCOL.md`](Assets/core/Hardware/S2_MINI_PROTOCOL.md) — USB serial protocol used by the S2 Mini / ESP32 boards.
- [`docs/investigation/`](docs/investigation/) — historical research/audit notes; useful context, but not canonical current docs.

## Runtime loop

The normal animation cycle is:

1. Select an effect from the effect deck.
2. Play it for `effectTime` seconds.
3. Select a destination effect and transition.
4. Transition from the current effect to the destination over `transitionTime` seconds.
5. Repeat.

Effects and transitions are selected from rotating decks. A card is drawn from the top half of the deck, then moved to the bottom. This gives random variety while reducing immediate repeats.

## Effect lifecycle

Each top-level effect has one runtime catalog instance.

```text
Init()       called once after reflection creates the effect
OnStart()   called every time the effect becomes active
UpdateTime() called before Draw() while active
Draw()      called every active frame
OnEnd()     reserved; Controller does not currently call it
```

Effects write into a `Color[] buffer` with `Penrose.Total == 900` entries. The controller copies the active effect or transition buffer into `penrose.buffer`, applies overlays/blending, sends hardware output, then updates the Unity preview mesh.

## Authoring effects and transitions

For a new effect, copy `Assets/effects/EmptyEffect.cs`, rename the file and class, remove `[RuntimeCatalogIgnore]`, and implement `Draw()`.

For a new transition, copy `Assets/transitions/EmptyTransition.cs`, rename the file and class, remove `[RuntimeCatalogIgnore]`, and implement its A-to-B blend and timing settings.

Choose the base class by shape:

- `EffectBase` for direct tile algorithms;
- `ScreenEffect` for rectangular 2D algorithms mapped onto Penrose tiles;
- `MixerBase` for wrappers/mixers that own child effects.

## Catalogs and indexing

Effects, transitions, and blenders are discovered by `Factory<T>` using reflection. Catalog entries are sorted by type full name, so indexes are deterministic for a fixed set of classes.

Indexes are not permanent IDs. Adding, removing, or renaming classes can shift sorted indexes. For debugging and inspection, prefer name-based controls such as `forceEffectName`.

## Beat system

`BeatManager` provides a shared beat clock for rhythmic visuals.

- Effects can use `beatManager.GetBeatBrightness()` for pulse-like brightness.
- `OnStart()` chooses a `beatVariant` so effects can have different rhythmic personalities.
- The current beat source is simulated/debug timing from Unity time, not an external sync source.
- If beat behavior is inactive, effects generally return to standard brightness/motion.

## Palette system

`EffectBase.APalette` is a shared animated palette. Effects sample it with normalized positions, usually through:

```csharp
APalette.read(position, interpolate)
```

The controller updates the palette every frame. Pressing Return reloads palette definitions at runtime, and transitions trigger palette changes for variety.

## Output paths

The active compiled output path is USB serial through `SerialOut` because `Controller.cs` currently defines `ENABLE_SERIAL`.

The older ACN/E1.31 UDP path remains in the code and is used only when serial is not compiled in.

## Debug controls

The live force-effect override is intended for development and visual testing:

- `forceEffect`: enable/disable the override from the Inspector or Escape key.
- `forceEffectName`: case-insensitive substring match against effect class names.

When enabled and matched, the controller cancels transitions, jumps immediately to the matching effect, and stays there while the force remains active.
