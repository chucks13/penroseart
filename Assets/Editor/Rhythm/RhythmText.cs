// Editor-side display-text formatting for BeatManager's contrived rhythm queries (ADR-0012): nullable
// beat and count values rendered for the BeatManager dashboard. Lives in the editor assembly with its
// only consumers; the rhythm-query data it formats stays in the runtime assembly.

#nullable enable

/// <summary>
/// Formats the nullable beat/count values of BeatManager's rhythm queries (phrase events, energy,
/// grid) into display text, rendering null as "—". One vocabulary so every rhythm-query readout
/// reads the same.
/// </summary>
public static class RhythmText
{
    /// <summary>A whole-beat count as "{n}b", or "—" when null.</summary>
    public static string Beats(int? value)
    {
        return value is { } beats ? $"{beats}b" : "—";
    }

    /// <summary>A plain count as "{n}", or "—" when null.</summary>
    public static string Count(int? value)
    {
        return value is { } count ? count.ToString() : "—";
    }
}
