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
///     OSC 1.1-compatible address pattern matcher. A <em>pattern</em> is the address a sender places in
///     a message; it may contain wildcards (<c>?</c>, <c>*</c>, <c>//</c>, <c>[...]</c>, <c>{...}</c>) that
///     a receiver expands at dispatch time. <see cref="Matches(ReadOnlySpan{char}, ReadOnlySpan{char})" />
///     tests a pattern against a literal address (such as a registered handler address) and
///     returns whether the pattern would dispatch to that address.
/// </summary>
/// <remarks>
///     Per OSC 1.0 and the OSC 1.1 path-traversal addition:
///     <list type="bullet">
///         <item><c>?</c> matches any single character within a pattern part (never <c>/</c>).</item>
///         <item><c>*</c> matches zero or more characters within a pattern part (never <c>/</c>).</item>
///         <item><c>//</c> matches across zero or more whole address parts.</item>
///         <item><c>[abc]</c> matches one of the listed characters.</item>
///         <item><c>[a-z]</c> matches one character in the range, inclusive.</item>
///         <item><c>[!abc]</c> matches one character NOT in the listed set. <c>!</c> negates only at position 0; <c>^</c> is literal.</item>
///         <item><c>{foo,bar}</c> matches any of the comma-separated alternatives.</item>
///         <item><c>/</c> is a literal path separator and is never matched by a wildcard.</item>
///     </list>
///     Liblo edge cases honored:
///     <list type="bullet">
///         <item>Backwards ranges <c>[z-a]</c> are treated as literal sets (<c>{z, -, a}</c>).</item>
///         <item>Trailing dash <c>[abc-]</c> and leading dash <c>[-abc]</c> are literal characters.</item>
///         <item><c>!</c> is negation only when it appears at position 0; <c>[a!b]</c> is the literal set <c>{a, !, b}</c>.</item>
///     </list>
///     Allocation profile: zero allocations on the happy path. Brace expansions <c>{...}</c>
///     use one <c>stackalloc</c> buffer per encounter sized to the spliced sub-pattern.
/// </remarks>
public static class OscAddressPattern {
    /// <summary>Returns <see langword="true" /> if <paramref name="pattern" /> matches the literal <paramref name="address" />.</summary>
    /// <param name="pattern">An OSC address pattern, possibly containing wildcards.</param>
    /// <param name="address">A literal OSC address (no wildcards).</param>
    /// <exception cref="OscAddressException">Thrown if <paramref name="pattern" /> is malformed (unbalanced brackets or braces).</exception>
    public static bool Matches(ReadOnlySpan<char> pattern, ReadOnlySpan<char> address) {
        ValidatePattern(pattern);
        OscAddress.ValidateLiteralAddress(address);
        return MatchSubPattern(pattern, address);
    }

    /// <summary>Byte-span overload of <see cref="Matches(ReadOnlySpan{char}, ReadOnlySpan{char})" /> for ASCII OSC addresses.</summary>
    /// <remarks>
    ///     Bridges to the char-based matcher via a <c>stackalloc</c> buffer. Suitable for
    ///     dispatchers that have wire bytes in hand and want to avoid materializing strings.
    ///     Allocates nothing on the heap.
    /// </remarks>
    public static bool Matches(ReadOnlySpan<byte> pattern, ReadOnlySpan<byte> address) {
        // Cap stack allocation so a pathologically long pattern can't blow the stack
        // fall back to ArrayPool<char>.Shared for anything larger.
        const int StackThreshold = 1024;

        // Rent up front (separate statement) so the assignment isn't a sub-expression
        // of the ternary below; declaring `Span<char>` at the assignment site keeps
        // stackalloc's escape scope aligned with the local's declaration scope.
        var rentedPattern = pattern.Length > StackThreshold
            ? System.Buffers.ArrayPool<char>.Shared.Rent(pattern.Length)
            : null;
        var rentedAddress = address.Length > StackThreshold
            ? System.Buffers.ArrayPool<char>.Shared.Rent(address.Length)
            : null;
        try {
            var patternChars = rentedPattern is not null
                ? rentedPattern.AsSpan(0, pattern.Length)
                : (Span<char>)stackalloc char[pattern.Length];
            var addressChars = rentedAddress is not null
                ? rentedAddress.AsSpan(0, address.Length)
                : (Span<char>)stackalloc char[address.Length];

            for (var i = 0; i < pattern.Length; i++) {
                patternChars[i] = (char)pattern[i];
            }
            for (var i = 0; i < address.Length; i++) {
                addressChars[i] = (char)address[i];
            }
            return Matches((ReadOnlySpan<char>)patternChars, (ReadOnlySpan<char>)addressChars);
        } finally {
            if (rentedPattern is not null) {
                System.Buffers.ArrayPool<char>.Shared.Return(rentedPattern);
            }
            if (rentedAddress is not null) {
                System.Buffers.ArrayPool<char>.Shared.Return(rentedAddress);
            }
        }
    }

