# Helper Architecture

This directory contains core architectural helpers for the Penrose Simulator, optimized for high-performance physical output.

### Key Files:
- **SerialOut.cs**: High-speed serial pipeline. Uses dedicated I/O threads, 64KB OS-level buffers, and a 2,000,000 Baud configuration (matching 12Mbps USB-CDC speeds) to achieve a consistent 60 FPS for up to 1800 pixels.
- **main.cpp**: ESP32-S2 (Lolin S2 Mini) firmware. Utilizes hardware-accelerated RMT channels for parallel LED output and non-blocking bulk serial reads to minimize frame latency (~4.1ms serial RX, ~8.4ms LED TX per 900-pixel board).
- **ScreenEffect.cs**: The 2D-to-Penrose mapping engine.
- **Controller.cs**: Orchestrates the expansion of 900 animation tiles to 1800 physical LEDs via `sendSerialFrame`. It distributes data across multiple serial boards by aligning the physical `wires` map with the hardware-reported starting offsets.

### Performance Specifications:
- **Target Framerate**: 60 FPS (16.6ms total window).
- **Bandwidth**: ~1.6 Mbps raw data per board.
- **Pixel Mapping**: 1:2 expansion (900 logical tiles to 1800 physical LEDs).
- **Hardware**: Distributed output across dual ESP32-S2 controllers using native USB-CDC.

For a full system overview, please refer to the primary CONTEXT.md in the project root.
