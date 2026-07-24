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

    // The last Cast, keyed by (focus player, Cue Mark beat). A mark is Cast when this key changes, so focus
    // handover, needle-drop and loop-exit are one mechanism: the focus position resolves to a different mark
    // and a normal Cast takes over. A steady position never re-casts the same mark.
    private int lastCastFocusPlayer = -1;
    private int lastCastMarkBeat = int.MinValue;

    // Whether the last Cast was an early runway-window cast whose Cue Mark the wire has not yet reached. Only
    // such a cast can produce loop-straddle flicker; it is cleared the moment the focus beat reaches that mark,
    // after which a backward jump re-asserts the prior segment normally (the designed one-mechanism behavior).
    private bool lastCastWasEarly;

    // Consecutive on-air Grid starts with no Cast, counted from observed Grid-lane wraps — never a self-ticked
    // beat. At the ceiling — the maximum Cue Mark gap expressed in Grids (64 beats / 16) — a loop pinned inside
    // one segment has never crossed a plan mark, so the Director injects one fresh dealt cast so the wall never
    // goes stale. Any Cast resets the count.
    private const int StarvationGridStartCeiling = TrackCueSheet.MaximumGapBeats / TrackCueSheet.GridBeats;
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
        // Initial seeding is not an operator override: clear the pending mask SetNextTransition just raised.
        overrideTransitionPending = false;
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
    /// Follows the on-air focus player's sheet by pure wire position and Casts due Cue Marks. The Controller's
    /// Effect hold freezes casting entirely (an inspection freeze); the ADR-0017 Hold Selected masks the dealt
    /// card instead and is applied at the cast, so marks keep firing. The target mark is the upcoming mark once
    /// the focus beat reaches its Transition's Runway start, otherwise the mark owning the current segment — so
    /// a normal forward crossing Casts on time and a jump (handover, needle-drop, loop) re-asserts the current
    /// segment. A mark is Cast only when its (focus, beat) key differs from the last Cast.
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

        // Once the focus beat reaches the last Cast's mark, an early runway-window cast is no longer "unreached":
        // clear the flag so a later backward jump re-asserts the prior segment normally. Wire observation only.
        if (lastCastFocusPlayer == focusPlayer && focusBeat >= lastCastMarkBeat)
        {
            lastCastWasEarly = false;
        }

        // A loop straddling the last mark's Runway start would otherwise flip the target between that next mark
        // and the prior owning mark each pass and re-cast every time; IsLoopStraddleReCast suppresses the
        // backward-adjacent owning-branch pass so a suppressed loop self-heals through the starvation guard below.
        var target = ResolveTargetMark(sheet, focusBeat, out var fromRunwayWindow);
        if (target is { } mark
            && !(lastCastFocusPlayer == focusPlayer && lastCastMarkBeat == mark.Beat)
            && !IsLoopStraddleReCast(sheet, mark, fromRunwayWindow, focusPlayer))
        {
            CastCue(focusPlayer, mark.Beat, mark.EffectIndex, mark.TransitionIndex, focusBeat, mark.Beat);
            return;
        }

        if (!gridStarted)
        {
            return;
        }

        // Starvation guard: a loop pinned inside one segment never changes the target mark, so no Cast fires.
        // Count on-air Grid starts and, at the ceiling, inject one cast dealt fresh from the sheet's bags at this
        // boundary — not the same owning mark re-fired, which would change nothing on the wall. The plan position
        // (the owning mark) is recorded as the last Cast so the plan resumes at the next real crossing; the
        // sheet never mutates.
        starvedGridStarts++;
        if (starvedGridStarts < StarvationGridStartCeiling)
        {
            return;
        }

        var injected = sheet.DealAt(focusBeat);
        var planMarkBeat = OwningMark(sheet, focusBeat) is { } owning ? owning.Beat : focusBeat;
        CastCue(focusPlayer, focusBeat, injected.EffectIndex, injected.TransitionIndex, focusBeat, planMarkBeat);
    }

    /// <summary>
    /// The mark the wall should currently be performing: the next mark once the focus beat has reached its
    /// Runway start (decide-at-cast at the last responsible moment), otherwise the mark owning the segment the
    /// focus beat sits in. Null before the first mark when it is not yet within Runway.
    /// </summary>
    /// <summary>
    /// The mark the wall should currently be performing: the next mark once the focus beat has reached its
    /// Runway start (decide-at-cast at the last responsible moment), otherwise the mark owning the segment the
    /// focus beat sits in. Null before the first mark when it is not yet within Runway. <paramref name="fromRunwayWindow"/>
    /// reports which branch produced the mark — true for the Runway-window (next-mark) branch, false for the
    /// owning-mark branch — so the caller can suppress only the owning-branch loop-straddle re-cast.
    /// </summary>
    private CuePlanMark? ResolveTargetMark(TrackCueSheet sheet, int focusBeat, out bool fromRunwayWindow)
    {
        if (FirstMarkAtOrAfter(sheet, focusBeat) is { } next)
        {
            var runwayBeats = controller.transitions[next.TransitionIndex].Repertoire.RunwayBeats;
            if (focusBeat >= next.Beat - runwayBeats)
            {
                fromRunwayWindow = true;
                return next;
            }
        }

        fromRunwayWindow = false;
        return OwningMark(sheet, focusBeat);
    }

    /// <summary>
    /// Whether an owning-branch target is a loop-straddle re-cast to suppress. Suppression is narrow: it covers
    /// only a loop straddling a Runway start where the last Cast was an <em>early</em> runway-window cast whose
    /// Cue Mark the wire never reached (<see cref="lastCastWasEarly"/>). Such a loop flips
    /// <see cref="ResolveTargetMark"/> between that next mark (Runway-window branch) on the forward pass and the
    /// prior owning mark on the backward pass; without suppression the (focus, beat) key alternates and re-casts
    /// the transition every pass — visible flicker. Suppress only when the target came from the owning branch,
    /// for the same focus player, the last Cast was early, and the target is exactly one segment below the last
    /// Cast (the sheet's first mark strictly after it is the last-cast mark). Once the wire actually reaches a
    /// mark the flag clears, so a backward jump re-asserts the prior segment normally — the designed
    /// one-mechanism behavior. Runway-window casts are never suppressed.
    /// </summary>
    private bool IsLoopStraddleReCast(TrackCueSheet sheet, CuePlanMark target, bool fromRunwayWindow, int focusPlayer)
    {
        if (fromRunwayWindow || lastCastFocusPlayer != focusPlayer || !lastCastWasEarly)
        {
            return false;
        }

        return FirstMarkStrictlyAfter(sheet, target.Beat) is { } next && next.Beat == lastCastMarkBeat;
    }

    /// <summary>The first Cue Mark whose beat is strictly after <paramref name="beat"/>, or null past the last mark.</summary>
    private static CuePlanMark? FirstMarkStrictlyAfter(TrackCueSheet sheet, int beat)
    {
        var marks = sheet.Marks;
        for (var i = 0; i < marks.Count; i++)
        {
            if (marks[i].Beat > beat)
            {
                return marks[i];
            }
        }

        return null;
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
    /// <summary>
    /// Casts one cue to the Switcher fire-and-forget, applying ADR-0017 override masks to the plan-dealt cards.
    /// <paramref name="cueMarkBeat"/> is the impact beat the cast aims at; <paramref name="planMarkBeat"/> is
    /// the plan position recorded as the last Cast (equal to the mark for a normal cast; the owning mark for a
    /// starvation injection whose impact beat is the boundary). Any Cast resets the starvation count.
    /// </summary>
    private void CastCue(int focusPlayer, int cueMarkBeat, int planEffectIndex, int planTransitionIndex, int focusBeat, int planMarkBeat)
    {
        var effectIndex = ResolveEffectOverride(planEffectIndex);
        var transitionIndex = ResolveTransitionOverride(planTransitionIndex);
        var transition = controller.transitions[transitionIndex];
        switcher.Cast(new SwitcherCueDirection(cueMarkBeat, effectIndex, transitionIndex, transition.Repertoire), FocusClockSnapshot(focusBeat));

        controller.currentTransition = transitionIndex;
        currentEffectIndexForSelection = effectIndex;
        lastCastFocusPlayer = focusPlayer;
        lastCastMarkBeat = planMarkBeat;
        // Early = a runway-window cast fired before the wire reached its Cue Mark (focus beat still behind it).
        lastCastWasEarly = focusBeat < cueMarkBeat;
        starvedGridStarts = 0;
        Trace(() => $"SYNC_CAST focus={focusPlayer} focusBeat={focusBeat} cueMark={cueMarkBeat} plan={planMarkBeat} transition={FormatTransition(transitionIndex)} target={FormatEffect(effectIndex)}");
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
        lastCastWasEarly = false;
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
