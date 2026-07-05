using System;
using UnityEngine;

/// <summary>High-level cadence source currently driving the Director.</summary>
public enum DirectorMode
{
    NotReady,
    Standalone,
    Synced,
    Hold
}

/// <summary>
/// Read-only snapshot of one Director-held Cue Sheet slot, for the Observatory and Unity Inspector.
/// A Cue Sheet is an index of empty Cue Marks over one Phrase; this exposes it as real state, not a
/// remembered verdict.
/// </summary>
public readonly struct CueSheetView
{
    /// <summary>The empty view reported for a slot holding no sheet.</summary>
    public static CueSheetView Empty { get; } = new CueSheetView(false, -1, -1, -1, Array.Empty<int>());

    /// <summary>Whether this slot currently holds a sheet.</summary>
    public readonly bool HasSheet;

    /// <summary>Absolute beat the sheet's Phrase starts on.</summary>
    public readonly int PhraseStartBeat;

    /// <summary>Absolute beat the sheet's Phrase ends on (its mandatory final Cue Mark).</summary>
    public readonly int PhraseEndBeat;

    /// <summary>Total Phrase length in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Phrase-relative Cue Mark offsets for display.</summary>
    public readonly int[] CueMarkOffsets;

    public CueSheetView(bool hasSheet, int phraseStartBeat, int phraseEndBeat, int phraseLengthBeats, int[] cueMarkOffsets)
    {
        HasSheet = hasSheet;
        PhraseStartBeat = phraseStartBeat;
        PhraseEndBeat = phraseEndBeat;
        PhraseLengthBeats = phraseLengthBeats;
        CueMarkOffsets = cueMarkOffsets ?? Array.Empty<int>();
    }
}

/// <summary>Read-only snapshot of the Director reducer's real state for the HUD and Unity Inspector.</summary>
public readonly struct DirectorStatus
{
    public static DirectorStatus NotReady { get; } = new DirectorStatus(
        DirectorMode.NotReady,
        false,
        -1,
        -1,
        string.Empty,
        -1,
        string.Empty,
        false,
        false,
        CueSheetView.Empty,
        CueSheetView.Empty);

    /// <summary>Which operating mode the Director is in this frame.</summary>
    public readonly DirectorMode Mode;

    /// <summary>True when the wall is in Synced Mode (the reducer is live).</summary>
    public readonly bool IsSyncedMode;

    /// <summary>Current live beat observed by the Director, or -1 outside Synced Mode.</summary>
    public readonly int CurrentBeat;

    /// <summary>Staged effect index for the next cast, or -1 when nothing is staged.</summary>
    public readonly int NextEffectIndex;

    /// <summary>Display name of the staged effect, or empty when nothing is staged.</summary>
    public readonly string NextEffectName;

    /// <summary>Staged transition index for the next cast, or -1 before the Director is ready.</summary>
    public readonly int NextTransitionIndex;

    /// <summary>Display name of the staged transition, or empty before the Director is ready.</summary>
    public readonly string NextTransitionName;

    /// <summary>Whether the staged Effect is kept after each completed move.</summary>
    public readonly bool HoldSelectedEffect;

    /// <summary>Whether the staged Transition is kept after each completed move.</summary>
    public readonly bool HoldSelectedTransition;

    /// <summary>The current Phrase's Cue Sheet.</summary>
    public readonly CueSheetView CurrentSheet;

    /// <summary>The next Phrase's Cue Sheet, built ahead from the announcement.</summary>
    public readonly CueSheetView NextSheet;

    public DirectorStatus(
        DirectorMode mode,
        bool isSyncedMode,
        int currentBeat,
        int nextEffectIndex,
        string nextEffectName,
        int nextTransitionIndex,
        string nextTransitionName,
        bool holdSelectedEffect,
        bool holdSelectedTransition,
        CueSheetView currentSheet,
        CueSheetView nextSheet)
    {
        Mode = mode;
        IsSyncedMode = isSyncedMode;
        CurrentBeat = currentBeat;
        NextEffectIndex = nextEffectIndex;
        NextEffectName = nextEffectName ?? string.Empty;
        NextTransitionIndex = nextTransitionIndex;
        NextTransitionName = nextTransitionName ?? string.Empty;
        HoldSelectedEffect = holdSelectedEffect;
        HoldSelectedTransition = holdSelectedTransition;
        CurrentSheet = currentSheet;
        NextSheet = nextSheet;
    }
}

/// <summary>
/// Decides what plays and when it changes, as a wire-change reducer (ADR-0011). In Synced Mode the
/// Director wakes once per new beat and does three things only: repair its two Cue Sheets by invariant,
/// Cast a Cue when a Grid carrying a Cue Mark begins, and hand that Cue to the Switcher fire-and-forget.
/// It reads musical truth only from <see cref="BeatManager"/>, never OSC directly, keeps no decision
/// memory, and never mirrors commitment — the Switcher alone owns that and answers accepted-or-not.
/// </summary>
[Serializable]
public sealed class Director
{
    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer standaloneTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;
    private readonly CueLog cueLog;

