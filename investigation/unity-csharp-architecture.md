# Penroseart Unity/C# architecture brief

## Scope and method

- Project: `/Users/hunter/Projects/penroseart`.
- Unity version: `6000.4.7f1` (`ProjectSettings/ProjectVersion.txt:1-2`).
- Built scene: `Assets/Scenes/SampleScene.unity` (`ProjectSettings/EditorBuildSettings.asset:8-9`).
- This investigation focused on custom scripts under `Assets/core`, `Assets/effects`, `Assets/transitions`, and `Assets/blenders`, plus Unity scene/YAML/config/package assets.
- **Tooling limitation:** the runtime did not expose Serena/LSP tools to this subagent. I used targeted text search and selective reads for C# symbols, and text/YAML search for Unity assets/config.
- No Memory Vault writes were made. No Unity editor/build/test run was performed, to avoid generated files and runtime side effects.

## Executive summary

This is a Unity Built-in Render Pipeline project for a 900-tile Penrose LED wall simulator/controller. Runtime visuals are generated into 1D `Color[]` buffers of length `Penrose.Total == 900`; the display mesh represents each tile as two triangles / six vertices, while hardware output maps the 900 tile colors onto 1800 physical LED positions via `JsonRawData.wires`.

The central runtime object is `Controller : Singleton<Controller>` (`Assets/core/Controller.cs:23`). `Controller.Start()` finds the scene `Penrose`, initializes geometry, creates all top-level effects/transitions/blenders by reflection, wires UI/OSC/drums/pixel input, starts the animation timer, and enables serial output (`Assets/core/Controller.cs:660-710`). `Controller.Update()` advances timers and palette/beat state, draws either the active effect or active transition, overlays drums/camera/pixel input, outputs serial or UDP data, and updates the Penrose mesh vertex colors (`Assets/core/Controller.cs:806-954`).

Important current-state findings:

- `Controller.cs` has a file-local `#define ENABLE_SERIAL` at line 2, so the active output path is serial (`sendSerialFrame`) rather than E1.31/ACN UDP, despite docs emphasizing ACN. `ENABLE_TELNET` and `PREP_CAPTURE` are not project scripting defines (`ProjectSettings/ProjectSettings.asset:614`) and are not file-defined in active code.
- `SampleScene` has only three custom MonoBehaviour bindings: `Controller`, `SliderScript`, and `Penrose`; most effects/transitions are not scene components and are instantiated by `Factory<T>` at runtime.
- Scene data includes a huge `Controller.jsonSource` blob at `Assets/Scenes/SampleScene.unity:2092`; local parsing confirmed it exactly matches `Assets/StreamingAssets/rawdata.json` and contains 900 tiles, 1800 wires, and 10800 mesh floats. `Penrose.Awake()` reads from `Controller.Instance.jsonSource`, not directly from StreamingAssets (`Assets/core/Penrose.cs:122-123`).
- The effect model is mostly non-MonoBehaviour plain C# classes. Additions/removals are discovered automatically by reflection in `Factory<T>` (`Assets/core/helpers/Factory.cs:19-27`), so adding a non-abstract `EffectBase`, `TransitionBase`, or `BlenderBase` subclass changes runtime catalogs and possibly keyboard/deck order.
- Several docs are directionally useful but stale/incomplete: top-level effects are singleton-like, but mixers create transient child effects; active code serializes output by USB serial, while `CONTEXT.md` describes ACN; beat variant comments/docs disagree with switch cases in code.

## Project and asset organization

### Root and Unity-generated files

- Root contains Unity-generated solution/project files (`Assembly-CSharp.csproj`, many package `.csproj`, `penroseart.slnx`) but `.gitignore` ignores `*.csproj`, `*.sln`, `Library/`, `Temp/`, `Obj/`, `Logs/`, and `UserSettings/`.
- Unity text serialization is enabled (`ProjectSettings/EditorSettings.asset:7-9`: Visible Meta Files, serialization mode 2). Preserve `.meta` files and GUIDs when moving/renaming assets.
- No custom `.asmdef` files were found. Custom code and TextMesh Pro example scripts compile into the default assembly.

### Key asset folders

- `Assets/core/` — runtime hub and infrastructure:
  - `Controller.cs`, `Penrose.cs`, `EffectBase.cs`, `ScreenEffect.cs`, `MixerBase.cs`, `TransitionBase.cs`.
  - I/O helpers: `SerialOut.cs`, `PixelReceiver.cs`, `UDPControllers.cs`, `OSCReader.cs`/`OSC.cs`, `CameraReader.cs`, `drums.cs`.
  - Utility helpers: `Factory.cs`, `Timer.cs`, `GPalette.cs`, `ExtensionMethods.cs`, `Singleton.cs`, `TelnetServer.cs`.
- `Assets/effects/` — generative effects, screen effects, mixers/wrappers.
- `Assets/transitions/` — transition/blend implementations.
- `Assets/blenders/` — external pixel-source blenders.
- `Assets/StreamingAssets/` — raw geometry/palette/image data. Notably:
  - `rawdata.json`, `rawdata1.json`.
  - `palettedata.txt`, `jenpalettes.txt`, `filelist.txt`.
  - `images/color/*.png`, `images/mono/*.png` for `kscope`.
