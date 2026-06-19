using System;
using UnityEngine;

/// <summary>
/// Decides what plays and when it changes.
/// The Director reads available musical timing, reads Performer repertoire, and directs the Switcher.
/// </summary>
[Serializable]
public sealed class Director
{
    private const int TransitionLengthBeats = 4;
    private const int MinimumChangeCadenceBeats = 16;

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
    private bool hasPhaseAnchor;
    private PhaseConfidence phaseAnchorConfidence = PhaseConfidence.Unlocked;
    private int phaseAnchorLandingBeat = -1;

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
        switcher.ShowNow(effectIndex);
        MarkChangedOnCurrentBeat();
        TransitionProgress = 0f;
        defaultTimer.Set(durationSeconds);
        defaultTimer.Reset();
        controller.effectText.text = switcher.CurrentName;
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
            return;
        }

        RunDefaultModeTimerDecision();
    }

    private bool IsSyncedMode => controller.beatManager.IsLiveSource && controller.beatManager.Beat is { };

    private void TickDefaultMode(float deltaTime)
    {
        TransitionProgress = defaultTimer.Value;
        defaultTimer.Update(deltaTime);
        TransitionProgress = defaultTimer.Value;
    }

    private void TickSyncedMode(int beat)
    {
        lastSyncedBeat = beat;

        if (controller.TryGetHeldEffectIndex(out _))
        {
            TransitionProgress = 0f;
            return;
        }

        RefreshPhaseAnchor(beat);

        if (switcher.IsTransitioning)
        {
            UpdateSyncedTransition(beat);
            return;
        }

        TransitionProgress = 0f;
        TryStartSyncedCue(beat);
    }

    private void RefreshPhaseAnchor(int beat)
    {
        var reading = PhaseClock.Resolve(BuildPhaseInput());
        if (reading.Confidence != PhaseConfidence.Unlocked)
        {
            phaseAnchorLandingBeat = GetLandingBeatFromPhasePosition(beat, reading.PhasePosition);
            hasPhaseAnchor = true;
            phaseAnchorConfidence = reading.Confidence;
            return;
        }

        if (hasPhaseAnchor)
        {
            CoastPhaseAnchor(beat);
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
        while (beat > phaseAnchorLandingBeat - TransitionLengthBeats)
        {
            phaseAnchorLandingBeat += MinimumChangeCadenceBeats;
        }
    }

    private static int GetLandingBeatFromPhasePosition(int beat, int phasePosition)
    {
        var beatsUntilLanding = PhaseClock.PhraseBeats - phasePosition + 1;
        while (beatsUntilLanding < TransitionLengthBeats)
        {
            beatsUntilLanding += MinimumChangeCadenceBeats;
        }

        return beat + beatsUntilLanding;
    }

    private void TryStartSyncedCue(int beat)
    {
        if (!hasPhaseAnchor || lastCueBeat == beat)
        {
            return;
        }

        var beatsUntilLanding = phaseAnchorLandingBeat - beat;
        if (beatsUntilLanding != TransitionLengthBeats || !CanChangeAtBeat(phaseAnchorLandingBeat))
        {
            return;
        }

        StartSyncedTransition(beat, phaseAnchorLandingBeat, PreferredRepertoireForLanding(beatsUntilLanding));
    }

    private Repertoire PreferredRepertoireForLanding(int beatsUntilLanding)
    {
        return controller.beatManager.Drop is { inProgress: false, beatsUntilStart: { } dropBeatsUntilStart } && dropBeatsUntilStart == beatsUntilLanding
            ? Repertoire.HandlesDrop
            : Repertoire.None;
    }

    private void StartSyncedTransition(int startBeat, int landingBeat, Repertoire preferredRepertoire)
    {
        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = PullEffect(preferredRepertoire);
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;
        controller.effectText.text = switcher.CurrentName;

        transitionStartBeat = startBeat;
        transitionLandingBeat = landingBeat;
        lastCueBeat = startBeat;
        TransitionProgress = 0f;
    }

    private void UpdateSyncedTransition(int beat)
    {
        if (beat >= transitionLandingBeat)
        {
            switcher.CompleteTransition();
            MarkChangedOnBeat(transitionLandingBeat);
            nextTransitionIndex = PullCard(transitionDeck);
            controller.currentTransition = nextTransitionIndex;
            controller.effectText.text = switcher.CurrentName;
            TransitionProgress = 0f;
            return;
        }

        TransitionProgress = Mathf.Clamp01((beat - transitionStartBeat) / (float)TransitionLengthBeats);
    }

    private bool CanChangeAtBeat(int beat)
    {
        return lastChangeBeat == int.MinValue || beat - lastChangeBeat >= MinimumChangeCadenceBeats;
    }

    private void RunDefaultModeTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
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
            switcher.CompleteTransition();
            defaultTimer.Set(controller.effectTime);
            defaultTimer.Reset();
            TransitionProgress = 0f;
            controller.effectText.text = switcher.CurrentName;
            nextTransitionIndex = PullCard(transitionDeck);
            controller.currentTransition = nextTransitionIndex;
            return;
        }

        var transitionIndex = nextTransitionIndex;
        var targetEffectIndex = PullEffect(Repertoire.None);
        switcher.StartTransition(targetEffectIndex, transitionIndex);
        controller.currentTransition = transitionIndex;
        defaultTimer.Set(controller.transitionTime);
        defaultTimer.Reset();
        TransitionProgress = 0f;
        controller.effectText.text = switcher.CurrentName;
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
