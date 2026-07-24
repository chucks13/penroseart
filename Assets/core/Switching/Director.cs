using System;
using System.Collections.Generic;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>High-level cadence source currently driving the Director.</summary>
public enum DirectorMode
{
    NotReady,
    Standalone,
    Synced,
    Hold
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
        false);

    /// <summary>Which operating mode the Director is in this frame.</summary>
    public readonly DirectorMode Mode;

    /// <summary>True when the wall is in Synced Mode (the reducer is live).</summary>
    public readonly bool IsSyncedMode;

    /// <summary>Current live beat observed by the Director, or -1 outside Synced Mode.</summary>
    public readonly int CurrentBeat;

    /// <summary>Staged effect index for the next Standalone move, or -1 when nothing is staged.</summary>
    public readonly int NextEffectIndex;

    /// <summary>Display name of the staged effect, or empty when nothing is staged.</summary>
    public readonly string NextEffectName;

    /// <summary>Staged transition index for the next Standalone move, or -1 before the Director is ready.</summary>
    public readonly int NextTransitionIndex;

    /// <summary>Display name of the staged transition, or empty before the Director is ready.</summary>
    public readonly string NextTransitionName;

    /// <summary>Whether the staged Effect is kept after each completed move.</summary>
    public readonly bool HoldSelectedEffect;

    /// <summary>Whether the staged Transition is kept after each completed move.</summary>
    public readonly bool HoldSelectedTransition;

    public DirectorStatus(
        DirectorMode mode,
        bool isSyncedMode,
        int currentBeat,
        int nextEffectIndex,
        string nextEffectName,
        int nextTransitionIndex,
        string nextTransitionName,
        bool holdSelectedEffect,
        bool holdSelectedTransition)
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
    }
}

/// <summary>
/// The Director's answer to a Switcher question (ADR-0020): whether to perform at all, and with which
/// Effect and Transition after override masking. Commands go down, questions go up — the Switcher asks,
/// the Director answers, and the Switcher executes the answer on its own timeline.
/// </summary>
public readonly struct CueDecision
{
    /// <summary>The frozen answer: the wall is under Hold (an inspection freeze) and nothing is performed.</summary>
    public static CueDecision Frozen => default;

    /// <summary>Whether the Switcher should perform this cue at all.</summary>
    public readonly bool Perform;

    /// <summary>Effect catalog index to perform, after override masking; meaningless unless <see cref="Perform"/>.</summary>
    public readonly int EffectIndex;

    /// <summary>Transition catalog index to perform, after override masking; meaningless unless <see cref="Perform"/>.</summary>
    public readonly int TransitionIndex;

    /// <summary>A performing decision carrying the masked Effect and Transition indices.</summary>
    public CueDecision(int effectIndex, int transitionIndex)
    {
        Perform = true;
        EffectIndex = effectIndex;
        TransitionIndex = transitionIndex;
    }
}

/// <summary>
/// Decides what plays; it never times a fire (ADR-0011, ADR-0020). In Synced Mode it builds one
/// track-scoped <see cref="TrackCueSheet"/> per player the moment that player's structure generation
/// changes, hands the on-air focus player's sheet to the <see cref="Switcher"/> every tick (an idempotent
/// Cast), and answers the Switcher's questions — a planned mark's masked cards, or a fresh one-off deal
/// when the wall has gone stale. It holds no Runway arithmetic, follows no position, observes no Grid,
/// and keeps no cast memory: execution belongs wholly to the Switcher. Standalone Mode (timer-driven,
/// no wire) is unchanged.
/// </summary>
[Serializable]
public sealed class Director
{
    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer standaloneTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;

    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;

    // One-shot override masks (ADR-0017): a manual SetNext* stages a pick that replaces exactly the next synced
    // cast's dealt card and is then consumed, so the plan resumes verbatim. Auto-staging and enabling a Hold
    // both clear it. Overrides mask, never mutate — the sheet stays a pure function of (structure, seed).
    private bool overrideEffectPending;
    private bool overrideTransitionPending;

    private DirectorMode lastLoggedMode = DirectorMode.NotReady;

    // One track-scoped Cue Sheet per physical player, mirroring the Players group. A slot is (re)built when
    // that player's structure generation changes (inequality only, never ordering); the seed is (generation,
    // player number). Track ID plays no role. sheetGeneration[slot] > 0 marks a live sheet; 0 marks none.
    private const int PlayerCount = RaveWireSnapshot.PlayerCount;
    private readonly TrackCueSheet[] sheets = new TrackCueSheet[PlayerCount];
    private readonly int[] sheetGeneration = new int[PlayerCount];

