# Code Context

## Files Retrieved

1. `readme.md` (lines 1-64) - concise lifecycle notes for effects, animation deck, beat variants, Nova override, palette system, `ScreenEffect`, mixers, wrappers.
2. `CONTEXT.md` (lines 1-48) - project overview and intended architecture, including controller, beat, buffer, palette, I/O, and build-symbol notes.
3. `Assets/core/helpers/CONTEXT.md` (lines 1-17) - helper/hardware performance notes for serial output and S2 Mini controllers.
4. `ProjectSettings/ProjectVersion.txt` (lines 1-2) - Unity editor version: `6000.4.7f1`.
5. `Packages/manifest.json` (lines 1-48) - Unity package dependencies, including Test Framework, UGUI, TextMesh Pro-related modules, Timeline, navigation, IDE integrations.
6. `ProjectSettings/EditorBuildSettings.asset` (lines 1-11) - enabled build scene: `Assets/Scenes/SampleScene.unity`.
7. `ProjectSettings/ProjectSettings.asset` (lines 15-16, 166-172, 258-260, 614-626) - product name, Android identifiers/settings, template scene, and empty scripting define symbols.
8. `Assembly-CSharp.csproj` (lines 1-48) - generated Unity C# project: `netstandard2.1`, C# 9, Unity build target `StandaloneOSX`, generated analyzers.
9. `penroseart.slnx` (lines 1-3) - generated solution includes only `Assembly-CSharp.csproj`.
10. `.vscode/settings.json` (lines 1-55) - hides Unity/generated assets in VS Code and points `dotnet.defaultSolution` at `penroseart.slnx`.
11. `.vsconfig` (lines 1-6) - Visual Studio managed game workload.
12. `.gitignore` (lines 1-21) - ignores Unity generated folders and generated C# project/solution files; keeps `Assets/**/*.meta`.
13. `Assets/Scenes/SampleScene.unity` (lines 1077-1102, 1998-2023, 2063-2092, 2767-2794) - scene entry objects and serialized `Controller`/`Penrose` configuration.
14. `Assets/core/Controller.cs.meta` (lines 1-8) - `Controller` script GUID and execution order `-2000`.
15. `Assets/core/Penrose.cs.meta` (lines 1-8) - `Penrose` script GUID and default execution order.
16. `Assets/core/Controller.cs` (lines 1-183, 187-280, 330-382, 386-456, 662-714, 717-766, 805-959) - main singleton/controller lifecycle, effect setup, output, OSC, transition, and update loop.
17. `Assets/core/EffectBase.cs` (lines 5-81) - base class/contract for effects.
18. `Assets/core/MixerBase.cs` (lines 3-17) - mixer/wrapper base class and child-effect selection constraint.
19. `Assets/core/TransitionBase.cs` (lines 6-95) - transition/blender contract with `A`, `B`, `V`, `D` and buffer.
20. `Assets/core/ScreenEffect.cs` (lines 4-120) - 2D screen-buffer to Penrose tile-buffer mapping helper.
21. `Assets/core/Penrose.cs` (lines 23-69, 74-123, 178-268, 270-365, 367-435) - JSON data schema, mesh/tile/bounds/ring generation, vertex color updates, tile data.
22. `Assets/core/helpers/Factory.cs` (lines 5-29) - reflection factory used to discover effects, transitions, and blenders.
23. `Assets/core/helpers/GPalette.cs` (lines 15-180, 211-310) - `GPalette` and `AnimPalette` color parsing/animation.
24. `Assets/core/BeatManager.cs` (lines 5-146) - simulated beat clock and beat brightness/time helpers.
25. `Assets/core/helpers/ExtensionMethods.cs` (lines 3-127) - math/color/buffer extension helpers such as `Map`, `Clear`, `MinBrightness`.
26. `Assets/core/helpers/BlenderBase.cs` (lines 5-18) - external-source blender contract.
27. `Assets/core/helpers/Timer.cs` (lines 3-29) - simple countdown timer used for effect/transition switching.
28. `Assets/core/helpers/Singleton.cs` (lines 3-25) - Unity `MonoBehaviour` singleton used by `Controller`.
29. `Assets/core/Perlin.cs` (lines 13-80) - Perlin noise utility used by effects.
30. `Assets/effects/Noise.cs` (lines 4-75) - representative generative `EffectBase` effect with beat/palette integration.
31. `Assets/effects/Julia.cs` (lines 5-126) - representative `ScreenEffect` subclass using 2D fractal buffer conversion.
32. `Assets/effects/Mirror.cs` (lines 5-108) - representative wrapper/mixer effect using `mirror2`/`mirror10` shape lists.
33. `Assets/effects/RandomEffectsMixer.cs` (lines 3-56) - representative multi-child mixer.
34. `Assets/effects/kscope.cs` (lines 1-145, 180-245) - image/StreamingAssets-backed screen effect.
35. `Assets/transitions/Fade.cs` (lines 2-39) - representative transition and blend implementation.
36. `Assets/transitions/RGBFade.cs` (lines 3-49) - representative RGB-channel transition/blender.
37. `Assets/blenders/RGBBlender.cs` (lines 6-26) - external-source RGB channel blender.
38. `Assets/blenders/SilhouetteBlender.cs` (lines 6-22) - external-source silhouette blender.
39. `Assets/core/PixelReceiver.cs` (lines 5-65) - UDP pixel stream receiver on port 7778.
40. `Assets/core/UDPControllers.cs` (lines 15-90) - background UDP receive helper.
41. `Assets/OSCReader.cs` (lines 197-378) - OSC listener/sender defaults and threaded read loop.
42. `Assets/core/SerialOut.cs` (lines 10-327) - S2 Mini serial discovery, handshake, threaded output, and debug info.
43. `Assets/core/S2_MINI_PROTOCOL.md` (lines 1-63) - firmware protocol for USB-serial LED driver boards.
44. `Assets/core/CameraReader.cs` (lines 1-352) - optional webcam overlay pipeline and OSC page 2 controls.
45. `Assets/core/drums.cs` (lines 1-198) - drum/ring visual overlay and UDP/OSC controls.
46. `Assets/core/helpers/TelnetServer.cs` (lines 11-170, 243-463) - conditional telnet command interface.
47. `Assets/core/Controller - nova.cs` (lines 1-120) - disabled `#if false` reference implementation.
48. `Assets/StreamingAssets/rawdata.json` (line 1; parsed as JSON) - current one-line Penrose data file with 900 tiles, 10,800 mesh floats, 1,800 wires, and shape lists.
49. `Assets/StreamingAssets/rawdata1.json` (lines 1-5; parsed as JSON) - alternate formatted Penrose data with same counts as `rawdata.json`.
50. `Assets/StreamingAssets/palettedata.txt` (lines 1-8), `Assets/StreamingAssets/jenpalettes.txt` (lines 1-12), `Assets/StreamingAssets/filelist.txt` (lines 1-6) - palette source data/files.
51. `Assets/core/PenroseShader.shader` (lines 1-8) - custom shader name used by `Penrose` material: `Unlit/Penrose`.

