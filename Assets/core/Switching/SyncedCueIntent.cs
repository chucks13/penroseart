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

    /// <summary>The Grid reached by the Cue contains or overlaps a Fill.</summary>
    Fill,

    /// <summary>The Cue Mark lands on an upcoming Drop.</summary>
    Drop,

    /// <summary>
    /// A Drop lands within one Grid AFTER this (ordinary) Cue Mark. Cast a Drop-capable Performer on
    /// this Cue so it is already on stage and settled entering the Drop (the cast-ahead half of the
    /// drop-protect design); the Drop's own Cue Mark is then suppressed by the Director's drop guard.
    /// </summary>
    DropApproaching
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

    /// <summary>
    /// Deck position of the preferred Performer card this cue casts from the effect deck, or -1 when
    /// the target is the staged choice. The Director pulls the card at the Cue commit point — deck
    /// candidates rotate only when a cue is actually sent.
    /// </summary>
    public readonly int EffectDeckIndex;

    private SyncedCueIntent(
        SyncedCueIntentKind kind,
        TransitionBeatPlan beatPlan,
        int currentBeat,
        int targetEffectIndex,
        CueEventIntent eventIntent,
        Repertoire preferredRepertoire,
        bool castPreferredPerformer,
        int effectDeckIndex)
    {
        Kind = kind;
        BeatPlan = beatPlan;
        CurrentBeat = currentBeat;
        BeatsUntilImpact = beatPlan.ImpactBeat - currentBeat;
        TargetEffectIndex = targetEffectIndex;
        EventIntent = eventIntent;
        PreferredRepertoire = preferredRepertoire;
        CastPreferredPerformer = castPreferredPerformer;
        EffectDeckIndex = effectDeckIndex;
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
    /// Evaluation never rotates the deck: a preferred Performer found on the deck is reported through
    /// <see cref="EffectDeckIndex"/>, and the Director pulls that card only when the cue is sent.
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
        if (!frame.HasGridAnchor)
        {
            throw new InvalidOperationException("Cannot evaluate a synced cue intent without a Grid Anchor.");
        }

        var beatPlan = TransitionBeatPlan.FromCueMark(frame.CueMarkBeat, transitionRepertoire);
        if (frame.PassLocalState.LastCueBeat == frame.CurrentBeat
            || frame.PassLocalState.PreviousCueMarkBeat == frame.CueMarkBeat
            || !beatPlan.IsCueBeat(frame.CurrentBeat))
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
        var effectDeckIndex = -1;
        var castPreferredPerformer = StagedEffectMatchesPreferredRepertoire(
            stagedEffectIndex,
            currentEffectIndex,
            preferredRepertoire,
            repertoireForEffect);
        if (!castPreferredPerformer
            && preferredRepertoire != Repertoire.None
            && !preserveStagedEffect
            && Deck.TryFindPreferred(
                deck,
                candidateIndex => (currentEffectIndex < 0 || candidateIndex != currentEffectIndex)
                    && (repertoireForEffect(candidateIndex) & preferredRepertoire) != 0,
                out var preferredDeckIndex))
        {
            targetEffectIndex = deck[preferredDeckIndex];
            effectDeckIndex = preferredDeckIndex;
            castPreferredPerformer = true;
        }

        return new SyncedCueIntent(
            SyncedCueIntentKind.Cue,
            beatPlan,
            frame.CurrentBeat,
            targetEffectIndex,
            eventIntent,
            preferredRepertoire,
            castPreferredPerformer,
            effectDeckIndex);
    }

    /// <summary>Combines the Grid reached by a scheduled Cue with live phrase events for casting.</summary>
    public static CueEventIntent ResolveEventIntent(
        TimingFrame frame,
        PhraseEventInfo? fill,
        PhraseEventInfo? drop)
    {
        if (!frame.HasGridAnchor)
        {
            return CueEventIntent.Ordinary;
        }

        return ResolveEventIntent(
            frame.CueMarkBeat - frame.CurrentBeat,
            BeatsUntilGridEnd(frame),
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
            castPreferredPerformer: false,
            effectDeckIndex: -1);
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
        int beatsUntilGridEnd,
        PhraseEventInfo? fill,
        PhraseEventInfo? drop)
    {
        if (drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart }
            && dropBeatsUntilStart == beatsUntilImpact)
        {
            return CueEventIntent.Drop;
        }

        // Cast ahead: a Drop landing within one Grid after this mark asks this ordinary Cue to put a
        // Drop-capable Performer on stage now, so it is settled before the Drop. Wins over a coincident
        // Fill — Drops are the higher-priority structural event the Director positions for.
        if (drop is { inProgress: false, beatsUntilStart: { } approachingDropBeats }
            && approachingDropBeats > beatsUntilImpact
            && approachingDropBeats <= beatsUntilImpact + PhraseWindow.DefaultGridBeats)
        {
            return CueEventIntent.DropApproaching;
        }

        if (FillOverlapsNextGrid(beatsUntilImpact, beatsUntilGridEnd, fill))
        {
            return CueEventIntent.Fill;
        }

        return CueEventIntent.Ordinary;
    }

    private static int BeatsUntilGridEnd(TimingFrame frame)
    {
        var gridEndBeat = frame.CueMarkBeat + PhraseWindow.DefaultGridBeats;
        if (frame.HasPhraseWindow)
        {
            gridEndBeat = Math.Min(gridEndBeat, frame.PhraseWindow.EndBeat);
        }

        return gridEndBeat - frame.CurrentBeat;
    }

    private static bool FillOverlapsNextGrid(int beatsUntilGridStart, int beatsUntilGridEnd, PhraseEventInfo? fill)
    {
        if (fill is not { } value)
        {
            return false;
        }

        if (value.inProgress)
        {
            return value.beatsUntilEnd == null || value.beatsUntilEnd >= beatsUntilGridStart;
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
        return beatsUntilStart < beatsUntilGridEnd && beatsUntilEnd >= beatsUntilGridStart;
    }

    /// <summary>Maps a resolved <see cref="CueEventIntent"/> to the Performer Repertoire it asks the Director to cast.</summary>
    public static Repertoire PreferredRepertoireFor(CueEventIntent eventIntent)
    {
        switch (eventIntent)
        {
            case CueEventIntent.Fill:
                return Repertoire.HandlesFill;
            case CueEventIntent.Drop:
            case CueEventIntent.DropApproaching:
                return Repertoire.HandlesDrop;
            default:
                return Repertoire.None;
        }
    }
}