- `Assets/TextMesh Pro/` — TextMesh Pro resources and examples. Examples are present and compile, but are not part of the custom effect system.

## Scene organization: `Assets/Scenes/SampleScene.unity`

### Build scene

`SampleScene.unity` is the only enabled build scene (`ProjectSettings/EditorBuildSettings.asset:8-9`).

### Custom script bindings

Local scene parsing found these custom MonoBehaviours:

| Scene object | Scene evidence | Script evidence | Notes |
|---|---:|---:|---|
| `Controller` | `Assets/Scenes/SampleScene.unity:1082`, script GUID at `:1097` | `Assets/core/Controller.cs.meta:2`, `Assets/core/Controller.cs:23` | Main runtime hub. Meta execution order is `-2000` (`Assets/core/Controller.cs.meta:7`), which matters because `Penrose.Awake()` reads `Controller.Instance.jsonSource`. |
| `Slider` | `Assets/Scenes/SampleScene.unity:2144`, script GUID at `:2181` | `Assets/core/helpers/SliderScript.cs.meta:2`, `Assets/core/helpers/SliderScript.cs:7` | UI brightness slider writes `controller.brightness` (`SliderScript.cs:13-18`). Scene slider range is 0-255 (`SampleScene.unity:2230-2233`). |
| `PenroseDisplay` | `Assets/Scenes/SampleScene.unity:2774`, script GUID at `:2789` | `Assets/core/Penrose.cs.meta:2`, `Assets/core/Penrose.cs:76` | Mesh display object with `Penrose`; scene serializes `bgColor`, `scale`, `gapScale` (`SampleScene.unity:2792-2794`). |

Other scene objects are Unity/TMP/UI primitives: `Main Camera`, `GUI Canvas`, `EventSystem`, `DebugText`, `EffectText`, `destIP`, `ontime`, `offtime`, display toggle, slider children, and labels.

### Important serialized controller fields

Evidence from `Assets/Scenes/SampleScene.unity`:

- `IP: 192.168.1.253` (`:2014`), `brightness: 64` (`:2015`), `displayOn: 1` (`:2013`).
- `useCamera: 0` (`:2018`).
- `effectTime: 10` (`:2020`), `currentTransition: 4` (`:2022`), `transitionTime: 4` (`:2023`).
- `paletteSource` is a multiline FastLED-style palette string (`:2064-2091`).
- `jsonSource` is a scene-serialized JSON wall map (`:2092`), locally verified equal to `Assets/StreamingAssets/rawdata.json`.
- `effectText`, `debugText`, `myIPText` references are serialized (`:2114-2116`).
- The serialized `penrose` field is `{fileID: 0}` (`:2117`); `Controller.Start()` uses `GameObject.FindObjectOfType<Penrose>()` instead (`Assets/core/Controller.cs:665`).
- `PenroseDisplay` serializes an empty `JsonRawData` object in the scene (`SampleScene.unity:3396-3401`), but `Penrose.Awake()` overwrites from `Controller.Instance.jsonSource` (`Penrose.cs:122-123`).

## Core runtime lifecycle

### Singleton and startup ordering

- `Controller` inherits `Singleton<Controller>` (`Assets/core/Controller.cs:23`; `Assets/core/helpers/Singleton.cs:3`).
- `Singleton<T>.Awake()` stores `_instance` and calls `DontDestroyOnLoad` (`Assets/core/helpers/Singleton.cs:19-21`).
- `Controller.cs.meta` sets script execution order to `-2000` (`Assets/core/Controller.cs.meta:7`), likely to ensure `Controller.Instance` exists before `Penrose.Awake()` reads controller JSON.
- `Singleton<T>.Instance` creates a new GameObject if no instance exists (`Assets/core/helpers/Singleton.cs:7-15`). Avoid calling `Controller.Instance` from edit-time/static contexts unless a scene Controller is definitely alive, because it can create an unintended runtime object.

### `Penrose` initialization

`Penrose.Awake()`:

- Requires `MeshFilter` and `MeshRenderer` (`Assets/core/Penrose.cs:74-75`).
- Gets those components and creates a material using shader `Unlit/Penrose` (`Penrose.cs:114-121`, shader at `Assets/core/PenroseShader.shader:1`).
- Reads `JsonRawData` from `Controller.Instance.jsonSource` (`Penrose.cs:122-123`).

`Controller.Start()` then calls `penrose.Init()` (`Controller.cs:665-666`). `Penrose.Init()` generates mesh, tiles, bounds, rings, and background brightness (`Penrose.cs:358-364`).

`Penrose` data model:

