using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Execution-seam tests for the plan-driven Switcher (ADR-0020). Immutable Track Cue Sheets are
// handed in, per-player wire snapshots advance execution, and assertions stay on public stage state.
public sealed class SwitcherExecutionTests
{
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
            new SolidEffect(Color.red),
            new SolidEffect(Color.blue),
            new SolidEffect(Color.green),
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
        switcher.BindDirector(director);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void RenderAtTimeAdvancesTransitionProgressFromStartTiming()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromBeatClock(startTime: 10f, secondsPerBeat: 0.5f));

        var buffer = switcher.RenderAtTime(10.25f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.red, Color.blue, 0.5f)));
    }

    [Test]
    public void RenderPromotesDestinationAfterStartedTransitionDuration()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromDefaultDuration(startTime: 10f));

        var buffer = switcher.RenderAtTime(11.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.CurrentTransitionName, Is.EqualTo(string.Empty));
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void ZeroDurationTransitionPromotesDestinationImmediately()
    {
        switcher.StartTransition(1, 1, TransitionStartTiming.FromDefaultDuration(startTime: 10f));

        var buffer = switcher.RenderAtTime(10f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void StartTransitionWhileRenderingReplacesMoveFromPreviousTarget()
    {
        switcher.StartTransition(1, 0, TransitionStartTiming.FromDefaultDuration(startTime: 10f));
        switcher.RenderAtTime(10.25f, out _);

        switcher.StartTransition(0, 0, TransitionStartTiming.FromDefaultDuration(startTime: 20f));
        var buffer = switcher.RenderAtTime(20.5f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(0));
        Assert.That(transition.A, Is.EqualTo(1));
        Assert.That(transition.B, Is.EqualTo(0));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.blue, Color.red, 0.5f)));
    }

    #region Sheet execution

    /// <summary>
    /// Pins an on-time Cue Mark: its full Runway begins at zero, Impact lands on the mark, and Tail completes after.
    /// </summary>
    [Test]
    public void ACueFiresSoItsImpactPointLandsOnTheCueMark()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var repertoire = cueTransition.Repertoire;
        var runwayStart = mark.Beat - repertoire.RunwayBeats;
        switcher.Cast(sheet);

        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        var runwayStartTime = Time.time;
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Runway is under way the instant the cast lands.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f), "An on-time cast runs the full Runway from zero.");

        switcher.RenderAtTime(runwayStartTime + (repertoire.RunwayBeats * 0.5f), out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Impact Point is mid-flight, not completion.");
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(repertoire.ImpactPoint).Within(0.01f),
            "The Impact Point lands exactly on the Cue Mark beat.");

        var buffer = switcher.RenderAtTime(
            runwayStartTime + ((repertoire.RunwayBeats + repertoire.TailBeats) * 0.5f),
            out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "The Tail completes after the Impact.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>
    /// Pins the whole of late arrival: a cue fires on its Runway beat, so a playhead that is past that beat
    /// performs nothing at all. This is what makes a needle-drop, a mid-track handover, and a late entry
    /// uneventful instead of a hard cut — no rule is needed beyond firing on the beat.
    /// </summary>
    [Test]
    public void ALateEntryPerformsNothing()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var lateBeat = mark.Beat - cueTransition.Repertoire.RunwayBeats + 1;
        switcher.Cast(sheet);
        var effectBefore = switcher.Status.CurrentEffectIndex;

        FeedSwitcherFrame(lateBeat, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "A passed Runway beat starts no Transition.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The wall is left alone.");
        Assert.That(mark.Fired, Is.False, "Nothing fired, so nothing is marked fired.");
    }

    /// <summary>Pins the zero-duration edge: a hard-cut mark promotes its destination on the mark tick.</summary>
    [Test]
    public void AHardCutMarkPromotesItsDestinationImmediately()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 1);
        var mark = sheet.Marks[0];
        switcher.Cast(sheet);

        FeedSwitcherFrame(mark.Beat, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "A zero-duration cast promotes without a render tick.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        var buffer = switcher.RenderAtTime(Time.time, out _);
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>
    /// Pins the same rule across a jump: landing past several marks performs none of them. No burst of
    /// catch-up cuts, because none of those Runway beats is the beat the playhead is on.
    /// </summary>
    [Test]
    public void ALateJumpPerformsNoneOfThePassedMarks()
    {
        var phrases = new[] { Phrase(1, 256, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: the long structure produces two marks.");
        switcher.Cast(sheet);
        var effectBefore = switcher.Status.CurrentEffectIndex;

        FeedSwitcherFrame(sheet.Marks[1].Beat, phrases, generation: 1, loopRolling: true);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "No catch-up cut is performed.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The wall keeps what it had.");
        Assert.That(sheet.Marks[0].Fired, Is.False, "The mark jumped over never fired.");
        Assert.That(sheet.Marks[1].Fired, Is.False, "Nor did the one the jump landed on, past its Runway.");
    }

    /// <summary>
    /// Pins the defect the live logs showed four times in twenty seconds: a handover made every mark behind
    /// the playhead due again and the furthest one performed as an instant cut on the Cast frame. A sheet cast
    /// mid-track performs none of its past.
    /// </summary>
    [Test]
    public void AFreshCastMidTrackPerformsNothingBehindThePlayhead()
    {
        var phrases = new[] { Phrase(1, 256, "intro") };
        var outgoing = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var incoming = BuildExecutionSheet(phrases, generation: 2, transitionIndex: 2);
        var midTrack = outgoing.Marks[1].Beat;
        switcher.Cast(outgoing);
        var effectBefore = switcher.Status.CurrentEffectIndex;
        FeedSwitcherFrame(midTrack, phrases, generation: 1);

        // Focus flapping between players is what re-cast a sheet mid-track on the live rig.
        switcher.Cast(incoming);
        FeedSwitcherFrame(midTrack, phrases, generation: 2);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1), "The handover performs no cut.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(effectBefore), "The handover leaves the wall alone.");
        foreach (var mark in incoming.Marks)
        {
            var runwayStart = mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;
            if (runwayStart < midTrack)
            {
                Assert.That(mark.Fired, Is.False, $"The mark at {mark.Beat} lies behind the playhead and never fired.");
            }
        }
    }

    /// <summary>
    /// Pins the one edge case the wall has: the DJ loops, the playhead returns to a Runway beat whose cue has
    /// already been performed, and the same cue must not play twice. The Director is asked instead, and the
    /// wall either moves somewhere new or stays put — never back to where it already was.
    /// </summary>
    [Test]
    public void ALoopBackOverAFiredCuePerformsAFreshCueRatherThanReplayingIt()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        // Through the Director, because the loop question is answered from the sheet it built.
        FeedDirectorFrame(focusBeat: 1, phrases, generation: 1);
        var mark = switcher.Sheet.Marks[0];
        var runwayStart = mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;

        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);
        Assert.That(mark.Fired, Is.True, "Setup: the planned cue fired and is marked on the cue itself.");

        // Roll the loop back over that same boundary several times. Whether any one crossing changes the wall is
        // dealt, so what must hold every time is that the spent cue is never the thing performed.
        for (var pass = 0; pass < 8; pass++)
        {
            FeedSwitcherFrame(runwayStart - 1, phrases, generation: 1);
            FeedSwitcherFrame(runwayStart, phrases, generation: 1);
            switcher.RenderAtTime(1_000_000f, out _);
            Assert.That(OnWallEffect(), Is.Not.EqualTo(mark.EffectIndex),
                $"pass {pass}: the spent cue's own destination was performed again.");
        }
    }

    /// <summary>
    /// Pins the one rule the whole plan exists to serve: the wall never holds still past four Grid Boundaries.
    /// Walked past the plan's last mark, so nothing planned can fire and only the Switcher's own boundary count
    /// can move the wall — the freeze a DJ looping a stretch with no marks used to cause.
    /// </summary>
    [Test]
    public void TheWallChangesWithinFourGridBoundariesEvenWithNoMarksLeft()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 1, phrases, generation: 1);
        var lastMark = switcher.Sheet.Marks[switcher.Sheet.Marks.Count - 1].Beat;

        var beat = lastMark + 1;
        for (var window = 0; window < 3; window++)
        {
            var before = OnWallEffect();
            var changed = false;
            // Four Grid Boundaries, plus one Grid of slack because the walk can start mid-Grid and the first
            // crossing is what the count measures from.
            const int windowBeats = TrackCueSheet.MaximumGapBeats + TrackCueSheet.GridBeats;
            for (var step = 0; step < windowBeats; step++, beat++)
            {
                FeedSwitcherFrame(beat, phrases, generation: 1);
                switcher.RenderAtTime(1_000_000f, out _);
                changed |= OnWallEffect() != before;
            }

            Assert.That(changed, Is.True,
                $"beats {beat - windowBeats}-{beat - 1} held the wall still past the ceiling.");
        }
    }

    /// <summary>
    /// Pins Hold against that ceiling: an inspection freeze outranks it, and releasing does not lose it — the
    /// next boundary performs rather than waiting for a plan that has nothing left to give.
    /// </summary>
    [Test]
    public void HoldOutranksTheCeilingAndReleasePerformsAtTheNextBoundary()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 1, phrases, generation: 1);
        var beat = switcher.Sheet.Marks[switcher.Sheet.Marks.Count - 1].Beat + 1;
        controller.heldEffect = 1;

        var held = OnWallEffect();
        for (var step = 0; step < TrackCueSheet.MaximumGapBeats * 2; step++, beat++)
        {
            FeedSwitcherFrame(beat, phrases, generation: 1);
            switcher.RenderAtTime(1_000_000f, out _);
        }

        Assert.That(OnWallEffect(), Is.EqualTo(held), "A held wall stays put however long the ceiling is past.");

        controller.heldEffect = -1;
        var changed = false;
        for (var step = 0; step < TrackCueSheet.GridBeats + 1; step++, beat++)
        {
            FeedSwitcherFrame(beat, phrases, generation: 1);
            switcher.RenderAtTime(1_000_000f, out _);
            changed |= OnWallEffect() != held;
        }

        Assert.That(changed, Is.True, "Release must perform at the next boundary, not wait a whole plan gap.");
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
        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(0));
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
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        Assert.That(mark.Fired, Is.True, "Setup: the mark performed.");

        director.ShowNow(2, controller.effectTime);
        // Production frame order: the Director maintains and hands over before the Switcher executes, so
        // a sheet the override had wiped would be rebuilt here and re-cast with cleared check-offs.
        FeedDirectorFrame(mark.Beat, phrases, generation: 1);

        Assert.That(switcher.Sheet.StructureGeneration, Is.EqualTo(1), "The override left the plan in force.");
        Assert.That(mark.Fired, Is.True, "The override did not reset the cue's fired state.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The plan did not snap the wall back off the override.");
    }

    /// <summary>
    /// Pins Hold as an inspection freeze: a mark coming due while held performs nothing and is not marked
    /// fired. Release does not chase it — the wall waits for the next boundary, because a Transition only
    /// ever starts on a Runway beat.
    /// </summary>
    [Test]
    public void HoldPerformsNothingAndReleaseWaitsForTheNextBoundary()
    {
        var phrases = new[] { Phrase(1, 256, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var held = sheet.Marks[0];
        var next = sheet.Marks[1];
        switcher.Cast(sheet);
        controller.heldEffect = 0;

        FeedSwitcherFrame(held.Beat - cueTransition.Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Hold freezes the Director's answer.");
        Assert.That(held.Fired, Is.False, "A frozen mark is not marked fired.");

        controller.heldEffect = -1;
        FeedSwitcherFrame(held.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Release mid-Grid does not chase the passed mark.");

        FeedSwitcherFrame(next.Beat - controller.transitions[next.TransitionIndex].Repertoire.RunwayBeats, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(next.EffectIndex), "The next boundary performs normally.");
    }

    /// <summary>Pins handover identity: re-casting the same player/generation does not reset fired check-offs.</summary>
    [Test]
    public void RecastingTheSameSheetDoesNotResetCheckOffs()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 2);
        var mark = sheet.Marks[0];
        var runwayStart = mark.Beat - cueTransition.Repertoire.RunwayBeats;
        switcher.Cast(sheet);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);

        switcher.Cast(sheet);
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark.EffectIndex), "Idempotent handover preserves the fired check-off.");
    }

    /// <summary>
    /// Pins the spacing rule under a held loop: the wall may ride a boundary through, but it can never hold
    /// past the widest gap the plan itself would have produced. Rolls a loop over one fired mark repeatedly
    /// and requires a fresh cue inside that window.
    /// </summary>
    /// <summary>
    /// Pins the fired-mark half of the ceiling: a loop parked on one spent boundary keeps being given fresh
    /// cues. The count that bounds the gap is the Switcher's own (TrackCueSheetTests pins how the deal reads it);
    /// what this covers is that re-crossing spent plan is a live trigger rather than a dead end.
    /// </summary>
    [Test]
    public void AHeldLoopKeepsBeingGivenFreshCues()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 1, phrases, generation: 1);
        var mark = switcher.Sheet.Marks[0];
        var runwayStart = mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);

        // Roll the same boundary repeatedly, as a held loop does. Counted by the destination actually moving,
        // because a hard-cut card completes inside its own frame and leaves no transition in flight to see.
        var performed = 0;
        var onWall = OnWallEffect();
        for (var pass = 0; pass < 24; pass++)
        {
            FeedSwitcherFrame(runwayStart - 1, phrases, generation: 1);
            FeedSwitcherFrame(runwayStart, phrases, generation: 1);
            if (OnWallEffect() != onWall)
            {
                performed++;
            }

            switcher.RenderAtTime(1_000_000f, out _);
            onWall = OnWallEffect();
        }

        Assert.That(performed, Is.GreaterThan(1),
            "A held loop must keep being given fresh cues rather than freezing the wall.");
    }

    /// <summary>The Effect the wall is heading to, whether or not a Transition currently owns the frame.</summary>
    private int OnWallEffect()
    {
        return switcher.Status.CurrentEffectIndex < 0
            ? switcher.Status.TargetEffectIndex
            : switcher.Status.CurrentEffectIndex;
    }

    /// <summary>
    /// Pins where a loop cue lands: on the boundary, with a whole Runway ahead of it, exactly like a planned
    /// cue. Off-plan is about who chose the card, not about the timing it gets.
    /// </summary>
    [Test]
    public void ALoopCueFliesAWholeRunwayOntoTheBoundary()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        FeedDirectorFrame(focusBeat: 1, phrases, generation: 1);
        var mark = switcher.Sheet.Marks[0];
        var runwayStart = mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;
        FeedSwitcherFrame(runwayStart, phrases, generation: 1);
        switcher.RenderAtTime(1_000_000f, out _);

        for (var pass = 0; pass < 24; pass++)
        {
            FeedSwitcherFrame(runwayStart - 1, phrases, generation: 1);
            FeedSwitcherFrame(runwayStart, phrases, generation: 1);
            if (switcher.Status.CurrentEffectIndex < 0)
            {
                Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f),
                    "Firing on the Runway beat means the Transition plays out whole rather than starting part-done.");
                return;
            }
        }

        Assert.Fail("No loop cue was ever performed, so its Runway could not be checked.");
    }

    /// <summary>Pins the independent Standalone seconds path after sheet execution.</summary>
    [Test]
    public void StandaloneSecondsPathIsUnaffectedBySheetExecution()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        var sheet = BuildExecutionSheet(phrases, generation: 1, transitionIndex: 1);
        switcher.Cast(sheet);
        FeedSwitcherFrame(sheet.Marks[0].Beat, phrases, generation: 1);

        switcher.StartTransition(0, 0, TransitionStartTiming.FromDefaultDuration(startTime: 20f));
        var buffer = switcher.RenderAtTime(21.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.red));
    }

    #endregion

    /// <summary>Builds a deterministic sheet restricted to one target Effect and Transition for execution assertions.</summary>
    private TrackCueSheet BuildExecutionSheet(StructurePhrase[] phrases, int generation, int transitionIndex)
    {
        FeedWire(focusBeat: 1, phrases, generation, loopRolling: false, gridBeat: 1);
        var structure = controller.beatManager.Players[0].Structure;
        return TrackCueSheet.Build(
            structure,
            new[] { new EffectDescriptor(1, controller.EffectiveRepertoire(1)) },
            new[] { new TransitionDescriptor(transitionIndex, controller.transitions[transitionIndex].Repertoire) },
            generation,
            playerNumber: 1);
    }

    /// <summary>Feeds one wire frame and lets only the Switcher execute its already-handed-over sheet.</summary>
    private void FeedSwitcherFrame(
        int focusBeat,
        StructurePhrase[] phrases,
        int generation,
        bool loopRolling = false,
        int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, loopRolling, gridBeat);
        switcher.Tick();
    }

    /// <summary>Feeds one production-order frame so the Director maintains/hands over before Switcher execution.</summary>
    private void FeedDirectorFrame(int focusBeat, StructurePhrase[] phrases, int generation, int? gridBeat = null)
    {
        FeedWire(focusBeat, phrases, generation, loopRolling: false, gridBeat);
        director.Tick(0f);
        switcher.Tick();
    }

    /// <summary>Translates one player wire snapshot, including the per-player loop lane, into BeatManager values.</summary>
    private void FeedWire(
        int focusBeat,
        StructurePhrase[] phrases,
        int generation,
        bool loopRolling,
        int? gridBeat)
    {
        var onAirGridBeat = gridBeat ?? (((focusBeat - 1) % 16) + 1);
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
            // The sheet player's own Grid lane, which is what the Switcher counts boundaries from.
            player.timingGrid = new TimingGrid
            {
                beat = onAirGridBeat,
                bar = ((onAirGridBeat - 1) / 4) + 1,
                state = "locked",
            };
            player.loopState = new LoopState
            {
                active = loopRolling ? 1 : 0,
                set = loopRolling ? 1 : 0,
                lengthBeats = loopRolling ? 16f : 0f,
                lengthMs = loopRolling ? 8_000 : 0,
                sizeNumerator = loopRolling ? 16 : 0,
                sizeDenominator = 1,
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
                RepertoireFlags.None,
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
                RepertoireFlags.None,
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
            var colors = new[] { Color.red, Color.blue };
            var source = colors[A];
            var target = colors[B];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Color.Lerp(source, target, V);
            }
        }
    }
}
