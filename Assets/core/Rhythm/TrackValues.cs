// Captures live-player and track identity wire values.

#nullable enable

using System;
using System.Collections.Generic;

/// <summary>Immutable track values captured for one BeatManager update.</summary>
public readonly struct TrackValues
{
    /// <summary>Captures live-player and track identity values.</summary>
    internal TrackValues(IReadOnlyList<int>? playersLive, string? title, int? id)
    {
        PlayersLive = playersLive;
        Title = title;
        Id = id;
    }

    /// <summary>Live player numbers, newest entrant first; null when the live source is inactive.</summary>
    public IReadOnlyList<int>? PlayersLive { get; }

    /// <summary>Wire display label for the focused track.</summary>
    public string? Title { get; }

    /// <summary>Deprecated source track identifier, preserved because it still arrives on the wire.</summary>
    public int? Id { get; }
}

public partial class BeatManager
{
    /// <summary>Track values captured from the settled musical frame.</summary>
    public TrackValues Track { get; private set; }

    /// <summary>Captures immutable track data without manufacturing a one-frame change signal.</summary>
    private TrackValues CaptureTrack()
    {
        var snapshot = wireSnapshot;
        return new TrackValues(
            CapturePlayersLive(),
            string.IsNullOrEmpty(snapshot.track) ? null : snapshot.track,
            snapshot.trackId >= 0 ? snapshot.trackId : null);
    }

    /// <summary>Parses the compact comma-separated player list into an immutable collection.</summary>
    private IReadOnlyList<int>? CapturePlayersLive()
    {
        if (!liveBeatActive)
        {
            return null;
        }

        var wire = wireSnapshot.playersLive;
        if (string.IsNullOrEmpty(wire))
        {
            return Array.Empty<int>();
        }

        var players = new List<int>();
        foreach (var token in wire.Split(','))
        {
            if (int.TryParse(token, out var number))
            {
                players.Add(number);
            }
        }

        return players.AsReadOnly();
    }
}
