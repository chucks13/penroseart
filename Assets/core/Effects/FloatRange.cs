// Defines the shared explicit min/max shape used when an Effect setting is a random float range.
using System;

/// <summary>
/// An explicit inclusive-minimum, inclusive-maximum range used by Effect Settings, with serialized
/// editor slider rails that runtime randomization ignores.
/// </summary>
[Serializable]
public sealed class FloatRange
{
    /// <summary>Creates an empty range for Unity serialization.</summary>
    public FloatRange() : this(0f, 0f, 0f, 1f)
    {
    }

    /// <summary>Creates a range from its authored minimum and maximum.</summary>
    public FloatRange(float min, float max) : this(min, max, min, max)
    {
    }

    /// <summary>Creates a range with authored endpoints and editor slider rails.</summary>
    /// <param name="min">The lower endpoint supplied to randomization.</param>
    /// <param name="max">The upper endpoint supplied to randomization.</param>
    /// <param name="lowRail">The lowest value shown by the editor slider.</param>
    /// <param name="highRail">The highest value shown by the editor slider.</param>
    public FloatRange(float min, float max, float lowRail, float highRail)
    {
        Min = min;
        Max = max;
        LowRail = lowRail;
        HighRail = highRail;
    }

    /// <summary>The authored lower endpoint supplied to randomization.</summary>
    public float Min;

    /// <summary>The authored upper endpoint supplied to randomization.</summary>
    public float Max;

    /// <summary>The lowest value spanned by the editor slider; runtime randomization does not read it.</summary>
    public float LowRail;

    /// <summary>The highest value spanned by the editor slider; runtime randomization does not read it.</summary>
    public float HighRail;
}