- `JsonData` contains `Mesh`, `tiles`, `wires`, and `shapes` (`Penrose.cs:24`, `:58-61`).
- `Penrose.Total` is 900 (`Penrose.cs:77`).
- `buffer` is a 900-color input buffer (`Penrose.cs:88`).
- Mesh arrays are sized as `Total * 2 * 3` vertices/triangles/colors (`Penrose.cs:97-99`), i.e. 5400 vertices/colors for two triangles per tile.
- `GenerateMesh()` consumes JSON mesh floats into Unity mesh vertices/colors and assigns `meshFilter.mesh` / `meshRenderer.material` (`Penrose.cs:217-266`).
- `GenerateTiles()` builds `TileData[]`, centers, positions, sections, rings, neighbors, angles/radii (`Penrose.cs:270-314`).
- `UpdateModelColors()` updates mesh vertex colors from `penrose.buffer` (`Penrose.cs:367-380`), with `FadeColorToBgColor()` blending against background based on color grayscale (`Penrose.cs:407-408`).

### `Controller.Start()` sequence

Evidence: `Assets/core/Controller.cs:660-710`.

1. Sets `Application.targetFrameRate = 60` (`:662`).
2. Finds `Penrose` and calls `penrose.Init()` (`:665-666`).
3. Initializes UI field text/listeners (`:668-676`).
4. Reflectively builds effects, transitions, blenders (`SetupEffects`, `SetupTransitions`, `SetupBlenders`; `:679-681`).
5. Sets UDP destination (`setIP(IP)`, `:682`) even though serial output is active in current source.
6. Adds `OSCReader` as a component and sets its handler (`:684-685`).
7. Creates drums overlay and pixel receiver (`:686-690`).
8. Optionally creates camera overlay if `useCamera` (`:692-697`).
9. Creates the timer and subscribes `OnTimerFinished` (`:699-700`).
10. Starts FPS coroutine and, because `#define ENABLE_SERIAL`, creates `SerialOut` and initializes it (`:703-710`).

### `Controller.Update()` frame flow

Evidence: `Assets/core/Controller.cs:806-954`.

1. `checkTime()`, `effectDelta = Time.deltaTime`, `timer.Update(effectDelta)` (`:808-813`). `Timer.Update()` invokes `onFinished` synchronously when elapsed (`Assets/core/helpers/Timer.cs:30-35`).
2. Reloads palette on Return (`Controller.cs:817-820`) and updates palette every frame (`:821`).
3. Toggles Nova effect override on Escape (`:824-827`).
4. Keyboard shortcuts:
   - `A` through `W` jump to effects in current keyboard bank (`:830-839`).
   - `X` toggles `keyboardBase` (`:842`).
   - Number keys trigger drum hits/rings (`:843-853`).
5. Updates drums and beat manager (`:854-855`).
6. Draw path:
   - If `NYE`, fills buffer with random black/white (`:856-864`).
   - Else if in transition: updates transition/effect time, draws transition, clones transition buffer into `penrose.buffer` (`:866-881`).
   - Else: updates/draws active effect, clones effect buffer into `penrose.buffer` (`:884-890`).
   - Applies optional filter and drum overlay (`:892-894`).
7. If camera enabled, updates/draws camera overlay (`:897-900`).
8. Checks `PixelReceiver`; if active, either blends with active blender/transition blender or replaces `penrose.buffer` with `blendBuffer` (`:914-943`).
9. Outputs frame:
   - Serial path active (`#if ENABLE_SERIAL`): `sendSerialFrame(penrose.buffer)` (`:947-948`).
   - UDP ACN fallback if serial define removed: `sendUDPFrame(penrose.buffer)` (`:949-950`).
10. Updates mesh vertex colors and sends OSC ping/state (`:953-954`).

## Effect / transition architecture

### Base lifecycle contract

The local README states the intended effect lifecycle: `Init()` once, `OnStart()` each activation, `Draw()` each displayed frame (`readme.md:1-5`). Code confirms this at the top-level effect array: `Controller.SetupEffects()` creates each factory effect and calls `Init()` once (`Assets/core/Controller.cs:161-173`), then chooses an initial effect, randomizes time, and calls `OnStart()` (`:180-183`).

`EffectBase` provides the shared contract:

- `buffer` (`Assets/core/EffectBase.cs:8`) holds 900 colors.
- Static `APalette` is initialized as `new AnimPalette()` (`:20`).
- `Init()` stores `Controller.Instance`, `controller.penrose`, `penrose.Tiles`, and allocates `new Color[Penrose.Total]` (`:35-40`).
- `RandomizeTime()` seeds `effectTime` to a random 0-4 hours (`:43-47`).
- `UpdateTime()` advances `effectTime` and stores `effectDelta` (`:49-53`).
- `OnStart()` enables beat behavior and assigns a random beat variant (`:56-59`).
- Subclasses must implement `DebugText()`, `OnEnd()`, and `Draw()` (`:31-66`).

**Important nuance:** top-level effects are created once, but mixers/wrappers instantiate child effects on `OnStart()`. Examples: `Mirror` creates and initializes `sourceEffect` each activation (`Assets/effects/Mirror.cs:76-80`), `RandomEffectsMixer` creates 2-3 child effects each activation (`Assets/effects/RandomEffectsMixer.cs:23-31`). So “each effect has only one instance” is true for the controller's top-level effect catalog, not for transient mixer children.

