# Penrose S2 Mini Firmware Protocol v1.0

## Overview
This document defines the communication protocol between the Penrose Simulator (C#) and the S2 Mini (ESP32-S2) distributed hardware controllers. The system uses USB-Serial to manage multiple boards, each responsible for specific segments of the Penrose Wall.

## Connection Settings
- **Baud Rate:** 2000000
- **Data Bits:** 8
- **Parity:** None
- **Stop Bits:** 1
- **Note:** The ESP32-S2 typically reboots upon Serial connection. The PC side includes a 200ms delay to allow the bootloader to finish before starting the handshake.

## Command Set

### 1. Handshake / Query ('?' - 0x3F)
Sent by the PC to discover what pixels this board controls.

**PC Sends:** 
`0x3F` (1 byte)

**Arduino Response:**
- `[1 byte]`: Board Type (0x01 = LED Driver)
- `[1 byte]`: Payload Size (N)
- `[2 bytes]`: Start Index (Big-Endian/MSB first).
- `[2 bytes]`: Pixel Count (Big-Endian/MSB first).

*Example:* An LED board handling pixels 900-1440 would respond: `0x01 0x04 0x03 0x84 0x02 0x1C` (where 0x04 is the payload size).

---

### 2. Data Packet ('D' - 0x44)
Sent by the PC to update the color buffer.

**PC Sends:**
- `0x44` (1 byte)
- `[2 bytes]`: Start Index
- `[2 bytes]`: Pixel Count
- `[Count * 3 bytes]`: Raw RGB data (R-G-B order).

**Arduino Action:**
Store these bytes in the local `CRGB` array and immediately call `FastLED.show()`.

## Arduino Implementation Strategy

### Initialization
Use `Serial.begin(230400)`. Since the ESP32-S2 uses native USB-CDC, the baud rate is largely ignored by the hardware, but keeping it at 230400 ensures compatibility with the C# `SerialPort` configuration.

### Main Loop
1. Poll `Serial.available()`.
2. Read the command byte.
3. Use a `switch` statement to route to:
    - `handleQuery()`: Write the hardcoded segment map.
    - `handleData()`: Read exactly `5 + (count * 3)` bytes, copy to `leds`, then call `FastLED.show()`.

### Buffer Management
Because the S2 Mini has ample RAM, it is recommended to allocate a `CRGB` array large enough to hold all pixels for its assigned segments plus a small serial RX buffer to prevent overflows during high-speed transfers.

## Error Handling
- If the Arduino receives an unknown command byte, it should flush the serial buffer to re-sync.
- The PC side implements a 1-second timeout. If the Arduino hangs during a read, the PC will mark the port as "ignored" and continue running the rest of the wall.

## Mapping Responsibility
The Arduino "owns" the mapping. If you want to change which part of the wall a board controls, simply update the start/count values in the Arduino sketch. The Penrose Simulator will automatically adjust its output the next time it connects.