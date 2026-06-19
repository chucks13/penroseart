/// <summary>
/// Result of asking whether the Director should issue a Synced Mode transition cue on the current beat.
/// </summary>
public enum SyncedCueDecisionKind
{
    /// <summary>No cue should be issued on this beat.</summary>
    Wait,

    /// <summary>The current beat is inside the transition runway and cadence permits the cue.</summary>
    Cue,

    /// <summary>The current beat is inside the runway, but the selected impact would violate change cadence.</summary>
    BlockedByCadence,
}

/// <summary>
/// Evaluates the beat-denominated cue window for a selected Synced Mode Phase Boundary.
/// </summary>
public readonly struct SyncedCueDecision
{
    public readonly SyncedCueDecisionKind Kind;
    public readonly TransitionBeatPlan BeatPlan;
    public readonly int CurrentBeat;
    public readonly int BeatsUntilImpact;

    private SyncedCueDecision(SyncedCueDecisionKind kind, TransitionBeatPlan beatPlan, int currentBeat)
    {
        Kind = kind;
        BeatPlan = beatPlan;
        CurrentBeat = currentBeat;
        BeatsUntilImpact = beatPlan.ImpactBeat - currentBeat;
    }

    public bool ShouldCue => Kind == SyncedCueDecisionKind.Cue;

    public bool BlockedByCadence => Kind == SyncedCueDecisionKind.BlockedByCadence;

    /// <summary>
    /// Builds the transition beat plan and decides whether the current beat should issue its cue.
    /// </summary>
    public static SyncedCueDecision Evaluate(
        int currentBeat,
        int selectedPhaseBoundary,
        TransitionRepertoire transitionRepertoire,
        int? lastCueBeat,
        int? previousSelectedPhaseBoundary,
        int minimumChangeCadenceBeats)
    {
        var beatPlan = TransitionBeatPlan.FromSelectedPhaseBoundary(selectedPhaseBoundary, transitionRepertoire);
        if (lastCueBeat == currentBeat)
        {
            return new SyncedCueDecision(SyncedCueDecisionKind.Wait, beatPlan, currentBeat);
        }

        if (!beatPlan.IsCueBeat(currentBeat))
        {
            return new SyncedCueDecision(SyncedCueDecisionKind.Wait, beatPlan, currentBeat);
        }

        if (!ChangeCadence.CanChangeAt(selectedPhaseBoundary, previousSelectedPhaseBoundary, minimumChangeCadenceBeats))
        {
            return new SyncedCueDecision(SyncedCueDecisionKind.BlockedByCadence, beatPlan, currentBeat);
        }

        return new SyncedCueDecision(SyncedCueDecisionKind.Cue, beatPlan, currentBeat);
    }
}
