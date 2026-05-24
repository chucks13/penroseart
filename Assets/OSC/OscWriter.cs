// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;

namespace RaveSystem.Osc {

/// <summary>
///     Span-based OSC 1.1-compatible message encoder. The caller owns the destination buffer and the writer
///     tracks the current position into it. Encoding allocates nothing on the heap on the happy
///     path. Address validation always runs so malformed outbound packets fail truthfully;
///     state machine checks and overflow checks are in <c>[Conditional("DEBUG")]</c> sections,
///     so release builds avoid those development-only checks.
/// </summary>
/// <remarks>
///     Lifecycle:
///     <list type="number">
///         <item>Construct with a destination <see cref="Span{T}" />.</item>
///         <item>Call <see cref="WriteAddress" /> exactly once.</item>
///         <item>Call any number of <c>WriteXxx</c> argument methods in argument order.</item>
///         <item>Call <see cref="Finish" /> to emit the type tag string and return the byte length.</item>
///     </list>
///     Tagless tokens (<see cref="OscToken.True" />, <see cref="OscToken.False" />,
///     <see cref="OscToken.Null" />, <see cref="OscToken.Impulse" />) are emitted by
///     <see cref="WriteTrue" />, <see cref="WriteFalse" />, <see cref="WriteNull" />,
///     <see cref="WriteImpulse" />: they update the type tag string but write no payload.
///     Arrays use <see cref="ArrayStart" /> / <see cref="ArrayEnd" />.
/// </remarks>
public ref struct OscWriter {
    /// <summary>Default inline type-tag scratch capacity (excludes the leading comma and trailing null).</summary>
    public const int MaxTypeTags = 60;

    private const byte TypeTagComma = (byte)',';

    private static readonly Encoding s_strictAscii = Encoding.GetEncoding(
        "us-ascii",
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    private readonly Span<byte> _destination;
    private readonly Span<byte> _externalTagScratch;
    private readonly byte[] _tagScratch;
    private int _tagCount;
    private int _arrayDepth;
    private int _addrEnd;
    private WriterState _state;

    /// <summary>Creates a writer that emits OSC bytes into <paramref name="destination" />.</summary>
    public OscWriter(Span<byte> destination) {
        _destination = destination;
        _externalTagScratch = default;
        _tagScratch = new byte[MaxTypeTags];
        _tagCount = 0;
        _arrayDepth = 0;
        _addrEnd = 0;
        BytesWritten = 0;
        _state = WriterState.AwaitingAddress;
    }

    /// <summary>
    ///     Creates a writer that emits OSC bytes into <paramref name="destination" /> and uses
    ///     caller-provided scratch storage for type tags.
    /// </summary>
    /// <remarks>
    ///     Use this overload when a message can exceed the default inline type-tag capacity.
    ///     The scratch span must remain valid until <see cref="Finish" /> returns.
    /// </remarks>
    public OscWriter(Span<byte> destination, Span<byte> typeTagScratch) {
        if (typeTagScratch.IsEmpty) {
            throw new ArgumentException("Type tag scratch must not be empty", nameof(typeTagScratch));
        }

        _destination = destination;
        _externalTagScratch = typeTagScratch;
        _tagScratch = new byte[MaxTypeTags];
        _tagCount = 0;
        _arrayDepth = 0;
        _addrEnd = 0;
        BytesWritten = 0;
        _state = WriterState.AwaitingAddress;
    }

    /// <summary>The number of bytes written so far. After <see cref="Finish" />, this is the encoded message length.</summary>
    public int BytesWritten { get; private set; }

    /// <summary>Writes the OSC address and prepares the writer to accept arguments.</summary>
    /// <remarks>
    ///     Address bytes go into the destination immediately, padded to a 4-byte boundary.
    ///     The type tag string is buffered in an inline scratch field and emitted at <see cref="Finish" />.
    /// </remarks>
    public void WriteAddress(ReadOnlySpan<char> address) {
        EnsureState(WriterState.AwaitingAddress, "WriteAddress");
        ValidateAddress(address);

        WriteAlignedAscii(address);
        _addrEnd = BytesWritten;

        _state = WriterState.WritingTypeTag;
    }

    /// <summary>Appends an int32 argument (tag <c>i</c>).</summary>
    public void WriteInt32(int value) {
        AppendTypeTag(OscToken.I32);
        BinaryPrimitives.WriteInt32BigEndian(EnsureArgSpan(4), value);
    }

    /// <summary>Appends an int64 argument (tag <c>h</c>).</summary>
    public void WriteInt64(long value) {
        AppendTypeTag(OscToken.I64);
        BinaryPrimitives.WriteInt64BigEndian(EnsureArgSpan(8), value);
    }

    /// <summary>Appends a float32 argument (tag <c>f</c>).</summary>
    public void WriteFloat32(float value) {
        AppendTypeTag(OscToken.F32);
        BinaryPrimitives.WriteInt32BigEndian(EnsureArgSpan(4), BitConverter.SingleToInt32Bits(value));
    }

    /// <summary>Appends a float64 (double) argument (tag <c>d</c>).</summary>
    public void WriteFloat64(double value) {
        AppendTypeTag(OscToken.F64);
        BinaryPrimitives.WriteInt64BigEndian(EnsureArgSpan(8), BitConverter.DoubleToInt64Bits(value));
    }

    /// <summary>Appends a string argument (tag <c>s</c>).</summary>
    public void WriteString(ReadOnlySpan<char> value) {
        AppendTypeTag(OscToken.Str);
        WriteAlignedAscii(value);
    }

    /// <summary>Appends a symbol argument (tag <c>S</c>).</summary>
    public void WriteSymbol(ReadOnlySpan<char> value) {
        AppendTypeTag(OscToken.Symbol);
        WriteAlignedAscii(value);
    }

    /// <summary>Appends a blob argument (tag <c>b</c>): 32-bit big-endian length prefix, raw bytes, 4-byte aligned padding.</summary>
    public void WriteBlob(ReadOnlySpan<byte> value) {
        AppendTypeTag(OscToken.Blob);
        BinaryPrimitives.WriteInt32BigEndian(EnsureArgSpan(4), value.Length);
        var contentSpan = EnsureArgSpan(value.Length);
        value.CopyTo(contentSpan);
        var padding = (4 - (value.Length & 3)) & 3;
        if (padding > 0) {
            EnsureArgSpan(padding).Clear();
        }
    }

    /// <summary>Appends a 64-bit NTP timetag argument (tag <c>t</c>).</summary>
    public void WriteTimeTag(OscTimeTag value) {
        AppendTypeTag(OscToken.TimeTag);
        BinaryPrimitives.WriteUInt64BigEndian(EnsureArgSpan(8), value.Value);
    }

    /// <summary>Appends a boolean true (tag <c>T</c>, no payload).</summary>
    public void WriteTrue() => AppendTypeTag(OscToken.True);

    /// <summary>Appends a boolean false (tag <c>F</c>, no payload).</summary>
    public void WriteFalse() => AppendTypeTag(OscToken.False);

    /// <summary>Appends a null/nil (tag <c>N</c>, no payload).</summary>
    public void WriteNull() => AppendTypeTag(OscToken.Null);

    /// <summary>Appends an impulse / bang event trigger (tag <c>I</c>, no payload).</summary>
    public void WriteImpulse() => AppendTypeTag(OscToken.Impulse);

    /// <summary>Appends a MIDI message argument (tag <c>m</c>).</summary>
    public void WriteMidi(OscMidi value) {
        AppendTypeTag(OscToken.Midi);
        BinaryPrimitives.WriteUInt32BigEndian(EnsureArgSpan(4), value.AsBigEndianUInt32());
    }

    /// <summary>Appends an RGBA color argument (tag <c>r</c>).</summary>
    public void WriteRgba(OscRgba value) {
        AppendTypeTag(OscToken.Color);
        BinaryPrimitives.WriteUInt32BigEndian(EnsureArgSpan(4), value.AsBigEndianUInt32());
    }

    /// <summary>Appends an ASCII char argument (tag <c>c</c>): 32-bit big-endian, low byte holds the character.</summary>
    public void WriteAsciiChar(char value) {
        AppendTypeTag(OscToken.Ascii);
        if (value > 0x7F) {
            ThrowFormat($"OSC ASCII char argument must be 0x00-0x7F (got 0x{(int)value:X4})");
        }
        BinaryPrimitives.WriteUInt32BigEndian(EnsureArgSpan(4), value);
    }

    /// <summary>Opens an OSC array (tag <c>[</c>). Pair with <see cref="ArrayEnd" />.</summary>
    public void ArrayStart() {
        AppendTypeTag(OscToken.ArrayStart);
        _arrayDepth++;
    }

    /// <summary>Closes an OSC array (tag <c>]</c>). Must be paired with <see cref="ArrayStart" />.</summary>
    public void ArrayEnd() {
        if (_arrayDepth == 0) {
            ThrowFormat("OSC array close tag has no matching array start");
        }
        AppendTypeTag(OscToken.ArrayEnd);
        _arrayDepth--;
    }

    /// <summary>
    ///     Closes the message: shifts the buffered argument bytes to make room for the type tag
    ///     string, writes the tag string (comma + tags + null + 4-byte padding), and returns the
    ///     total encoded byte length.
    /// </summary>
    public int Finish() {
        EnsureStateAtLeast(WriterState.WritingTypeTag, "Finish");
        if (_arrayDepth != 0) {
            ThrowFormat($"OSC message has {_arrayDepth} unclosed array start tag(s)");
        }

        // Tag string layout: ',' + N tag bytes + null + padding to next 4-byte boundary.
        // (long) arithmetic prevents overflow when _tagCount comes from a caller-provided
        // scratch span near int.MaxValue.
        var tagBytesIncludingCommaAndNull = (long)_tagCount + 2L;
        var tagStringSizeLong = (tagBytesIncludingCommaAndNull + 3L) & ~3L;
        if (tagStringSizeLong > int.MaxValue - _addrEnd) {
            ThrowFormat($"OSC tag string size {tagStringSizeLong} overflows int range from address end {_addrEnd}");
        }
        var tagStringSize = (int)tagStringSizeLong;

        var argsLength = BytesWritten - _addrEnd;
        EnsureCapacityForFinish(tagStringSize, argsLength);

        // Shift argument bytes right by tagStringSize so they sit after the tag string.
        // Span<T>.CopyTo handles overlapping ranges as if read into a temporary first.
        if (argsLength > 0) {
            var src = _destination.Slice(_addrEnd, argsLength);
            var dst = _destination.Slice(_addrEnd + tagStringSize, argsLength);
            src.CopyTo(dst);
        }

        // Lay down the tag string at _addrEnd.
        var tagRegion = _destination.Slice(_addrEnd, tagStringSize);
        tagRegion[0] = TypeTagComma;
        CopyTypeTagsTo(tagRegion[1..]);
        // Null + padding bytes.
        tagRegion[(1 + _tagCount)..].Clear();

        BytesWritten = _addrEnd + tagStringSize + argsLength;
        _state = WriterState.Sealed;
        return BytesWritten;
    }

    private void WriteAlignedAscii(ReadOnlySpan<char> value) {
        int byteCount;
        try {
            byteCount = s_strictAscii.GetByteCount(value);
        } catch (EncoderFallbackException ex) {
            throw new OscFormatException(
                "OSC strings and addresses must be ASCII; got non-ASCII character", ex);
        }

        // (long) arithmetic prevents overflow when byteCount is near int.MaxValue.
        var nullPaddedLengthLong = ((long)byteCount + 4L) & ~3L;
        if (nullPaddedLengthLong > int.MaxValue - BytesWritten) {
            ThrowFormat($"OSC string of {byteCount} bytes (aligned to {nullPaddedLengthLong}) overflows int range from position {BytesWritten}");
        }
        var dst = EnsureArgSpan((int)nullPaddedLengthLong);
        try {
            s_strictAscii.GetBytes(value, dst);
        } catch (EncoderFallbackException ex) {
            throw new OscFormatException(
                "OSC strings and addresses must be ASCII; got non-ASCII character", ex);
        }
        dst[byteCount..].Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> EnsureArgSpan(int length) {
        EnsureCapacityDebug(length);
        var slice = _destination.Slice(BytesWritten, length);
        BytesWritten += length;
        return slice;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendTypeTag(OscToken token) {
        EnsureStateAtLeast(WriterState.WritingTypeTag, "argument write");

        var tagCapacity = _externalTagScratch.IsEmpty ? MaxTypeTags : _externalTagScratch.Length;
        if (_tagCount >= tagCapacity) {
            ThrowFormat($"OSC message exceeds type tag scratch capacity {tagCapacity}");
        }

        if (_externalTagScratch.IsEmpty) {
            _tagScratch[_tagCount] = (byte)token;
        } else {
            _externalTagScratch[_tagCount] = (byte)token;
        }
        _tagCount++;
        _state = WriterState.WritingArguments;
    }

    [Conditional("DEBUG")]
    private readonly void EnsureState(WriterState expected, string operation) {
        if (_state != expected) {
            throw new OscWriterStateException(
                $"OscWriter operation '{operation}' requires state {expected} but writer is in state {_state}");
        }
    }

    [Conditional("DEBUG")]
    private readonly void EnsureStateAtLeast(WriterState minimum, string operation) {
        if ((int)_state < (int)minimum || _state == WriterState.Sealed) {
            throw new OscWriterStateException(
                $"OscWriter operation '{operation}' is invalid in state {_state}");
        }
    }

    [Conditional("DEBUG")]
    private readonly void EnsureCapacityDebug(int additional) {
        if (BytesWritten + additional > _destination.Length) {
            throw new OscFormatException(
                $"OscWriter destination too small: need {BytesWritten + additional} bytes, have {_destination.Length}");
        }
    }

    [Conditional("DEBUG")]
    private readonly void EnsureCapacityForFinish(int tagStringSize, int argsLength) {
        var required = _addrEnd + tagStringSize + argsLength;
        if (required > _destination.Length) {
            throw new OscFormatException(
                $"OscWriter destination too small at Finish: need {required} bytes, have {_destination.Length}");
        }
    }

    private static void ValidateAddress(ReadOnlySpan<char> address) =>
        // Senders may send pattern addresses (with wildcards) per OSC; validate as pattern,
        // not as literal. Handler registration on the receiver side validates as literal.
        OscAddressPattern.ValidatePattern(address);

    private void CopyTypeTagsTo(Span<byte> destination) {
        if (_externalTagScratch.IsEmpty) {
            _tagScratch.AsSpan(0, _tagCount).CopyTo(destination);
        } else {
            _externalTagScratch[.._tagCount].CopyTo(destination);
        }
    }

    private static void ThrowFormat(string message) => throw new OscFormatException(message);

    private enum WriterState : byte {
        AwaitingAddress = 0,
        WritingTypeTag = 1,
        WritingArguments = 2,
        Sealed = 3,
    }

}

}
