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

/// <summary>Current scheduling reason reported by the Director for observability.</summary>
public enum DirectorDecision
{
    NotReady,
    StandaloneTimer,
    WaitingForGrid,
    WaitingForRunway,
    WaitingForCadence,
    CueingTransition,
    Hold
}

/// <summary>
/// Read-only snapshot of Director sequencing state for the HUD and Unity Inspector.
/// </summary>
/// <summary>Terminal outcome of the Director's most recent Synced cue decision.</summary>
public enum CueDecisionOutcome
{
    /// <summary>No Synced cue decision has been reached yet.</summary>
    None,

    /// <summary>The cue was sent to the Switcher.</summary>
    Sent,

    /// <summary>The cue cleared timing but a held effect suppressed it; nothing was sent.</summary>
    Held,

    /// <summary>The Cue Mark fell inside the minimum change cadence; the mark was skipped.</summary>
    BlockedByCadence,

    /// <summary>
    /// A Drop coincided with the Cue Mark while a Drop-capable Performer was already on stage, so the
    /// Director issued no cue and left that Performer to play the Drop itself (drop-protect).
    /// </summary>
    DropProtected
}

/// <summary>Where a cast choice came from when the Director committed a cue.</summary>
public enum CueCastSource
{
    /// <summary>No cast was made for this decision.</summary>
    None,

    /// <summary>The staged choice was used.</summary>
    Staged,

    /// <summary>A read-only deck find matched the preferred Repertoire and was pulled at the commit point.</summary>
    DeckFind,

    /// <summary>The event asked for a Repertoire no candidate could satisfy; the staged choice stood in.</summary>
    NoPreferredAvailable
}

/// <summary>
/// Read-only record of the Director's most recent terminal Synced cue decision: what the musical
/// event asked for, what timing answered, and — when a cast happened — which Performer and
/// Transition were chosen and where they came from. Surfaced through
/// <see cref="DirectorStatus.LastCue"/> so the observatory can answer "why did the wall just do that".
/// </summary>
public readonly struct CueDecision
{
    /// <summary>The empty decision reported before any Synced cue decision is reached.</summary>
    public static CueDecision None { get; } = new CueDecision(
        CueDecisionOutcome.None,
        beat: -1,
        impactBeat: -1,
        CueEventIntent.Ordinary,
        Repertoire.None,
        effectIndex: -1,
        effectName: string.Empty,
        CueCastSource.None,
        transitionIndex: -1,
        transitionName: string.Empty,
        CueCastSource.None);

    public readonly CueDecisionOutcome Outcome;

    /// <summary>Beat the decision was made on.</summary>
    public readonly int Beat;

    /// <summary>Impact Point the decision aimed at (the Cue Mark), or -1 when none applied.</summary>
    public readonly int ImpactBeat;

    /// <summary>Musical event meaning the cue was classified as.</summary>
    public readonly CueEventIntent EventIntent;

    /// <summary>Repertoire the event asked the Director to cast, or None for an ordinary cue.</summary>
    public readonly Repertoire PreferredRepertoire;

    /// <summary>
    /// Effect the decision settled on: the cast target for Sent/Held, the protected on-stage
    /// Performer for DropProtected, or -1 when no effect was involved.
    /// </summary>
    public readonly int EffectIndex;
    public readonly string EffectName;
    public readonly CueCastSource EffectSource;

    /// <summary>Transition the decision selected, or -1 when none was selected.</summary>
    public readonly int TransitionIndex;
    public readonly string TransitionName;
    public readonly CueCastSource TransitionSource;

    /// <summary>
    /// Beats between the decision and its Impact Point. Zero or negative means the transition starts
    /// with no Runway left — the move lands as a hard cut.
    /// </summary>
    public int BeatsBeforeImpact => ImpactBeat - Beat;

    public CueDecision(
        CueDecisionOutcome outcome,
        int beat,
        int impactBeat,
        CueEventIntent eventIntent,
        Repertoire preferredRepertoire,
        int effectIndex,
        string effectName,
        CueCastSource effectSource,
        int transitionIndex,
        string transitionName,
        CueCastSource transitionSource)
    {
        Outcome = outcome;
        Beat = beat;
        ImpactBeat = impactBeat;
        EventIntent = eventIntent;
        PreferredRepertoire = preferredRepertoire;
        EffectIndex = effectIndex;
        EffectName = effectName ?? string.Empty;
        EffectSource = effectSource;
        TransitionIndex = transitionIndex;
        TransitionName = transitionName ?? string.Empty;
        TransitionSource = transitionSource;
    }
}

