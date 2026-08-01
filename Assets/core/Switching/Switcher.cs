using System;
using UnityEngine;

/// <summary>
/// Where a synced cue came from. Everything except <see cref="Plan"/> is the Switcher covering for a plan
/// the playhead escaped, so HUDs badge those and stay silent for the sheet's own marks.
/// </summary>
public enum CueSource
{
    /// <summary>Performed from the Cue Sheet's own mark — the ordinary case, shown without comment.</summary>
    Plan,

    /// <summary>
    /// Dealt through the anomaly doorway — a re-crossed fired mark, a self-blend mark, or Stillness up —
    /// and scheduled toward the boundary closing the Grid where the sighting was reported.
    /// </summary>
    OffPlan,
}

/// <summary>
/// Read-only snapshot of the Mechanical Switcher's current stage state for HUDs and inspectors.
/// </summary>
public readonly struct SwitcherStatus
{
    /// <summary>The snapshot shown before the Switcher has anything on stage: no Effect, no Transition, no progress.</summary>
    public static SwitcherStatus NotReady { get; } = new SwitcherStatus(
        -1,
        string.Empty,
        string.Empty,
        -1,
        string.Empty,
        -1,
        string.Empty,
        0f,
        CueSource.Plan,
        -1,
        null,
        default);

    /// <summary>Index of the Effect on stage, or -1 while a Transition owns the frame.</summary>
    public readonly int CurrentEffectIndex;

    /// <summary>Display name of the Effect on stage, or empty while a Transition owns the frame.</summary>
    public readonly string CurrentEffectName;

    /// <summary>Display name of the Effect a running Transition is moving away from.</summary>
    public readonly string SourceEffectName;

    /// <summary>Index of the Effect the stage is heading to, whether or not a Transition owns the frame.</summary>
    public readonly int TargetEffectIndex;

    /// <summary>Display name of the Effect the stage is heading to.</summary>
    public readonly string TargetEffectName;

    /// <summary>Index of the running Transition, or -1 when an Effect owns the frame.</summary>
    public readonly int CurrentTransitionIndex;

    /// <summary>Display name of the running Transition, or empty when an Effect owns the frame.</summary>
    public readonly string CurrentTransitionName;

    /// <summary>How far the running Transition has travelled, in 0-to-1; zero when none is running.</summary>
    public readonly float TransitionProgress;

    /// <summary>
    /// Where the most recent synced cue came from. Sticky until the next cue performs (not just while its
    /// Transition runs), so a glance at the HUD after the wall changes still finds the reason.
    /// </summary>
    public readonly CueSource LastCueSource;

    /// <summary>The Grid Boundary beat of the most recent synced cue, or -1 before any cue has performed.</summary>
    public readonly int LastCueMarkBeat;

    /// <summary>
    /// The last question asked through the anomaly doorway, or null before any ask. Last value only —
    /// history stays in the Cue Log and traces.
    /// </summary>
    public readonly OffPlanSighting? LastOffPlanSighting;

    /// <summary>
    /// The Director's answer to <see cref="LastOffPlanSighting"/> — a no-perform is a ride-through.
    /// Meaningless while <see cref="LastOffPlanSighting"/> is null.
    /// </summary>
    public readonly CueDecision LastOffPlanAnswer;

    /// <summary>Captures one stage snapshot.</summary>
    /// <param name="currentEffectIndex">Effect on stage, or -1 while a Transition owns the frame.</param>
    /// <param name="currentEffectName">Display name of the Effect on stage.</param>
    /// <param name="sourceEffectName">Display name of the Effect a running Transition is leaving.</param>
    /// <param name="targetEffectIndex">Effect the stage is heading to.</param>
    /// <param name="targetEffectName">Display name of the Effect the stage is heading to.</param>
    /// <param name="currentTransitionIndex">Running Transition, or -1 when an Effect owns the frame.</param>
    /// <param name="currentTransitionName">Display name of the running Transition.</param>
    /// <param name="transitionProgress">Transition progress, clamped to 0-to-1.</param>
    /// <param name="lastCueSource">Where the most recent synced cue came from.</param>
    /// <param name="lastCueMarkBeat">Grid Boundary beat of the most recent synced Cue, or -1 before any.</param>
    /// <param name="lastOffPlanSighting">Last question asked through the anomaly doorway, or null before any ask.</param>
    /// <param name="lastOffPlanAnswer">The Director's answer to that question; a no-perform is a ride-through.</param>
    public SwitcherStatus(
        int currentEffectIndex,
        string currentEffectName,
        string sourceEffectName,
        int targetEffectIndex,
        string targetEffectName,
        int currentTransitionIndex,
        string currentTransitionName,
        float transitionProgress,
        CueSource lastCueSource,
        int lastCueMarkBeat,
        OffPlanSighting? lastOffPlanSighting,
        CueDecision lastOffPlanAnswer)
    {
        CurrentEffectIndex = currentEffectIndex;
        CurrentEffectName = currentEffectName;
        SourceEffectName = sourceEffectName;
        TargetEffectIndex = targetEffectIndex;
        TargetEffectName = targetEffectName;
        CurrentTransitionIndex = currentTransitionIndex;
        CurrentTransitionName = currentTransitionName;
        TransitionProgress = Mathf.Clamp01(transitionProgress);
        LastCueSource = lastCueSource;
        LastCueMarkBeat = lastCueMarkBeat;
        LastOffPlanSighting = lastOffPlanSighting;
        LastOffPlanAnswer = lastOffPlanAnswer;
    }

    /// <summary>Whether anything is on stage yet: an Effect is showing, or a Transition is running.</summary>
    public bool Ready => CurrentEffectIndex >= 0 || CurrentTransitionIndex >= 0;

    /// <summary>What to call whatever owns the frame — the running Transition, else the Effect on stage.</summary>
    public string StageName => CurrentTransitionIndex >= 0 ? CurrentTransitionName : CurrentEffectName;
}

