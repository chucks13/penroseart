using System;

/// <summary>
/// Result of asking whether the Director should issue a Synced Mode transition cue on the current beat.
/// </summary>
public enum SyncedCueIntentKind
{
    /// <summary>No cue should be issued on this beat.</summary>
    Wait,

    /// <summary>The current beat is inside the transition runway and cadence permits the cue.</summary>
    Cue,

    /// <summary>The current beat is inside the transition runway, but the minimum-change cadence blocks it.</summary>
    BlockedByCadence
}

/// <summary>
/// Director-facing intent for a Synced Mode Cue, including beat timing and Repertoire-aware casting.
/// </summary>
public readonly struct SyncedCueIntent
{
    public readonly SyncedCueIntentKind Kind;
    public readonly TransitionBeatPlan BeatPlan;
    public readonly int CurrentBeat;
    public readonly int BeatsUntilImpact;
    public readonly int TargetEffectIndex;
    public readonly Repertoire PreferredRepertoire;
    public readonly bool CastPreferredPerformer;

    private SyncedCueIntent(
        SyncedCueIntentKind kind,
        TransitionBeatPlan beatPlan,
        int currentBeat,
        int targetEffectIndex,
        Repertoire preferredRepertoire,
        bool castPreferredPerformer)
    {
        Kind = kind;
        BeatPlan = beatPlan;
        CurrentBeat = currentBeat;
        BeatsUntilImpact = beatPlan.ImpactBeat - currentBeat;
        TargetEffectIndex = targetEffectIndex;
        PreferredRepertoire = preferredRepertoire;
        CastPreferredPerformer = castPreferredPerformer;
    }

    public bool ShouldCue => Kind == SyncedCueIntentKind.Cue;

    public bool BlockedByCadence => Kind == SyncedCueIntentKind.BlockedByCadence;

    /// <summary>True when the Cue is landing on an upcoming Drop and asks for a Drop-capable Performer.</summary>
    public bool DropAligned => (PreferredRepertoire & Repertoire.HandlesDrop) != 0;

    /// <summary>
    /// Builds a Synced Mode cue intent from the Timing Frame, selected Transition timing,
    /// live Drop data, staged Effect choice, and advertised Effect Repertoire.
    /// </summary>
    public static SyncedCueIntent Evaluate(
        TimingFrame frame,
        TransitionRepertoire transitionRepertoire,
        PhraseEventInfo? drop,
        int stagedEffectIndex,
        bool preserveStagedEffect,
        int currentEffectIndex,
        int[] deck,
        Func<int, Repertoire> repertoireForEffect,
        int minimumChangeCadenceBeats)
    {
        if (!frame.HasPhaseAnchor)
        {
            throw new InvalidOperationException("Cannot evaluate a synced cue intent without a Phase Anchor.");
        }

        var beatPlan = TransitionBeatPlan.FromSelectedPhaseBoundary(frame.SelectedPhaseBoundary, transitionRepertoire);
        if (frame.PassLocalState.LastCueBeat == frame.CurrentBeat)
        {
            return new SyncedCueIntent(SyncedCueIntentKind.Wait, beatPlan, frame.CurrentBeat, stagedEffectIndex, Repertoire.None, false);
        }

        if (!beatPlan.IsCueBeat(frame.CurrentBeat))
        {
            return new SyncedCueIntent(SyncedCueIntentKind.Wait, beatPlan, frame.CurrentBeat, stagedEffectIndex, Repertoire.None, false);
        }

        if (!ChangeCadence.CanChangeAt(
            frame.SelectedPhaseBoundary,
            frame.PassLocalState.PreviousSelectedPhaseBoundary,
            minimumChangeCadenceBeats))
        {
            return new SyncedCueIntent(SyncedCueIntentKind.BlockedByCadence, beatPlan, frame.CurrentBeat, stagedEffectIndex, Repertoire.None, false);
        }

        var preferredRepertoire = PreferredRepertoireForLanding(drop, beatPlan.ImpactBeat - frame.CurrentBeat);
        if (preferredRepertoire != Repertoire.None && repertoireForEffect == null)
        {
            throw new ArgumentNullException(nameof(repertoireForEffect));
        }

        var targetEffectIndex = stagedEffectIndex;
        var castPreferredPerformer = StagedEffectMatchesPreferredRepertoire(
            stagedEffectIndex,
            currentEffectIndex,
            preferredRepertoire,
            repertoireForEffect);
        if (!castPreferredPerformer
            && preferredRepertoire != Repertoire.None
            && !preserveStagedEffect
            && EffectDeckSelection.TryPullPreferred(
                deck,
                currentEffectIndex,
                preferredRepertoire,
                repertoireForEffect,
                out var preferredEffectIndex))
        {
            targetEffectIndex = preferredEffectIndex;
            castPreferredPerformer = true;
        }

        return new SyncedCueIntent(
            SyncedCueIntentKind.Cue,
            beatPlan,
            frame.CurrentBeat,
            targetEffectIndex,
            preferredRepertoire,
            castPreferredPerformer);
    }

    private static bool StagedEffectMatchesPreferredRepertoire(
        int stagedEffectIndex,
        int currentEffectIndex,
        Repertoire preferredRepertoire,
        Func<int, Repertoire> repertoireForEffect)
    {
        return preferredRepertoire != Repertoire.None
            && stagedEffectIndex != currentEffectIndex
            && (repertoireForEffect(stagedEffectIndex) & preferredRepertoire) != 0;
    }

    private static Repertoire PreferredRepertoireForLanding(PhraseEventInfo? drop, int beatsUntilImpact)
    {
        return drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart }
            && dropBeatsUntilStart == beatsUntilImpact
            ? Repertoire.HandlesDrop
            : Repertoire.None;
    }
}
