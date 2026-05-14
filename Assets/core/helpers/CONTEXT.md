# Helper Architecture

This directory contains core architectural helpers for the Penrose Simulator.

### Key Files:
- **SerialOut.cs**: Manages serial communication from Unity to ESP32, including port discovery, data buffering, and multi-threading for high-speed pixel output.
- **main.cpp**: The ESP32 firmware responsible for receiving serial data and driving the FastLED strips.
- **ScreenEffect.cs**: The 2D-to-Penrose mapping engine.

For a full system overview, please refer to the primary CONTEXT.md in the project root.
