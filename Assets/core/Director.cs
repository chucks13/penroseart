using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>High-level cadence source currently driving the Director.</summary>
public enum DirectorMode
{
    NotReady,
    Default,
    Synced,
    Hold
}

/// <summary>Current scheduling reason reported by the Director for observability.</summary>
public enum DirectorDecision
{
    NotReady,
    DefaultTimer,
    DefaultTransition,
    WaitingForPhase,
    WaitingForRunway,
    WaitingForCadence,
    CueingTransition,
    Transitioning,
    Hold
}

/// <summary>
/// Read-only snapshot of Director sequencing state for the HUD and Unity Inspector.
/// </summary>
public readonly struct DirectorStatus
{
    public static DirectorStatus NotReady { get; } = new DirectorStatus(
        DirectorMode.NotReady,
        DirectorDecision.NotReady,
        false,
        false,
        false,
        PhaseConfidence.Unlocked,
        PhaseReading.Unavailable,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        0f);

    public readonly DirectorMode Mode;
    public readonly DirectorDecision Decision;
    public readonly bool IsSyncedMode;
    public readonly bool IsHeld;
    public readonly bool HasPhaseAnchor;
    public readonly PhaseConfidence PhaseAnchorConfidence;
    public readonly PhaseReading Phase;
    public readonly int PhaseAnchorLandingBeat;
    public readonly int LastSyncedBeat;
    public readonly int LastChangeBeat;
    public readonly int TransitionStartBeat;
    public readonly int TransitionLandingBeat;
    public readonly int BeatsUntilLanding;
    public readonly int BeatsUntilRunway;
    public readonly int BeatsUntilCadenceReady;
    public readonly float TransitionProgress;

    public DirectorStatus(
        DirectorMode mode,
        DirectorDecision decision,
        bool isSyncedMode,
        bool isHeld,
        bool hasPhaseAnchor,
        PhaseConfidence phaseAnchorConfidence,
        PhaseReading phase,
        int phaseAnchorLandingBeat,
        int lastSyncedBeat,
        int lastChangeBeat,
        int transitionStartBeat,
        int transitionLandingBeat,
        int currentBeat,
        int beatsUntilLanding,
        int beatsUntilRunway,
        int beatsUntilCadenceReady,
        float transitionProgress)
    {
        Mode = mode;
        Decision = decision;
        IsSyncedMode = isSyncedMode;
        IsHeld = isHeld;
        HasPhaseAnchor = hasPhaseAnchor;
        PhaseAnchorConfidence = phaseAnchorConfidence;
        Phase = phase;
        PhaseAnchorLandingBeat = phaseAnchorLandingBeat;
        LastSyncedBeat = lastSyncedBeat;
        LastChangeBeat = lastChangeBeat;
        TransitionStartBeat = transitionStartBeat;
        TransitionLandingBeat = transitionLandingBeat;
        CurrentBeat = currentBeat;
        BeatsUntilLanding = beatsUntilLanding;
        BeatsUntilRunway = beatsUntilRunway;
        BeatsUntilCadenceReady = beatsUntilCadenceReady;
        TransitionProgress = transitionProgress;
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

    private readonly struct TransitionPlan
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

        public TransitionPlan(
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

        public float Progress(float now)
        {
            return DurationSeconds > 0f ? Mathf.Clamp01((now - StartTime) / DurationSeconds) : 1f;
        }

        public bool IsComplete(float now)
        {
            return Progress(now) >= 1f;
        }
    }

    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer defaultTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;

    private int nextTransitionIndex;
    private int lastSyncedBeat = -1;
    private int lastChangeBeat = int.MinValue;
    private int lastCueBeat = -1;
    private int transitionStartBeat = -1;
    private int transitionLandingBeat = -1;
    private TransitionPlan transitionPlan;
    private bool hasPhaseAnchor;
    private PhaseConfidence phaseAnchorConfidence = PhaseConfidence.Unlocked;
    private int phaseAnchorLandingBeat = -1;
    private PhaseReading phaseReading = PhaseReading.Unavailable;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;
    private int lastLoggedSyncedBeat = -1;
    private int lastLoggedTransitionBeat = -1;
    private PhaseInput phaseInput = new PhaseInput(-1, -1, -1, -1, -1, -1);
    private int trackPhaseStartBeat = -1;
    private int trackPhaseBoundaryBeat = -1;
    private int[] trackPhaseImpactBeats = Array.Empty<int>();
    private int trackPhaseImpactIndex;

    /// <summary>
    /// Progress for the current mechanical transition. Default Mode uses the legacy timer;
    /// Synced Mode derives it from the live beat count so the Switcher never interprets timing.
    /// </summary>
    public float TransitionProgress { get; private set; }

    /// <summary>Whether the Director currently has a phase grid to aim at.</summary>
    public bool HasPhaseAnchor => hasPhaseAnchor;

    /// <summary>Confidence for the current phase anchor.</summary>
    public PhaseConfidence PhaseAnchorConfidence => phaseAnchorConfidence;

    /// <summary>Absolute beat where the current phase anchor next lands, or -1 when unlocked.</summary>
    public int PhaseAnchorLandingBeat => phaseAnchorLandingBeat;

    /// <summary>Last live OSC beat observed by the Director, or -1 before Synced Mode starts.</summary>
    public int LastSyncedBeat => lastSyncedBeat;

    /// <summary>Whether live beat data is currently allowed to drive sequencing.</summary>
    public bool IsSyncedMode => controller.beatManager.IsLiveSource
        && controller.beatManager.Beat is { };

    /// <summary>Current read-only sequencing snapshot for runtime HUDs and inspector diagnostics.</summary>
    public DirectorStatus Status => BuildStatus();

    public Director(
        Controller controller,
        Switcher switcher,
        Timer defaultTimer,
        int[] effectDeck,
        int[] transitionDeck,
        int initialTransitionIndex)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.switcher = switcher ?? throw new ArgumentNullException(nameof(switcher));
        this.defaultTimer = defaultTimer ?? throw new ArgumentNullException(nameof(defaultTimer));
        this.effectDeck = effectDeck ?? throw new ArgumentNullException(nameof(effectDeck));
        this.transitionDeck = transitionDeck ?? throw new ArgumentNullException(nameof(transitionDeck));
        nextTransitionIndex = initialTransitionIndex;
    }

    /// <summary>Advances the Director's current cadence clock or live musical scheduling.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        if (IsSyncedMode && controller.beatManager.Beat is { } beat)
        {
            TickSyncedMode(beat);
            return;
        }

        TickDefaultMode(deltaTime);
    }

    /// <summary>Immediate developer/manual effect selection. Resets Default Mode cadence.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace($"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        switcher.ShowNow(effectIndex);
        transitionPlan = default;
        ResetTrackPhasePlan();
        MarkChangedOnCurrentBeat();
        TransitionProgress = 0f;
        defaultTimer.Set(durationSeconds);
        defaultTimer.Reset();
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

        if (switcher.IsTransitioning || switcher.CurrentEffectIndex != heldEffectIndex)
        {
            ShowNow(heldEffectIndex, controller.effectTime);
        }
    }

    /// <summary>Default Mode timer callback.</summary>
    public void OnTimerFinished()
    {
        if (IsSyncedMode)
        {
            Trace($"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Beat)} transitioning={switcher.IsTransitioning}");
            return;
        }

        Trace($"TIMER_FINISHED_DEFAULT transitioning={switcher.IsTransitioning} progress={TransitionProgress:0.###}");
        RunDefaultModeTimerDecision();
    }

    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var currentBeat = isSynced && controller.beatManager.Beat is { } beat ? beat : -1;
        var beatsUntilLanding = hasPhaseAnchor && currentBeat >= 0 ? phaseAnchorLandingBeat - currentBeat : -1;
        var runwayBeats = NextTransitionRepertoire.RunwayBeats;
        var beatsUntilRunway = beatsUntilLanding >= 0 ? beatsUntilLanding - runwayBeats : -1;
        var beatsUntilCadenceReady = currentBeat >= 0 && lastChangeBeat != int.MinValue
            ? Math.Max(0, MinimumChangeCadenceBeats - (currentBeat - lastChangeBeat))
            : 0;

        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Default;
        var decision = ResolveDecision(isHeld, isSynced, beatsUntilLanding, beatsUntilCadenceReady, runwayBeats);

        return new DirectorStatus(
            mode,
            decision,
            isSynced,
            isHeld,
            hasPhaseAnchor,
            phaseAnchorConfidence,
            phaseReading,
            phaseAnchorLandingBeat,
            lastSyncedBeat,
            lastChangeBeat,
            transitionStartBeat,
            transitionLandingBeat,
            currentBeat,
            beatsUntilLanding,
            beatsUntilRunway,
            beatsUntilCadenceReady,
            TransitionProgress);
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
            return switcher.IsTransitioning ? DirectorDecision.DefaultTransition : DirectorDecision.DefaultTimer;
        }

        if (switcher.IsTransitioning)
        {
            return DirectorDecision.Transitioning;
        }

        if (!hasPhaseAnchor)
        {
            return DirectorDecision.WaitingForPhase;
        }

        if (beatsUntilCadenceReady > 0)
        {
            return DirectorDecision.WaitingForCadence;
        }

        return beatsUntilLanding is >= 1 && beatsUntilLanding <= runwayBeats
            ? DirectorDecision.CueingTransition
            : DirectorDecision.WaitingForRunway;
    }

    private void TickDefaultMode(float deltaTime)
    {
        phaseReading = PhaseReading.Unavailable;
        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
        transitionPlan = default;
        ResetTrackPhasePlan();
        TransitionProgress = defaultTimer.Value;
        defaultTimer.Update(deltaTime);
        TransitionProgress = defaultTimer.Value;
    }

    private void TickSyncedMode(int beat)
    {
        var previousSyncedBeat = lastSyncedBeat;
        var beatRewoundToNewPass = BeatRewoundToNewPass(previousSyncedBeat, beat);
        lastSyncedBeat = beat;

        if (beatRewoundToNewPass)
        {
            lastCueBeat = -1;
            ResetTrackPhasePlan();
            if (lastChangeBeat > beat)
            {
                lastChangeBeat = int.MinValue;
            }
        }

        if (controller.TryGetHeldEffectIndex(out _))
        {
            TransitionProgress = 0f;
            return;
        }

        RefreshPhaseAnchor(beat);
        LogBeatRewindIfNeeded(previousSyncedBeat, beat, beatRewoundToNewPass);
        LogSyncedBeatIfNeeded(beat);

        if (switcher.IsTransitioning)
        {
            UpdateSyncedTransition(beat, previousSyncedBeat, beatRewoundToNewPass);
            return;
        }

        TransitionProgress = 0f;
        TryStartSyncedCue(beat);
    }

    private void RefreshPhaseAnchor(int beat)
    {
        var previousLandingBeat = phaseAnchorLandingBeat;
        var previousConfidence = phaseAnchorConfidence;
        phaseInput = BuildPhaseInput();
        phaseReading = PhaseClock.Resolve(phaseInput);
        if (phaseReading.Confidence != PhaseConfidence.Unlocked)
        {
            phaseAnchorLandingBeat = ResolvePhaseImpactBeat(beat, out var targetSource);
            hasPhaseAnchor = true;
            phaseAnchorConfidence = phaseReading.Confidence;
            if (phaseAnchorLandingBeat != previousLandingBeat || phaseAnchorConfidence != previousConfidence)
            {
                Trace($"ANCHOR_SET beat={beat} input={FormatPhaseInput()} phase={FormatPhase()} target={targetSource} landing={phaseAnchorLandingBeat} previousLanding={FormatBeat(previousLandingBeat)}");
            }

            return;
        }

        if (hasPhaseAnchor)
        {
            CoastPhaseAnchor(beat);
            if (phaseAnchorLandingBeat != previousLandingBeat)
            {
                Trace($"ANCHOR_COAST beat={beat} input={FormatPhaseInput()} landing={phaseAnchorLandingBeat} previousLanding={FormatBeat(previousLandingBeat)}");
            }

            return;
        }

        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
    }

    private PhaseInput BuildPhaseInput()
    {
        var beatManager = controller.beatManager;
        var phase = beatManager.Phase;
        return new PhaseInput(
            beatManager.Beat ?? -1,
            beatManager.TotalBeats ?? -1,
            beatManager.BeatInBar ?? -1,
            phase is { inPhase: true } ? 1 : phase is { } ? 0 : -1,
            phase?.beatsUntilNext ?? -1,
            phase?.lengthBeats ?? -1);
    }

    private void CoastPhaseAnchor(int beat)
    {
        while (beat >= phaseAnchorLandingBeat)
        {
            phaseAnchorLandingBeat += MinimumChangeCadenceBeats;
        }
    }

    private int ResolvePhaseImpactBeat(int beat, out string targetSource)
    {
        if (TryResolveTrackPhaseImpactBeat(beat, out var trackPhaseImpactBeat, out targetSource))
        {
            return trackPhaseImpactBeat;
        }

        ResetTrackPhasePlan();
        targetSource = "phase-clock-grid";
        return GetLandingBeatFromPhasePosition(beat, phaseReading.PhasePosition);
    }

    private bool TryResolveTrackPhaseImpactBeat(int beat, out int impactBeat, out string targetSource)
    {
        impactBeat = -1;
        targetSource = "none";
        if (phaseInput.PhaseActive != 1
            || !PhraseWindow.TryFromTrackPhase(
                beat,
                phaseInput.PhaseCountBeats,
                phaseInput.PhaseLengthBeats,
                out var phraseWindow))
        {
            return false;
        }

        impactBeat = ResolveTrackPhasePlanImpactBeat(beat, phraseWindow, out targetSource);
        return true;
    }

    private int ResolveTrackPhasePlanImpactBeat(int beat, PhraseWindow phraseWindow, out string targetSource)
    {
        if (NeedsTrackPhasePlan(beat, phraseWindow.StartBeat, phraseWindow.EndBeat))
        {
            BuildTrackPhasePlan(beat, phraseWindow);
        }

        while (trackPhaseImpactIndex < trackPhaseImpactBeats.Length - 1
            && trackPhaseImpactBeats[trackPhaseImpactIndex] <= beat)
        {
            trackPhaseImpactIndex++;
        }

        var target = trackPhaseImpactBeats.Length > 0
            ? trackPhaseImpactBeats[Mathf.Clamp(trackPhaseImpactIndex, 0, trackPhaseImpactBeats.Length - 1)]
            : phraseWindow.EndBeat;
        targetSource = target == trackPhaseBoundaryBeat ? "track-phase-boundary" : "track-phase-slot";
        return target;
    }

    private bool NeedsTrackPhasePlan(int beat, int phraseStartBeat, int phraseBoundaryBeat)
    {
        if (trackPhaseImpactBeats.Length == 0 || trackPhaseImpactBeats[trackPhaseImpactBeats.Length - 1] <= beat)
        {
            return true;
        }

        var samePhraseWindow = Math.Abs(phraseStartBeat - trackPhaseStartBeat) < PhraseWindow.DefaultSlotBeats
            && Math.Abs(phraseBoundaryBeat - trackPhaseBoundaryBeat) < PhraseWindow.DefaultSlotBeats;
        return !samePhraseWindow;
    }

    private void BuildTrackPhasePlan(int beat, PhraseWindow phraseWindow)
    {
        trackPhaseStartBeat = phraseWindow.StartBeat;
        trackPhaseBoundaryBeat = phraseWindow.EndBeat;
        trackPhaseImpactIndex = 0;

        var futureInteriorSlots = new List<int>();
        foreach (var slotBeat in phraseWindow.ImpactSlotsAfter(beat))
        {
            if (slotBeat < phraseWindow.EndBeat && CanChangeAtBeat(slotBeat))
            {
                futureInteriorSlots.Add(slotBeat);
            }
        }

        var selectedTargets = new List<int>();
        var interiorTransitionCount = futureInteriorSlots.Count > 0
            ? UnityEngine.Random.Range(0, futureInteriorSlots.Count + 1)
            : 0;
        for (var i = 0; i < interiorTransitionCount; i++)
        {
            var chosenIndex = UnityEngine.Random.Range(0, futureInteriorSlots.Count);
            selectedTargets.Add(futureInteriorSlots[chosenIndex]);
            futureInteriorSlots.RemoveAt(chosenIndex);
        }

        if (CanChangeAtBeat(phraseWindow.EndBeat))
        {
            selectedTargets.Add(phraseWindow.EndBeat);
        }

        selectedTargets.Sort();
        trackPhaseImpactBeats = selectedTargets.Count > 0
            ? selectedTargets.ToArray()
            : new[] { phraseWindow.EndBeat };
        Trace($"TRACK_PHASE_PLAN beat={beat} phraseStart={phraseWindow.StartBeat} boundary={phraseWindow.EndBeat} targets={FormatBeatList(trackPhaseImpactBeats)} interiorSelected={interiorTransitionCount} lastChange={FormatBeat(lastChangeBeat)}");
    }

    private void ResetTrackPhasePlan()
    {
        trackPhaseStartBeat = -1;
        trackPhaseBoundaryBeat = -1;
        trackPhaseImpactBeats = Array.Empty<int>();
        trackPhaseImpactIndex = 0;
    }

    private static int GetLandingBeatFromPhasePosition(int beat, int phasePosition)
    {
        var beatsUntilLanding = PhaseClock.PhraseBeats - phasePosition + 1;
        return beat + beatsUntilLanding;
    }

    private void TryStartSyncedCue(int beat)
    {
        if (!hasPhaseAnchor || lastCueBeat == beat)
        {
            return;
        }

        var transitionIndex = nextTransitionIndex;
        var repertoire = controller.transitions[transitionIndex].Repertoire;
        var impactBeat = phaseAnchorLandingBeat;
        var beatPlan = TransitionBeatPlan.FromImpactBeat(impactBeat, repertoire);
        var startBeat = beatPlan.StartBeat;
        var beatsUntilImpact = impactBeat - beat;
        if (beatsUntilImpact < 1 || beatsUntilImpact > repertoire.RunwayBeats)
        {
            return;
        }

        if (!CanChangeAtBeat(impactBeat))
        {
            Trace($"SYNC_CUE_BLOCKED_CADENCE beat={beat} impact={impactBeat} runway={repertoire.RunwayBeats} lastChange={FormatBeat(lastChangeBeat)}");
            lastCueBeat = beat;
            return;
        }

        var preferredRepertoire = PreferredRepertoireForLanding(beatsUntilImpact);
        Trace($"SYNC_CUE beat={beat} start={startBeat} impact={impactBeat} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} lateBy={Math.Max(0, beat - startBeat)} preferred={preferredRepertoire}");
        StartSyncedTransition(transitionIndex, beatPlan, repertoire, preferredRepertoire);
    }

    private Repertoire PreferredRepertoireForLanding(int beatsUntilLanding)
    {
        return controller.beatManager.Drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart } && dropBeatsUntilStart == beatsUntilLanding
            ? Repertoire.HandlesDrop
            : Repertoire.None;
    }

    private void StartSyncedTransition(
        int transitionIndex,
        TransitionBeatPlan beatPlan,
        TransitionRepertoire repertoire,
        Repertoire preferredRepertoire)
    {
        var targetEffectIndex = PullEffect(preferredRepertoire);
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;

        var secondsPerBeat = CurrentSecondsPerBeat();
        var beatFraction = controller.beatManager.BeatFraction ?? 0f;
        var elapsedBeats = Mathf.Max(0f, lastSyncedBeat - beatPlan.StartBeat + beatFraction);
        var startTime = Time.time - (elapsedBeats * secondsPerBeat);
        transitionPlan = new TransitionPlan(transitionIndex, targetEffectIndex, beatPlan, repertoire, startTime, secondsPerBeat);

        transitionStartBeat = transitionPlan.StartBeat;
        transitionLandingBeat = transitionPlan.ImpactBeat;
        lastCueBeat = lastSyncedBeat;
        lastLoggedTransitionBeat = -1;
        TransitionProgress = transitionPlan.Progress(Time.time);
        Trace($"SYNC_TRANSITION_START beat={lastSyncedBeat} beatFraction={beatFraction:0.###} elapsedBeats={elapsedBeats:0.###} start={transitionStartBeat} impact={transitionLandingBeat} complete={transitionPlan.CompleteBeat} durationSeconds={transitionPlan.DurationSeconds:0.###} progress={TransitionProgress:0.###} transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} preferred={preferredRepertoire}");
    }

    private void UpdateSyncedTransition(int beat, int previousBeat, bool beatRewoundToNewPass)
    {
        if (!transitionPlan.Active)
        {
            Trace($"SYNC_TRANSITION_MISSING_PLAN beat={beat} transition={FormatTransition(switcher.CurrentTransitionIndex)} target={FormatEffect(switcher.TransitionTargetEffectIndex)}");
            TransitionProgress = 0f;
            return;
        }

        var now = Time.time;
        TransitionProgress = transitionPlan.Progress(now);
        if (beatRewoundToNewPass)
        {
            transitionLandingBeat = beat;
            MarkChangedOnBeat(beat);
            Trace($"SYNC_TRANSITION_IMPACT_ON_REWIND beat={beat} previousBeat={FormatBeat(previousBeat)} plannedImpact={transitionPlan.ImpactBeat} complete={transitionPlan.CompleteBeat} progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
        }

        if (transitionPlan.IsComplete(now))
        {
            Trace($"SYNC_TRANSITION_COMPLETE_REQUEST beat={beat} previousBeat={FormatBeat(previousBeat)} rewound={beatRewoundToNewPass} impact={transitionLandingBeat} plannedImpact={transitionPlan.ImpactBeat} complete={transitionPlan.CompleteBeat} progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
            switcher.CompleteTransition();
            MarkChangedOnBeat(transitionLandingBeat);
            nextTransitionIndex = PullCard(transitionDeck);
            controller.currentTransition = nextTransitionIndex;
            transitionPlan = default;
            TransitionProgress = 0f;
            Trace($"SYNC_TRANSITION_COMPLETE_DONE beat={beat} rewound={beatRewoundToNewPass} current={FormatEffect(switcher.CurrentEffectIndex)} lastChange={FormatBeat(lastChangeBeat)} nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        if (beat != lastLoggedTransitionBeat)
        {
            Trace($"SYNC_TRANSITION_PROGRESS beat={beat} rewound={beatRewoundToNewPass} start={transitionPlan.StartBeat} impact={transitionLandingBeat} plannedImpact={transitionPlan.ImpactBeat} complete={transitionPlan.CompleteBeat} progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
            lastLoggedTransitionBeat = beat;
        }
    }

    private void LogModeIfChanged()
    {
        var mode = controller.TryGetHeldEffectIndex(out _)
            ? DirectorMode.Hold
            : IsSyncedMode
                ? DirectorMode.Synced
                : DirectorMode.Default;

        if (mode == lastLoggedMode)
        {
            return;
        }

        Trace($"MODE {lastLoggedMode}->{mode} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)} transitioning={switcher.IsTransitioning}");
        lastLoggedMode = mode;
    }

    private void LogBeatRewindIfNeeded(int previousBeat, int beat, bool beatRewoundToNewPass)
    {
        if (!beatRewoundToNewPass)
        {
            return;
        }

        Trace($"BEAT_REWIND previousBeat={previousBeat} currentBeat={beat} input={FormatPhaseInput()} phase={FormatPhase()} anchor={FormatBeat(phaseAnchorLandingBeat)} transitioning={switcher.IsTransitioning} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} progress={TransitionProgress:0.###} lastChange={FormatBeat(lastChangeBeat)} target={FormatEffect(switcher.TransitionTargetEffectIndex)}");
    }

    private static bool BeatRewoundToNewPass(int previousBeat, int beat)
    {
        return previousBeat >= 1
            && beat >= 1
            && beat < previousBeat
            && previousBeat - beat + 1 >= MinimumChangeCadenceBeats;
    }

    private void LogSyncedBeatIfNeeded(int beat)
    {
        if (beat == lastLoggedSyncedBeat)
        {
            return;
        }

        var beatsUntilLanding = hasPhaseAnchor ? phaseAnchorLandingBeat - beat : -1;
        var canChangeAtLanding = hasPhaseAnchor && CanChangeAtBeat(phaseAnchorLandingBeat);
        Trace($"SYNC_BEAT beat={beat} input={FormatPhaseInput()} phase={FormatPhase()} anchor={FormatBeat(phaseAnchorLandingBeat)} until={FormatBeat(beatsUntilLanding)} canChangeAtLanding={canChangeAtLanding} transitioning={switcher.IsTransitioning} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} progress={TransitionProgress:0.###} lastChange={FormatBeat(lastChangeBeat)}");
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
        return lastChangeBeat == int.MinValue || beat - lastChangeBeat >= MinimumChangeCadenceBeats;
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

    private string FormatPhase()
    {
        return phaseReading.PhasePosition > 0
            ? $"{phaseReading.PhasePosition}/16:{phaseReading.Confidence}"
            : $"none:{phaseReading.Confidence}";
    }

    private string FormatPhaseInput()
    {
        return $"beat={phaseInput.Beat},total={FormatBeat(phaseInput.TotalBeats)},barBeat={FormatBeat(phaseInput.BeatInBar)},phaseActive={phaseInput.PhaseActive},phaseCount={FormatBeat(phaseInput.PhaseCountBeats)},phaseLength={FormatBeat(phaseInput.PhaseLengthBeats)}";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "none";
    }

    private static string FormatNullableBeat(int? beat)
    {
        return beat is { } value ? value.ToString() : "none";
    }

    private static string FormatBeatList(int[] beats)
    {
        return beats == null || beats.Length == 0 ? "none" : string.Join(",", beats);
    }

    private void RunDefaultModeTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace($"DEFAULT_HOLD held={FormatEffect(heldEffectIndex)} transitioning={switcher.IsTransitioning} current={FormatEffect(switcher.CurrentEffectIndex)}");
            if (switcher.IsTransitioning || switcher.CurrentEffectIndex != heldEffectIndex)
            {
                ShowNow(heldEffectIndex, controller.effectTime);
            }
            else
            {
                defaultTimer.Reset();
            }

            return;
        }

        if (switcher.IsTransitioning)
        {
            Trace($"DEFAULT_TRANSITION_COMPLETE_REQUEST progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
            switcher.CompleteTransition();
            defaultTimer.Set(controller.effectTime);
            defaultTimer.Reset();
            TransitionProgress = 0f;
            nextTransitionIndex = PullCard(transitionDeck);
            controller.currentTransition = nextTransitionIndex;
            Trace($"DEFAULT_TRANSITION_COMPLETE_DONE current={FormatEffect(switcher.CurrentEffectIndex)} nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = PullEffect(Repertoire.None);
        Trace($"DEFAULT_TRANSITION_START transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} durationSeconds={controller.transitionTime:0.###}");
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;
        defaultTimer.Set(controller.transitionTime);
        defaultTimer.Reset();
        TransitionProgress = 0f;
    }

    private int PullEffect(Repertoire preferredRepertoire)
    {
        if (preferredRepertoire == Repertoire.None)
        {
            return PullCard(effectDeck);
        }

        for (var i = 0; i < effectDeck.Length; i++)
        {
            var effectIndex = effectDeck[i];
            if ((controller.effects[effectIndex].Repertoire & preferredRepertoire) == 0)
            {
                continue;
            }

            RemoveDeckCardAt(effectDeck, i);
            return effectIndex;
        }

        return PullCard(effectDeck);
    }

    private void MarkChangedOnCurrentBeat()
    {
        if (controller.beatManager.IsLiveSource && controller.beatManager.Beat is { } beat)
        {
            MarkChangedOnBeat(beat);
        }
    }

    private void MarkChangedOnBeat(int beat)
    {
        lastChangeBeat = beat;
    }

    private static int PullCard(int[] deck)
    {
        var length = deck.Length;
        var index = UnityEngine.Random.Range(0, length / 2);
        var result = deck[index];
        RemoveDeckCardAt(deck, index);
        return result;
    }

    private static void RemoveDeckCardAt(int[] deck, int index)
    {
        var result = deck[index];
        for (var i = index; i < deck.Length - 1; i++)
        {
            deck[i] = deck[i + 1];
        }

        deck[deck.Length - 1] = result;
    }
}
