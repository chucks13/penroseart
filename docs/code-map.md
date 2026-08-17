# Code Map

This map summarizes the project-authored runtime and editor code. It is meant as an orientation aid before editing, not as a replacement for reading the source.

## Root assets

| File | Role |
| --- | --- |
| `Assets/OSC/*.cs` | Vendored, generic `RaveSystem.Osc` wire-format, dispatch, and transport library; Penrose/Rave policy stays outside this layer. |
| `Assets/core/IO/TouchOscSurface.cs` | Active TouchOSC adapter: receives operator controls on UDP 6969 and sends surface feedback. |
| `Assets/core/IO/RaveOscReceiver.cs` | Unity-hosted adapter that applies current Rave on-air snapshots to `BeatManager` before Director decisions. |

## Core runtime

| File | Role |
| --- | --- |
| `Assets/core/Runtime/Controller.cs` | Main runtime hub: lifecycle, catalogs, timing, input routing, output routing, overlays, preview update. |
| `Assets/core/Runtime/Penrose.cs` | Penrose layout model, tile metadata, mesh generation, bounds, and Unity preview color updates. |
| `Assets/core/Runtime/PenroseShader.shader` | Unlit shader used by the Unity wall preview mesh. |
| `Assets/core/Runtime/WallData.cs` | Data contracts for `Assets/StreamingAssets/`: `LayoutData` (the pattern, `penrose_layout.txt`) and `WiringData` (per-art-piece LED addressing, `wiring_*.txt`); comment-stripping parse + wire-map validation. |
| `Assets/core/Runtime/CueLog.cs` | Always-on per-run diagnostic sink: one `penrose-<timestamp>.log` session file under `persistentDataPath/Logs`, rotated to the newest 20, opened lazily on the first line. Owns the file, not the record format — callers hand it finished lines. |
| `Assets/core/Effects/EffectBase.cs` | Base contract for tile-buffer effects. |
| `Assets/core/Effects/ScreenEffect.cs` | Base for rectangular 2D algorithms mapped onto Penrose tiles. |
| `Assets/core/Effects/MixerBase.cs` | Base for effects that own child effects. |
| `Assets/core/Transitions/TransitionBase.cs` | Base for effect-to-effect transitions and transition-as-blender behavior. |
| `Assets/core/Reference/Transition.cs` | Legacy/orphaned transition shape inheriting `EffectBase`; not used by the current controller. |
| `Assets/core/Rhythm/BeatManager.cs` | Private Rave snapshot ownership, live/Standalone source handling, `IsSynced`, per-frame derivation, and frame-coherent Data Surface capture. |
| `Assets/core/Rhythm/TimingValues.cs`, `TrackValues.cs`, `LiveOrderValues.cs` | Captured route, timing, playhead-position, track-identity, and ordered live-player focus values. |
| `Assets/core/Rhythm/PlayersValues.cs` | Frame-coherent snapshot of all six physical players, including each player's timing, transport, loop, Grid, structure, and cursor values. |
| `Assets/core/Rhythm/BeatsValues.cs`, `OffbeatsValues.cs`, `PulsesValues.cs`, `Duration.cs` | Beat/offbeat wire countdowns and triggers, plus tempo-based musical pulses. |
| `Assets/core/Rhythm/PhraseValues.cs`, `FillValues.cs`, `DropValues.cs`, `EnergyValues.cs`, `GridValues.cs`, `StockEnvelopes.cs`, `Spans.cs`, `PhraseHandleValues.cs` | Phrase-structure wire values, direct progress facts, and Build/Decay calculations. `Spans.cs` holds the Before/In span pair; `PhraseHandleValues.cs` serves the seven Song Structure phrase handles from the Focus player's structure cursor. |
| `Assets/core/Rhythm/LoopValues.cs`, `LevelsValues.cs` | Loop wire values and always-available normalized/smoothed/peak audio-band values. |
| `Assets/core/Rhythm/Waveforms.cs`, `Waveform.cs`, `WaveformPool.cs`, `Routine.cs` | Explicit immutable Waveform acquisition, clock-bound playback, Pool codec/load path, and four-bar Routine composition. |
| `Assets/core/Switching/Director.cs` | Standalone cadence plus Synced planning and decisions: maintains six track-sheet slots, hands the on-air focus player's sheet to the Switcher, and answers the due-mark question and the one anomaly doorway (`DecideOffPlanCue`: a re-crossed fired mark, a self-blend mark, or Stillness — ride through or a fresh dealt cue, never the on-wall Effect or the one being moved toward) with override-aware `CueDecision` values, remembering nothing between asks. |
| `Assets/core/Switching/TrackCueSheet.cs` | Pure full-track Cue Sheet builder: seeded Effect/Transition bags, baked assignments, no more than four actual Grids without a transition, drop/fill Anchor casting and clearance, and deterministic `DealOffPlanCueAt(...)` off-plan deals. |
| `Assets/core/Switching/Deck.cs` | Rotating card deck used by Standalone effect and transition selection. |
| `Assets/core/Switching/Switcher.cs` | Holds the handed-over Cue Sheet and its permanent check-offs; thinks once per Grid at Grid start from the on-air beat and Grid, gives an unfired non-self-blend planned Cue priority, schedules planned and Off-Plan Cues at boundary-minus-Runway, owns Runway/Impact/Tail timing and always-on Grid-counted Stillness, and reports every anomaly through the Director's one Off-Plan doorway. |
| `Assets/core/Transitions/TransitionSettings*.cs` | Transition Repertoire/settings assets, code defaults, saved authoring values, and validation. |
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
| `Assets/Editor/Controller/ControllerEditor.cs` | Compact scene/configuration/runtime-health inspector with Effect Hold and a Tuning Window launcher. |
| `Assets/Editor/Rhythm/BeatManagerDrawer.cs` | BeatManager property drawer and dashboard adapter. |
| `Assets/Editor/Rhythm/BeatManagerDashboardModel.cs` | Editor-only rhythm dashboard display model, including phrase-event and rhythm text formatting. |
| `Assets/Editor/Rhythm/BeatManagerDashboardRenderer.cs` | IMGUI rendering for the BeatManager dashboard. |
| `Assets/Editor/Rhythm/PhraseEventView.cs` | Editor-side Fill/Drop display model: chip, meter, readout, and Now/Soon/Idle state derived from the captured values. |
| `Assets/Editor/Rhythm/RhythmText.cs` | Editor-side text formatting for nullable musical beat/count facts (`"16b"`, plain counts, `"—"` for null). |
| `Assets/Editor/Rhythm/Waveforms/WaveformPoolEditor.cs` | Waveform Pool editor window and save path. |
| `Assets/Editor/Rhythm/Waveforms/WaveformPlot.cs` | Shared editor plotter for runtime `Waveform.Sample` output. |
| `Assets/Editor/Effects/EffectHoldRenderer.cs` | Shared Effect / Hold control for the Controller inspector and Tuning Window. |
| `Assets/Editor/Transitions/TransitionSettingsAssetUtility.cs` | Transition settings asset creation/restoration utility. |
| `Assets/Editor/Tuning/PenroseTuningWindow.cs` | Canonical workspace for live sequencing, rhythm observation, and saved Effect or Transition tuning. |
| `Assets/Editor/Shared/LiveControllerAccess.cs` | Shared editor helper for resolving live Controller state and play-mode repaint. |
| `Assets/Editor/Tuning/CueSheetTimeline.cs` | Pure Unity-free projection of a Cue Sheet into Grid rows: `CueSheetBeatMark` flags, `CueSheetGridRow`, and `Build`. Rows restart at every phrase, as the Grid itself does. |
| `Assets/Editor/Tuning/CueSheetTimelineRenderer.cs` | IMGUI tracker rendering of the Cue Sheet: one row per Grid, up to 16 columns with a short row where a phrase ends, hollow pending marks and solid fired ones. |
| `Assets/Editor/Tuning/TransitionBarRenderer.cs` | Always-on fixed-height Live tab strip showing the running A-to-B Transition and its progress, or the on-air Effect at rest. Achromatic so it never borrows tracker plan colours. |

