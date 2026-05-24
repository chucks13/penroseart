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
///     A 64-bit OSC time tag (NTP timestamp format): the high 32 bits are seconds since
///     <c>1900-01-01T00:00:00Z</c> and the low 32 bits are a binary fraction of one second
///     (1 / 2^32 second per increment). The special value <see cref="Immediately" />
///     (<c>0x00000000_00000001</c>) instructs receivers to dispatch the bundle without delay.
/// </summary>
/// <remarks>
///     The NTP epoch is 1900-01-01 UTC, 70 years before the Unix epoch (1970-01-01 UTC).
///     The offset is exactly <c>2_208_988_800</c> seconds. Conversion is integer-only to avoid
///     IEEE 754 drift at the sub-microsecond level.
/// </remarks>
public readonly struct OscTimeTag : IEquatable<OscTimeTag>, IComparable<OscTimeTag> {
    /// <summary>Seconds between the NTP epoch (1900-01-01) and the Unix epoch (1970-01-01).</summary>
    private const ulong NtpUnixEpochOffsetSeconds = 2_208_988_800UL;

    /// <summary>Ticks per second: 10,000,000 (each tick is 100 ns).</summary>
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;

    /// <summary>The "immediately" sentinel: dispatch with zero delay. Wire value <c>0x00000000_00000001</c>.</summary>
    public static readonly OscTimeTag Immediately = new(1UL);

    /// <summary>The zero time tag (NTP epoch 1900-01-01T00:00:00Z exactly).</summary>
    public static readonly OscTimeTag Zero = new(0UL);

    /// <summary>The raw 64-bit NTP value. High 32 bits are seconds, low 32 bits are fractional seconds (1/2^32 s).</summary>
    public ulong Value { get; }

    /// <summary>Creates an <see cref="OscTimeTag" /> from a raw 64-bit NTP value.</summary>
    public OscTimeTag(ulong value) {
        Value = value;
    }

    /// <summary>Creates an <see cref="OscTimeTag" /> from explicit seconds and fractional-seconds halves.</summary>
    /// <param name="seconds">Seconds since 1900-01-01T00:00:00Z.</param>
    /// <param name="fraction">Fractional seconds, where 1 unit = 1/2^32 second.</param>
    public OscTimeTag(uint seconds, uint fraction) {
        Value = ((ulong)seconds << 32) | fraction;
    }

    /// <summary>The NTP "seconds" component (high 32 bits).</summary>
    public uint Seconds => (uint)(Value >> 32);

    /// <summary>The NTP "fraction" component (low 32 bits, units of 1/2^32 second).</summary>
    public uint Fraction => (uint)(Value & 0xFFFF_FFFFUL);

    /// <summary>True if this is the special "immediately" sentinel <c>0x00000000_00000001</c>.</summary>
    public bool IsImmediately => Value == 1UL;

    /// <summary>Returns the current UTC time as an <see cref="OscTimeTag" />.</summary>
    public static OscTimeTag Now => FromDateTimeOffset(DateTimeOffset.UtcNow);

    /// <summary>Converts a <see cref="DateTimeOffset" /> to an NTP <see cref="OscTimeTag" />.</summary>
    /// <remarks>
    ///     <paramref name="value" /> is converted to UTC first. The Unix tick count is split into
    ///     whole seconds and remainder ticks, then the NTP epoch offset is added to the seconds and
    ///     the remainder ticks are scaled to 1/2^32-second fractions using integer arithmetic only.
    /// </remarks>
    public static OscTimeTag FromDateTimeOffset(DateTimeOffset value) {
        var unixTicks = value.ToUniversalTime().Ticks - DateTimeOffset.UnixEpoch.Ticks;
        var unixSeconds = Math.DivRem(unixTicks, TicksPerSecond, out var remainderTicks);
        if (remainderTicks < 0) {
            unixSeconds--;
            remainderTicks += TicksPerSecond;
        }

        var ntpSeconds = unixSeconds + (long)NtpUnixEpochOffsetSeconds;
        if (ntpSeconds is < 0 or > uint.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(value), value, "OSC timetag is outside the 32-bit NTP seconds range");
        }

        var fraction = (ulong)remainderTicks * (1UL << 32) / (ulong)TicksPerSecond;
        return new OscTimeTag(((ulong)ntpSeconds << 32) | fraction);
    }

    /// <summary>Converts this <see cref="OscTimeTag" /> back to a UTC <see cref="DateTimeOffset" />.</summary>
    /// <remarks>The "immediately" sentinel converts to <see cref="DateTimeOffset.UtcNow" />.</remarks>
    public DateTimeOffset ToDateTimeOffset() {
        if (IsImmediately) {
            return DateTimeOffset.UtcNow;
        }

        var unixSeconds = (long)Seconds - (long)NtpUnixEpochOffsetSeconds;
        var remainderTicks = (long)((ulong)Fraction * (ulong)TicksPerSecond / (1UL << 32));
        var totalTicks = DateTimeOffset.UnixEpoch.Ticks + (unixSeconds * TicksPerSecond) + remainderTicks;
        return new DateTimeOffset(totalTicks, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public bool Equals(OscTimeTag other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscTimeTag other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(OscTimeTag other) => Value.CompareTo(other.Value);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(OscTimeTag left, OscTimeTag right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(OscTimeTag left, OscTimeTag right) => !left.Equals(right);

    /// <summary>Less-than comparison.</summary>
    public static bool operator <(OscTimeTag left, OscTimeTag right) => left.Value < right.Value;

    /// <summary>Less-than-or-equal comparison.</summary>
    public static bool operator <=(OscTimeTag left, OscTimeTag right) => left.Value <= right.Value;

    /// <summary>Greater-than comparison.</summary>
    public static bool operator >(OscTimeTag left, OscTimeTag right) => left.Value > right.Value;

    /// <summary>Greater-than-or-equal comparison.</summary>
    public static bool operator >=(OscTimeTag left, OscTimeTag right) => left.Value >= right.Value;

    /// <inheritdoc />
    public override string ToString() => IsImmediately
        ? "OscTimeTag(Immediately)"
        : $"OscTimeTag({Seconds}.{Fraction:X8})";
}

}
