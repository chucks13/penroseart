# AGENTS.md - PenroseArt

PenroseArt is a Unity-hosted C# creative/hardware runtime for the Penrose Wall LED installation. Unity hosts the scene, preview, input, and platform packaging; the core product is the plain-C# runtime that renders a 900-tile `Color[]` buffer and sends it to hardware.

Follow the existing creative-coding / installation-controller shape. Do not turn this into a conventional prefab-heavy Unity app or enterprise service architecture unless the user explicitly asks for that larger change.

Navigation/editing: Serena first — activate Serena project `penroseart` before repo navigation or edits.

## Agent instruction files

This repo no longer uses a DOX `AGENTS.md` hierarchy. Do not recreate child `AGENTS.md` files or a DOX checker/gate unless Hunter explicitly asks for DOX again.

`AGENTS.md` is the one physical root instruction file. `CLAUDE.md` and `GEMINI.md` are symlinks to it. Edit `AGENTS.md` directly.

## Agent skills

### Issue tracker

Issues and PRDs are tracked as local markdown under `.scratch/<feature-slug>/`. External PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Triage uses the default status strings: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repo: root `CONTEXT.md` plus root `docs/adr/`. See `docs/agents/domain.md`.

## Start Here

- `README.md` — current orientation and runtime loop.
- `CONTEXT.md` — canonical project vocabulary, platform/output notes, and architecture guide.
- `docs/runtime-architecture.md`, `docs/effect-authoring.md`, and `docs/code-map.md` — runtime shape, effect authoring, and file map.
- `Assets/core/S2_MINI_PROTOCOL.md` — USB serial protocol for the S2 Mini / ESP32 boards.
- `docs/investigation/` — historical research/audit notes; useful context, but not canonical current docs.

## Default Memory Reads

Before Unity validation, OSC/RaveSystem work, or build/test troubleshooting, read these Memory Vault entries in addition to normal startup context:

- `memory:penroseart-unity-test-runner-editor-open-options` — explains `scripts/unity-tests.sh`, the open-Editor `PenroseUnityTestBridge`, and why direct Unity batchmode fails when the project is already open.
- `memory:penroseart-unity-osc-workflow-improvements` — records the OSC compile/test scripts and Unity Test Framework command details.

## Development Philosophy

- Treat the core C# runtime as the product. Unity scene objects, UI, and assets wrap around these core files; they are not the primary architecture.
- When the user asks to replace a source of truth or data model, make the existing core system reflect the new model and update its real consumers. Do not import new runtime data into a side snapshot while leaving the application on the old model unless the user asks for a staged migration.

## Core Files and Systems

Start with these before adding new structures:

- `Assets/core/Controller.cs` — main runtime hub: initialization, timing, effect/transition selection, input, output, overlays, and mesh updates.
- `Assets/core/Penrose.cs` — 900-tile Penrose model, JSON data, mesh generation, tile metadata, and buffer-to-mesh color mapping.
- `Assets/core/EffectBase.cs` — base contract for direct generative effects.
- `Assets/core/ScreenEffect.cs` — helper for effects that render into a rectangular buffer before mapping onto Penrose tiles.
- `Assets/core/MixerBase.cs` — base for effects that own child effects and combine or transform their buffers.
- `Assets/core/TransitionBase.cs` — base for transitions between effects and some external-source blend behavior.
- `Assets/core/helpers/Factory.cs` — reflection-based discovery and instantiation of effect, transition, and blender classes.
- `Assets/core/helpers/GPalette.cs` and `Assets/core/BeatManager.cs` — shared color and rhythm systems. If a task changes the live beat source or beat data contract, refactoring `BeatManager.cs` and its direct effect/consumer call sites is in scope.
- `Assets/core/SerialOut.cs`, `Assets/OSCReader.cs`, `Assets/core/PixelReceiver.cs`, and `Assets/core/drums.cs` — hardware/control/input paths.

`Controller.cs` is intentionally central. Refactor it only with explicit approval because many hardware, scene, and runtime behaviors pass through it. Small wiring changes needed to connect an approved runtime model change are allowed; broad Controller restructuring still requires explicit approval.

## Adding Effects, Transitions, and Blenders

