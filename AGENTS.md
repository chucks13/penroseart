# AGENTS.md - PenroseArt

> Maintenance note: `AGENTS.md`, `CLAUDE.md`, and `GEMINI.md` are intentionally kept byte-identical. Edit `AGENTS.md` first, then copy the result to the other two files.

This repo is a Unity-hosted C# creative/hardware runtime for the Penrose Wall LED installation. Follow the existing project shape instead of turning it into a conventional Unity application.

## Development Philosophy

- Treat the core C# runtime as the product. Unity scene objects, UI, and assets wrap around these core files; they are not the primary architecture.
- Preserve the current creative-coding / installation-controller style unless the user explicitly asks for a larger architecture change.
- Prefer small, direct changes to the existing systems over new frameworks, service layers, ScriptableObject registries, prefab hierarchies, or generic abstractions.
- Small and direct does not mean smallest possible diff. If the touched system's shape is the problem, fix that shape within the requested scope instead of layering a workaround onto it.
- When the user asks to replace a source of truth or data model, make the existing core system reflect the new model and update its real consumers. Do not import new runtime data into a side snapshot while leaving the application on the old model unless the user asks for a staged migration.
- Do not "normalize" the project into typical enterprise Unity patterns without approval.
- This project is not primarily TDD-driven. Add pragmatic tests when changing testable core logic, protocol handling, mapping, palette/beat behavior, or new abstractions; do not create heavy test infrastructure for purely visual tuning unless requested.

## Core Files and Systems

Start with these before adding new structures:

- `Assets/core/Controller.cs` - main runtime hub: initialization, timing, effect/transition selection, input, output, overlays, and mesh updates.
- `Assets/core/Penrose.cs` - 900-tile Penrose model, JSON data, mesh generation, tile metadata, and buffer-to-mesh color mapping.
- `Assets/core/EffectBase.cs` - base contract for direct generative effects.
- `Assets/core/ScreenEffect.cs` - helper for effects that render into a rectangular buffer before mapping onto Penrose tiles.
- `Assets/core/MixerBase.cs` - base for effects that own child effects and combine or transform their buffers.
- `Assets/core/TransitionBase.cs` - base for transitions between effects and some external-source blend behavior.
- `Assets/core/helpers/Factory.cs` - reflection-based discovery and instantiation of effect, transition, and blender classes.
- `Assets/core/helpers/GPalette.cs` and `Assets/core/BeatManager.cs` - shared color and rhythm systems. If a task changes the live beat source or beat data contract, refactoring `BeatManager.cs` and its direct effect/consumer call sites is in scope.
- `Assets/core/SerialOut.cs`, `Assets/OSCReader.cs`, `Assets/core/PixelReceiver.cs`, and `Assets/core/drums.cs` - hardware/control/input paths.

`Controller.cs` is intentionally central. Refactor it only with explicit approval because many hardware, scene, and runtime behaviors pass through it. Small wiring changes needed to connect an approved runtime model change are allowed; broad Controller restructuring still requires explicit approval.

## Adding Effects, Transitions, and Blenders

- New visuals usually belong as C# classes under `Assets/effects/`, not as new scene object systems.
- Subclass `EffectBase` for direct 900-tile generative effects.
- Subclass `ScreenEffect` for 2D algorithms that need projection onto the Penrose layout.
- Subclass `MixerBase` for wrappers/mixers that own child effects.
- Subclass `TransitionBase` for effect-to-effect transitions.
- Subclass `BlenderBase` for external pixel-source blending.
- Concrete subclasses are discovered by reflection through `Factory<T>`. Adding/removing/renaming one changes the runtime catalog, keyboard/effect indexing, and deck behavior.
- New concrete effect/transition/blender classes should have parameterless constructors and do their setup in `Init()` / `OnStart()` as the existing classes do.
- Effects write to a `Color[]` buffer of `Penrose.Total == 900`; do not bypass the Penrose buffer model without approval.

## Unity and Asset Rules

- Do not hand-edit Unity-generated `.csproj`, `.sln`, or `.slnx` files. Regenerate them through Unity when needed.
- Preserve `Assets/**/*.meta` files and GUIDs when moving or renaming assets.
- Most project-authored runtime code currently lives in Unity's generated `Assembly-CSharp` assembly with no custom `.asmdef` files and mostly global namespace. Do not add assembly boundaries or namespaces as cleanup unless requested.
- Important runtime data is serialized in `Assets/Scenes/SampleScene.unity`, including `Controller.jsonSource` and `Controller.paletteSource`. Treat scene data changes as behavior changes.
- `Assets/TextMesh Pro/` resources and generated Unity settings may be Unity/import-owned. Do not revert, delete, or reorganize them without confirming ownership.

## Serial, Output, and Cross-Platform Rules

- Current target focus is Windows, with macOS development also important. The project should stay as cross-platform as practical, but serial output is a core requirement.
- Standalone API compatibility is intentionally `.NET Standard 2.1`; `System.IO.Ports` support is provided through platform-specific Unity plugin assets under `Assets/Plugins/System.IO.Ports/`.
- Do not switch Standalone back to `.NET Framework 4.8 + Unity additions` unless the desktop serial plugin path is proven unsuitable or the user explicitly approves the rollback.
- Ask before changing any hardware/control path:
  - `#define ENABLE_SERIAL`
  - `SerialOut.cs`
  - S2 Mini serial protocol behavior
  - E1.31/ACN UDP output
  - OSC ports/messages
  - `PixelReceiver`
  - drum or camera overlay behavior
  - telnet/debug command behavior
- The UDP/E1.31 path still exists, but serial is currently the active compiled output path.
- If Android/IL2CPP becomes a production target, do not assume the Standalone serial approach applies; that likely needs a platform-specific USB serial transport.

## Running and Validation

- Opening Play Mode can have side effects: it may scan/open serial ports, start UDP listeners, interact with attached hardware, and rewrite `StreamingAssets/images/*/files.txt` through `kscope`.
- Ask before running Play Mode when hardware state, local ports, or working-tree cleanliness matter.
- For script-level checks, use Unity-regenerated project files before trusting `dotnet build`; stale generated projects can report the wrong target framework after API compatibility changes.
- Prefer validation that matches the change:
  - visual tweaks: inspect in Unity/playback when safe;
  - core logic: add/run focused unit tests when practical;
  - serial/protocol changes: validate against the actual board/firmware contract;
  - scene/settings changes: verify serialized values and Unity-generated artifacts.

## Things Not To Do

- Do not replace the buffer/effect architecture with prefab-heavy Unity composition unless explicitly requested.
- Do not add large dependency-injection, event-bus, service-layer, or package architectures for one-off changes.
- Do not create manual registries for effects/transitions/blenders when `Factory<T>` already handles discovery.
- Do not preserve a compatibility path that leaves the active runtime using stale data. If the user asks for a new live source of truth, migrate the relevant consumers to it.
- Do not silently change hardware output modes or protocol details.
- Do not treat generated Unity files as stable hand-authored source.
