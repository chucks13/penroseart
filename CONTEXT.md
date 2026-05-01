# Penrose Simulator Context & Architecture Guide

## Overview
This project is a real-time controller for the Penrose Wall light installation. It generates generative visuals and outputs them via the ACN (E1.31) protocol to LED hardware.

## Core Components

### 1. Controller.cs (The Singleton Hub)
Manages the main loop, effect switching, and hardware output.
- **Deck System**: Ensures variety by drawing effects from the top half of a randomized array and moving them to the bottom.
- **State Machine**: Alternates between a "Playing" state (generative effects) and a "Transition" state (blending between effects).
- **Timing**: Defaults to 10s per effect with a 2s transition.
- **Testing Override (Nova Technique)**: Allows locking selection to a specific effect by name for debugging, toggled via the Inspector or the `Escape` key.

### 2. The Buffer System
The system works on a 1D array of `UnityEngine.Color`. 
- **Effects**: Inherit from `EffectBase`. They fill their local `buffer`.
- **ScreenEffects**: A middleware "Lens" for 2D visuals. It provides an abstract layer that maps a virtual 2D grid onto the 1D Penrose layout using static interpolation weights for performance.
- **Mixers/Wrappers**: Inherit from `MixerBase`. They manipulate child effects and use type-checking to ensure they only pick generative children.
- **Transitions**: Inherit from `TransitionBase`. They blend between two `EffectBase` buffers.
- **Penrose.cs**: Holds the physical mapping. The `wires` array maps the 1D buffer to DMX universes.

### 3. Reactive Systems
- **Dance.cs**: "Warp" timing logic. It detects beats and manipulates `deltaTime` to create rhythmic visual motion (speeding up and slowing down time).
- **OSC.cs**: Ingests beat triggers and remote control commands via Port 6969.

### 4. Input/Output
- **ACN/E1.31**: Standard DMX-over-Ethernet. Found in `sendACN`.
- **OSC**: Incoming remote control on port 6969. Handles brightness, effect selection, and beats.

## Build Symbols (Conditional Compilation)
The project uses Scripting Define Symbols to keep the core code clean and performant. 
- `ENABLE_TELNET`: Enables the remote command-line interface (Port 23).
- `ENABLE_BLENDING`: Enables the `PixelReceiver` and the dual-source frame blending logic.
- `PREP_CAPTURE`: Enables the localhost pixel feedback loop for external capture tools.

## Operational Logic
1. **Initialization**: The `Controller` instantiates all effects through a Factory and initializes the UDP hardware connection.
2. **Loop**: Every frame, the `activeEffect` draws to a 1D color array.
3. **Output**: The `sendUDPFrame` method iterates through the `wires` map in `Penrose.cs`, converting the 1D color buffer into DMX universes sent via ACN.
