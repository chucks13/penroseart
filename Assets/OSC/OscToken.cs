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
///     OSC type tag identifiers. Enum values are the literal ASCII codes of the on-wire
///     type tag characters, so a token can be cast to <see cref="byte" /> and written directly
///     into a type tag string with no translation step.
/// </summary>
/// <remarks>
///     OSC 1.0 defines four "required" types (i, f, s, b) and ten "non-standard" but widely
///     supported types (h, d, t, T, F, N, I, m, c, r, S). OSC 1.1 promotes the
///     <see cref="True" />, <see cref="False" />, <see cref="Null" />, <see cref="Impulse" />,
///     and <see cref="TimeTag" /> tags to required support. Array delimiters [ and ] are also
///     defined here for completeness. <see cref="True" />, <see cref="False" />,
///     <see cref="Null" />, and <see cref="Impulse" /> have no payload bytes:
///     the value is encoded entirely by the tag.
///     <para>
///     Member names follow the OSC spec convention of <c>tag-letter</c> + <c>bit-width</c>
///     (<see cref="I32" />, <see cref="F32" />, etc.) rather than <c>Int32</c>/<c>Float32</c>
///     to keep the enum aligned with the wire-format view of the tag character.
///     </para>
/// </remarks>
public enum OscToken : byte {
    /// <summary>32-bit big-endian two's complement integer. Tag <c>i</c>.</summary>
    I32 = (byte)'i',

    /// <summary>32-bit big-endian IEEE 754 float. Tag <c>f</c>.</summary>
    F32 = (byte)'f',

    /// <summary>Null-terminated, 4-byte aligned ASCII string. Tag <c>s</c>.</summary>
    Str = (byte)'s',

    /// <summary>32-bit big-endian length prefix followed by raw bytes, 4-byte aligned. Tag <c>b</c>.</summary>
    Blob = (byte)'b',

    /// <summary>64-bit big-endian two's complement integer. Tag <c>h</c>.</summary>
    I64 = (byte)'h',

    /// <summary>64-bit big-endian IEEE 754 double. Tag <c>d</c>.</summary>
    F64 = (byte)'d',

    /// <summary>64-bit NTP timetag. Tag <c>t</c>.</summary>
    TimeTag = (byte)'t',

    /// <summary>Boolean true. No payload bytes. Tag <c>T</c>.</summary>
    True = (byte)'T',

    /// <summary>Boolean false. No payload bytes. Tag <c>F</c>.</summary>
    False = (byte)'F',

    /// <summary>Null / nil. No payload bytes. Tag <c>N</c>.</summary>
    Null = (byte)'N',

    /// <summary>Impulse / bang event trigger. No payload bytes. Tag <c>I</c>.</summary>
    Impulse = (byte)'I',

    /// <summary>4-byte MIDI message: port, status, data1, data2. Tag <c>m</c>.</summary>
    Midi = (byte)'m',

    /// <summary>32-bit ASCII character (low byte). Tag <c>c</c>.</summary>
    Ascii = (byte)'c',

    /// <summary>4-byte RGBA color: red, green, blue, alpha. Tag <c>r</c>.</summary>
    Color = (byte)'r',

    /// <summary>Null-terminated, 4-byte aligned ASCII symbol (interned string). Tag <c>S</c>.</summary>
    Symbol = (byte)'S',

    /// <summary>Array open bracket. Tag <c>[</c>. Pairs with <see cref="ArrayEnd" />.</summary>
    ArrayStart = (byte)'[',

    /// <summary>Array close bracket. Tag <c>]</c>. Pairs with <see cref="ArrayStart" />.</summary>
    ArrayEnd = (byte)']',
}

}
