// The Loop doorway: a flat, per-update mirror of the focus deck's loop_state wire lane.

#nullable enable

/// <summary>
/// Flat mirror of the focus deck's <c>loop_state</c> wire lane, captured once per hub update.
/// Loop is playback state, not a timeline event, so this view derives no span, edge, progress, or
/// envelope values. Every fact is nullable under the Data Surface's wire-sentinel rule.
/// </summary>
public readonly struct LoopView
{
    /// <summary>Whether the focus deck's loop is rolling; null when the wire reports no fact.</summary>
    public bool? Rolling { get; }

    /// <summary>
    /// Whether a loop region is set on the focus deck; null when the wire reports no fact. A set
    /// region can persist while playback is idle.
    /// </summary>
    public bool? RegionSet { get; }

    /// <summary>Measured loop length in beats; 0 is the wire's real "no measurable region" fact.</summary>
    public float? LengthBeats { get; }

    /// <summary>Measured loop duration in whole milliseconds; null for a negative wire sentinel.</summary>
    public int? LengthMs { get; }

    /// <summary>
    /// Nominal quantized loop size in beats, translated from the wire's numerator and denominator;
    /// null when the wire reports no valid fraction.
    /// </summary>
    public float? NominalSizeBeats { get; }

    /// <summary>Builds the flat view from one settled wire snapshot with sentinels already translated.</summary>
    internal LoopView(bool? rolling, bool? regionSet, float? lengthBeats, int? lengthMs,
        float? nominalSizeBeats)
    {
        Rolling = rolling;
        RegionSet = regionSet;
        LengthBeats = lengthBeats;
        LengthMs = lengthMs;
        NominalSizeBeats = nominalSizeBeats;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// Flat mirror of the focus-only loop wire lane, captured once per hub update ahead of effect
    /// Draw and identical for every reader within a frame.
    /// </summary>
    public LoopView Loop { get; private set; }

    /// <summary>
    /// Captures the flat Loop doorway from the settled focus-deck transport state, translating
    /// each wire sentinel independently and deriving no additional loop semantics.
    /// </summary>
    private LoopView CaptureLoop()
    {
        var state = beatData.snapshot.loopState;
        return new LoopView(
            TriStateOrNull(state.active),
            TriStateOrNull(state.set),
            state.lengthBeats >= 0f ? state.lengthBeats : null,
            NonNegativeOrNull(state.lengthMs),
            NominalSizeOrNull(state.sizeNumerator, state.sizeDenominator));
    }

    /// <summary>
    /// The nominal loop size in beats from the wire's size fraction. A non-positive denominator or
    /// negative numerator means "none reported"; a 0/0 fraction is never a zero-beat loop.
    /// </summary>
    private static float? NominalSizeOrNull(int sizeNumerator, int sizeDenominator)
    {
        if (sizeDenominator <= 0 || sizeNumerator < 0)
        {
            return null;
        }

        return sizeNumerator / (float)sizeDenominator;
    }
}
