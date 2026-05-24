<!--
Copyright © 2026 Hunter Luisi. All rights reserved.
Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.
-->

# RaveSystem.Osc

A general-purpose, allocation-conscious .NET implementation of [OSC 1.0](https://opensoundcontrol.stanford.edu/spec-1_0.html).

`RaveSystem.Osc` is a self-contained library. It has no references to RaveSystem domain types and is intended to be reusable by any .NET consumer that needs to send, receive, encode, decode, or dispatch Open Sound Control messages.

## Status

Feature-complete against the OSC 1.0 specification. All public types are in place. Test suite covers the spec example bytes verbatim plus per-tag round-trip encode/decode.

## Implements (full OSC 1.0)

- **Wire format**: messages and bundles per the OSC 1.0 specification.
- **All 14 OSC 1.0 type tags**: `i f s b h d t T F N I m c r S`, plus the `[ ]` array delimiters for nested arguments.
- **Bundles**: `#bundle` envelope with NTP time tags; nested bundles supported.
- **NTP time tags**: 64-bit fixed-point, 1900-01-01 UTC epoch, integer-only conversion (no floating-point drift).
- **Address validation**: literal-address checks (sender-registered handler addresses) and pattern-address checks (sender-on-the-wire addresses).
- **Address pattern matching**: full OSC 1.0 wildcard support (`?`, `*`, `[abc]`, `[a-z]`, `[!abc]`, `{foo,bar}`), including the spec edge cases (backwards ranges as literal sets, `!` only at start, trailing/leading `-` literal).
- **Address-space dispatch**: handler registration and routing for receiver-side use, with bundle decomposition and timetag forwarding.
- **UDP transport**: sender and receiver, broadcast-capable, allocation-free per send/receive after warm-up.

## Does NOT implement (out of OSC 1.0 scope)

- **OSC 1.1 features**. The 1.1 NIME 2009 proposal introduces `//` xpath patterns, payload-bearing `T`/`F` arguments, and deprecates `I`/`N`. It is a separate proposal, not a successor.
- **OSCQuery**. A separate spec layered on top of OSC; belongs in its own library.
- **TCP-OSC framing**. SLIP framing for stream transport is not part of OSC 1.0; it can be added later as an additional transport without breaking the UDP API.

These omissions are intentional and not a deferred backlog. They live outside the OSC 1.0 specification.

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

## Performance notes

The encoder, decoder, pattern matcher, and dispatch path are designed to be allocation-free per call after one-time setup. Specifically:

- **`OscWriter` and `OscReader`** are `ref struct`s. They live entirely on the stack and never escape to the heap. Tag-string buffering uses C# 12 `[InlineArray]` on the writer for the common case; callers can provide a larger scratch span to the `OscWriter` overload for messages with more than 60 type tags.
- **String encoding** uses a single static strict-ASCII `Encoding` instance configured with `EncoderFallback.ExceptionFallback` so non-ASCII input throws `OscFormatException` rather than silently substituting `?`.
- **Address validation** uses `SearchValues<char>` (.NET 8+) for the reserved-character set so the JIT can vectorize the lookup.
- **`OscDispatcher`** uses an immutable-snapshot pattern (`volatile Registration[]`) so dispatch reads the registration list without locking and without per-dispatch allocation; only `Register`/`Unregister` allocate (a new array under a lock).
- **`OscUdpSender`** caches its destination as a `SocketAddress` and uses the .NET 8+ `Socket.SendTo(ReadOnlySpan<byte>, SocketFlags, SocketAddress)` overload, the documented allocation-free send path. `Send` is `[MethodImpl(AggressiveInlining)]`.
- **`OscUdpSocket`** uses a single 64 KB buffer and a single `SocketAddress` reused across receives via the `Socket.ReceiveFromAsync(Memory<byte>, SocketFlags, SocketAddress, CancellationToken)` overload. `ValueTask<int>` avoids the `Task` allocation when the receive completes synchronously.
- **Address pattern matcher byte overload** stack-allocates up to 1024 chars and falls back to `ArrayPool<char>.Shared` for pathologically long patterns, so the common path never touches the heap and the rare large-pattern path uses a pooled buffer.

This makes the library suitable for tight-loop broadcasters (e.g., 30 Hz device-state projection) without GC pressure.

## License

Part of RaveSystem. Attribution to upstream references where applicable: TinyOSC (ISC, Martin Roth) and OscCore (MIT, Tilde Love Project) for clean-room translation references; FastOSC (LGPL-3.0, VolcanicArts) and liblo (LGPL-2.1, Steve Harris et al.) consulted for spec disambiguation only (no copied code).
