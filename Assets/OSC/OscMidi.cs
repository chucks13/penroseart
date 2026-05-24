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
///     A 4-byte OSC MIDI message: <see cref="Port" />, <see cref="Status" />, <see cref="Data1" />,
///     <see cref="Data2" />. Encoded on the wire as four raw bytes in that order under type tag
///     <c>m</c>. The OSC 1.0 spec does not interpret the bytes; they are passed through to a MIDI
///     device or library on the receiving side.
/// </summary>
/// <remarks>Creates a 4-byte MIDI message.</remarks>
public readonly struct OscMidi : IEquatable<OscMidi> {
    /// <summary>Creates a 4-byte MIDI message.</summary>
    public OscMidi(byte port, byte status, byte data1, byte data2) {
        Port = port;
        Status = status;
        Data1 = data1;
        Data2 = data2;
    }

    /// <summary>Port byte. Application-defined; commonly the MIDI port or channel index.</summary>
    public byte Port { get; }

    /// <summary>MIDI status byte (e.g., <c>0x90</c> = Note On, channel 0).</summary>
    public byte Status { get; }

    /// <summary>First MIDI data byte (e.g., note number for Note On).</summary>
    public byte Data1 { get; }

    /// <summary>Second MIDI data byte (e.g., velocity for Note On).</summary>
    public byte Data2 { get; }

    /// <summary>Packs the four bytes into a single big-endian <see cref="uint" /> for wire output.</summary>
    public uint AsBigEndianUInt32() =>
        ((uint)Port << 24) | ((uint)Status << 16) | ((uint)Data1 << 8) | Data2;

    /// <inheritdoc />
    public bool Equals(OscMidi other) =>
        Port == other.Port && Status == other.Status && Data1 == other.Data1 && Data2 == other.Data2;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscMidi other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Port, Status, Data1, Data2);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(OscMidi left, OscMidi right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(OscMidi left, OscMidi right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"OscMidi(port={Port:X2}, status={Status:X2}, data1={Data1:X2}, data2={Data2:X2})";
}

}