`CueSheet.cs` and the Live Timeline files have been removed. Track-scoped Cue Sheet visualization is the Grid tracker on the Tuning window's Live tab.

## Effects

### Direct tile effects

| Effect | Role |
| --- | --- |
| `Angles` | Tile-angle hue sweep where energy sets directional brightness depth and beat phase moves a hue front without changing brightness. |
| `AnimateLoops` | Animated packed shape-loop coloring over a background. |
| `ColorSparkle` | Fading sparkle field over persistent buffer trails. |
| `Flock` | Boid simulation projected to tile positions. |
| `Julia` | Julia fractal evaluated directly at each Penrose tile center. |
| `MazeFlyer` | First-person flight through a randomized voxel maze, ray-traced directly into the tile buffer; includes Standalone and Sync Settings assets. |
| `Nibbler` | Random neighbor walkers that paint fading trails. |
| `Noise` | Palette-based Perlin tile shader with beat distortion modes. |
| `NoiseTunnel` | Radius/diagonal tunnel bands from tile positions. |
| `Pulse` | Two-color ping-pong fill based on tile type. |
| `TileShapes` | Random packed Penrose shape-list flashes. |
| `Tunnel` | Direct tile-space tunnel from tile radius, tile-index phase, and a mode-specific cycle phase. |
| `Vortex` | Spinner-driven nearest-source palette field. |
| `Lightning` | Branching stochastic paths from center-star shapes. |

