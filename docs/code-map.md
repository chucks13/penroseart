# Code Map

This map summarizes the project-authored runtime code. It is meant as an orientation aid before editing, not as a replacement for reading the source.

## Root assets

| File | Role |
| --- | --- |
| `Assets/OSC.cs` | Shared OSC message type plus parser/serializer helpers. The `OSC` MonoBehaviour inside this file is mostly legacy in this project. |
| `Assets/OSCReader.cs` | Active OSC reader MonoBehaviour used by `Controller`; receives packets on a background thread and dispatches parsed messages on Unity's main thread. |

## Core runtime

| File | Role |
| --- | --- |
| `Assets/core/Runtime/Controller.cs` | Main runtime hub: lifecycle, catalogs, timing, input routing, output routing, overlays, preview update. |
| `Assets/core/Runtime/Penrose.cs` | Penrose JSON data, tile metadata, mesh generation, bounds, and Unity preview color updates. |
| `Assets/core/Effects/EffectBase.cs` | Base contract for tile-buffer effects. |
| `Assets/core/Effects/ScreenEffect.cs` | Base for rectangular 2D algorithms mapped onto Penrose tiles. |
| `Assets/core/Effects/MixerBase.cs` | Base for effects that own child effects. |
| `Assets/core/Transitions/TransitionBase.cs` | Base for effect-to-effect transitions and transition-as-blender behavior. |
| `Assets/core/Reference/Transition.cs` | Legacy/orphaned transition shape inheriting `EffectBase`; not used by the current controller. |
| `Assets/core/Rhythm/BeatManager.cs` | Shared live/simulated rhythm state and beat-reactive helper functions. |
| `Assets/core/Rhythm/BeatManagerQueries.cs` | Contrived rhythm-query layer (ADR-0002): the nullable `PhraseEventInfo`/`EnergyInfo`/`PhaseInfo`/`LevelsInfo` shapes and the `BeatManager` queries that build them. |
| `Assets/core/IO/RaveOscReceiver.cs` | Unity-hosted bridge that applies current Rave OSC on-air snapshots into `BeatManager` before the Director ticks. |
| `Assets/core/Switching/Director.cs` | Decision layer for Standalone/Synced/Hold sequencing, staged choices, cues, and status. |
| `Assets/core/Switching/OnAirTiming.cs` | Synced Mode timing seam that turns live beat/Track Phase state into a Director-facing Timing Frame and current Cue Mark. |
| `Assets/core/Switching/PhaseClock.cs` | Resolves the fixed 16-beat Phase reading from beat, bar, and Track Phase evidence. |
| `Assets/core/Switching/PhraseWindow.cs` | Derives phrase spans and phase boundaries from Track Phase countdown/length data. |
| `Assets/core/Switching/CueSheet.cs` | Selects relative Cue Marks inside a Phrase while keeping the final phrase boundary mandatory and reusing same-length plans. |
| `Assets/core/Switching/ChangeCadence.cs` | Minimum-change cadence rule shared by timing, cue, and status decisions. |
| `Assets/core/Switching/SyncedCueIntent.cs` | Synced cue/casting seam combining Timing Frame, Transition Repertoire, Drop data, staged choices, and Effect Repertoire. |
| `Assets/core/Switching/EffectDeckSelection.cs` | Effect deck draw/preference rules, including Repertoire-aware preferred Performer pulls. |
| `Assets/core/Transitions/TransitionBeatPlan.cs` | Converts Cue Mark plus Transition Runway/Tail into start/impact/completion beats. |
| `Assets/core/Switching/Switcher.cs` | Mechanical execution of ShowNow/StartTransition/RenderAtTime, Switcher-held Loaded Cue scheduling, and active A-to-B progress. |
| `Assets/core/Transitions/TransitionSettings*.cs` | Transition Repertoire/settings assets, code defaults, saved authoring values, and validation. |
| `Assets/core/Rhythm/PhraseEventView.cs` | Display model of a phrase-event rhythm query (Fill/Drop): chip, meter, readout, and Now/Soon/Idle state. Shared by the Beat Manager inspector and any future telnet/OSC readout. |
| `Assets/core/Rhythm/RhythmText.cs` | Shared text formatting for the rhythm queries' nullable beat/count values (`"16b"`, plain counts, `"—"` for null). |
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