## Key Code

### Identity

- This is a Unity/C# real-time controller/simulator for the Penrose Wall light installation.
- Root docs state it generates generative visuals and outputs to LED hardware over ACN/E1.31 (`CONTEXT.md` lines 3-4), while current source has serial output enabled by a file-local define (`Assets/core/Controller.cs` line 2) and helper docs describe ESP32-S2 USB-serial output (`Assets/core/helpers/CONTEXT.md` lines 5-15).
- Product metadata: company `Hunter`, product `penrose_simulator` (`ProjectSettings/ProjectSettings.asset` lines 15-16). Android package is `com.hunter.penrosesimulator` (`ProjectSettings/ProjectSettings.asset` lines 166-172).

### Main entry points

- Unity scene: `Assets/Scenes/SampleScene.unity` is the only enabled build scene (`ProjectSettings/EditorBuildSettings.asset` lines 7-10).
- Scene objects:
  - `Controller` GameObject uses script GUID `5f47cb3b2738dba41858c13614a36b80` (`Assets/Scenes/SampleScene.unity` lines 1077-1102); `Controller.cs.meta` sets execution order `-2000` (lines 1-8).
  - `PenroseDisplay` GameObject uses `Penrose` script GUID `f95c9fce4577c274998e22a24e80df9a` (`Assets/Scenes/SampleScene.unity` lines 2767-2794).
