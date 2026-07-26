# AGENTS.md - PenroseArt

PenroseArt is a Unity-hosted C# creative/hardware runtime for the Penrose Wall LED installation. Unity hosts the scene, preview, input, and platform packaging; the core product is the plain-C# runtime that renders a 900-tile `Color[]` buffer and sends it to hardware.

Follow the existing creative-coding / installation-controller shape. Do not turn this into a conventional prefab-heavy Unity app or enterprise service architecture unless the user explicitly asks for that larger change.

Navigation/editing: Serena first — activate Serena project `penroseart` before repo navigation or edits.

## Session Startup (mandatory)

Load these skills via the Skill tool before any other work, every session: `unity`, `csharp`, `matt-engineering`. This is a hard gate, also injected by the SessionStart hook in `.claude/settings.json`. Do not navigate, edit, compile, or test until all three are loaded.

## Agent instruction files

`AGENTS.md` is the one physical root instruction file. `CLAUDE.md` and `GEMINI.md` are symlinks to it. Edit `AGENTS.md` directly.

## Agent skills

### Issue tracker

Issues and specs live in the repo's GitHub Issues (via the `gh` CLI). External PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Triage uses the default status strings: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repo: root `CONTEXT.md` plus root `docs/adr/`. See `docs/agents/domain.md`.

## Start Here

- `README.md` — current orientation and runtime loop.
- `CONTEXT.md` — canonical project glossary (pure vocabulary; architecture, platform, and output notes live in `docs/runtime-architecture.md`).
- `docs/runtime-architecture.md`, `docs/effect-authoring.md`, and `docs/code-map.md` — runtime shape, effect authoring, and file map.
- `Assets/core/Hardware/S2_MINI_PROTOCOL.md` — USB serial protocol for the S2 Mini / ESP32 boards.
- `docs/osc-client-contract.md` — the RaveSystem OSC wire contract (a synced copy owned by RaveSystem; never edited here). The single authority on what the wire actually provides — per-player clocks, transport predicates, loop state, timing grids, structure. Read it before any BeatManager, receiver, Director/Switcher, or player-data work instead of inferring wire facts from code or memory.
- `docs/investigation/` — historical research/audit notes; useful context, but not canonical current docs.

## Default Memory Reads

Before Unity validation, OSC/RaveSystem work, or build/test troubleshooting, read these Memory Vault entries in addition to normal startup context:

- `memory:penroseart-unity-test-runner-editor-open-options` — explains `scripts/unity-tests.sh`, the open-Editor `PenroseUnityTestBridge`, and why direct Unity batchmode fails when the project is already open.
- `memory:penroseart-unity-osc-workflow-improvements` — records the OSC compile/test scripts and Unity Test Framework command details.

## Development Philosophy

- Treat the core C# runtime as the product. Unity scene objects, UI, and assets wrap around these core files; they are not the primary architecture.
- **Behavioral claims in docs, comments, and memories are hypotheses to verify against code, tests, and logs — not facts to build on.** When documentation layers disagree, test the claim against the runtime instead of picking a layer to trust, then fix the losing document in the same session. Neither defend machinery nor delete it on a doc claim alone.
- Custom property drawers and inspectors are downstream debug views; runtime code must not be preserved just to keep them fed. They follow the runtime, not the reverse, and should be changed as needed.

## Simplicity and Hard Cuts

- Make **hard cuts**: when a pattern, name, signature, or structure changes, change it
  everywhere in one pass and delete the old form. **Best pattern, in the best place, once.**
- This is a personal installation with no installed base. Do not add backwards-compatibility
  shims, dual code paths, or parallel `*Legacy`/`*V2` types unless explicitly asked. Do not
  keep old implementations "just in case" — **git history is the safety net.**
- Treat "this is low-risk / minimal change / let's keep both for now" as a **smell** when it
  means preserving a second copy of anything. Prefer the decisive refactor that leaves
  exactly one canonical form.
- Start with the simplest design that serves the **first real production caller**; prefer
  direct composition of existing code. Tests and prototypes alone do not establish a need
  for new structure. Applies to tickets, specs, prototypes, and implementation.
- Prototype approval covers only interfaces exercised by realistic caller examples;
  unexercised surface remains unapproved.
- Any diagnostic code warning or above is to be treated as an error and must be either brought up to the user or fixed.

### Design preflight

Before designing or adding any new system, abstraction, or module, answer these four
questions and surface the answers before writing the code:

1. **Who is the first real caller?** Name the production caller. Tests and prototypes
   do not count.
2. **Which existing seams or modules were inspected, and why can't they be extended?**
   Name the files you actually read.
3. **Why is new structure needed?** State what the existing shape cannot express.
4. **What gets deleted in the same pass?** New structure that leaves a second way to do
   the same thing is an unfinished hard cut.

If you cannot answer all four, the change is not designed yet — do not start it.

## Core Files and Systems

Start with these before adding new structures:

