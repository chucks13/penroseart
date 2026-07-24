// Captures the newest-first order of players in the live set.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>Immutable live-player order captured for one BeatManager update.</summary>
public readonly struct LiveOrderValues
{
    /// <summary>Captured player numbers in newest-first order; null only in a default value.</summary>
    private readonly ReadOnlyCollection<int>? players;

    /// <summary>Captures valid player numbers in newest-first order.</summary>
    internal LiveOrderValues(int[] players)
    {
        this.players = Array.AsReadOnly(players);
    }

    /// <summary>Player numbers in newest-first live-set order.</summary>
    public IReadOnlyList<int> Players => (IReadOnlyList<int>?)players ?? Array.Empty<int>();

    /// <summary>The on-air focus player number, or null when the live order is empty.</summary>
    public int? Focus => players is { Count: > 0 } ? players[0] : null;
}

public partial class BeatManager
{
    /// <summary>The newest-first live-player order and its on-air focus.</summary>
    public LiveOrderValues LiveOrder { get; private set; }

    /// <summary>Captures the live-order wire lane.</summary>
    private LiveOrderValues CaptureLiveOrder() => TranslateLiveOrder(wireSnapshot.playersLive);

    /// <summary>
    /// Translates the comma-separated live-order lane, preserving valid player order while
    /// ignoring malformed or out-of-range tokens.
    /// </summary>
    private static LiveOrderValues TranslateLiveOrder(string? wire)
    {
        if (string.IsNullOrEmpty(wire))
        {
            return new LiveOrderValues(Array.Empty<int>());
        }

        var players = new List<int>();
        foreach (var token in wire.Split(','))
        {
            if (int.TryParse(token, out var player) &&
                player is >= 1 and <= PenroseArt.RaveOsc.RaveWireSnapshot.PlayerCount)
            {
                players.Add(player);
            }
        }

        return new LiveOrderValues(players.ToArray());
    }
}
