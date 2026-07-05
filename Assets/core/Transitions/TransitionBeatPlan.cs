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
    /// Beat the plan locks: one beat before the Runway start. From the Lock Point on, the loaded cue
    /// fires as-is; the Director's last chance to commit or change it is the beat before.
    /// </summary>
    public int LockPointBeat => StartBeat - 1;

    /// <summary>
    /// Whether a cue for this plan can still commit on this beat: strictly before the Lock Point.
    /// A later commit does not cue at all — the Switcher never starts a transition behind its plan,
    /// so progress always runs forward from the Start Beat (no backdating, no accidental hard cut).
    /// </summary>
    public bool CanCommitAt(int beat)
    {
        return beat < LockPointBeat;
    }
}
