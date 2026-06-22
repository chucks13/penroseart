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
    WaitingForPhase,
    WaitingForRunway,
    WaitingForCadence,
    CueingTransition,
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
        false);

    public readonly DirectorMode Mode;
    public readonly DirectorDecision Decision;
    public readonly bool IsSyncedMode;
    public readonly bool HasPhaseAnchor;
    public readonly PhaseConfidence PhaseAnchorConfidence;
    public readonly PhaseReading Phase;
    /// <summary>Source of the current On-Air Timing target.</summary>
    public readonly TimingFrameSource TimingSource;
    public readonly int PhaseAnchorLandingBeat;
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

    public DirectorStatus(
        DirectorMode mode,
        DirectorDecision decision,
        bool isSyncedMode,
        bool hasPhaseAnchor,
        PhaseConfidence phaseAnchorConfidence,
        PhaseReading phase,
        TimingFrameSource timingSource,
        int phaseAnchorLandingBeat,
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
        bool holdSelectedTransition)
    {
        Mode = mode;
        Decision = decision;
        IsSyncedMode = isSyncedMode;
        HasPhaseAnchor = hasPhaseAnchor;
        PhaseAnchorConfidence = phaseAnchorConfidence;
        Phase = phase;
        TimingSource = timingSource;
        PhaseAnchorLandingBeat = phaseAnchorLandingBeat;
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
    private readonly OnAirTiming onAirTiming = new OnAirTiming();

    private int currentEffectIndexForSelection = -1;
    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;
    private int lastSyncedBeat = -1;
    private int lastChangeBeat = int.MinValue;
    private int lastCueBeat = -1;
    private int transitionStartBeat = -1;
    private int transitionLandingBeat = -1;
    private TimingFrame timingFrame = TimingFrame.Unavailable;
    private DirectorMode lastLoggedMode = DirectorMode.NotReady;
    private int lastLoggedSyncedBeat = -1;

    /// <summary>Whether the Director currently has a phase grid to aim at.</summary>
    public bool HasPhaseAnchor => timingFrame.HasPhaseAnchor;

    /// <summary>Confidence for the current phase anchor.</summary>
    public PhaseConfidence PhaseAnchorConfidence => timingFrame.PhaseAnchorConfidence;

    /// <summary>Absolute beat where the current phase anchor next lands, or -1 when unlocked.</summary>
    public int PhaseAnchorLandingBeat => timingFrame.PhaseAnchorLandingBeat;

    /// <summary>Whether live OSC data is currently driving sequencing.</summary>
    public bool IsSyncedMode => controller != null && controller.beatManager != null && controller.beatManager.IsLiveSource;

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
        currentEffectIndexForSelection = switcher.CurrentEffectIndex;
        SetNextTransition(initialTransitionIndex);
        StageNextEffect(Repertoire.None, currentEffectIndexForSelection);
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

            return;
        }

        TickStandaloneMode(deltaTime);
    }

    /// <summary>Immediate developer/manual effect selection. Resets Standalone Mode cadence.</summary>
    public void ShowNow(int effectIndex, float durationSeconds)
    {
        Trace($"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} live={controller.beatManager.IsLiveSource} beat={FormatNullableBeat(controller.beatManager.Beat)}");
        switcher.ShowNow(effectIndex);
        currentEffectIndexForSelection = effectIndex;
        onAirTiming.Reset();
        timingFrame = TimingFrame.Unavailable;
        MarkChangedOnCurrentBeat();
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
        var beatsUntilLanding = timingFrame.HasPhaseAnchor && currentBeat >= 0 ? timingFrame.PhaseAnchorLandingBeat - currentBeat : -1;
        var runwayBeats = NextTransitionRepertoire.RunwayBeats;
        // Match the cue path: cadence belongs to the selected landing boundary, not to the
        // current beat. A tailed transition can complete while the next boundary is already valid.
        var beatsUntilCadenceReady = 0;
        if (currentBeat >= 0 && lastChangeBeat != int.MinValue)
        {
            var cadenceReadyBeat = lastChangeBeat + MinimumChangeCadenceBeats;
            var selectedBoundarySatisfiesCadence = timingFrame.HasPhaseAnchor && timingFrame.PhaseAnchorLandingBeat >= cadenceReadyBeat;
            if (!selectedBoundarySatisfiesCadence)
            {
                beatsUntilCadenceReady = Math.Max(0, cadenceReadyBeat - currentBeat);
            }
        }

        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var decision = ResolveDecision(isHeld, isSynced, beatsUntilLanding, beatsUntilCadenceReady, runwayBeats);

        return new DirectorStatus(
            mode,
            decision,
            isSynced,
            timingFrame.HasPhaseAnchor,
            timingFrame.PhaseAnchorConfidence,
            timingFrame.Phase,
            timingFrame.Source,
            timingFrame.PhaseAnchorLandingBeat,
            lastChangeBeat,
            transitionLandingBeat,
            currentBeat,
            beatsUntilLanding,
            beatsUntilCadenceReady,
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
            return DirectorDecision.StandaloneTimer;
        }

        if (!timingFrame.HasPhaseAnchor)
        {
            return DirectorDecision.WaitingForPhase;
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

    private void TickStandaloneMode(float deltaTime)
    {
        timingFrame = TimingFrame.Unavailable;
        onAirTiming.Reset();
        standaloneTimer.Update(deltaTime);
    }

    private void TickSyncedMode(int beat)
    {
        var previousSyncedBeat = lastSyncedBeat;
        lastSyncedBeat = beat;

        if (controller.TryGetHeldEffectIndex(out _))
        {
            return;
        }

        RefreshTimingFrame();
        if (timingFrame.BeatRewoundToNewPass)
        {
            lastCueBeat = -1;
            lastChangeBeat = int.MinValue;
        }

        LogBeatRewindIfNeeded(previousSyncedBeat, beat, timingFrame.BeatRewoundToNewPass);
        LogSyncedBeatIfNeeded(beat);
        TryStartSyncedCue(timingFrame);
    }

    private void RefreshTimingFrame()
    {
        var previousSelectedPhaseBoundary = lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat;
        var previousLandingBeat = timingFrame.PhaseAnchorLandingBeat;
        var previousConfidence = timingFrame.PhaseAnchorConfidence;
        timingFrame = onAirTiming.ReadFrame(
            OnAirTimingInput.From(controller.beatManager),
            previousSelectedPhaseBoundary,
            MinimumChangeCadenceBeats);

        if (!timingFrame.HasPhaseAnchor)
        {
            return;
        }

        if (timingFrame.IsCoasting)
        {
            if (timingFrame.PhaseAnchorLandingBeat != previousLandingBeat)
            {
                Trace($"ANCHOR_COAST beat={timingFrame.CurrentBeat} input={FormatTimingInput()} landing={timingFrame.PhaseAnchorLandingBeat} previousLanding={FormatBeat(previousLandingBeat)}");
            }

            return;
        }

        if (timingFrame.PhaseAnchorLandingBeat != previousLandingBeat || timingFrame.PhaseAnchorConfidence != previousConfidence)
        {
            Trace($"ANCHOR_SET beat={timingFrame.CurrentBeat} input={FormatTimingInput()} phase={FormatPhase()} target={FormatTimingSource(timingFrame.Source)} landing={timingFrame.PhaseAnchorLandingBeat} previousLanding={FormatBeat(previousLandingBeat)}");
        }
    }

    private void TryStartSyncedCue(TimingFrame frame)
    {
        if (!frame.HasPhaseAnchor)
        {
            return;
        }

        var beat = frame.CurrentBeat;
        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = nextEffectIndex;
        ValidateTransitionIndex(transitionIndex);
        ValidateEffectIndex(targetEffectIndex);
        var repertoire = controller.transitions[transitionIndex].Repertoire;
        var lastCue = lastCueBeat >= 0 ? (int?)lastCueBeat : null;
        var previousSelectedPhaseBoundary = lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat;
        var cueDecision = SyncedCueDecision.Evaluate(
            beat,
            frame.SelectedPhaseBoundary,
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
        var secondsPerBeat = CurrentSecondsPerBeat();
        var beatFraction = controller.beatManager.BeatFraction ?? 0f;
        var elapsedBeats = Mathf.Max(0f, lastSyncedBeat - beatPlan.StartBeat + beatFraction);
        var startTime = Time.time - (elapsedBeats * secondsPerBeat);
        switcher.StartTransition(
            targetEffectIndex,
            transitionIndex,
            TransitionStartTiming.FromBeatClock(startTime, secondsPerBeat));
        controller.currentTransition = transitionIndex;

        transitionStartBeat = beatPlan.StartBeat;
        transitionLandingBeat = beatPlan.ImpactBeat;
        MarkChangedOnBeat(transitionLandingBeat);
        lastCueBeat = lastSyncedBeat;
        currentEffectIndexForSelection = targetEffectIndex;
        StageNextChoices(Repertoire.None, currentEffectIndexForSelection);
        Trace($"SYNC_TRANSITION_START beat={lastSyncedBeat} beatFraction={beatFraction:0.###} elapsedBeats={elapsedBeats:0.###} start={transitionStartBeat} impact={transitionLandingBeat} transition={FormatTransition(transitionIndex)} target={FormatEffect(targetEffectIndex)} runway={repertoire.RunwayBeats} preferred={preferredRepertoire}");
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

        Trace($"BEAT_REWIND previousBeat={previousBeat} currentBeat={beat} input={FormatTimingInput()} phase={FormatPhase()} anchor={FormatBeat(timingFrame.PhaseAnchorLandingBeat)} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} lastChange={FormatBeat(lastChangeBeat)}");
    }

    private void LogSyncedBeatIfNeeded(int beat)
    {
        if (beat == lastLoggedSyncedBeat)
        {
            return;
        }

        var beatsUntilLanding = timingFrame.HasPhaseAnchor ? timingFrame.PhaseAnchorLandingBeat - beat : -1;
        var canChangeAtLanding = timingFrame.HasPhaseAnchor && CanChangeAtBeat(timingFrame.PhaseAnchorLandingBeat);
        Trace($"SYNC_BEAT beat={beat} input={FormatTimingInput()} phase={FormatPhase()} source={FormatTimingSource(timingFrame.Source)} anchor={FormatBeat(timingFrame.PhaseAnchorLandingBeat)} until={FormatBeat(beatsUntilLanding)} canChangeAtLanding={canChangeAtLanding} transitionStart={FormatBeat(transitionStartBeat)} transitionLanding={FormatBeat(transitionLandingBeat)} lastChange={FormatBeat(lastChangeBeat)}");
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
        return timingFrame.Phase.PhasePosition > 0
            ? $"{timingFrame.Phase.PhasePosition}/16:{timingFrame.Phase.Confidence}"
            : $"none:{timingFrame.Phase.Confidence}";
    }

    private string FormatTimingInput()
    {
        var input = timingFrame.Input;
        return $"beat={input.Beat},total={FormatBeat(input.TotalBeats)},barBeat={FormatBeat(input.BeatInBar)},phaseActive={input.TrackPhaseActive},phaseCount={FormatBeat(input.BeatsUntilPhraseBoundary)},phaseLength={FormatBeat(input.PhraseLengthBeats)}";
    }

    private static string FormatTimingSource(TimingFrameSource source)
    {
        switch (source)
        {
            case TimingFrameSource.PhaseClockGrid:
                return "phase-clock-grid";
            case TimingFrameSource.SelectedPhaseBoundary:
                return "selected-phase-boundary";
            case TimingFrameSource.TrackPhaseBoundary:
                return "track-phase-boundary";
            case TimingFrameSource.Coast:
                return "coast";
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
        StageNextChoices(Repertoire.None, currentEffectIndexForSelection);
        standaloneTimer.Set(transitionDurationSeconds + controller.effectTime);
        standaloneTimer.Reset();
    }

    private void StageNextChoices(Repertoire preferredRepertoire)
    {
        StageNextChoices(preferredRepertoire, currentEffectIndexForSelection);
    }

    private void StageNextChoices(Repertoire preferredRepertoire, int currentEffectIndex)
    {
        StageNextEffect(preferredRepertoire, currentEffectIndex);
        StageNextTransition();
    }

    private void StageNextEffect(Repertoire preferredRepertoire, int currentEffectIndex)
    {
        if (holdSelectedEffect)
        {
            Trace($"NEXT_EFFECT_HELD nextEffect={FormatEffect(nextEffectIndex)}");
            return;
        }

        nextEffectIndex = PullEffect(preferredRepertoire, currentEffectIndex);
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

    private int PullEffect(Repertoire preferredRepertoire, int currentEffectIndex)
    {
        return EffectDeckSelection.PullNext(
            effectDeck,
            currentEffectIndex,
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
