using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only snapshot of the Mechanical Switcher's current stage state for HUDs and inspectors.
/// </summary>
public readonly struct SwitcherStatus
{
    public static SwitcherStatus NotReady { get; } = new SwitcherStatus(
        false,
        -1,
        string.Empty,
        -1,
        string.Empty,
        -1,
        string.Empty,
        -1,
        string.Empty,
        string.Empty,
        0f);

    public readonly bool Ready;
    public readonly int CurrentEffectIndex;
    public readonly string CurrentEffectName;
    public readonly int SourceEffectIndex;
    public readonly string SourceEffectName;
    public readonly int TargetEffectIndex;
    public readonly string TargetEffectName;
    public readonly int CurrentTransitionIndex;
    public readonly string CurrentTransitionName;
    public readonly string StageName;
    public readonly float TransitionProgress;

    public SwitcherStatus(
        bool ready,
        int currentEffectIndex,
        string currentEffectName,
        int sourceEffectIndex,
        string sourceEffectName,
        int targetEffectIndex,
        string targetEffectName,
        int currentTransitionIndex,
        string currentTransitionName,
        string stageName,
        float transitionProgress)
    {
        Ready = ready;
        CurrentEffectIndex = currentEffectIndex;
        CurrentEffectName = currentEffectName;
        SourceEffectIndex = sourceEffectIndex;
        SourceEffectName = sourceEffectName;
        TargetEffectIndex = targetEffectIndex;
        TargetEffectName = targetEffectName;
        CurrentTransitionIndex = currentTransitionIndex;
        CurrentTransitionName = currentTransitionName;
        StageName = stageName;
        TransitionProgress = Mathf.Clamp01(transitionProgress);
    }
}

/// <summary>
/// Timing context for a started A-to-B Transition.
/// </summary>
public readonly struct TransitionStartTiming
{
    private readonly bool useBeatDuration;
    private readonly float secondsPerBeat;

    private TransitionStartTiming(float startTime, bool useBeatDuration, float secondsPerBeat)
    {
        if (useBeatDuration && secondsPerBeat <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(secondsPerBeat), secondsPerBeat, "Seconds per beat must be positive.");
        }

        StartTime = startTime;
        this.useBeatDuration = useBeatDuration;
        this.secondsPerBeat = secondsPerBeat;
    }

    /// <summary>Unity time when the transition should be considered started.</summary>
    public float StartTime { get; }

    /// <summary>Creates timing for beat-denominated Synced Mode execution.</summary>
    public static TransitionStartTiming FromBeatClock(float startTime, float secondsPerBeat)
    {
        return new TransitionStartTiming(startTime, true, secondsPerBeat);
    }

    /// <summary>Creates timing for Standalone Mode execution using the transition's default duration.</summary>
    public static TransitionStartTiming FromDefaultDuration(float startTime)
    {
        return new TransitionStartTiming(startTime, false, 0f);
    }

    /// <summary>Resolves the active execution duration from the selected transition's repertoire.</summary>
    public float DurationSeconds(TransitionRepertoire repertoire)
    {
        var durationSeconds = useBeatDuration
            ? repertoire.DurationBeats * secondsPerBeat
            : repertoire.DefaultDurationSeconds;
        if (durationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(repertoire), durationSeconds, "Transition duration cannot be negative.");
        }

        return durationSeconds;
    }
}

/// <summary>
/// Mechanical stage switcher for Penrose performers. It executes the Cue Sheet handed over by the
/// Director (ADR-0020): each tick it reads BeatManager directly for the sheet player's beat, performs
/// due Cue Marks so each Impact Point lands on its mark, and checks marks off permanently, so no
/// re-crossing — loop, back-cue, or needle-drop — ever re-fires one. It owns all Runway/Impact/Tail
/// timing and selects nothing — every decision is asked of the bound <see cref="Director"/>. It holds
/// no loaded-cue lifecycle, no Lock Point, and no accept/reject verdict.
/// </summary>
[Serializable]
public sealed class Switcher
{
    private readonly Controller controller;
    private readonly EffectBase[] effects;
    private readonly TransitionBase[] transitions;

