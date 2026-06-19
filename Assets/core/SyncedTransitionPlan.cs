/// <summary>
/// Beat-derived playback plan for a Synced Mode transition once the Director has issued a cue.
/// </summary>
public readonly struct SyncedTransitionPlan
{
    public readonly int TransitionIndex;
    public readonly int TargetEffectIndex;
    public readonly int StartBeat;
    public readonly int ImpactBeat;
    public readonly int CompleteBeat;
    public readonly TransitionRepertoire Repertoire;
    public readonly float StartTime;
    public readonly float DurationSeconds;
    public readonly bool Active;

    public SyncedTransitionPlan(
        int transitionIndex,
        int targetEffectIndex,
        TransitionBeatPlan beatPlan,
        TransitionRepertoire repertoire,
        float startTime,
        float secondsPerBeat)
    {
        TransitionIndex = transitionIndex;
        TargetEffectIndex = targetEffectIndex;
        StartBeat = beatPlan.StartBeat;
        ImpactBeat = beatPlan.ImpactBeat;
        CompleteBeat = beatPlan.CompleteBeat;
        Repertoire = repertoire;
        StartTime = startTime;
        DurationSeconds = repertoire.DurationBeats * secondsPerBeat;
        Active = true;
    }

    /// <summary>Frame-smooth transition progress clamped to the A-to-B transition duration.</summary>
    public float Progress(float now)
    {
        return DurationSeconds > 0f ? Clamp01((now - StartTime) / DurationSeconds) : 1f;
    }

    /// <summary>Builds the current update decision without completing the Mechanical Switcher itself.</summary>
    public SyncedTransitionUpdate EvaluateUpdate(
        int currentBeat,
        bool beatRewoundToNewPass,
        int recordedImpactBeat,
        float now)
    {
        var progress = Progress(now);
        var impactBeat = beatRewoundToNewPass ? currentBeat : recordedImpactBeat;
        return new SyncedTransitionUpdate(
            progress,
            beatRewoundToNewPass,
            impactBeat,
            progress >= 1f);
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f)
        {
            return 0f;
        }

        return value >= 1f ? 1f : value;
    }
}

/// <summary>
/// Pure update result for an in-flight Synced Mode transition.
/// </summary>
public readonly struct SyncedTransitionUpdate
{
    public readonly float Progress;
    public readonly bool RecordImpactOnRewind;
    public readonly int ImpactBeat;
    public readonly bool ShouldComplete;

    public SyncedTransitionUpdate(float progress, bool recordImpactOnRewind, int impactBeat, bool shouldComplete)
    {
        Progress = progress;
        RecordImpactOnRewind = recordImpactOnRewind;
        ImpactBeat = impactBeat;
        ShouldComplete = shouldComplete;
    }
}
