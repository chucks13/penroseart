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
/// Decides what plays and when it changes as a wire-change reducer (ADR-0011, ADR-0019). In Synced Mode the
/// wall runs the track-scoped <see cref="TrackCueSheet"/> of the on-air focus player: one complete show plan
/// is built per player the moment that player's structure generation changes, and the Director follows the
/// wire — the focus player's live order, that player's absolute beat, and the on-air Grid — to Cast the plan's
/// Cue Marks fire-and-forget through the <see cref="Switcher"/>. It keeps no self-ticked beat count: position
/// is read from the wire only, so the drift bugs of the old reactive Director cannot return. Standalone Mode
/// (timer-driven, no wire) is unchanged.
/// </summary>
[Serializable]
public sealed class Director
{
    private readonly Controller controller;
    private readonly Switcher switcher;
    private readonly Timer standaloneTimer;
    private readonly int[] effectDeck;
    private readonly int[] transitionDeck;

    private int currentEffectIndexForSelection = -1;
    private int nextEffectIndex = -1;
    private int nextTransitionIndex;
    private bool holdSelectedEffect;
    private bool holdSelectedTransition;

    private DirectorMode lastLoggedMode = DirectorMode.NotReady;

    // One track-scoped Cue Sheet per physical player, mirroring the Players group. A slot is (re)built when
    // that player's structure generation changes (inequality only, never ordering); the seed is (generation,
    // player number). Track ID plays no role. sheetGeneration[slot] > 0 marks a live sheet; 0 marks none.
    private const int PlayerCount = RaveWireSnapshot.PlayerCount;
    private readonly TrackCueSheet[] sheets = new TrackCueSheet[PlayerCount];
    private readonly int[] sheetGeneration = new int[PlayerCount];

    // The last Cast, keyed by (focus player, Cue Mark beat). A mark is Cast when this key changes, so focus
    // handover, needle-drop and loop-exit are one mechanism: the focus position resolves to a different mark
    // and a normal Cast takes over. A steady position never re-casts the same mark.
    private int lastCastFocusPlayer = -1;
    private int lastCastMarkBeat = int.MinValue;

    // Consecutive on-air Grid starts with no Cast, counted from observed Grid-lane wraps — never a self-ticked
    // beat. At the ceiling (four Grid starts, 64 beats) the Director re-asserts the focus segment so a loop
    // pinned inside one segment never leaves the wall stale. Any Cast resets the count.
    private const int StarvationGridStartCeiling = 4;
    private int starvedGridStarts;

    /// <summary>Last on-air Grid beat observed while recognizing a return to beat one; null before the first read.</summary>
    private int? previousOnAirGridBeat;

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

    /// <summary>Index of the Effect staged for the next Standalone A-to-B move.</summary>
    public int NextEffectIndex => nextEffectIndex;

    /// <summary>Index of the Transition staged for the next Standalone A-to-B move.</summary>
    public int NextTransitionIndex => nextTransitionIndex;

    /// <summary>Whether the staged Effect should be kept after each completed move.</summary>
    public bool HoldSelectedEffect => holdSelectedEffect;

    /// <summary>Whether the staged Transition should be kept after each completed move.</summary>
    public bool HoldSelectedTransition => holdSelectedTransition;

    /// <summary>Stages the Effect that the next Standalone A-to-B move should target.</summary>
    public void SetNextEffect(int effectIndex)
    {
        ValidateEffectIndex(effectIndex);
        nextEffectIndex = effectIndex;
        Trace(() => $"NEXT_EFFECT_SET nextEffect={FormatEffect(nextEffectIndex)} hold={holdSelectedEffect}");
    }

    /// <summary>Stages the Transition that the next Standalone A-to-B move should use.</summary>
    public void SetNextTransition(int transitionIndex)
    {
        ValidateTransitionIndex(transitionIndex);
        nextTransitionIndex = transitionIndex;
        controller.currentTransition = nextTransitionIndex;
        Trace(() => $"NEXT_TRANSITION_SET nextTransition={FormatTransition(nextTransitionIndex)} hold={holdSelectedTransition}");
    }

    /// <summary>When enabled, the currently staged Effect is staged again after each completed Standalone move.</summary>
    public void SetHoldSelectedEffect(bool hold)
    {
        holdSelectedEffect = hold;
        Trace(() => $"NEXT_EFFECT_HOLD_SET hold={holdSelectedEffect} nextEffect={FormatEffect(nextEffectIndex)}");
    }