    private int currentEffectIndex = -1;
    private int currentTransitionIndex = -1;
    private bool isTransitioning;
    private float transitionStartTime;
    private float transitionDurationSeconds = 1f;
    private float transitionProgress;

    // The one decider (ADR-0020): commands come down (the immediate and Standalone StartTransition
    // pushes, the sheet handover); questions go up through director.Decide*. Bound once at startup.
    private Director director;

    // The plan in force and its check-offs. The sheet is immutable and never written to; firedMarks is
    // Switcher execution state only, parallel to sheet.Marks, and lives and dies with the handover.
    // A check-off is permanent for the life of the handover: nothing but a fresh Cast clears one.
    private TrackCueSheet sheet;
    private bool[] firedMarks = Array.Empty<bool>();

    /// <summary>Last on-air Grid beat observed while recognizing a return to beat one; null before the first read.</summary>
    private int? previousGridBeat;

    // Diagnostics only. Backward beat motion decides nothing now, so this exists purely so a trace can
    // say that a loop or back-cue went past and left the check-offs standing.
    private int? previousSheetPlayerBeat;

    // Consecutive on-air Grid starts with nothing performed. Every one of them asks the Director for a cue,
    // carrying this count; the Director may decline the early ones and cannot decline at the ceiling — the
    // maximum Cue Mark gap expressed in Grids — so the wall never holds longer than the widest gap the plan
    // itself would have produced. A decline leaves the count standing; only a performed cue resets it.
    public const int StarvationGridStartCeiling = TrackCueSheet.MaximumStalenessAsks;
    private int starvedGridStarts;

    // Total staleness asks made, for the life of this Switcher. Monotonic on purpose, and deliberately NOT
    // cleared by Cast: starvedGridStarts restarts at one after every performed cue, so it cannot seed the
    // deal — a loop starving twice at one Grid Boundary would ask with the same number both times and be
    // dealt the same card. Surviving a handover matters too, because focus can flap away from a player and
    // back to the same sheet identity, which would otherwise replay a seed this Switcher has already used.
    private int stalenessAskSequence;

    /// <summary>
    /// The Cue Sheet in force — the plan this Switcher is performing. A default sheet
    /// (<see cref="TrackCueSheet.StructureGeneration"/> of zero) means no plan is in force.
    /// </summary>
    public TrackCueSheet Sheet => sheet;

    /// <summary>
    /// One check-off per <see cref="TrackCueSheet.Marks"/> entry, by index: true once that Cue Mark has been
    /// performed. Exposed read-only so debug views can show what actually happened against what was planned;
    /// writing check-offs is Switcher execution state and stays private.
    /// </summary>
    public IReadOnlyList<bool> FiredMarks => firedMarks;

    /// <summary>
    /// Consecutive on-air Grid starts with nothing performed. Every one asks the Director for a cue and
    /// carries this count, so the Director can decline early asks; at <see cref="StarvationGridStartCeiling"/>
    /// it can no longer decline. Any performed cue puts this back to zero.
    /// </summary>
    public int StarvedGridStarts => starvedGridStarts;

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>Active transition index while a transition owns the frame; otherwise -1.</summary>
    public int CurrentTransitionIndex => isTransitioning ? currentTransitionIndex : -1;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

    /// <summary>Display name for the effect or transition currently on stage.</summary>
    public string CurrentName => isTransitioning ? transitions[currentTransitionIndex].Name : effects[currentEffectIndex].Name;

    /// <summary>Current read-only mechanical stage snapshot for runtime HUDs and inspector diagnostics.</summary>
    public SwitcherStatus Status => BuildStatus();

    public Switcher(Controller controller, EffectBase[] effects, TransitionBase[] transitions)
    {
        if (controller == null)
        {
            throw new ArgumentNullException(nameof(controller));
        }

        this.controller = controller;
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
    }

    /// <summary>
    /// Binds the one decider the Switcher asks before performing anything. Separate from construction
    /// because the reference is genuinely mutual: the Director also pushes <see cref="ShowNow"/> and the
    /// Standalone <see cref="StartTransition(int, int, TransitionStartTiming)"/> down into the Switcher.
    /// </summary>
    public void BindDirector(Director director)
    {
        if (director == null)
        {
            throw new ArgumentNullException(nameof(director));
        }

        this.director = director;
    }