/// <summary>
/// Mechanical stage switcher for Penrose performers. It executes the Cue Sheet handed over by the
/// Director (ADR-0009) and thinks once per Grid, at the Grid's start, from the on-air BeatManager
/// surface — the on-air grid is the timing authority, and the Switcher knows no player number at
/// execution time. The think gives the closing boundary's unfired mark priority, then routes a fired
/// mark, self-blend, or Stillness through one doorway to the bound <see cref="Director"/>. Starting the
/// blend at boundary-minus-Runway is a mechanical scheduled act of that decision, not a fresh decision.
/// A mark whose fire beat the playhead never lands on is a Missed Cue: not performed, not spent, never
/// fired late. A handover changes nothing on the wall by
/// itself and resets nothing. It owns all Runway/Impact/Tail timing and selects nothing — every
/// decision is asked of the Director. It holds no cue lifecycle: no Standby Cue, no Lock Point, no
/// verdict, no revocation window.
/// </summary>
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

    /// <summary>
    /// The one decider (ADR-0009): commands come down (the immediate and Standalone
    /// <see cref="StartTransition(int, int, float)"/> pushes, the sheet handover); questions go up through
    /// <see cref="Director.DecideCue"/> and <see cref="Director.DecideOffPlanCue"/>. Bound once at startup.
    /// </summary>
    private Director director;

    /// <summary>
    /// The plan in force. Each mark records the beat it fired on, so firing a cue marks the cue; what the
    /// Switcher keeps beside the sheet is the wall's own state — Stillness, the Grid's scheduled act, the
    /// last-observed beat, the ask counter, and the last cue's provenance badge.
    /// </summary>
    private TrackCueSheet sheet;

    /// <summary>
    /// The on-air beat <see cref="Tick"/> last acted on. Tick runs every frame and a beat spans many of
    /// them, so this is what makes each beat observed once. A beat snap-back is logged as a discontinuity;
    /// the wire loop flag may corroborate traces but never selects behavior.
    /// </summary>
    private int? lastSeenBeat;

    /// <summary>
    /// Grid position at the last observed beat — the same watch-for-change pattern as
    /// <see cref="lastSeenBeat"/>. A Grid start is the position going down between two observed beats,
    /// never a frame happening to sample exactly 1.
    /// </summary>
    private int? lastSeenGridBeat;

    /// <summary>
    /// Whether a think has established the Stillness baseline. The first think observes a Grid start with
    /// no whole Grid behind it, so it counts nothing; every later think counts the Grid it closes.
    /// </summary>
    private bool hasBaseline;

    /// <summary>
    /// Stillness: whole Grids the wall has sat through since the last fired cue. A property of the wall,
    /// not of any sheet — it survives handovers — and it is checked at every Grid start: three still
    /// Grids since the last fire make the fourth Grid ask.
    /// </summary>
    private int stillGrids;

    /// <summary>Whether a cue fired since the last think — the fact the next think's Stillness update consumes.</summary>
    private bool firedSinceThink;

    /// <summary>
    /// The Cue Sheet mark behind the scheduled act, or null when the act is Off-Plan.
    /// Meaningless while <see cref="scheduledFireBeat"/> is null.
    /// </summary>
    private CuePlanMark scheduledMark;

    /// <summary>The Director's answer for the scheduled act.</summary>
    private CueDecision scheduledCue;

    /// <summary>The Grid Boundary beat where the scheduled act lands.</summary>
    private int scheduledBoundaryBeat;

    /// <summary>The beat the scheduled act leaves on, or null when no act is scheduled.</summary>
    private int? scheduledFireBeat;

    /// <summary>
    /// How many off-plan asks this run has made — the seed dimension that stops a loop re-crossing one
    /// boundary from being handed the same card twice. Never reset: a handover resets nothing.
    /// </summary>
    private int offPlanAsks;

    /// <summary>
    /// The last question asked through the anomaly doorway, or null before any ask. Kept only so the
    /// status snapshot can show an operator why the wall last changed — or rode through — off plan.
    /// </summary>
    private OffPlanSighting? lastOffPlanSighting;

    /// <summary>The Director's answer to <see cref="lastOffPlanSighting"/>; meaningless while that is null.</summary>
    private CueDecision lastOffPlanAnswer;

    /// <summary>
    /// Provenance of the most recent synced cue, held for <see cref="Status"/> so the Live strip can badge a
    /// wall change the plan did not call for. Sticky until the next cue, not until the Transition
    /// completes — the explanation outlives the move it explains.
    /// </summary>
    private CueSource lastCueSource = CueSource.Plan;

    /// <summary>Grid Boundary beat of the most recent synced Cue, or -1 before any Cue has performed.</summary>
    private int lastCueBoundaryBeat = -1;

    /// <summary>
    /// The Cue Sheet in force — the plan this Switcher is performing. A default sheet
    /// (<see cref="TrackCueSheet.StructureGeneration"/> of zero) means no plan is in force.
    /// </summary>
    public TrackCueSheet Sheet => sheet;

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

    /// <summary>The Effect still showing on the wall: the source while a Transition owns the frame, otherwise the currently active effect.</summary>
    public int TransitionSourceEffectIndex => isTransitioning ? transitions[currentTransitionIndex].A : currentEffectIndex;

    /// <summary>Current read-only mechanical stage snapshot for runtime HUDs and inspector diagnostics.</summary>
    public SwitcherStatus Status => BuildStatus();

    /// <summary>Binds the Switcher to the runtime hub and the two performer catalogs it renders from.</summary>
    /// <param name="controller">The runtime hub owning the beat clock, the trace sink, and the transition mirror.</param>
    /// <param name="effects">The Effect catalog; catalog position is Effect identity throughout.</param>
    /// <param name="transitions">The Transition catalog; catalog position is Transition identity throughout.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
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
    /// because the reference is genuinely mutual: the Director also pushes its immediate and Standalone
    /// <see cref="StartTransition(int, int, float)"/> commands down into the Switcher.
    /// </summary>
    /// <param name="director">The one decider every question goes up to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="director"/> is null.</exception>
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
    /// <see cref="Tick"/> performs the marks. A handover changes nothing on the wall by itself and resets
    /// nothing of the wall's own: Stillness, the check-offs, and any Transition already in flight all
    /// stand, and the next change comes at a mark or at the Stillness deadline. An unstarted scheduled
    /// act is the outgoing plan's decision, not the wall's, so it leaves with its sheet — only a started
    /// Transition is fire-and-forget. Idempotent on the sheet's
    /// (<see cref="TrackCueSheet.PlayerNumber"/>, <see cref="TrackCueSheet.StructureGeneration"/>)
    /// identity, so the Director calls it every synced tick and keeps zero handover state. <c>Cast(default)</c>
    /// clears the plan (generation 0, player 0) and is how Standalone Mode turns sheet execution off.
    /// </summary>
    public void Cast(TrackCueSheet sheet)
    {
        if (sheet.PlayerNumber == this.sheet.PlayerNumber
            && sheet.StructureGeneration == this.sheet.StructureGeneration)
        {
            return;
        }

        this.sheet = sheet;
        ClearScheduledAct();
        Trace(() => sheet.StructureGeneration > 0
            ? $"SWITCHER_CAST player={sheet.PlayerNumber} generation={sheet.StructureGeneration} marks={sheet.Marks.Count}"
            : "SWITCHER_CAST_CLEARED plan=<none>");
    }

    /// <summary>
    /// Executes the plan against the on-air surface. Called by the Controller each frame after
    /// <see cref="Director.Tick"/>, so a handover always precedes execution in the same frame. Every frame
    /// observes the current Grid, so a Grid start that arrives after the absolute beat update still causes
    /// one think. New absolute beats fire or lapse scheduled acts; a loop snap-back before a fire beat
    /// keeps the act for the re-walked pass.
    /// </summary>
    public void Tick()
    {
        // The on-air grid is the timing authority — anything the DJ does is represented in it — and the
        // Switcher knows no player number at execution time.
        if (controller.beatManager.Timing.Beat is not { } beat
            || controller.beatManager.Grid.Beat is not { } gridBeat)
        {
            return;
        }

        var isNewBeat = beat != lastSeenBeat;

        // A Grid start is a decrease in its current position. Holding at One counts only when the
        // absolute beat advances, which preserves consecutive one-beat Grids without thinking twice
        // when repeated frames carry the same state.
        var crossed = lastSeenGridBeat is { } lastGrid
            ? gridBeat < lastGrid || (isNewBeat && gridBeat == 1 && lastGrid == 1)
            : gridBeat == 1;
        lastSeenGridBeat = gridBeat;

        if (!isNewBeat)
        {
            if (crossed)
            {
                Think(beat, gridBeat);
            }

            return;
        }

        if (lastSeenBeat is { } previousBeat && beat != previousBeat + 1)
        {
            // A discontinuity is the wire's story: a snap-back is a loop, a forward skip is frames
            // outrunning beats or a needle-drop. The Grid lane rides along so a lagging grid datagram
            // at the jump is visible in the log instead of inferred from silence.
            Trace(() => $"SWITCHER_BEAT_JUMP from={previousBeat} to={beat} gridBeat={gridBeat}");
        }

        lastSeenBeat = beat;

        if (scheduledFireBeat is { } fireBeat && beat == fireBeat)
        {
            var mark = scheduledMark;
            var cue = scheduledCue;
            var boundaryBeat = scheduledBoundaryBeat;
            var source = mark == null ? CueSource.OffPlan : CueSource.Plan;
            ClearScheduledAct();
            if (mark != null)
            {
                mark.FiredAtBeat = beat;
            }

            Perform(beat, boundaryBeat, cue, source);
        }
        else if (scheduledFireBeat is { } missedFireBeat && beat > missedFireBeat)
        {
            // The playhead escaped past the fire beat without landing on it — a forward jump. The act
            // lapses, and a plan mark remains unspent.
            var lapsedBoundaryBeat = scheduledBoundaryBeat;
            var lapsedSource = scheduledMark == null ? CueSource.OffPlan : CueSource.Plan;
            ClearScheduledAct();
            Trace(() => $"SWITCHER_LAPSE boundary={lapsedBoundaryBeat} fire={missedFireBeat} beat={beat} source={lapsedSource}");
        }

        if (crossed)
        {
            Think(beat, gridBeat);
        }
    }

    /// <summary>
    /// The once-per-Grid decision, taken at the Grid's start from on-air state — everything this Grid
    /// needs. Stillness is counted first. An unfired, non-self-blend mark on the closing boundary has
    /// priority and is scheduled at boundary-minus-Runway. Otherwise a fired mark, self-blend, or Stillness
    /// goes through <see cref="Director.DecideOffPlanCue"/> at most once, with a taken Cue scheduled toward
    /// that same closing boundary.
    /// </summary>
    /// <param name="beat">The on-air beat observed when this Grid starts.</param>
    /// <param name="gridBeat">The current one-based Grid position.</param>
    private void Think(int beat, int gridBeat)
    {
        var loopRolling = controller.beatManager.Loop.Rolling;
        if (!hasBaseline)
        {
            // The first think has no whole Grid behind it, so there is nothing to count yet.
            hasBaseline = true;
        }
        else if (firedSinceThink)
        {
            stillGrids = 0;
        }
        else
        {
            stillGrids++;
        }

        firedSinceThink = false;

        var candidate = NextBoundaryMark(beat);

        // A pending Off-Plan act belongs to the Grid that just closed, so it lapses before this Grid
        // decides. A plan act may survive a short Grid when the same closing mark is still ahead.
        if (scheduledFireBeat is { } pendingFireBeat)
        {
            if (scheduledMark == null)
            {
                var lapsedBoundaryBeat = scheduledBoundaryBeat;
                ClearScheduledAct();
                Trace(() => $"SWITCHER_LAPSE boundary={lapsedBoundaryBeat} fire={pendingFireBeat} beat={beat} source={CueSource.OffPlan}");
            }
            else if (!ReferenceEquals(scheduledMark, candidate))
            {
                var droppedMarkBeat = scheduledMark.Beat;
                Trace(() => $"SWITCHER_UNSCHEDULE mark={droppedMarkBeat} fire={pendingFireBeat} beat={beat}");
                ClearScheduledAct();
            }
        }

        // One line per think makes the log distinguish "no mark at this boundary" from "a mark passed
        // unseen" — without it a silent think and a skipped think read identically (2026-07-31 session).
        var seenCandidate = candidate;
        var scheduledKind = scheduledMark == null ? CueSource.OffPlan : CueSource.Plan;
        Trace(() => $"SWITCHER_THINK beat={beat} loopRolling={loopRolling} stillGrids={stillGrids} candidate={(seenCandidate == null ? "none" : seenCandidate.Fired ? $"{seenCandidate.Beat}:fired" : seenCandidate.Beat.ToString())} scheduled={(scheduledFireBeat == null ? "none" : $"{scheduledKind}:{scheduledBoundaryBeat}")} onWall={FormatEffect(TransitionSourceEffectIndex)} toward={FormatEffect(TransitionTargetEffectIndex)}");

        OffPlanAnomaly? anomaly = null;

        if (candidate != null && scheduledFireBeat == null)
        {
            if (candidate.Fired)
            {
                // A fired mark is permanently checked off and can only enter the doorway.
                anomaly = OffPlanAnomaly.FiredMark;
            }
            else if (candidate.EffectIndex == TransitionTargetEffectIndex)
            {
                // A mark that blends into the Effect already on the wall can only enter the doorway.
                anomaly = OffPlanAnomaly.SelfBlend;
            }
            else
            {
                var cue = director.DecideCue(candidate);
                if (!cue.Perform)
                {
                    // A refused answer is Hold (ADR-0009) — traced so a passed mark under a held wall
                    // reads as the operator's doing, not as a mark the Switcher lost.
                    Trace(() => $"SWITCHER_HOLD mark={candidate.Beat} beat={beat}");
                }
                else
                {
                    ScheduleAct(beat, candidate.Beat, cue, candidate);
                }
            }
        }

        // Three whole Grids since the last fire and nothing scheduled to feed this one: the fourth Grid
        // asks. A planned unfired mark always wins because it was scheduled above.
        if (anomaly == null
            && stillGrids >= TrackCueSheet.MaximumGapGrids - 1
            && scheduledFireBeat == null)
        {
            anomaly = OffPlanAnomaly.StillnessUp;
        }

        // Every anomaly goes through one doorway, at most once per think: the Switcher reports what it
        // saw and the Director decides — ride through, or a fresh Off-Plan Cue scheduled toward this
        // Grid's closing boundary. The anomaly kind is diagnostic only.
        if (anomaly is { } seen)
        {
            // The ask is counted here, before the value is built, so constructing a Sighting stays pure
            // data — the Stillness and ask counters never leave the Switcher; the Sighting carries snapshots.
            offPlanAsks++;
            var sighting = new OffPlanSighting(
                seen,
                beat,
                stillGrids + 1,
                offPlanAsks,
                TransitionSourceEffectIndex,
                TransitionTargetEffectIndex);
            var answer = director.DecideOffPlanCue(sighting);
            lastOffPlanSighting = sighting;
            lastOffPlanAnswer = answer;
            if (answer.Perform)
            {
                // TODO: TrackCueSheet does not expose the current Grid's closing boundary when no mark occupies it.
                var boundaryBeat = candidate?.Beat ?? beat + TrackCueSheet.GridBeats - gridBeat + 1;
                ScheduleAct(beat, boundaryBeat, answer, null);
            }
        }
    }

    /// <summary>
    /// The mark on this Grid's closing boundary, or null when that boundary carries none. Marks sit on
    /// Grid Boundaries, so the nearest mark within one nominal Grid of the start is the closing one.
    /// </summary>
    /// <param name="beat">The on-air beat the Grid starts on.</param>
    private CuePlanMark NextBoundaryMark(int beat)
    {
        if (sheet.Marks is not { } marks)
        {
            // No plan in force — a default sheet carries no mark list at all.
            return null;
        }

        CuePlanMark next = null;
        foreach (var mark in marks)
        {
            if (mark.Beat > beat && (next == null || mark.Beat < next.Beat))
            {
                next = mark;
            }
        }

        return next != null && next.Beat - beat <= TrackCueSheet.GridBeats ? next : null;
    }

    /// <summary>
    /// Turns one Director answer into the Grid's single act, or performs it now when its Runway starts
    /// on this think beat.
    /// </summary>
    /// <param name="thinkBeat">Absolute beat where the decision was made.</param>
    /// <param name="boundaryBeat">Grid Boundary beat where the Cue lands.</param>
    /// <param name="cue">Director answer to perform.</param>
    /// <param name="planMark">Cue Sheet mark to spend on fire, or null for an Off-Plan Cue.</param>
    private void ScheduleAct(
        int thinkBeat,
        int boundaryBeat,
        CueDecision cue,
        CuePlanMark planMark)
    {
        var source = planMark == null ? CueSource.OffPlan : CueSource.Plan;
        var fireBeat = boundaryBeat - transitions[cue.TransitionIndex].Repertoire.RunwayBeats;
        if (fireBeat == thinkBeat)
        {
            if (planMark != null)
            {
                planMark.FiredAtBeat = thinkBeat;
            }

            Perform(thinkBeat, boundaryBeat, cue, source);
            return;
        }

        if (fireBeat < thinkBeat)
        {
            Trace(() => $"SWITCHER_MISS boundary={boundaryBeat} fire={fireBeat} beat={thinkBeat} source={source}");
            return;
        }

        scheduledMark = planMark;
        scheduledCue = cue;
        scheduledBoundaryBeat = boundaryBeat;
        scheduledFireBeat = fireBeat;
        Trace(() => $"SWITCHER_SCHEDULE boundary={boundaryBeat} fire={fireBeat} source={source} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.EffectIndex)}");
    }

    /// <summary>Forgets the Grid's scheduled act.</summary>
    private void ClearScheduledAct()
    {
        scheduledMark = null;
        scheduledCue = default;
        scheduledBoundaryBeat = 0;
        scheduledFireBeat = null;
    }

    /// <summary>
    /// Starts or replaces a transition from the current stage destination to the target effect, running for the
    /// Transition's own default duration — the seconds-denominated move Standalone Mode and every immediate
    /// operator pick use. The Switcher owns progress and completion after this call; if another transition is
    /// still rendering, the previous destination becomes the source for this new last-command-wins move.
    /// </summary>
    /// <param name="targetEffectIndex">Effect catalog index the move lands on.</param>
    /// <param name="transitionIndex">Transition catalog index performing the move.</param>
    /// <param name="startTimeSeconds">Unity time the move is considered to have started.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either index is outside the runtime catalog.</exception>
    public void StartTransition(int targetEffectIndex, int transitionIndex, float startTimeSeconds)
    {
        ValidateTransitionIndex(transitionIndex);
        StartTransition(
            targetEffectIndex,
            transitionIndex,
            startTimeSeconds,
            transitions[transitionIndex].Repertoire.DefaultDurationSeconds,
            Time.time);
    }

    /// <summary>Starts one fully resolved transition and defers its diagnostic display reads.</summary>
    /// <param name="targetEffectIndex">Effect catalog index the move lands on.</param>
    /// <param name="transitionIndex">Transition catalog index performing the move.</param>
    /// <param name="startTimeSeconds">Unity time the move is considered to have started.</param>
    /// <param name="durationSeconds">
    /// How long the move runs. Resolved by the caller, because the two production paths denominate it
    /// differently: Standalone in the Transition's own seconds, a synced cue in beats off the live clock.
    /// </param>
    /// <param name="progressNowSeconds">Unity time to seed the first progress reading from.</param>
    private void StartTransition(
        int targetEffectIndex,
        int transitionIndex,
        float startTimeSeconds,
        float durationSeconds,
        float progressNowSeconds)
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
        transitionStartTime = startTimeSeconds;
        transitionDurationSeconds = durationSeconds;
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
    /// Performs one Cue at its Runway beat. The Transition starts now, reaches its Impact Point at the
    /// Grid Boundary one Runway later, then resolves its Tail. The Runway is always the decided
    /// Transition's own.
    /// </summary>
    /// <param name="fireBeat">The absolute beat where the Transition starts.</param>
    /// <param name="boundaryBeat">The Grid Boundary beat where the Cue lands.</param>
    /// <param name="cue">What the Director said to play.</param>
    /// <param name="source">Whether the Cue came from the plan or the anomaly doorway.</param>
    private void Perform(int fireBeat, int boundaryBeat, CueDecision cue, CueSource source)
    {
        var repertoire = transitions[cue.TransitionIndex].Repertoire;
        var nowSeconds = Time.time;
        lastCueSource = source;
        lastCueBoundaryBeat = boundaryBeat;

        // A performed cue is the only Stillness reset cause; the next think consumes this fact.
        firedSinceThink = true;

        // A synced cue is denominated in beats, so its seconds come off the live clock rather than the
        // Transition's authored default.
        StartTransition(
            cue.EffectIndex,
            cue.TransitionIndex,
            nowSeconds,
            repertoire.DurationBeats * SecondsPerBeat(),
            nowSeconds);
        // The Switcher owns what is on stage, so it owns the Controller's transition mirror.
        controller.currentTransition = cue.TransitionIndex;
        Trace(() => $"SWITCHER_PERFORM boundary={boundaryBeat} source={source} fire={fireBeat} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.EffectIndex)}");
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

    /// <summary>Builds the read-only stage snapshot from whichever performer owns the frame.</summary>
    private SwitcherStatus BuildStatus()
    {
        if (isTransitioning)
        {
            var transition = transitions[currentTransitionIndex];
            return new SwitcherStatus(
                -1,
                string.Empty,
                EffectName(transition.A),
                transition.B,
                EffectName(transition.B),
                currentTransitionIndex,
                transition.Name,
                transitionProgress,
                lastCueSource,
                lastCueBoundaryBeat,
                lastOffPlanSighting,
                lastOffPlanAnswer);
        }

        var currentName = EffectName(currentEffectIndex);
        return new SwitcherStatus(
            currentEffectIndex,
            currentName,
            currentName,
            currentEffectIndex,
            currentName,
            -1,
            string.Empty,
            0f,
            lastCueSource,
            lastCueBoundaryBeat,
            lastOffPlanSighting,
            lastOffPlanAnswer);
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
