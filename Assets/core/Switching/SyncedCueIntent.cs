using System;

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
/// The casting half of a Synced Mode Cue: which Performer the cue targets, given the musical event
/// intent and the staged/deck choices. Timing — wait, cue, or block on cadence — is the Cue
/// Planner's verdict (<see cref="CuePlanner.EvaluateCueTiming"/>); this type never answers "when".
/// </summary>
public readonly struct SyncedCueIntent
{
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
        int targetEffectIndex,
        CueEventIntent eventIntent,
        Repertoire preferredRepertoire,
        bool castPreferredPerformer,
        int effectDeckIndex)
    {
        TargetEffectIndex = targetEffectIndex;
        EventIntent = eventIntent;
        PreferredRepertoire = preferredRepertoire;
        CastPreferredPerformer = castPreferredPerformer;
        EffectDeckIndex = effectDeckIndex;
    }

    /// <summary>True when the scheduled Cue asks for a Fill-capable Performer.</summary>
    public bool FillAligned => EventIntent == CueEventIntent.Fill;

    /// <summary>True when the Cue is landing on an upcoming Drop and asks for a Drop-capable Performer.</summary>
    public bool DropAligned => EventIntent == CueEventIntent.Drop;

    /// <summary>
    /// Casts the Performer for a cue that the Cue Planner already cleared to fire: the staged choice
    /// by default, or — for an event-aligned cue without held/manual staging — a preferred Performer
    /// found on the deck. Casting never rotates the deck: a deck find is reported through
    /// <see cref="EffectDeckIndex"/>, and the Director pulls that card only when the cue is sent.
    /// </summary>
    public static SyncedCueIntent Cast(
        CueEventIntent eventIntent,
        int stagedEffectIndex,
        bool preserveStagedEffect,
        int currentEffectIndex,
        int[] deck,
        Func<int, Repertoire> repertoireForEffect)
    {
        var preferredRepertoire = PreferredRepertoireFor(eventIntent);
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
