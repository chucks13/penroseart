// Edge identity rules shared by every Data Surface doorway (ADR-0015: the hub owns musical moment identity).

#nullable enable

using System.Collections.Generic;

/// <summary>
/// The edge identity rules every doorway capture reuses. An Edge is a polled, frame-coherent,
/// payload-free read: the hub evaluates these once per hub update, ahead of effect Draw, and
/// serves the result inside the captured views — true during exactly the frame the hub observed
/// the moment, false again on the next update. At most one edge per frame; boundaries are never
/// queued (ADR-0015). Edges are never null: with no data they rest at false, so they wire straight
/// into consumer logic without branching.
/// </summary>
internal static class Edges
{
    /// <summary>
    /// A rhythm gate's opening: the served gate moved from closed-or-unavailable to open. Null is
    /// "not available", not "closed with certainty" — but a gate first observed open still opened
    /// from this hub's point of view, so null → true counts as an opening.
    /// </summary>
    internal static bool Opened(bool? previous, bool? current)
    {
        return current == true && previous != true;
    }

    /// <summary>
    /// A cycle's wrap during continuous presence: the phase stepped backward between two consecutive
    /// observations that both carried a phase. Appearing (null → value) is not a wrap — a clock
    /// starting is a mode change, not a boundary the music crossed.
    /// </summary>
    internal static bool Wrapped(float? previousPhase, float? currentPhase)
    {
        return previousPhase is { } previous && currentPhase is { } current && current < previous;
    }

    /// <summary>
    /// A labeled state's change: the current identity differs from the prior observed identity,
    /// including changes to and from null — appearing and vanishing are changes. Identity, never a
    /// display label: callers compare the datum that names the state (e.g. the (trackId, title) pair).
    /// </summary>
    internal static bool Changed<T>(T previous, T current)
    {
        return !EqualityComparer<T>.Default.Equals(previous, current);
    }

    /// <summary>
    /// A Span's onset: the hub observed known-outside → inside, or — for Spans that are also
    /// labeled states (Phrase, Energy run) — a new identity during continuous presence, which is
    /// one span ending and another beginning on the same frame. First observation of an
    /// already-running span (null → inside: sync starting mid-drop) is NOT an onset — the music
    /// crossed that boundary unobserved, and firing here would recreate the mid-event false-flash
    /// bug this hub exists to kill (ADR-0015). Mid-event activation reads <c>Current</c> instead.
    /// </summary>
    internal static bool SpanStarted(bool? previousInside, bool currentInside, bool identityChanged = false)
    {
        return currentInside && (previousInside == false || (previousInside == true && identityChanged));
    }

    /// <summary>
    /// A Span's close: it was inside and now is not — including the facts vanishing outright
    /// (signals outlive facts: the Ended edge fires the frame they go) — or its identity changed
    /// during continuous presence (a back-to-back boundary ends the old span as it starts the new).
    /// </summary>
    internal static bool SpanEnded(bool? previousInside, bool currentInside, bool identityChanged = false)
    {
        return previousInside == true && (!currentInside || identityChanged);
    }
}