    /// <summary>
    /// The Cue Sheet built for each physical player slot, indexed by player number minus one. A slot whose
    /// <see cref="TrackCueSheet.StructureGeneration"/> is zero has no sheet. Exposed read-only so debug views
    /// can show what the Director planned for every loaded track, not just the one on air.
    /// </summary>
    public IReadOnlyList<TrackCueSheet> Sheets => sheets;

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
        // Initial seeding is not an operator override: clear the pending mask SetNextTransition just raised.
        overrideTransitionPending = false;
        StageNextEffect(switcher.TransitionTargetEffectIndex);
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

    /// <summary>Index of the Effect staged for the next Standalone A-to-B move.</summary>
    public int NextEffectIndex => nextEffectIndex;

    /// <summary>Index of the Transition staged for the next Standalone A-to-B move.</summary>
    public int NextTransitionIndex => nextTransitionIndex;

    /// <summary>Whether the staged Effect should be kept after each completed move.</summary>
    public bool HoldSelectedEffect => holdSelectedEffect;

    /// <summary>Whether the staged Transition should be kept after each completed move.</summary>
    public bool HoldSelectedTransition => holdSelectedTransition;

    /// <summary>
    /// Stages the Effect for the next A-to-B move: the next Standalone move, and — as a one-shot ADR-0017
    /// override — exactly the next synced plan cast, which plays this pick instead of its dealt card before the
    /// plan resumes verbatim. Masks, never mutates the sheet.
    /// </summary>
    public void SetNextEffect(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);
        nextEffectIndex = effectIndex;
        overrideEffectPending = true;
        Trace(() => $"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>
    /// Stages the Transition for the next A-to-B move: the next Standalone move, and — as a one-shot ADR-0017
    /// override — exactly the next synced plan cast, before the plan resumes verbatim. Masks, never mutates.
    /// </summary>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
        overrideTransitionPending = true;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_SET nextTransition={FormatTransition(nextTransitionIndex)} hold={holdSelectedTransition}");
    }

