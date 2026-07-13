# Code Map

This map summarizes the project-authored runtime and editor code. It is meant as an orientation aid before editing, not as a replacement for reading the source.

## Root assets

| File | Role |
| --- | --- |
| `Assets/OSC.cs` | Shared OSC message type plus parser/serializer helpers. The `OSC` MonoBehaviour inside this file is mostly legacy in this project. |
| `Assets/OSCReader.cs` | Active OSC reader MonoBehaviour used by `Controller`; receives packets on a background thread and dispatches parsed messages on Unity's main thread. |

## Core runtime

| File | Role |
| --- | --- |
| `Assets/core/Runtime/Controller.cs` | Main runtime hub: lifecycle, catalogs, timing, input routing, output routing, overlays, preview update. |
| `Assets/core/Runtime/Penrose.cs` | Penrose layout model, tile metadata, mesh generation, bounds, and Unity preview color updates. |
| `Assets/core/Runtime/WallData.cs` | Data contracts for `Assets/StreamingAssets/`: `LayoutData` (the pattern, `penrose_layout.txt`) and `WiringData` (per-art-piece LED addressing, `wiring_*.txt`); comment-stripping parse + wire-map validation. |
| `Assets/core/Effects/EffectBase.cs` | Base contract for tile-buffer effects. |
| `Assets/core/Effects/ScreenEffect.cs` | Base for rectangular 2D algorithms mapped onto Penrose tiles. |
| `Assets/core/Effects/MixerBase.cs` | Base for effects that own child effects. |
| `Assets/core/Transitions/TransitionBase.cs` | Base for effect-to-effect transitions and transition-as-blender behavior. |
| `Assets/core/Reference/Transition.cs` | Legacy/orphaned transition shape inheriting `EffectBase`; not used by the current controller. |
| `Assets/core/Rhythm/BeatManager.cs` | Shared live/simulated rhythm state and beat-reactive helper functions. |
| `Assets/core/Rhythm/BeatManagerQueries.cs` | Contrived rhythm-query layer (ADR-0002): the nullable `PhraseEventInfo`/`EnergyInfo`/`GridInfo`/`LevelsInfo` shapes and the `BeatManager` queries that build them. |
| `Assets/core/IO/RaveOscReceiver.cs` | Unity-hosted bridge that applies current Rave OSC on-air snapshots into `BeatManager` before the Director ticks. |
| `Assets/core/Switching/Director.cs` | Wire-change reducer (ADR-0011) for Standalone/Synced/Hold sequencing: holds two Cue Sheets repaired by invariant on each new-beat wake, casts a Cue lazily and preference-based when a Grid carrying a Cue Mark begins, and hands it to the Switcher fire-and-forget. Records no decision memory. Plus staged choices and read-only status. |
| `Assets/core/Switching/CueSheet.cs` | Builds a Cue Sheet: an index of Cue Marks over an announced phrase length, marks on Grid Boundaries, gaps of one to four Grids, phrase end always marked; layout is an announcement-seeded random roll. |
| `Assets/core/Switching/Deck.cs` | The rotating card deck behind effect and transition casting: rotate-to-back pulls, draw-window random draws, and read-only preferred finds pulled only on Switcher acceptance. |
| `Assets/core/Switching/Switcher.cs` | Mechanical execution of ShowNow/StartTransition/RenderAtTime and sole owner of cue commitment (one beat-domain lock, private runway/tail/lock math; loading a cue returns accepted-or-not), Switcher-held Loaded Cue scheduling, and active A-to-B progress. |
| `Assets/core/Transitions/TransitionSettings*.cs` | Transition Repertoire/settings assets, code defaults, saved authoring values, and validation. |
| `Assets/core/Rhythm/Waveform.cs` | Immutable Waveform kernel plus clock-bound `Envelope`/`Lerp` playback value. |
| `Assets/core/Rhythm/WaveformPool.cs` | Shared waveform pool codec and runtime load path for `StreamingAssets/penrose_waveforms.txt`. |
| `Assets/core/ReactiveInputs/drums.cs` | Drum and ring overlay system plus UDP/OSC-style trigger handling. |
| `Assets/core/Hardware/SerialOut.cs` | USB serial hardware discovery and frame sending for S2 Mini / ESP32 boards. |
| `Assets/core/IO/PixelReceiver.cs` | UDP pixel-source receiver for external frame blending. |
| `Assets/core/ReactiveInputs/CameraReader.cs` | Optional webcam overlay and OSC camera controls. |
| `Assets/core/IO/UDPControllers.cs` | Small background UDP receive helper. |
| `Assets/core/Effects/Perlin.cs` | Shared Perlin/fBm noise utility. |
| `Assets/core/Reference/Controller - nova.cs` | Inactive reference-only controller experiment under `#if false`. |

## Helpers

| File | Role |
| --- | --- |
| `Assets/core/helpers/Factory.cs` | Reflection catalog builder and `[RuntimeCatalogIgnore]` attribute. |
| `Assets/core/Blending/BlenderBase.cs` | Base contract for external-source blenders. |
| `Assets/core/helpers/GPalette.cs` | Palette loading, sampling, and animated palette transitions. |
| `Assets/core/helpers/Timer.cs` | Plain C# timer used by the Director's Standalone Mode cadence path. |
| `Assets/core/helpers/Singleton.cs` | Generic Unity singleton used by `Controller`. |
| `Assets/core/helpers/ExtensionMethods.cs` | Shared numeric, vector, color, and buffer helper methods. |
| `Assets/core/helpers/SliderScript.cs` | Scene UI slider binding for controller brightness. |
| `Assets/core/helpers/TelnetServer.cs` | Optional telnet command server compiled only with `ENABLE_TELNET`. |

