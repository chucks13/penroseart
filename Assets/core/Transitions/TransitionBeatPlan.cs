/// <summary>
/// Beat-domain timing for an A-to-B Transition around a Cue Mark.
/// </summary>
public readonly struct TransitionBeatPlan
{
    /// <summary>Absolute beat where the transition should start.</summary>
    public readonly int StartBeat;

    /// <summary>Cue Mark where the transition's Impact Point should land.</summary>
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
    /// Creates the beat plan for a transition whose Impact Point lands on the Cue Mark.
    /// </summary>
    public static TransitionBeatPlan FromCueMark(int cueMarkBeat, TransitionRepertoire repertoire)
    {
        return new TransitionBeatPlan(
            cueMarkBeat - repertoire.RunwayBeats,
            cueMarkBeat,
            cueMarkBeat + repertoire.TailBeats);
    }

    /// <summary>
    /// Whether this beat can start the plan: any beat from Runway start through completion.
    /// A start after the Runway begins is late; the Switcher backdates its progress from the Cue Mark.
    /// </summary>
    public bool IsCueBeat(int beat)
    {
        return beat >= StartBeat && beat <= CompleteBeat;
    }
}