- `Assets/core/Runtime/Controller.cs` — main runtime hub: initialization, timing, effect/transition selection, input, output, overlays, and mesh updates.
- `Assets/core/Runtime/Penrose.cs` — 900-tile Penrose model, JSON data, mesh generation, tile metadata, and buffer-to-mesh color mapping.
- `Assets/core/Effects/EffectBase.cs` — base contract for direct generative effects.
- `Assets/core/Effects/ScreenEffect.cs` — helper for effects that render into a rectangular buffer before mapping onto Penrose tiles.
- `Assets/core/Effects/MixerBase.cs` — base for effects that own child effects and combine or transform their buffers.
- `Assets/core/Transitions/TransitionBase.cs` — base for transitions between effects and some external-source blend behavior.
- `Assets/core/helpers/Factory.cs` — reflection-based discovery and instantiation of effect, transition, and blender classes.
- `Assets/core/helpers/GPalette.cs` and `Assets/core/Rhythm/BeatManager.cs` — shared color and rhythm systems. If a task changes the live beat source or beat data contract, refactoring `BeatManager.cs` and its direct effect/consumer call sites is in scope; adding or changing a data surface is not (see below).
- `Assets/core/Hardware/SerialOut.cs`, `Assets/OSCReader.cs`, `Assets/core/IO/PixelReceiver.cs`, and `Assets/core/ReactiveInputs/drums.cs` — hardware/control/input paths.

`Controller.cs` is intentionally central. Refactor it only with explicit approval because many hardware, scene, and runtime behaviors pass through it. Small wiring changes needed to connect an approved runtime model change are allowed; broad Controller restructuring still requires explicit approval.

`BeatManager.cs` is the single musical source: no other module re-derives musical facts. Adding or changing any BeatManager data surface requires asking Hunter first, with a proposal that states the musical fact, the consumer that needs it, and why. Do not add a surface speculatively, and do not compute a musical fact locally to avoid the ask.

## Adding Effects, Transitions, and Blenders

- New visuals usually belong as C# classes under `Assets/effects/`, not as new scene object systems.
- For new effects, copy `Assets/effects/EmptyEffect.cs`, rename the file and class, remove `[RuntimeCatalogIgnore]`, then implement the frame algorithm. See `docs/effect-authoring.md`.
- For new transitions, copy `Assets/transitions/EmptyTransition.cs`, rename the file and class, remove `[RuntimeCatalogIgnore]`, then implement the A-to-B blend and timing settings. See `docs/effect-authoring.md`.
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

- **Never launch Unity yourself** — not the GUI, not `Unity -batchmode`, not via Unity Hub.
  A second Unity instance fails when the project is already open in the Editor (which it
  usually is on this machine). The repo scripts handle this.
- To compile: `scripts/unity-compile.sh`. To test: `scripts/unity-tests.sh` (uses the
  open-Editor test bridge when Unity is running). Read diagnostics from the log paths
  the scripts print, not from stdout.
- **Delegated workers must never run the Unity scripts or launch Unity.** Read-only and
  workspace-write sandboxes block Unity Licensing Client state and local IPC outside
  the repo, causing a 75-second licensing failure and misleading package/compiler errors.
  Workers may run pure .NET checks and inspect Unity logs; the unsandboxed coordinator
  owns Unity compile/test validation. The scripts enforce this with a host-access probe,
  supervise the Unity process they start, and stop only their owned processes on failure.
- `dotnet build`, Roslyn, and IDE/LSP diagnostics are not Unity compilation. Only the
  scripts above validate a compile.
- **`.meta` files are Unity-generated, source-controlled identity files.** Never
  hand-create, copy, or hand-edit one. For a new asset, create only the asset and let
  Unity generate its `.meta` during import; then commit both. When moving, renaming,
  or deleting an existing asset, perform the same operation on its `.meta` so the GUID
  remains stable. Never omit an existing `.meta` from Git: losing or regenerating it
  changes the GUID and can break serialized references. Files outside Unity asset
  locations, such as repo scripts and documentation, do not need `.meta` files.
- Do not hand-edit Unity-generated `.csproj`, `.sln`, or `.slnx` files. Regenerate them through Unity when needed.
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
- Penrose/Rave application policy belongs in `Assets/OSC/Rave/`, `Assets/core/IO/RaveOscReceiver.cs`, `Assets/core/Rhythm/BeatManager.cs`, or other core consumers — not as special cases in the generic OSC files.
- Before changing `Assets/OSC/*.cs`, state whether the change is a generic OSC/library change, a Unity compatibility port change, or Penrose/Rave application policy. If unclear, stop and ask.
- Root-level `Assets/OSC.cs` and `Assets/OSCReader.cs` are project-specific/legacy integration files, not the vendored `Assets/OSC/` library boundary.
- For OSC work, read `docs/adr/0003-vendored-ravesystem-osc-boundary.md` before editing the vendored library or adapter layer.

## Documentation and Workflows

- ADR style: the domain-modeling skill's `ADR-FORMAT.md` is the single authority. Read that file before writing an ADR; never derive the format from past ADRs or from summaries of it.
- **Document what you touch:** any symbol you touch or create gets C# XML doc comments (symbol-scoped, not whole-file; no retroactive sweeps). See `docs/adr/0014-document-what-you-touch.md`.
- `docs/investigation/` is historical context, not canonical current documentation. Do not edit historical notes to make them look current; update canonical docs and link back when needed.

## Project-wide Boundaries

- Do not replace the buffer/effect architecture with prefab-heavy Unity composition unless explicitly requested.
- Do not add large dependency-injection, event-bus, service-layer, or package architectures for one-off changes.
- Do not preserve a compatibility path that leaves the active runtime using stale data. If the user asks for a new live source of truth, migrate the relevant consumers to it.
- Do not silently change hardware output modes, protocol details, OSC ports/messages, or control paths.