- Serialized controller settings in scene include `IP: 192.168.1.253`, `brightness: 64`, `useCamera: 0`, `effectTime: 10`, `currentTransition: 4`, `transitionTime: 4` (`Assets/Scenes/SampleScene.unity` lines 1998-2023), plus large embedded `paletteSource` and one-line embedded `jsonSource` (`Assets/Scenes/SampleScene.unity` lines 2063-2092).

### Core contracts

```csharp
// Assets/core/EffectBase.cs lines 5-66
public abstract class EffectBase {
  public Color[] buffer;
  public Controller controller;
  protected Penrose penrose;
  protected Penrose.TileData[] tiles;
  public static AnimPalette APalette=new AnimPalette();
  public BeatData beat => controller.beatManager.beatData;
  public bool beatEnable = true;
  public int beatVariant;
  public abstract string DebugText();
  public virtual void Init() { /* binds Controller.Instance, Penrose, Tiles, 900-color buffer */ }
  public virtual void OnStart() { beatEnable = true; beatVariant = beatManager.GetRandomVariant(); }
  public abstract void OnEnd();
  public abstract void Draw();
}
```

```csharp
// Assets/core/TransitionBase.cs lines 6-95
public abstract class TransitionBase {
  public Color[] buffer;
  public float[] settings;
  public int A { get; set; }
  public int B { get; set; }
  public float V { get; set; }   // clamped progress 0..1
  public float D => 1f - v;
  public virtual void Blend(Color[] dest, Color[] src1, Color[] src2) {}
  public abstract void OnStart();
  public abstract void OnEnd();
  public abstract void Draw();
}
```

```csharp
// Assets/core/ScreenEffect.cs lines 85-96, 98-120
public static void ConvertScreenBuffer(ref Color[] screenBuffer, in Color[] buffer) { ... }
public override void Init() {
  base.Init();
  if(width < 0) {
    width = (int)penrose.Bounds.size.x.Round();
    height = (int)penrose.Bounds.size.y.Round();
  }
  screenBuffer = new Color[width * height];
  if(neighbors != null) return;
  neighbors = new ScreenMap[buffer.Length][];
  InitWeights();
}
```

### Controller lifecycle and data flow

- `Controller.Start()` sets 60 FPS, finds and initializes `Penrose`, binds GUI inputs, discovers effects/transitions/blenders via reflection factories, initializes UDP/OSC/drums/pixel receiver/camera, and initializes serial output if `ENABLE_SERIAL` is active (`Assets/core/Controller.cs` lines 662-714).
- Effects are discovered by `Factory<EffectBase>` and each is instantiated once and initialized (`Assets/core/Controller.cs` lines 161-183). `Factory<T>` selects non-abstract subclasses of `T` from the assembly (`Assets/core/helpers/Factory.cs` lines 19-27).
- Selection uses a deck: `pullCard()` picks from the top half then moves the selected index to the bottom (`Assets/core/Controller.cs` lines 143-158). Nova override can force an effect by name substring (`Assets/core/Controller.cs` lines 717-730).
- Transitions run as a state toggle in `OnTimerFinished()`: playing effect -> transition with `A=currentEffect`, `B=GetNewEffectIndex()`, palette change, then transition -> new current effect (`Assets/core/Controller.cs` lines 733-766).
- Per-frame `Update()` updates timer, palette, beat manager, active effect/transition, optional filter, drum overlay, optional camera overlay, optional pixel-source blending, hardware output, mesh colors, and OSC ping (`Assets/core/Controller.cs` lines 805-959).

### Penrose model and buffers

