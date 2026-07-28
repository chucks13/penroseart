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

    /// <summary>Dealt fresh because a loop re-crossed a mark that had already fired (never replayed).</summary>
    LoopRedeal,

    /// <summary>Dealt because the wall reached the four-Grid stillness ceiling with nothing planned to perform.</summary>
    Ceiling,
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
        -1);

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
    /// Transition runs), so a glance at the HUD after the wall changes still finds the reason. Reset to
    /// <see cref="CueSource.Plan"/> on handover — a badge never outlives the plan it explains.
    /// </summary>
    public readonly CueSource LastCueSource;

    /// <summary>The mark beat the most recent synced cue anchored to, or -1 before any cue has performed.</summary>
    public readonly int LastCueMarkBeat;

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
    /// <param name="lastCueMarkBeat">Mark beat the most recent synced cue anchored to, or -1 before any.</param>
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
        int lastCueMarkBeat)
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
    }

    /// <summary>Whether anything is on stage yet: an Effect is showing, or a Transition is running.</summary>
    public bool Ready => CurrentEffectIndex >= 0 || CurrentTransitionIndex >= 0;

    /// <summary>What to call whatever owns the frame — the running Transition, else the Effect on stage.</summary>
    public string StageName => CurrentTransitionIndex >= 0 ? CurrentTransitionName : CurrentEffectName;
}

