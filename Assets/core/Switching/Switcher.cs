using System;
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
/// Director (ADR-0020): each tick it reads BeatManager directly for the sheet player's beat and Grid lanes,
/// asks about the Cue Mark whose Runway begins on that beat, and fires the answer the same frame so the
/// Transition's Impact Point lands on the mark. A mark whose Runway has already gone by is missed, not
/// performed late, and nothing checks it off. A check-off is permanent, so no re-crossing — loop, back-cue,
/// or needle-drop — ever re-fires a mark; re-crossing one asks for an off-plan cue instead. It owns all
/// Runway/Impact/Tail timing and selects nothing — every decision is asked of the bound
/// <see cref="Director"/>. It holds no cue between beats and no lifecycle around one: no Standby Cue, no
/// Lock Point, no verdict, no revocation window.
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

    // The plan in force. Each mark carries its own CuePlanMark.Fired, so the Switcher keeps no execution
    // state beside the sheet: firing a cue marks the cue.
    private TrackCueSheet sheet;

    // The beat Tick last acted on. Tick runs every frame and a beat spans many of them, so this is what makes
    // "fire on the Runway beat" happen once rather than once per frame for the length of that beat.
    private int? actedBeat;

    // Grid Boundaries crossed since the last Impact Point. The plan spaces its marks one to four boundaries
    // apart, so reaching the fourth with nothing performed means the plan cannot feed the playhead: the DJ is
    // looping a stretch the plan left empty, or an inspection freeze has just ended. Counted in boundaries
    // rather than beats because a loop re-crosses the same beat numbers — only crossings measure elapsed music.
    private int boundariesSinceImpact;

    // How many off-plan asks this handover has made, the seed dimension that stops a loop re-crossing one
    // boundary from being handed the same card twice. Handover-scoped like the count above: both reset on Cast,
    // so no spacing or deal history leaks from one plan into the next.
    private int offPlanAsks;

    /// <summary>
    /// The Cue Sheet in force — the plan this Switcher is performing. A default sheet
    /// (<see cref="TrackCueSheet.StructureGeneration"/> of zero) means no plan is in force.
    /// </summary>
    public TrackCueSheet Sheet => sheet;

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
        this.director = director ?? throw new ArgumentNullException(nameof(director));
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
    /// The handover: takes the Cue Sheet now in force. "Cast" hands over the plan — it does not time a fire;
    /// <see cref="Tick"/> performs the marks. Idempotent on the sheet's
    /// (<see cref="TrackCueSheet.PlayerNumber"/>, <see cref="TrackCueSheet.StructureGeneration"/>) identity,
    /// so the Director calls it every synced tick and keeps zero handover state. <c>Cast(default)</c> clears
    /// the plan (generation 0, player 0) and is how Standalone Mode turns sheet execution off.
    /// </summary>
    public void Cast(TrackCueSheet sheet)
    {
        if (sheet.PlayerNumber == this.sheet.PlayerNumber
            && sheet.StructureGeneration == this.sheet.StructureGeneration)
        {
            return;
        }

        this.sheet = sheet;

        // A handover is a fresh start: the incoming player is somewhere else in its own track, so neither the
        // beat already acted on nor the outgoing plan's spacing and deal history mean anything here.
        actedBeat = null;
        boundariesSinceImpact = 0;
        offPlanAsks = 0;
        Trace(() => sheet.StructureGeneration > 0
            ? $"SWITCHER_CAST player={sheet.PlayerNumber} generation={sheet.StructureGeneration} marks={sheet.Marks.Count}"
            : "SWITCHER_CAST_CLEARED plan=<none>");
    }

    /// <summary>
    /// Follows the sheet player's beat and fires each cue on its Runway start — the beat a Transition has to
    /// leave on for its Impact Point to land on the mark. Called by the Controller each frame after
    /// <see cref="Director.Tick"/>, so a handover always precedes execution in the same frame. A mark the
    /// playhead reaches again has already fired, which only happens when the DJ loops, so the Director is
    /// asked for a fresh cue rather than replaying the spent one. Counting Grid Boundaries alongside the plan
    /// is what bounds the wall: a boundary reached with the plan's widest legal gap already spent is asked
    /// even though no mark sits on it, so no loop and no released freeze can hold the wall still forever.
    /// </summary>
    public void Tick()
    {
        // The Switcher performs the plan it holds against that plan's own player — never the focus
        // player, which it does not know about. PlayerNumber is 1-based.
        var slot = sheet.PlayerNumber - 1;
        var players = controller.beatManager.Players;
        if (slot < 0 || slot >= players.Count || players[slot].Beat is not { } beat)
        {
            return;
        }

        if (beat == actedBeat)
        {
            return;
        }

        actedBeat = beat;

        // A Grid Boundary is the Grid lane returning to one — phrase-relative, so a shortened phrase restarts
        // it early and the count follows the music. Without that lane there is no way to know where boundaries
        // fall, so the count simply never advances and only the plan's own marks perform.
        var onBoundary = players[slot].GridBeat == 1;
        if (onBoundary)
        {
            boundariesSinceImpact++;
        }

        foreach (var mark in sheet.Marks)
        {
            if (beat != mark.Beat - transitions[mark.TransitionIndex].Repertoire.RunwayBeats)
            {
                continue;
            }

            // A fired mark's Impact Point would land on the next boundary, one Grid further out than the
            // boundaries counted so far.
            var cue = mark.Fired
                ? AskOffPlan(mark.Beat, boundariesSinceImpact + 1)
                : director.DecideCue(mark);
            if (!cue.Perform)
            {
                // Held, or a boundary the Director chose to ride through. Either way the wall stays put, and a
                // mark that never fires simply does not happen — the plan says what to perform, not what must
                // be on the wall at a beat.
                return;
            }

            mark.Fired = true;
            Perform(beat, cue);
            return;
        }

        // Nothing in the plan fires on this beat. Once the count reaches the ceiling the plan has demonstrably
        // failed to feed the playhead, so the boundary is asked anyway; the deal is certain at that point, which
        // is what makes the wall holding still past TrackCueSheet.MaximumGapBeats impossible. Below the ceiling
        // nothing is asked, so an off-plan cue can never pre-empt a plan the playhead is still walking through.
        if (!onBoundary || boundariesSinceImpact < TrackCueSheet.MaximumGapGrids)
        {
            return;
        }

        // Standing on the boundary that spent the last legal Grid, so performing now is the ceiling gap itself.
        var offPlan = AskOffPlan(beat, boundariesSinceImpact);
        if (offPlan.Perform)
        {
            Perform(beat, offPlan);
        }
    }

    /// <summary>
    /// Asks the Director what to do at a Grid Boundary the plan cannot cover, counting the ask so a loop
    /// re-crossing one boundary is never handed the same card twice.
    /// </summary>
    /// <param name="boundaryBeat">Absolute Grid Boundary beat being asked about.</param>
    /// <param name="gapGrids">The gap in Grids that performing here would produce.</param>
    private CueDecision AskOffPlan(int boundaryBeat, int gapGrids)
    {
        offPlanAsks++;
        return director.DecideOffPlanCue(boundaryBeat, gapGrids, offPlanAsks);
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
    /// Performs one cue. It is fired on its Runway beat, so the Transition starts now, its Impact Point lands a
    /// Runway later, and its Tail resolves after that. The Impact Point is derived from the Transition the
    /// Director actually dealt, so an override or an off-plan card lands where its own Runway puts it rather
    /// than where the plan assumed.
    /// </summary>
    /// <param name="fireBeat">The beat this cue leaves on — one Runway before its Impact Point.</param>
    /// <param name="cue">What the Director said to play.</param>
    private void Perform(int fireBeat, CueDecision cue)
    {
        var repertoire = transitions[cue.TransitionIndex].Repertoire;
        var nowSeconds = Time.time;

        // The wall is changing, so the spacing rule starts counting again from this Impact Point.
        boundariesSinceImpact = 0;

        StartTransition(
            cue.EffectIndex,
            cue.TransitionIndex,
            TransitionStartTiming.FromBeatClock(nowSeconds, SecondsPerBeat()),
            nowSeconds,
            repertoire);
        // The Switcher owns what is on stage, so it owns the Controller's transition mirror.
        controller.currentTransition = cue.TransitionIndex;
        Trace(() => $"SWITCHER_PERFORM impact={fireBeat + repertoire.RunwayBeats} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.EffectIndex)}");
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
