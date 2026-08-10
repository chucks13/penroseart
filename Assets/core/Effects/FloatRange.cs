// Defines the shared float endpoint-pair shape used by Effect Settings with editor tuning rails.
using System;

/// <summary>
/// A lower and upper numeric endpoint pair used by Effect Settings, with serialized editor slider
/// rails that do not affect effect evaluation.
/// </summary>
[Serializable]
public sealed class FloatRange
{
    /// <summary>Creates an empty range for Unity serialization.</summary>
    public FloatRange() : this(0f, 0f, 0f, 1f)
    {
    }

    /// <summary>Creates an endpoint pair whose editor rails start at its authored endpoints.</summary>
    public FloatRange(float min, float max) : this(min, max, min, max)
    {
    }

    /// <summary>Creates an endpoint pair with explicit editor slider rails.</summary>
    /// <param name="min">The authored lower endpoint.</param>
    /// <param name="max">The authored upper endpoint.</param>
    /// <param name="lowRail">The lowest value shown by the editor slider.</param>
    /// <param name="highRail">The highest value shown by the editor slider.</param>
    public FloatRange(float min, float max, float lowRail, float highRail)
    {
        Min = min;
        Max = max;
        LowRail = lowRail;
        HighRail = highRail;
    }

    /// <summary>The authored lower endpoint.</summary>
    public float Min;

    /// <summary>The authored upper endpoint.</summary>
    public float Max;

    /// <summary>The lowest value spanned by the editor slider; effect evaluation does not read it.</summary>
    public float LowRail;

    /// <summary>The highest value spanned by the editor slider; effect evaluation does not read it.</summary>
    public float HighRail;
}