### Factory/reflection discovery

`Factory<T>` reflects all non-abstract subclasses of `T` in the assembly and instantiates via `Activator.CreateInstance` (`Assets/core/helpers/Factory.cs:19-27`). `Controller` uses it for:

- Effects (`Factory<EffectBase>`) at `Controller.cs:163-176`.
- Transitions (`Factory<TransitionBase>`) at `Controller.cs:388-399`.
- Blenders (`Factory<BlenderBase>`) at `Controller.cs:403-412`.

Implications for agents:

- Adding any non-abstract subclass automatically changes runtime catalogs.
- Reflection order is not explicitly sorted; keyboard shortcuts, deck order, initial `currentTransition` index, and debug listings can shift when scripts are added/removed.
- Constructors should stay parameterless.
- Because `EffectBase.Init()` assumes `Controller.Instance.penrose` and `penrose.Tiles` are ready, do not instantiate/init effects before `Controller.Start()` has called `penrose.Init()`.

### Deck/random selection and Nova override

`Controller` uses a “draw from top half and move to bottom” deck mechanism:

- `initDeck()` fills `[0..count-1]` (`Controller.cs:143-148`).
- `pullCard()` randomly picks from the top half (`Random.Range(0, length / 2)`) and moves that card to the end (`:150-158`).
- `GetNewEffectIndex()` returns the first effect whose name contains `forceEffectName` if `forceEffect` is true, otherwise pulls from `effectDeck` (`:717-730`).
- Escape toggles `forceEffect` at runtime (`:824-827`); the scene exposes `forceEffect`/`forceEffectName` in the inspector via public fields (`:70-74`).

Timer transition flow (`OnTimerFinished`, `Controller.cs:733-765`):

- If currently transitioning, switch to transition target `B`, reset timer to `effectTime`, update effect label, and draw a new transition card (`:735-745`).
- If currently playing, start transition: randomize/start current transition, set `A=currentEffect`, `B=GetNewEffectIndex()`, change palette, start target effect, set timer to `transitionTime`, set `currentEffect=-1`, and show transition name (`:748-765`).

### Effect class inventory

Custom effect classes discovered in `Assets/effects`:

| Type | Base | Path / declaration | Notes |
|---|---|---:|---|
| `Angles` | `EffectBase` | `Assets/effects/Angles.cs:7` | Angle-based tile coloring. |
| `AnimateLoops` | `EffectBase` | `Assets/effects/AnimateLoops.cs:5` | Uses `JsonRawData.shapes.loops` (`AnimateLoops.cs:26`). |
| `ColorSparkle` | `EffectBase` | `Assets/effects/ColorSparkle.cs:4` | Fading sparkle buffer. |
| `Flock` | `EffectBase` | `Assets/effects/Flock.cs:4` | Boid simulation. |
| `lightning` | `EffectBase` | `Assets/effects/lightning.cs:5` | Uses center star shapes (`lightning.cs:48`). Lowercase class name. |
| `Nibbler` | `EffectBase` | `Assets/effects/Nibbler.cs:4` | Random neighbor walks. |
| `Noise` | `EffectBase` | `Assets/effects/Noise.cs:4` | Perlin noise, palette, beat brightness/color/time modes (`Noise.cs:34-70`). |
| `NoiseTunnel` | `EffectBase` | `Assets/effects/NoiseTunnel.cs:4` | Perlin tunnel. |
| `Pulse` | `EffectBase` | `Assets/effects/Pulse.cs:4` | Pulse fills. |
| `TileShapes` | `EffectBase` | `Assets/effects/TileShapes.cs:5` | Random shape lists from JSON (`TileShapes.cs:34-62`). |
| `Vortex` | `EffectBase` | `Assets/effects/Vortex.cs:4` | Spinner/palette logic. |
| `Julia` | `ScreenEffect` | `Assets/effects/Julia.cs:5` | 2D Julia fractal mapped to Penrose (`Julia.cs:71-127`). |
| `MetaBalls` | `ScreenEffect` | `Assets/effects/MetaBalls.cs:5` | 2D metaballs then `ConvertScreenBuffer`. |
| `Petals` | `ScreenEffect` | `Assets/effects/Petals.cs:5` | Despite inheriting `ScreenEffect`, directly writes tile buffer using shapes. |
| `RainbowBars` | `ScreenEffect` | `Assets/effects/RainbowBars.cs:5` | 2D screen buffer conversion (`RainbowBars.cs:83`). |
| `Ripple` | `ScreenEffect` | `Assets/effects/Ripple.cs:5` | 2D ripples then conversion. |
| `Tunnel` | `ScreenEffect` | `Assets/effects/Tunnel.cs:6` | Direct tile tunnel but inherits `ScreenEffect`. |
| `Waterfall` | `ScreenEffect` | `Assets/effects/Waterfall.cs:5` | 2D waterfall then conversion (`Waterfall.cs:91`). |
| `fluid` | `ScreenEffect` | `Assets/effects/fluid.cs:6` | Lowercase class name; screen-ish fluid. |
| `kscope` | `ScreenEffect` | `Assets/effects/kscope.cs:13` | Loads PNGs from `StreamingAssets/images`, maps through mirror lists. Lowercase class name. |
| `Mirror` | `MixerBase` | `Assets/effects/Mirror.cs:5` | Wrapper: child effect + mirror2/mirror10 shape lists (`Mirror.cs:73-105`). |
| `NoiseMixer` | `MixerBase` | `Assets/effects/NoiseMixer.cs:3` | Two child effects mixed by Perlin; suppresses child beat pulses (`NoiseMixer.cs:33-38`, `:47-66`). |
| `Panels` | `MixerBase` | `Assets/effects/Panels.cs:4` | Panel/ring section patterns; case 2 child-effect mode currently unreachable because `Random.Range(0, 2)` only yields 0 or 1 (`Panels.cs:22`, `:34-40`). |
| `RandomEffectsMixer` | `MixerBase` | `Assets/effects/RandomEffectsMixer.cs:3` | Adds 2-3 child effect buffers (`RandomEffectsMixer.cs:23-56`). |
| `ShapeGlitch` | `MixerBase` | `Assets/effects/ShapeGlitch.cs:3` | Child effect plus shape highlighting. |
| `yinyangmixer` | `MixerBase` | `Assets/effects/yinyangmixer.cs:5` | Lowercase class name; two child effects. |

