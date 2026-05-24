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
///     Base type for all <c>RaveSystem.Osc</c> failures. Catch this to handle any OSC-layer failure
///     as a single concept, or catch a more specific subtype to react to a particular failure class.
/// </summary>
public class OscException : Exception {
    /// <summary>Creates an <see cref="OscException" /> with the given message.</summary>
    public OscException(string message) : base(message) { }

    /// <summary>Creates an <see cref="OscException" /> with the given message and inner exception.</summary>
    public OscException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     Thrown when the OSC wire format is violated: missing null terminator, mis-aligned padding,
///     truncated bundle element, type tag inconsistent with arguments, or similar bytes-on-the-wire
///     defect.
/// </summary>
public sealed class OscFormatException : OscException {
    /// <summary>Creates an <see cref="OscFormatException" /> with the given message.</summary>
    public OscFormatException(string message) : base(message) { }

    /// <summary>Creates an <see cref="OscFormatException" /> with the given message and inner exception.</summary>
    public OscFormatException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     Thrown when an OSC address or address pattern is invalid: missing leading <c>/</c>, contains a
///     reserved character in a position the OSC grammar disallows, or unbalanced <c>[</c>/<c>{</c>
///     in a pattern.
/// </summary>
public sealed class OscAddressException : OscException {
    /// <summary>Creates an <see cref="OscAddressException" /> with the given message.</summary>
    public OscAddressException(string message) : base(message) { }

    /// <summary>Creates an <see cref="OscAddressException" /> with the given message and inner exception.</summary>
    public OscAddressException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     Thrown when an <c>OscWriter</c> is used out of order: writing arguments before the type tag is
///     closed, writing the address twice, or otherwise violating the writer state machine. Raised only
///     in DEBUG builds; release builds skip the state checks for zero-cost hot paths.
/// </summary>
public sealed class OscWriterStateException : OscException {
    /// <summary>Creates an <see cref="OscWriterStateException" /> with the given message.</summary>
    public OscWriterStateException(string message) : base(message) { }

    /// <summary>Creates an <see cref="OscWriterStateException" /> with the given message and inner exception.</summary>
    public OscWriterStateException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///     Thrown when an <c>OscReader</c> is used out of order: reading an argument before the address has
///     been parsed, reading without first advancing via <c>MoveNext</c>, reading the same argument twice
///     without an intervening <c>MoveNext</c>, or reading after the type tag string has been exhausted.
///     Distinct from <see cref="OscFormatException" /> (malformed wire bytes) so consumers can react to
///     API misuse separately from data-quality failures. Raised in all build configurations.
/// </summary>
public sealed class OscReaderStateException : OscException {
    /// <summary>Creates an <see cref="OscReaderStateException" /> with the given message.</summary>
    public OscReaderStateException(string message) : base(message) { }

    /// <summary>Creates an <see cref="OscReaderStateException" /> with the given message and inner exception.</summary>
    public OscReaderStateException(string message, Exception innerException) : base(message, innerException) { }
}

}