    /// <summary>Returns <see langword="true" /> if <paramref name="pattern" /> is a syntactically well-formed OSC address pattern.</summary>
    public static bool IsValidPattern(ReadOnlySpan<char> pattern) =>
        TryValidatePattern(pattern, out _);

    /// <summary>Validates the syntactic shape of <paramref name="pattern" />, reporting the first error if invalid.</summary>
    public static bool TryValidatePattern(ReadOnlySpan<char> pattern, out string? error) {
        if (pattern.IsEmpty) {
            error = "OSC pattern is empty";
            return false;
        }

        if (pattern[0] != '/') {
            error = "OSC pattern must start with '/'";
            return false;
        }

        if (pattern.Length == 1) {
            error = "OSC pattern '/' has no path parts";
            return false;
        }

        if (pattern[^1] == '/') {
            error = "OSC pattern must not end with '/'";
            return false;
        }

        // Scan the pattern character-by-character. Bracket / brace groups
        // (already validated for balance and non-emptiness) are skipped over
        // by jumping the cursor past the matching close, so we don't re-scan
        // their contents.
        var i = 0;
        while (i < pattern.Length) {
            var c = pattern[i];
            if (IsForbiddenPatternCharacter(c)) {
                error = $"OSC pattern contains invalid character at index {i} (char 0x{(int)c:X2})";
                return false;
            }
            if (c == '/' && i > 0 && pattern[i - 1] == '/' && (i + 1 >= pattern.Length || (i > 1 && pattern[i - 2] == '/'))) {
                error = $"OSC pattern contains empty path part at index {i}";
                return false;
            }
            if (c == '[') {
                var close = FindBracketClose(pattern, i);
                if (close < 0) {
                    error = $"OSC pattern has unbalanced '[' at index {i}";
                    return false;
                }
                if (close == i + 1) {
                    error = $"OSC pattern has empty bracket set at index {i}";
                    return false;
                }
                i = close + 1;
                continue;
            }
            if (c == '{') {
                var close = FindBraceClose(pattern, i);
                if (close < 0) {
                    error = $"OSC pattern has unbalanced '{{' at index {i}";
                    return false;
                }
                if (close == i + 1) {
                    error = $"OSC pattern has empty brace expression at index {i}";
                    return false;
                }
                i = close + 1;
                continue;
            }
            if (c is ']' or '}') {
                error = $"OSC pattern has unmatched '{c}' at index {i}";
                return false;
            }
            i++;
        }

        error = null;
        return true;
    }

    /// <summary>Throws <see cref="OscAddressException" /> if <paramref name="pattern" /> is not a valid pattern.</summary>
    public static void ValidatePattern(ReadOnlySpan<char> pattern) {
        if (!TryValidatePattern(pattern, out var error)) {
            throw new OscAddressException(error!);
        }
    }