### `ScreenEffect` mapping layer

`ScreenEffect` supports effects written against a rectangular virtual grid:

- Static `width`, `height`, and `neighbors` cache mapping data (`Assets/core/ScreenEffect.cs:18-22`).
- `Init()` sets `width/height` from `penrose.Bounds`, allocates `screenBuffer`, and initializes interpolation weights once (`ScreenEffect.cs:98-119`).
- `ConvertScreenBuffer()` maps each tile through four weighted virtual-screen neighbors (`ScreenEffect.cs:86-94`).

Risk: mapping is static for the app lifetime. If wall geometry/bounds change at runtime or between scenes, `ScreenEffect.neighbors` is not invalidated.

### Mixer/wrapper patterns

- `MixerBase.GetRandomEffect()` loops until it picks a non-`MixerBase` effect, preventing nested mixers (`Assets/core/MixerBase.cs:8-16`).
- Passive mixer example: `RandomEffectsMixer` lets children choose their own beat variants (`RandomEffectsMixer.cs:27-31`).
- Active/suppressed child beat examples: `NoiseMixer` sets `effects[i].beatEnable = false` (`NoiseMixer.cs:33-38`), `yinyangmixer` does similarly (`Assets/effects/yinyangmixer.cs:37-41`).
- Unified beat example: `Mirror` copies the wrapper beat variant into its child (`Mirror.cs:80-81`).

### Transition classes

All transition classes inherit `TransitionBase`, not `EffectBase` (`Assets/core/TransitionBase.cs:6`). Shared model:

- `buffer` is a 900-color transition output (`TransitionBase.cs:10`).
- `A`, `B`, `V`, and `D` track source effect index, destination effect index, progress, and inverse progress (`TransitionBase.cs:39-64`).
- `DebugText()` reports `A => B` with weights (`TransitionBase.cs:66`).
- `Blend()` exists so transitions can also act as external pixel-source blenders when selected through telnet/prep tooling (`TransitionBase.cs:29-35`, concrete overrides in transition files).

Transition inventory:

| Type | Path / declaration | Notes |
|---|---:|---|
| `DirectionalWipe` | `Assets/transitions/DirectionalWipe.cs:3` | Angle-based wipe over tile positions (`DirectionalWipe.cs:20-61`). |
| `Fade` | `Assets/transitions/Fade.cs:2` | Linear blend; `Blend` usage `[ratio]` (`Fade.cs:17-36`). |
| `FizzleTransition` | `Assets/transitions/FizzleTransition.cs:5` | Random fixed order (`FizzleTransition.cs:8-36`). |
| `IndexWipe` | `Assets/transitions/IndexWipe.cs:2` | Index-progress wipe. |
| `IrisTransition` | `Assets/transitions/IrisTransition.cs:5` | Radial/directional iris. |
| `NoiseTransition` | `Assets/transitions/NoiseTransition.cs:6` | Perlin threshold plus border color (`NoiseTransition.cs:17-58`). |
| `RGBFade` | `Assets/transitions/RGBFade.cs:3` | Channel-staggered fade. |

## Rendering and hardware output

### In-memory rendering model

- Active effects and transitions always draw into local `Color[]` buffers sized to `Penrose.Total` (900).
- `Controller` clones the active output into `penrose.buffer` (`Controller.cs:878-890`) before overlays/blending/output.
- `Penrose.UpdateModelColors()` copies each tile color into six mesh vertex colors (`Penrose.cs:367-380`). Rendering uses `Unlit/Penrose`, a simple color-material shader (`Assets/core/PenroseShader.shader:1-8`).
- Graphics settings use no custom render pipeline (`ProjectSettings/GraphicsSettings.asset:41`), default rendering path 1 (`:44`), and always include `PenroseShader` by GUID (`GraphicsSettings.asset:31-37`, `Assets/core/PenroseShader.shader.meta:2`).

