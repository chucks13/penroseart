// Defines the shared explicit min/max shape used when an Effect setting is a random float range.
using System;

/// <summary>An explicit inclusive-minimum, inclusive-maximum range used by Effect Settings.</summary>
[Serializable]
public sealed class FloatRange
{
    /// <summary>Creates an empty range for Unity serialization.</summary>
    public FloatRange()
    {
    }

    /// <summary>Creates a range from its authored minimum and maximum.</summary>
    public FloatRange(float min, float max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The authored lower endpoint supplied to randomization.</summary>
    public float Min;

    /// <summary>The authored upper endpoint supplied to randomization.</summary>
    public float Max;
}
