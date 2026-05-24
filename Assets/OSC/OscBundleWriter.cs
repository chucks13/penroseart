// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace RaveSystem.Osc {

/// <summary>
///     Span-based OSC 1.0 bundle encoder. A bundle is the literal ASCII string <c>"#bundle"</c>
///     followed by a null terminator and 4-byte alignment padding (8 bytes total), a 64-bit NTP
///     timetag (8 bytes), and zero or more <em>elements</em>: 32-bit big-endian element size
///     followed by that many element bytes. Each element is either an OSC message or a nested
///     OSC bundle.
/// </summary>
/// <remarks>
///     Lifecycle:
///     <list type="number">
///         <item>Construct with a destination <see cref="Span{T}" /> and the bundle's <see cref="OscTimeTag" />.</item>
///         <item>For each element, call <see cref="BeginElement" /> to obtain a sub-span, write the message into that sub-span (typically using <see cref="OscWriter" />), then call <see cref="EndElement" /> with the byte count.</item>
///         <item>Call <see cref="Finish" /> to return the total byte length.</item>
///     </list>
///     Nested bundles are supported: the inner element is itself written with another
///     <see cref="OscBundleWriter" /> over the sub-span returned by <see cref="BeginElement" />.
/// </remarks>
public ref struct OscBundleWriter {
    /// <summary>The bundle preamble bytes (<c>"#bundle\0"</c>), 8 bytes including the null padding.</summary>
    private static readonly byte[] PreambleBytes = Encoding.ASCII.GetBytes("#bundle\0");

    public static ReadOnlySpan<byte> Preamble => PreambleBytes;

    private readonly Span<byte> _destination;
    private int _activeElementSizeOffset;
    private BundleState _state;

    /// <summary>Creates a bundle writer that emits OSC bytes into <paramref name="destination" /> with the given <paramref name="timeTag" />.</summary>
    public OscBundleWriter(Span<byte> destination, OscTimeTag timeTag) {
        _destination = destination;
        BytesWritten = 0;
        _activeElementSizeOffset = -1;
        _state = BundleState.AcceptingElements;

        EnsureCapacityDebug(16);
        Preamble.CopyTo(_destination);
        BinaryPrimitives.WriteUInt64BigEndian(_destination[8..], timeTag.Value);
        BytesWritten = 16;
    }

    /// <summary>The number of bytes written so far. After <see cref="Finish" />, this is the encoded bundle length.</summary>
    public int BytesWritten { get; private set; }

    /// <summary>
    ///     Reserves space for an element size prefix and returns a writable sub-span where the
    ///     caller can encode the element's bytes (a message or a nested bundle). Pair with
    ///     <see cref="EndElement" /> to finalize the element.
    /// </summary>
    public Span<byte> BeginElement() {
        EnsureState(BundleState.AcceptingElements, "BeginElement");
        EnsureCapacityDebug(4);

        _activeElementSizeOffset = BytesWritten;
        BinaryPrimitives.WriteInt32BigEndian(_destination[BytesWritten..], 0);
        BytesWritten += 4;

        _state = BundleState.WritingElement;
        return _destination[BytesWritten..];
    }

    /// <summary>Patches the size of the element opened by <see cref="BeginElement" /> and advances past it.</summary>
    /// <param name="elementByteCount">The number of bytes the caller wrote into the sub-span returned by <see cref="BeginElement" />.</param>
    public void EndElement(int elementByteCount) {
        EnsureState(BundleState.WritingElement, "EndElement");

        if (elementByteCount < 0) {
            throw new OscFormatException($"OSC bundle element size must be non-negative (got {elementByteCount})");
        }

        if ((elementByteCount & 3) != 0) {
            throw new OscFormatException(
                $"OSC bundle element size must be 4-byte aligned (got {elementByteCount})");
        }

        if (BytesWritten + elementByteCount > _destination.Length) {
            throw new OscFormatException(
                $"OSC bundle element size {elementByteCount} exceeds destination capacity {_destination.Length - BytesWritten}");
        }

        BinaryPrimitives.WriteInt32BigEndian(
            _destination.Slice(_activeElementSizeOffset, 4),
            elementByteCount);
        BytesWritten += elementByteCount;
        _activeElementSizeOffset = -1;

        _state = BundleState.AcceptingElements;
    }

    /// <summary>Closes the bundle and returns the total encoded byte length.</summary>
    public int Finish() {
        EnsureState(BundleState.AcceptingElements, "Finish");
        _state = BundleState.Sealed;
        return BytesWritten;
    }

    [Conditional("DEBUG")]
    private readonly void EnsureState(BundleState expected, string operation) {
        if (_state != expected) {
            throw new OscWriterStateException(
                $"OscBundleWriter operation '{operation}' requires state {expected} but writer is in state {_state}");
        }
    }

    [Conditional("DEBUG")]
    private readonly void EnsureCapacityDebug(int additional) {
        if (BytesWritten + additional > _destination.Length) {
            throw new OscFormatException(
                $"OscBundleWriter destination too small: need {BytesWritten + additional} bytes, have {_destination.Length}");
        }
    }

    private enum BundleState : byte {
        AcceptingElements = 0,
        WritingElement = 1,
        Sealed = 2,
    }
}

}