- New visuals usually belong as C# classes under `Assets/effects/`, not as new scene object systems.
- For new effects, copy `Assets/effects/EmptyEffect.cs`, rename the file and class, remove `[RuntimeCatalogIgnore]`, then implement the frame algorithm. See `docs/effect-authoring.md`.
- Subclass `EffectBase` for direct 900-tile generative effects.
- Subclass `ScreenEffect` for 2D algorithms that need projection onto the Penrose layout.
- Subclass `MixerBase` for wrappers/mixers that own child effects.
- Subclass `TransitionBase` for effect-to-effect transitions.
- Subclass `BlenderBase` for external pixel-source blending.
- Concrete subclasses are discovered by reflection through `Factory<T>`. Adding, removing, or renaming one changes the runtime catalog, keyboard/effect indexing, and deck behavior.
- Do not create manual registries for effects, transitions, or blenders when reflection discovery already handles the catalog.
- New concrete effect/transition/blender classes should have parameterless constructors and do their setup in `Init()` / `OnStart()` as existing classes do.
- Effects write to a `Color[]` buffer of `Penrose.Total == 900`; do not bypass the Penrose buffer model without approval.

## Unity and Asset Rules

- Do not hand-edit Unity-generated `.csproj`, `.sln`, or `.slnx` files. Regenerate them through Unity when needed.
- Most project-authored core/effect runtime code lives in Unity's generated `Assembly-CSharp` assembly and mostly global namespace. `Assets/OSC/` is the current exception with dedicated `.asmdef` files. Do not add new assembly boundaries or namespaces as cleanup unless requested.
- Important runtime data is serialized in `Assets/Scenes/SampleScene.unity`, including `Controller.jsonSource` and `Controller.paletteSource`. Treat scene data changes as behavior changes.
- `Assets/TextMesh Pro/` resources and generated Unity settings may be Unity/import-owned. Do not revert, delete, or reorganize them without confirming ownership.
- Do not treat Unity-generated files as stable hand-authored source.

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

## OSC Boundary

- `Assets/OSC/*.cs` is a Unity-compatible vendored copy of the generic `RaveSystem.Osc` library. Keep generic OSC behavior, wire format, dispatch, transport, and Unity compatibility concerns there.
- Penrose/Rave application policy belongs in `Assets/OSC/Rave/`, `Assets/core/RaveOscReceiver.cs`, `Assets/core/BeatManager.cs`, or other core consumers — not as special cases in the generic OSC files.
- Before changing `Assets/OSC/*.cs`, state whether the change is a generic OSC/library change, a Unity compatibility port change, or Penrose/Rave application policy. If unclear, stop and ask.
- Root-level `Assets/OSC.cs` and `Assets/OSCReader.cs` are project-specific/legacy integration files, not the vendored `Assets/OSC/` library boundary.
- For OSC work, read `docs/adr/0003-vendored-ravesystem-osc-boundary.md` before editing the vendored library or adapter layer.

## Documentation and Workflows

- Issues and PRDs live as markdown files under `.scratch/<feature-slug>/`; see `docs/agents/issue-tracker.md`.
- Triage state is recorded as a `Status:` line using the canonical strings in `docs/agents/triage-labels.md`.
- This is a single-context repo: use `CONTEXT.md` and `docs/adr/` for domain vocabulary and decisions; see `docs/agents/domain.md`.
- ADR style follows `memory:penroseart-adr-conventions`.
- `docs/investigation/` is historical context, not canonical current documentation. Do not edit historical notes to make them look current; update canonical docs and link back when needed.

## Running and Validation

- Opening Play Mode can have side effects: it may scan/open serial ports, start UDP listeners, interact with attached hardware, and rewrite `StreamingAssets/images/*/files.txt` through `kscope`.
- For script-level checks, use Unity-regenerated project files before trusting `dotnet build`; stale generated projects can report the wrong target framework after API compatibility changes.
- Use `scripts/unity-compile.sh` for Unity compile/import checks instead of hand-assembling raw Unity CLI invocations.
- Use `scripts/unity-tests.sh` for Unity Test Framework runs. Use `UNITY_TEST_FILTER` for focused tests and increase `UNITY_EDITOR_TEST_TIMEOUT` only after checking whether the Editor is busy, in Play Mode, compiling, or importing.
- Use `scripts/osc-compile.sh` for a fast `Assets/OSC` compile check and `scripts/osc-tests.sh` for OSC-focused EditMode tests.
- For changed scripts, run `bash -n scripts/<script>.sh` plus the narrowest wrapper that exercises the changed behavior.
- Prefer validation that matches the change:
  - visual tweaks: inspect in Unity/playback when safe;
  - core logic: add/run focused unit tests when practical;
  - serial/protocol changes: validate against the actual board/firmware contract;
  - scene/settings changes: verify serialized values and Unity-generated artifacts.

## Project-wide Boundaries

- Do not replace the buffer/effect architecture with prefab-heavy Unity composition unless explicitly requested.
- Do not add large dependency-injection, event-bus, service-layer, or package architectures for one-off changes.
- Do not preserve a compatibility path that leaves the active runtime using stale data. If the user asks for a new live source of truth, migrate the relevant consumers to it.
- Do not silently change hardware output modes, protocol details, OSC ports/messages, or control paths.
