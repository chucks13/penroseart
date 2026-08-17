# Penrose S2 Mini host protocol

> **Status:** This document describes the host contract that `SerialOut.cs` implements. The current build does not define `ENABLE_SERIAL`, so USB serial output is dormant. The current build uses UDP/E1.31 output. This repository does not contain device firmware. We did not verify firmware conformance.

## Connection

The host opens each candidate port with these settings:

- Baud rate: 2,000,000.
- Data bits: 8.

- Parity: none.
- Stop bits: 1.

The host also sets these port options:

- DTR and RTS: enabled.
- Read timeout: 500 ms during discovery.
- Write timeout: 1,000 ms.

After the port opens, the host waits 2 seconds for the ESP32-S2 boot process. It then discards received boot data and starts the query.

This protocol encodes all 16-bit values as unsigned, big-endian values. It sends the most-significant byte first.

## Commands

### Query: `?` (`0x3F`)

The host sends one byte:

```text
3F
```

The device must reply with:

```text
[board type: 1 byte] [payload size: 1 byte] [payload: N bytes]
```

For an LED driver, the response fields are:

- Board type: `0x01`.
- Payload size: `0x04`.
- Start index: 2 bytes.
- Pixel count: 2 bytes.

For example, a board that owns pixels 900 through 1439 replies:

```text
01 04 03 84 02 1C
```

The host rejects any board type other than `0x01`. It uses the returned start index and count to select pixels for that board.

### Data: `D` (`0x44`) and latch: `L` (`0x4C`)

For each frame, the host writes one contiguous packet:

```text
44 [start: 2 bytes] [count: 2 bytes] [RGB: count × 3 bytes] 4C
```

Each pixel is three bytes in red, green, blue order. The device must buffer the `D` payload. The final `L` command commits the buffered colors to the LEDs. The host does not wait for an acknowledgement after this frame packet.

### Synchronize: `S` (`0x53`)

The host recovery routine discards its input and output buffers, sends `S`, and waits up to 600 ms for one response byte:

- ACK: `0x06`.
- NACK: `0x15`.

Either response tells the host that the device command loop is alive. Any other response or a timeout fails recovery. `SerialOut` defines this recovery routine, but the current send path does not call it.

## Failure behavior

If discovery fails, the host closes the port and ignores it for 10 seconds before another attempt. A frame-write exception marks only that board unavailable. The other boards continue.

## Mapping responsibility

The device supplies its start index and pixel count in the query response. Change those values in the device firmware to change the owned range. On the next successful connection, the host uses the new range without a host-side mapping entry.
