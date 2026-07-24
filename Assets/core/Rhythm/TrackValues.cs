// Captures live-player and track identity wire values.

#nullable enable

/// <summary>Immutable track values captured for one BeatManager update.</summary>
public readonly struct TrackValues
{
    /// <summary>Captures track identity values.</summary>
    internal TrackValues(string? title, int? id)
    {
        Title = title;
        Id = id;
    }

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
            string.IsNullOrEmpty(snapshot.track) ? null : snapshot.track,
            snapshot.trackId >= 0 ? snapshot.trackId : null);
    }
}
