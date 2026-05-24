// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Buffers.Binary;
using System.Text;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace RaveSystem.Osc {

/// <summary>
///     Span-based OSC 1.0 message decoder. Parses the address, walks the type tag string,
///     and exposes one <c>ReadXxx</c> method per supported tag. Allocates only when explicitly
///     materializing strings or blobs as managed objects (see <see cref="ReadStringAlloc" />
///     and <see cref="ReadBlobAlloc" />); the span-returning forms allocate nothing.
/// </summary>
/// <remarks>
///     Lifecycle:
///     <list type="number">
///         <item>Construct with a source <see cref="ReadOnlySpan{T}" /> containing one OSC message.</item>
///         <item>Read the address with <see cref="ReadAddress" />.</item>
///         <item>Repeatedly call <see cref="MoveNext" />; for each <see langword="true" /> return,
///         call the <c>ReadXxx</c> matching <see cref="CurrentTag" />.</item>
///     </list>
///     The reader does no validation against an expected schema. The caller is responsible
///     for matching tags to <c>ReadXxx</c> calls. Mismatched calls either advance the cursor
///     incorrectly or throw <see cref="OscFormatException" /> on bounds violations.
/// </remarks>
public ref struct OscReader {
    private readonly ReadOnlySpan<byte> _source;
    private int _typeTagPos;
    private int _argumentsPos;
    private ReaderState _state;

    /// <summary>Creates a reader over <paramref name="source" />, expected to contain one OSC message.</summary>
    public OscReader(ReadOnlySpan<byte> source) {
        _source = source;
        Position = 0;
        _typeTagPos = -1;
        _argumentsPos = -1;
        CurrentTag = default;
        _state = ReaderState.AwaitingAddress;
    }

    /// <summary>The OSC type tag of the argument most recently positioned by <see cref="MoveNext" />.</summary>
    public OscToken CurrentTag { get; private set; }

    /// <summary>The current byte offset within the source.</summary>
    public int Position { get; private set; }

    /// <summary>Reads the OSC address and the type tag string, returning the address as a UTF-8 / ASCII span into the source buffer.</summary>
    /// <remarks>
    ///     The returned span is a slice of the source buffer (no allocation). It excludes the
    ///     null terminator and any padding bytes. After this call, <see cref="MoveNext" /> can
    ///     iterate the arguments.
    /// </remarks>
    public ReadOnlySpan<byte> ReadAddress() {
        if (_state != ReaderState.AwaitingAddress) {
            throw new OscFormatException("ReadAddress can only be called once, immediately after construction");
        }

        var addressEnd = FindNullTerminator(_source[Position..]);
        if (addressEnd < 0) {
            throw new OscFormatException("OSC address has no null terminator");
        }

        var addressBytes = _source.Slice(Position, addressEnd);
        if (addressBytes.IsEmpty || addressBytes[0] != (byte)'/') {
            throw new OscFormatException("OSC address must start with '/'");
        }
        EnsureAscii(addressBytes, "OSC address");

        // (long) cast prevents overflow when addressEnd is near int.MaxValue.
        var addressAdvance = ((long)addressEnd + 1L + 3L) & ~3L;
        if ((long)Position + addressAdvance > _source.Length) {
            throw new OscFormatException(
                $"OSC address alignment padding (advance {addressAdvance}) exceeds remaining buffer at position {Position}");
        }
        Position += (int)addressAdvance;

        // Type tag string starts with ',' and is itself null-terminated and 4-byte aligned.
        if (Position >= _source.Length || _source[Position] != (byte)',') {
            throw new OscFormatException("OSC message missing type tag string (expected leading ',')");
        }

        var typeTagStart = Position + 1;
        var tagEnd = FindNullTerminator(_source[typeTagStart..]);
        if (tagEnd < 0) {
            throw new OscFormatException("OSC type tag string has no null terminator");
        }

        _typeTagPos = typeTagStart;
        // (long) cast prevents overflow when tagEnd is near int.MaxValue.
        var tagAdvance = (1L + (long)tagEnd + 1L + 3L) & ~3L;
        if ((long)Position + tagAdvance > _source.Length) {
            throw new OscFormatException(
                $"OSC type tag string alignment padding (advance {tagAdvance}) exceeds remaining buffer at position {Position}");
        }
        _argumentsPos = Position + (int)tagAdvance;

        _state = ReaderState.ReadyToIterate;
        return addressBytes;
    }

    /// <summary>Advances to the next argument tag. Returns <see langword="false" /> when the type tag string is exhausted.</summary>
    /// <remarks>
    ///     Skip-safe: if the caller positioned on an argument by a previous <see cref="MoveNext" /> and
    ///     did not call a matching <c>ReadXxx</c>, the unread payload bytes are consumed so the next tag
    ///     advance lands on the correct argument. Tagless tokens (<c>T</c>, <c>F</c>, <c>N</c>, <c>I</c>,
    ///     <c>[</c>, <c>]</c>) carry no payload and therefore cost nothing to skip.
    /// </remarks>
    public bool MoveNext() {
        if (_state == ReaderState.AwaitingAddress) {
            throw new OscFormatException("MoveNext called before ReadAddress");
        }

        if (_state == ReaderState.PositionedAtArgument) {
            SkipCurrentArgumentPayload();
        }

        if (_typeTagPos >= _source.Length || _source[_typeTagPos] == 0) {
            _state = ReaderState.Exhausted;
            return false;
        }

        CurrentTag = (OscToken)_source[_typeTagPos++];
        _state = ReaderState.PositionedAtArgument;
        return true;
    }

    private void SkipCurrentArgumentPayload() {
        switch (CurrentTag) {
            case OscToken.I32:
            case OscToken.F32:
            case OscToken.Midi:
            case OscToken.Color:
            case OscToken.Ascii:
                AdvanceArgumentsPos(4);
                break;
            case OscToken.I64:
            case OscToken.F64:
            case OscToken.TimeTag:
                AdvanceArgumentsPos(8);
                break;
            case OscToken.Str:
            case OscToken.Symbol:
                var stringSpan = _source[_argumentsPos..];
                var nullPos = FindNullTerminator(stringSpan);
                if (nullPos < 0) {
                    throw new OscFormatException("OSC string argument has no null terminator");
                }
                // (long) cast prevents overflow when nullPos is near int.MaxValue.
                var stringAdvance = ((long)nullPos + 1L + 3L) & ~3L;
                AdvanceArgumentsPosChecked(stringAdvance);
                break;
            case OscToken.Blob:
                if (_argumentsPos + 4 > _source.Length) {
                    throw new OscFormatException("OSC blob length prefix is truncated");
                }
                var length = BinaryPrimitives.ReadInt32BigEndian(_source.Slice(_argumentsPos, 4));
                if (length < 0) {
                    throw new OscFormatException($"OSC blob length must be non-negative (got {length})");
                }
                // (long) cast prevents overflow when length is near int.MaxValue; the spec allows
                // values up to int.MaxValue but the skip advance is length + alignment + prefix.
                var blobAdvance = 4L + (((long)length + 3L) & ~3L);
                AdvanceArgumentsPosChecked(blobAdvance);
                break;
            // Tagless tokens carry no payload; nothing to advance past.
            case OscToken.True:
            case OscToken.False:
            case OscToken.Null:
            case OscToken.Infinitum:
            case OscToken.ArrayStart:
            case OscToken.ArrayEnd:
                break;
            default:
                throw new OscFormatException(
                    $"OSC type tag '{(char)CurrentTag}' (0x{(byte)CurrentTag:X2}) is not recognized");
        }
    }

    private void AdvanceArgumentsPos(int byteCount) {
        if (_argumentsPos + byteCount > _source.Length) {
            throw new OscFormatException(
                $"OSC argument of {byteCount} bytes (tag '{(char)CurrentTag}') exceeds source length {_source.Length} at position {_argumentsPos}");
        }
        _argumentsPos += byteCount;
    }

    private void AdvanceArgumentsPosChecked(long byteCount) {
        if ((long)_argumentsPos + byteCount > _source.Length) {
            throw new OscFormatException(
                $"OSC argument of {byteCount} bytes (tag '{(char)CurrentTag}') exceeds source length {_source.Length} at position {_argumentsPos}");
        }
        _argumentsPos += (int)byteCount;
    }

    /// <summary>Reads an int32 argument (tag <c>i</c>).</summary>
    public int ReadInt32() {
        EnsureArgumentReady(nameof(ReadInt32));
        var span = ConsumeArg(4);
        return BinaryPrimitives.ReadInt32BigEndian(span);
    }

    /// <summary>Reads an int64 argument (tag <c>h</c>).</summary>
    public long ReadInt64() {
        EnsureArgumentReady(nameof(ReadInt64));
        var span = ConsumeArg(8);
        return BinaryPrimitives.ReadInt64BigEndian(span);
    }

    /// <summary>Reads a float32 argument (tag <c>f</c>).</summary>
    public float ReadFloat32() {
        EnsureArgumentReady(nameof(ReadFloat32));
        var span = ConsumeArg(4);
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(span));
    }

    /// <summary>Reads a float64 argument (tag <c>d</c>).</summary>
    public double ReadFloat64() {
        EnsureArgumentReady(nameof(ReadFloat64));
        var span = ConsumeArg(8);
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(span));
    }

    /// <summary>Reads a string argument as a span into the source buffer (no allocation). Excludes the null terminator and padding.</summary>
    public ReadOnlySpan<byte> ReadStringSpan() {
        EnsureArgumentReady(nameof(ReadStringSpan));
        var bytes = _source[_argumentsPos..];
        var nullPos = FindNullTerminator(bytes);
        if (nullPos < 0) {
            throw new OscFormatException("OSC string argument has no null terminator");
        }

        var slice = bytes[..nullPos];
        EnsureAscii(slice, "OSC string argument");
        // (long) cast prevents overflow when nullPos is near int.MaxValue.
        var advance = ((long)nullPos + 1L + 3L) & ~3L;
        if ((long)_argumentsPos + advance > _source.Length) {
            throw new OscFormatException(
                $"OSC string argument alignment padding (advance {advance}) exceeds remaining buffer at position {_argumentsPos}");
        }
        _argumentsPos += (int)advance;
        return slice;
    }

    /// <summary>Reads a string argument and decodes it to a managed <see cref="string" />.</summary>
    public string ReadStringAlloc() => Encoding.ASCII.GetString(ReadStringSpan());

    /// <summary>Reads a blob argument as a span into the source buffer (no allocation).</summary>
    public ReadOnlySpan<byte> ReadBlobSpan() {
        EnsureArgumentReady(nameof(ReadBlobSpan));
        var lengthSpan = ConsumeArg(4);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthSpan);
        if (length < 0) {
            throw new OscFormatException($"OSC blob length must be non-negative (got {length})");
        }
        // (long) cast prevents overflow when length is near int.MaxValue.
        if ((long)_argumentsPos + length > _source.Length) {
            throw new OscFormatException($"OSC blob length {length} exceeds remaining buffer");
        }

        var slice = _source.Slice(_argumentsPos, length);
        var advance = ((long)length + 3L) & ~3L;
        if ((long)_argumentsPos + advance > _source.Length) {
            throw new OscFormatException($"OSC blob alignment padding for length {length} exceeds remaining buffer");
        }
        _argumentsPos += (int)advance;
        return slice;
    }

    /// <summary>Reads a blob argument and copies it to a freshly allocated byte array.</summary>
    public byte[] ReadBlobAlloc() => ReadBlobSpan().ToArray();

    /// <summary>Reads a 64-bit NTP timetag argument (tag <c>t</c>).</summary>
    public OscTimeTag ReadTimeTag() {
        EnsureArgumentReady(nameof(ReadTimeTag));
        var span = ConsumeArg(8);
        return new OscTimeTag(BinaryPrimitives.ReadUInt64BigEndian(span));
    }

    /// <summary>Reads a MIDI message argument (tag <c>m</c>).</summary>
    public OscMidi ReadMidi() {
        EnsureArgumentReady(nameof(ReadMidi));
        var span = ConsumeArg(4);
        return new OscMidi(span[0], span[1], span[2], span[3]);
    }

    /// <summary>Reads an RGBA color argument (tag <c>r</c>).</summary>
    public OscRgba ReadRgba() {
        EnsureArgumentReady(nameof(ReadRgba));
        var span = ConsumeArg(4);
        return new OscRgba(span[0], span[1], span[2], span[3]);
    }

    /// <summary>Reads an ASCII char argument (tag <c>c</c>): low byte of a big-endian 32-bit value.</summary>
    /// <remarks>Throws <see cref="OscFormatException" /> if any high bits are set; the spec requires ASCII (0x00-0x7F).</remarks>
    public char ReadAsciiChar() {
        EnsureArgumentReady(nameof(ReadAsciiChar));
        var span = ConsumeArg(4);
        var value = BinaryPrimitives.ReadUInt32BigEndian(span);
        if (value > 0x7F) {
            throw new OscFormatException(
                $"OSC ASCII char argument must be 0x00-0x7F (got 0x{value:X8})");
        }
        return (char)value;
    }

    private void EnsureArgumentReady(string operation) {
        if (_state != ReaderState.PositionedAtArgument) {
            throw new OscReaderStateException(
                $"OscReader.{operation} requires an argument positioned by MoveNext (state {ReaderState.PositionedAtArgument}); current state is {_state}");
        }
        _state = ReaderState.ReadyToIterate;
    }

    private ReadOnlySpan<byte> ConsumeArg(int length) {
        if (_argumentsPos + length > _source.Length) {
            throw new OscFormatException(
                $"OSC argument read of {length} bytes exceeds source length {_source.Length} at position {_argumentsPos}");
        }
        var slice = _source.Slice(_argumentsPos, length);
        _argumentsPos += length;
        return slice;
    }

    private static void EnsureAscii(ReadOnlySpan<byte> bytes, string valueName) {
        for (var i = 0; i < bytes.Length; i++) {
            if (bytes[i] > 0x7F) {
                throw new OscFormatException($"{valueName} contains non-ASCII byte at index {i} (0x{bytes[i]:X2})");
            }
        }
    }

    private static int FindNullTerminator(ReadOnlySpan<byte> span) => span.IndexOf((byte)0);

    private enum ReaderState : byte {
        AwaitingAddress = 0,
        ReadyToIterate = 1,
        PositionedAtArgument = 2,
        Exhausted = 3,
    }
}

}