### Serial output path: currently active

Because `Assets/core/Controller.cs:2` defines `ENABLE_SERIAL`, the frame output branch calls `sendSerialFrame()` (`Controller.cs:947-948`).

`Controller.sendSerialFrame()`:

- Uses `penrose.JsonRawData.wires` (`Controller.cs:372`).
- Resizes `serialOutputBuffer` to wire length if needed (`:373-374`).
- Maps each physical LED index to tile color via `data[wires[i] / 2]` (`:378-379`).
- Calls `serial.send(serialOutputBuffer, level)` (`:382`).

`SerialOut`:

- Ignores the `baudRate` parameter and hard-codes `targetBaudRate = 2000000` (`Assets/core/SerialOut.cs:53-56`).
- Scans serial ports with `SerialPort.GetPortNames()` (`SerialOut.cs:68-70`).
- Asynchronously handshakes boards by sending `CMD_QUERY` (`?`, `0x3F`) and reading board type/payload/range (`SerialOut.cs:106-177`).
- Uses one background I/O thread per board (`SerialOut.cs:182-184`, `:221-261`).
- Packs `CMD_DATA` plus start/count/RGB bytes and appends `CMD_LATCH` (`SerialOut.cs:233-258`).
- Copies frame data and signals each board thread in `send()` (`SerialOut.cs:265-292`).
- Exposes a debug string of OS/active/connecting/ignored ports (`SerialOut.cs:299+`).

### UDP / E1.31 path: present but inactive with current define

If `ENABLE_SERIAL` is removed/undefined, `Controller.Update()` calls `sendUDPFrame()` (`Controller.cs:949-950`). The UDP path:

- `setupUDP()` creates a `UdpClient` and `IPEndPoint` from scene `IP` and port 5568 (`Controller.cs:237-241`, `Controller.cs:57-60`).
- `sendUDPFrame()` maps `wires` to a 5400-byte buffer with channel order R, B, G (`Controller.cs:336-353`).
- It sends DMX/E1.31-sized chunks of 510 bytes via `sendACN()` (`Controller.cs:357-361`).
- `sendACN()` is `async Task` and uses `await client.SendAsync(...)` (`Controller.cs:249-280`), but calls in `sendUDPFrame()` are not awaited (`Controller.cs:359-361`), so exceptions can be lost and frames can overlap.

### Pixel receiver / external blend input

- `PixelReceiver.Init()` opens UDP port 7778 and allocates a 900-color buffer (`Assets/core/PixelReceiver.cs:13-16`).
- Packets use a 6-byte header; context `0x00` means RGB data, `byteOffset / 3` selects the starting pixel, and packets set `timeout = 100` frames (`PixelReceiver.cs:29-62`).
- `Controller.Update()` checks `readPixel.Update()` and either blends with `ActiveBlender` / `ActiveTransitionBlender` or replaces `penrose.buffer` (`Controller.cs:914-943`).
- `UDPReceive` runs a background receive loop forever and swallows exceptions (`Assets/core/UDPControllers.cs:32-39`, `:69-84`). It has no stop/dispose path.

### Drums overlay and OSC

- `drums.Init()` opens UDP port 8500 for OpenPixel-like drum packets (`Assets/core/drums.cs:30-37`).
- `drums.Draw()` overlays hit/ring colors based on five fixed points (`drums.cs:51-114`).
- `drums.handleOpenPixel()` triggers hits when bytes `[4..8]` exceed 20 (`drums.cs:161-166`).
- `OSCReader` is added dynamically by `Controller.Start()` (`Controller.cs:684-685`). It defaults to listening on UDP 6969 and sending to `192.168.1.255:6161` (`Assets/OSCReader.cs:197-201`).
- `OSCReader.Awake()` creates `UDPPacketIO`, starts a background read thread, and dispatches queued messages on Unity `Update()` (`OSCReader.cs:218-233`, `:256-282`).
- `OSCReader.Close()` uses `ReadThread.Abort()` (`OSCReader.cs:299-307`). That is risky/obsolete in modern .NET/Unity contexts; verify before changing threading behavior.
- `Controller.OscHandler()` has an empty `/beat` branch (`Controller.cs:491-493`); current `BeatManager` is an internal simulation rather than OSC-driven.

### Camera overlay

Camera overlay is disabled in the scene (`SampleScene.unity:2018`). If enabled, `Controller.Start()` creates `CameraReader` (`Controller.cs:692-697`). `CameraReader` requests webcam authorization and starts a `WebCamTexture` (`Assets/core/CameraReader.cs:69-75`), samples it into a virtual screen, then maps through `ScreenEffect.ConvertScreenBuffer` (`CameraReader.cs:114-146`).

## Palette and beat systems

### Palette