public readonly struct DirectorStatus
{
    public static DirectorStatus NotReady { get; } = new DirectorStatus(
        DirectorMode.NotReady,
        DirectorDecision.NotReady,
        false,
        false,
        TimingFrameSource.Unlocked,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        string.Empty,
        -1,
        string.Empty,
        false,
        false,
        CueSheetStatus.Empty,
        CueDecision.None);

    public readonly DirectorMode Mode;
    public readonly DirectorDecision Decision;
    public readonly bool IsSyncedMode;
    public readonly bool HasCueMark;
    /// <summary>Source of the current On-Air Timing target.</summary>
    public readonly TimingFrameSource TimingSource;
    /// <summary>Absolute beat the current Cue Mark lands on, or -1 when unlocked.</summary>
    public readonly int CueMarkBeat;
    public readonly int LastChangeBeat;
    public readonly int TransitionLandingBeat;
    public readonly int BeatsUntilLanding;
    public readonly int BeatsUntilCadenceReady;
    public readonly int NextEffectIndex;
    public readonly string NextEffectName;
    public readonly int NextTransitionIndex;
    public readonly string NextTransitionName;
    public readonly bool HoldSelectedEffect;
    public readonly bool HoldSelectedTransition;
    /// <summary>Current Cue Sheet snapshot from On-Air Timing.</summary>
    public readonly CueSheetStatus CueSheet;

    /// <summary>The Director's most recent terminal Synced cue decision.</summary>
    public readonly CueDecision LastCue;

    public DirectorStatus(
        DirectorMode mode,
        DirectorDecision decision,
        bool isSyncedMode,
        bool hasCueMark,
        TimingFrameSource timingSource,
        int cueMarkBeat,
        int lastChangeBeat,
        int transitionLandingBeat,
        int currentBeat,
        int beatsUntilLanding,
        int beatsUntilCadenceReady,
        int nextEffectIndex,
        string nextEffectName,
        int nextTransitionIndex,
        string nextTransitionName,
        bool holdSelectedEffect,
        bool holdSelectedTransition,
        CueSheetStatus cueSheet,
        CueDecision lastCue)
    {
        Mode = mode;
        Decision = decision;
        IsSyncedMode = isSyncedMode;
        HasCueMark = hasCueMark;
        TimingSource = timingSource;
        CueMarkBeat = cueMarkBeat;
        LastChangeBeat = lastChangeBeat;
        TransitionLandingBeat = transitionLandingBeat;
        CurrentBeat = currentBeat;
        BeatsUntilLanding = beatsUntilLanding;
        BeatsUntilCadenceReady = beatsUntilCadenceReady;
        NextEffectIndex = nextEffectIndex;
        NextEffectName = nextEffectName ?? string.Empty;
        NextTransitionIndex = nextTransitionIndex;
        NextTransitionName = nextTransitionName ?? string.Empty;
        HoldSelectedEffect = holdSelectedEffect;
        HoldSelectedTransition = holdSelectedTransition;
        CueSheet = cueSheet;
        LastCue = lastCue;
    }

    /// <summary>Current live beat observed by the Director, or -1 outside Synced Mode.</summary>
    public readonly int CurrentBeat;
}

/// <summary>
/// Decides what plays and when it changes.
/// The Director reads available musical timing, reads Performer repertoire, and directs the Switcher.
/// </summary>
[Serializable]
public sealed class Director
{
    private const int MinimumChangeCadenceBeats = 16;

    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer standaloneTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;
    private readonly CuePlanner cuePlanner = new CuePlanner();

    private int currentEffectIndexForSelection = -1;
    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;
    private bool nextEffectIsManualSelection;
    private bool nextTransitionIsManualSelection;
    private int lastSyncedBeat = -1;
    private int transitionStartBeat = -1;
    private int transitionLandingBeat = -1;
    private TimingFrame timingFrame = TimingFrame.Unavailable;
    private int lastTrackId = -1;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;
    private int lastLoggedSyncedBeat = -1;
    private int lastDropProtectedBeat = -1;
    private CueDecision lastCueDecision = CueDecision.None;

