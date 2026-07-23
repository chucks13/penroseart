// Captures a deck's loop wire state: the on-air focus deck and each physical player.
#nullable enable

/// <summary>Immutable mirror of one deck's loop wire state — the on-air focus (<see cref="BeatManager.Loop"/>) or a physical player (<see cref="PlayerValues.Loop"/>).</summary>
public readonly struct LoopValues
{
    /// <summary>Captures all direct loop wire values and the nominal beat length.</summary>
    internal LoopValues(bool rolling, bool regionSet, float? lengthBeats, int? lengthMilliseconds,
        int? sizeNumerator, int? sizeDenominator, float? nominalSizeBeats)
    {
        Rolling = rolling;
        RegionSet = regionSet;
        LengthBeats = lengthBeats;
        LengthMilliseconds = lengthMilliseconds;
        SizeNumerator = sizeNumerator;
        SizeDenominator = sizeDenominator;
        NominalSizeBeats = nominalSizeBeats;
    }

    /// <summary>Whether the deck's loop is rolling; false when no loop lane is available.</summary>
    public bool Rolling { get; }
    /// <summary>Whether a loop region is set; false when no loop lane is available.</summary>
    public bool RegionSet { get; }
    /// <summary>Measured loop length in beats.</summary>
    public float? LengthBeats { get; }
    /// <summary>Measured loop length in milliseconds.</summary>
    public int? LengthMilliseconds { get; }
    /// <summary>Wire numerator of the nominal quantized loop size.</summary>
    public int? SizeNumerator { get; }
    /// <summary>Wire denominator of the nominal quantized loop size.</summary>
    public int? SizeDenominator { get; }
    /// <summary>Derived nominal quantized loop size in beats.</summary>
    public float? NominalSizeBeats { get; }
}

public partial class BeatManager
{
    /// <summary>The focus deck's loop wire values.</summary>
    public LoopValues Loop { get; private set; }

    /// <summary>Captures the on-air loop wire lane.</summary>
    private LoopValues CaptureLoop() => TranslateLoop(wireSnapshot.loopState);

    /// <summary>
    /// Translates one loop wire lane — raw non-negative values, nominal size only from a positive
    /// denominator — shared by the on-air capture and the per-player captures.
    /// </summary>
    private static LoopValues TranslateLoop(in PenroseArt.RaveOsc.LoopState state)
    {
        var numerator = NonNegativeOrNull(state.sizeNumerator);
        var denominator = NonNegativeOrNull(state.sizeDenominator);
        return new LoopValues(
            TriStateTrue(state.active),
            TriStateTrue(state.set),
            state.lengthBeats >= 0f ? state.lengthBeats : null,
            NonNegativeOrNull(state.lengthMs),
            numerator,
            denominator,
            numerator is { } n && denominator is { } d && d > 0 ? n / (float)d : null);
    }
}
