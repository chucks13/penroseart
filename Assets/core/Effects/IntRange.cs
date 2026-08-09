// Defines an integer Roll range with editor-owned slider calibration rails.

using System;

/// <summary>
/// Stores the inclusive minimum and exclusive maximum supplied to integer randomization, plus the
/// serialized rails used only by editor authoring surfaces.
/// </summary>
[Serializable]
public sealed class IntRange
{
    /// <summary>Creates a single-value zero range with zero-to-one editor slider rails.</summary>
    public IntRange() : this(0, 1, 0, 1)
    {
    }

    /// <summary>Creates a range whose authored endpoints also seed its editor slider rails.</summary>
    /// <param name="minInclusive">The lowest integer that randomization may return.</param>
    /// <param name="maxExclusive">The first integer excluded from randomization.</param>
    public IntRange(int minInclusive, int maxExclusive) :
        this(minInclusive, maxExclusive, minInclusive, maxExclusive)
    {
    }

    /// <summary>Creates a range with authored endpoints and editor slider rails.</summary>
    /// <param name="minInclusive">The lowest integer that randomization may return.</param>
    /// <param name="maxExclusive">The first integer excluded from randomization.</param>
    /// <param name="lowRail">The lowest value shown by the editor slider.</param>
    /// <param name="highRail">The highest value shown by the editor slider.</param>
    public IntRange(int minInclusive, int maxExclusive, int lowRail, int highRail)
    {
        MinInclusive = minInclusive;
        MaxExclusive = maxExclusive;
        LowRail = lowRail;
        HighRail = highRail;
    }

    /// <summary>The inclusive lower endpoint supplied to <c>UnityEngine.Random.Range(int, int)</c>.</summary>
    public int MinInclusive;

    /// <summary>The exclusive upper endpoint supplied to <c>UnityEngine.Random.Range(int, int)</c>.</summary>
    public int MaxExclusive;

    /// <summary>The lowest value spanned by the editor slider; runtime randomization does not read it.</summary>
    public int LowRail;

    /// <summary>The highest value spanned by the editor slider; runtime randomization does not read it.</summary>
    public int HighRail;
}