    /// <summary>When enabled, the currently staged Transition is staged again after each completed Standalone move.</summary>
    public void SetHoldSelectedTransition(bool hold)
    {
        holdSelectedTransition = hold;
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
            Trace(() => $"TIMER_IGNORED_SYNC beat={FormatNullableBeat(controller.beatManager.Timing.Beat)}");
            return;
        }

        Trace(() => "TIMER_FINISHED_STANDALONE");
        RunStandaloneTimerDecision();
    }

    /// <summary>Advances Standalone cadence after clearing synchronized reducer state.</summary>
    private void TickStandaloneMode(float deltaTime)
    {
        // The mode boundary clears the plan-follow state so no stale sheet or Grid observation crosses a
        // Standalone gap. Idempotent every frame (ADR-0007). The Switcher's fire-and-forget Cast parks
        // nothing, so there is no beat-domain cue to abort.
        ResetReducerMemory();
        standaloneTimer.Update(deltaTime);
    }

    /// <summary>
    /// Runs the plan-driven reducer for one frame: maintains the six sheet slots, observes the on-air Grid,
    /// and follows the on-air focus player's sheet to Cast Cue Marks at their Runway starts.
    /// </summary>
    private void TickSyncedMode()
    {
        MaintainSheets();
        var gridStarted = ObserveOnAirGridStart();
        FollowFocus(gridStarted);
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
    /// Follows the on-air focus player's sheet by pure wire position and Casts due Cue Marks. A held Effect
    /// suspends casting (ADR-0017: Hold trumps). The target mark is the upcoming mark once the focus beat
    /// reaches its Transition's Runway start, otherwise the mark owning the current segment — so a normal
    /// forward crossing Casts on time and a jump (handover, needle-drop, loop) re-asserts the current segment.
    /// A mark is Cast only when its (focus, beat) key differs from the last Cast.
    /// </summary>
    private void FollowFocus(bool gridStarted)
    {
        if (controller.TryGetHeldEffectIndex(out _))
        {
            return;
        }

        if (!TryResolveFocus(out var focusPlayer, out var sheet, out var focusBeat))
        {
            return;
        }

        if (sheet.Marks == null || sheet.Marks.Count == 0)
        {
            return;
        }

        if (ResolveTargetMark(sheet, focusBeat) is { } mark
            && !(lastCastFocusPlayer == focusPlayer && lastCastMarkBeat == mark.Beat))
        {
            CastMark(focusPlayer, mark, focusBeat);
            return;
        }

        if (!gridStarted)
        {
            return;
        }

        // Starvation guard: a loop pinned inside one segment never changes the target mark, so no Cast fires.
        // Count on-air Grid starts and, at the ceiling, re-assert the owning mark so the wall never goes stale.
        starvedGridStarts++;
        if (starvedGridStarts < StarvationGridStartCeiling)
        {
            return;
        }

        if (OwningMark(sheet, focusBeat) is { } owning)
        {
            CastMark(focusPlayer, owning, focusBeat);
        }
        else
        {
            starvedGridStarts = 0;
        }
    }

    /// <summary>
    /// The mark the wall should currently be performing: the next mark once the focus beat has reached its
    /// Runway start (decide-at-cast at the last responsible moment), otherwise the mark owning the segment the
    /// focus beat sits in. Null before the first mark when it is not yet within Runway.
    /// </summary>
    private CuePlanMark? ResolveTargetMark(TrackCueSheet sheet, int focusBeat)
    {
        if (FirstMarkAtOrAfter(sheet, focusBeat) is { } next)
        {
            var runwayBeats = controller.transitions[next.TransitionIndex].Repertoire.RunwayBeats;
            if (focusBeat >= next.Beat - runwayBeats)
            {
                return next;
            }
        }

        return OwningMark(sheet, focusBeat);
    }

    /// <summary>The first Cue Mark whose beat is at or after <paramref name="focusBeat"/>, or null past the last mark.</summary>
    private static CuePlanMark? FirstMarkAtOrAfter(TrackCueSheet sheet, int focusBeat)
    {
        var marks = sheet.Marks;
        for (var i = 0; i < marks.Count; i++)
        {
            if (marks[i].Beat >= focusBeat)
            {
                return marks[i];
            }
        }

        return null;
    }

    /// <summary>The greatest Cue Mark whose beat is at or before <paramref name="focusBeat"/>, or null in the run-in.</summary>
    private static CuePlanMark? OwningMark(TrackCueSheet sheet, int focusBeat)
    {
        var marks = sheet.Marks;
        CuePlanMark? owning = null;
        for (var i = 0; i < marks.Count; i++)
        {
            if (marks[i].Beat <= focusBeat)
            {
                owning = marks[i];
            }
            else
            {
                break;
            }
        }

        return owning;
    }

    /// <summary>
    /// Resolves the on-air focus player, its live Cue Sheet, and its absolute wire beat. False when there is no
    /// focus, no built sheet for that player, or no usable beat — each degraded silently (the wall keeps playing).
    /// </summary>
    private bool TryResolveFocus(out int focusPlayer, out TrackCueSheet sheet, out int focusBeat)
    {
        focusPlayer = -1;
        sheet = default;
        focusBeat = 0;

        if (controller.beatManager.LiveOrder.Focus is not { } focus)
        {
            return false;
        }

        var slot = focus - 1;
        if (slot < 0 || slot >= PlayerCount || sheetGeneration[slot] <= 0)
        {
            return false;
        }

        if (controller.beatManager.Players[slot].Beat is not { } beat)
        {
            return false;
        }

        focusPlayer = focus;
        sheet = sheets[slot];
        focusBeat = beat;
        return true;
    }

    /// <summary>
    /// Casts one Cue Mark to the Switcher fire-and-forget: the mark's baked Effect and Transition, aimed at the
    /// mark's absolute beat against the focus player's clock. The Switcher owns all Runway/impact/tail timing;
    /// the Director only decides which mark, and when, from wire facts. Any Cast resets the starvation count.
    /// </summary>
    private void CastMark(int focusPlayer, CuePlanMark mark, int focusBeat)
    {
        var transition = controller.transitions[mark.TransitionIndex];
        var cue = new SwitcherCueDirection(mark.Beat, mark.EffectIndex, mark.TransitionIndex, transition.Repertoire);
        switcher.Cast(cue, FocusClockSnapshot(focusBeat));

        controller.currentTransition = mark.TransitionIndex;
        currentEffectIndexForSelection = mark.EffectIndex;
        lastCastFocusPlayer = focusPlayer;
        lastCastMarkBeat = mark.Beat;
        starvedGridStarts = 0;
        Trace(() => $"SYNC_CAST focus={focusPlayer} focusBeat={focusBeat} mark={mark.Beat} transition={FormatTransition(mark.TransitionIndex)} target={FormatEffect(mark.EffectIndex)}");
    }

    /// <summary>Observes the on-air Grid lane and reports whether it wrapped back to beat one this frame.</summary>
    private bool ObserveOnAirGridStart()
    {
        var gridBeat = controller.beatManager.Grid.Beat;
        var started = gridBeat is 1 && previousOnAirGridBeat is { } previous && previous != 1;
        previousOnAirGridBeat = gridBeat;
        return started;
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

    /// <summary>Captures the beat-clock facts the Switcher Cast needs, timed against the focus player's beat.</summary>
    private SwitcherClockSnapshot FocusClockSnapshot(int focusBeat)
    {
        return new SwitcherClockSnapshot(
            focusBeat,
            controller.beatManager.Timing.BeatProgress ?? 0f,
            CurrentSecondsPerBeat(),
            Time.time);
    }

    /// <summary>Returns live seconds per beat, falling back to the established 120-BPM cadence.</summary>
    private float CurrentSecondsPerBeat()
    {
        return controller.beatManager.Timing.Bpm is { } bpm && bpm > 0f ? 60f / bpm : 0.5f;
    }

    /// <summary>Clears the plan-follow reducer state across a mode boundary, forcing a fresh sheet build on return.</summary>
    private void ResetReducerMemory()
    {
        Array.Clear(sheetGeneration, 0, sheetGeneration.Length);
        lastCastFocusPlayer = -1;
        lastCastMarkBeat = int.MinValue;
        starvedGridStarts = 0;
        previousOnAirGridBeat = null;
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
