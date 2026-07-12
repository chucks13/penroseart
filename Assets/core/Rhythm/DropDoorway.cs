// The Drop doorway: the hit-hard moment as a Span with its anticipation facts (beat-data spec).
// Also home of the countdown-lane machinery the Fill doorway reuses.

#nullable enable

/// <summary>
/// One translated countdown-state wire lane (drop_state and fill_state share the shape: active
/// tri-state, count, length, remaining — so the rules live once). The wire's count and length
/// describe the running event while active and the upcoming one while counting down; this capture
/// sorts each value into its one home, translates sentinels to null, and evaluates the span edges
/// against the prior observed presence the hub retains between updates (ADR-0015).
/// </summary>
internal readonly struct CountdownSpanCapture
{
    /// <summary>Beats remaining in the running event, including the current beat. Null unless inside.</summary>
    internal readonly int? BeatsRemaining;

    /// <summary>The running event's total length in beats. Null unless inside.</summary>
    internal readonly int? SpanLengthBeats;

    /// <summary>Beats until the upcoming event begins (1 on the beat before it starts). Null while inside.</summary>
    internal readonly int? NextInBeats;

    /// <summary>The upcoming event's total length in beats. Null while inside.</summary>
    internal readonly int? NextLengthBeats;

    /// <summary>Occurrences remaining on the track. Served in both states; never a running-event test.</summary>
    internal readonly int? RemainingOnTrack;

    /// <summary>Whether the event is running now (wire <c>active == 1</c>).</summary>
    internal readonly bool Inside;

    /// <summary>0..1 position through the running event, or null when its shape is unknown.</summary>
    internal readonly float? Progress;

    /// <summary>Beats elapsed since the event's start, smoothed by the shared sub-beat clock. Null unless anchorable.</summary>
    internal readonly float? ElapsedBeats;

    /// <summary>The running event's length as the Stock Envelopes' default window.</summary>
    internal readonly float? WindowBeats;

    /// <summary>Edge: the event began this frame (counting-down → active, witnessed).</summary>
    internal readonly bool Started;

    /// <summary>Edge: the event ended this frame — including the lane vanishing outright.</summary>
    internal readonly bool Ended;

    internal CountdownSpanCapture(int? beatsRemaining, int? spanLengthBeats, int? nextInBeats,
        int? nextLengthBeats, int? remainingOnTrack, bool inside, float? progress, float? elapsedBeats,
        float? windowBeats, bool started, bool ended)
    {
        BeatsRemaining = beatsRemaining;
        SpanLengthBeats = spanLengthBeats;
        NextInBeats = nextInBeats;
        NextLengthBeats = nextLengthBeats;
        RemainingOnTrack = remainingOnTrack;
        Inside = inside;
        Progress = progress;
        ElapsedBeats = elapsedBeats;
        WindowBeats = windowBeats;
        Started = started;
        Ended = ended;
    }
}

/// <summary>
/// The hit-hard moment, one Data Surface doorway: anticipation while counting down, full
/// commitment while inside. Focus-only wire truth — the on-air focus player's drop, served as the
/// lane says, never re-selected client-side. Facts are nullable; the span's edges are never null.
/// </summary>
public readonly struct DropView
{
    /// <summary>
    /// The drop as a Span. Detect a running drop with <c>Span.Current.HasValue</c> — never with
    /// <see cref="RemainingOnTrack"/>, which can read 0 while a drop still runs (wire contract:
    /// the occurrence count passes the drop's marker before the drop ends).
    /// </summary>
    public SpanView<DropFacts> Span { get; }

    /// <summary>Beats until the next drop begins (1 on the beat before the slam). Null while a drop runs — the wire's countdown describes the running drop then.</summary>
    public int? NextInBeats { get; }

    /// <summary>Length of the upcoming drop in beats. Null while a drop runs.</summary>
    public int? NextLengthBeats { get; }

    /// <summary>Drop occurrences whose designated drop point has not yet passed. Can read 0 while a drop still runs — never a running-drop test.</summary>
    public int? RemainingOnTrack { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal DropView(SpanView<DropFacts> span, int? nextInBeats, int? nextLengthBeats, int? remainingOnTrack)
    {
        Span = span;
        NextInBeats = nextInBeats;
        NextLengthBeats = nextLengthBeats;
        RemainingOnTrack = remainingOnTrack;
    }
}

/// <summary>Facts while a drop runs.</summary>
public readonly struct DropFacts
{
    /// <summary>Beats remaining in the drop, including the current beat.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>The drop's total length in beats.</summary>
    public int? LengthBeats { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal DropFacts(int? beatsRemaining, int? lengthBeats)
    {
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Drop doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public DropView Drop { get; private set; }

    /// <summary>
    /// Prior observed drop presence (true inside, false counting down, null lane unavailable),
    /// retained between hub updates so the span's edges genuinely witness the onset (ADR-0015).
    /// The tri-state matters: an unavailable lane appearing mid-drop never synthesizes Started.
    /// </summary>
    private bool? previousDropInside;

    /// <summary>Captures the Drop doorway from the settled transport state.</summary>
    private DropView CaptureDrop()
    {
        var lane = CaptureCountdownLane(beatData.snapshot.dropState, ref previousDropInside);
        var facts = lane.Inside ? new DropFacts(lane.BeatsRemaining, lane.SpanLengthBeats) : (DropFacts?)null;
        var span = new SpanView<DropFacts>(facts, lane.Progress, lane.Started, lane.Ended,
            lane.ElapsedBeats, lane.WindowBeats);
        return new DropView(span, lane.NextInBeats, lane.NextLengthBeats, lane.RemainingOnTrack);
    }

    /// <summary>
    /// The one spelling of the countdown-state span rules, shared by the Drop and Fill captures.
    /// An unavailable lane (<c>active == -1</c> — the contract's complete all-sentinel shape)
    /// serves nothing; while inside, count/length describe the running event; while counting
    /// down, they describe the upcoming one — each value has exactly one home per state
    /// (ADR-0013: served once, nothing dropped).
    /// </summary>
    private CountdownSpanCapture CaptureCountdownLane(PenroseArt.RaveOsc.CountdownState state,
        ref bool? previousInside)
    {
        var inside = state.active == 1;
        var started = Edges.SpanStarted(previousInside, inside);
        var ended = Edges.SpanEnded(previousInside, inside);
        previousInside = state.active >= 0 ? inside : (bool?)null;

        if (state.active < 0)
        {
            return new CountdownSpanCapture(null, null, null, null, null,
                inside: false, progress: null, elapsedBeats: null, windowBeats: null, started, ended);
        }

        var elapsed = inside ? ElapsedInSpan(state.countBeats, state.lengthBeats) : null;
        return new CountdownSpanCapture(
            inside ? NonNegativeOrNull(state.countBeats) : null,
            inside ? NonNegativeOrNull(state.lengthBeats) : null,
            inside ? null : NonNegativeOrNull(state.countBeats),
            inside ? null : NonNegativeOrNull(state.lengthBeats),
            NonNegativeOrNull(state.remaining),
            inside,
            ProgressOverLength(elapsed, state.lengthBeats),
            elapsed,
            inside ? LengthOrNull(state.lengthBeats) : null,
            started,
            ended);
    }
}
