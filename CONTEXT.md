# Penrose Simulator Context & Architecture Guide

## Overview

This project is a real-time controller for the Penrose Wall light installation. It generates generative visuals for a 900-tile Penrose model in Unity and currently outputs to LED hardware through high-speed USB serial (`SerialOut`) for S2 Mini / ESP32 boards.

The older ACN/E1.31 UDP output path still exists in `Controller.sendUDPFrame()` / `sendACN()`, but the active build path is serial because `Assets/core/Controller.cs` file-defines `ENABLE_SERIAL`.

## Core Components

### 1. Controller.cs (The Singleton Hub)

Manages the main loop, effect switching, overlays, input, and hardware output.

- **Deck System**: Ensures variety by drawing effects/transitions from the top half of a rotating deck and moving selected entries to the bottom.
- **State Machine**: Alternates between a playing state (generative effects) and a transition state (blending between effects).
- **Timing**: Defaults to 10 seconds per effect with a 2 second transition.
- **Testing Override (Nova Technique)**: Allows locking selection to effects whose names match `forceEffectName`, toggled via the Inspector or the `Escape` key.

### 2. Beat Manager (Synchronization)

Provides a global heartbeat for the installation. The current implementation is a simulated/debug beat source; future versions may poll OSC or another live synchronization source.

- **BeatData**: Shared BPM/current-beat/timing state.
- **Variants**: Supports rhythmic personalities such as every beat, alternating beats, measure start, subdivisions, and syncopation. The current code and docs disagree on the numbering of variants 4/5/6; confirm intended behavior before changing it.
- **Rhythmic Logic**: Uses an x^4 decay curve to create sharp visual kicks without making off-beat visuals too dark.
- **Propagation**: Mixers can pass rhythm to children, let children choose independently, or suppress child pulsing.

### 3. Buffer and Effect System

The runtime works on `UnityEngine.Color[]` buffers sized to `Penrose.Total == 900`.

- **Effects**: Inherit from `EffectBase` and fill their local 900-color `buffer`.
- **ScreenEffects**: Render into a rectangular virtual screen and map that image onto the irregular Penrose tile layout through precomputed interpolation weights.
- **Mixers/Wrappers**: Inherit from `MixerBase`, own child effects, and combine or transform child buffers.
- **Transitions**: Inherit from `TransitionBase` and blend between two effect buffers by effect index.
- **Penrose.cs**: Holds the physical model, tile metadata, JSON data, mesh generation, and buffer-to-mesh color mapping.

### 4. Palette System (GPalette / AnimPalette)

A shared color-management and animation system.

- **Global Coordination**: `EffectBase.APalette` is static, so all effects share a cohesive palette state.
- **Runtime Control**: `Controller` updates palette animation and can trigger global palette shifts or reloads via the `Return` key.
- **Integration**: Effects query colors using normalized positions, allowing palette details to remain separate from generative logic.

### 5. Input and Output

- **Primary output**: USB serial via `SerialOut`, using `sendSerialFrame()` to expand the 900 logical Penrose tiles through `penrose.JsonRawData.wires` into the physical LED order.
- **Serial runtime support**: Standalone API compatibility is `.NET Standard 2.1`; desktop `System.IO.Ports` support is supplied by platform-specific plugin assets under `Assets/Plugins/System.IO.Ports/` for macOS, Windows, and Linux x64.
- **Fallback/legacy output**: ACN/E1.31 UDP code remains present in `sendUDPFrame()` / `sendACN()` and is used only when serial is not compiled in.
- **Control/input paths**: OSC (`OSCReader`), optional PixelReceiver blending, drum overlays, keyboard shortcuts, and optional telnet/debug paths.

## Build Symbols and Platform Notes

The project uses conditional compilation for optional output/control paths.

- `ENABLE_SERIAL`: Currently file-defined in `Controller.cs`, so serial is the active output path for the compiled controller.
- `ENABLE_TELNET`: Enables the remote command-line interface on port 23.
- `ENABLE_BLENDING`: Enables the `PixelReceiver` and dual-source frame blending logic.
- `PREP_CAPTURE`: Enables localhost pixel feedback/capture helper behavior.

Android, iOS, and WebGL serial support are not covered by the desktop `System.IO.Ports` plugin setup. If those become production targets, they need either serial-disabled builds or a platform-specific transport.

## Operational Logic

1. **Initialization**: `Controller` initializes `Penrose`, discovers effects/transitions/blenders through `Factory<T>`, configures UI fields, starts OSC/control helpers, and initializes serial output when enabled.
2. **Loop**: The active effect or transition draws into a 900-color buffer; overlays/blenders can modify it.
3. **Output**: The active serial path maps the Penrose buffer to physical LED order and sends frames through `SerialOut`; the legacy UDP path maps the same data into ACN/E1.31 universes.
4. **Scene update**: `Penrose.UpdateModelColors()` applies the current buffer to the Unity mesh for visualization.
