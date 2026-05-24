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
///     An OSC "symbol": a string identifier carried under type tag <c>S</c> rather than the
///     plain string tag <c>s</c>. The wire format is identical to a regular OSC string
///     (null-terminated, 4-byte aligned ASCII) — only the type tag differs. Use this when the
///     receiving application semantically distinguishes interned identifiers from arbitrary
///     strings.
/// </summary>
/// <remarks>Creates an <see cref="OscSymbol" /> wrapping <paramref name="value" />.</remarks>
public readonly struct OscSymbol : IEquatable<OscSymbol> {
    /// <summary>Creates an <see cref="OscSymbol" /> wrapping <paramref name="value" />.</summary>
    public OscSymbol(string value) {
        Value = value;
    }

    /// <summary>The symbol's underlying string.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public bool Equals(OscSymbol other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscSymbol other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(OscSymbol left, OscSymbol right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(OscSymbol left, OscSymbol right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"OscSymbol({Value})";
}

}
