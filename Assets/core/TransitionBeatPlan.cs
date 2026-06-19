/// <summary>
/// Beat-domain timing for an A-to-B Transition around a Selected Impact Beat.
/// </summary>
public readonly struct TransitionBeatPlan
{
    /// <summary>Absolute beat where the transition should start.</summary>
    public readonly int StartBeat;

    /// <summary>Selected Impact Beat where the transition's Impact Point should land.</summary>
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
    /// Creates the beat plan for a transition whose Impact Point lands on the selected impact beat.
    /// </summary>
    public static TransitionBeatPlan FromImpactBeat(int impactBeat, TransitionRepertoire repertoire)
    {
        return new TransitionBeatPlan(
            impactBeat - repertoire.RunwayBeats,
            impactBeat,
            impactBeat + repertoire.TailBeats);
    }

    /// <summary>Whether the beat is inside the Runway window before the Impact Point.</summary>
    public bool IsCueBeat(int beat)
    {
        return beat >= StartBeat && beat < ImpactBeat;
    }
}
