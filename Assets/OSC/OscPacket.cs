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
///     Top-level classification of an OSC 1.0 packet: a <see cref="Message" /> begins with
///     <c>'/'</c>; a <see cref="Bundle" /> begins with the literal bytes <c>"#bundle\0"</c>.
///     Anything else is invalid and rejected by <see cref="OscPacket.Classify" />.
/// </summary>
public enum OscPacketKind : byte {
    /// <summary>The packet is an OSC message: <c>address + type-tag-string + arguments</c>.</summary>
    Message = 1,

    /// <summary>The packet is an OSC bundle: <c>"#bundle\0" + timetag + (size + element)*</c>.</summary>
    Bundle = 2,
}

/// <summary>
///     Top-level helpers for classifying an OSC packet. The OSC 1.0 spec distinguishes a packet's
///     kind by its leading byte: <c>'/'</c> for messages, <c>'#'</c> for bundles. A receiver
///     typically calls <see cref="Classify" /> first, then constructs either an <see cref="OscReader" />
///     or an <see cref="OscBundleReader" /> over the same span.
/// </summary>
public static class OscPacket {
    /// <summary>Returns the packet kind for <paramref name="packet" />, or throws <see cref="OscFormatException" /> if it is malformed.</summary>
    public static OscPacketKind Classify(ReadOnlySpan<byte> packet) {
        if (packet.IsEmpty) {
            throw new OscFormatException("OSC packet is empty");
        }

        var leadByte = packet[0];
        if (leadByte == (byte)'/') {
            return OscPacketKind.Message;
        }

        if (leadByte == (byte)'#') {
            if (packet.Length < 8 || !packet[..8].SequenceEqual(OscBundleWriter.Preamble)) {
                throw new OscFormatException("OSC packet starts with '#' but is not a valid \"#bundle\" preamble");
            }
            return OscPacketKind.Bundle;
        }

        throw new OscFormatException(
            $"OSC packet must start with '/' (message) or '#' (bundle), got 0x{leadByte:X2}");
    }

    /// <summary>Returns <see langword="true" /> and sets <paramref name="kind" /> if the packet is well-formed at the top level; <see langword="false" /> otherwise.</summary>
    public static bool TryClassify(ReadOnlySpan<byte> packet, out OscPacketKind kind) {
        if (packet.IsEmpty) {
            kind = default;
            return false;
        }

        var leadByte = packet[0];
        if (leadByte == (byte)'/') {
            kind = OscPacketKind.Message;
            return true;
        }

        if (leadByte == (byte)'#' && packet.Length >= 8 && packet[..8].SequenceEqual(OscBundleWriter.Preamble)) {
            kind = OscPacketKind.Bundle;
            return true;
        }

        kind = default;
        return false;
    }
}

}
