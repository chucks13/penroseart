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

/// <summary>Musical event meaning used to cast a Director-facing Cue.</summary>
public enum CueEventIntent
{
    /// <summary>The Cue is ordinary phrase motion.</summary>
    Ordinary,

    /// <summary>The phase reached by the Cue contains or overlaps a Fill.</summary>
    Fill,

    /// <summary>The Cue Mark lands on an upcoming Drop.</summary>
    Drop
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
    public readonly CueEventIntent EventIntent;
    public readonly Repertoire PreferredRepertoire;
    public readonly bool CastPreferredPerformer;

    private SyncedCueIntent(
        SyncedCueIntentKind kind,
        TransitionBeatPlan beatPlan,
        int currentBeat,
        int targetEffectIndex,
        CueEventIntent eventIntent,
        Repertoire preferredRepertoire,
        bool castPreferredPerformer)
    {
        Kind = kind;
        BeatPlan = beatPlan;
        CurrentBeat = currentBeat;
        BeatsUntilImpact = beatPlan.ImpactBeat - currentBeat;
        TargetEffectIndex = targetEffectIndex;
        EventIntent = eventIntent;
        PreferredRepertoire = preferredRepertoire;
        CastPreferredPerformer = castPreferredPerformer;
    }

    public bool ShouldCue => Kind == SyncedCueIntentKind.Cue;

    public bool BlockedByCadence => Kind == SyncedCueIntentKind.BlockedByCadence;

    /// <summary>True when the scheduled Cue asks for a Fill-capable Performer.</summary>
    public bool FillAligned => EventIntent == CueEventIntent.Fill;

    /// <summary>True when the Cue is landing on an upcoming Drop and asks for a Drop-capable Performer.</summary>
    public bool DropAligned => EventIntent == CueEventIntent.Drop;

    /// <summary>
    /// Builds a Synced Mode cue intent from the Timing Frame, selected Transition timing,
    /// event-aware casting context, staged Effect choice, and advertised Effect Repertoire.
    /// When an event-aligned cue casts a preferred Performer from the deck, evaluating the intent
    /// reserves that card by rotating it through <see cref="EffectDeckSelection.TryPullPreferred"/>.
    /// </summary>
    public static SyncedCueIntent Evaluate(
        TimingFrame frame,
        TransitionRepertoire transitionRepertoire,
        CueEventIntent eventIntent,
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

        var beatPlan = TransitionBeatPlan.FromCueMark(frame.CueMarkBeat, transitionRepertoire);
        if (frame.PassLocalState.LastCueBeat == frame.CurrentBeat || !beatPlan.IsCueBeat(frame.CurrentBeat))
        {
            return NoCue(SyncedCueIntentKind.Wait, beatPlan, frame.CurrentBeat, stagedEffectIndex, eventIntent);
        }

        var preferredRepertoire = PreferredRepertoireFor(eventIntent);
        if (!ChangeCadence.CanChangeAt(
            frame.CueMarkBeat,
            frame.PassLocalState.PreviousCueMarkBeat,
            minimumChangeCadenceBeats))
        {
            return NoCue(
                SyncedCueIntentKind.BlockedByCadence,
                beatPlan,
                frame.CurrentBeat,
                stagedEffectIndex,
                eventIntent,
                preferredRepertoire);
        }

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
            eventIntent,
            preferredRepertoire,
            castPreferredPerformer);
    }

    /// <summary>Combines the phase reached by a scheduled Cue with live phrase events for casting.</summary>
    public static CueEventIntent ResolveEventIntent(
        TimingFrame frame,
        PhraseEventInfo? fill,
        PhraseEventInfo? drop)
    {
        if (!frame.HasPhaseAnchor)
        {
            return CueEventIntent.Ordinary;
        }

        return ResolveEventIntent(
            frame.CueMarkBeat - frame.CurrentBeat,
            BeatsUntilPhaseEnd(frame),
            fill,
            drop);
    }

    private static SyncedCueIntent NoCue(
        SyncedCueIntentKind kind,
        TransitionBeatPlan beatPlan,
        int currentBeat,
        int stagedEffectIndex,
        CueEventIntent eventIntent,
        Repertoire preferredRepertoire = Repertoire.None)
    {
        return new SyncedCueIntent(
            kind,
            beatPlan,
            currentBeat,
            stagedEffectIndex,
            eventIntent,
            preferredRepertoire,
            false);
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

    private static CueEventIntent ResolveEventIntent(
        int beatsUntilImpact,
        int beatsUntilPhaseEnd,
        PhraseEventInfo? fill,
        PhraseEventInfo? drop)
    {
        if (drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart }
            && dropBeatsUntilStart == beatsUntilImpact)
        {
            return CueEventIntent.Drop;
        }

        if (FillOverlapsNextPhase(beatsUntilImpact, beatsUntilPhaseEnd, fill))
        {
            return CueEventIntent.Fill;
        }

        return CueEventIntent.Ordinary;
    }

    private static int BeatsUntilPhaseEnd(TimingFrame frame)
    {
        var phaseEndBeat = frame.CueMarkBeat + PhraseWindow.DefaultPhaseBeats;
        if (frame.HasPhraseWindow)
        {
            phaseEndBeat = Math.Min(phaseEndBeat, frame.PhraseWindow.EndBeat);
        }

        return phaseEndBeat - frame.CurrentBeat;
    }

    private static bool FillOverlapsNextPhase(int beatsUntilPhaseStart, int beatsUntilPhaseEnd, PhraseEventInfo? fill)
    {
        if (fill is not { } value)
        {
            return false;
        }

        if (value.inProgress)
        {
            return value.beatsUntilEnd == null || value.beatsUntilEnd >= beatsUntilPhaseStart;
        }

        if (value.beatsUntilStart is not { } beatsUntilStart)
        {
            return false;
        }

        if (beatsUntilStart < 0)
        {
            return false;
        }

        var lengthBeats = value.lengthBeats ?? 1;
        var beatsUntilEnd = beatsUntilStart + Math.Max(1, lengthBeats);
        return beatsUntilStart < beatsUntilPhaseEnd && beatsUntilEnd >= beatsUntilPhaseStart;
    }

    /// <summary>Maps a resolved <see cref="CueEventIntent"/> to the Performer Repertoire it asks the Director to cast.</summary>
    public static Repertoire PreferredRepertoireFor(CueEventIntent eventIntent)
    {
        switch (eventIntent)
        {
            case CueEventIntent.Fill:
                return Repertoire.HandlesFill;
            case CueEventIntent.Drop:
                return Repertoire.HandlesDrop;
            default:
                return Repertoire.None;
        }
    }
}