### Screen-space effects

| Effect | Role |
| --- | --- |
| `MetaBalls` | Screen-space metaball field mapped to tiles. |
| `Petals` | Shape-list coloring; inherits `ScreenEffect` but writes tile buffer directly. |
| `RainbowBars` | Directional screen-space palette bars mapped to tiles. |
| `Ripple` | Expanding screen-space ripple rings mapped to tiles. |
| `Waterfall` | Falling screen-space droplets over a palette background. |
| `Fluid` | Tile-neighbor diffusion simulation; inherits `ScreenEffect` but writes tile buffer directly. |
| `Kscope` | Texture kaleidoscope/image scroller using StreamingAssets images and mirror groups. |

### Mixers and wrappers

| Effect | Role |
| --- | --- |
| `Mirror` | Wraps one child effect and mirrors it through Penrose mirror groups. |
| `NoiseMixer` | Mixes two child effects using Perlin noise. |
| `RandomEffectsMixer` | Additively mixes two or three child effects. |
| `ShapeGlitch` | Child effect plus blinking/fading packed shape overlays. |
| `YinYangMixer` | Two child effects split into rotating angular regions. |

### Template

| File | Role |
| --- | --- |
| `EmptyEffect` | Copy/rename starter effect. Marked `[RuntimeCatalogIgnore]`, so it is never included in the runtime catalog. |

## Transitions

| Transition | Role |
| --- | --- |
| `Fade` | Linear crossfade; also supports `[ratio]` external blending. |
| `EmptyTransition` | Copy/rename starter transition. Marked `[RuntimeCatalogIgnore]`, so it is never included in the runtime catalog. |
| `IndexWipe` | Raw tile-index wipe; also supports `[ratio]` external blending. |
| `FizzleTransition` | Fixed shuffled reveal order; also supports `[ratio]` external blending. |
| `DirectionalWipe` | Angle-based geometry wipe; also supports `[ratio] [angle]` external blending. |
| `IrisTransition` | Radial iris transition; also supports `[ratio] [direction]` external blending. |
| `NoiseTransition` | Perlin threshold transition with border color; also supports `[ratio] [borderHue]` external blending. |
| `RGBFade` | Staggered per-channel fade. |

## External blenders

| Blender | Role |
| --- | --- |
| `RGBBlender` | Per-channel mix between native and external buffers. Requires three settings. |
| `SilhouetteBlender` | Treats exact black external pixels as transparent and blends non-black pixels over native output. |