    /// <summary>
    /// Seeds the Switcher from Controller startup state. The effect is assumed to have already run OnStart().
    /// </summary>
    public void SetInitialEffect(int effectIndex, int transitionIndex)
    {
        ValidateEffectIndex(effectIndex);
        ValidateTransitionIndex(transitionIndex);

        currentEffectIndex = effectIndex;
        currentTransitionIndex = transitionIndex;
        isTransitioning = false;
        transitionProgress = 0f;
        Trace(() => $"SWITCHER_INIT current={FormatEffect(effectIndex)} nextTransition={FormatTransition(transitionIndex)}");
    }

    /// <summary>
    /// The handover (ADR-0020): takes the Cue Sheet now in force and resets its check-offs. "Cast" hands
    /// over the plan — it does not time a fire; <see cref="Tick"/> performs the marks. Idempotent on the
    /// sheet's (<see cref="TrackCueSheet.PlayerNumber"/>, <see cref="TrackCueSheet.StructureGeneration"/>)
    /// identity, so the Director calls it every synced tick and keeps zero handover state.
    /// <c>Cast(default)</c> clears the plan (generation 0, player 0) and is how Standalone Mode turns
    /// sheet execution off.
    /// </summary>
    public void Cast(TrackCueSheet sheet)
    {
        if (sheet.PlayerNumber == this.sheet.PlayerNumber
            && sheet.StructureGeneration == this.sheet.StructureGeneration)
        {
            return;
        }

        this.sheet = sheet;
        firedMarks = sheet.Marks is { Count: > 0 } marks ? new bool[marks.Count] : Array.Empty<bool>();
        previousGridBeat = null;
        previousSheetPlayerBeat = null;
        starvedGridStarts = 0;
        Trace(() => sheet.StructureGeneration > 0
            ? $"SWITCHER_CAST player={sheet.PlayerNumber} generation={sheet.StructureGeneration} marks={firedMarks.Length} checkOffsCleared"
            : "SWITCHER_CAST_CLEARED plan=<none>");
    }

    /// <summary>
    /// Executes the plan in force for one frame: performs the due Cue Mark and escalates staleness to
    /// the Director. Called by the Controller each frame after <see cref="Director.Tick"/> so a handover
    /// always precedes execution in the same frame. Beat motion is read forward only — a check-off is
    /// permanent, so backward motion of any kind is simply a stretch of already-performed plan.
    /// </summary>
    public void Tick()
    {
        if (sheet.Marks == null || sheet.Marks.Count == 0)
        {
            return;
        }

        // The Switcher performs the plan it holds against that plan's own player — never the focus
        // player, which it does not know about. PlayerNumber is 1-based.
        var slot = sheet.PlayerNumber - 1;
        var players = controller.beatManager.Players;
        if (slot < 0 || slot >= players.Count || players[slot].Beat is not { } beat)
        {
            return;
        }

        // Observed unconditionally so the previous-frame comparison stays coherent even on a frame that fires.
        var gridStarted = ObserveGridStart();
        TraceBackwardMotion(beat, players[slot].Loop.Rolling);

        if (TryPerformDueMark(beat))
        {
            return;
        }

        if (!gridStarted)
        {
            return;
        }

        starvedGridStarts++;
        Trace(() => $"SWITCHER_GRID_START beat={beat} starved={starvedGridStarts}/{StarvationGridStartCeiling}");

        stalenessAskSequence++;
        var decision = director.DecideStalenessCue(beat, starvedGridStarts, stalenessAskSequence);
        Trace(() => decision.Perform
            ? $"SWITCHER_STALENESS beat={beat} ask={starvedGridStarts}/{StarvationGridStartCeiling} sequence={stalenessAskSequence} cue={FormatEffect(decision.EffectIndex)} via={FormatTransition(decision.TransitionIndex)}"
            : $"SWITCHER_STALENESS beat={beat} ask={starvedGridStarts}/{StarvationGridStartCeiling} sequence={stalenessAskSequence} cue=<wait>");
        if (!decision.Perform)
        {
            // A decline leaves the deficit standing, so the next Grid start asks again with a higher ask.
            return;
        }

        // The Impact Point sits one Runway ahead so the Runway starts exactly now and the Transition plays in
        // full. Anchoring it to the current beat — as this path used to — puts the whole Runway behind the
        // playhead, which clamps the start time to now and lands progress at 1: a jump cut, every time.
        PerformCue(beat, beat + transitions[decision.TransitionIndex].Repertoire.RunwayBeats, decision);
    }

