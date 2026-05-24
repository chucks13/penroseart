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
///     A 4-byte OSC RGBA color: <see cref="R" />, <see cref="G" />, <see cref="B" />,
///     <see cref="A" />. Encoded on the wire as four raw bytes in that order under type tag
///     <c>r</c>. Each channel is an 8-bit unsigned value, <c>0</c>-<c>255</c>.
/// </summary>
/// <remarks>Creates a 4-byte RGBA color.</remarks>
public readonly struct OscRgba : IEquatable<OscRgba> {
    /// <summary>Creates a 4-byte RGBA color.</summary>
    public OscRgba(byte r, byte g, byte b, byte a) {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Red channel, <c>0</c>-<c>255</c>.</summary>
    public byte R { get; }

    /// <summary>Green channel, <c>0</c>-<c>255</c>.</summary>
    public byte G { get; }

    /// <summary>Blue channel, <c>0</c>-<c>255</c>.</summary>
    public byte B { get; }

    /// <summary>Alpha channel, <c>0</c>-<c>255</c> (<c>0</c> = fully transparent, <c>255</c> = fully opaque).</summary>
    public byte A { get; }

    /// <summary>Packs the four channels into a single big-endian <see cref="uint" /> for wire output.</summary>
    public uint AsBigEndianUInt32() =>
        ((uint)R << 24) | ((uint)G << 16) | ((uint)B << 8) | A;

    /// <inheritdoc />
    public bool Equals(OscRgba other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscRgba other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(OscRgba left, OscRgba right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(OscRgba left, OscRgba right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"OscRgba(r={R}, g={G}, b={B}, a={A})";
}

}
