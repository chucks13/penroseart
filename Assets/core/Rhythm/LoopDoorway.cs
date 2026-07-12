// The Loop doorway: the focus deck's rolling loop as a Span (beat-data spec).

#nullable enable

/// <summary>
/// The focus deck's loop, one Data Surface doorway. Focus-only wire truth: if several live
/// players loop, only the focus player's loop appears here. The Span is the <em>rolling</em> loop
/// — audio actually cycling — while <see cref="RegionSet"/> answers the separate question "does a
/// loop region exist" (a paused player can hold a set region with nothing rolling). Facts are
/// nullable; the span's edges are never null.
/// </summary>
public readonly struct LoopView
{
    /// <summary>
    /// The rolling loop as a Span. Loops repeat beat numbers — absolute track progress goes stale
    /// inside one. The wire reports no position within the loop cycle, so the span's Progress is
    /// null and its Stock Envelopes rest at 0; the loop's length facts are what it serves.
    /// </summary>
    public SpanView<LoopFacts> Span { get; }

    /// <summary>
    /// A loop region exists on the focus deck (it can persist while playback is paused). Answers
    /// a different question than the Span: rolling and set are independent wire truths.
    /// </summary>
    public bool? RegionSet { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal LoopView(SpanView<LoopFacts> span, bool? regionSet)
    {
        Span = span;
        RegionSet = regionSet;
    }
}

/// <summary>Facts while a loop rolls. Fractional loops are real (a 1/2-beat loop is 0.5).</summary>
public readonly struct LoopFacts
{
    /// <summary>Measured region length in beats; 0 is the wire's real answer "no measurable region".</summary>
    public float? LengthBeats { get; }

    /// <summary>Measured region duration in whole milliseconds.</summary>
    public int? LengthMs { get; }

    /// <summary>
    /// Nominal quantized size in beats (the wire's size fraction, numerator over denominator),
    /// when the player reported one. Can differ from the measured region.
    /// </summary>
    public float? NominalSizeBeats { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal LoopFacts(float? lengthBeats, int? lengthMs, float? nominalSizeBeats)
    {
        LengthBeats = lengthBeats;
        LengthMs = lengthMs;
        NominalSizeBeats = nominalSizeBeats;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Loop doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public LoopView Loop { get; private set; }

    /// <summary>
    /// Prior observed rolling state (true rolling, false known idle, null lane unavailable),
    /// retained between hub updates so the span's edges genuinely witness the loop engaging and
    /// releasing (ADR-0015).
    /// </summary>
    private bool? previousLoopInside;

    /// <summary>
    /// Captures the Loop doorway from the settled transport state. The span is inside exactly
    /// while the wire reports looping audio rolling (<c>active == 1</c>); an unavailable lane
    /// (<c>active == -1</c>, the contract's complete all-sentinel shape) serves nothing. The
    /// region flag translates its own tri-state, so a set-but-idle region (<c>active 0, set 1</c>)
    /// reads as real data outside the span.
    /// </summary>
    private LoopView CaptureLoop()
    {
        var state = beatData.snapshot.loopState;
        var inside = state.active == 1;
        var started = Edges.SpanStarted(previousLoopInside, inside);
        var ended = Edges.SpanEnded(previousLoopInside, inside);
        previousLoopInside = state.active >= 0 ? inside : (bool?)null;

        var facts = inside
            ? new LoopFacts(
                state.lengthBeats >= 0f ? state.lengthBeats : (float?)null,
                NonNegativeOrNull(state.lengthMs),
                NominalSizeOrNull(state.sizeNumerator, state.sizeDenominator))
            : (LoopFacts?)null;

        // No elapsed anchor: the wire reports no position within the loop cycle, so Progress and
        // the envelopes stay at their resting state whatever the loop's length.
        var span = new SpanView<LoopFacts>(facts, progress: null, started, ended,
            elapsedBeats: null, lengthBeats: null);
        return new LoopView(span, TriStateOrNull(state.set));
    }

    /// <summary>
    /// The nominal loop size in beats from the wire's size fraction. A denominator greater than
    /// zero means a nominal size is available (contract rule); a 0/0 fraction is "none reported",
    /// never a zero-beat loop.
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