- `EffectBase.APalette` is a static `AnimPalette` (`Assets/core/EffectBase.cs:20`).
- `AnimPalette` constructor reads `Controller.Instance.paletteSource`, parses hex and gradient definitions, and falls back to built-in static samples if no palettes are parsed (`Assets/core/helpers/GPalette.cs:116-146`).
- `Controller.Update()` reloads `APalette` on Return (`Controller.cs:817-820`) and calls `APalette.Update()` every frame (`Controller.cs:821`).
- Palette transitions are randomized through `AnimPalette.Change()`; `Controller.JumpToEffect()` and transition starts call `EffectBase.APalette.Change()` (`Controller.cs:420`, `:755`).

Risk: because `APalette` construction depends on `Controller.Instance.paletteSource`, don't reference `EffectBase`/effects from static/editor contexts before the scene Controller is initialized.

### Beat

- `BeatData` defaults active at 120 BPM with 4 beats per measure (`Assets/core/BeatManager.cs:5-11`).
- `BeatManager.Update()` derives beat position from `Time.time`, not external OSC (`BeatManager.cs:24-48`).
- `EffectBase.OnStart()` assigns `beatVariant = beatManager.GetRandomVariant()` (`EffectBase.cs:56-59`), and many effects call `GetBeatBrightness(...)` during `Draw()`.
- `GetBeatBrightness()` maps variants by switch (`BeatManager.cs:75-112`). Current code maps `case 5` to 8th notes, `case 6` to 16th notes, and `case 4` to syncopated (`BeatManager.cs:105-110`). This conflicts with comments/docs that describe variant 4 as 8th, 5 as 16th, 6 as syncopated. Clarify before changing beat behavior.

## Packages and project settings

- Unity editor version: `6000.4.7f1` (`ProjectSettings/ProjectVersion.txt:1-2`).
- Main packages in `Packages/manifest.json`:
  - AI Navigation `2.0.12` (`manifest.json:3`).
  - IDE integrations Visual Studio `2.0.27`, VSCode `1.1.4` (`manifest.json:6-7`).
  - Test Framework `1.6.0` (`manifest.json:9`) — no custom tests found under `Assets`.
  - Timeline `1.8.12` (`manifest.json:10`).
  - uGUI `2.0.0`, UI Builder `2.0.0` (`manifest.json:11-12`).
  - Built-in modules include `imageconversion`, `ui`, `uielements`, `unitywebrequest`, etc. (`manifest.json:22`, `:32-40`).
- Built-in render pipeline: no custom render pipeline (`GraphicsSettings.asset:41`).
- Project uses Gamma color space (`ProjectSettings/ProjectSettings.asset:50`, `m_ActiveColorSpace: 0`).
- Product/app IDs: company `Hunter`, product `penrose_simulator` (`ProjectSettings.asset:15-16`), Android app ID `com.hunter.penrosesimulator` (`ProjectSettings.asset:166-167`).
- Project scripting define symbols are empty (`ProjectSettings.asset:614`); active serial output comes from file-local `#define ENABLE_SERIAL` in `Controller.cs`, not project settings.

## Safe working guide for future agents

### Navigation and edit safety

- Prefer symbol-aware navigation for C# if available. If not, use targeted searches by symbol/method names; avoid broad reads of `Library/`, `Temp/`, `Obj/`, `Logs/`, generated `.csproj`, and TMP example folders unless directly relevant.
- Do not edit generated `.csproj`/`.slnx` files; Unity regenerates them and `.gitignore` ignores them.
- Preserve `.meta` files and GUIDs. Scene script references depend on script meta GUIDs, e.g. Controller GUID `5f47cb3b2738dba41858c13614a36b80` (`Assets/core/Controller.cs.meta:2`, `SampleScene.unity:1097`).
- Be cautious editing `SampleScene.unity`: `Controller.jsonSource` and `paletteSource` are huge serialized strings and easy to corrupt. If the source of truth should be `StreamingAssets`, ask before migrating scene data.
- Use exact class/file names carefully. Some classes are lowercase (`fluid`, `kscope`, `lightning`, `yinyangmixer`), and factory names come from `GetType().ToString()`.

### Adding/changing effects

- New top-level effect: create a non-abstract parameterless class inheriting `EffectBase` or `ScreenEffect`, implement `DebugText`, `OnStart`, `OnEnd`, `Draw`, and call `base.Init()` / `base.OnStart()` when overriding.
- Always allocate/write within `buffer.Length == Penrose.Total`; do not assume a different wall size without changing `Penrose.Total`, JSON data, mesh generation, wire mapping, output buffers, and protocol assumptions together.
- If using 2D coordinates, inherit `ScreenEffect` and write `screenBuffer`, then call `ConvertScreenBuffer(ref screenBuffer, in buffer)`.
- If using child effects, inherit `MixerBase` so `GetRandomEffect()` avoids recursive mixers. Decide deliberately whether child beat is passive, unified, or suppressed.
- Any new subclass is auto-discovered; verify effect count/order and keyboard mapping after adding it.

### Running the project safely

Before pressing Play or running batchmode, be aware runtime side effects include:

