/// <summary>
/// Beat-domain timing for an A-to-B Transition around a Selected Phase Boundary.
/// </summary>
public readonly struct TransitionBeatPlan
{
    /// <summary>Absolute beat where the transition should start.</summary>
    public readonly int StartBeat;

    /// <summary>Selected Phase Boundary where the transition's Impact Point should land.</summary>
    public readonly int ImpactBeat;

    /// <summary>Absolute beat where the transition should fully complete.</summary>
    public readonly int CompleteBeat;

    private TransitionBeatPlan(int startBeat, int impactBeat, int completeBeat)
    {
        StartBeat = startBeat;
        ImpactBeat = impactBeat;
        CompleteBeat = completeBeat;
    }

    /// <summary>
    /// Creates the beat plan for a transition whose Impact Point lands on the Selected Phase Boundary.
    /// </summary>
    public static TransitionBeatPlan FromSelectedPhaseBoundary(int selectedPhaseBoundary, TransitionRepertoire repertoire)
    {
        return new TransitionBeatPlan(
            selectedPhaseBoundary - repertoire.RunwayBeats,
            selectedPhaseBoundary,
            selectedPhaseBoundary + repertoire.TailBeats);
    }

    /// <summary>Whether the beat should start this plan; zero-Runway cuts cue on the Impact Beat.</summary>
    public bool IsCueBeat(int beat)
    {
        return StartBeat == ImpactBeat
            ? beat == ImpactBeat
            : beat >= StartBeat && beat < ImpactBeat;
    }
}