- Logical visual state is a `Color[]` of `Penrose.Total == 900` tiles (`Assets/core/Penrose.cs` lines 76-92).
- JSON data schema includes `Mesh`, `tiles`, `wires`, and `shapes` with lists such as `loops`, `stars`, `lines0..4`, `lotusballs`, `starballs`, `mirror2`, `mirror10` (`Assets/core/Penrose.cs` lines 23-61).
- `Penrose.Awake()` currently parses JSON from `Controller.Instance.jsonSource`, not directly from `StreamingAssets/rawdata.json` (`Assets/core/Penrose.cs` lines 114-123).
- `GenerateMesh()` builds a 6-vertex-per-tile mesh from `JsonRawData.Mesh`, applies scale/gap/y-flip, and assigns the material (`Assets/core/Penrose.cs` lines 217-268).
- `GenerateTiles()` derives `TileData` center, integer position, section, type, neighbor data, radius, angle, and tile angle (`Assets/core/Penrose.cs` lines 270-314).
- `UpdateModelColors()` colors each tile’s six mesh vertices from `penrose.buffer` (`Assets/core/Penrose.cs` lines 367-383).

### Output paths

- Current compiled path: `#define ENABLE_SERIAL` at `Assets/core/Controller.cs` line 2 means `Update()` calls `sendSerialFrame()` instead of `sendUDPFrame()` (`Assets/core/Controller.cs` lines 947-951).
- Serial output maps 900 animation tiles to 1,800 physical LEDs using `penrose.JsonRawData.wires[i] / 2`, then calls `SerialOut.send()` (`Assets/core/Controller.cs` lines 365-382).
- `SerialOut` discovers serial ports, performs S2 Mini query handshake `?`/`0x3F`, reads board start/count, starts one I/O thread per board, packs `D`/`0x44` data plus latch `L`/`0x4C`, and supports debug text (`Assets/core/SerialOut.cs` lines 53-327).
- If serial is disabled, `sendUDPFrame()` packs RGB-ish data into E1.31/ACN universes. Note channel order in code is `r`, `b`, `g` (`Assets/core/Controller.cs` lines 330-362), and UDP destination is scene `IP` on port 5568 (`Assets/core/Controller.cs` lines 57-63, 237-247).
- Optional PREP_CAPTURE feedback sends chunked pixel data to localhost:7777 (`Assets/core/Controller.cs` lines 283-329), but `PREP_CAPTURE` is not currently defined in ProjectSettings.

### Input/overlay systems

- `OSCReader` defaults: listens on port `6969`, sends to `192.168.1.255:6161`, and dispatches messages on Unity `Update()` from a background read thread (`Assets/OSCReader.cs` lines 197-378).
- `Controller.OSCpage1()` handles brightness `/1/vscroll1`, NYE toggle `/1/nav1`, effect buttons `/1/push#`, period `/1/hscroll1`, and `/ping` replies (`Assets/core/Controller.cs` lines 431-456 plus continuation in same method).
- `PixelReceiver` listens on UDP port 7778 for a 6-byte-header RGB pixel stream and keeps source active for 100 frames (`Assets/core/PixelReceiver.cs` lines 11-65). `Controller.Update()` can blend or replace the native effect buffer with that source (`Assets/core/Controller.cs` lines 914-945).
- `drums` listens on UDP port 8500, draws five hit/ring overlays over the tile buffer, and supports OSC page 3/test keys (`Assets/core/drums.cs` lines 1-198; key tests in `Controller.Update()` lines 843-854).
- `CameraReader` is optional (`useCamera` serialized false) and maps webcam samples through `ScreenEffect.ConvertScreenBuffer()` before mixing into the effect buffer (`Assets/core/CameraReader.cs` lines 1-352).
- `TelnetServer` is fully behind `#if ENABLE_TELNET`; commands include `help`, `echo`, `list effects`, `list blenders`, `effect <name> [time]`, `blender <name> ...`, `nye on|off`, and PREP_CAPTURE-only `dummy` (`Assets/core/helpers/TelnetServer.cs` lines 11-170, 243-463).

### Effects/transitions/blenders discovered

`Factory<T>` discovers these at runtime from the assembly; there are no `.asmdef` files.