    /// <summary>
    /// Starts or replaces a transition from the current stage destination to the target effect.
    /// The Switcher owns progress and completion after this call; if another transition is still
    /// rendering, the previous destination becomes the source for this new last-command-wins move.
    /// </summary>
    public void StartTransition(int targetEffectIndex, int transitionIndex, TransitionStartTiming timing)
    {
        ValidateTransitionIndex(transitionIndex);
        StartTransition(targetEffectIndex, transitionIndex, timing, Time.time, transitions[transitionIndex].Repertoire);
    }

    /// <summary>Starts one fully resolved transition and defers its diagnostic display reads.</summary>
    private void StartTransition(
        int targetEffectIndex,
        int transitionIndex,
        TransitionStartTiming timing,
        float progressNowSeconds,
        TransitionRepertoire repertoire)
    {
        var sourceEffectIndex = isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;
        ValidateEffectIndex(sourceEffectIndex);
        ValidateEffectIndex(targetEffectIndex);
        ValidateTransitionIndex(transitionIndex);

        var transition = transitions[transitionIndex];
        transition.RandomizeTime();
        transition.V = 0f;
        transition.A = sourceEffectIndex;
        transition.B = targetEffectIndex;
        transition.OnStart();

        EffectBase.APalette.Change();
        StartEffect(targetEffectIndex);

        currentTransitionIndex = transitionIndex;
        currentEffectIndex = -1;
        transitionStartTime = timing.StartTime;
        transitionDurationSeconds = timing.DurationSeconds(repertoire);
        transitionProgress = ProgressAt(progressNowSeconds);
        isTransitioning = true;
        Trace(() => $"SWITCHER_START transition={FormatTransition(transitionIndex)} source={FormatEffect(sourceEffectIndex)} target={FormatEffect(targetEffectIndex)} A={transition.A} B={transition.B} durationSeconds={transitionDurationSeconds:0.###} progress={transitionProgress:0.###}");
        if (transitionDurationSeconds == 0f)
        {
            CompleteTransition();
        }
    }

    /// <summary>
    /// Renders the active effect or transition at the supplied Unity time into a cloned 900-tile buffer.
    /// </summary>
    public Color[] RenderAtTime(float nowSeconds, out string debugText)
    {
        if (isTransitioning)
        {
            transitionProgress = ProgressAt(nowSeconds);
            if (transitionProgress >= 1f)
            {
                CompleteTransition();
            }
        }

        if (isTransitioning)
        {
            var transition = transitions[currentTransitionIndex];
            transition.V = transitionProgress;
            transition.UpdateTime();

            var indexA = transition.A;
            var indexB = transition.B;

            effects[indexA].UpdateTime();
            if (indexA != indexB)
            {
                effects[indexB].UpdateTime();
            }

            transition.Draw();
            debugText = transition.DebugText();
            return (Color[])transition.buffer.Clone();
        }

        ValidateEffectIndex(currentEffectIndex);
        var effect = effects[currentEffectIndex];
        effect.UpdateTime();
        effect.Draw();
        debugText = effect.DebugText();
        return (Color[])effect.buffer.Clone();
    }

