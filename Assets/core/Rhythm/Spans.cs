// Implements the Before and In spans that serve the Stock Envelopes for one musical piece.
#nullable enable

using UnityEngine;

/// <summary>
/// The approach to a musical piece: the Stock Envelopes read across a caller-named window of whole
/// beats that ends where the piece begins.
/// </summary>
/// <remarks>
/// Total by construction. An unavailable piece, an unusable window, and a piece further off than the
/// window all read as infinitely far, so <see cref="Build"/> rests at zero and <see cref="Decay"/>
/// rests at one — a speed multiplier written against <see cref="Decay"/> therefore means "no response"
/// whenever nothing is coming, including Standalone Mode. Having no length of its own, this span never
/// defaults its window.
/// </remarks>
public readonly struct BeforeSpan
{
    /// <summary>Continuous beats until the piece begins; null while no such piece is coming.</summary>
    private readonly float? beatsUntil;

    /// <summary>Creates an approach span from a continuous distance to the piece.</summary>
    /// <param name="continuousBeatsUntil">
    /// Beats until the piece begins, smoothed within the beat so the value moves continuously;
    /// null when no such piece is upcoming.
    /// </param>
    internal BeforeSpan(float? continuousBeatsUntil)
    {
        beatsUntil = continuousBeatsUntil;
    }

    /// <summary>Rises from zero to one across the <paramref name="windowBeats"/> beats before the piece.</summary>
    /// <param name="windowBeats">Length of the approach runway in whole beats.</param>
    public float Build(int windowBeats) => StockEnvelopes.Rise(Approach(windowBeats));

    /// <summary>Falls from one to zero across the <paramref name="windowBeats"/> beats before the piece.</summary>
    /// <param name="windowBeats">Length of the approach runway in whole beats.</param>
    public float Decay(int windowBeats) => StockEnvelopes.Fall(Approach(windowBeats));

    /// <summary>
    /// Total 0..1 position along the runway: zero while the piece is unknown or beyond the window,
    /// one as it lands. Totality lives here so both envelopes inherit it from one reading.
    /// </summary>
    private float Approach(int windowBeats) =>
        windowBeats > 0 && beatsUntil is { } until
            ? Mathf.Clamp01((windowBeats - until) / windowBeats)
            : 0f;
}

/// <summary>
/// The passage through an active musical piece: the Stock Envelopes read from the piece's start to its end.
/// </summary>
/// <remarks>
/// Both envelopes rest at zero whenever no piece is active or its length is unknown. The piece's own
/// length is the window when the caller names none, so the common case needs no argument.
/// </remarks>
public readonly struct InSpan
{
    /// <summary>Continuous beats elapsed through the active piece; null while none is active.</summary>
    private readonly float? elapsedBeats;

    /// <summary>The piece's own length, used as the window when the caller names none.</summary>
    private readonly int? lengthBeats;

    /// <summary>Creates a through-the-piece span from its elapsed position and own length.</summary>
    /// <param name="elapsedBeats">
    /// Beats elapsed since the piece began, smoothed within the beat; null while it is not active.
    /// </param>
    /// <param name="lengthBeats">The piece's own length in whole beats; null when unknown.</param>
    internal InSpan(float? elapsedBeats, int? lengthBeats)
    {
        this.elapsedBeats = elapsedBeats;
        this.lengthBeats = lengthBeats;
    }

    /// <summary>Rises across the piece's full length, or across the requested window of whole beats.</summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the piece's own length.</param>
    public float Build(int? windowBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, windowBeats ?? lengthBeats);

    /// <summary>Falls across the piece's full length, or across the requested window of whole beats.</summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the piece's own length.</param>
    public float Decay(int? windowBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, windowBeats ?? lengthBeats);
}