- Effects in `Assets/effects/`: `Angles`, `AnimateLoops`, `ColorSparkle`, `Flock`, `Julia`, `MetaBalls`, `Mirror`, `Nibbler`, `Noise`, `NoiseMixer`, `NoiseTunnel`, `Panels`, `Petals`, `Pulse`, `RainbowBars`, `RandomEffectsMixer`, `Ripple`, `ShapeGlitch`, `TileShapes`, `Tunnel`, `Vortex`, `Waterfall`, `fluid`, `kscope`, `lightning`, `yinyangmixer`.
- Transitions in `Assets/transitions/`: `DirectionalWipe`, `Fade`, `FizzleTransition`, `IndexWipe`, `IrisTransition`, `NoiseTransition`, `RGBFade`.
- External blenders in `Assets/blenders/`: `RGBBlender`, `SilhouetteBlender`.

Examples:

- `Noise : EffectBase` chooses random scale/speed/amplifier/palette offset/distortion mode and uses `BeatManager` for brightness, hue shift, or time warp (`Assets/effects/Noise.cs` lines 4-75).
- `Julia : ScreenEffect` draws a 2D Julia fractal into `screenBuffer`, then calls `ConvertScreenBuffer()` to fill the 900-tile buffer (`Assets/effects/Julia.cs` lines 5-126).
- `Mirror : MixerBase` wraps one generated child effect, chooses `mirror2` or `mirror10` shape data, patches an 8-tile centerline, and copies sampled colors across mirror groups (`Assets/effects/Mirror.cs` lines 5-108).
- `Fade : TransitionBase` draws effect A and B, then blends their buffers by `V/D`; its `Blend()` method also doubles as an external-source blend (`Assets/transitions/Fade.cs` lines 2-39).

## Architecture

### Directory layout

- Root Unity project: `Assets/`, `Packages/`, `ProjectSettings/`, generated `Library/`, `Temp/`, `obj/`, solution/project files.
- `Assets/core/`: runtime core: `Controller`, `Penrose`, effect/transition base classes, beat, serial/UDP/OSC/camera/drum support, shader, protocol docs.
- `Assets/core/helpers/`: reflection factory, palette, singleton, timer, telnet, blender base, extension methods, slider script.
- `Assets/effects/`: generative visual effects and mixer/wrapper effects.
- `Assets/transitions/`: transition effects between two `EffectBase` buffers.
- `Assets/blenders/`: blenders for native effect buffer plus external pixel source.
- `Assets/StreamingAssets/`: Penrose geometry/mapping JSON files, palette text files, image assets/file lists used by `kscope`.
- `Assets/Scenes/`: only `SampleScene.unity` is enabled for builds.
- `Assets/TextMesh Pro/`: imported TextMesh Pro resources/examples; compiled into generated project, but not central project logic.
- `Assets/palettes/`: currently empty.

### Runtime data flow

1. Unity loads `SampleScene`; `Controller` has early execution order and `PenroseDisplay` has the `Penrose` mesh component.
2. `Penrose.Awake()` reads embedded JSON from `Controller.Instance.jsonSource`; `Controller.Start()` calls `penrose.Init()` to generate mesh, tile metadata, bounds, and rings.
3. `Controller.SetupEffects()`, `SetupTransitions()`, and `SetupBlenders()` use reflection to instantiate all non-abstract subclasses.
4. Each frame, active `EffectBase.Draw()` or active `TransitionBase.Draw()` writes a 900-color buffer.
5. Optional overlays/mixes run: filter, drums, camera, and external pixel receiver/blender.
6. Hardware output is currently serial: logical 900-tile buffer expands to 1,800 physical LEDs through `wires[i] / 2`; if serial is disabled, E1.31/ACN UDP universes are sent instead.
7. `Penrose.UpdateModelColors()` mirrors the output buffer in the Unity mesh preview.

### Conventions and extension points

- No C# namespaces; all project classes are global.
- Most class names match filenames and use PascalCase, but several are lowercase (`fluid`, `kscope`, `lightning`, `yinyangmixer`, `drums`).
- New generated effects should subclass `EffectBase` or `ScreenEffect`, call `base.Init()`/`base.OnStart()`, maintain their own `buffer`, implement `DebugText()`, `OnEnd()`, and `Draw()`.
- New mixers/wrappers should subclass `MixerBase`; `MixerBase.GetRandomEffect()` avoids selecting other `MixerBase` subclasses for child effects (`Assets/core/MixerBase.cs` lines 3-17).
- New transitions subclass `TransitionBase`; external source blenders can subclass `BlenderBase` or use `TransitionBase.Blend()`.
- Effects generally use `EffectBase.APalette.read(normalizedPosition, interpolate)` for shared color harmony and `beatManager.GetBeatBrightness()` / `GetBeatTime()` for rhythm.
- Common helper methods: `Color[] Clear()`, `Fade()`, `MinBrightness()`, `Map()`, `Map01()`, `Perlin.Noise()`.

