using System;

/// <summary>
/// Musical capabilities a Performer advertises to the Director.
/// Repertoire is an input to casting and cue decisions; it never decides sequencing by itself.
/// Only the capability flags are cast on. The energy affinities are advertised and currently unconsumed:
/// energy is a Performer input read live from BeatManager rather than a casting one, and the
/// affinity flags are the declaration half of a contract whose consumption side is still open.
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

    /// <summary>The Performer says it suits Low-energy sections. Advertised only; nothing casts on it.</summary>
    EnergyLow = 1 << 2,

    /// <summary>The Performer says it suits Mid-energy sections. Advertised only; nothing casts on it.</summary>
    EnergyMid = 1 << 3,

    /// <summary>The Performer says it suits High-energy sections. Advertised only; nothing casts on it.</summary>
    EnergyHigh = 1 << 4,
}