    private static bool MatchSubPattern(ReadOnlySpan<char> pattern, ReadOnlySpan<char> address) {
        int pi = 0, ai = 0;
        while (pi < pattern.Length) {
            var pc = pattern[pi];

            if (pc == '/' && pi + 1 < pattern.Length && pattern[pi + 1] == '/') {
                return MatchPathTraversal(pattern[(pi + 2)..], address[ai..]);
            }

            if (pc == '*') {
                pi++;
                // The * cannot cross '/'. Find the upper bound for matching: the next '/' in
                // address or end-of-address.
                var maxAi = ai;
                while (maxAi < address.Length && address[maxAi] != '/') {
                    maxAi++;
                }
                // Try every match length from 0 .. (maxAi - ai).
                for (var k = ai; k <= maxAi; k++) {
                    if (MatchSubPattern(pattern[pi..], address[k..])) {
                        return true;
                    }
                }
                return false;
            }

            if (pc == '{') {
                var close = FindBraceClose(pattern, pi);
                var altsSpan = pattern[(pi + 1)..close];
                var tail = pattern[(close + 1)..];
                return MatchAlternatives(altsSpan, tail, address[ai..]);
            }

            if (ai >= address.Length) {
                return false;
            }

            var ac = address[ai];

            switch (pc) {
                case '?':
                    if (ac == '/') {
                        return false;
                    }
                    pi++;
                    ai++;
                    break;

                case '[':
                    var close = FindBracketClose(pattern, pi);
                    var setSpan = pattern[(pi + 1)..close];
                    if (ac == '/' || !MatchBracket(setSpan, ac)) {
                        return false;
                    }
                    pi = close + 1;
                    ai++;
                    break;

                default:
                    if (pc != ac) {
                        return false;
                    }
                    pi++;
                    ai++;
                    break;
            }
        }

        return ai == address.Length;
    }

    private static bool MatchPathTraversal(ReadOnlySpan<char> tail, ReadOnlySpan<char> address) {
        if (MatchSubPattern(tail, address)) {
            return true;
        }

        for (var i = 0; i < address.Length; i++) {
            if (address[i] == '/' && MatchSubPattern(tail, address[(i + 1)..])) {
                return true;
            }
        }
        return false;
    }

    private static bool MatchAlternatives(ReadOnlySpan<char> alts, ReadOnlySpan<char> tail, ReadOnlySpan<char> address) {
        // The longest possible alt is the entire alts span (one alt, no commas), so a single
        // buffer sized for alts.Length + tail.Length covers every iteration. CA2014: do not
        // stackalloc inside the loop body; reuse this buffer instead.
        Span<char> spliced = stackalloc char[alts.Length + tail.Length];

        var altStart = 0;
        for (var k = 0; k <= alts.Length; k++) {
            if (k == alts.Length || alts[k] == ',') {
                var alt = alts[altStart..k];
                var slice = spliced[..(alt.Length + tail.Length)];
                alt.CopyTo(slice);
                tail.CopyTo(slice[alt.Length..]);
                if (MatchSubPattern(slice, address)) {
                    return true;
                }
                altStart = k + 1;
            }
        }
        return false;
    }

    private static bool MatchBracket(ReadOnlySpan<char> set, char c) {
        if (set.IsEmpty) {
            return false;
        }

        var negate = set[0] == '!';
        var i = negate ? 1 : 0;
        var match = false;

        while (i < set.Length) {
            // Range form X-Y, only when '-' is between two chars and start <= end.
            if (i + 2 < set.Length && set[i + 1] == '-') {
                var rangeStart = set[i];
                var rangeEnd = set[i + 2];
                if (rangeStart <= rangeEnd) {
                    if (c >= rangeStart && c <= rangeEnd) {
                        match = true;
                    }
                    i += 3;
                    continue;
                }
                // Backwards range: fall through and treat as literal.
            }

            if (set[i] == c) {
                match = true;
            }
            i++;
        }

        return match != negate;
    }

    private static bool IsForbiddenPatternCharacter(char c) => c is < (char)0x20 or > (char)0x7E or ' ' or '#';

    private static int FindBracketClose(ReadOnlySpan<char> pattern, int openIndex) {
        for (var i = openIndex + 1; i < pattern.Length; i++) {
            if (pattern[i] == ']') {
                return i;
            }
            if (pattern[i] == '/') {
                return -1;
            }
        }
        return -1;
    }

    private static int FindBraceClose(ReadOnlySpan<char> pattern, int openIndex) {
        for (var i = openIndex + 1; i < pattern.Length; i++) {
            if (pattern[i] == '}') {
                return i;
            }
            if (pattern[i] == '/') {
                return -1;
            }
        }
        return -1;
    }
}

}
