// The Track doorway: deck and track identity (beat-data spec).

#nullable enable

using System;
using System.Collections.Generic;

/// <summary>
/// Deck and track identity, one Data Surface doorway. The playing track is a labeled state, so it
/// carries a Changed edge keyed on identity — the (trackId, title) pair — never on the display
/// label alone. Facts are nullable; the edge is never null and rests at false.
/// </summary>
public readonly struct TrackView
{
    /// <summary>
    /// Live player numbers, newest entrant first; the first is the on-air focus. Empty = nobody
    /// live (a real wire answer); null = unavailable (the live source is not active, so nothing
    /// can be claimed either way).
    /// </summary>
    public IReadOnlyList<int>? PlayersLive { get; }

    /// <summary>
    /// "Title - Artist" display label. Never an identity — do not compare it to detect changes;
    /// that is what <see cref="Changed"/> is for.
    /// </summary>
    public string? TrackTitle { get; }

    /// <summary>Deprecated per-source rekordbox id. Served (ADR-0013 floor); nothing new depends on it.</summary>
    public int? TrackId { get; }

    /// <summary>
    /// Edge: the playing track's identity changed this frame — including appearing from and
    /// vanishing to nothing on air. Never null.
    /// </summary>
    public bool Changed { get; }

    /// <summary>Built only by the hub's per-update capture, with the Changed edge already evaluated.</summary>
    internal TrackView(IReadOnlyList<int>? playersLive, string? trackTitle, int? trackId, bool changed)
    {
        PlayersLive = playersLive;
        TrackTitle = trackTitle;
        TrackId = trackId;
        Changed = changed;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Track doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public TrackView Track { get; private set; }

    /// <summary>
    /// Prior observed playing-track identity — the (trackId, title) pair after sentinel
    /// translation — retained between hub updates so the Changed edge genuinely witnesses each
    /// track change (ADR-0015: the hub owns musical moment identity).
    /// </summary>
    private (int? trackId, string? title) previousTrackIdentity;

    /// <summary>
    /// Captures the Track doorway from the settled transport state. Track facts are transport
    /// facts, not clock facts: they gate on their own wire sentinels (empty string, -1), never on
    /// Synced Mode — a title can be on air before a usable beat clock is.
    /// </summary>
    private TrackView CaptureTrack()
    {
        var snapshot = beatData.snapshot;
        var title = string.IsNullOrEmpty(snapshot.track) ? null : snapshot.track;
        var trackId = snapshot.trackId >= 0 ? snapshot.trackId : (int?)null;

        var identity = (trackId, title);
        var changed = Edges.Changed(previousTrackIdentity, identity);
        previousTrackIdentity = identity;

        return new TrackView(CapturePlayersLive(), title, trackId, changed);
    }

    /// <summary>
    /// Parses the wire's CSV player list into served numbers, newest entrant first (the wire's own
    /// order). Null iff the live source is not active — with no wire there is no answer; an empty
    /// wire string is the real answer "nobody live".
    /// </summary>
    private IReadOnlyList<int>? CapturePlayersLive()
    {
        if (!IsLiveSource)
        {
            return null;
        }

        var wire = beatData.snapshot.playersLive;
        if (string.IsNullOrEmpty(wire))
        {
            return Array.Empty<int>();
        }

        var tokens = wire.Split(',');
        var players = new List<int>(tokens.Length);
        foreach (var token in tokens)
        {
            if (int.TryParse(token, out var number))
            {
                players.Add(number);
            }
        }

        return players;
    }
}
