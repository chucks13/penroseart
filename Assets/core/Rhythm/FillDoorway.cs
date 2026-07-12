// The Fill doorway: the phrase-tail transition garnish as a Span (beat-data spec).

#nullable enable

/// <summary>
/// The phrase-tail transition garnish, one Data Surface doorway. Wire truth: the fill is selected
/// across <em>all</em> live players (soonest wins) — the live set's next fill, not necessarily the
/// focus player's — and is served as selected, never re-selected client-side. Facts are nullable;
/// the span's edges are never null.
/// </summary>
public readonly struct FillView
{
    /// <summary>The fill as a Span: facts while one runs, Started/Ended, Build/Decay.</summary>
    public SpanView<FillFacts> Span { get; }

    /// <summary>Beats until the selected upcoming fill begins (1 on the beat before it starts). Null while a fill runs.</summary>
    public int? NextInBeats { get; }

    /// <summary>Length of the selected upcoming fill in beats. Null while a fill runs.</summary>
    public int? NextLengthBeats { get; }

    /// <summary>Fills remaining on the selected player's track, including the selected fill while it runs. Not a live-set total.</summary>
    public int? RemainingOnTrack { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal FillView(SpanView<FillFacts> span, int? nextInBeats, int? nextLengthBeats, int? remainingOnTrack)
    {
        Span = span;
        NextInBeats = nextInBeats;
        NextLengthBeats = nextLengthBeats;
        RemainingOnTrack = remainingOnTrack;
    }
}

/// <summary>Facts while a fill runs.</summary>
public readonly struct FillFacts
{
    /// <summary>Beats remaining in the fill, including the current beat.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>The fill's total length in beats.</summary>
    public int? LengthBeats { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal FillFacts(int? beatsRemaining, int? lengthBeats)
    {
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Fill doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public FillView Fill { get; private set; }

    /// <summary>
    /// Prior observed fill presence (true inside, false counting down, null lane unavailable),
    /// retained between hub updates so the span's edges genuinely witness the onset (ADR-0015).
    /// </summary>
    private bool? previousFillInside;

    /// <summary>
    /// Captures the Fill doorway from the settled transport state: the shared countdown-lane
    /// rules (see <see cref="CountdownSpanCapture"/>) over the live set's selected fill.
    /// </summary>
    private FillView CaptureFill()
    {
        var lane = CaptureCountdownLane(beatData.snapshot.fillState, ref previousFillInside);
        var facts = lane.Inside ? new FillFacts(lane.BeatsRemaining, lane.SpanLengthBeats) : (FillFacts?)null;
        var span = new SpanView<FillFacts>(facts, lane.Progress, lane.Started, lane.Ended,
            lane.ElapsedBeats, lane.WindowBeats);
        return new FillView(span, lane.NextInBeats, lane.NextLengthBeats, lane.RemainingOnTrack);
    }
}
