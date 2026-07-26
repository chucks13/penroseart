// Captures current and explicitly named next Energy values.
#nullable enable

using System;

/// <summary>The track-relative Low, Mid, and High phrase-level intensity ladder.</summary>
public enum Energy
{
    /// <summary>The track's lower intensity tier.</summary>
    Low,
    /// <summary>The track's middle intensity tier.</summary>
    Mid,
    /// <summary>The track's higher intensity tier.</summary>
    High,
}

/// <summary>The direction from the current energy level to the next reported level.</summary>
public enum EnergyTrend
{
    /// <summary>The next known level is lower.</summary>
    Falling,
    /// <summary>No different next level is known.</summary>
    Steady,
    /// <summary>The next known level is higher.</summary>
    Rising,
}

/// <summary>Immutable current-energy wire facts and derived values.</summary>
public readonly struct EnergyValues
{
    /// <summary>Continuous elapsed position retained for Build and Decay.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Captures the current Energy run and its direct derived values.</summary>
    internal EnergyValues(Energy? level, int? beatsRemaining, int? lengthBeats,
        float? progress, EnergyTrend? trend, float? elapsedBeats)
    {
        Level = level;
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
        Progress = progress;
        Trend = trend;
        this.elapsedBeats = elapsedBeats;
    }

    /// <summary>The current track-relative energy level.</summary>
    public Energy? Level { get; }

    /// <summary>Beats remaining in the current same-level run.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>Total length of the current same-level run.</summary>
    public int? LengthBeats { get; }

    /// <summary>Derived 0..1 position through the current run.</summary>
    public float? Progress { get; }

    /// <summary>Derived direction toward the next reported energy level.</summary>
    public EnergyTrend? Trend { get; }

    /// <summary>Rises across the full energy run or requested window of whole beats.</summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the run's own length.</param>
    public float Build(int? windowBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, windowBeats ?? LengthBeats);

    /// <summary>Falls across the full energy run or requested window of whole beats.</summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the run's own length.</param>
    public float Decay(int? windowBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, windowBeats ?? LengthBeats);
}

/// <summary>Immutable facts from the wire's explicitly named next-energy lane.</summary>
public readonly struct NextEnergyValues
{
    /// <summary>Captures the explicitly named next Energy run.</summary>
    internal NextEnergyValues(Energy? level, int? beatsUntil, int? lengthBeats)
    {
        Level = level;
        BeatsUntil = beatsUntil;
        LengthBeats = lengthBeats;
    }

    /// <summary>The next different energy level.</summary>
    public Energy? Level { get; }

    /// <summary>Beats until the next different energy level begins.</summary>
    public int? BeatsUntil { get; }

    /// <summary>Total length of the next energy run.</summary>
    public int? LengthBeats { get; }
}

public partial class BeatManager
{
    /// <summary>The current energy run and its derived values.</summary>
    public EnergyValues Energy { get; private set; }

    /// <summary>The wire's explicitly named next energy run.</summary>
    public NextEnergyValues NextEnergy { get; private set; }

    /// <summary>Captures current and next Energy lanes from the settled wire snapshot.</summary>
    private void CaptureEnergyValues()
    {
        var state = wireSnapshot.energyState;
        var level = ParseEnergy(state.label);
        var elapsed = level != null ? ElapsedInDuration(state.countBeats, state.lengthBeats) : null;

        var next = wireSnapshot.nextEnergyState;
        var nextLevel = ParseEnergy(next.label);
        Energy = new EnergyValues(
            level,
            level != null ? NonNegativeOrNull(state.countBeats) : null,
            level != null ? NonNegativeOrNull(state.lengthBeats) : null,
            ProgressOverLength(elapsed, state.lengthBeats),
            ContriveTrend(level, nextLevel),
            elapsed);
        NextEnergy = new NextEnergyValues(
            nextLevel,
            nextLevel != null ? NonNegativeOrNull(next.countBeats) : null,
            nextLevel != null ? NonNegativeOrNull(next.lengthBeats) : null);
    }

    /// <summary>Translates the wire's closed Energy labels without inventing another vocabulary.</summary>
    private static Energy? ParseEnergy(string? label)
    {
        if (string.Equals(label, "Low", StringComparison.OrdinalIgnoreCase)) return global::Energy.Low;
        if (string.Equals(label, "Mid", StringComparison.OrdinalIgnoreCase)) return global::Energy.Mid;
        if (string.Equals(label, "High", StringComparison.OrdinalIgnoreCase)) return global::Energy.High;
        return null;
    }

    /// <summary>Compares current and next Energy values into a readable direction.</summary>
    private static EnergyTrend? ContriveTrend(Energy? level, Energy? nextLevel)
    {
        if (level is not { } current) return null;
        if (nextLevel is not { } next || next == current) return EnergyTrend.Steady;
        return next > current ? EnergyTrend.Rising : EnergyTrend.Falling;
    }
}
