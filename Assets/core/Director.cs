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
    StandaloneTransition,
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
        PhaseConfidence.Unlocked,
        PhaseReading.Unavailable,
        -1,
        -1,
        -1,
        -1,
        -1,
        -1,
        0f,
        -1,
        string.Empty,
        -1,
        string.Empty,
        false,
        false);

    public readonly DirectorMode Mode;
    public readonly DirectorDecision Decision;
    public readonly bool IsSyncedMode;
    public readonly bool HasPhaseAnchor;
    public readonly PhaseConfidence PhaseAnchorConfidence;
    public readonly PhaseReading Phase;
    public readonly int PhaseAnchorLandingBeat;
    public readonly int LastChangeBeat;
    public readonly int TransitionLandingBeat;
    public readonly int BeatsUntilLanding;
    public readonly int BeatsUntilCadenceReady;
    public readonly float TransitionProgress;
    public readonly int NextEffectIndex;
    public readonly string NextEffectName;
    public readonly int NextTransitionIndex;
    public readonly string NextTransitionName;
    public readonly bool HoldSelectedEffect;
    public readonly bool HoldSelectedTransition;

    public DirectorStatus(
        DirectorMode mode,
        DirectorDecision decision,
        bool isSyncedMode,
        bool hasPhaseAnchor,
        PhaseConfidence phaseAnchorConfidence,
        PhaseReading phase,
        int phaseAnchorLandingBeat,
        int lastChangeBeat,
        int transitionLandingBeat,
        int currentBeat,
        int beatsUntilLanding,
        int beatsUntilCadenceReady,
        float transitionProgress,
        int nextEffectIndex,
        string nextEffectName,
        int nextTransitionIndex,
        string nextTransitionName,
        bool holdSelectedEffect,
        bool holdSelectedTransition)
    {
        Mode = mode;
        Decision = decision;
        IsSyncedMode = isSyncedMode;
        HasPhaseAnchor = hasPhaseAnchor;
        PhaseAnchorConfidence = phaseAnchorConfidence;
        Phase = phase;
        PhaseAnchorLandingBeat = phaseAnchorLandingBeat;
        LastChangeBeat = lastChangeBeat;
        TransitionLandingBeat = transitionLandingBeat;
        CurrentBeat = currentBeat;
        BeatsUntilLanding = beatsUntilLanding;
        BeatsUntilCadenceReady = beatsUntilCadenceReady;
        TransitionProgress = transitionProgress;
        NextEffectIndex = nextEffectIndex;
        NextEffectName = nextEffectName ?? string.Empty;
        NextTransitionIndex = nextTransitionIndex;
        NextTransitionName = nextTransitionName ?? string.Empty;
        HoldSelectedEffect = holdSelectedEffect;
        HoldSelectedTransition = holdSelectedTransition;
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

    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;
    private int lastSyncedBeat = -1;
    private int lastChangeBeat = int.MinValue;
    private int lastCueBeat = -1;
    private int transitionStartBeat = -1;
    private int transitionLandingBeat = -1;
    private SyncedTransitionPlan transitionPlan;
    private bool hasPhaseAnchor;
    private PhaseConfidence phaseAnchorConfidence = PhaseConfidence.Unlocked;
    private int phaseAnchorLandingBeat = -1;
    private PhaseReading phaseReading = PhaseReading.Unavailable;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;
    private int lastLoggedSyncedBeat = -1;
    private int lastLoggedTransitionBeat = -1;
    private PhaseInput phaseInput = new PhaseInput(-1, -1, -1, -1, -1, -1);
    private int selectedBoundaryPhraseStartBeat = -1;
    private int selectedBoundaryPhraseEndBeat = -1;
    private int selectedBoundaryPhraseLengthBeats = -1;
    private int[] selectedPhaseBoundaries = Array.Empty<int>();
    private int selectedPhaseBoundaryIndex;

    /// <summary>
    /// Progress for the current mechanical transition. Standalone Mode uses the legacy timer;
    /// Synced Mode derives it from the live beat count so the Switcher never interprets timing.
    /// </summary>
    public float TransitionProgress { get; private set; }

    /// <summary>Whether the Director currently has a phase grid to aim at.</summary>
    public bool HasPhaseAnchor => hasPhaseAnchor;

    /// <summary>Confidence for the current phase anchor.</summary>
    public PhaseConfidence PhaseAnchorConfidence => phaseAnchorConfidence;

    /// <summary>Absolute beat where the current phase anchor next lands, or -1 when unlocked.</summary>
    public int PhaseAnchorLandingBeat => phaseAnchorLandingBeat;

    /// <summary>Whether live OSC data is currently driving sequencing.</summary>
    public bool IsSyncedMode => controller.beatManager.IsLiveSource;

    /// <summary>Current read-only sequencing snapshot for runtime HUDs and inspector diagnostics.</summary>
    public DirectorStatus Status => BuildStatus();

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
        Trace($"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>Stages the Transition that the next A-to-B move should use.</summary>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
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
        SetNextTransition(initialTransitionIndex);
        StageNextEffect(Repertoire.None);
    }

    /// <summary>Advances the Director's current cadence clock or live musical scheduling.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        if (IsSyncedMode)
        {
            if (controller.beatManager.Beat is { } beat)
            {
                TickSyncedMode(beat);
            }
            else
            {
                TransitionProgress = 0f;
            }

            return;
        }

        TickStandaloneMode(deltaTime);
    }

    /// <summary>Immediate developer/manual effect selection. Resets Standalone Mode cadence.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace($"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        switcher.ShowNow(effectIndex);
        transitionPlan = default;
        ResetSelectedPhaseBoundaryPlan();
        MarkChangedOnCurrentBeat();
        TransitionProgress = 0f;
        standaloneTimer.Set(durationSeconds);
        standaloneTimer.Reset();
        StageNextChoices(Repertoire.None);
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

    /// <summary>Standalone Mode timer callback.</summary>
    public void OnTimerFinished()
    {
        if (IsSyncedMode)
        {
            Trace($"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Beat)} transitioning={switcher.IsTransitioning}");
            return;
        }

        Trace($"TIMER_FINISHED_STANDALONE transitioning={switcher.IsTransitioning} progress={TransitionProgress:0.###}");
        RunStandaloneTimerDecision();
    }

    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var currentBeat = isSynced && controller.beatManager.Beat is { } beat ? beat : -1;
        var beatsUntilLanding = hasPhaseAnchor && currentBeat >= 0 ? phaseAnchorLandingBeat - currentBeat : -1;
        var runwayBeats = NextTransitionRepertoire.RunwayBeats;
        var beatsUntilCadenceReady = currentBeat >= 0 && lastChangeBeat != int.MinValue
            ? Math.Max(0, MinimumChangeCadenceBeats - (currentBeat - lastChangeBeat))
            : 0;

        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var decision = ResolveDecision(isHeld, isSynced, beatsUntilLanding, beatsUntilCadenceReady, runwayBeats);

        return new DirectorStatus(
            mode,
            decision,
            isSynced,
            hasPhaseAnchor,
            phaseAnchorConfidence,
            phaseReading,
            phaseAnchorLandingBeat,
            lastChangeBeat,
            transitionLandingBeat,
            currentBeat,
            beatsUntilLanding,
            beatsUntilCadenceReady,
            TransitionProgress,
            nextEffectIndex,
            EffectName(nextEffectIndex),
            nextTransitionIndex,
            TransitionName(nextTransitionIndex),
            holdSelectedEffect,
            holdSelectedTransition);
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
            return switcher.IsTransitioning ? DirectorDecision.StandaloneTransition : DirectorDecision.StandaloneTimer;
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

    private void TickStandaloneMode(float deltaTime)
    {
        phaseReading = PhaseReading.Unavailable;
        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
        transitionPlan = default;
        ResetSelectedPhaseBoundaryPlan();
        TransitionProgress = standaloneTimer.Value;
        standaloneTimer.Update(deltaTime);
        TransitionProgress = standaloneTimer.Value;
    }

    private void TickSyncedMode(int beat)
    {
        var previousSyncedBeat = lastSyncedBeat;
        var beatRewoundToNewPass = BeatRewoundToNewPass(previousSyncedBeat, beat);
        lastSyncedBeat = beat;

        if (beatRewoundToNewPass)
        {
            lastCueBeat = -1;
            lastChangeBeat = int.MinValue;
        }

        if (controller.TryGetHeldEffectIndex(out _))
        {
            TransitionProgress = 0f;
            return;
        }

        RefreshPhaseAnchor(beat, beatRewoundToNewPass);
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

    private void RefreshPhaseAnchor(int beat, bool beatRewoundToNewPass)
    {
        var previousLandingBeat = phaseAnchorLandingBeat;
        var previousConfidence = phaseAnchorConfidence;
        phaseInput = BuildPhaseInput();
        phaseReading = PhaseClock.Resolve(phaseInput);
        if (phaseReading.Confidence != PhaseConfidence.Unlocked)
        {
            phaseAnchorLandingBeat = ResolveSelectedPhaseBoundary(beat, beatRewoundToNewPass, out var targetSource);
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

    private int ResolveSelectedPhaseBoundary(int beat, bool beatRewoundToNewPass, out string targetSource)
    {
        if (TryResolveTrackPhaseBoundary(beat, beatRewoundToNewPass, out var selectedPhaseBoundary, out targetSource))
        {
            return selectedPhaseBoundary;
        }

        ResetSelectedPhaseBoundaryPlan();
        targetSource = "phase-clock-grid";
        return GetLandingBeatFromPhasePosition(beat, phaseReading.PhasePosition);
    }

    private bool TryResolveTrackPhaseBoundary(
        int beat,
        bool beatRewoundToNewPass,
        out int selectedPhaseBoundary,
        out string targetSource)
    {
        selectedPhaseBoundary = -1;
        targetSource = "none";
        if (!PhraseWindow.TryFromTrackPhase(
            beat,
            phaseInput.PhaseCountBeats,
            phaseInput.PhaseLengthBeats,
            out var phraseWindow))
        {
            return false;
        }

        selectedPhaseBoundary = ResolveSelectedPhaseBoundaryFromPlan(beat, phraseWindow, beatRewoundToNewPass, out targetSource);
        return true;
    }

    private int ResolveSelectedPhaseBoundaryFromPlan(
        int beat,
        PhraseWindow phraseWindow,
        bool beatRewoundToNewPass,
        out string targetSource)
    {
        if (NeedsSelectedPhaseBoundaryPlan(beat, phraseWindow))
        {
            BuildSelectedPhaseBoundaryPlan(beat, phraseWindow);
        }
        else if (beatRewoundToNewPass)
        {
            selectedPhaseBoundaryIndex = 0;
        }

        while (selectedPhaseBoundaryIndex < selectedPhaseBoundaries.Length - 1
            && selectedPhaseBoundaries[selectedPhaseBoundaryIndex] <= beat)
        {
            selectedPhaseBoundaryIndex++;
        }

        var target = selectedPhaseBoundaries.Length > 0
            ? selectedPhaseBoundaries[Mathf.Clamp(selectedPhaseBoundaryIndex, 0, selectedPhaseBoundaries.Length - 1)]
            : phraseWindow.EndBeat;
        targetSource = target == selectedBoundaryPhraseEndBeat ? "track-phase-boundary" : "selected-phase-boundary";
        return target;
    }

    private bool NeedsSelectedPhaseBoundaryPlan(int beat, PhraseWindow phraseWindow)
    {
        if (selectedPhaseBoundaries.Length == 0 || selectedPhaseBoundaries[selectedPhaseBoundaries.Length - 1] <= beat)
        {
            return true;
        }

        return selectedBoundaryPhraseStartBeat != phraseWindow.StartBeat
            || selectedBoundaryPhraseEndBeat != phraseWindow.EndBeat
            || selectedBoundaryPhraseLengthBeats != phraseWindow.LengthBeats;
    }

    private void BuildSelectedPhaseBoundaryPlan(int beat, PhraseWindow phraseWindow)
    {
        var plan = SelectedPhaseBoundaryPlan.Build(
            phraseWindow,
            beat,
            CanChangeAtBeat,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));

        selectedBoundaryPhraseStartBeat = plan.PhraseStartBeat;
        selectedBoundaryPhraseEndBeat = plan.PhraseEndBeat;
        selectedBoundaryPhraseLengthBeats = phraseWindow.LengthBeats;
        selectedPhaseBoundaries = plan.SelectedPhaseBoundaries;
        selectedPhaseBoundaryIndex = 0;

        var interiorTransitionCount = Math.Max(0, selectedPhaseBoundaries.Length - 1);
        Trace($"SELECTED_PHASE_BOUNDARY_PLAN beat={beat} phraseStart={selectedBoundaryPhraseStartBeat} phraseEnd={selectedBoundaryPhraseEndBeat} phraseLength={selectedBoundaryPhraseLengthBeats} targets={FormatBeatList(selectedPhaseBoundaries)} interiorSelected={interiorTransitionCount} lastChange={FormatBeat(lastChangeBeat)}");
    }

    private void ResetSelectedPhaseBoundaryPlan()
    {
        selectedBoundaryPhraseStartBeat = -1;
        selectedBoundaryPhraseEndBeat = -1;
        selectedBoundaryPhraseLengthBeats = -1;
        selectedPhaseBoundaries = Array.Empty<int>();
        selectedPhaseBoundaryIndex = 0;
    }

    private static int GetLandingBeatFromPhasePosition(int beat, int phasePosition)
    {
        var beatsUntilLanding = PhaseClock.PhraseBeats - phasePosition + 1;
        return beat + beatsUntilLanding;
    }

    private void TryStartSyncedCue(int beat)
    {
        if (!hasPhaseAnchor)
        {
            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(targetEffectIndex);
        var repertoire = controller.transitions[transitionIndex].Repertoire;
        var lastCue = lastCueBeat >= 0 ? (int?)lastCueBeat : null;
        var previousSelectedPhaseBoundary = lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat;
        var cueDecision = SyncedCueDecision.Evaluate(
            beat,
            phaseAnchorLandingBeat,
            repertoire,
            lastCue,
            previousSelectedPhaseBoundary,
            MinimumChangeCadenceBeats);

        if (cueDecision.Kind == SyncedCueDecisionKind.Wait)
        {
            return;
        }

        if (cueDecision.BlockedByCadence)
        {
            Trace($"SYNC_CUE_BLOCKED_CADENCE beat={beat} selectedBoundary={cueDecision.BeatPlan.ImpactBeat} runway={repertoire.RunwayBeats} lastChange={FormatBeat(lastChangeBeat)}");
            lastCueBeat = beat;
            return;
        }

        var preferredRepertoire = PreferredRepertoireForLanding(cueDecision.BeatsUntilImpact);
        Trace($"SYNC_CUE beat={beat} start={cueDecision.BeatPlan.StartBeat} selectedBoundary={cueDecision.BeatPlan.ImpactBeat} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} lateBy={Math.Max(0, beat - cueDecision.BeatPlan.StartBeat)} preferred={preferredRepertoire} transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)}");
        StartSyncedTransition(transitionIndex, targetEffectIndex, cueDecision.BeatPlan, repertoire, preferredRepertoire);
    }

    private Repertoire PreferredRepertoireForLanding(int beatsUntilLanding)
    {
        return controller.beatManager.Drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart } && dropBeatsUntilStart == beatsUntilLanding
            ? Repertoire.HandlesDrop
            : Repertoire.None;
    }

    private void StartSyncedTransition(
        int transitionIndex,
        int targetEffectIndex,
        TransitionBeatPlan beatPlan,
        TransitionRepertoire repertoire,
        Repertoire preferredRepertoire)
    {
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;

        var secondsPerBeat = CurrentSecondsPerBeat();
        var beatFraction = controller.beatManager.BeatFraction ?? 0f;
        var elapsedBeats = Mathf.Max(0f, lastSyncedBeat - beatPlan.StartBeat + beatFraction);
        var startTime = Time.time - (elapsedBeats * secondsPerBeat);
        transitionPlan = new SyncedTransitionPlan(transitionIndex, targetEffectIndex, beatPlan, repertoire, startTime, secondsPerBeat);

        transitionStartBeat = transitionPlan.StartBeat;
        transitionLandingBeat = transitionPlan.ImpactBeat;
        MarkChangedOnBeat(transitionLandingBeat);
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

        var update = transitionPlan.EvaluateUpdate(
            beat,
            beatRewoundToNewPass,
            transitionLandingBeat,
            Time.time);
        TransitionProgress = update.Progress;
        if (update.RecordImpactOnRewind)
        {
            transitionLandingBeat = update.ImpactBeat;
            MarkChangedOnBeat(update.ImpactBeat);
            Trace($"SYNC_TRANSITION_IMPACT_ON_REWIND beat={beat} previousBeat={FormatBeat(previousBeat)} plannedImpact={transitionPlan.ImpactBeat} complete={transitionPlan.CompleteBeat} progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
        }

        if (update.ShouldComplete)
        {
            Trace($"SYNC_TRANSITION_COMPLETE_REQUEST beat={beat} previousBeat={FormatBeat(previousBeat)} rewound={beatRewoundToNewPass} impact={update.ImpactBeat} plannedImpact={transitionPlan.ImpactBeat} complete={transitionPlan.CompleteBeat} progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
            switcher.CompleteTransition();
            StageNextChoices(Repertoire.None);
            transitionPlan = default;
            TransitionProgress = 0f;
            Trace($"SYNC_TRANSITION_COMPLETE_DONE beat={beat} rewound={beatRewoundToNewPass} current={FormatEffect(switcher.CurrentEffectIndex)} lastChange={FormatBeat(lastChangeBeat)} nextEffect={FormatEffect(nextEffectIndex)} nextTransition={FormatTransition(nextTransitionIndex)}");
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
                : DirectorMode.Standalone;

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
        var previousSelectedPhaseBoundary = lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat;
        return ChangeCadence.CanChangeAt(beat, previousSelectedPhaseBoundary, MinimumChangeCadenceBeats);
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

    private void RunStandaloneTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace($"STANDALONE_HOLD held={FormatEffect(heldEffectIndex)} transitioning={switcher.IsTransitioning} current={FormatEffect(switcher.CurrentEffectIndex)}");
            if (switcher.IsTransitioning || switcher.CurrentEffectIndex != heldEffectIndex)
            {
                ShowNow(heldEffectIndex, controller.effectTime);
            }
            else
            {
                standaloneTimer.Reset();
            }

            return;
        }

        if (switcher.IsTransitioning)
        {
            Trace($"STANDALONE_TRANSITION_COMPLETE_REQUEST progress={TransitionProgress:0.###} target={FormatEffect(switcher.TransitionTargetEffectIndex)} transition={FormatTransition(switcher.CurrentTransitionIndex)}");
            switcher.CompleteTransition();
            standaloneTimer.Set(controller.effectTime);
            standaloneTimer.Reset();
            TransitionProgress = 0f;
            StageNextChoices(Repertoire.None);
            Trace($"STANDALONE_TRANSITION_COMPLETE_DONE current={FormatEffect(switcher.CurrentEffectIndex)} nextEffect={FormatEffect(nextEffectIndex)} nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(targetEffectIndex);
        var transitionRepertoire = controller.transitions[transitionIndex].Repertoire;
        var transitionDurationSeconds = transitionRepertoire.DefaultDurationSeconds;
        Trace($"STANDALONE_TRANSITION_START transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} durationSeconds={transitionDurationSeconds:0.###}");
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;
        standaloneTimer.Set(transitionDurationSeconds);
        standaloneTimer.Reset();
        TransitionProgress = 0f;
    }

    private void StageNextChoices(Repertoire preferredRepertoire)
    {
        StageNextEffect(preferredRepertoire);
        StageNextTransition();
    }

    private void StageNextEffect(Repertoire preferredRepertoire)
    {
        if (holdSelectedEffect)
        {
            Trace($"NEXT_EFFECT_HELD nextEffect={FormatEffect(nextEffectIndex)}");
            return;
        }

        nextEffectIndex = PullEffect(preferredRepertoire);
        Trace($"NEXT_EFFECT_STAGED nextEffect={FormatEffect(nextEffectIndex)} preferred={preferredRepertoire}");
    }

    private void StageNextTransition()
    {
        if (holdSelectedTransition)
        {
            controller.currentTransition = nextTransitionIndex;
            Trace($"NEXT_TRANSITION_HELD nextTransition={FormatTransition(nextTransitionIndex)}");
            return;
        }

        nextTransitionIndex = PullCard(transitionDeck);
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

    private int PullEffect(Repertoire preferredRepertoire)
    {
        return EffectDeckSelection.PullNext(
            effectDeck,
            switcher.CurrentEffectIndex,
            preferredRepertoire,
            effectIndex => controller.effects[effectIndex].Repertoire,
            (minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive));
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
