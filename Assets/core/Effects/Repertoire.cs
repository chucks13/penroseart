using System;

/// <summary>
/// Musical capabilities a Performer advertises to the Director.
/// Repertoire is an input to casting and cue decisions; it never decides sequencing by itself.
/// </summary>
[Flags]
public enum Repertoire
{
    /// <summary>No advertised musical-structure behavior.</summary>
    None = 0,

    /// <summary>The Performer can express a Fill as an additive/in-place response.</summary>
    HandlesFill = 1 << 0,

    /// <summary>The Performer can express or land on a Drop.</summary>
    HandlesDrop = 1 << 1,

    /// <summary>The Performer can respond intentionally to Low/Mid/High energy state.</summary>
    RespondsToEnergy = 1 << 2,
}