## Build / Test / Run Workflows

- **Unity editor:** Open the project with Unity `6000.4.7f1` (`ProjectSettings/ProjectVersion.txt` lines 1-2). Run/play `Assets/Scenes/SampleScene.unity`; it is the only enabled build scene.
- **C# compile check:** `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` completed successfully in this environment and emitted `Assembly-CSharp.dll` under `Temp/bin/Debug/`. It reported 17 warnings, including:
  - `fluid.beatVariant` hides `EffectBase.beatVariant`.
  - `sendACN(...)` calls in `Controller.sendUDPFrame()` are not awaited.
  - `TileShapes.cs(84)` self-assignment.
  - obsolete Unity APIs (`UnityWebRequest.isNetworkError/isHttpError`, `FindObjectOfType<T>()`).
  - unassigned `SliderScript` fields.
- **Tests:** `com.unity.test-framework` and `com.unity.ext.nunit` are dependencies, but no `Assets/**/Tests/**`, `*Test*.cs`, `[Test]`, `[UnityTest]`, or NUnit usage was found.
- **Build targets:** Generated `Assembly-CSharp.csproj` says Unity build target `StandaloneOSX` (`Assembly-CSharp.csproj` lines 37-43). Player settings also include Android identifiers and target architecture fields (`ProjectSettings/ProjectSettings.asset` lines 166-172, 258-260), and there is an `android_wall.aab` artifact at repo root.
- **Compile symbols:** `ProjectSettings/ProjectSettings.asset` has `scriptingDefineSymbols: {}` (line 614). Active serial output is instead file-local `#define ENABLE_SERIAL` in `Controller.cs`. `ENABLE_TELNET` and `PREP_CAPTURE` code is present but inactive unless defines are added; root docs mention `ENABLE_BLENDING`, but no active `#if ENABLE_BLENDING` usage was found.

## Unknowns / Unity-specific follow-up

- **Deployment output path:** Docs emphasize ACN/E1.31 UDP, but current source compiles serial output by default. Confirm whether production should use USB-serial S2 Mini boards or E1.31 UDP.
- **Firmware location:** `Assets/core/helpers/CONTEXT.md` references `main.cpp`, but no `main.cpp` exists under `Assets/`. Firmware may live in another repo or is missing.
- **Embedded vs StreamingAssets JSON:** `Penrose.Awake()` uses the scene-serialized `jsonSource`, while `Assets/StreamingAssets/rawdata.json` and `rawdata1.json` also exist. Confirm source of truth and update workflow for Penrose geometry/mapping.
- **Reflection order dependency:** Effects/transitions are discovered by `Assembly.GetTypes()` order. Scene serializes `currentTransition: 4`; adding/removing classes may shift indices. Confirm whether this is acceptable.
- **`startEffect` field:** Serialized in scene, but `SetupEffects()` currently ignores it and always uses `GetNewEffectIndex()`.
- **Unity asset hygiene:** `kscope` writes `StreamingAssets/images/*/files.txt` at runtime on non-Android. Confirm whether this side effect is intended in editor/play mode.
- **Thread/runtime behavior:** UDP and serial helper threads catch broad exceptions in places; hardware validation is needed to know expected failure/degradation behavior.
- **TextMesh Pro import state:** Many TMP resource/example files are present and show as modified/untracked in git status; likely imported/generated Unity assets, but ownership was not investigated.

## Start Here

Open `Assets/core/Controller.cs` first. It owns initialization, effect/transition selection, overlays, input, and hardware output. Then open `Assets/core/Penrose.cs` to understand the 900-tile model and physical `wires` mapping, followed by `Assets/core/EffectBase.cs`, `Assets/core/ScreenEffect.cs`, and one concrete effect such as `Assets/effects/Noise.cs` or `Assets/effects/Julia.cs`.