/// <summary>
/// Mechanical stage switcher for Penrose performers. It executes the Cue Sheet handed over by the
/// Director (ADR-0009): each tick it reads BeatManager directly for the sheet player's beat and Grid lanes,
/// asks about the Cue Mark whose Runway begins on that beat, and fires the answer the same frame so the
/// Transition's Impact Point lands on the mark. A mark whose Runway has already gone by is missed, not
/// performed late, and nothing checks it off. A check-off is permanent, so no re-crossing — loop, back-cue,
/// or needle-drop — ever re-fires a mark; re-crossing one asks for an off-plan cue instead. It owns all
/// Runway/Impact/Tail timing and selects nothing — every decision is asked of the bound
/// <see cref="Director"/>. It holds no cue between beats and no lifecycle around one: no Standby Cue, no
/// Lock Point, no verdict, no revocation window.
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
    /// <see cref="Director.DecideCue"/>, <see cref="Director.DecideOffPlanCue"/>, and
    /// <see cref="Director.PeekTransitionIndex"/>. Bound once at startup.
    /// </summary>
    private Director director;

    /// <summary>
    /// The plan in force. Each mark records the beat it fired on, so the Switcher keeps no execution state
    /// beside the sheet: firing a cue marks the cue.
    /// </summary>
    private TrackCueSheet sheet;

    /// <summary>
    /// The beat <see cref="Tick"/> last acted on. Tick runs every frame and a beat spans many of them, so this
    /// is what makes "fire on the Runway beat" happen once rather than once per frame for the length of it.
    /// </summary>
    private int? actedBeat;

    /// <summary>
    /// Grid Boundaries of stillness — how many the wall has crossed since the last cue's mark. Anchored at
    /// the Cue Mark: <see cref="Perform"/> seeds the count so the in-flight cue's own landing
    /// crossing spends the seed instead of counting as stillness, which is what keeps the ceiling a full
    /// four Grids and a legal 64-beat plan gap un-pre-empted. This is the run-time backstop, and a separate
    /// rule from the plan-time one: the Director never builds a gap wider than
    /// <see cref="TrackCueSheet.MaximumGapBeats"/>, but a jump can leave a Missed Cue behind, and a DJ
    /// looping a stretch the plan left empty, or an inspection freeze ending, can still leave the playhead
    /// with nothing to perform. Reaching the ceiling with nothing performed means the plan cannot feed the
    /// playhead, and the boundary is asked anyway. Counted in boundaries rather than beats because a loop
    /// re-crosses the same beat numbers — only crossings measure elapsed music.
    /// </summary>
    private int boundariesSinceCue;

    /// <summary>
    /// How many off-plan asks this handover has made, the seed dimension that stops a loop re-crossing one
    /// boundary from being handed the same card twice. Handover-scoped like <see cref="boundariesSinceCue"/>:
    /// both reset on <see cref="Cast"/>, so no spacing or deal history leaks from one plan into the next.
    /// </summary>
    private int offPlanAsks;

    /// <summary>
    /// Whether the playhead is inside the final Grid before the stillness ceiling with the closing deal
    /// already certain. Armed on the Grid Boundary crossing that leaves exactly one legal Grid of stillness
    /// (<see cref="TrackCueSheet.MaximumGapGrids"/> - 1 counted crossings) when no unfired plan mark can
    /// still feed that Grid — the decision to act is taken once, at the Grid's start. Nothing is dealt and
    /// no one-shot override is consumed at arm time; the flag only remembers that this Grid must end with a
    /// cue. Cleared by every performed cue, by <see cref="Cast"/>, and at the ceiling boundary itself where
    /// the at-boundary backstop takes over.
    /// </summary>
    private bool ceilingArmed;

    /// <summary>
    /// Provenance of the most recent synced cue, held for <see cref="Status"/> so the Live strip can badge a
    /// wall change the plan did not call for. Sticky until the next cue or handover, not until the
    /// Transition completes — the explanation outlives the move it explains.
    /// </summary>
    private CueSource lastCueSource = CueSource.Plan;

    /// <summary>Mark beat the most recent synced cue anchored to, or -1 before any cue has performed.</summary>
    private int lastCueMarkBeat = -1;

    /// <summary>
    /// The Cue Sheet in force — the plan this Switcher is performing. A default sheet
    /// (<see cref="TrackCueSheet.StructureGeneration"/> of zero) means no plan is in force.
    /// </summary>
    public TrackCueSheet Sheet => sheet;

    /// <summary>Currently active effect index, or -1 while a transition owns the frame.</summary>
    public int CurrentEffectIndex => isTransitioning ? -1 : currentEffectIndex;

    /// <summary>The destination effect while transitioning, otherwise the currently active effect.</summary>
    public int TransitionTargetEffectIndex => isTransitioning ? transitions[currentTransitionIndex].B : currentEffectIndex;

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
        boundariesSinceCue = 0;
        offPlanAsks = 0;
        ceilingArmed = false;
        lastCueSource = CueSource.Plan;
        lastCueMarkBeat = -1;
        Trace(() => sheet.StructureGeneration > 0
            ? $"SWITCHER_CAST player={sheet.PlayerNumber} generation={sheet.StructureGeneration} marks={sheet.Marks.Count}"
            : "SWITCHER_CAST_CLEARED plan=<none>");
    }

    /// <summary>
    /// Follows the sheet player's beat and fires each cue on its Runway start — the beat a Transition has to
    /// leave on for its Impact Point to land on the mark. Which Runway that is belongs to the Transition that
    /// will actually fly, not to the plan's baked card, so the Director is asked first
    /// (<see cref="Director.PeekTransitionIndex"/>) and a staged override still lands its Impact on the mark.
    /// Called by the Controller each frame after <see cref="Director.Tick"/>, so a handover always precedes
    /// execution in the same frame. A mark the playhead reaches again has already fired, which only happens
    /// when the DJ loops, so the Director is asked for a fresh cue rather than replaying the spent one.
    /// Counting Grid Boundaries alongside the plan is what bounds the wall: a boundary reached with the plan's
    /// widest legal gap already spent is asked even though no mark sits on it, so no loop and no released
    /// freeze can hold the wall still forever.
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

        var firstActSinceHandover = actedBeat == null;
        actedBeat = beat;

        // A Grid Boundary is the Grid lane returning to one — phrase-relative, so a shortened phrase restarts
        // it early and the count follows the music. Without that lane there is no way to know where boundaries
        // fall, so the count simply never advances and only the plan's own marks perform. The handover beat is
        // the start line of the new plan's stillness, not elapsed music, so a handover landing on a boundary
        // does not count that crossing.
        var onBoundary = players[slot].GridBeat == 1;
        if (onBoundary && !firstActSinceHandover)
        {
            boundariesSinceCue++;
        }

        foreach (var mark in sheet.Marks)
        {
            var spent = mark.Fired;
            CueDecision cue;
            if (spent)
            {
                // A spent cue is reachable again only by re-crossing the very beat it left on — a loop, a
                // back-cue, a needle-drop. The beat it left on is the fact, not the beat the plan would have
                // chosen: an override may have flown it on a different Runway. Its Impact Point would land on
                // the next boundary, one Grid further out than the boundaries counted so far.
                if (beat != mark.FiredAtBeat)
                {
                    continue;
                }

                // The count is floored at zero: its -1 seed only exists to absorb the landing crossing, and a loop
                // that turned back before the mark's boundary simply never spent it.
                // The extra Grid is the landing still ahead of this fire beat — a runway-zero cue fired on
                // its own boundary has none, and that crossing was already counted this very tick.
                cue = AskOffPlan(mark.Beat, Math.Max(boundariesSinceCue, 0) + (mark.Beat > mark.FiredAtBeat ? 1 : 0));
            }
            else
            {
                // Count the Runway back from the Impact Point using the Transition that would actually perform
                // this mark, so an override with its own Runway leaves on its own beat and still lands on the
                // mark. Asking is free: the peek never spends the one-shot, only DecideCue below does.
                if (beat != mark.Beat - transitions[director.PeekTransitionIndex(mark)].Repertoire.RunwayBeats)
                {
                    continue;
                }

                cue = director.DecideCue(mark);
            }

            if (!cue.Perform)
            {
                // Held, or a boundary the Director chose to ride through. Either way the wall stays put, and a
                // mark that never fires simply does not happen — the plan says what to perform, not what must
                // be on the wall at a beat.
                return;
            }

            if (!spent)
            {
                mark.FiredAtBeat = beat;
            }

            Perform(beat, mark.Beat, cue, spent ? CueSource.LoopRedeal : CueSource.Plan);
            return;
        }

        // Nothing in the plan fires on this beat. One Grid short of the ceiling the next boundary's deal is
        // already certain, so the decision to act is taken here, at the Grid's start — armed once, not
        // re-judged every beat. The plan is consulted exactly once: an unfired mark that can still fire by
        // this Grid's closing boundary keeps the plan in charge and the ceiling quiet.
        if (onBoundary && boundariesSinceCue == TrackCueSheet.MaximumGapGrids - 1
            && !PlanFeedsPlayheadBy(beat, beat + TrackCueSheet.GridBeats))
        {
            ceilingArmed = true;
        }

        // An armed ceiling fires mid-Grid, early enough that the Transition flies its whole Runway and its
        // Impact lands on the next boundary — exactly like a planned mark. Firing at the boundary itself
        // held the wall still for the whole gap plus the Transition's own length (2026-07-28 live). The
        // boundary is projected from the Grid lane, not tracked as an absolute beat, so a loop pass that
        // re-walks the final Grid fires on the same lane and lands on the same boundary it re-approaches.
        // The peek spends nothing; the commit deals the same card because both are seeded by the boundary
        // and the ask alone.
        if (ceilingArmed && players[slot].GridBeat is { } gridBeat && gridBeat != 1)
        {
            var boundaryBeat = beat + (TrackCueSheet.GridBeats - gridBeat + 1);
            var peekedTransition = director.PeekOffPlanTransitionIndex(
                boundaryBeat, boundariesSinceCue + 1, offPlanAsks + 1);
            if (peekedTransition >= 0
                && boundaryBeat - beat == transitions[peekedTransition].Repertoire.RunwayBeats)
            {
                var cue = AskOffPlan(boundaryBeat, boundariesSinceCue + 1);
                if (cue.Perform)
                {
                    Perform(beat, boundaryBeat, cue, CueSource.Ceiling);
                }

                return;
            }
        }

        // At the boundary itself the ceiling is the backstop for everything the armed lane cannot fly: a
        // zero-Runway card, a boundary arriving early off a shortened phrase, a Hold covering the fire
        // lane, or a jump that skipped it. The deal is certain at the ceiling, which is what makes the
        // wall holding still past TrackCueSheet.MaximumGapBeats impossible. Below the ceiling nothing is
        // asked, so an off-plan cue can never pre-empt a plan the playhead is still walking through.
        if (!onBoundary || boundariesSinceCue < TrackCueSheet.MaximumGapGrids)
        {
            return;
        }

        // Standing on the boundary that spent the last legal Grid: the armed lane is over, and performing
        // now is the ceiling gap itself.
        ceilingArmed = false;
        var offPlanCue = AskOffPlan(beat, boundariesSinceCue);
        if (offPlanCue.Perform)
        {
            Perform(beat, beat, offPlanCue, CueSource.Ceiling);
        }
    }

    /// <summary>
    /// Whether any unfired mark can still fire after <paramref name="beat"/> and at or before
    /// <paramref name="boundaryBeat"/> — the guard that keeps a foreseeable ceiling cue from pre-empting a
    /// plan that is about to feed the playhead itself. Counts each mark's Runway back from its landing with
    /// the Transition that would actually perform it, exactly as the planned-mark path does.
    /// </summary>
    private bool PlanFeedsPlayheadBy(int beat, int boundaryBeat)
    {
        foreach (var mark in sheet.Marks)
        {
            if (mark.Fired)
            {
                continue;
            }

            var fireBeat = mark.Beat - transitions[director.PeekTransitionIndex(mark)].Repertoire.RunwayBeats;
            if (fireBeat > beat && fireBeat <= boundaryBeat)
            {
                return true;
            }
        }

        return false;
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
    /// Performs one cue. It is fired on its Runway beat, so the Transition starts now and reaches its mark
    /// one Runway later, resolving its Tail after that. The Runway is the dealt Transition's own, so an
    /// override or an off-plan card reaches the mark on its own timing rather than the plan's baked card's.
    /// </summary>
    /// <param name="fireBeat">The beat this cue leaves on — one Runway before its mark.</param>
    /// <param name="markBeat">
    /// The beat this cue is anchored to — the stillness anchor: its Cue Mark, or for an
    /// off-plan deal the Grid Boundary the ask borrowed as its mark. Equals <paramref name="fireBeat"/>
    /// when the cue fires on the very boundary it was asked about, whose landing then trails past it.
    /// </param>
    /// <param name="cue">What the Director said to play.</param>
    /// <param name="source">Where this cue came from: the sheet's mark, a loop re-deal, or the ceiling.</param>
    private void Perform(int fireBeat, int markBeat, CueDecision cue, CueSource source)
    {
        var repertoire = transitions[cue.TransitionIndex].Repertoire;
        var nowSeconds = Time.time;
        lastCueSource = source;
        lastCueMarkBeat = markBeat;

        // Stillness anchors at the mark, not at the fire. When the fire leads its mark, the
        // landing boundary still ahead belongs to this cue: the count starts one short so that crossing
        // brings it to zero. A cue fired on the boundary it was asked about has no landing left to absorb.
        boundariesSinceCue = markBeat > fireBeat ? -1 : 0;
        // Any performed cue, whatever its source, ends the stillness the ceiling was armed against.
        ceilingArmed = false;

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
        Trace(() => $"SWITCHER_PERFORM mark={markBeat} source={source} fire={fireBeat} runway={repertoire.RunwayBeats} tail={repertoire.TailBeats} transition={FormatTransition(cue.TransitionIndex)} target={FormatEffect(cue.EffectIndex)}");
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
                lastCueMarkBeat);
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
            lastCueMarkBeat);
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