    private readonly struct TransitionCueSelection
    {
        private TransitionCueSelection(
            int transitionIndex,
            TransitionRepertoire repertoire,
            int deckIndex)
        {
            TransitionIndex = transitionIndex;
            Repertoire = repertoire;
            DeckIndex = deckIndex;
        }

        public readonly int TransitionIndex;
        public readonly TransitionRepertoire Repertoire;
        public readonly int DeckIndex;

        public static TransitionCueSelection Staged(int transitionIndex, TransitionRepertoire repertoire)
        {
            return new TransitionCueSelection(transitionIndex, repertoire, deckIndex: -1);
        }

        public static TransitionCueSelection DeckCandidate(int transitionIndex, TransitionRepertoire repertoire, int deckIndex)
        {
            return new TransitionCueSelection(transitionIndex, repertoire, deckIndex);
        }
    }

    /// <summary>Whether the Director currently has a Cue Mark to aim at.</summary>
    public bool HasCueMark => timingFrame.HasCueMark;

    /// <summary>
    /// Whether the wall is in Synced Mode: a usable beat clock is running. Reads the single mode authority
    /// (<see cref="BeatManager.IsSynced"/>), not OSC transport liveness — OSC connected but idle (sentinels,
    /// no track playing) is Standalone, not Synced (ADR-0007).
    /// </summary>
    public bool IsSyncedMode => controller != null && controller.beatManager != null && controller.beatManager.IsSynced;

    /// <summary>Current read-only sequencing snapshot for runtime HUDs and inspector diagnostics.</summary>
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