## Editor tooling

| File | Role |
| --- | --- |
| `Assets/Editor/Controller/ControllerEditor.cs` | Custom Controller inspector and Director timing observatory. |
| `Assets/Editor/Rhythm/BeatManagerDrawer.cs` | BeatManager property drawer and dashboard adapter. |
| `Assets/Editor/Rhythm/BeatManagerDashboardModel.cs` | Editor-only rhythm dashboard display model, including phrase-event and rhythm text formatting. |
| `Assets/Editor/Rhythm/BeatManagerDashboardRenderer.cs` | IMGUI rendering for the BeatManager dashboard. |
| `Assets/Editor/Rhythm/PhraseEventView.cs` | Editor-side display model of a phrase-event rhythm query (Fill/Drop): chip, meter, readout, and Now/Soon/Idle state. Formats the core `PhraseEventInfo` for the BeatManager dashboard. |
| `Assets/Editor/Rhythm/RhythmText.cs` | Editor-side text formatting for rhythm-query nullable beat/count values (`"16b"`, plain counts, `"—"` for null). |
| `Assets/Editor/Rhythm/Waveforms/WaveformPoolEditor.cs` | Waveform Pool editor window and save path. |
| `Assets/Editor/Rhythm/Waveforms/WaveformPlot.cs` | Shared editor plotter for runtime `Waveform.Sample` output. |
| `Assets/Editor/Effects/EffectSelectorDrawer.cs` | Effect selector property drawer. |
| `Assets/Editor/Transitions/TransitionSettingsAssetUtility.cs` | Transition settings asset creation/restoration utility. |
| `Assets/Editor/Tuning/PenroseTuningWindow.cs` | Tuning window for transitions and related authoring controls. |
| `Assets/Editor/Shared/LiveControllerAccess.cs` | Shared editor helper for resolving live Controller state and play-mode repaint. |

## Effects

### Direct tile effects

| Effect | Role |
| --- | --- |
| `Angles` | Tile-angle hue sweep with beat brightness. |
| `AnimateLoops` | Animated packed shape-loop coloring over a background. |
| `ColorSparkle` | Fading sparkle field over persistent buffer trails. |
| `Flock` | Boid simulation projected to tile positions. |
| `Nibbler` | Random neighbor walkers that paint fading trails. |
| `Noise` | Palette-based Perlin tile shader with beat distortion modes. |
| `NoiseTunnel` | Radius/diagonal tunnel bands from tile positions. |
| `Pulse` | Two-color ping-pong fill based on tile type. |
| `TileShapes` | Random packed Penrose shape-list flashes. |
| `Vortex` | Spinner-driven nearest-source palette field. |
| `lightning` | Branching stochastic paths from center-star shapes. |

### Screen-space effects

| Effect | Role |
| --- | --- |
| `Julia` | Julia fractal mapped from a rectangular buffer to Penrose tiles. |
| `MetaBalls` | Screen-space metaball field mapped to tiles. |
| `Petals` | Shape-list coloring; inherits `ScreenEffect` but writes tile buffer directly. |
| `RainbowBars` | Directional screen-space palette bars mapped to tiles. |
| `Ripple` | Expanding screen-space ripple rings mapped to tiles. |
| `Tunnel` | Direct tile-space tunnel; inherits `ScreenEffect` but writes tile buffer directly. |
| `Waterfall` | Falling screen-space droplets over a palette background. |
| `fluid` | Tile-neighbor diffusion simulation; inherits `ScreenEffect` but writes tile buffer directly. |
| `kscope` | Texture kaleidoscope/image scroller using StreamingAssets images and mirror groups. |

### Mixers and wrappers

| Effect | Role |
| --- | --- |
| `Mirror` | Wraps one child effect and mirrors it through Penrose mirror groups. |
| `NoiseMixer` | Mixes two child effects using Perlin noise. |
| `Panels` | Panel/section color modes, with a currently unreachable child-effect mode. |
| `RandomEffectsMixer` | Additively mixes two or three child effects. |
| `ShapeGlitch` | Child effect plus blinking/fading packed shape overlays. |
| `yinyangmixer` | Two child effects split into rotating angular regions. |

### Template

| File | Role |
| --- | --- |
| `EmptyEffect` | Copy/rename starter effect. Marked `[RuntimeCatalogIgnore]`, so it is never included in the runtime catalog. |

## Transitions

| Transition | Role |
| --- | --- |
| `Fade` | Linear crossfade; also supports `[ratio]` external blending. |
| `IndexWipe` | Raw tile-index wipe; also supports `[ratio]` external blending. |
| `FizzleTransition` | Fixed shuffled reveal order; also supports `[ratio]` external blending. |
| `DirectionalWipe` | Angle-based geometry wipe; also supports `[ratio] [angle]` external blending. |
| `IrisTransition` | Radial iris transition; also supports `[ratio] [direction]` external blending. |
| `NoiseTransition` | Perlin threshold transition with border color; also supports `[ratio] [borderHue]` external blending. |
| `RGBFade` | Staggered per-channel fade. Its current `Usage()` string advertises a second parameter that implementation does not use. |

## External blenders

| Blender | Role |
| --- | --- |
| `RGBBlender` | Per-channel mix between native and external buffers. Requires three settings. |
| `SilhouetteBlender` | Treats exact black external pixels as transparent and blends non-black pixels over native output. |
