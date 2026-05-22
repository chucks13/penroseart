# PenroseArt Runtime Architecture

PenroseArt is a Unity-hosted C# runtime for the Penrose Wall LED installation. Unity provides the scene host, editor workflow, preview mesh, UI text, keyboard input, and platform packaging. The visual system itself is mostly plain C# classes that render into fixed-size color buffers and then send those buffers to both the Unity preview mesh and hardware output.

## Runtime shape

```text
Unity scene
  └─ Controller MonoBehaviour
       ├─ Penrose model and preview mesh
       ├─ effect catalog: EffectBase[]
       ├─ transition catalog: TransitionBase[]
       ├─ blender catalog: BlenderBase[]
       ├─ shared systems: palette, beat manager, drums, timer
       ├─ inputs: keyboard, OSC, optional PixelReceiver/camera/telnet
       └─ outputs: serial hardware path or legacy UDP/ACN path
```

The project intentionally does **not** model every effect as a scene object. Effects, transitions, and blenders are runtime objects discovered from C# types. This keeps authoring close to creative-coding practice: copy a class, rename it, implement the frame algorithm, and let the runtime catalog discover it.

## Startup sequence

`Controller.Start()` is the main startup boundary.

1. Find and initialize the scene's `Penrose` component.
2. Discover and instantiate every non-ignored `EffectBase` subclass through `Factory<EffectBase>`.
3. Discover and instantiate every non-ignored `TransitionBase` subclass.
4. Discover and instantiate every non-ignored `BlenderBase` subclass.
5. Create plain C# helpers such as `drums`, `PixelReceiver`, `BeatManager`, `Timer`, and optionally `CameraReader` / `SerialOut`.
6. Register OSC handlers and timer callbacks.
7. Enter the frame loop in `Controller.Update()`.

Unity calls lifecycle methods only on `MonoBehaviour` components. Most PenroseArt runtime objects are plain C# objects, so `Controller` calls their lifecycle methods manually.

## Catalog discovery and indexing

`Factory<T>` builds catalogs by reflecting over the assembly that contains `T`.

A type appears in the catalog when it is:

- a concrete class;
- not abstract;
- a subclass of the catalog base type; and
- not marked with `[RuntimeCatalogIgnore]`.

The resulting types are sorted by `Type.FullName` using ordinal comparison. This makes indexes deterministic for a fixed set of classes.

Indexes are still not permanent IDs. Adding, removing, or renaming an effect can move later sorted indexes. Use name-based controls for debugging and operator workflows when possible.

## Effect lifecycle

Each top-level effect has one catalog instance.

```text
Init()       once after creation
OnStart()   every time the effect becomes active
UpdateTime() every active frame before Draw()
Draw()      every active frame
OnEnd()     reserved, but Controller does not currently call it
```

`EffectBase.Init()` connects the effect to `Controller.Instance`, the active `Penrose` model, tile metadata, and a `Color[] buffer` sized to `Penrose.Total`.

`EffectBase.Draw()` implementations write one frame into that local `buffer`. The controller then copies the active effect buffer into `penrose.buffer`.

## Transition lifecycle

Transitions blend two effect indexes:

- `A`: source effect index;
- `B`: destination effect index;
- `V`: progress from `0` to `1`;
- `D`: remaining progress, `1 - V`.

When the effect timer finishes while not transitioning, `Controller.OnTimerFinished()` selects the next effect and transition, sets `A` / `B`, calls the transition's `OnStart()`, starts the destination effect, and switches to transition mode.

During transition frames, the controller updates both effects, calls the active transition's `Draw()`, and copies the transition buffer into `penrose.buffer`.

When the transition timer finishes, the destination effect becomes `currentEffect`, transition mode ends, and the next transition is drawn from the transition deck.

## Deck selection

Effects and transitions use rotating integer decks.

1. Initialize a deck as `[0, 1, 2, ... count - 1]`.
2. Pick a random index from the top half of the deck.
3. Remove that entry.
4. Move it to the bottom.

This gives variety without immediate repeats while still eventually cycling through the catalog.

## Force-effect override

`forceEffect` and `forceEffectName` are the primary live debugging controls for selecting an effect by name. When enabled, the controller searches `effects[i].Name` with a case-insensitive substring match.

When a match exists, the controller cancels any active transition, jumps immediately to the matching effect, and prevents timer expiry from transitioning away while the override remains active.

## Buffer flow

```text
Effect or transition buffer
  -> penrose.buffer
  -> optional filter/drum/camera/pixel-source blending
  -> serial or UDP hardware output
  -> Penrose.UpdateModelColors() for Unity preview
```

`Penrose.Total` is 900 logical tiles. The active serial path expands those logical tiles through the JSON `wires` map into physical LED order before sending packets to the S2 Mini / ESP32 boards.

## Major subsystems

| Area | Main files | Responsibility |
| --- | --- | --- |
| Runtime hub | `Assets/core/Controller.cs` | Catalogs, lifecycle, timing, input routing, output routing, preview update. |
| Geometry/model | `Assets/core/Penrose.cs` | JSON data, tile metadata, Unity mesh generation, buffer-to-mesh colors. |
| Effects | `Assets/core/EffectBase.cs`, `Assets/effects/*.cs` | Generate 900-tile frames. |
| Screen effects | `Assets/core/ScreenEffect.cs` | Map rectangular screen buffers onto the Penrose tile layout. |
| Mixers/wrappers | `Assets/core/MixerBase.cs`, mixer effects | Own child effects and combine/transform their buffers. |
| Transitions | `Assets/core/TransitionBase.cs`, `Assets/transitions/*.cs` | Blend effect A to effect B, and sometimes act as external-source blenders. |
| External blenders | `Assets/core/helpers/BlenderBase.cs`, `Assets/blenders/*.cs` | Mix incoming pixel-source data with the native Penrose buffer. |
| Palette | `Assets/core/helpers/GPalette.cs` | Global palette sampling and animated palette transitions. |
| Beat/drums | `Assets/core/BeatManager.cs`, `Assets/core/drums.cs` | Simulated beat clock and drum/ring overlays. |
| Serial output | `Assets/core/SerialOut.cs` | USB serial discovery and frame output for S2 Mini / ESP32 boards. |
| OSC | `Assets/OSC.cs`, `Assets/OSCReader.cs` | OSC parsing/serialization and active OSC reader component. |

## Known architectural pressure points

These are documented facts, not requests to change behavior during documentation work.

- `Controller` owns many responsibilities and is the primary future refactor target.
- Several numeric scene fields still depend on catalog indexes; name-based controls are safer.
- `OnEnd()` exists on effects/transitions but is not called by the current controller.
- `Controller - nova.cs` is inactive reference code under `#if false` and references missing/incompatible concepts.
- Optional telnet code is inactive by default and should be revisited before re-enabling.