- Serial port scanning/opening every run because `ENABLE_SERIAL` is file-defined (`Controller.cs:2`, `SerialOut.cs:68-112`). Disconnect hardware or change configuration only with approval.
- UDP listeners on 6969 (OSC), 7778 (pixel receiver), and 8500 (drums) (`OSCReader.cs:199`, `PixelReceiver.cs:14`, `drums.cs:36`).
- `kscope` rewrites `Assets/StreamingAssets/images/*/files.txt` on non-Android during `Init()` (`Assets/effects/kscope.cs:105-111`). Running Play Mode can therefore dirty tracked StreamingAssets files.
- Telnet support is currently inactive, but if `ENABLE_TELNET` is enabled it listens on port 23 and `Service()` appends to `log.txt` (`Assets/core/helpers/TelnetServer.cs:243-257`, `:421-437`).

### Validation path

- Best validation is opening/running with Unity `6000.4.7f1` and `Assets/Scenes/SampleScene.unity`.
- If CLI validation is approved, run Unity batchmode compile/playmode checks with a dedicated log file; expect `Library/` and `Logs/` writes.
- No custom tests were found under `Assets`. If behavior changes are needed, consider adding Unity Test Framework tests, but this project is currently runtime/hardware-heavy.
- For output changes, validate separately with hardware/offline packet tests because serial, E1.31, OSC, UDP pixel receiver, and scene mesh rendering have different failure modes.

## Risks, discrepancies, and open questions

### Confirmed from local code/assets

1. **Active output path contradicts docs.** `CONTEXT.md` describes ACN/E1.31 output (`CONTEXT.md:4`, `:37-48`), but active `Controller.cs` file-defines `ENABLE_SERIAL` (`Controller.cs:2`) and thus uses serial output (`Controller.cs:947-948`). Clarify intended production output path before changing output code.
2. **Top-level singleton lifecycle vs mixer children.** `readme.md` says each effect has only one instance and is never destroyed (`readme.md:1-5`), but mixers/wrappers create new child effects on activation (`Mirror.cs:76-80`, `RandomEffectsMixer.cs:23-31`). Treat the README as top-level effect guidance, not a universal allocation invariant.
3. **Beat variant mismatch.** Code maps variants 4/5/6 differently than comments/docs (`BeatManager.cs:105-110`, `readme.md:25-29`). Needs user decision before “fixing.”
4. **Serial protocol docs drift.** `S2_MINI_PROTOCOL.md` says baud 2,000,000 in connection settings but later says Arduino `Serial.begin(230400)` and 200ms boot delay; code ignores `Init(230400)`, hard-codes 2,000,000, waits 2 seconds, and appends `CMD_LATCH` (`SerialOut.cs:53-58`, `:125`, `:258`; protocol doc `Assets/core/S2_MINI_PROTOCOL.md:7`, `:11`, `:40-46`). Clarify firmware contract before changing serial protocol.
5. **Reflection order is unsorted.** `Factory<T>` does not sort types (`Factory.cs:19-24`). Adding/removing scripts can shift indexes used by keyboard shortcuts and scene `currentTransition`.
6. **Thread lifecycle hazards.** `UDPReceive` loops forever and swallows receive exceptions (`UDPControllers.cs:69-84`); `OSCReader.Close()` uses `Thread.Abort()` (`OSCReader.cs:299-307`); `SerialOut` board thread catches all exceptions and only marks board not ready (`SerialOut.cs:260-261`). Be careful with playmode/domain reload behavior and hardware error visibility.
7. **`Panels` child-effect mode appears unreachable.** `which = Random.Range(0, 2)` (`Panels.cs:22`) only yields 0 or 1 for integer overload, but `case 2` exists in both `OnStart` and `Draw` (`Panels.cs:34-40`, `:96+`). Ask before changing because it affects visuals.
8. **`kscope` can dirty assets at runtime.** It regenerates `files.txt` in StreamingAssets image folders on non-Android (`kscope.cs:105-111`).
9. **No asmdefs/tests.** All custom code is in default assembly with TMP examples; no custom test files were found. Compile scope is broad.

### Gaps needing user clarification or web/API research

- Intended production target: Unity Editor simulator, standalone desktop controller, Android (`android_wall.aab` exists), or USB-serial hardware controller? This affects `System.IO.Ports`, permissions, threading, and output path.
- Source of truth for wall geometry/palettes: scene-serialized `jsonSource`/`paletteSource` vs `Assets/StreamingAssets/rawdata.json` and palette text files.
- Whether to preserve current file-local `#define ENABLE_SERIAL` or move output mode to project scripting symbols / inspector config.
- E1.31/ACN packet correctness and channel order (UDP path writes R,B,G while serial writes R,G,B). Verify against actual LED hardware/protocol before modifying.
- Unity 6000 compatibility/best practices for `Thread.Abort`, `System.IO.Ports`, `WebCamTexture` authorization, and synchronous `UnityWebRequest` polling on Android should be researched if those areas are changed.
- Whether TextMesh Pro examples should remain in `Assets/TextMesh Pro/Examples & Extras`; they compile into the default assembly but are not custom runtime logic.
