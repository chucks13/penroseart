using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

// Execution-seam tests for the once-per-Grid Switcher. Scripted on-air beat timelines — steady
// advance, loop snap-back, forward jump, handover mid-still-spell, short Grids — drive execution
// with the Director bound, and assertions stay on wall-visible stage state.
public sealed class SwitcherExecutionTests
{
    /// <summary>One colour per Effect catalog position, shared by the test Effects and the test Transition.</summary>
    private static readonly Color[] EffectColors = { Color.red, Color.blue, Color.green };

    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;
    private TimedTransition transition;
    private TimedTransition cueTransition;
    private HardCutTransition hardCutTransition;

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    /// <summary>Builds the real Switcher/Director cycle required by sheet execution.</summary>
    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("SwitcherExecutionTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);

        transition = new TimedTransition();
        cueTransition = new TimedTransition(runwayBeats: 2, tailBeats: 2);
        hardCutTransition = new HardCutTransition();
        var effects = new EffectBase[]
        {
            new SolidEffect(EffectColors[0]),
            new SolidEffect(EffectColors[1]),
            new SolidEffect(EffectColors[2]),
        };
        var transitions = new TransitionBase[] { transition, hardCutTransition, cueTransition };
        controller.effects = effects;
        controller.transitions = transitions;
        transition.BindController(controller);
        transition.Init();
        hardCutTransition.BindController(controller);
        hardCutTransition.Init();
        cueTransition.BindController(controller);
        cueTransition.Init();
        controller.effectDeck = new[] { 0, 1, 2 };
        controller.transitionDeck = new[] { 0, 1 };
        controller.currentTransition = 0;
        controller.timer = new Timer(controller.effectTime, false);
        switcher = new Switcher(controller, effects, transitions);
        switcher.SetInitialEffect(0, 0);
        controller.switcher = switcher;
        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;
        // Pin the run salt (ADR-0008) so every dealt card is deterministic across test runs.
        director.SheetSalt = 0;
        switcher.BindDirector(director);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void RenderAtTimeAdvancesTransitionProgressFromItsStartTime()
    {
        switcher.StartTransition(1, 0, startTimeSeconds: 10f);

        var buffer = switcher.RenderAtTime(10.5f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(EffectColors[0], EffectColors[1], 0.5f)));
    }

