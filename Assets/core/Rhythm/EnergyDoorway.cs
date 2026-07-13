// The Energy doorway: macro intensity — the current run as a Span, the next level, the trend
// (beat-data spec). Also home of the one closed Energy ladder.

#nullable enable

using System;

/// <summary>
/// The one closed intensity ladder — Low, Mid, High — the shared vocabulary wherever intensity is
/// spoken, whatever the subject: a track's Energy run here, a Waveform's derived Energy in the
/// Waveforms surface. Track-relative by wire contract: a Low is this track's Low, never absolute loudness.
/// </summary>
public enum Energy
{
    /// <summary>The track's lower intensity tier.</summary>
    Low,

    /// <summary>The middle tier — spelled Mid, never "Medium".</summary>
    Mid,

    /// <summary>The track's peak intensity tier.</summary>
    High,
}

/// <summary>Where the energy is heading, from comparing the current run's level to the next different run's.</summary>
public enum EnergyTrend
{
    /// <summary>The next known run sits lower on the ladder.</summary>
    Falling,

    /// <summary>No different run is known ahead — the music holds this level as far as the analysis sees.</summary>
    Steady,

    /// <summary>The next known run sits higher on the ladder. "Rising, change in 8 beats" is the build-up signal.</summary>
    Rising,
}

/// <summary>
/// Macro intensity on the shared Low/Mid/High ladder, one Data Surface doorway. The current run
/// of same-level phrases is a Span; the next different level and the trend are the build-up
/// signals — the macro intensity control above per-beat reactivity. Facts are nullable; the edges
/// are never null and rest at false.
/// </summary>
public readonly struct EnergyView
{
    /// <summary>
    /// The current Energy run as a Span. Its length covers the complete run, including same-level
    /// phrases already played, so mid-run activation still reads an honest position.
    /// </summary>
    public SpanView<EnergyFacts> Run { get; }

    /// <summary>Edge: the energy level changed this frame — including appearing from and vanishing to unavailable. Never null.</summary>
    public bool Changed { get; }

    /// <summary>The upcoming different level. Null when no different run is known ahead.</summary>
    public Energy? NextLevel { get; }

    /// <summary>Beats until that different run begins (1 on the beat before the change).</summary>
    public int? NextChangeInBeats { get; }

    /// <summary>The complete upcoming run's length in beats.</summary>
    public int? NextRunLengthBeats { get; }

    /// <summary>
    /// Where the energy is heading. Contrived from the current and next levels over the ladder;
    /// Steady when the current level is known and no different next run is announced — the wire
    /// only reports a next run whose level differs, so its absence is the analysis saying "holding".
    /// Null when the current level itself is unknown.
    /// </summary>
    public EnergyTrend? Trend { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated and edges evaluated.</summary>
    internal EnergyView(SpanView<EnergyFacts> run, bool changed, Energy? nextLevel, int? nextChangeInBeats,
        int? nextRunLengthBeats, EnergyTrend? trend)
    {
        Run = run;
        Changed = changed;
        NextLevel = nextLevel;
        NextChangeInBeats = nextChangeInBeats;
        NextRunLengthBeats = nextRunLengthBeats;
        Trend = trend;
    }
}

/// <summary>Facts of the current Energy run.</summary>
public readonly struct EnergyFacts
{
    /// <summary>The current level. Track-relative — a Low here is this track's Low.</summary>
    public Energy Level { get; }

    /// <summary>Beats remaining in the run, including the current beat.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>The complete run's length in beats, including same-level phrases already played.</summary>
    public int? LengthBeats { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal EnergyFacts(Energy level, int? beatsRemaining, int? lengthBeats)
    {
        Level = level;
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Energy doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame.
    /// </summary>
    public EnergyView Energy { get; private set; }

    /// <summary>
    /// Prior observed run level (null = unavailable or outside the closed vocabulary), retained
    /// between hub updates so the Changed edge and the run's boundary edges genuinely witness
    /// each level change (ADR-0015).
    /// </summary>
    private global::Energy? previousEnergyLevel;

    /// <summary>
    /// Captures the Energy doorway from the settled transport state. A run is inside its span
    /// exactly while the wire reports a level in the closed vocabulary; a level change during
    /// continuous presence is a run boundary — the old run's Ended and the new run's Started fire
    /// together (consecutive same-level phrases are one run by contract, so back-to-back
    /// same-level runs cannot occur).
    /// </summary>
    private EnergyView CaptureEnergy()
    {
        var snapshot = beatData.snapshot;
        var state = snapshot.energyState;
        var level = ParseEnergy(state.label);

        var changed = Edges.Changed(previousEnergyLevel, level);
        var previousInside = previousEnergyLevel != null ? true : (bool?)null;
        var started = Edges.SpanStarted(previousInside, level != null, changed);
        var ended = Edges.SpanEnded(previousInside, level != null, changed);
        previousEnergyLevel = level;

        var elapsed = level != null ? ElapsedInSpan(state.countBeats, state.lengthBeats) : null;
        var facts = level is { } current
            ? new EnergyFacts(current, NonNegativeOrNull(state.countBeats), NonNegativeOrNull(state.lengthBeats))
            : (EnergyFacts?)null;
        var run = new SpanView<EnergyFacts>(facts, ProgressOverLength(elapsed, state.lengthBeats),
            started, ended, elapsed, LengthOrNull(state.lengthBeats));

        var next = snapshot.nextEnergyState;
        var nextLevel = ParseEnergy(next.label);
        return new EnergyView(run, changed, nextLevel, NonNegativeOrNull(next.countBeats),
            NonNegativeOrNull(next.lengthBeats), ContriveTrend(level, nextLevel));
    }

    /// <summary>
    /// Parses a wire energy label against the closed Low/Mid/High vocabulary. An unrecognized
    /// label reads as unavailable rather than degrading to a wrong tier.
    /// </summary>
    private static global::Energy? ParseEnergy(string? label)
    {
        // global:: throughout — inside BeatManager the simple name Energy binds to the doorway
        // property, not the ladder enum.
        if (string.Equals(label, "Low", StringComparison.OrdinalIgnoreCase))
        {
            return global::Energy.Low;
        }

        if (string.Equals(label, "Mid", StringComparison.OrdinalIgnoreCase))
        {
            return global::Energy.Mid;
        }

        if (string.Equals(label, "High", StringComparison.OrdinalIgnoreCase))
        {
            return global::Energy.High;
        }

        return null;
    }

    /// <summary>
    /// Contrives the trend from the current and next levels: the ladder comparison when a
    /// different next run is known; Steady when the current level is known and none is (the wire
    /// only announces differing runs); null when the current level itself is unknown.
    /// </summary>
    private static EnergyTrend? ContriveTrend(global::Energy? level, global::Energy? nextLevel)
    {
        if (level is not { } current)
        {
            return null;
        }

        if (nextLevel is not { } next || next == current)
        {
            return EnergyTrend.Steady;
        }

        return next > current ? EnergyTrend.Rising : EnergyTrend.Falling;
    }
}
