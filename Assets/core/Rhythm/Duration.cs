// The Duration ladder: the one shared musical ladder of note values (beat-data spec).

#nullable enable

/// <summary>
/// The shared musical ladder of note values: how much musical time something occupies, named by
/// note value rather than a count. One ladder serves both sides of the vocabulary — a Hump's width
/// <em>occupies</em> a Duration, and a Duration Pulse or Gate <em>runs every</em> Duration
/// (<see cref="PulsesView.Every"/>). Sixteenth is the fastest allowed; finer rates are deliberately
/// excluded — musically unneeded and a full-wall flicker hazard (ADR-0001). Backbeat/offbeat
/// placements are implementation spelling, not rungs on this ladder.
/// </summary>
public enum Duration
{
    /// <summary>The full bar — four beats in the wall's common-time model.</summary>
    Whole,

    /// <summary>Half the bar — two beats.</summary>
    Half,

    /// <summary>One beat.</summary>
    Quarter,

    /// <summary>Half a beat.</summary>
    Eighth,

    /// <summary>A quarter of a beat — the fastest rung allowed.</summary>
    Sixteenth,
}
