<!--
Copyright © 2026 Hunter Luisi. All rights reserved.
Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.
-->

# RaveSystem.Osc

A general-purpose, allocation-conscious .NET implementation of OSC 1.1, using the [OSC 1.0 specification](https://opensoundcontrol.stanford.edu/spec-1_0.html), the official [OSC 1.1 note](https://opensoundcontrol.stanford.edu/spec-1_1.html), and the [OSC 1.1 NIME paper](https://ccrma.stanford.edu/groups/osc/files/2009-NIME-OSC-1.1.pdf) as the standards sources.

`RaveSystem.Osc` is a self-contained library. It has no references to RaveSystem domain types and is intended to be reusable by any .NET consumer that needs to send, receive, encode, decode, or dispatch Open Sound Control messages.

## Status

Feature-complete for the OSC 1.1 core behaviors implemented in this library. All public types are in place. Test suite covers the OSC example bytes verbatim plus per-tag round-trip encode/decode and OSC 1.1 path/stream additions.

## Standards target

- **OSC 1.1 core**: messages, bundles, type tags, timetags, `//` path-traversing patterns, required support for `T F N I t`, Impulse/bang naming for `I`, and SLIP packet framing for stream transports.
- **OSCQuery**: separate HTTP/JSON/WebSocket discovery protocol; not part of the core wire encoder/decoder.

## Implements

- **Wire format**: messages and bundles per the OSC specifications.
- **OSC core type tags**: `i f s b h d t T F N I m c r S`, plus the `[ ]` array delimiters for nested arguments.
- **Bundles**: `#bundle` envelope with NTP time tags; nested bundles supported.
- **NTP time tags**: 64-bit fixed-point, 1900-01-01 UTC epoch, integer-only conversion (no floating-point drift).
- **Address validation**: literal-address checks (sender-registered handler addresses) and pattern-address checks (sender-on-the-wire addresses).
- **Address pattern matching**: full OSC wildcard support (`?`, `*`, `//`, `[abc]`, `[a-z]`, `[!abc]`, `{foo,bar}`), including the spec edge cases (backwards ranges as literal sets, `!` only at start, trailing/leading `-` literal).
- **Address-space dispatch**: handler registration and routing for receiver-side use, with bundle decomposition and timetag forwarding.
- **UDP transport**: sender and receiver, broadcast-capable, allocation-free per send/receive after warm-up.
- **SLIP stream framing**: OSC 1.1 packet framing helpers for TCP, serial, WebSocket, or other stream adapters.

## Not implemented yet

- **OSCQuery**. A separate spec layered beside OSC for HTTP/JSON address-space discovery and optional WebSocket streaming.
- **First-class TCP/serial/WebSocket transports**. `OscSlipFraming` provides the packet framing primitive; actual stream socket adapters are additive transport work.
- **OSC 1.1 optional type reservations beyond the concrete tags in this library**. The 1.1 paper references additional recommended optional types but does not publish the full table in the paper text; implement only from authoritative sources.

## Quick start

### Send an OSC message over UDP

```csharp
using System.Net;
using RaveSystem.Osc;

var buffer = new byte[256];
var writer = new OscWriter(buffer);
writer.WriteAddress("/oscillator/4/frequency");
writer.WriteFloat32(440.0f);
var written = writer.Finish();

using var sender = new OscUdpSender(new IPEndPoint(IPAddress.Loopback, 9000));
sender.Send(buffer.AsSpan(0, written));
```

### Receive and dispatch incoming messages

```csharp
using System.Net;
using RaveSystem.Osc;

var dispatcher = new OscDispatcher();
dispatcher.Register("/oscillator/*/frequency", (ReadOnlySpan<byte> address, ref OscReader reader, OscTimeTag _t) =>
{
    reader.MoveNext();
    var hz = reader.ReadFloat32();
    Console.WriteLine($"{System.Text.Encoding.ASCII.GetString(address)} -> {hz} Hz");
});

using var receiver = new OscUdpSocket(new IPEndPoint(IPAddress.Any, 9000));
receiver.PacketReceived += (packet, _sender) => dispatcher.Dispatch(packet);
receiver.Start();

await Task.Delay(Timeout.Infinite);
```

### Build a bundle with two messages

```csharp
var buffer = new byte[1024];
var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);

var slot1 = bundle.BeginElement();
var msg1 = new OscWriter(slot1);
msg1.WriteAddress("/voice/1/note");
msg1.WriteInt32(60);
bundle.EndElement(msg1.Finish());

var slot2 = bundle.BeginElement();
var msg2 = new OscWriter(slot2);
msg2.WriteAddress("/voice/2/note");
msg2.WriteInt32(64);
bundle.EndElement(msg2.Finish());

var bundleBytes = bundle.Finish();
sender.Send(buffer.AsSpan(0, bundleBytes));
```

### Pattern-match an address

```csharp
OscAddressPattern.Matches("/voice/[1-2]/*", "/voice/1/note");   // true
OscAddressPattern.Matches("/voice/{a,b}/x",  "/voice/c/x");     // false
OscAddressPattern.Matches("/foo/*",          "/foo/bar/baz");   // false (* doesn't cross /)
```

## Public exception hierarchy

OSC wire-format, address, writer-state, and reader-state failures surface through `OscException` (or a subtype). Catch the base type to handle OSC data and API failures as a single concept, or catch a specific subtype:

- `OscFormatException` — wire-format defect (truncated bytes, missing terminator, bad alignment, blob length out of range, etc.).
- `OscAddressException` — invalid address or pattern (missing leading `/`, reserved character, unbalanced brackets, etc.).
- `OscWriterStateException` — writer used out of order. DEBUG-only; release builds elide the state-machine checks.
- `OscReaderStateException` — reader used out of order: read before `ReadAddress`, read before `MoveNext`, read twice without an intervening `MoveNext`, or read after `MoveNext` returned `false`. Raised in all build configurations.

## Unity port notes

This copy targets PenroseArt's Unity runtime (`netstandard2.1`, C# 9). It keeps the RaveSystem OSC API and wire-format behavior, but a few upstream implementation details were downgraded from modern .NET so Unity can compile it:

- **`OscWriter` and `OscReader`** remain `ref struct`s. Writer tag-string scratch storage uses a small managed byte array instead of C# 12 `[InlineArray]`.
- **String encoding** still uses a single static strict-ASCII `Encoding` instance configured with `EncoderFallback.ExceptionFallback` so non-ASCII input throws `OscFormatException` rather than silently substituting `?`.
- **Address validation** uses a simple string lookup for reserved characters instead of .NET 8 `SearchValues<char>`.
- **`OscDispatcher`** keeps the immutable-snapshot registration pattern (`volatile Registration[]`) but uses `object` locks and `System.Threading.Timer` instead of `Lock`, `TimeProvider`, and `ITimer`.
- **`OscUdpSender` and `OscUdpSocket`** use socket APIs available to Unity's .NET Standard profile. They may allocate around send/reply where upstream .NET 8+ uses span-based socket overloads.
- **Address pattern matcher byte overload** still stack-allocates up to 1024 chars and falls back to `ArrayPool<char>.Shared` for pathologically long patterns.

## License

Part of RaveSystem. Attribution to upstream references where applicable: TinyOSC (ISC, Martin Roth) and OscCore (MIT, Tilde Love Project) for clean-room translation references; FastOSC (LGPL-3.0, VolcanicArts) and liblo (LGPL-2.1, Steve Harris et al.) consulted for spec disambiguation only (no copied code).