    private int currentEffectIndexForSelection = -1;
    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;
    private bool nextEffectIsManualSelection;
    private bool nextTransitionIsManualSelection;

    // Reducer wake memory. lastWakeBeat gates decisions to once per new beat; lastGridBeat is the previous
    // 16-count so a new Grid is read as the count moving backwards (a wrap), never as equality with 1.
    private int lastWakeBeat = -1;
    private int lastGridBeat = -1;

    // Phrase-lane expectation memory. Each wake the phrase_state lane's countdown should advance one toward
    // its boundary; the beat the countdown "would hit 0" is beat 1 of the next Phrase (there is no zero), so
    // a wrap is the expected turnover. -1 means the lane has not been observed yet.
    private int lastPhraseObservedBeat = -1;
    private int lastPhraseBeatsUntilNext = -1;

    private CueSheetSlot currentSlot = CueSheetSlot.Empty;
    private CueSheetSlot nextSlot = CueSheetSlot.Empty;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;

    /// <summary>
    /// One held Cue Sheet plus the announcement (Phrase label and length) it was built from. Identity
    /// (<see cref="BuiltFrom"/>) is the announced label and length, nothing else: the absolute anchor is
    /// captured once at build or shift and translates the sheet's Phrase-relative marks to absolute beats for
    /// the Switcher — it is mark-placement arithmetic, never identity, so position wobble on an unchanged
    /// announcement can never re-roll the sheet.
    /// </summary>
    private readonly struct CueSheetSlot
    {
        public static CueSheetSlot Empty { get; } = new CueSheetSlot(false, default, -1, -1, null);

        public readonly bool HasSheet;
        public readonly CueSheet Sheet;
        public readonly int PhraseStartBeat;
        public readonly int PhraseLengthBeats;
        public readonly string PhraseLabel;

        public CueSheetSlot(bool hasSheet, CueSheet sheet, int phraseStartBeat, int phraseLengthBeats, string phraseLabel)
        {
            HasSheet = hasSheet;
            Sheet = sheet;
            PhraseStartBeat = phraseStartBeat;
            PhraseLengthBeats = phraseLengthBeats;
            PhraseLabel = phraseLabel;
        }

        public int PhraseEndBeat => PhraseStartBeat + PhraseLengthBeats;

        /// <summary>
        /// Whether this slot was built from exactly this announcement (label and length). Keying to the
        /// announced values — not to a re-derived beat — is what makes timing wobble unable to re-roll a sheet.
        /// </summary>
        public bool BuiltFrom(string phraseLabel, int phraseLengthBeats) =>
            HasSheet && PhraseLabel == phraseLabel && PhraseLengthBeats == phraseLengthBeats;

        /// <summary>Whether the sheet carries a Cue Mark on exactly this absolute beat.</summary>
        public bool HasCueMarkAt(int absoluteBeat)
        {
            if (!HasSheet || Sheet.CueMarkOffsets == null)
            {
                return false;
            }

            var targetOffset = absoluteBeat - PhraseStartBeat;
            foreach (var offset in Sheet.CueMarkOffsets)
            {
                if (offset == targetOffset)
                {
                    return true;
                }
            }

            return false;
        }

        public CueSheetView ToView() => HasSheet
            ? new CueSheetView(true, PhraseStartBeat, PhraseEndBeat, PhraseLengthBeats, (int[])Sheet.CueMarkOffsets.Clone())
            : CueSheetView.Empty;
    }

    /// <summary>One cast choice: the catalog index and, when it came from a deck peek, the deck position to pull on acceptance.</summary>
    private readonly struct Cast
    {
        private Cast(int index, int deckIndex)
        {
            Index = index;
            DeckIndex = deckIndex;
        }

        public readonly int Index;
        public readonly int DeckIndex;

        public static Cast Staged(int index) => new Cast(index, -1);

        public static Cast FromDeck(int index, int deckIndex) => new Cast(index, deckIndex);
    }

