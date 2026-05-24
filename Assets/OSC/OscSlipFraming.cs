// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Buffers.Binary;

namespace RaveSystem.Osc {

/// <summary>
///     OSC 1.1 SLIP framing helpers for stream transports such as TCP, serial, and WebSocket
///     adapters that need explicit packet boundaries.
/// </summary>
/// <remarks>
///     OSC datagram transports such as UDP carry packet length out of band and do not need this
///     framing. Stream transports encode each OSC packet between SLIP END bytes and escape literal
///     END and ESC bytes inside the packet payload.
/// </remarks>
public static class OscSlipFraming {
    /// <summary>SLIP END delimiter byte (<c>0xC0</c>), written before and after each encoded OSC packet.</summary>
    public const byte End = 0xC0;

    /// <summary>SLIP escape introducer byte (<c>0xDB</c>), used before escaped END and ESC bytes.</summary>
    public const byte Esc = 0xDB;

    /// <summary>SLIP escaped-END replacement byte (<c>0xDC</c>), following <see cref="Esc" /> for a literal END payload byte.</summary>
    public const byte EscEnd = 0xDC;

    /// <summary>SLIP escaped-ESC replacement byte (<c>0xDD</c>), following <see cref="Esc" /> for a literal ESC payload byte.</summary>
    public const byte EscEsc = 0xDD;

    /// <summary>Returns the number of bytes required to SLIP-frame <paramref name="packet" />.</summary>
    public static int GetEncodedLength(ReadOnlySpan<byte> packet) {
        var length = 2L;
        foreach (var b in packet) {
            length += b is End or Esc ? 2 : 1;
            if (length > int.MaxValue) {
                throw new OscFormatException("OSC SLIP encoded packet length exceeds int range");
            }
        }
        return (int)length;
    }

    /// <summary>
    ///     Attempts to SLIP-frame <paramref name="packet" /> into <paramref name="destination" />.
    /// </summary>
    /// <param name="packet">The raw OSC packet bytes to frame.</param>
    /// <param name="destination">The destination span that receives the framed packet.</param>
    /// <param name="bytesWritten">The number of bytes written when the method returns <see langword="true" />; otherwise zero.</param>
    /// <param name="requiredLength">The destination length required to encode the complete frame.</param>
    /// <returns><see langword="true" /> when the frame fit in <paramref name="destination" />; otherwise <see langword="false" />.</returns>
    public static bool TryEncodePacket(
        ReadOnlySpan<byte> packet,
        Span<byte> destination,
        out int bytesWritten,
        out int requiredLength) {
        requiredLength = GetEncodedLength(packet);
        if (destination.Length < requiredLength) {
            bytesWritten = 0;
            return false;
        }

        var pos = 0;
        destination[pos++] = End;
        foreach (var b in packet) {
            switch (b) {
                case End:
                    destination[pos++] = Esc;
                    destination[pos++] = EscEnd;
                    break;
                case Esc:
                    destination[pos++] = Esc;
                    destination[pos++] = EscEsc;
                    break;
                default:
                    destination[pos++] = b;
                    break;
            }
        }
        destination[pos++] = End;
        bytesWritten = pos;
        return true;
    }

    /// <summary>
    ///     SLIP-frames <paramref name="packet" /> into <paramref name="destination" /> and returns
    ///     the number of bytes written.
    /// </summary>
    /// <exception cref="OscFormatException">Thrown when <paramref name="destination" /> is too small for the encoded frame.</exception>
    public static int EncodePacket(ReadOnlySpan<byte> packet, Span<byte> destination) {
        if (TryEncodePacket(packet, destination, out var written, out var requiredLength)) {
            return written;
        }
        throw new OscFormatException(
            $"OSC SLIP destination too small: need {requiredLength} bytes, have {destination.Length}");
    }

    /// <summary>
    ///     Decodes one complete SLIP frame from <paramref name="frame" /> into
    ///     <paramref name="destination" /> and returns the raw OSC packet byte count.
    /// </summary>
    /// <exception cref="OscFormatException">
    ///     Thrown when the frame is missing END delimiters, contains an invalid escape sequence,
    ///     or does not fit in <paramref name="destination" />.
    /// </exception>
    public static int DecodePacket(ReadOnlySpan<byte> frame, Span<byte> destination) {
        if (frame.Length < 2 || frame[0] != End || frame[^1] != End) {
            throw new OscFormatException("OSC SLIP frame must start and end with END bytes");
        }

        var pos = 0;
        var i = 1;
        while (i < frame.Length - 1) {
            var b = frame[i++];
            if (b == End) {
                throw new OscFormatException("OSC SLIP frame contains an unexpected END byte before the trailing delimiter");
            }

            if (b == Esc) {
                if (i >= frame.Length - 1) {
                    throw new OscFormatException("OSC SLIP escape sequence is truncated");
                }
                b = frame[i++] switch {
                    EscEnd => End,
                    EscEsc => Esc,
                    var escaped => throw new OscFormatException(
                        $"OSC SLIP escape sequence contains invalid byte 0x{escaped:X2}"),
                };
            }

            if (pos >= destination.Length) {
                throw new OscFormatException(
                    $"OSC SLIP decoded packet exceeds destination length {destination.Length}");
            }
            destination[pos++] = b;
        }
        return pos;
    }
}

}
