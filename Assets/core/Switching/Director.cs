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
    public static CueSheetView Empty { get; } = new CueSheetView(false, -1, string.Empty, Array.Empty<int>());

    /// <summary>Whether this slot currently holds a sheet.</summary>
    public readonly bool HasSheet;

    /// <summary>Total Phrase length in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Announced Phrase label — the sheet's identity, together with its length.</summary>
    public readonly string PhraseLabel;

    /// <summary>Phrase-relative Cue Mark offsets for display.</summary>
    public readonly int[] CueMarkOffsets;

    public CueSheetView(bool hasSheet, int phraseLengthBeats, string phraseLabel, int[] cueMarkOffsets)
    {
        HasSheet = hasSheet;
        PhraseLengthBeats = phraseLengthBeats;
        PhraseLabel = phraseLabel ?? string.Empty;
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
/// Decides what plays and when it changes, as a wire-change reducer (ADR-0011). In Synced Mode the Director
/// watches only the five lanes (timing_grid, phrase_state, next_phrase_state, fill_state, drop_state) and does
/// no timing of its own: it repairs its two Cue Sheets by invariant, Casts a Cue when a Grid carrying a Cue
/// Mark begins, and hands that Cue to the Switcher fire-and-forget. It reads musical truth only from
/// <see cref="BeatManager"/>, never OSC directly, holds no absolute beat (sheets are Phrase-relative; the one
/// absolute beat a Cue needs is minted at the Switcher seam), keeps no decision memory, and never mirrors
/// commitment — the Switcher alone owns that and answers accepted-or-not.
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

    // Any upward roll of the countdown is a turnover, deliberately unguarded: a DJ looping a section rolls
    // the countdown back up mid-phrase, and the wall treats that as the boundary it looks like on the wire.
    // Phantom instances during loops are accepted cosmetics — sheets self-heal on the announcement check and
    // the starvation guard floors any stillness — so no monotonicity or near-boundary guard belongs here
    // (live evidence: the 2026-07-05 session logs, where a looped 16-beat section sawtoothed the countdown).
    private readonly LaneMemory<PhraseLaneObservation> phraseLane = new LaneMemory<PhraseLaneObservation>(
        (previous, current) => previous is { } last && current.BeatsUntilNext > last.BeatsUntilNext,
        clearOnAbsence: true);

    private readonly LaneMemory<PhraseAnnouncement> nextAnnouncementLane = new LaneMemory<PhraseAnnouncement>(
        (previous, current) => !(previous is { } last) || !last.Equals(current),
        clearOnAbsence: true);

    // The current Phrase instance as a wrap ordinal — incremented each time the phrase lane's wrap fires, never
    // from a beat. It counts observed Phrase boundaries, so the next slot can be keyed to the coming instance
    // (phraseInstance + 1) instead of to the announcement's (label, length), which cannot tell two real phrases
    // that share an identity apart.
    private int phraseInstance;

    // Consecutive Grid-lane wakes with no accepted cast — a counted wake in the same shape as
    // phraseInstance, incremented at the Grid wake, never read from a beat. Any backward grid move is a wake
    // (a true 16-to-1 wrap, a dropped One, a DJ scrub), deliberately: four starved cast opportunities in a row
    // mean a starved wall whatever caused the wakes, so under heavy scrubbing the guard may fire earlier than
    // 64 real beats — accepted; backsteps can only bring the floor forward, never delay it. It resets to zero
    // on any accepted Cue offer (the wall is committed to change) and with reducer memory.
    private int starvedWakes;

    // The sheet builder's own law is a Cue Mark at least every four Grids; the guard restates that ceiling in
    // live time. The count resets at the accepted offer and the wall changes one Grid later, so the wakes at
    // +16/+32/+48/+64 read 1/2/3/4 — the fourth stands 48 beats past the last change and targets the 64th beat.
    private const int StarvationWakeCeiling = 4;

    private CueSheetSlot currentSlot = CueSheetSlot.Empty;
    private CueSheetSlot nextSlot = CueSheetSlot.Empty;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;

    /// <summary>
    /// One held Cue Sheet plus the announcement (Phrase label and length) it was built from. Identity
    /// (<see cref="BuiltFrom"/>) is the announced label and length, nothing else: the sheet holds only
    /// Phrase-relative Cue Mark offsets, no absolute anchor, so position wobble on an unchanged announcement
    /// can never re-roll the sheet. The one absolute beat a Cue needs is minted at the Switcher seam, never here.
    /// </summary>
    private readonly struct CueSheetSlot
    {
        public static CueSheetSlot Empty { get; } = new CueSheetSlot(false, default, -1, null, -1);

        public readonly bool HasSheet;
        public readonly CueSheet Sheet;
        public readonly int PhraseLengthBeats;
        public readonly string PhraseLabel;

        /// <summary>
        /// The Phrase instance (wrap ordinal) this sheet was built for. Instances distinguish two real phrases
        /// that share a (label, length) — the discriminator a name comparison cannot see — so this, not the
        /// announcement, is what the next-slot guard keys on.
        /// </summary>
        public readonly int Instance;

        public CueSheetSlot(bool hasSheet, CueSheet sheet, int phraseLengthBeats, string phraseLabel, int instance)
        {
            HasSheet = hasSheet;
            Sheet = sheet;
            PhraseLengthBeats = phraseLengthBeats;
            PhraseLabel = phraseLabel;
            Instance = instance;
        }

        /// <summary>
        /// Whether this slot holds a sheet built from exactly this announcement (label and length). The seed is
        /// derived from these values, so this identifies a re-roll target; it no longer decides whether to build
        /// (the instance stamp does), only whether an existing instance's slot must recast on a changed announcement.
        /// </summary>
        public bool BuiltFrom(string phraseLabel, int phraseLengthBeats) =>
            HasSheet && PhraseLabel == phraseLabel && PhraseLengthBeats == phraseLengthBeats;

        /// <summary>Whether this slot holds the sheet built for exactly this Phrase instance (wrap ordinal).</summary>
        public bool IsForInstance(int instance) => HasSheet && Instance == instance;

        /// <summary>Whether the sheet carries a Cue Mark at exactly this Phrase-relative offset.</summary>
        public bool HasCueMarkAtOffset(int offset)
        {
            if (!HasSheet || Sheet.CueMarkOffsets == null)
            {
                return false;
            }

            foreach (var markOffset in Sheet.CueMarkOffsets)
            {
                if (markOffset == offset)
                {
                    return true;
                }
            }

            return false;
        }

        public CueSheetView ToView() => HasSheet
            ? new CueSheetView(true, PhraseLengthBeats, PhraseLabel, (int[])Sheet.CueMarkOffsets.Clone())
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

    /// <summary>
    /// One watched wire lane's observation memory: the last reading, and whether the lane's own change fired
    /// this wake. Instantiated once per lane (grid, phrase, next-announcement) with that lane's change rule, so
    /// the observe/compare/remember pattern lives exactly once. Observation only — it feeds wrap detection and
    /// the Cue Log and never decides anything the reducer does not already decide. A lane whose reading is
    /// absent this wake either clears its memory (<paramref name="clearOnAbsence"/> true, so a later reading reads
    /// as "appeared after absence") or leaves it untouched.
    /// </summary>
    private sealed class LaneMemory<T>
        where T : struct
    {
        private readonly Func<T?, T, bool> fired;
        private readonly bool clearOnAbsence;
        private T? last;

        public LaneMemory(Func<T?, T, bool> fired, bool clearOnAbsence)
        {
            this.fired = fired;
            this.clearOnAbsence = clearOnAbsence;
        }

        /// <summary>
        /// Observes this wake's reading, reporting whether the lane's change fired together with the previous and
        /// current readings (the two sides of a wrap or identity change), then remembers the reading per the
        /// lane's absence rule.
        /// </summary>
        public LaneChange<T> Observe(T? current)
        {
            var previous = last;
            var didFire = current is { } value && fired(previous, value);
            if (current.HasValue || clearOnAbsence)
            {
                last = current;
            }

            return new LaneChange<T>(didFire, previous, current);
        }

        /// <summary>Forgets the last reading so the lane rejoins mid-stream without a spurious wrap or change.</summary>
        public void Forget() => last = null;
    }

    /// <summary>One lane observation's outcome: whether the change fired, and the readings on either side of it.</summary>
    private readonly struct LaneChange<T>
        where T : struct
    {
        public LaneChange(bool fired, T? previous, T? current)
        {
            Fired = fired;
            Previous = previous;
            Current = current;
        }

        public readonly bool Fired;
        public readonly T? Previous;
        public readonly T? Current;
    }

    /// <summary>
    /// One phrase_state reading the phrase lane watches: the announcement (label, length) alongside the
    /// countdown whose upward roll is the wrap. The countdown drives wrap detection; the label and length are
    /// carried so a PHRASE_TURNOVER can name both sides of the boundary.
    /// </summary>
    private readonly struct PhraseLaneObservation
    {
        public PhraseLaneObservation(string label, int length, int beatsUntilNext)
        {
            Label = label;
            Length = length;
            BeatsUntilNext = beatsUntilNext;
        }

        public readonly string Label;
        public readonly int Length;
        public readonly int BeatsUntilNext;
    }

    /// <summary>A Phrase announcement identity — the (label, length) the next-announcement lane compares by value.</summary>
    private readonly struct PhraseAnnouncement : IEquatable<PhraseAnnouncement>
    {
        public PhraseAnnouncement(string label, int length)
        {
            Label = label;
            Length = length;
        }

        public readonly string Label;
        public readonly int Length;

        public bool Equals(PhraseAnnouncement other) => Length == other.Length && Label == other.Label;

        public override bool Equals(object obj) => obj is PhraseAnnouncement other && Equals(other);

        public override int GetHashCode() => unchecked(((Label?.GetHashCode() ?? 0) * 397) ^ Length);
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
        Trace(() => $"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>Stages the Transition that the next A-to-B move should use.</summary>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
        nextTransitionIsManualSelection = true;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_SET nextTransition={FormatTransition(nextTransitionIndex)} hold={holdSelectedTransition}");
    }

    /// <summary>When enabled, the currently staged Effect is staged again after each completed move.</summary>
    public void SetHoldSelectedEffect(bool hold)
    {
        holdSelectedEffect = hold;
        Trace(() => $"NEXT_EFFECT_HOLD_SET hold={holdSelectedEffect} nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>When enabled, the currently staged Transition is staged again after each completed move.</summary>
    public void SetHoldSelectedTransition(bool hold)
    {
        holdSelectedTransition = hold;
        Trace(() => $"NEXT_TRANSITION_HOLD_SET hold={holdSelectedTransition} nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    /// <summary>Advances the Director's Standalone cadence clock or, in Synced Mode, the beat-driven reducer.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        // The mode authority alone owns the Synced/Standalone fallthrough (ADR-0007). The reducer reads only
        // the five lanes from here; the one absolute beat a cast needs is minted later, at the Switcher seam.
        if (IsSyncedMode)
        {
            TickSyncedMode();
        }
        else
        {
            TickStandaloneMode(deltaTime);
        }
    }

    /// <summary>Immediate developer/manual effect selection. Resets Standalone Mode cadence and reducer memory.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace(() => $"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Position.Beat)}");
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
            Trace(() => $"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Position.Beat)}");
            return;
        }

        Trace(() => "TIMER_FINISHED_STANDALONE");
        RunStandaloneTimerDecision();
    }

    /// <summary>Advances Standalone cadence after clearing synchronized cue and sheet state.</summary>
    private void TickStandaloneMode(float deltaTime)
    {
        // The mode boundary owns cue teardown and reducer reset: a beat-domain cue loaded while Synced carries
        // a Unity-time start and would fire into a dead clock, so abort any Switcher-held cue (even a locked
        // one). Sheet memory must not cross a Standalone gap either; the hub owns and clears Grid identity as
        // its synchronized doorway vanishes. Idempotent every-frame (ADR-0007).
        switcher.AbortLoadedCue();
        ResetReducerMemory();
        standaloneTimer.Update(deltaTime);
    }

    private void TickSyncedMode()
    {
        // Two lane-driven, idempotent steps. Repair watches the phrase lanes by expectation; Cast watches the
        // Grid count. Neither keeps a beat gate: a repeated observation of the same lane values is not an event,
        // so a frame that changes no watched lane changes nothing.
        RepairSheets();
        CastOnNewGrid();
    }

    /// <summary>
    /// Repairs the two Cue Sheet slots by invariant on every wake, watching the phrase_state lane by
    /// observation. Turnover is the observed countdown wrap — the count jumping back up into the next Phrase —
    /// so no end-beat or elapsed-track arithmetic decides it; the next sheet shifts to current and the emptied
    /// slot refills. Sheets are keyed to the announced label and length, so timing wobble on an unchanged
    /// announcement never re-rolls a sheet; only a changed announcement can. Startup, OSC dropout, a missed
    /// announcement, and normal turnover are all the same checks — there is no cold-join case.
    /// </summary>
    private void RepairSheets()
    {
        // Phrase lane: an upward roll of the countdown is the turnover. It advances the instance ordinal (a
        // counted boundary, never a beat), logs both sides tagged with the incoming instance, and shifts the
        // next sheet into current (the emptied slot refills below) — logged on every observed wrap, whether or
        // not a sheet is present to promote.
        var phraseChange = phraseLane.Observe(ReadPhraseObservation());
        if (phraseChange.Fired)
        {
            phraseInstance++;
            var outgoing = phraseChange.Previous.Value;
            var incoming = phraseChange.Current.Value;
            cueLog?.PhraseTurnover(outgoing.Label, outgoing.Length, incoming.Label, incoming.Length, phraseInstance);
            PromoteNextToCurrent();
        }

        // Next-announcement lane: log an identity change straight off the wire, tagged with the coming instance
        // (phraseInstance + 1) the next slot is keyed to, independent of the sheet build below. A build the
        // guard suppresses still records its announcement change here, and one appearing after absence logs
        // replaced=none.
        var nextChange = nextAnnouncementLane.Observe(ReadNextAnnouncement());
        if (nextChange.Fired)
        {
            var incoming = nextChange.Current.Value;
            var replaced = nextChange.Previous;
            cueLog?.NextPhrase(incoming.Label, incoming.Length, replaced?.Label, replaced?.Length, phraseInstance + 1);
        }

        var hasCurrent = TryReadCurrentAnnouncement(out var currentLength, out var currentLabel);

        // The current sheet must match the wire's current announcement on every wake: keep it when its
        // (label, length) still match; rebuild when they do not. Identity ignores position, so wobble can
        // never fire this — and a genuine discontinuity (a promotion whose sheet does not match the new current
        // announcement) heals here immediately.
        if (currentSlot.HasSheet && hasCurrent && !currentSlot.BuiltFrom(currentLabel, currentLength))
        {
            currentSlot = BuildSlot(CueLogSlot.Current, CueLogBuildReason.Rebuild, currentLength, currentLabel, phraseInstance);
        }

        // (a) No current sheet -> build from the current Phrase announcement.
        if (!currentSlot.HasSheet && hasCurrent)
        {
            currentSlot = BuildSlot(CueLogSlot.Current, CueLogBuildReason.Build, currentLength, currentLabel, phraseInstance);
        }

        // (b) Ordinal guard: the next slot is keyed to the coming instance, not to the announcement. Build when
        //     no sheet exists for that instance; recast in place when one does but its announced (label, length)
        //     changed. A same-(label, length) successor is a different instance, so it builds its own sheet
        //     instead of being suppressed as a duplicate of the current Phrase.
        var comingInstance = phraseInstance + 1;
        if (TryReadNextAnnouncement(out var nextLength, out var nextLabel)
            && (!nextSlot.IsForInstance(comingInstance) || !nextSlot.BuiltFrom(nextLabel, nextLength)))
        {
            var reason = nextSlot.IsForInstance(comingInstance) ? CueLogBuildReason.Rebuild : CueLogBuildReason.Build;
            nextSlot = BuildSlot(CueLogSlot.Next, reason, nextLength, nextLabel, comingInstance);
        }
    }

    /// <summary>
    /// Reads this wake's phrase_state into the phrase lane's observation, or null when the wire carried no
    /// countdown. The countdown drives wrap detection (its upward roll is the turnover — there is no zero, so
    /// the beat a countdown "would hit 0" is beat 1 of the next Phrase); the label and length ride along so a
    /// PHRASE_TURNOVER can name both sides.
    /// </summary>
    private PhraseLaneObservation? ReadPhraseObservation()
    {
        return controller.beatManager.Phrase.Span.Current is { BeatsRemaining: { } beatsRemaining } phrase
            ? new PhraseLaneObservation(phrase.Name, phrase.LengthBeats ?? -1, beatsRemaining)
            : (PhraseLaneObservation?)null;
    }

    /// <summary>
    /// Reads this wake's next_phrase_state announcement identity for the next-announcement lane, or null when
    /// the wire announced no next Phrase — an absence the lane clears, so a later announcement reads as one
    /// appearing after absence. Raw observation of the wire, logged as the announcement changes; every positive
    /// announced length — Grid-multiple or irregular — builds a sheet, so the lane and the sheet agree.
    /// </summary>
    private PhraseAnnouncement? ReadNextAnnouncement()
    {
        var phrase = controller.beatManager.Phrase;
        return phrase.NextName is { } nextName
            ? new PhraseAnnouncement(nextName, phrase.NextLengthBeats ?? -1)
            : (PhraseAnnouncement?)null;
    }

    /// <summary>Shifts the next sheet into the current slot and empties the next slot (the observed turnover).</summary>
    private void PromoteNextToCurrent()
    {
        currentSlot = nextSlot;
        nextSlot = CueSheetSlot.Empty;
        if (currentSlot.HasSheet)
        {
            Trace(() => $"SHEET_PROMOTED phrase=\"{currentSlot.PhraseLabel}\" length={currentSlot.PhraseLengthBeats}");
        }
    }

    /// <summary>
    /// Reads the current Phrase announcement into an announcement identity, or returns false when there is
    /// no usable one. A non-positive or non-16-multiple length is treated as no usable announcement — the
    /// builder throws on such lengths, so the reducer never hands one down.
    /// </summary>
    private bool TryReadCurrentAnnouncement(out int phraseLengthBeats, out string phraseLabel)
    {
        phraseLengthBeats = -1;
        phraseLabel = null;
        if (controller.beatManager.Phrase.Span.Current is { LengthBeats: { } lengthBeats } phrase
            && lengthBeats > 0)
        {
            phraseLengthBeats = lengthBeats;
            phraseLabel = phrase.Name;
            return true;
        }

        return false;
    }

    /// <summary>Reads the next Phrase announcement into an announcement identity, or returns false when there is no usable one.</summary>
    private bool TryReadNextAnnouncement(out int phraseLengthBeats, out string phraseLabel)
    {
        phraseLengthBeats = -1;
        phraseLabel = null;
        var phrase = controller.beatManager.Phrase;
        if (phrase.NextName is { } nextName && phrase.NextLengthBeats is { } lengthBeats && lengthBeats > 0)
        {
            phraseLengthBeats = lengthBeats;
            phraseLabel = nextName;
            return true;
        }

        return false;
    }

    /// <summary>Builds one deterministic Cue Sheet slot and emits its downstream display event.</summary>
    private CueSheetSlot BuildSlot(CueLogSlot slot, CueLogBuildReason reason, int phraseLengthBeats, string phraseLabel, int instance)
    {
        var sheet = CueSheet.Build(phraseLengthBeats, SheetSeed(phraseLabel, phraseLengthBeats));
        var displayStart = DisplaySheetStart(slot, phraseLengthBeats);
        Trace(() => $"SHEET_BUILT slot={(slot == CueLogSlot.Current ? "current" : "next")} reason={(reason == CueLogBuildReason.Build ? "build" : "rebuild")} start={displayStart} length={phraseLengthBeats} marks=[{string.Join(",", sheet.CueMarkOffsets)}]");
        cueLog?.SheetBuilt(slot, reason, phraseLabel, displayStart, phraseLengthBeats, sheet.CueMarkOffsets);
        return new CueSheetSlot(true, sheet, phraseLengthBeats, phraseLabel, instance);
    }

    /// <summary>
    /// FNV-1a seed derived from the announcement itself (label and length), so a sheet's random roll is
    /// deterministic per announcement without any absolute anchor: two Phrases sharing a label and length roll
    /// the same sheet, and timing wobble — which changes neither — can never re-roll it.
    /// </summary>
    private static int SheetSeed(string phraseLabel, int phraseLengthBeats)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in phraseLabel ?? string.Empty)
            {
                hash = (hash ^ c) * 16777619u;
            }

            hash = (hash ^ (uint)phraseLengthBeats) * 16777619u;
            return (int)hash;
        }
    }

    /// <summary>
    /// Derives the sheet's absolute Phrase-start beat for the frozen log and trace grammars from live lane
    /// values at log time — a display derivation like the Observatory, never a decision input or stored anchor.
    /// -1 when the wire has no usable beat or countdown right now.
    /// </summary>
    private int DisplaySheetStart(CueLogSlot slot, int phraseLengthBeats)
    {
        if (!(controller.beatManager.Position.Beat is { } beat))
        {
            return -1;
        }

        if (slot == CueLogSlot.Current)
        {
            return controller.beatManager.Phrase.Span.Current is { BeatsRemaining: { } beatsRemaining }
                ? beat + beatsRemaining - phraseLengthBeats
                : -1;
        }

        return controller.beatManager.Phrase.NextInBeats is { } nextInBeats
            ? beat + nextInBeats
            : -1;
    }

    /// <summary>
    /// Casts and hands off a Cue when the hub reports that the 16-count returned to the One. Grid identity
    /// belongs to <see cref="BeatManager.Grid"/>; a first or backward mid-Grid reading does not synthesize a wrap.
    /// </summary>
    private void CastOnNewGrid()
    {
        var grid = controller.beatManager.Grid;
        if (!grid.Wrapped || !(grid.Current is { Beat: { } gridBeat }))
        {
            return;
        }

        // A wake the wall has not yet answered: count it. Any accepted offer below resets the count, so it holds
        // the consecutive starved wakes the starvation guard reads.
        starvedWakes++;

        // The Phrase, read live: its position in the Phrase is length - beatsUntilNext. Both the carried-mark
        // path and the no-sheet boundary path need it, so it is read once here.
        if (!(controller.beatManager.Phrase.Span.Current is { BeatsRemaining: { } beatsUntilNext, LengthBeats: { } lengthBeats } phrase))
        {
            return;
        }

        var position = lengthBeats - beatsUntilNext;

        // The next Grid Boundary, read live and wire-first: whichever the wire says comes first — the grid
        // lane's next One (one Grid past this Boundary, less however far into the Grid a dropped One left us,
        // 16 - (gridBeat - 1)) or the phrase lane's next downbeat. The wire re-anchors the grid at Phrase ends,
        // so on an irregular Phrase's final partial Grid the Boundary arrives early; taking the min reads that
        // re-anchor instead of extrapolating a full Grid. For regular Phrases at grid wakes the two agree. Both
        // the carried-mark path and the starvation guard target it — one boundary rule, computed once.
        var beatsToBoundary = Math.Min(CueSheet.GridBeats - (gridBeat - 1), beatsUntilNext);

        // No sheet was built (an irregular Phrase whose length is not a Grid multiple carries no marks), yet the
        // one rule still holds: the Phrase end carries a Cue. When the boundary lands within this Grid
        // (beatsUntilNext <= one Grid), offer it straight from live lane values — beatsToMark is the countdown
        // itself, the Phrase-relative offset is the announced length (position + countdown), and display context
        // is the lane's own label and length since there is no slot to read. Everything downstream is the same
        // OfferCue seam; a boundary the Grid never brings within one Grid falls through to the starvation guard
        // below, and a short runway is the Switcher's call to reject.
        if (!currentSlot.HasSheet)
        {
            if (beatsUntilNext <= CueSheet.GridBeats)
            {
                OfferCue(position + beatsUntilNext, beatsUntilNext, phrase.Name, lengthBeats);
                return;
            }
        }
        else
        {
            // The carried Cue Mark, read live: the mark sits one Boundary ahead of the beat's position in the
            // Phrase, and it lives in the current sheet. beatsToBoundary is also what the Switcher seam mints
            // its one absolute beat from, in OfferCue, as clock beat + beats-to-mark.
            var carriedMarkOffset = position + beatsToBoundary;
            if (currentSlot.HasCueMarkAtOffset(carriedMarkOffset))
            {
                OfferCue(carriedMarkOffset, beatsToBoundary, currentSlot.PhraseLabel, currentSlot.PhraseLengthBeats);
                return;
            }
        }

        // Starvation guard, last in the cast path: the sheet's carried mark (above) and the music's boundary
        // (the no-sheet branch) both get first refusal, so this fires only when neither offered this wake. When
        // the starved wakes reach the ceiling the one rule still holds in live time — offer a Cue at the next
        // Grid Boundary from live lane values (the same beatsToBoundary the carried path targets, display
        // context off the phrase lane), so the wall never freezes past four Grids and a guard cast lands on a
        // true Boundary even from a mid-Grid wake. A rejected offer (including a short mid-Grid runway) leaves
        // the count and the guard retries next wake; a legal 64-gap sheet's own carried mark arrives at this
        // same wake and is taken above, so a sheet that provides is never overridden.
        if (starvedWakes >= StarvationWakeCeiling)
        {
            OfferCue(position + beatsToBoundary, beatsToBoundary, phrase.Name, lengthBeats);
        }
    }

    /// <summary>
    /// Casts an Effect and Transition for the Cue Mark and offers the Cue to the Switcher fire-and-forget,
    /// then acts on the Switcher's one answer. Casting is lazy and preference-based: a Fill on this Grid or a
    /// Drop on the next makes capable Repertoire preferred, never required. The Director makes no keep/recast
    /// decision of its own — a same-mark offer the Switcher answers <see cref="CueUpsertResult.Kept"/> rides
    /// the loaded cue unchanged, a <see cref="CueUpsertResult.Rejected"/> offer touches nothing, and only a
    /// <see cref="CueUpsertResult.Loaded"/> answer pulls the peeked deck cards and re-stages.
    /// </summary>
    private void OfferCue(int carriedMarkOffset, int beatsToMark, string phraseLabel, int phraseLength)
    {
        // The one absolute beat a Cue needs is minted here, at the Switcher seam: the live clock beat plus the
        // beats-to-mark. No usable beat means no handoff — the Synced-but-no-beat edge the mode gate covers.
        if (!(controller.beatManager.Position.Beat is { } seamBeat))
        {
            return;
        }

        var cueMarkBeat = seamBeat + beatsToMark;

        // A held Effect suspends rotation: offer no cue and leave the held Performer on stage.
        if (controller.TryGetHeldEffectIndex(out _))
        {
            Trace(() => $"SYNC_CUE_HELD beat={seamBeat} cueMark={cueMarkBeat}");
            return;
        }

        var preferredRepertoire = PreferredRepertoireForGrid(beatsToMark);
        var effectCast = CastEffect(preferredRepertoire);
        var transitionCast = CastTransition(preferredRepertoire);
        var cue = new SwitcherCueDirection(
            cueMarkBeat,
            effectCast.Index,
            transitionCast.Index,
            controller.transitions[transitionCast.Index].Repertoire);

        // Display context for the frozen log grammars comes from the caller: a carried mark reads it off the
        // current sheet slot, a no-sheet boundary reads it off the live phrase lane. The Phrase-relative offset
        // is the one just computed. Neither is a stored verdict — the Switcher's answer alone decides commitment.

        // The loaded-cue view captured before the offer names the displaced cue (for the Cue Log) and, on a
        // keep, the very cue that rides — a display read, never a decision: the Switcher's answer decides.
        var priorLoaded = switcher.LoadedCueStatus;
        var answer = switcher.UpsertLoadedCue(cue, CurrentSwitcherClockSnapshot(seamBeat));

        // Any accepted offer — from any cast path — commits the wall to change, so the starvation count starts
        // over. A rejected offer leaves it, and the guard retries next wake.
        if (answer == CueUpsertResult.Kept || answer == CueUpsertResult.Loaded)
        {
            starvedWakes = 0;
        }

        if (answer == CueUpsertResult.Kept)
        {
            // A same-mark keep: the loaded mark equals the offered mark, so its display context is this cast's.
            Trace(() => $"SYNC_CUE_KEEP beat={seamBeat} loaded={priorLoaded.CueMarkBeat} cueMark={cueMarkBeat}");
            cueLog?.CueKept(
                phraseLabel,
                cueMarkBeat,
                priorLoaded.CueMarkBeat,
                carriedMarkOffset,
                phraseLength,
                EffectName(priorLoaded.TargetEffectIndex),
                TransitionName(priorLoaded.TransitionIndex));
            return;
        }

        var accepted = answer == CueUpsertResult.Loaded;
        cueLog?.CueCast(
            phraseLabel,
            cueMarkBeat,
            carriedMarkOffset,
            phraseLength,
            EffectName(effectCast.Index),
            TransitionName(transitionCast.Index),
            ToCueFlavor(preferredRepertoire),
            accepted);

        if (!accepted)
        {
            // The Switcher alone owns commitment; a rejected offer commits nothing and touches no deck.
            Trace(() => $"SYNC_CUE_REJECTED beat={seamBeat} cueMark={cueMarkBeat} transition={FormatTransition(transitionCast.Index)} target={FormatEffect(effectCast.Index)}");
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
        Trace(() => $"SYNC_CUE_SENT beat={seamBeat} start={loaded.StartBeat} cueMark={cueMarkBeat} transition={FormatTransition(transitionCast.Index)} target={FormatEffect(effectCast.Index)} preferred={preferredRepertoire}");
        cueLog?.CueLoaded(
            phraseLabel,
            loaded.CueMarkBeat,
            carriedMarkOffset,
            phraseLength,
            EffectName(loaded.TargetEffectIndex),
            TransitionName(loaded.TransitionIndex),
            loaded.StartBeat,
            loaded.LockPointBeat,
            loaded.RunwayBeats,
            loaded.TailBeats,
            priorLoaded.HasCue ? CueLogUpsert.Replaced : CueLogUpsert.New,
            priorLoaded.HasCue ? priorLoaded.CueMarkBeat : (int?)null);
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
    private Repertoire PreferredRepertoireForGrid(int beatsToMark)
    {
        // Windows in beats-from-now: "this Grid" is [0, beatsToMark) — the Grid whose Boundary is the Cue Mark;
        // "next Grid" is [beatsToMark, beatsToMark + 16), which the Drop lands on the front of. Read from the
        // Fill/Drop lanes' own countdowns, never an absolute beat.
        if (EventStartsWithin(controller.beatManager.Fill.NextInBeats, 0, beatsToMark))
        {
            return Repertoire.HandlesFill;
        }

        if (EventStartsWithin(controller.beatManager.Drop.NextInBeats, beatsToMark, beatsToMark + CueSheet.GridBeats))
        {
            return Repertoire.HandlesDrop;
        }

        return Repertoire.None;
    }

    /// <summary>Whether an upcoming event begins inside the half-open beat window.</summary>
    private static bool EventStartsWithin(int? beatsUntilStart, int windowStartBeatsFromNow, int windowEndExclusiveBeatsFromNow)
    {
        if (beatsUntilStart is { } start)
        {
            return start >= windowStartBeatsFromNow && start < windowEndExclusiveBeatsFromNow;
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

    /// <summary>Captures the canonical beat clock facts required at the Switcher handoff.</summary>
    private SwitcherClockSnapshot CurrentSwitcherClockSnapshot(int beat)
    {
        return new SwitcherClockSnapshot(
            beat,
            controller.beatManager.Clock.BeatFraction ?? 0f,
            CurrentSecondsPerBeat(),
            Time.time);
    }

    /// <summary>Returns live seconds per beat, falling back to the established 120-BPM cadence.</summary>
    private float CurrentSecondsPerBeat()
    {
        return controller.beatManager.Clock.Bpm is { } bpm && bpm > 0f ? 60f / bpm : 0.5f;
    }

    /// <summary>Clears Director-owned sheet and phrase observations across a mode boundary.</summary>
    private void ResetReducerMemory()
    {
        currentSlot = CueSheetSlot.Empty;
        nextSlot = CueSheetSlot.Empty;
        phraseLane.Forget();
        nextAnnouncementLane.Forget();
        phraseInstance = 0;
        starvedWakes = 0;
    }

    /// <summary>Builds the downstream read-only view of the Director's current state.</summary>
    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var currentBeat = isSynced && controller.beatManager.Position.Beat is { } beat ? beat : -1;

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

    /// <summary>Emits one deferred trace when the Director's displayed mode changes.</summary>
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

        Trace(() => $"MODE {lastLoggedMode}->{mode} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Position.Beat)}");
        lastLoggedMode = mode;
    }

    /// <summary>Consumes one Standalone timer wake, honoring Effect Hold or starting the staged move.</summary>
    private void RunStandaloneTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace(() => $"STANDALONE_HOLD held={FormatEffect(heldEffectIndex)} current={FormatEffect(currentEffectIndexForSelection)}");
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
        Trace(() => $"STANDALONE_TRANSITION_START transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} durationSeconds={transitionDurationSeconds:0.###}");
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

    /// <summary>Stages the next Effect unless the existing staged choice is held.</summary>
    private void StageNextEffect(int currentEffectIndex)
    {
        if (holdSelectedEffect)
        {
            Trace(() => $"NEXT_EFFECT_HELD nextEffect={FormatEffect(nextEffectIndex)}");
            return;
        }

        nextEffectIndex = Deck.PullRandom(
            effectDeck,
            candidateIndex => currentEffectIndex < 0 || candidateIndex != currentEffectIndex,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        nextEffectIsManualSelection = false;
        Trace(() => $"NEXT_EFFECT_STAGED nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>Stages the next Transition unless the existing staged choice is held.</summary>
    private void StageNextTransition()
    {
        if (holdSelectedTransition)
        {
            controller.currentTransition = nextTransitionIndex;
            Trace(() => $"NEXT_TRANSITION_HELD nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        nextTransitionIndex = Deck.PullRandom(
            transitionDeck,
            _ => true,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
        nextTransitionIsManualSelection = false;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_STAGED nextTransition={FormatTransition(nextTransitionIndex)}");
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

    /// <summary>Writes a Director trace whose display values are resolved only when tracing is enabled.</summary>
    private void Trace(Func<string> message)
    {
        controller.LogDirectorSwitching(() => $"Director {message()}");
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
