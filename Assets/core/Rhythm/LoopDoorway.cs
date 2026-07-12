// The Loop doorway: the focus deck's set region and rolling loop Span (beat-data spec).

#nullable enable

/// <summary>
/// The focus deck's loop, one Data Surface doorway. Focus-only wire truth: if several live
/// players loop, only the focus player's loop appears here. <see cref="Region"/> describes the set
/// region whether idle or rolling, <see cref="Span"/> is the <em>rolling</em> loop — audio actually
/// cycling — and <see cref="RegionSet"/> answers whether a region exists. Facts are nullable; the
/// span's edges are never null.
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
    /// The set region's measured and nominal facts, whether the region is idle or rolling. Null
    /// when the lane is unavailable or none of its region facts are valid. <see cref="RegionSet"/>
    /// answers whether a region exists; <see cref="Span"/> answers whether it is rolling.
    /// </summary>
    public LoopFacts? Region { get; }

    /// <summary>
    /// A loop region exists on the focus deck (it can persist while playback is paused). Answers
    /// a different question than the Span: rolling and set are independent wire truths.
    /// </summary>
    public bool? RegionSet { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal LoopView(SpanView<LoopFacts> span, LoopFacts? region, bool? regionSet)
    {
        Span = span;
        Region = region;
        RegionSet = regionSet;
    }
}

/// <summary>Facts describing a loop region. Fractional loops are real (a 1/2-beat loop is 0.5).</summary>
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
    /// region flag and facts remain available outside the span, so a set-but-idle region
    /// (<c>active 0, set 1</c>) keeps all valid wire facts.
    /// </summary>
    private LoopView CaptureLoop()
    {
        var state = beatData.snapshot.loopState;
        var inside = state.active == 1;
        var started = Edges.SpanStarted(previousLoopInside, inside);
        var ended = Edges.SpanEnded(previousLoopInside, inside);
        previousLoopInside = state.active >= 0 ? inside : (bool?)null;

        var lengthBeats = state.lengthBeats >= 0f ? state.lengthBeats : (float?)null;
        var lengthMs = NonNegativeOrNull(state.lengthMs);
        var nominalSizeBeats = NominalSizeOrNull(state.sizeNumerator, state.sizeDenominator);
        var translatedFacts = new LoopFacts(lengthBeats, lengthMs, nominalSizeBeats);
        var region = state.active >= 0
            && (lengthBeats.HasValue || lengthMs.HasValue || nominalSizeBeats.HasValue)
                ? translatedFacts
                : (LoopFacts?)null;
        var spanFacts = inside ? translatedFacts : (LoopFacts?)null;

        // No elapsed anchor: the wire reports no position within the loop cycle, so Progress and
        // the envelopes rest until an elapsed anchor exists. The measured length is still the
        // span's length anchor while rolling.
        var span = new SpanView<LoopFacts>(spanFacts, progress: null, started, ended,
            elapsedBeats: null, lengthBeats: inside ? lengthBeats : null);
        return new LoopView(span, region, TriStateOrNull(state.set));
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