    public Director(
        Controller controller,
        Switcher switcher,
        Timer standaloneTimer,
        int[] effectDeck,
        int[] transitionDeck,
        int initialTransitionIndex,
        CueLog cueLog = null)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        this.controller = controller;
        this.switcher = switcher ?? throw new ArgumentNullException(nameof(switcher));
        this.standaloneTimer = standaloneTimer ?? throw new ArgumentNullException(nameof(standaloneTimer));
        this.effectDeck = effectDeck ?? throw new ArgumentNullException(nameof(effectDeck));
        this.transitionDeck = transitionDeck ?? throw new ArgumentNullException(nameof(transitionDeck));
        this.cueLog = cueLog;
        currentEffectIndexForSelection = switcher.CurrentEffectIndex;
        SetNextTransition(initialTransitionIndex);
        nextTransitionIsManualSelection = false;
        StageNextEffect(currentEffectIndexForSelection);
    }

    /// <summary>
    /// Whether the wall is in Synced Mode: a usable beat clock is running. Reads the single mode authority
    /// (<see cref="BeatManager.IsSynced"/>), not OSC transport liveness (ADR-0007).
    /// </summary>
    public bool IsSyncedMode => controller != null && controller.beatManager != null && controller.beatManager.IsSynced;

    /// <summary>Current read-only reducer snapshot for runtime HUDs and inspector diagnostics.</summary>
    public DirectorStatus Status => IsReady ? BuildStatus() : DirectorStatus.NotReady;

    private bool IsReady =>
        controller != null
        && controller.beatManager != null
        && controller.effects != null
        && controller.transitions != null
        && switcher != null
        && standaloneTimer != null
        && effectDeck != null
        && transitionDeck != null;

    /// <summary>Index of the Effect staged for the next A-to-B move.</summary>
    public int NextEffectIndex => nextEffectIndex;

    /// <summary>Index of the Transition staged for the next A-to-B move.</summary>
    public int NextTransitionIndex => nextTransitionIndex;

    /// <summary>Whether the staged Effect should be kept after each completed move.</summary>
    public bool HoldSelectedEffect => holdSelectedEffect;

    /// <summary>Whether the staged Transition should be kept after each completed move.</summary>
    public bool HoldSelectedTransition => holdSelectedTransition;

    /// <summary>Stages the Effect that the next A-to-B move should target.</summary>
    public void SetNextEffect(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);
        nextEffectIndex = effectIndex;
        nextEffectIsManualSelection = true;
        Trace($"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>Stages the Transition that the next A-to-B move should use.</summary>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
        nextTransitionIsManualSelection = true;
        controller.currentTransition = nextTransitionIndex;
        Trace($"NEXT_TRANSITION_SET nextTransition={FormatTransition(nextTransitionIndex)} hold={holdSelectedTransition}");
    }

    /// <summary>When enabled, the currently staged Effect is staged again after each completed move.</summary>
    public void SetHoldSelectedEffect(bool hold)
    {
        holdSelectedEffect = hold;
        Trace($"NEXT_EFFECT_HOLD_SET hold={holdSelectedEffect} nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>When enabled, the currently staged Transition is staged again after each completed move.</summary>
    public void SetHoldSelectedTransition(bool hold)
    {
        holdSelectedTransition = hold;
        Trace($"NEXT_TRANSITION_HOLD_SET hold={holdSelectedTransition} nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    /// <summary>Advances the Director's Standalone cadence clock or, in Synced Mode, the beat-driven reducer.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        // Synced Mode needs both the mode authority and a running absolute beat. If the clock is gone —
        // or a frame of Synced Mode arrives without a usable Beat — fall through to Standalone (ADR-0007).
        if (IsSyncedMode && controller.beatManager.Beat is { } beat)
        {
            TickSyncedMode(beat);
        }
        else
        {
            TickStandaloneMode(deltaTime);
        }
    }

    /// <summary>Immediate developer/manual effect selection. Resets Standalone Mode cadence and reducer memory.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace($"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        switcher.ShowNow(effectIndex);
        currentEffectIndexForSelection = effectIndex;
        ResetReducerMemory();
        standaloneTimer.Set(durationSeconds);
        standaloneTimer.Reset();
        StageNextChoices();
    }

    /// <summary>Applies Hold as an inspection freeze: keeps the held effect on stage, suspending rotation.</summary>
    public void ApplyHold()
    {
        if (!controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            return;
        }

        if (currentEffectIndexForSelection != heldEffectIndex)
        {
            ShowNow(heldEffectIndex, controller.effectTime);
        }
        else
        {
            standaloneTimer.Reset();
        }
    }

    /// <summary>Standalone Mode timer callback.</summary>
    public void OnTimerFinished()
    {
        if (IsSyncedMode)
        {
            Trace($"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Beat)}");
            return;
        }

        Trace("TIMER_FINISHED_STANDALONE");
        RunStandaloneTimerDecision();
    }

    private void TickStandaloneMode(float deltaTime)
    {
        // The mode boundary owns cue teardown and reducer reset: a beat-domain cue loaded while Synced carries
        // a Unity-time start and would fire into a dead clock, so abort any Switcher-held cue (even a locked
        // one). Sheet and Grid memory must not cross a Standalone gap either. Idempotent every-frame (ADR-0007).
        switcher.AbortLoadedCue();
        ResetReducerMemory();
        standaloneTimer.Update(deltaTime);
    }

    private void TickSyncedMode(int beat)
    {
        // One wake per new beat: nothing in the decision path runs per frame.
        if (beat == lastWakeBeat)
        {
            return;
        }

        lastWakeBeat = beat;
        RepairSheets(beat);
        CastOnNewGrid(beat);
    }

    /// <summary>
    /// Repairs the two Cue Sheet slots by invariant on every wake, watching the phrase_state lane by
    /// expectation. Turnover is the expected countdown wrap — the beat a countdown "would hit 0" is beat 1 of
    /// the next Phrase — so no end-beat arithmetic decides it; the next sheet shifts to current and the emptied
    /// slot refills. Sheets are keyed to the announced label and length, so timing wobble on an unchanged
    /// announcement never re-rolls a sheet; only a changed announcement can. Startup, OSC dropout, a missed
    /// announcement, and normal turnover are all the same checks — there is no cold-join case.
    /// </summary>
    private void RepairSheets(int beat)
    {
        var wrapped = IsExpectedPhraseWrap(beat);
        RememberPhraseLane(beat, controller.beatManager.Phrase);

        // Expected phrase wrap (turnover): the announced next Phrase is now current, so the next sheet shifts
        // to current and the emptied slot refills below.
        if (wrapped)
        {
            PromoteNextToCurrent();
        }

        var hasCurrent = TryReadCurrentAnnouncement(beat, out var currentStart, out var currentLength, out var currentLabel);

        // The current sheet must match the wire's current announcement on every wake: keep it when its
        // (label, length) still match; rebuild when they do not. Identity ignores position, so wobble can
        // never fire this — and a stale promotion (a flapped next sheet shifted in) heals here immediately.
        if (currentSlot.HasSheet && hasCurrent && !currentSlot.BuiltFrom(currentLabel, currentLength))
        {
            currentSlot = BuildSlot(CueLogSlot.Current, CueLogBuildReason.Rebuild, currentStart, currentLength, currentLabel);
        }

        // (a) No current sheet -> build from the current Phrase announcement.
        if (!currentSlot.HasSheet && hasCurrent)
        {
            currentSlot = BuildSlot(CueLogSlot.Current, CueLogBuildReason.Build, currentStart, currentLength, currentLabel);
        }

        // (b) No next sheet, or the announced (label, length) it was built from changed -> build from the next
        //     Phrase announcement. Never duplicate the current Phrase (the wire can briefly announce it as next).
        if (TryReadNextAnnouncement(beat, out var nextStart, out var nextLength, out var nextLabel)
            && !nextSlot.BuiltFrom(nextLabel, nextLength)
            && !currentSlot.BuiltFrom(nextLabel, nextLength))
        {
            nextSlot = BuildSlot(CueLogSlot.Next, nextSlot.HasSheet ? CueLogBuildReason.Rebuild : CueLogBuildReason.Build, nextStart, nextLength, nextLabel);
        }
    }

    /// <summary>
    /// Whether the phrase_state countdown was expected to wrap on this wake: projecting the last observed
    /// beats-until-next forward by the beats elapsed reaches or passes the boundary. There is no zero — the
    /// beat the countdown "would hit 0" is beat 1 of the next Phrase — so a wrap is the expected turnover.
    /// </summary>
    private bool IsExpectedPhraseWrap(int beat)
    {
        if (lastPhraseObservedBeat < 0 || lastPhraseBeatsUntilNext < 0)
        {
            return false;
        }

        var predicted = lastPhraseBeatsUntilNext - (beat - lastPhraseObservedBeat);
        return predicted <= 0;
    }

    /// <summary>Records this wake's phrase_state observation so the next wake can watch it by expectation.</summary>
    private void RememberPhraseLane(int beat, PhraseInfo? phrase)
    {
        lastPhraseObservedBeat = beat;
        lastPhraseBeatsUntilNext = phrase is { beatsUntilNext: { } bun } ? bun : -1;
    }

    /// <summary>Shifts the next sheet into the current slot and empties the next slot (the expected turnover).</summary>
    private void PromoteNextToCurrent()
    {
        currentSlot = nextSlot;
        nextSlot = CueSheetSlot.Empty;
        if (currentSlot.HasSheet)
        {
            Trace($"SHEET_PROMOTED start={currentSlot.PhraseStartBeat} length={currentSlot.PhraseLengthBeats}");
        }
    }

    /// <summary>
    /// Reads the current Phrase announcement into an announcement identity, or returns false when there is
    /// no usable one. A non-positive or non-16-multiple length is treated as no usable announcement — the
    /// builder throws on such lengths, so the reducer never hands one down.
    /// </summary>
    private bool TryReadCurrentAnnouncement(int beat, out int phraseStartBeat, out int phraseLengthBeats, out string phraseLabel)
    {
        phraseStartBeat = -1;
        phraseLengthBeats = -1;
        phraseLabel = null;
        if (controller.beatManager.Phrase is { beatsUntilNext: { } beatsUntilNext, lengthBeats: { } lengthBeats } phrase
            && IsUsablePhraseLength(lengthBeats))
        {
            phraseStartBeat = beat + beatsUntilNext - lengthBeats;
            phraseLengthBeats = lengthBeats;
            phraseLabel = phrase.label;
            return true;
        }

        return false;
    }

    /// <summary>Reads the next Phrase announcement into an announcement identity, or returns false when there is no usable one.</summary>
    private bool TryReadNextAnnouncement(int beat, out int phraseStartBeat, out int phraseLengthBeats, out string phraseLabel)
    {
        phraseStartBeat = -1;
        phraseLengthBeats = -1;
        phraseLabel = null;
        if (controller.beatManager.NextPhrase is { beatsUntilChange: { } beatsUntilChange, lengthBeats: { } lengthBeats } nextPhrase
            && IsUsablePhraseLength(lengthBeats))
        {
            phraseStartBeat = beat + beatsUntilChange;
            phraseLengthBeats = lengthBeats;
            phraseLabel = nextPhrase.label;
            return true;
        }

        return false;
    }

    private static bool IsUsablePhraseLength(int lengthBeats) =>
        lengthBeats > 0 && lengthBeats % CueSheet.GridBeats == 0;

    private CueSheetSlot BuildSlot(CueLogSlot slot, CueLogBuildReason reason, int phraseStartBeat, int phraseLengthBeats, string phraseLabel)
    {
        var sheet = CueSheet.Build(phraseLengthBeats, phraseStartBeat, phraseStartBeat);
        Trace($"SHEET_BUILT slot={(slot == CueLogSlot.Current ? "current" : "next")} reason={(reason == CueLogBuildReason.Build ? "build" : "rebuild")} start={phraseStartBeat} length={phraseLengthBeats} marks=[{string.Join(",", sheet.CueMarkOffsets)}]");
        cueLog?.SheetBuilt(slot, reason, phraseLabel, phraseStartBeat, phraseLengthBeats, sheet.CueMarkOffsets);
        return new CueSheetSlot(true, sheet, phraseStartBeat, phraseLengthBeats, phraseLabel);
    }

    /// <summary>
    /// Casts and hands off a Cue when a new Grid carrying a Cue Mark begins. A new Grid is the 16-count
    /// moving backwards (a wrap); a dropped packet that skips the One still trips the wrap, so no Grid is
    /// missed, and the first reading joins mid-Grid without casting.
    /// </summary>
    private void CastOnNewGrid(int beat)
    {
        // No grid lane this wake: nothing to evaluate, so no cast. A real Synced-Mode exit is a mode boundary
        // that resets grid memory (ADR-0007); this method never special-cases it.
        if (!(controller.beatManager.Grid is { } grid))
        {
            return;
        }

        var gridBeat = grid.Beat;
        var previousGridBeat = lastGridBeat;
        lastGridBeat = gridBeat;

        if (previousGridBeat < 0 || gridBeat >= previousGridBeat)
        {
            return;
        }

        // The Grid that just began runs from its Boundary to the next; a Cue Mark it carries sits on that
        // next Boundary — the beat a transition started this Grid would land on.
        var gridStartBeat = beat - (gridBeat - 1);
        var carriedCueMarkBeat = gridStartBeat + CueSheet.GridBeats;
        if (!HasCueMarkAt(carriedCueMarkBeat))
        {
            return;
        }

        OfferCue(beat, carriedCueMarkBeat);
    }

    private bool HasCueMarkAt(int absoluteBeat) =>
        currentSlot.HasCueMarkAt(absoluteBeat) || nextSlot.HasCueMarkAt(absoluteBeat);

    /// <summary>
    /// Resolves the display context (Phrase label, and the Cue Mark's Phrase-relative offset and Phrase
    /// length) for an absolute Cue Mark beat by asking the slot that carries it. For the Cue Log view only;
    /// the label and length are read straight off the live slot, never a stored verdict.
    /// </summary>
    private void ResolveCueContext(int absoluteBeat, out string phraseLabel, out int phraseRelativeOffset, out int phraseLength)
    {
        var slot = currentSlot.HasCueMarkAt(absoluteBeat) ? currentSlot
            : nextSlot.HasCueMarkAt(absoluteBeat) ? nextSlot
            : CueSheetSlot.Empty;
        phraseLabel = slot.PhraseLabel;
        phraseRelativeOffset = absoluteBeat - slot.PhraseStartBeat;
        phraseLength = slot.PhraseLengthBeats;
    }

    /// <summary>
    /// Casts an Effect and Transition for the Cue Mark and offers the Cue to the Switcher fire-and-forget,
    /// then acts on the Switcher's one answer. Casting is lazy and preference-based: a Fill on this Grid or a
    /// Drop on the next makes capable Repertoire preferred, never required. The Director makes no keep/recast
    /// decision of its own — a same-mark offer the Switcher answers <see cref="CueUpsertResult.Kept"/> rides
    /// the loaded cue unchanged, a <see cref="CueUpsertResult.Rejected"/> offer touches nothing, and only a
    /// <see cref="CueUpsertResult.Loaded"/> answer pulls the peeked deck cards and re-stages.
    /// </summary>
    private void OfferCue(int beat, int cueMarkBeat)
    {
        // A held Effect suspends rotation: offer no cue and leave the held Performer on stage.
        if (controller.TryGetHeldEffectIndex(out _))
        {
            Trace($"SYNC_CUE_HELD beat={beat} cueMark={cueMarkBeat}");
            return;
        }

        var preferredRepertoire = PreferredRepertoireForGrid(beat, cueMarkBeat);
        var effectCast = CastEffect(preferredRepertoire);
        var transitionCast = CastTransition(preferredRepertoire);
        var cue = new SwitcherCueDirection(
            cueMarkBeat,
            effectCast.Index,
            transitionCast.Index,
            controller.transitions[transitionCast.Index].Repertoire);

        // The loaded-cue view captured before the offer names the displaced cue (for the Cue Log) and, on a
        // keep, the very cue that rides — a display read, never a decision: the Switcher's answer decides.
        var priorLoaded = switcher.LoadedCueStatus;
        var answer = switcher.UpsertLoadedCue(cue, CurrentSwitcherClockSnapshot(beat));

        if (answer == CueUpsertResult.Kept)
        {
            Trace($"SYNC_CUE_KEEP beat={beat} loaded={priorLoaded.CueMarkBeat} cueMark={cueMarkBeat}");
            if (cueLog != null)
            {
                ResolveCueContext(priorLoaded.CueMarkBeat, out var keptLabel, out var keptOffset, out var keptLength);
                cueLog.CueKept(
                    keptLabel,
                    cueMarkBeat,
                    priorLoaded.CueMarkBeat,
                    keptOffset,
                    keptLength,
                    EffectName(priorLoaded.TargetEffectIndex),
                    TransitionName(priorLoaded.TransitionIndex));
            }

            return;
        }

        var accepted = answer == CueUpsertResult.Loaded;
        if (cueLog != null)
        {
            ResolveCueContext(cueMarkBeat, out var castLabel, out var castOffset, out var castLength);
            cueLog.CueCast(
                castLabel,
                cueMarkBeat,
                castOffset,
                castLength,
                EffectName(effectCast.Index),
                TransitionName(transitionCast.Index),
                ToCueFlavor(preferredRepertoire),
                accepted);
        }

        if (!accepted)
        {
            // The Switcher alone owns commitment; a rejected offer commits nothing and touches no deck.
            Trace($"SYNC_CUE_REJECTED beat={beat} cueMark={cueMarkBeat} transition={FormatTransition(transitionCast.Index)} target={FormatEffect(effectCast.Index)}");
            return;
        }

        // Loaded: pull the peeked deck cards now, at the commit point, then re-stage fresh choices.
        if (effectCast.DeckIndex >= 0)
        {
            Deck.PullAt(effectDeck, effectCast.DeckIndex);
        }

        if (transitionCast.DeckIndex >= 0)
        {
            Deck.PullAt(transitionDeck, transitionCast.DeckIndex);
        }

        controller.currentTransition = transitionCast.Index;
        currentEffectIndexForSelection = effectCast.Index;
        StageNextChoices(currentEffectIndexForSelection);
        var loaded = switcher.LoadedCueStatus;
        Trace($"SYNC_CUE_SENT beat={beat} start={loaded.StartBeat} cueMark={cueMarkBeat} transition={FormatTransition(transitionCast.Index)} target={FormatEffect(effectCast.Index)} preferred={preferredRepertoire}");
        if (cueLog != null)
        {
            ResolveCueContext(loaded.CueMarkBeat, out var loadedLabel, out var loadedOffset, out var loadedLength);
            cueLog.CueLoaded(
                loadedLabel,
                loaded.CueMarkBeat,
                loadedOffset,
                loadedLength,
                EffectName(loaded.TargetEffectIndex),
                TransitionName(loaded.TransitionIndex),
                loaded.StartBeat,
                loaded.LockPointBeat,
                loaded.RunwayBeats,
                loaded.TailBeats,
                priorLoaded.HasCue ? CueLogUpsert.Replaced : CueLogUpsert.New,
                priorLoaded.HasCue ? priorLoaded.CueMarkBeat : (int?)null);
        }
    }

    /// <summary>Maps the preferred casting Repertoire onto the Cue Log's Fill/Drop/None flavor vocabulary.</summary>
    private static CueFlavor ToCueFlavor(Repertoire preferredRepertoire)
    {
        if ((preferredRepertoire & Repertoire.HandlesFill) != 0)
        {
            return CueFlavor.Fill;
        }

        if ((preferredRepertoire & Repertoire.HandlesDrop) != 0)
        {
            return CueFlavor.Drop;
        }

        return CueFlavor.None;
    }

    /// <summary>
    /// The Repertoire a Cue on this Grid prefers: HandlesFill when a Fill lands on this Grid, HandlesDrop
    /// when a Drop lands on the next Grid, else None. A preference, never a mandate.
    /// </summary>
    private Repertoire PreferredRepertoireForGrid(int beat, int cueMarkBeat)
    {
        // "This Grid" is [beat, cueMarkBeat) — the Grid whose Boundary is the Cue Mark; "next Grid" is
        // [cueMarkBeat, cueMarkBeat + 16), which the Drop lands on the front of.
        if (EventStartsWithin(controller.beatManager.Fill, beat, beat, cueMarkBeat))
        {
            return Repertoire.HandlesFill;
        }

        if (EventStartsWithin(controller.beatManager.Drop, beat, cueMarkBeat, cueMarkBeat + CueSheet.GridBeats))
        {
            return Repertoire.HandlesDrop;
        }

        return Repertoire.None;
    }

    private static bool EventStartsWithin(PhraseEventInfo? eventInfo, int beat, int windowStartBeat, int windowEndExclusiveBeat)
    {
        if (eventInfo is { beatsUntilStart: { } beatsUntilStart })
        {
            var startBeat = beat + beatsUntilStart;
            return startBeat >= windowStartBeat && startBeat < windowEndExclusiveBeat;
        }

        return false;
    }

    private Cast CastEffect(Repertoire preferredRepertoire)
    {
        var stagedIndex = nextEffectIndex;

        // Manual staging and Hold pin the Effect; with no preference there is nothing to cast toward.
        if (holdSelectedEffect || nextEffectIsManualSelection || preferredRepertoire == Repertoire.None)
        {
            return Cast.Staged(stagedIndex);
        }

        if ((controller.EffectiveRepertoire(stagedIndex) & preferredRepertoire) != 0)
        {
            return Cast.Staged(stagedIndex);
        }

        if (Deck.TryFindPreferred(
                effectDeck,
                candidateIndex => (controller.EffectiveRepertoire(candidateIndex) & preferredRepertoire) != 0,
                out var deckIndex))
        {
            return Cast.FromDeck(effectDeck[deckIndex], deckIndex);
        }

        return Cast.Staged(stagedIndex);
    }

    private Cast CastTransition(Repertoire preferredRepertoire)
    {
        var stagedIndex = nextTransitionIndex;

        if (holdSelectedTransition || nextTransitionIsManualSelection || preferredRepertoire == Repertoire.None)
        {
            return Cast.Staged(stagedIndex);
        }

        if ((controller.transitions[stagedIndex].Repertoire.Tags & preferredRepertoire) != 0)
        {
            return Cast.Staged(stagedIndex);
        }

        if (Deck.TryFindPreferred(
                transitionDeck,
                candidateIndex => (controller.transitions[candidateIndex].Repertoire.Tags & preferredRepertoire) != 0,
                out var deckIndex))
        {
            return Cast.FromDeck(transitionDeck[deckIndex], deckIndex);
        }

        return Cast.Staged(stagedIndex);
    }

    private SwitcherClockSnapshot CurrentSwitcherClockSnapshot(int beat)
    {
        return new SwitcherClockSnapshot(
            beat,
            controller.beatManager.BeatFraction ?? 0f,
            CurrentSecondsPerBeat(),
            Time.time);
    }

    private float CurrentSecondsPerBeat()
    {
        return controller.beatManager.Bpm is { } bpm && bpm > 0f ? 60f / bpm : 0.5f;
    }

    private void ResetReducerMemory()
    {
        currentSlot = CueSheetSlot.Empty;
        nextSlot = CueSheetSlot.Empty;
        lastWakeBeat = -1;
        lastGridBeat = -1;
        lastPhraseObservedBeat = -1;
        lastPhraseBeatsUntilNext = -1;
    }

    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var currentBeat = isSynced && controller.beatManager.Beat is { } beat ? beat : -1;

        return new DirectorStatus(
            mode,
            isSynced,
            currentBeat,
            nextEffectIndex,
            EffectName(nextEffectIndex),
            nextTransitionIndex,
            TransitionName(nextTransitionIndex),
            holdSelectedEffect,
            holdSelectedTransition,
            currentSlot.ToView(),
            nextSlot.ToView());
    }

    private void LogModeIfChanged()
    {
        var mode = IsSyncedMode ? DirectorMode.Synced : DirectorMode.Standalone;
        if (controller.TryGetHeldEffectIndex(out _))
        {
            mode = DirectorMode.Hold;
        }

        if (mode == lastLoggedMode)
        {
            return;
        }

        Trace($"MODE {lastLoggedMode}->{mode} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        lastLoggedMode = mode;
    }

    private void RunStandaloneTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace($"STANDALONE_HOLD held={FormatEffect(heldEffectIndex)} current={FormatEffect(currentEffectIndexForSelection)}");
            if (currentEffectIndexForSelection != heldEffectIndex)
            {
                ShowNow(heldEffectIndex, controller.effectTime);
            }
            else
            {
                standaloneTimer.Reset();
            }

            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(targetEffectIndex);
        var transitionRepertoire = controller.transitions[transitionIndex].Repertoire;
        var transitionDurationSeconds = transitionRepertoire.DefaultDurationSeconds;
        Trace($"STANDALONE_TRANSITION_START transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} durationSeconds={transitionDurationSeconds:0.###}");
        switcher.StartTransition(
            targetEffectIndex,
            transitionIndex,
            TransitionStartTiming.FromDefaultDuration(Time.time));
        controller.currentTransition = transitionIndex;
        currentEffectIndexForSelection = targetEffectIndex;
        StageNextChoices(currentEffectIndexForSelection);
        standaloneTimer.Set(transitionDurationSeconds + controller.effectTime);
        standaloneTimer.Reset();
    }

    private void StageNextChoices()
    {
        StageNextChoices(currentEffectIndexForSelection);
    }

    private void StageNextChoices(int currentEffectIndex)
    {
        StageNextEffect(currentEffectIndex);
        StageNextTransition();
    }

    private void StageNextEffect(int currentEffectIndex)
    {
        if (holdSelectedEffect)
        {
            Trace($"NEXT_EFFECT_HELD nextEffect={FormatEffect(nextEffectIndex)}");
            return;
        }

        nextEffectIndex = Deck.PullRandom(
            effectDeck,
            candidateIndex => currentEffectIndex < 0 || candidateIndex != currentEffectIndex,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        nextEffectIsManualSelection = false;
        Trace($"NEXT_EFFECT_STAGED nextEffect={FormatEffect(nextEffectIndex)}");
    }

    private void StageNextTransition()
    {
        if (holdSelectedTransition)
        {
            controller.currentTransition = nextTransitionIndex;
            Trace($"NEXT_TRANSITION_HELD nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        nextTransitionIndex = Deck.PullRandom(
            transitionDeck,
            _ => true,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        nextTransitionIsManualSelection = false;
        controller.currentTransition = nextTransitionIndex;
        Trace($"NEXT_TRANSITION_STAGED nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    private bool IsValidEffectIndex(int effectIndex)
    {
        return controller.effects != null && effectIndex >= 0 && effectIndex < controller.effects.Length;
    }

    private bool IsValidTransitionIndex(int transitionIndex)
    {
        return controller.transitions != null && transitionIndex >= 0 && transitionIndex < controller.transitions.Length;
    }

    private void ValidateEffectIndex(int effectIndex)
    {
        if (!IsValidEffectIndex(effectIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(effectIndex), effectIndex, "Effect index is outside the runtime catalog.");
        }
    }

    private void ValidateTransitionIndex(int transitionIndex)
    {
        if (!IsValidTransitionIndex(transitionIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(transitionIndex), transitionIndex, "Transition index is outside the runtime catalog.");
        }
    }

    private string EffectName(int effectIndex)
    {
        return IsValidEffectIndex(effectIndex) ? controller.effects[effectIndex].Name : string.Empty;
    }

    private string TransitionName(int transitionIndex)
    {
        return IsValidTransitionIndex(transitionIndex) ? controller.transitions[transitionIndex].Name : string.Empty;
    }

    private void Trace(string message)
    {
        controller.LogDirectorSwitching($"Director {message}");
    }

    private string FormatEffect(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < controller.effects.Length
            ? $"{effectIndex}:{controller.effects[effectIndex].Name}"
            : $"{effectIndex}:<none>";
    }

    private string FormatTransition(int transitionIndex)
    {
        return transitionIndex >= 0 && transitionIndex < controller.transitions.Length
            ? $"{transitionIndex}:{controller.transitions[transitionIndex].Name}"
            : $"{transitionIndex}:<none>";
    }

    private static string FormatNullableBeat(int? beat)
    {
        return beat is { } value ? value.ToString() : "none";
    }
}