    /// <summary>
    /// Holds the staged Effect: it is re-staged after each Standalone move, and in Synced Mode it trumps every
    /// dealt card while held (marks keep firing on cadence with the held pick). Enabling a Hold clears any
    /// pending one-shot override so releasing it lands on exactly what the sheet says.
    /// </summary>
    public void SetHoldSelectedEffect(bool hold)
    {
        holdSelectedEffect = hold;
        if (hold)
        {
            overrideEffectPending = false;
        }

        Trace(() => $"NEXT_EFFECT_HOLD_SET hold={holdSelectedEffect} nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>
    /// Holds the staged Transition: re-staged after each Standalone move, and in Synced Mode it trumps every
    /// dealt card while held. Enabling a Hold clears any pending one-shot override so release lands on the plan.
    /// </summary>
    public void SetHoldSelectedTransition(bool hold)
    {
        holdSelectedTransition = hold;
        if (hold)
        {
            overrideTransitionPending = false;
        }

        Trace(() => $"NEXT_TRANSITION_HOLD_SET hold={holdSelectedTransition} nextTransition={FormatTransition(nextTransitionIndex)}");
    }

    /// <summary>Advances the Director's Standalone cadence clock or, in Synced Mode, the plan-driven reducer.</summary>
    public void Tick(float deltaTime)
    {
        LogModeIfChanged();

        // The mode authority alone owns the Synced/Standalone fallthrough (ADR-0007).
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
        Trace(() => $"SHOW_NOW effect={FormatEffect(effectIndex)} durationSeconds={durationSeconds:0.###} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
        switcher.ShowNow(effectIndex);
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

        if (switcher.TransitionTargetEffectIndex != heldEffectIndex)
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
            Trace(() => $"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
            return;
        }

        Trace(() => "TIMER_FINISHED_STANDALONE");
        RunStandaloneTimerDecision();
    }

    /// <summary>Advances Standalone cadence after clearing synchronized reducer state.</summary>
    private void TickStandaloneMode(float deltaTime)
    {
        // The mode boundary clears the sheet slots and the Switcher's in-force sheet so no stale plan
        // crosses a Standalone gap. Idempotent every frame (ADR-0007).
        ResetReducerMemory();
        standaloneTimer.Update(deltaTime);
    }

    /// <summary>
    /// Runs the decider for one frame: maintains the six sheet slots, then hands the on-air focus
    /// player's sheet over. <see cref="Switcher.Cast"/> is idempotent on the sheet's identity, so casting
    /// every tick keeps the Director free of handover memory; no focus or no built sheet casts a default
    /// sheet, which clears the plan in force.
    /// </summary>
    private void TickSyncedMode()
    {
        MaintainSheets();
        switcher.Cast(TryResolveFocusSheet(out var sheet) ? sheet : default);
    }

    /// <summary>
    /// Rebuilds each player's Cue Sheet slot when that player's structure generation changes. A sheet is built
    /// only from a complete structure (visible phrase list equals the announced count); until it converges the
    /// slot stays empty and the generation is left unstamped so the build retries. Generation zero (no
    /// structure) clears the slot. Determinism replaces caching: the seed is (generation, player number).
    /// </summary>
    private void MaintainSheets()
    {
        var players = controller.beatManager.Players;
        for (var slot = 0; slot < PlayerCount; slot++)
        {
            var structure = players[slot].Structure;
            var generation = structure.Generation;
            if (generation == sheetGeneration[slot])
            {
                continue;
            }

            if (generation <= 0)
            {
                sheets[slot] = default;
                sheetGeneration[slot] = generation;
                continue;
            }

            if (structure.PhraseCount <= 0 || structure.Phrases.Count != structure.PhraseCount)
            {
                // Structure not yet complete: keep playing and retry next frame without stamping.
                continue;
            }

            var playerNumber = slot + 1;
            var built = TrackCueSheet.Build(
                structure,
                BuildEffectDescriptors(),
                BuildTransitionDescriptors(),
                generation,
                playerNumber);
            sheets[slot] = built;
            sheetGeneration[slot] = generation;
            Trace(() => $"SHEET_BUILT player={playerNumber} generation={generation} marks={built.Marks.Count}");
        }
    }

    /// <summary>
    /// Resolves the on-air focus player's built Cue Sheet. False when there is no focus or no built sheet
    /// for it, each degraded silently (the wall keeps playing); <paramref name="sheet"/> is then default.
    /// </summary>
    private bool TryResolveFocusSheet(out TrackCueSheet sheet)
    {
        sheet = default;
        if (controller.beatManager.LiveOrder.Focus is not { } focus)
        {
            return false;
        }

        var slot = focus - 1;
        if (slot < 0 || slot >= PlayerCount || sheetGeneration[slot] <= 0)
        {
            return false;
        }

        sheet = sheets[slot];
        return true;
    }

    /// <summary>
    /// Answers what the Switcher should perform for a planned Cue Mark (ADR-0020): frozen under Hold —
    /// an inspection freeze, so the mark performs on release — otherwise the mark's baked cards with the
    /// ADR-0017 override masks applied. The sheet is never mutated; overrides mask.
    /// </summary>
    public CueDecision DecideCue(CuePlanMark mark)
    {
        return Decide(mark.EffectIndex, mark.TransitionIndex);
    }

    /// <summary>
    /// Answers the Switcher's staleness escalation: nothing has performed for the whole starvation
    /// window, so deal one fresh off-plan card from the on-air focus player's sheet at
    /// <paramref name="beat"/> and mask it like any other deal. Frozen under Hold, and when there is no
    /// focus or no built sheet.
    /// </summary>
    public CueDecision DecideOneOff(int beat)
    {
        if (!TryResolveFocusSheet(out var sheet))
        {
            return CueDecision.Frozen;
        }

        var dealt = sheet.DealAt(beat);
        return Decide(dealt.EffectIndex, dealt.TransitionIndex);
    }

    /// <summary>
    /// The one decision policy behind both questions (ADR-0020): Hold freezes the wall and answers
    /// no-perform; otherwise the plan-dealt cards pass through the ADR-0017 override masks.
    /// </summary>
    private CueDecision Decide(int planEffectIndex, int planTransitionIndex)
    {
        if (controller.TryGetHeldEffectIndex(out _))
        {
            return CueDecision.Frozen;
        }

        return new CueDecision(ResolveEffectOverride(planEffectIndex), ResolveTransitionOverride(planTransitionIndex));
    }

    /// <summary>
    /// Applies ADR-0017 override masking to a plan-dealt Effect: a Hold trumps every deal and returns the held
    /// pick; otherwise a one-shot staged pick replaces exactly this cast and is then consumed; with neither, the
    /// plan's card plays. A masking read only — the sheet is never mutated.
    /// </summary>
    private int ResolveEffectOverride(int planEffectIndex)
    {
        if (holdSelectedEffect)
        {
            return nextEffectIndex;
        }

        if (overrideEffectPending)
        {
            overrideEffectPending = false;
            return nextEffectIndex;
        }

        return planEffectIndex;
    }

    /// <summary>
    /// Applies ADR-0017 override masking to a plan-dealt Transition: a Hold trumps every deal; otherwise a
    /// one-shot staged pick replaces exactly this cast and is then consumed; with neither, the plan's card plays.
    /// </summary>
    private int ResolveTransitionOverride(int planTransitionIndex)
    {
        if (holdSelectedTransition)
        {
            return nextTransitionIndex;
        }

        if (overrideTransitionPending)
        {
            overrideTransitionPending = false;
            return nextTransitionIndex;
        }

        return planTransitionIndex;
    }

    /// <summary>Builds the Effect catalog as descriptors (index + live effective repertoire) for the sheet builder.</summary>
    private IReadOnlyList<EffectDescriptor> BuildEffectDescriptors()
    {
        var effects = controller.effects;
        var descriptors = new EffectDescriptor[effects.Length];
        for (var i = 0; i < effects.Length; i++)
        {
            descriptors[i] = new EffectDescriptor(i, controller.EffectiveRepertoire(i));
        }

        return descriptors;
    }

    /// <summary>Builds the Transition catalog as descriptors (index + repertoire) for the sheet builder.</summary>
    private IReadOnlyList<TransitionDescriptor> BuildTransitionDescriptors()
    {
        var transitions = controller.transitions;
        var descriptors = new TransitionDescriptor[transitions.Length];
        for (var i = 0; i < transitions.Length; i++)
        {
            descriptors[i] = new TransitionDescriptor(i, transitions[i].Repertoire);
        }

        return descriptors;
    }

    /// <summary>
    /// Clears the sheet slots and the Switcher's in-force sheet across a mode boundary, forcing a fresh
    /// build and handover on return to Synced Mode.
    /// </summary>
    private void ResetReducerMemory()
    {
        Array.Clear(sheetGeneration, 0, sheetGeneration.Length);
        switcher.Cast(default);
    }

    /// <summary>Builds the downstream read-only view of the Director's current state.</summary>
    private DirectorStatus BuildStatus()
    {
        var isSynced = IsSyncedMode;
        var isHeld = controller.TryGetHeldEffectIndex(out _);
        var mode = isHeld ? DirectorMode.Hold : isSynced ? DirectorMode.Synced : DirectorMode.Standalone;
        var currentBeat = isSynced && controller.beatManager.Timing.Beat is { } beat ? beat : -1;

        return new DirectorStatus(
            mode,
            isSynced,
            currentBeat,
            nextEffectIndex,
            EffectName(nextEffectIndex),
            nextTransitionIndex,
            TransitionName(nextTransitionIndex),
            holdSelectedEffect,
            holdSelectedTransition);
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

        Trace(() => $"MODE {lastLoggedMode}->{mode} synced={controller.beatManager.IsSynced} beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
        lastLoggedMode = mode;
    }

    /// <summary>Consumes one Standalone timer wake, honoring Effect Hold or starting the staged move.</summary>
    private void RunStandaloneTimerDecision()
    {
        if (controller.TryGetHeldEffectIndex(out var heldEffectIndex))
        {
            Trace(() => $"STANDALONE_HOLD held={FormatEffect(heldEffectIndex)} current={FormatEffect(switcher.TransitionTargetEffectIndex)}");
            if (switcher.TransitionTargetEffectIndex != heldEffectIndex)
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
        StageNextChoices();
        standaloneTimer.Set(transitionDurationSeconds + controller.effectTime);
        standaloneTimer.Reset();
    }

    /// <summary>Stages the next Standalone choices from the stage's current destination effect.</summary>
    private void StageNextChoices()
    {
        StageNextEffect(switcher.TransitionTargetEffectIndex);
        StageNextTransition();
    }

    /// <summary>Stages the next Standalone Effect unless the existing staged choice is held.</summary>
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
        // An auto-staged pick is not an operator override, so it never masks a synced cast.
        overrideEffectPending = false;
        Trace(() => $"NEXT_EFFECT_STAGED nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>Stages the next Standalone Transition unless the existing staged choice is held.</summary>
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
        // An auto-staged pick is not an operator override, so it never masks a synced cast.
        overrideTransitionPending = false;
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
