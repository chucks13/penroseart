// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Buffers.Binary;

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

namespace RaveSystem.Osc {

/// <summary>
///     Span-based OSC 1.0 bundle decoder. Reads the <c>"#bundle\0"</c> preamble + 64-bit timetag
///     + zero or more <c>[int32 element_size][element bytes]</c> entries. Each element is either
///     an OSC message (call <see cref="OscReader" /> on the returned span) or a nested bundle
///     (recurse with another <see cref="OscBundleReader" />).
/// </summary>
public ref struct OscBundleReader {
    private readonly ReadOnlySpan<byte> _source;
    private int _position;

    /// <summary>The bundle's NTP timetag.</summary>
    public OscTimeTag TimeTag { get; }

    /// <summary>Creates a bundle reader over <paramref name="source" />, expected to start with <c>"#bundle\0"</c>.</summary>
    public OscBundleReader(ReadOnlySpan<byte> source) {
        if (source.Length < 16) {
            throw new OscFormatException(
                $"OSC bundle requires at least 16 bytes (preamble + timetag); got {source.Length}");
        }

        if (!source[..8].SequenceEqual(OscBundleWriter.Preamble)) {
            throw new OscFormatException("OSC bundle preamble must be \"#bundle\\0\"");
        }

        _source = source;
        TimeTag = new OscTimeTag(BinaryPrimitives.ReadUInt64BigEndian(source[8..16]));
        _position = 16;
    }

    /// <summary>True when there is at least one more element to read.</summary>
    public readonly bool HasMoreElements => _position < _source.Length;

    /// <summary>Reads the next element, returning its bytes as a span into the source buffer.</summary>
    /// <remarks>
    ///     The caller decides what the element is: if the first byte is <c>'/'</c> it is a message
    ///     (parse with <see cref="OscReader" />); if the first 8 bytes are <c>"#bundle\0"</c> it
    ///     is a nested bundle (recurse with another <see cref="OscBundleReader" />).
    /// </remarks>
    public ReadOnlySpan<byte> ReadNextElement() {
        if (!HasMoreElements) {
            throw new OscFormatException("OSC bundle has no more elements");
        }

        if (_position + 4 > _source.Length) {
            throw new OscFormatException("OSC bundle element size prefix is truncated");
        }

        var size = BinaryPrimitives.ReadInt32BigEndian(_source.Slice(_position, 4));
        if (size < 0) {
            throw new OscFormatException($"OSC bundle element size must be non-negative (got {size})");
        }
        if ((size & 3) != 0) {
            throw new OscFormatException($"OSC bundle element size must be 4-byte aligned (got {size})");
        }
        // (long) cast prevents overflow when size is near int.MaxValue and 4-byte aligned.
        if ((long)_position + 4 + size > _source.Length) {
            throw new OscFormatException(
                $"OSC bundle element of {size} bytes exceeds remaining {_source.Length - _position - 4} bytes");
        }

        var elementSpan = _source.Slice(_position + 4, size);
        _position += 4 + size;
        return elementSpan;
    }
}

}
