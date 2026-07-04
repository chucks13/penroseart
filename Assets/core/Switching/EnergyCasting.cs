#nullable enable

/// <summary>
/// Maps live phrase Energy to the <see cref="Repertoire"/> the Director should prefer when casting a
/// Performer for a Cue. Energy drives effect casting only — it never creates Cue Marks and never outranks
/// an event intent's Drop/Fill preference; the Director folds this in behind those (see
/// <see cref="SyncedCueIntent.Cast"/>).
/// </summary>
public static class EnergyCasting
{
    /// <summary>
    /// The energy-affinity <see cref="Repertoire"/> to prefer for a Performer cast at <paramref name="impactBeat"/>.
    /// If an energy change lands at or within <paramref name="castAheadBeats"/> after the impact, the incoming
    /// level is preferred — the cast Performer spends its cadence stint in the new energy, so it is staged for
    /// where the track is going (the same cast-ahead shape as <see cref="CueEventIntent.DropApproaching"/>).
    /// Otherwise the current level is preferred. Returns <see cref="Repertoire.None"/> when Energy is
    /// unavailable (Standalone, or off-air), so casting falls back to the staged choice.
    /// </summary>
    /// <param name="energy">Live Energy query, or null when unavailable.</param>
    /// <param name="currentBeat">The current on-air beat; the frame <paramref name="energy"/>'s countdown is measured from.</param>
    /// <param name="impactBeat">Absolute beat the cast Performer takes the stage on.</param>
    /// <param name="castAheadBeats">The Performer's minimum cadence stint; a change within this window after impact casts ahead.</param>
    public static Repertoire PreferredEnergyRepertoire(EnergyInfo? energy, int currentBeat, int impactBeat, int castAheadBeats)
    {
        if (energy is not { } value)
        {
            return Repertoire.None;
        }

        var level = value.level;
        if (value.next is { } next && value.beatsUntilChange is { } beatsUntilChange)
        {
            var beatsUntilImpact = impactBeat - currentBeat;
            if (beatsUntilChange >= beatsUntilImpact && beatsUntilChange <= beatsUntilImpact + castAheadBeats)
            {
                level = next;
            }
        }

        return RepertoireForLevel(level);
    }

    /// <summary>Maps a closed <see cref="EnergyLevel"/> to its energy-affinity <see cref="Repertoire"/> flag.</summary>
    private static Repertoire RepertoireForLevel(EnergyLevel level)
    {
        return level switch
        {
            EnergyLevel.Low => Repertoire.EnergyLow,
            EnergyLevel.Mid => Repertoire.EnergyMid,
            EnergyLevel.High => Repertoire.EnergyHigh,
            _ => Repertoire.None,
        };
    }
}