    /// <summary>
    /// The one firing rule (ADR-0020): performs the last unfired mark whose Runway has started and
    /// silently checks off every mark before it. This single rule covers a normal forward crossing, a
    /// mid-track focus handover, a needle-drop jump, and a late entry with no special cases. The whole
    /// list is scanned — Runway lengths vary per transition, so the window starts are not monotonic in
    /// mark order; when windows overlap the plan conflicts with itself, and taking the later mark is the
    /// accepted resolution. Returns whether a cue was performed this frame.
    /// </summary>
    private bool TryPerformDueMark(int beat)
    {
        var marks = sheet.Marks;
        var due = -1;
        for (var i = 0; i < marks.Count; i++)
        {
            if (firedMarks[i])
            {
                continue;
            }

            if (beat >= marks[i].Beat - transitions[marks[i].TransitionIndex].Repertoire.RunwayBeats)
            {
                due = i;
            }
        }

        if (due < 0)
        {
            return false;
        }

        var mark = marks[due];
        var decision = director.DecideCue(mark);
        if (!decision.Perform)
        {
            // Frozen (Hold): check nothing off, so the mark still performs on release.
            Trace(() => $"SWITCHER_FROZEN beat={beat} mark[{due}]@{mark.Beat} planned={FormatEffect(mark.EffectIndex)} via={FormatTransition(mark.TransitionIndex)}");
            return false;
        }

        var skipped = 0;
        for (var i = 0; i < due; i++)
        {
            if (!firedMarks[i])
            {
                skipped++;
            }

            firedMarks[i] = true;
        }

        firedMarks[due] = true;
        Trace(() => $"SWITCHER_DUE beat={beat} mark[{due}]@{mark.Beat} planned={FormatEffect(mark.EffectIndex)}/{FormatTransition(mark.TransitionIndex)} decided={FormatEffect(decision.EffectIndex)}/{FormatTransition(decision.TransitionIndex)} skippedCheckOffs={skipped}");
        PerformCue(beat, mark.Beat, decision);
        return true;
    }

    /// <summary>
    /// Reports backward movement of the sheet player's beat, and how many check-offs it did not disturb.
    /// Purely diagnostic: a loop, a back-cue, and a needle-drop all decide nothing here, and this trace is
    /// how a log shows that the wall stayed where the plan last put it rather than snapping backward.
    /// </summary>
    private void TraceBackwardMotion(int beat, bool loopRolling)
    {
        var movedBackward = previousSheetPlayerBeat is { } previous && beat < previous;
        var from = previousSheetPlayerBeat;
        previousSheetPlayerBeat = beat;
        if (!movedBackward)
        {
            return;
        }

        Trace(() => $"SWITCHER_BACKWARD from={from} to={beat} loopRolling={loopRolling} checkOffsStanding={CountCheckOffs()}/{firedMarks.Length}");
    }

    /// <summary>Counts standing check-offs for a trace; called only when diagnostics are enabled.</summary>
    private int CountCheckOffs()
    {
        var fired = 0;
        foreach (var mark in firedMarks)
        {
            if (mark)
            {
                fired++;
            }
        }

        return fired;
    }

    /// <summary>Observes the on-air Grid lane and reports whether it wrapped back to beat one this frame.</summary>
    private bool ObserveGridStart()
    {
        var gridBeat = controller.beatManager.Grid.Beat;
        var started = gridBeat is 1 && previousGridBeat is { } previous && previous != 1;
        previousGridBeat = gridBeat;
        return started;
    }

    /// <summary>
    /// Performs one decided cue: its Transition starts on its Runway beat (<c>Impact − Runway</c>), its
    /// Impact Point lands on <paramref name="impactBeat"/>, and its Tail completes after. Never later
    /// than now: an on-time cue starts on its Runway beat; a late cue starts already underway with a
    /// compressed Runway; a cue is never parked to wait for a future beat.
    /// </summary>
    private void PerformCue(int currentBeat, int impactBeat, CueDecision decision)
    {
        var repertoire = transitions[decision.TransitionIndex].Repertoire;
        var runwayStartBeat = impactBeat - repertoire.RunwayBeats;
        var secondsPerBeat = SecondsPerBeat();
        var nowSeconds = Time.time;
        var startTime = Mathf.Min(TimeAtBeat(runwayStartBeat, currentBeat, secondsPerBeat, nowSeconds), nowSeconds);

        StartTransition(
            decision.EffectIndex,
            decision.TransitionIndex,
            TransitionStartTiming.FromBeatClock(startTime, secondsPerBeat),
            nowSeconds,
            repertoire);
        // The Switcher owns what is on stage, so it owns the Controller's transition mirror.
        controller.currentTransition = decision.TransitionIndex;
        starvedGridStarts = 0;
        Trace(() => $"SWITCHER_PERFORM impact={impactBeat} runwayStart={runwayStartBeat} startTime={startTime:0.###} now={nowSeconds:0.###} transition={FormatTransition(decision.TransitionIndex)} target={FormatEffect(decision.EffectIndex)}");
    }