    [Test]
    public void RenderPromotesDestinationAfterStartedTransitionDuration()
    {
        switcher.StartTransition(1, 0, startTimeSeconds: 10f);

        var buffer = switcher.RenderAtTime(11.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.CurrentTransitionName, Is.EqualTo(string.Empty));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(EffectColors[1]));
    }

    [Test]
    public void ZeroDurationTransitionPromotesDestinationImmediately()
    {
        switcher.StartTransition(1, 1, startTimeSeconds: 10f);

        var buffer = switcher.RenderAtTime(10f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(EffectColors[1]));
    }

    [Test]
    public void StartTransitionWhileRenderingReplacesMoveFromPreviousTarget()
    {
        switcher.StartTransition(1, 0, startTimeSeconds: 10f);
        switcher.RenderAtTime(10.25f, out _);

        switcher.StartTransition(0, 0, startTimeSeconds: 20f);
        var buffer = switcher.RenderAtTime(20.5f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(0));
        Assert.That(transition.A, Is.EqualTo(1), "The previous destination became this move's source.");
        Assert.That(transition.B, Is.EqualTo(0));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(EffectColors[1], EffectColors[0], 0.5f)));
    }

    #region Sheet execution

    /// <summary>
    /// Steady advance: the Grid-start think schedules the mark's blend, nothing happens before
    /// boundary-minus-Runway, the blend starts there whole, its Impact lands on the mark, and its Tail
    /// completes after — fire and forget.
    /// </summary>
    [Test]
    public void ACueBlendStartsAtBoundaryMinusRunwayAndLandsOnTheMark()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var repertoire = cueTransition.Repertoire;
        var fireBeat = mark.Beat - repertoire.RunwayBeats;
        switcher.Cast(sheet);

        for (var beat = 1; beat < fireBeat; beat++)
        {
            FeedSwitcherFrame(beat, phrases, generation: 1);
            Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1),
                $"Beat {beat}: nothing starts before boundary-minus-Runway.");
        }

        FeedSwitcherFrame(fireBeat, phrases, generation: 1);
        var fireTime = Time.time;
        Assert.That(mark.Fired, Is.True, "Firing the blend is what spends the mark.");
        Assert.That(mark.FiredAtBeat, Is.EqualTo(fireBeat), "The blend leaves at boundary-minus-Runway.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Runway is under way.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f),
            "An on-time fire runs the full Runway from zero.");

        switcher.RenderAtTime(fireTime + (repertoire.RunwayBeats * 0.5f), out _);
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(repertoire.ImpactPoint).Within(0.01f),
            "The Impact Point lands exactly on the Cue Mark beat.");

        // A hair past the Tail's end: at exactly runway-plus-tail seconds, float truncation on a large
        // Editor-uptime Time.time can leave progress fractionally under 1 and the assert flaky.
        var buffer = switcher.RenderAtTime(
            fireTime + ((repertoire.RunwayBeats + repertoire.TailBeats) * 0.5f) + 0.05f,
            out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "The Tail completes after the Impact.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(EffectColors[mark.EffectIndex]));
    }

    /// <summary>
    /// A timeline crossing short Grids: with a phrase ending off-cycle the Grid restarts early, the
    /// think follows the music, and every planned blend still leaves at its mark's boundary minus the
    /// decided Transition's Runway.
    /// </summary>
    [Test]
    public void EveryPlannedBlendStartsAtBoundaryMinusRunwayAcrossShortGrids()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 40, "intro"), Phrase(41, 168, "up") };
        // Two Effect cards: never-into-itself makes consecutive marks alternate targets, so a steady walk
        // can fire every mark as planned with no self-blend sighting reaching the doorway.
        var sheet = BuildExecutionSheet(phrases, generation: 1, effectCards: 2);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: the structure produces several marks.");
        switcher.SetInitialEffect(1 - sheet.Marks[0].EffectIndex, 0);
        switcher.Cast(sheet);

        for (var beat = 1; beat <= 168; beat++)
        {
            FeedSwitcherFrame(beat, phrases, generation: 1);
            switcher.RenderAtTime(1_000_000f, out _);
            Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.Plan),
                $"Beat {beat}: a steady walk over a legal plan never needs an off-plan cue.");
        }

        var runway = cueTransition.Repertoire.RunwayBeats;
        foreach (var mark in sheet.Marks)
        {
            Assert.That(mark.Fired, Is.True, $"The mark at {mark.Beat} fires on a steady walk.");
            Assert.That(mark.FiredAtBeat, Is.EqualTo(mark.Beat - runway),
                $"The mark at {mark.Beat} left at boundary-minus-Runway, short Grid or not.");
        }
    }

    /// <summary>
    /// A staged override is resolved at the think, so it flies its own Runway and still lands its Impact
    /// on the mark; the beat the plan's baked card would have left on passes without a second fire.
    /// </summary>
    [Test]
    public void AStagedOverrideIsDecidedAtTheThinkAndFliesItsOwnRunway()
    {
        // Baked card: the one-beat Runway. Override: the two-beat Runway, so the two leave on different beats.
        StageSheetCatalog(transition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var overrideIndex = TransitionIndex(cueTransition);
        var overrideRepertoire = cueTransition.Repertoire;
        Assert.That(overrideRepertoire.RunwayBeats, Is.Not.EqualTo(transition.Repertoire.RunwayBeats),
            "Setup: the override must differ from the baked card in its Runway.");
        switcher.Cast(sheet);
        director.SetNextTransition(overrideIndex);

        var thinkBeat = mark.Beat - TrackCueSheet.GridBeats;
        var overrideFireBeat = mark.Beat - overrideRepertoire.RunwayBeats;
        WalkSwitcher(thinkBeat, overrideFireBeat, phrases, generation: 1);
        var fireTime = Time.time;

        Assert.That(mark.Fired, Is.True, "The cue leaves on the decided override's Runway beat.");
        Assert.That(mark.FiredAtBeat, Is.EqualTo(overrideFireBeat));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(overrideIndex), "The override is what performs.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f),
            "The override flies its whole Runway rather than starting part-done.");

        switcher.RenderAtTime(fireTime + (overrideRepertoire.RunwayBeats * 0.5f), out _);
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(overrideRepertoire.ImpactPoint).Within(0.01f),
            "The Impact Point lands on the Cue Mark beat, not beside it.");

        // The beat the baked card would have left on, now behind the cue rather than ahead of it.
        FeedSwitcherFrame(mark.Beat - transition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(overrideIndex), "No second cue interrupted the first.");
    }

    /// <summary>
    /// Staged overrides apply from the next think onward: an override staged after this Grid's think does
    /// not touch the already-scheduled act — the plan's decided card flies — and fire-and-forget extends
    /// to the scheduled act.
    /// </summary>
    [Test]
    public void AnOverrideStagedAfterTheThinkWaitsForTheNextThink()
    {
        StageSheetCatalog(transition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var bakedIndex = TransitionIndex(transition);
        var overrideIndex = TransitionIndex(cueTransition);
        switcher.Cast(sheet);

        var thinkBeat = mark.Beat - TrackCueSheet.GridBeats;
        FeedSwitcherFrame(thinkBeat, phrases, generation: 1);
        director.SetNextTransition(overrideIndex);

        // The override's own Runway beat comes first; the already-decided act must not have moved to it.
        WalkSwitcher(thinkBeat + 1, mark.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(mark.Fired, Is.False, "The scheduled act keeps the card decided at the think.");

        WalkSwitcher(mark.Beat - cueTransition.Repertoire.RunwayBeats + 1, mark.Beat - transition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(mark.Fired, Is.True, "The act fires on the decided card's own Runway beat.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(bakedIndex),
            "The plan's decided card performs; the late-staged override waits for the next think.");
    }

    /// <summary>
    /// A playhead that arrives mid-Grid has missed the Grid's think, so nothing fires — a needle-drop, a
    /// mid-track entry, and a late start are uneventful instead of a hard cut.
    /// </summary>
    [Test]
    public void ALateEntryPerformsNothing()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var lateBeat = mark.Beat - cueTransition.Repertoire.RunwayBeats + 1;
        switcher.Cast(sheet);
        var effectBefore = switcher.Status.CurrentEffectIndex;

        FeedSwitcherFrame(lateBeat, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "A missed think starts no Transition.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The wall is left alone.");
        Assert.That(mark.Fired, Is.False, "Nothing fired, so nothing is marked fired.");
    }

    /// <summary>
    /// A forward jump that skips the scheduled act's fire beat leaves a Missed Cue: the act lapses, the
    /// mark is not performed and not spent, and nothing ever fires late.
    /// </summary>
    [Test]
    public void AForwardJumpLapsesTheScheduledActWithoutFiringLate()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 256, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var fireBeat = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);
        var effectBefore = switcher.Status.CurrentEffectIndex;

        // Think, then advance to just short of the fire beat, then jump past it and the mark.
        WalkSwitcher(mark.Beat - TrackCueSheet.GridBeats, fireBeat - 2, phrases, generation: 1);
        WalkSwitcher(mark.Beat + 2, mark.Beat + 8, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "No catch-up cut is performed.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The wall keeps what it had.");
        Assert.That(mark.Fired, Is.False, "The skipped mark lapsed: not performed, not spent.");
    }

    /// <summary>
    /// A handover changes nothing on the wall by itself: a sheet cast mid-track performs none of its past,
    /// and the next change waits for a mark or the stillness deadline.
    /// </summary>
    [Test]
    public void AHandoverChangesNothingByItself()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 256, "intro") };
        var outgoing = BuildExecutionSheet(phrases, generation: 1);
        var incoming = BuildExecutionSheet(phrases, generation: 2);
        var midTrack = outgoing.Marks[1].Beat + 1;
        switcher.Cast(outgoing);
        var effectBefore = switcher.Status.CurrentEffectIndex;
        FeedSwitcherFrame(midTrack, phrases, generation: 1);

        // Focus flapping between players is what re-cast a sheet mid-track on the live rig.
        switcher.Cast(incoming);
        FeedSwitcherFrame(midTrack + 1, phrases, generation: 2);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "The handover performs no cut.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The handover leaves the wall alone.");
        foreach (var mark in incoming.Marks)
        {
            if (mark.Beat <= midTrack)
            {
                Assert.That(mark.Fired, Is.False, $"The mark at {mark.Beat} lies behind the playhead and never fired.");
            }
        }
    }

    /// <summary>
    /// Fire and forget across a handover: a blend already in flight when a new sheet arrives runs to
    /// completion no matter what.
    /// </summary>
    [Test]
    public void AHandoverMidFlightLetsTheBlendRunToCompletion()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var outgoing = BuildExecutionSheet(phrases, generation: 1);
        var incoming = BuildExecutionSheet(phrases, generation: 2);
        var mark = outgoing.Marks[0];
        switcher.Cast(outgoing);
        WalkSwitcher(mark.Beat - TrackCueSheet.GridBeats, mark.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.GreaterThanOrEqualTo(0), "Setup: a blend is in flight.");

        switcher.Cast(incoming);
        var buffer = switcher.RenderAtTime(1_000_000f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex),
            "The in-flight blend completed onto its target despite the handover.");
        Assert.That(buffer[0], Is.EqualTo(EffectColors[mark.EffectIndex]));
    }

    /// <summary>
    /// An unstarted scheduled act is the outgoing plan's decision, not the wall's: a handover abandons
    /// it, the old fire beat passes with nothing on it, and the outgoing mark stays unspent.
    /// </summary>
    [Test]
    public void AHandoverAbandonsTheUnstartedScheduledAct()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var outgoing = BuildExecutionSheet(phrases, generation: 1);
        var incoming = BuildExecutionSheet(phrases, generation: 2);
        var mark = outgoing.Marks[0];
        var fireBeat = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(outgoing);
        WalkSwitcher(mark.Beat - TrackCueSheet.GridBeats, fireBeat - 1, phrases, generation: 1);
        var effectBefore = switcher.Status.CurrentEffectIndex;

        switcher.Cast(incoming);
        WalkSwitcher(fireBeat, mark.Beat, phrases, generation: 2);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "The outgoing plan's act never fires.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The handover leaves the wall alone.");
        Assert.That(mark.Fired, Is.False, "The abandoned act's mark is not spent.");
    }

    /// <summary>
    /// Stillness is the wall's own counter and survives handovers: still Grids counted before a handover
    /// keep counting after it, so the fourth still Grid fires on time instead of the spell restarting.
    /// </summary>
    [Test]
    public void AHandoverMidStillSpellDoesNotResetStillness()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var lastImpact = WalkDirectorPastAllMarks(phrases, generation: 1);

        // Two whole still Grids on the outgoing plan, then the handover lands mid-Grid.
        WalkDirector(lastImpact + 1, lastImpact + (2 * TrackCueSheet.GridBeats) + 2, phrases, generation: 1);
        var effectBefore = OnWallEffect();

        // The rest of the spell runs on the incoming plan; every remaining mark is behind the playhead.
        var ceilingBeat = lastImpact + (3 * TrackCueSheet.GridBeats);
        WalkDirector(lastImpact + (2 * TrackCueSheet.GridBeats) + 3, ceilingBeat - 1, phrases, generation: 2);
        Assert.That(OnWallEffect(), Is.EqualTo(effectBefore), "The handover itself changed nothing.");

        WalkDirector(ceilingBeat, ceilingBeat, phrases, generation: 2);
        Assert.That(OnWallEffect(), Is.Not.EqualTo(effectBefore),
            "The fourth still Grid fired on the wall's own count; a handover reset would have pushed it out.");
        Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.OffPlan));
        Assert.That(switcher.Status.LastCueMarkBeat, Is.EqualTo(ceilingBeat), "The Ceiling cue was taken at the Grid start.");
    }

    /// <summary>
    /// The stillness deadline: three whole Grids since the last fired cue mean the fourth Grid fires —
    /// and the Ceiling cue is taken at the Grid start, never mid-Grid.
    /// </summary>
    [Test]
    public void TheFourthStillGridFiresAtItsStart()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        Assert.That(switcher.Status.LastOffPlanSighting, Is.Null,
            "Before any ask the snapshot's off-plan element reads as empty.");
        var lastImpact = WalkDirectorPastAllMarks(phrases, generation: 1);
        var effectBefore = OnWallEffect();
        var ceilingBeat = lastImpact + (3 * TrackCueSheet.GridBeats);

        for (var beat = lastImpact + 1; beat < ceilingBeat; beat++)
        {
            WalkDirector(beat, beat, phrases, generation: 1);
            Assert.That(OnWallEffect(), Is.EqualTo(effectBefore),
                $"Beat {beat}: the wall holds through three whole still Grids.");
        }

        WalkDirector(ceilingBeat, ceilingBeat, phrases, generation: 1);
        Assert.That(OnWallEffect(), Is.Not.EqualTo(effectBefore), "The fourth still Grid fires.");
        Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.OffPlan));
        Assert.That(switcher.Status.LastCueMarkBeat, Is.EqualTo(ceilingBeat),
            "The Ceiling cue anchors to the Grid start it was taken at.");

        // The Live tab's data source: after an off-plan think the snapshot carries the question that was
        // asked and the answer that came back — last value only, empty history stays in the traces.
        var status = switcher.Status;
        Assert.That(status.LastOffPlanSighting, Is.Not.Null, "The snapshot carries the last off-plan question.");
        var sighting = status.LastOffPlanSighting.Value;
        Assert.That(sighting.Anomaly, Is.EqualTo(OffPlanAnomaly.StillnessUp));
        Assert.That(sighting.BoundaryBeat, Is.EqualTo(ceilingBeat));
        Assert.That(sighting.GapGrids, Is.EqualTo(TrackCueSheet.MaximumGapGrids));
        Assert.That(sighting.OnWallEffectIndex, Is.EqualTo(effectBefore),
            "The Sighting snapshots what the wall was showing when it was asked.");
        Assert.That(status.LastOffPlanAnswer.Perform, Is.True, "The answer that came back was a take.");
        Assert.That(status.LastOffPlanAnswer.EffectIndex, Is.EqualTo(OnWallEffect()),
            "The take on the snapshot is the take on the wall.");
    }

    /// <summary>
    /// Verifies that a Grid start received after the absolute beat update triggers one think on that same
    /// beat, while repeated frames at Grid position one do not think again.
    /// </summary>
    [Test]
    public void ALateGridDatagramAtAGridStartStillThinks()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var lastImpact = WalkDirectorPastAllMarks(phrases, generation: 1);
        var ceilingBeat = lastImpact + (3 * TrackCueSheet.GridBeats);

        // Three whole still Grids, stopping one beat short of the deadline Grid start.
        WalkDirector(lastImpact + 1, ceilingBeat - 1, phrases, generation: 1);
        Assert.That(switcher.Status.LastOffPlanSighting, Is.Null,
            "Setup: no anomaly has been reported before the deadline Grid.");

        // The absolute beat arrives before the current Grid state. The next frame corrects the Grid without
        // advancing the beat, so the Switcher must observe that state change rather than returning early.
        FeedSwitcherFrame(ceilingBeat, phrases, generation: 1, gridBeat: 16);
        Assert.That(switcher.Status.LastOffPlanSighting, Is.Null,
            "The stale Grid frame does not trigger the deadline think.");
        FeedSwitcherFrame(ceilingBeat, phrases, generation: 1, gridBeat: 1);

        var firstSighting = switcher.Status.LastOffPlanSighting;
        Assert.That(firstSighting, Is.Not.Null, "The corrected Grid state triggers the missed think.");
        Assert.That(firstSighting.Value.BoundaryBeat, Is.EqualTo(ceilingBeat));
        Assert.That(firstSighting.Value.Ask, Is.EqualTo(1), "The late Grid start is one off-plan ask.");

        FeedSwitcherFrame(ceilingBeat, phrases, generation: 1, gridBeat: 1);

        Assert.That(switcher.Status.LastOffPlanSighting.Value.Ask, Is.EqualTo(firstSighting.Value.Ask),
            "Repeated frames at the same Grid start do not think again.");
    }

    /// <summary>
    /// A held loop after the plan's last mark: re-crossed Grid starts are elapsed music, so stillness
    /// keeps counting and the fourth crossing fires a fresh Ceiling cue — while the spent mark itself is
    /// never re-fired.
    /// </summary>
    [Test]
    public void AHeldLoopKeepsTheWallAliveThroughStillnessWithoutReplayingSpentMarks()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var lastImpact = WalkDirectorPastAllMarks(phrases, generation: 1);
        var firedBeats = new System.Collections.Generic.List<int?>();
        foreach (var mark in switcher.Sheet.Marks)
        {
            firedBeats.Add(mark.FiredAtBeat);
        }

        // Loop the Grid after the last mark: walk it, snap back, walk it again.
        var changedAtBeat = -1;
        for (var pass = 0; pass < 5 && changedAtBeat < 0; pass++)
        {
            for (var beat = lastImpact + 1; beat <= lastImpact + TrackCueSheet.GridBeats; beat++)
            {
                var before = OnWallEffect();
                WalkDirector(beat, beat, phrases, generation: 1);
                if (OnWallEffect() != before)
                {
                    changedAtBeat = beat;
                    break;
                }
            }
        }

        Assert.That(changedAtBeat, Is.EqualTo(lastImpact + TrackCueSheet.GridBeats),
            "The looped stretch fires at a Grid start once stillness is up — the loop keeps the wall alive.");
        Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.OffPlan));
        for (var i = 0; i < firedBeats.Count; i++)
        {
            Assert.That(switcher.Sheet.Marks[i].FiredAtBeat, Is.EqualTo(firedBeats[i]),
                "A spent mark is never re-fired by a loop.");
        }
    }

    /// <summary>
    /// A loop that re-crosses a spent mark reports it through the one doorway: the Grid-start think sees
    /// the fired mark at the coming boundary and asks the Director once, and the wall shows the model's
    /// answer — the ask-1 deal taken, or a ride-through leaving the wall alone — while the spent mark and
    /// the sheet stay exactly as they were.
    /// </summary>
    /// <summary>
    /// A loop that re-crosses a spent mark reports it through the one doorway, once per Grid-start think:
    /// each re-crossing produces exactly one ask — pinned by the rising ask number the model's deal is
    /// seeded with — and the wall shows the answer: ride-throughs leave it alone, and the eventual take is
    /// a fresh card that is never the Effect already on the wall, while the spent mark and the sheet stay
    /// exactly as they were.
    /// </summary>
    [Test]
    public void AReCrossedFiredMarkGoesThroughTheDoorwayAndIsNeverReplayed()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorOnly(1, phrases, generation: 1);
        var mark = switcher.Sheet.Marks[0];
        var thinkBeat = mark.Beat - TrackCueSheet.GridBeats;
        // Start the wall off the mark's own card so the plan walk fires it normally.
        switcher.SetInitialEffect((mark.EffectIndex + 1) % controller.effects.Length, 0);
        WalkDirector(thinkBeat, mark.Beat, phrases, generation: 1);
        Assert.That(mark.Fired, Is.True, "Setup: the steady walk fires the plan's first mark.");
        var firedAt = mark.FiredAtBeat;
        var onWall = OnWallEffect();
        var expectedSheet = ExpectedFocusSheet(generation: 1);

        // The DJ holds a loop over the Grid before the spent mark: every re-crossed Grid start re-sees the
        // fired mark and asks the doorway exactly once, the still gap and ask number rising together,
        // until the rising cadence takes — certain by the Stillness Ceiling's gap.
        var took = false;
        for (var pass = 1; pass <= TrackCueSheet.MaximumGapGrids - 1 && !took; pass++)
        {
            WalkDirector(thinkBeat, thinkBeat + TrackCueSheet.GridBeats - 1, phrases, generation: 1);
            var expected = expectedSheet.DealOffPlanCueAt(
                thinkBeat, gapGrids: pass + 1, ask: pass, onWallEffectIndex: onWall, movingTowardEffectIndex: onWall);
            Assert.That(mark.FiredAtBeat, Is.EqualTo(firedAt), "The doorway answer never re-spends or edits the spent mark.");
            if (expected.Take)
            {
                took = true;
                Assert.That(expected.EffectIndex, Is.Not.EqualTo(onWall), "A doorway answer is never the Effect on the wall.");
                Assert.That(OnWallEffect(), Is.EqualTo(expected.EffectIndex),
                    $"Pass {pass}: the wall shows the ask-{pass} deal — that ask number only fits one doorway ask per think.");
                Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.OffPlan));
                Assert.That(switcher.Status.LastCueMarkBeat, Is.EqualTo(thinkBeat),
                    "The off-plan cue anchors to the Grid start it was taken at.");
            }
            else
            {
                Assert.That(OnWallEffect(), Is.EqualTo(onWall), $"Pass {pass}: a ride-through leaves the wall alone.");
            }
        }

        Assert.That(took, Is.True, "The rising cadence takes before the wall can outsit the Ceiling.");
    }

    /// <summary>
    /// A mark blending into the Effect already on the wall — lined up by a handover, never by a built
    /// sheet — goes through the doorway instead of firing: the wall shows the model's answer, which is
    /// never the Effect on the wall, and the colliding mark is never spent, lapsing at its boundary.
    /// </summary>
    [Test]
    public void AMarkBlendingIntoTheOnWallEffectGoesThroughTheDoorwayAndLapses()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorOnly(1, phrases, generation: 1);
        var mark = switcher.Sheet.Marks[0];
        var thinkBeat = mark.Beat - TrackCueSheet.GridBeats;
        // A handover lined the wall up with the mark's own card — the collision only a swap or loop makes.
        switcher.SetInitialEffect(mark.EffectIndex, 0);

        WalkDirector(thinkBeat, thinkBeat, phrases, generation: 1);

        var expected = ExpectedFocusSheet(generation: 1)
            .DealOffPlanCueAt(thinkBeat, gapGrids: 1, ask: 1, onWallEffectIndex: mark.EffectIndex, movingTowardEffectIndex: mark.EffectIndex);
        Assert.That(OnWallEffect(), Is.EqualTo(expected.Take ? expected.EffectIndex : mark.EffectIndex),
            "The wall shows the doorway's answer and nothing else.");
        if (expected.Take)
        {
            Assert.That(expected.EffectIndex, Is.Not.EqualTo(mark.EffectIndex),
                "A doorway answer is never the Effect on the wall or the one the mark moves toward.");
            Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.OffPlan));
        }

        // The colliding mark's decision moment has passed: it lapses at its boundary, never fired.
        WalkDirector(thinkBeat + 1, mark.Beat, phrases, generation: 1);
        Assert.That(mark.Fired, Is.False, "A doorway answer never spends the mark it stood in for.");
    }

    /// <summary>
    /// A loop snap-back before the scheduled fire beat keeps the act: the re-walked pass arrives on the
    /// same beat and the blend still leaves at boundary-minus-Runway, landing on the boundary the loop
    /// re-approaches.
    /// </summary>
    [Test]
    public void ALoopSnapBackBeforeTheFireBeatStillFiresOnTheReWalkedPass()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        var fireBeat = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        var thinkBeat = mark.Beat - TrackCueSheet.GridBeats;
        switcher.Cast(sheet);

        WalkSwitcher(thinkBeat, fireBeat - 2, phrases, generation: 1);
        // The DJ loops: the beat counter snaps back inside the Grid, then walks the same beats again.
        WalkSwitcher(thinkBeat + 1, fireBeat, phrases, generation: 1);

        Assert.That(mark.Fired, Is.True, "The re-walked pass reaches the fire beat and the act fires.");
        Assert.That(mark.FiredAtBeat, Is.EqualTo(fireBeat), "The blend leaves at boundary-minus-Runway on the re-walk.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f),
            "The blend flies whole onto the boundary the loop re-approaches.");
    }

    /// <summary>
    /// The plan's widest legal gap — 64 beats — plays out with no off-plan cue: at the fourth still
    /// Grid's start the closing mark is already scheduled to fire inside that Grid, so the plan itself
    /// satisfies the deadline and the Ceiling stays quiet.
    /// </summary>
    [Test]
    public void ALegalMaximumGapIsNeverPreEmptedByTheCeiling()
    {
        // The walk is seeded, so scan generations until the Director's own cast sheet carries a
        // maximum-width gap between consecutive marks. The scan feeds the Director alone so no
        // execution state leaks into the timeline under test.
        var phrases = new[] { Phrase(1, 449, "intro") };
        CuePlanMark gapOpen = null;
        CuePlanMark gapClose = null;
        var generation = 0;
        for (var candidate = 1; candidate <= 128 && gapClose == null; candidate++)
        {
            FeedDirectorOnly(focusBeat: 1, phrases, candidate);
            var marks = switcher.Sheet.Marks;
            for (var i = 1; i < marks.Count; i++)
            {
                if (marks[i].Beat - marks[i - 1].Beat == TrackCueSheet.MaximumGapBeats
                    && controller.transitions[marks[i - 1].TransitionIndex].Repertoire.RunwayBeats > 0)
                {
                    gapOpen = marks[i - 1];
                    gapClose = marks[i];
                    generation = candidate;
                    break;
                }
            }
        }

        Assert.That(gapClose, Is.Not.Null, "Setup: no scanned generation dealt the widest legal gap.");

        var openFire = gapOpen.Beat - controller.transitions[gapOpen.TransitionIndex].Repertoire.RunwayBeats;
        var closeFire = gapClose.Beat - controller.transitions[gapClose.TransitionIndex].Repertoire.RunwayBeats;
        WalkDirector(gapOpen.Beat - TrackCueSheet.GridBeats, openFire, phrases, generation);
        Assert.That(gapOpen.Fired, Is.True, "Setup: the gap's opening mark fires on its Runway beat.");
        switcher.RenderAtTime(1_000_000f, out _); // Complete the opening move; the wall is now still.

        for (var beat = openFire + 1; beat < closeFire; beat++)
        {
            WalkDirector(beat, beat, phrases, generation);
            Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0),
                $"Beat {beat}: an off-plan cue pre-empted a plan the playhead is still walking through.");
        }

        WalkDirector(closeFire, closeFire, phrases, generation);
        Assert.That(gapClose.Fired, Is.True, "The gap's closing mark performs as planned.");
        Assert.That(switcher.Status.LastCueSource, Is.EqualTo(CueSource.Plan));
    }

    /// <summary>
    /// The first think is the stillness baseline: a track whose opening mark sits the widest legal gap
    /// from the start is reached and performed with no off-plan cue, because the Grid start the walk
    /// begins on has no whole Grid behind it to count.
    /// </summary>
    [Test]
    public void ATrackOpeningWidestLegalGapIsNotPreEmpted()
    {
        var phrases = new[] { Phrase(1, 449, "intro") };
        CuePlanMark opener = null;
        var generation = 0;
        for (var candidate = 1; candidate <= 128 && opener == null; candidate++)
        {
            FeedDirectorOnly(focusBeat: 1, phrases, candidate);
            var first = switcher.Sheet.Marks[0];
            if (first.Beat - 1 == TrackCueSheet.MaximumGapBeats
                && controller.transitions[first.TransitionIndex].Repertoire.RunwayBeats > 0)
            {
                opener = first;
                generation = candidate;
            }
        }

        Assert.That(opener, Is.Not.Null, "Setup: no scanned generation opened with the widest legal gap.");
        // Start the wall off the opener's card so the boundary carries a performable mark, not a self-blend.
        switcher.SetInitialEffect((opener.EffectIndex + 1) % controller.effects.Length, 0);

        var openerFire = opener.Beat - controller.transitions[opener.TransitionIndex].Repertoire.RunwayBeats;
        for (var beat = 1; beat < openerFire; beat++)
        {
            WalkDirector(beat, beat, phrases, generation);
            Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0),
                $"Beat {beat}: an off-plan cue pre-empted the plan's opening mark.");
        }

        WalkDirector(openerFire, openerFire, phrases, generation);
        Assert.That(opener.Fired, Is.True, "The opening mark performs as planned.");
    }

    /// <summary>
    /// Hold is an inspection freeze answered at the think: nothing performs, the mark is not spent, and
    /// release does not chase it — the wall waits for the next think and the next mark.
    /// </summary>
    [Test]
    public void HoldAtTheThinkPerformsNothingAndReleaseWaitsForTheNextThink()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 256, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var held = sheet.Marks[0];
        var next = sheet.Marks[1];
        switcher.Cast(sheet);
        var onWall = switcher.Status.CurrentEffectIndex;
        controller.heldEffect = onWall;

        WalkSwitcher(held.Beat - TrackCueSheet.GridBeats, held.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(onWall), "Hold freezes the Director's answer.");
        Assert.That(held.Fired, Is.False, "A frozen mark is not marked fired.");

        controller.heldEffect = -1;
        FeedSwitcherFrame(held.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(onWall), "Release mid-Grid does not chase the passed mark.");

        WalkSwitcher(held.Beat + 2, next.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(next.EffectIndex), "The next mark performs normally.");
    }

    /// <summary>
    /// Hold outranks the stillness deadline, and releasing does not lose it: the next think finds
    /// stillness still up and fires rather than waiting for a plan with nothing left to give.
    /// </summary>
    [Test]
    public void HoldOutranksTheCeilingAndReleasePerformsAtTheNextThink()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var lastImpact = WalkDirectorPastAllMarks(phrases, generation: 1);
        controller.heldEffect = 1;

        var held = OnWallEffect();
        var beat = lastImpact + 1;
        for (var step = 0; step < TrackCueSheet.MaximumGapBeats * 2; step++, beat++)
        {
            WalkDirector(beat, beat, phrases, generation: 1);
        }

        Assert.That(OnWallEffect(), Is.EqualTo(held), "A held wall stays put however long the deadline is past.");

        controller.heldEffect = -1;
        var changed = false;
        for (var step = 0; step < TrackCueSheet.GridBeats + 1; step++, beat++)
        {
            WalkDirector(beat, beat, phrases, generation: 1);
            changed |= OnWallEffect() != held;
        }

        Assert.That(changed, Is.True, "Release must perform at the next think, not wait a whole plan gap.");
    }

    /// <summary>Pins handover identity: re-casting the same player/generation does not reset fired check-offs.</summary>
    [Test]
    public void RecastingTheSameSheetDoesNotResetCheckOffs()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        switcher.Cast(sheet);
        WalkSwitcher(mark.Beat - TrackCueSheet.GridBeats, mark.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);
        var firedAt = mark.FiredAtBeat;

        switcher.Cast(sheet);
        WalkSwitcher(mark.Beat, mark.Beat + 2, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "Idempotent handover preserves the fired check-off.");
        Assert.That(mark.FiredAtBeat, Is.EqualTo(firedAt), "The check-off itself is untouched.");
    }

    /// <summary>
    /// Pins the operator override as a performed move: an immediate pick starts a real Transition into
    /// the chosen Effect rather than cutting to it.
    /// </summary>
    [Test]
    public void AnOperatorOverrideStartsATransitionRatherThanCutting()
    {
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Setup: effect zero is on the wall.");

        director.ShowNow(2, controller.effectTime);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "A transition owns the frame, so nothing was cut.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0), "The staged card performs the override.");
    }

    /// <summary>
    /// Pins fire-and-forget: an operator override interjects without disturbing the plan in force, so the
    /// sheet stays cast and an already-performed mark never fires a second time.
    /// </summary>
    [Test]
    public void AnOperatorOverrideLeavesTheInForceSheetAndItsCheckOffsAlone()
    {
        StageSheetCatalog(cueTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        var mark = sheet.Marks[0];
        switcher.Cast(sheet);
        WalkSwitcher(mark.Beat - TrackCueSheet.GridBeats, mark.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(mark.Fired, Is.True, "Setup: the mark performed.");

        director.ShowNow(2, controller.effectTime);
        // Production frame order: the Director maintains and hands over before the Switcher executes, so
        // a sheet the override had wiped would be rebuilt here and re-cast with cleared check-offs.
        FeedDirectorFrame(mark.Beat, phrases, generation: 1);

        Assert.That(switcher.Sheet.StructureGeneration, Is.EqualTo(1), "The override left the plan in force.");
        Assert.That(mark.Fired, Is.True, "The override did not reset the cue's fired state.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The plan did not snap the wall back off the override.");
    }

    /// <summary>Pins the independent Standalone seconds path after sheet execution.</summary>
    [Test]
    public void StandaloneSecondsPathIsUnaffectedBySheetExecution()
    {
        StageSheetCatalog(hardCutTransition);
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1);
        switcher.Cast(sheet);
        FeedSwitcherFrame(sheet.Marks[0].Beat, phrases, generation: 1);

        switcher.StartTransition(2, TransitionIndex(transition), startTimeSeconds: 20f);
        var buffer = switcher.RenderAtTime(21.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(2));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(EffectColors[2]));
    }

    #endregion

    /// <summary>The Effect the wall is heading to, whether or not a Transition currently owns the frame.</summary>
    private int OnWallEffect()
    {
        return switcher.Status.CurrentEffectIndex < 0
            ? switcher.Status.TargetEffectIndex
            : switcher.Status.CurrentEffectIndex;
    }

    /// <summary>
    /// Puts <paramref name="sheetTransition"/> at Transition catalog position zero and starts the wall on
    /// Effect 1. Catalog position is Cue Sheet identity, so the one-card catalogs
    /// <see cref="BuildExecutionSheet"/> hands the builder name exactly this Transition and Effect 0 — and the
    /// wall starts somewhere else, so every planned cue has somewhere to move it.
    /// </summary>
    private void StageSheetCatalog(TransitionBase sheetTransition)
    {
        var index = TransitionIndex(sheetTransition);
        (controller.transitions[0], controller.transitions[index]) =
            (controller.transitions[index], controller.transitions[0]);
        switcher.SetInitialEffect(1, 0);
    }

    /// <summary>The catalog position a Transition instance currently occupies.</summary>
    private int TransitionIndex(TransitionBase target)
    {
        return System.Array.IndexOf(controller.transitions, target);
    }

    /// <summary>
    /// Builds a deterministic sheet over the leading cards of each catalog for execution assertions: one
    /// Transition card always, and <paramref name="effectCards"/> Effect cards — one by default, so every
    /// mark shares one target; two makes consecutive marks alternate (never-into-itself).
    /// </summary>
    private TrackCueSheet BuildExecutionSheet(StructurePhrase[] phrases, int generation, int effectCards = 1)
    {
        FeedWire(focusBeat: 1, phrases, generation, gridBeat: 1);
        var structure = controller.beatManager.Players[0].Structure;
        var effectDescriptors = new EffectDescriptor[effectCards];
        for (var i = 0; i < effectCards; i++)
        {
            effectDescriptors[i] = new EffectDescriptor(controller.effects[i].Repertoire);
        }

        return TrackCueSheet.Build(
            structure,
            effectDescriptors,
            new[] { new TransitionDescriptor(controller.transitions[0].Repertoire) },
            generation,
            playerNumber: 1);
    }

    /// <summary>
    /// Rebuilds the sheet the Director maintains for player 1 from the full test catalogs — the
    /// deterministic ground truth doorway assertions deal from (ADR-0008: same structure, seed, and
    /// catalogs deal the same sheet). Call only after a wire frame has delivered the structure.
    /// </summary>
    private TrackCueSheet ExpectedFocusSheet(int generation)
    {
        var effectDescriptors = new EffectDescriptor[controller.effects.Length];
        for (var i = 0; i < controller.effects.Length; i++)
        {
            effectDescriptors[i] = new EffectDescriptor(controller.effects[i].Repertoire);
        }

        var transitionDescriptors = new TransitionDescriptor[controller.transitions.Length];
        for (var i = 0; i < controller.transitions.Length; i++)
        {
            transitionDescriptors[i] = new TransitionDescriptor(controller.transitions[i].Repertoire);
        }

        return TrackCueSheet.Build(
            controller.beatManager.Players[0].Structure,
            effectDescriptors,
            transitionDescriptors,
            generation,
            playerNumber: 1);
    }

    /// <summary>Feeds one wire frame and lets only the Switcher execute its already-handed-over sheet.</summary>
    private void FeedSwitcherFrame(int focusBeat, StructurePhrase[] phrases, int generation, int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, gridBeat);
        switcher.Tick();
    }

    /// <summary>Feeds one production-order frame so the Director maintains/hands over before Switcher execution.</summary>
    private void FeedDirectorFrame(int focusBeat, StructurePhrase[] phrases, int generation, int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, gridBeat);
        director.Tick(0f);
        switcher.Tick();
    }

    /// <summary>
    /// Feeds one wire frame to the Director alone — sheet maintenance and handover with no Switcher
    /// execution — so a generation scan leaves no execution state behind.
    /// </summary>
    private void FeedDirectorOnly(int focusBeat, StructurePhrase[] phrases, int generation)
    {
        FeedWire(focusBeat, phrases, generation, gridBeat: null);
        director.Tick(0f);
    }

    /// <summary>Feeds every beat from <paramref name="fromBeat"/> through <paramref name="toBeat"/> to the Switcher alone.</summary>
    private void WalkSwitcher(int fromBeat, int toBeat, StructurePhrase[] phrases, int generation)
    {
        for (var beat = fromBeat; beat <= toBeat; beat++)
        {
            FeedSwitcherFrame(beat, phrases, generation);
        }
    }

    /// <summary>Feeds every beat in production frame order, settling any completed blend after each beat.</summary>
    private void WalkDirector(int fromBeat, int toBeat, StructurePhrase[] phrases, int generation)
    {
        for (var beat = fromBeat; beat <= toBeat; beat++)
        {
            FeedDirectorFrame(beat, phrases, generation);
            switcher.RenderAtTime(1_000_000f, out _);
        }
    }

    /// <summary>
    /// Walks the whole track in production order so every planned mark fires and completes, and returns
    /// the last fired mark's boundary — the beat the wall's still spell measures from.
    /// </summary>
    private int WalkDirectorPastAllMarks(StructurePhrase[] phrases, int generation)
    {
        WalkDirector(1, 1, phrases, generation);
        var lastMark = switcher.Sheet.Marks[switcher.Sheet.Marks.Count - 1];
        WalkDirector(2, lastMark.Beat, phrases, generation);
        Assert.That(lastMark.Fired, Is.True, "Setup: the steady walk fires the plan's last mark.");
        return lastMark.Beat;
    }

    /// <summary>
    /// Translates one player wire snapshot into BeatManager values. Execution reads only the on-air beat
    /// and timing-grid lanes, so a loop is expressed as the beat sequence a loop actually produces rather
    /// than as a loop lane.
    /// </summary>
    private void FeedWire(int focusBeat, StructurePhrase[] phrases, int generation, int? gridBeat)
    {
        // The wire's timing grid is phrase-relative: it restarts at every phrase start, so a phrase
        // ending off-cycle produces a short Grid.
        var onAirGridBeat = gridBeat ?? PhraseRelativeGridBeat(focusBeat, phrases);
        BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
        {
            snapshot.beatInBar = ((focusBeat - 1) % 4) + 1;
            snapshot.beat = new BeatPosition { current = focusBeat, total = -1 };
            snapshot.bpm = 120f;
            snapshot.timingGrid = new TimingGrid
            {
                beat = onAirGridBeat,
                bar = ((onAirGridBeat - 1) / 4) + 1,
                state = "locked",
            };
            snapshot.playersLive = "1";
            snapshot.players ??= new PlayerState[RaveWireSnapshot.PlayerCount];
            var player = PlayerState.Unavailable;
            player.clock = new PlayerClock
            {
                bpm = 120f,
                beat = focusBeat,
                bar = ((focusBeat - 1) / 4) + 1,
                beatInBar = ((focusBeat - 1) % 4) + 1,
                beatPulse = 1f,
            };
            player.transport = new PlayerTransport { playing = 1, cued = 0, onAir = 1, master = 1, synced = 1 };
            player.timingGrid = new TimingGrid
            {
                beat = onAirGridBeat,
                bar = ((onAirGridBeat - 1) / 4) + 1,
                state = "locked",
            };
            player.structure = new PlayerStructure
            {
                generation = generation,
                trackId = "track" + generation,
                source = "analyzed",
                totalBeats = 512,
                phraseCount = phrases.Length,
                phrases = phrases,
            };
            snapshot.players[0] = player;
        });
        controller.beatManager.Update(0f);
    }

    /// <summary>
    /// The phrase-relative Grid position for <paramref name="beat"/>: the Grid restarts at every phrase
    /// start, and beats past the last phrase keep cycling from it.
    /// </summary>
    private static int PhraseRelativeGridBeat(int beat, StructurePhrase[] phrases)
    {
        var phraseStart = 1;
        foreach (var phrase in phrases)
        {
            if (phrase.startBeat <= beat)
            {
                phraseStart = phrase.startBeat;
            }
        }

        return ((beat - phraseStart) % 16) + 1;
    }

    /// <summary>Creates one structure phrase for deterministic Track Cue Sheet construction.</summary>
    private static StructurePhrase Phrase(int startBeat, int endBeat, string type)
    {
        return new StructurePhrase
        {
            startBeat = startBeat,
            endBeat = endBeat,
            type = type,
            variant = 0,
        };
    }

    private sealed class SolidEffect : EffectBase
    {
        private readonly Color color;

        public SolidEffect(Color color)
        {
            this.color = color;
            buffer = new Color[Penrose.Total];
        }

        public override string DebugText() => string.Empty;

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = color;
            }
        }
    }

    private sealed class HardCutTransition : TransitionBase
    {
        public HardCutTransition()
        {
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                runwayBeats: 0,
                tailBeats: 0,
                TransitionShape.Blend,
                TransitionIntensity.High,
                defaultDurationSeconds: 0f));
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
        }
    }

    private sealed class TimedTransition : TransitionBase
    {
        private readonly int runwayBeats;
        private readonly int tailBeats;

        /// <summary>Creates a blend Transition whose beat-domain shape can pin both rendering and cue timing.</summary>
        public TimedTransition(int runwayBeats = 1, int tailBeats = 0)
        {
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                runwayBeats,
                tailBeats,
                TransitionShape.Blend,
                TransitionIntensity.Medium,
                defaultDurationSeconds: 1f));
        }

        public override void OnStart()
        {
        }

        public override void OnEnd()
        {
        }

        public override void Draw()
        {
            var source = EffectColors[A];
            var target = EffectColors[B];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Color.Lerp(source, target, V);
            }
        }
    }
}
