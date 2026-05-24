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
///     OSC address validation. An OSC <em>literal address</em> is the address a sender places in
///     a message: it must start with <c>/</c>, contain only printable ASCII, and must not contain
///     any of the OSC pattern meta-characters (<c>? * [ ] { } ,</c>), <c>#</c>, or space. Pattern
///     validation (used at dispatch time) is provided separately and allows those characters.
/// </summary>
/// <remarks>
///     Per OSC 1.0: an OSC Address Pattern is a list of OSC Address Pattern Parts each preceded
///     by <c>/</c>. A literal address is the same shape but with no pattern characters. We do not
///     allow trailing slashes or empty parts; both are common interop foot-guns and the spec
///     example addresses (<c>/oscillator/4/frequency</c>, <c>/foo</c>) never use them.
/// </remarks>
public static class OscAddress {
    /// <summary>OSC reserved characters that may not appear in a literal address part.</summary>
    /// <remarks>
    ///     <c>?</c>, <c>*</c>, <c>[</c>, <c>]</c>, <c>{</c>, <c>}</c>, <c>,</c> are pattern
    ///     meta-characters; <c>#</c> is reserved for <c>#bundle</c>; <c>/</c> is the part
    ///     separator and is checked separately; <c>' '</c> (space) is disallowed in addresses.
    /// </remarks>
    public static ReadOnlySpan<char> ReservedCharacters => " #*,/?[]{}";

    /// <summary>Validates that <paramref name="address" /> is a well-formed OSC literal address.</summary>
    /// <param name="address">The address to validate.</param>
    /// <returns><c>true</c> if valid; <c>false</c> otherwise.</returns>
    public static bool IsValidLiteralAddress(ReadOnlySpan<char> address) =>
        TryValidateLiteralAddress(address, out _);

    /// <summary>
    ///     Validates that <paramref name="address" /> is a well-formed OSC literal address,
    ///     reporting the first failure reason if it is not.
    /// </summary>
    /// <param name="address">The address to validate.</param>
    /// <param name="error">On failure, a short human-readable reason; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if valid; <c>false</c> otherwise.</returns>
    public static bool TryValidateLiteralAddress(ReadOnlySpan<char> address, out string? error) {
        if (address.IsEmpty) {
            error = "OSC address is empty";
            return false;
        }

        if (address[0] != '/') {
            error = "OSC address must start with '/'";
            return false;
        }

        if (address.Length == 1) {
            error = "OSC address '/' has no path parts";
            return false;
        }

        if (address[^1] == '/') {
            error = "OSC address must not end with '/'";
            return false;
        }

        for (var i = 1; i < address.Length; i++) {
            var c = address[i];

            if (c is < (char)0x20 or > (char)0x7E) {
                error = $"OSC address contains non-printable ASCII at index {i} (char 0x{(int)c:X2})";
                return false;
            }

            if (c == '/') {
                if (address[i - 1] == '/') {
                    error = $"OSC address contains empty path part at index {i}";
                    return false;
                }
                continue;
            }

            if (IsReserved(c)) {
                error = $"OSC address contains reserved character '{c}' at index {i}";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>Throws <see cref="OscAddressException" /> if <paramref name="address" /> is not a valid literal address.</summary>
    public static void ValidateLiteralAddress(ReadOnlySpan<char> address) {
        if (!TryValidateLiteralAddress(address, out var error)) {
            throw new OscAddressException(error!);
        }
    }

    private const string ReservedCharSet = " #*,?[]{}";

    private static bool IsReserved(char c) => ReservedCharSet.IndexOf(c) >= 0;
}

}