    /// <summary>Converts an absolute sheet beat to Unity time against the live beat clock.</summary>
    private float TimeAtBeat(int targetBeat, int currentBeat, float secondsPerBeat, float nowSeconds)
    {
        var beatFraction = Mathf.Clamp01(controller.beatManager.Timing.BeatProgress ?? 0f);
        var beatsUntil = targetBeat - currentBeat - beatFraction;
        return nowSeconds + (beatsUntil * secondsPerBeat);
    }

    /// <summary>Live seconds per beat, falling back to the established 120-BPM cadence.</summary>
    private float SecondsPerBeat()
    {
        return controller.beatManager.Timing.Bpm is { } bpm && bpm > 0f ? 60f / bpm : 0.5f;
    }

    /// <summary>Promotes the transition target and emits the deferred completion trace.</summary>
    private void CompleteTransition()
    {
        var transition = transitions[currentTransitionIndex];
        var completedTransitionIndex = currentTransitionIndex;
        var sourceEffectIndex = transition.A;
        var targetEffectIndex = transition.B;
        currentEffectIndex = targetEffectIndex;
        isTransitioning = false;
        transitionProgress = 0f;
        Trace(() => $"SWITCHER_COMPLETE transition={FormatTransition(completedTransitionIndex)} source={FormatEffect(sourceEffectIndex)} current={FormatEffect(currentEffectIndex)} targetWas={targetEffectIndex}");
    }

    private float ProgressAt(float now)
    {
        return transitionDurationSeconds == 0f
            ? 1f
            : Mathf.Clamp01((now - transitionStartTime) / transitionDurationSeconds);
    }

    private SwitcherStatus BuildStatus()
    {
        if (effects == null || transitions == null)
        {
            return SwitcherStatus.NotReady;
        }

        if (isTransitioning)
        {
            var transition = transitions[currentTransitionIndex];
            var sourceName = EffectName(transition.A);
            var targetName = EffectName(transition.B);
            return new SwitcherStatus(
                true,
                -1,
                string.Empty,
                transition.A,
                sourceName,
                transition.B,
                targetName,
                currentTransitionIndex,
                transition.Name,
                transition.Name,
                transitionProgress);
        }

        var currentName = EffectName(currentEffectIndex);
        return new SwitcherStatus(
            currentEffectIndex >= 0,
            currentEffectIndex,
            currentName,
            currentEffectIndex,
            currentName,
            currentEffectIndex,
            currentName,
            -1,
            string.Empty,
            currentName,
            0f);
    }

    private string EffectName(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < effects.Length ? effects[effectIndex].Name : string.Empty;
    }

    private string FormatEffect(int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < effects.Length ? $"{effectIndex}:{effects[effectIndex].Name}" : $"{effectIndex}:<none>";
    }

    private string FormatTransition(int transitionIndex)
    {
        return transitionIndex >= 0 && transitionIndex < transitions.Length ? $"{transitionIndex}:{transitions[transitionIndex].Name}" : $"{transitionIndex}:<none>";
    }

    /// <summary>Writes a Switcher trace whose display values are resolved only when tracing is enabled.</summary>
    private void Trace(Func<string> message)
    {
        controller.LogDirectorSwitching(message);
    }

    private void StartEffect(int effectIndex)
    {
        var effect = effects[effectIndex];
        effect.RandomizeTime();
        effect.OnStart();
    }

    private void ValidateEffectIndex(int effectIndex)
    {
        if (effectIndex < 0 || effectIndex >= effects.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(effectIndex), effectIndex, "Effect index is outside the runtime catalog.");
        }
    }

    private void ValidateTransitionIndex(int transitionIndex)
    {
        if (transitionIndex < 0 || transitionIndex >= transitions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionIndex), transitionIndex, "Transition index is outside the runtime catalog.");
        }
    }
}