    public Director(
        Controller controller,
        Switcher switcher,
        Timer standaloneTimer,
        int[] effectDeck,
        int[] transitionDeck,
        int initialTransitionIndex)
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
        currentEffectIndexForSelection = switcher.CurrentEffectIndex;
        SetNextTransition(initialTransitionIndex);
        nextTransitionIsManualSelection = false;
        StageNextEffect(currentEffectIndexForSelection);
    }

    /// <summary>Advances the Director's current cadence clock or live musical scheduling.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        // Synced Mode needs both the mode authority and a running absolute beat to sequence on structure.
        // If the clock is gone (Standalone) — or a frame of Synced Mode arrives without a usable Beat —
        // fall through to Standalone sequencing rather than freezing on a dead return (ADR-0007).
        if (IsSyncedMode && controller.beatManager.Beat is { } beat)
        {
            TickSyncedMode(beat);
        }
        else
        {
            TickStandaloneMode(deltaTime);
        }
    }

    /// <summary>Immediate developer/manual effect selection. Resets Standalone Mode cadence.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace($"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        switcher.ShowNow(effectIndex);
        currentEffectIndexForSelection = effectIndex;
        cuePlanner.Reset();
        timingFrame = TimingFrame.Unavailable;
        lastCueDecision = CueDecision.None;
        MarkChangedOnCurrentBeat();
        standaloneTimer.Set(durationSeconds);
        standaloneTimer.Reset();
        StageNextChoices();
    }

    /// <summary>
    /// Applies Hold as an inspection freeze. Hold suspends rotation by keeping the held effect on stage.
    /// </summary>
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

    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var currentBeat = isSynced && controller.beatManager.Beat is { } beat ? beat : -1;
        var beatsUntilLanding = timingFrame.HasCueMark && currentBeat >= 0 ? timingFrame.CueMarkBeat - currentBeat : -1;
        var runwayBeats = NextTransitionRepertoire.RunwayBeats;
        var beatsUntilCadenceReady = GetBeatsUntilCadenceReady(currentBeat);

        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var decision = ResolveDecision(isHeld, isSynced, beatsUntilLanding, beatsUntilCadenceReady, runwayBeats);

        return new DirectorStatus(
            mode,
            decision,
            isSynced,
            timingFrame.HasCueMark,
            timingFrame.Source,
            timingFrame.CueMarkBeat,
            cuePlanner.LastChangeBeat,
            transitionLandingBeat,
            currentBeat,
            beatsUntilLanding,
            beatsUntilCadenceReady,
            nextEffectIndex,
            EffectName(nextEffectIndex),
            nextTransitionIndex,
            TransitionName(nextTransitionIndex),
            holdSelectedEffect,
            holdSelectedTransition,
            timingFrame.CueSheet,
            lastCueDecision);
    }

    private DirectorDecision ResolveDecision(
        bool isHeld,
        bool isSynced,
        int beatsUntilLanding,
        int beatsUntilCadenceReady,
        int runwayBeats)
    {
        if (isHeld)
        {
            return DirectorDecision.Hold;
        }

        if (!isSynced)
        {
            return DirectorDecision.StandaloneTimer;
        }

        if (!timingFrame.HasCueMark)
        {
            return DirectorDecision.WaitingForGrid;
        }

        if (beatsUntilCadenceReady > 0)
        {
            return DirectorDecision.WaitingForCadence;
        }

        var inCueWindow = runwayBeats == 0
            ? beatsUntilLanding == 0
            : beatsUntilLanding is >= 1 && beatsUntilLanding <= runwayBeats;
        return inCueWindow ? DirectorDecision.CueingTransition : DirectorDecision.WaitingForRunway;
    }

    private int GetBeatsUntilCadenceReady(int currentBeat)
    {
        if (currentBeat < 0 || cuePlanner.LastChangeBeat == int.MinValue)
        {
            return 0;
        }

        var cadenceReadyBeat = cuePlanner.LastChangeBeat + MinimumChangeCadenceBeats;
        // Match the cue path: cadence belongs to the selected Cue Mark, not the
        // current beat. A tailed transition can complete while the next Cue Mark
        // is already valid.
        var cueMarkSatisfiesCadence = timingFrame.HasCueMark && timingFrame.CueMarkBeat >= cadenceReadyBeat;
        return cueMarkSatisfiesCadence ? 0 : Math.Max(0, cadenceReadyBeat - currentBeat);
    }

    private void TickStandaloneMode(float deltaTime)
    {
        timingFrame = TimingFrame.Unavailable;
        cuePlanner.Reset();
        // The mode boundary owns cue teardown: a beat-domain cue loaded while Synced carries a Unity-time
        // start and would fire into a dead clock, so abort any Switcher-held cue (even a locked one) on
        // entering Standalone. Fire-and-forget and idempotent, so the every-frame call is fine (ADR-0007).
        switcher.AbortLoadedCue();
        standaloneTimer.Update(deltaTime);
    }

    private void TickSyncedMode(int beat)
    {
        ResetCuePlannerOnTrackChange();

        var previousSyncedBeat = lastSyncedBeat;
        lastSyncedBeat = beat;

        RefreshTimingFrame();

        LogBeatRewindIfNeeded(previousSyncedBeat, beat, timingFrame.BeatRewoundToNewPass);
        LogSyncedBeatIfNeeded(beat);

        TryStartSyncedCue(timingFrame);
    }

    /// <summary>
    /// Tail of the staged Next Transition — the late-cue window the planner holds an unconsumed
    /// Cue Mark open for, since a cue there can still start backdated and complete.
    /// </summary>
    private int StagedTransitionTailBeats()
    {
        return IsValidTransitionIndex(nextTransitionIndex)
            ? controller.transitions[nextTransitionIndex].Repertoire.TailBeats
            : 0;
    }

    private void RefreshTimingFrame()
    {
        var previousLandingBeat = timingFrame.CueMarkBeat;
        var input = OnAirTimingInput.From(controller.beatManager);
        timingFrame = cuePlanner.Plan(
            input,
            MinimumChangeCadenceBeats,
            StagedTransitionTailBeats());

        if (timingFrame.HasCueMark && timingFrame.CueMarkBeat != previousLandingBeat)
        {
            Trace($"ANCHOR_SET beat={timingFrame.CurrentBeat} input={FormatTimingInput()} target={FormatTimingSource(timingFrame.Source)} landing={timingFrame.CueMarkBeat} previousLanding={FormatBeat(previousLandingBeat)}");
        }
    }

    /// <summary>
    /// Resets the cue planner when the on-air track identity changes. RaveSystem assigns each track a
    /// stable id, so a change means the beat counter restarted on a new song; stale cadence memory must
    /// not cross tracks whose counters do not rewind (replaces GridSync's track-change reset).
    /// </summary>
    private void ResetCuePlannerOnTrackChange()
    {
        var trackId = controller.beatManager.TrackId ?? -1;
        if (trackId != lastTrackId)
        {
            cuePlanner.Reset();
            lastTrackId = trackId;
        }
    }

    private SwitcherClockSnapshot CurrentSwitcherClockSnapshot(int beat)
    {
        return new SwitcherClockSnapshot(
            beat,
            controller.beatManager.BeatFraction ?? 0f,
            CurrentSecondsPerBeat(),
            Time.time);
    }

    private void CommitSentCue(int beat, SwitcherCueDirection cue, TransitionBeatPlan beatPlan)
    {
        transitionStartBeat = beatPlan.StartBeat;
        transitionLandingBeat = beatPlan.ImpactBeat;
        cuePlanner.MarkChanged(beatPlan.ImpactBeat);
        cuePlanner.RecordCueIssued(beat);
        controller.currentTransition = cue.TransitionIndex;
        currentEffectIndexForSelection = cue.TargetEffectIndex;
        StageNextChoices(currentEffectIndexForSelection);
        Trace($"SYNC_CUE_SENT beat={beat} start={beatPlan.StartBeat} impact={beatPlan.ImpactBeat} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.TargetEffectIndex)} runway={cue.TransitionRepertoire.RunwayBeats}");
    }

    private bool TryStartSyncedCue(TimingFrame frame)
    {
        if (!frame.HasCueMark)
        {
            return false;
        }

        var beat = frame.CurrentBeat;
        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            controller.beatManager.Fill,
            controller.beatManager.Drop);

        // Drop guard (drop-protect, half A): when a Drop coincides with this Cue Mark and the on-air
        // Performer already expresses Drops, issue NO cue. A transition landing on the drop would step on
        // the effect's own slam, and the effect reads beatManager.Drop itself — so the most dramatic drop
        // is to leave the capable Performer on stage. Bails before any deck pull/consume; the Cue Mark is
        // not stranded because the cursor advances once the beat passes the mark and a phrase boundary
        // promotes off live Track Phase, not off cue consumption. (Cast-ahead, half B, runs the Grid
        // before via CueEventIntent.DropApproaching, so a Drop-capable Performer is usually already here.)
        if (eventIntent == CueEventIntent.Drop
            && IsValidEffectIndex(currentEffectIndexForSelection)
            && (controller.EffectiveRepertoire(currentEffectIndexForSelection) & Repertoire.HandlesDrop) != 0)
        {
            lastCueDecision = new CueDecision(
                CueDecisionOutcome.DropProtected,
                beat,
                frame.CueMarkBeat,
                eventIntent,
                Repertoire.HandlesDrop,
                currentEffectIndexForSelection,
                EffectName(currentEffectIndexForSelection),
                CueCastSource.None,
                transitionIndex: -1,
                transitionName: string.Empty,
                CueCastSource.None);
            if (beat != lastDropProtectedBeat)
            {
                Trace($"SYNC_CUE_DROP_PROTECTED beat={beat} cueMark={frame.CueMarkBeat} current={FormatEffect(currentEffectIndexForSelection)}");
                lastDropProtectedBeat = beat;
            }

            return false;
        }

        var transitionSelection = SelectTransitionForEventIntent(frame, eventIntent);
        var transitionIndex = transitionSelection.TransitionIndex;
        var stagedEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(stagedEffectIndex);
        var repertoire = transitionSelection.Repertoire;
        var preferredRepertoire = SyncedCueIntent.PreferredRepertoireFor(eventIntent);
        var transitionSource = transitionSelection.DeckIndex >= 0
            ? CueCastSource.DeckFind
            : preferredRepertoire != Repertoire.None && (repertoire.Tags & preferredRepertoire) == 0
                ? CueCastSource.NoPreferredAvailable
                : CueCastSource.Staged;
        var beatPlan = TransitionBeatPlan.FromCueMark(frame.CueMarkBeat, repertoire);

        // Energy casts the effect (never the transition, never a Cue Mark): once the Impact Point is known,
        // prefer the level the cast Performer will actually spend its cadence stint in. Event intent (Drop/Fill)
        // still outranks it — folded in behind that inside SyncedCueIntent.Cast.
        var energyPreference = EnergyCasting.PreferredEnergyRepertoire(
            controller.beatManager.Energy, beat, beatPlan.ImpactBeat, MinimumChangeCadenceBeats);
        var effectivePreferredRepertoire = preferredRepertoire != Repertoire.None ? preferredRepertoire : energyPreference;

        var verdict = cuePlanner.EvaluateCueTiming(beatPlan, beat, MinimumChangeCadenceBeats);
        if (verdict == CueTimingVerdict.Wait)
        {
            return false;
        }

        if (verdict == CueTimingVerdict.BlockedByCadence)
        {
            lastCueDecision = new CueDecision(
                CueDecisionOutcome.BlockedByCadence,
                beat,
                beatPlan.ImpactBeat,
                eventIntent,
                effectivePreferredRepertoire,
                effectIndex: -1,
                effectName: string.Empty,
                CueCastSource.None,
                transitionIndex,
                TransitionName(transitionIndex),
                transitionSource);
            Trace($"SYNC_CUE_BLOCKED_CADENCE beat={beat} cueMark={beatPlan.ImpactBeat} runway={repertoire.RunwayBeats} lastChange={FormatBeat(cuePlanner.LastChangeBeat)}");
            cuePlanner.RecordCueIssued(beat);
            return true;
        }

        var cueIntent = SyncedCueIntent.Cast(
            eventIntent,
            stagedEffectIndex,
            preserveStagedEffect: holdSelectedEffect || nextEffectIsManualSelection,
            currentEffectIndex: currentEffectIndexForSelection,
            deck: effectDeck,
            repertoireForEffect: effectIndex => controller.EffectiveRepertoire(effectIndex),
            energyPreference: energyPreference);

        ValidateEffectIndex(cueIntent.TargetEffectIndex);
        var effectSource = cueIntent.EffectDeckIndex >= 0
            ? CueCastSource.DeckFind
            : cueIntent.PreferredRepertoire != Repertoire.None && !cueIntent.CastPreferredPerformer
                ? CueCastSource.NoPreferredAvailable
                : CueCastSource.Staged;
        var cue = new SwitcherCueDirection(
            beatPlan.ImpactBeat,
            cueIntent.TargetEffectIndex,
            transitionIndex,
            repertoire);
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            lastCueDecision = new CueDecision(
                CueDecisionOutcome.Held,
                beat,
                beatPlan.ImpactBeat,
                eventIntent,
                cueIntent.PreferredRepertoire,
                cueIntent.TargetEffectIndex,
                EffectName(cueIntent.TargetEffectIndex),
                effectSource,
                transitionIndex,
                TransitionName(transitionIndex),
                transitionSource);
            cuePlanner.RecordCueIssued(beat);
            Trace($"SYNC_CUE_HELD beat={beat} held={FormatEffect(heldEffectIndex)} start={beatPlan.StartBeat} impact={beatPlan.ImpactBeat} transition={FormatTransition(transitionIndex)} target={FormatEffect(cueIntent.TargetEffectIndex)}");
            return true;
        }

        // Deck candidates rotate only when a cue is actually sent (the Cue Intent contract): the
        // event-cast cards found above are pulled here, at the commit point, so the fresh choices
        // staged in CommitSentCue cannot redraw them — and a Wait, cadence block, or Hold leaves
        // the decks untouched.
        if (transitionSelection.DeckIndex >= 0)
        {
            Deck.PullAt(transitionDeck, transitionSelection.DeckIndex);
            Trace($"NEXT_TRANSITION_EVENT_STAGED nextTransition={FormatTransition(transitionIndex)} preferred={preferredRepertoire}");
        }

        if (cueIntent.EffectDeckIndex >= 0)
        {
            Deck.PullAt(effectDeck, cueIntent.EffectDeckIndex);
        }

        var clock = CurrentSwitcherClockSnapshot(beat);
        switcher.UpsertLoadedCue(cue, clock);
        CommitSentCue(beat, cue, beatPlan);
        lastCueDecision = new CueDecision(
            CueDecisionOutcome.Sent,
            beat,
            beatPlan.ImpactBeat,
            eventIntent,
            cueIntent.PreferredRepertoire,
            cueIntent.TargetEffectIndex,
            EffectName(cueIntent.TargetEffectIndex),
            effectSource,
            transitionIndex,
            TransitionName(transitionIndex),
            transitionSource);
        return true;
    }

    private TransitionCueSelection SelectTransitionForEventIntent(TimingFrame frame, CueEventIntent eventIntent)
    {
        var preferredRepertoire = SyncedCueIntent.PreferredRepertoireFor(eventIntent);
        var stagedTransitionIndex = nextTransitionIndex;
        var stagedRepertoire = controller.transitions[stagedTransitionIndex].Repertoire;
        if (holdSelectedTransition || nextTransitionIsManualSelection || preferredRepertoire == Repertoire.None)
        {
            return TransitionCueSelection.Staged(stagedTransitionIndex, stagedRepertoire);
        }

        if ((stagedRepertoire.Tags & preferredRepertoire) != 0
            && CanTransitionCueNow(frame, stagedRepertoire))
        {
            return TransitionCueSelection.Staged(stagedTransitionIndex, stagedRepertoire);
        }

        if (Deck.TryFindPreferred(
            transitionDeck,
            candidateIndex =>
            {
                var candidateRepertoire = controller.transitions[candidateIndex].Repertoire;
                return (candidateRepertoire.Tags & preferredRepertoire) != 0
                    && CanTransitionCueNow(frame, candidateRepertoire);
            },
            out var deckIndex))
        {
            var preferredTransitionIndex = transitionDeck[deckIndex];
            var preferredTransitionRepertoire = controller.transitions[preferredTransitionIndex].Repertoire;
            return TransitionCueSelection.DeckCandidate(preferredTransitionIndex, preferredTransitionRepertoire, deckIndex);
        }

        return TransitionCueSelection.Staged(stagedTransitionIndex, stagedRepertoire);
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

    private void LogBeatRewindIfNeeded(int previousBeat, int beat, bool beatRewoundToNewPass)
    {
        if (!beatRewoundToNewPass)
        {
            return;
        }

        Trace($"BEAT_REWIND previousBeat={previousBeat} currentBeat={beat} input={FormatTimingInput()} anchor={FormatBeat(timingFrame.CueMarkBeat)} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} lastChange={FormatBeat(cuePlanner.LastChangeBeat)}");
    }

    private void LogSyncedBeatIfNeeded(int beat)
    {
        if (beat == lastLoggedSyncedBeat)
        {
            return;
        }

        var beatsUntilLanding = timingFrame.HasCueMark ? timingFrame.CueMarkBeat - beat : -1;
        var canChangeAtLanding = timingFrame.HasCueMark && CanChangeAtBeat(timingFrame.CueMarkBeat);
        Trace($"SYNC_BEAT beat={beat} input={FormatTimingInput()} source={FormatTimingSource(timingFrame.Source)} anchor={FormatBeat(timingFrame.CueMarkBeat)} until={FormatBeat(beatsUntilLanding)} canChangeAtLanding={canChangeAtLanding} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} lastChange={FormatBeat(cuePlanner.LastChangeBeat)}");
        lastLoggedSyncedBeat = beat;
    }

    private TransitionRepertoire NextTransitionRepertoire =>
        nextTransitionIndex >= 0 && nextTransitionIndex < controller.transitions.Length
            ? controller.transitions[nextTransitionIndex].Repertoire
            : TransitionRepertoire.Default;

    private float CurrentSecondsPerBeat()
    {
        return controller.beatManager.Bpm is { } bpm && bpm > 0f ? 60f / bpm : 0.5f;
    }

    private bool CanChangeAtBeat(int beat)
    {
        return cuePlanner.CanChangeAt(beat, MinimumChangeCadenceBeats);
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

    private string FormatTimingInput()
    {
        var input = timingFrame.Input;
        return $"beat={input.Beat},phaseCount={FormatBeat(input.BeatsUntilPhraseEnd)},phaseLength={FormatBeat(input.PhraseLengthBeats)},nextStart={FormatBeat(input.NextPhraseStartInBeats)},nextLength={FormatBeat(input.NextPhraseLengthBeats)}";
    }

    private static string FormatTimingSource(TimingFrameSource source)
    {
        switch (source)
        {
            case TimingFrameSource.CueMark:
                return "cue-mark";
            case TimingFrameSource.TrackPhaseBoundary:
                return "track-phase-boundary";
            default:
                return "unlocked";
        }
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "none";
    }

    private static string FormatNullableBeat(int? beat)
    {
        return beat is { } value ? value.ToString() : "none";
    }

    private static bool CanTransitionCueNow(TimingFrame frame, TransitionRepertoire repertoire)
    {
        return TransitionBeatPlan.FromCueMark(frame.CueMarkBeat, repertoire).IsCueBeat(frame.CurrentBeat);
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

    private void MarkChangedOnCurrentBeat()
    {
        // A non-null Beat already implies the mode authority is Synced (Beat gates on IsActive => IsSynced),
        // so the running beat is the only gate needed here — IsLiveSource is connectivity, never mode (ADR-0007).
        if (controller.beatManager.Beat is { } beat)
        {
            cuePlanner.MarkChanged(beat);
        }
    }

    

    
}
