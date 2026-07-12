// The uniform Span view every event doorway serves (beat-data spec: one shape, six users).

#nullable enable

using UnityEngine;

/// <summary>
/// The one spelling of the Stock Envelope curve, shared by <see cref="SpanView{TFacts}"/> and
/// <see cref="GridView"/> so it can never fork: the curve character is fixed — that is what
/// "stock" means; there is no curve factory. Both shapes ride one anchor, beats elapsed since
/// the span's start over a window in beats, and rest at 0 whenever the anchor is absent —
/// "no gesture" is the honest idle state. Implementation, not a companion view type: callers
/// own their anchoring (a span's own length, the Grid's 16-beat cycle re-anchoring at the wrap).
/// </summary>
internal static class StockEnvelopes
{
    /// <summary>Build: rises 0→1 across the window, then holds at 1; rests at 0 with no anchor.</summary>
    internal static float Build(float? elapsedBeats, float? windowBeats)
    {
        return Position(elapsedBeats, windowBeats) is { } progress
            ? Mathf.SmoothStep(0f, 1f, progress)
            : 0f;
    }

    /// <summary>Decay: peaks at 1 on the anchor and falls to 0 across the window; rests at 0 with no anchor.</summary>
    internal static float Decay(float? elapsedBeats, float? windowBeats)
    {
        return Position(elapsedBeats, windowBeats) is { } progress
            ? 1f - Mathf.SmoothStep(0f, 1f, progress)
            : 0f;
    }

    /// <summary>
    /// The shared anchoring: elapsed beats over the window, clamped to 0..1. Null when there is
    /// no anchor or no usable window — the resting state both curves read as 0.
    /// </summary>
    private static float? Position(float? elapsedBeats, float? windowBeats)
    {
        if (elapsedBeats is not { } elapsed || windowBeats is not { } window || window <= 0f)
        {
            return null;
        }

        return Mathf.Clamp01(elapsed / window);
    }
}

/// <summary>
/// The uniform view of a Span — anything musical with an inside: a start, an extent, an end
/// (Fill, Drop, Phrase, Energy run, Loop). One shape, learned once: nullable facts while inside,
/// never-null Started/Ended Edges, and the Build/Decay Stock Envelopes. Facts are nullable;
/// signals are not — edges rest at false and envelopes rest at 0, so they wire straight into
/// rendering math and degrade to "no gesture" without branching. Detect a running event from
/// <see cref="Current"/> (mid-event activation works from Span facts alone), never from
/// remaining-occurrence counts, which can read 0 while the event still runs.
/// </summary>
/// <typeparam name="TFacts">The concept's facts while inside the span (e.g. <c>DropFacts</c>).</typeparam>
public readonly struct SpanView<TFacts> where TFacts : struct
{
    /// <summary>
    /// Beats elapsed since the span's start, smoothed by the shared intra-beat clock. Null when
    /// not inside the span or when the wire's count/length cannot anchor an elapsed position —
    /// the one value both <see cref="Progress"/> and the Stock Envelopes anchor on.
    /// </summary>
    private readonly float? elapsedBeats;

    /// <summary>The span's own length in beats — the Stock Envelopes' default window.</summary>
    private readonly float? lengthBeats;

    /// <summary>The span's concept facts while inside one; null = not inside (or not knowing of) this span right now.</summary>
    public TFacts? Current { get; }

    /// <summary>0..1 position through the span. Null when not inside or its length is unknown.</summary>
    public float? Progress { get; }

    /// <summary>Edge: this span began this frame. Never null; rests at false.</summary>
    public bool Started { get; }

    /// <summary>Edge: this span ended this frame — including the frame its facts vanish. Never null; rests at false.</summary>
    public bool Ended { get; }

    /// <summary>
    /// Stock Envelope: rises 0→1 across its window, anchored at the span's start. Duration in
    /// beats; null = the span's own length. Past its window it holds at 1 until the span ends;
    /// outside the span it rests at 0. The curve character is fixed — that is what "stock" means;
    /// a different response hand-rolls from facts and Edges, which stays fully free.
    /// </summary>
    public float Build(float? durationBeats = null)
    {
        return StockEnvelopes.Build(elapsedBeats, durationBeats ?? lengthBeats);
    }

    /// <summary>
    /// Stock Envelope: peaks at 1 on the span's start and falls to 0 across its window. Same
    /// duration rule as <see cref="Build"/>; past its window and outside the span it rests at 0.
    /// </summary>
    public float Decay(float? durationBeats = null)
    {
        return StockEnvelopes.Decay(elapsedBeats, durationBeats ?? lengthBeats);
    }

    /// <summary>
    /// Built only by the hub's per-update capture, with edges already evaluated and the elapsed
    /// anchor baked in — a captured view stays frame-coherent however late it is read.
    /// </summary>
    internal SpanView(TFacts? current, float? progress, bool started, bool ended,
        float? elapsedBeats, float? lengthBeats)
    {
        Current = current;
        Progress = progress;
        Started = started;
        Ended = ended;
        this.elapsedBeats = elapsedBeats;
        this.lengthBeats = lengthBeats;
    }
}
