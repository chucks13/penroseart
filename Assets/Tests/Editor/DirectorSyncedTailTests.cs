using System.Reflection;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

public sealed class DirectorSyncedTailTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("DirectorSyncedTailTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.effects = new EffectBase[] { new TestEffect(), new TestEffect(), new TestEffect(Repertoire.HandlesDrop) };
        controller.transitions = new TransitionBase[] { new TailedTransition(), new TailedTransition() };
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.effectDeck = new[] { 1, 2, 0 };
        controller.transitionDeck = new[] { 0 };
        controller.currentTransition = 0;
        controller.timer = new Timer(controller.effectTime, false);

        switcher = new Switcher(controller, controller.effects, controller.transitions);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;

        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void DirectorCommitsCueWellBeforeTheRunwayStarts()
    {
        director.SetNextEffect(1);

        // Beat 594 is 15 beats before the 609 boundary — far outside the Runway-4 window the old
        // model waited for. Commit-before-lock cues here, on the first beat the mark is targeted.
        SetTrackPhaseBeat(594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The Director commits as soon as the mark is targeted, not when the runway arrives.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.StartBeat, Is.EqualTo(605), "The sent cue's plan still starts the transition at Cue Mark minus Runway.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "A committed cue is loaded, not started; the stage waits for the Start Beat.");
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void MarkAtItsLockPointIsMissedAndTheNextMarkCommitsInstead()
    {
        director.SetNextEffect(1);

        // Beat 604 is the 609 mark's Lock Point for a Runway-4 transition; commits stop the beat before.
        SetTrackPhaseBeat(604, beatsUntilPhraseEnd: 5, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Nothing may commit at or past the Lock Point; the mark is missed, never fired late.");
        Assert.That(director.Status.LastCue.Outcome, Is.EqualTo(CueDecisionOutcome.None));

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The missed mark never fires; the next mark commits instead.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(625));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(625));
    }

    [Test]
    public void DirectorCuesTheLatestStagedEffectAtTheNextCommittableMark()
    {
        director.SetNextEffect(1);
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "Setup: the 609 boundary commits the staged Performer.");
        RenderTransitionPastCompletion();

        director.SetNextEffect(2);
        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(625));
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(2), "An effect staged after a commit is cast at the next mark, not into the sent cue.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
    }

    [Test]
    public void SentCueStartsItsTransitionAtRunwayStartNotAtCommit()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: the early commit loads the cue.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "No transition may start at commit time.");

        // The Start Beat 605 lies 11 beats (5.5s at 120 BPM) past the beat-594 commit; render just past it.
        RenderTransitionAt(6f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The transition starts once time reaches the Start Beat.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TransitionProgress, Is.LessThan(1f));
    }

    [Test]
    public void TailedTransitionCompletionKeepsNextAnchorOnUpcomingTrackPhaseBoundary()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The setup should commit a tailed transition toward beat 609.");

        RenderTransitionPastCompletion();
        SetTrackPhaseBeat(613, beatsUntilPhraseEnd: 12, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0), "The tailed transition should have completed.");
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(625));
    }

    [Test]
    public void CommittingTailedTransitionMarksCadenceAtItsCueMark()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The setup should commit a tailed transition toward beat 609.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609), "Cadence is marked at the Impact Point when the cue commits, not when it fires.");
    }

    [Test]
    public void ZeroRunwayTailedTransitionMissesItsImpactBeatOnceTheLockPointPasses()
    {
        controller.transitions[0] = new ZeroRunwayTailedTransition();
        controller.transitions[0].BindController(controller);
        controller.transitions[0].Init();
        director.SetNextTransition(0);

        // For Runway 0 the Lock Point is the beat before the mark: beat 608 is already too late for 609.
        SetTrackPhaseBeat(608, beatsUntilPhraseEnd: 1, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "The 609 mark can no longer commit; it is missed for good.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "No late or backdated cut may fire for the missed 609 impact.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(625), "The cursor moves on; the next mark commits instead.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(625));
    }

    [Test]
    public void NextTransitionCanCommitAfterTailedTransitionCompletes()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        RenderTransitionPastCompletion();

        SetTrackPhaseBeat(617, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The Director should commit the next transition as soon as cadence allows after Tail completion.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(625));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
    }

    [Test]
    public void DirectorCommitsTheNextMarkWhileTailedTransitionIsStillRendering()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        // The cue starts at beat 605 (1s past the beat-603 commit); render inside its 4s span.
        RenderTransitionAt(2f);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The previous transition Tail is still mechanically rendering in this test.");

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(625));
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(625), "The next mark commits while the previous Tail renders; commit never waits for the runway.");
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(625));
    }

    [Test]
    public void CommittingTailedTransitionImmediatelyStagesFollowingMove()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(director.Status.NextEffectIndex, Is.Not.EqualTo(1), "The consumed target should not remain staged until Tail completion.");
    }

    [Test]
    public void SyncedCueDoesNotRecommitTheSameMandatoryBoundaryBeforeItsLockPoint()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(602, beatsUntilPhraseEnd: 7, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.LastCue.Beat, Is.EqualTo(602), "Setup: the 609 boundary commits on beat 602.");

        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(director.Status.LastCue.Beat, Is.EqualTo(602), "A committed boundary must not issue a second cue on a later commit-eligible beat.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void CommittedCueLoadsTheUpcomingPhraseCueMarkOnceTheReadingCountsDown()
    {
        director.SetNextEffect(1);

        // Commit toward the current Phrase's 609 boundary (before its Lock Point 604).
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "Setup: the mandatory boundary commits toward 609.");
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));

        director.SetNextEffect(2);

        // The 609 boundary is committed; the feed now counts down to the next Phrase (its own length 64)
        // starting in 2 beats via next_phrase_state. The upcoming-phrase sheet loads its first cue mark past 609.
        SetUpcomingPhraseBeat(607, nextStartInBeats: 2, nextPhraseLengthBeats: 64);
        director.Tick(0f);

        Assert.That(director.Status.HasCueMark, Is.True);
        Assert.That(director.Status.CueMarkBeat, Is.GreaterThan(609), "The upcoming Phrase's cue mark loads past the committed boundary.");
        Assert.That((director.Status.CueMarkBeat - 1) % 16, Is.EqualTo(0), "Upcoming cue marks land on a 16-grid boundary.");
    }

    [Test]
    public void MissedMandatoryBoundaryDoesNotFireWhenNextPhraseFrameArrivesOnTheBoundary()
    {
        controller.transitions[0] = new HardCutTransition();
        controller.transitions[0].BindController(controller);
        controller.transitions[0].Init();
        director.SetNextTransition(0);

        // Beat 608 is the hard cut's Lock Point for 609: the boundary can no longer commit.
        SetTrackPhaseBeat(608, beatsUntilPhraseEnd: 1, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Setup: the 609 boundary is already past commit.");

        SetTrackPhaseBeat(609, beatsUntilPhraseEnd: 64, phraseLengthBeats: 64);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "The missed boundary never hard-cuts, even when the next Phrase frame lands exactly on it.");
        Assert.That(director.Status.LastChangeBeat, Is.Not.EqualTo(609), "No cue for 609 ever commits.");
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The new Phrase's sheet takes over planning.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.GreaterThan(609), "The committed cue aims at the new Phrase's own mark.");
    }

    [Test]
    public void DropAlignedCueCastsDropCapablePerformerWhenAvailable()
    {
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The Director should commit a synced cue onto the Drop's mark.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(2), "The Drop-aligned cue should cast the available Drop-capable Performer.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void HeldEffectCueLeavesDeckCandidatesUnrotated()
    {
        controller.effectDeck = new[] { 1, 2, 0 };
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 2, 1 });
        controller.heldEffect = 1;
        var effectDeckBeforeTick = (int[])controller.effectDeck.Clone();
        var transitionDeckBeforeTick = (int[])controller.transitionDeck.Clone();
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A held cue must not commit a transition.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBeforeTick), "Deck candidates rotate only when a cue is actually sent; a held cue sends none.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckBeforeTick), "Deck candidates rotate only when a cue is actually sent; a held cue sends none.");
    }

    [Test]
    public void DropAlignedCuePreservesManualStagedPerformerWhenDropCapablePerformerIsAvailable()
    {
        director.SetNextEffect(1);
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The Director should commit a synced cue onto the Drop's mark.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "Manual staging should preserve the chosen Performer instead of recasting from the deck.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void DropAlignedCueCastsDropCapableTransitionWhenAvailable()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(1));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void FillAlignedCueCastsFillCapableTransitionWhenAvailable()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesFill, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingFill(beatsUntilStart: 7);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(1));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void EventAlignedCuePreservesHeldTransition()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 1 });
        director.SetNextTransition(0);
        director.SetHoldSelectedTransition(true);
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void EventAlignedCuePreservesManualTransitionWithoutHold()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 2, tailBeats: 0) },
            new[] { 0, 1 });
        director.SetNextTransition(0);
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0));
        Assert.That(director.Status.NextTransitionIndex, Is.Not.EqualTo(1), "Manual staging should not be recast by event-aware transition selection.");
    }

    [Test]
    public void EventAlignedCueKeepsStagedTransitionWhenPreferredTransitionCannotCueNow()
    {
        // Commit-before-lock inverts which runway locks first: the LONG-runway Drop transition's Lock
        // Point (609 - 8 - 1 = 600) has already passed on beat 601, while the short staged one can
        // still commit (lock 607). The preferred candidate must fail commit-eligibility, not delay the cue.
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new EventTransition(RepertoireFlags.None, runwayBeats: 1, tailBeats: 0), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 8, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Transition preference must not delay a valid scheduled cue.");
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.NextTransitionIndex, Is.EqualTo(0));
        Assert.That(controller.currentTransition, Is.EqualTo(0));
    }

    [Test]
    public void SameWindowBeatRewindKeepsCueSheetAndMovesCursorBack()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            // Beat 588 is the 593 mark's Lock Point (Runway 4), and beat 637 is the 641 boundary's,
            // so no cue commits anywhere in this test: it pins pure sheet/cursor mechanics.
            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.CueMark));

            SetTrackPhaseBeat(637, beatsUntilPhraseEnd: 4, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(641));

            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
            Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "No beat in this test is commit-eligible for its mark.");
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(int.MinValue));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void SameWindowBeatRewindLetsDirectorCueSameCueMarkAgain()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);
            // Beat 586 is before the 593 mark's Lock Point 588, so the sheet builds and the cue
            // commits on this same first synced beat.
            SetTrackPhaseBeat(586, beatsUntilPhraseEnd: 55, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "The setup should commit the Cue Mark on the first pass.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));

            SetTrackPhaseBeat(637, beatsUntilPhraseEnd: 4, phraseLengthBeats: 64);
            director.Tick(0f);
            director.SetNextEffect(2);

            SetTrackPhaseBeat(586, beatsUntilPhraseEnd: 55, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.CueMark));
            Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(2), "The rewound loop pass should be allowed to commit the same Cue Mark again.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void LoopReplayPastACommittedCueMarkDoesNotRepresentOrRecommitIt()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);
            SetTrackPhaseBeat(586, beatsUntilPhraseEnd: 55, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "Setup: the 593 Cue Mark commits on the first pass.");
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));

            SetTrackPhaseBeat(637, beatsUntilPhraseEnd: 4, phraseLengthBeats: 64);
            director.Tick(0f);
            director.SetNextEffect(2);

            // The loop replays from 597 — after the committed mark. The pass-local commit memory
            // (593 < 597) survives this rewind, so the committed mark must not be re-presented or
            // re-committed; only a replay from before the mark re-arms it. The replay's own commit
            // may only aim at the NEXT mark.
            SetTrackPhaseBeat(597, beatsUntilPhraseEnd: 44, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(641), "The committed mark must not be re-presented inside its window.");
            Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(641), "Any cue committed on the replay aims at the next mark, never the consumed one.");
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(641));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void DifferentLengthPhraseUpdateBeforeCommitRetargetsTheCurrentCueMark()
    {
        const int staleCueMarkBeat = 593;
        const int currentCueMarkBeat = 609;
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);

            // Beat 588 is the stale mark's Lock Point, so it is presented but can no longer commit.
            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(staleCueMarkBeat));
            Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
            Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));

            SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
            director.Tick(0f);

            Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(currentCueMarkBeat), "The rebuilt sheet's mark commits, not the stale one.");
            Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(currentCueMarkBeat));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void PhraseAndStagedChangesAfterLockDoNotMutateTheLockedCue()
    {
        const int sentCueMarkBeat = 609;
        director.SetNextEffect(1);
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "The setup should commit the cue toward 609.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(sentCueMarkBeat));

        // Reach the Lock Point in wall time (beat 604 = 0.5s past the beat-603 commit at 120 BPM)
        // without reaching the Start Beat (beat 605 = 1s).
        RenderTransitionAt(0.6f);
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "Setup: the loaded cue locks at its Lock Point.");

        controller.transitions[1] = new ZeroRunwayTailedTransition();
        controller.transitions[1].BindController(controller);
        controller.transitions[1].Init();
        director.SetNextTransition(1);
        director.SetNextEffect(2);
        SetTrackPhaseBeat(606, beatsUntilPhraseEnd: 35, phraseLengthBeats: 64);
        director.Tick(0f);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(sentCueMarkBeat), "A locked cue fires as-is; later Phrase evidence cannot re-aim it.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "Later staging cannot rewrite the locked cue's cast.");
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0), "A newly staged Transition cannot rewrite the locked cue.");
    }

    [Test]
    public void HeldEffectSuppressesSyncedCueCommand()
    {
        const int heldEffectIndex = 1;
        const int stagedEffectIndex = 2;
        controller.heldEffect = heldEffectIndex;
        director.SetNextEffect(stagedEffectIndex);
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Hold));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A held cue loads nothing into the Switcher.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.Not.EqualTo(stagedEffectIndex));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(-1));
    }


    [Test]
    public void HeldEffectStillRefreshesOnAirTiming()
    {
        controller.heldEffect = 1;
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Hold));
        Assert.That(director.Status.HasCueMark, Is.True);
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(director.Status.CueSheet.HasSheet, Is.True);
    }

    [Test]
    public void LiveButIdleSentinelDropsToStandaloneAndAbortsLoadedCue()
    {
        // OSC connected but idle: the source stays live while the wire carries 4-count/tempo sentinels.
        // A cue was loaded while Synced (beat 7 is before the mark-20 cue's Lock Point 15); the clock
        // then drops. The Director must reach Standalone (not freeze on a dead return) and abort the
        // Switcher-held cue so it cannot fire into a dead clock.
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 20,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: controller.transitions[0].Repertoire);
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(currentBeat: 7, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));
        Assume.That(switcher.LoadedCueStatus.HasCue, Is.True, "A cue should be loaded while Synced.");

        controller.beatManager.beatData.snapshot.bpm = -1f;
        controller.beatManager.beatData.snapshot.beatInBar = -1;
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = -1, total = -1 };

        var timerBefore = controller.timer.Value;
        director.Tick(2f);

        Assert.That(controller.beatManager.IsLiveSource, Is.True, "The OSC source is still connected.");
        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Standalone));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Entering Standalone aborts the loaded cue.");
        Assert.That(controller.timer.Value, Is.GreaterThan(timerBefore), "The Standalone rotation timer keeps ticking.");
    }

    [Test]
    public void DropAlignedSentCueReportsDeckFindCastInLastCueDecision()
    {
        Assert.That(director.Status.LastCue.Outcome, Is.EqualTo(CueDecisionOutcome.None), "No decision is reported before the first terminal cue decision.");

        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Sent));
        Assert.That(decision.Beat, Is.EqualTo(601));
        Assert.That(decision.ImpactBeat, Is.EqualTo(609));
        Assert.That(decision.BeatsBeforeImpact, Is.EqualTo(8));
        Assert.That(decision.EventIntent, Is.EqualTo(CueEventIntent.Drop));
        Assert.That(decision.PreferredRepertoire, Is.EqualTo(RepertoireFlags.HandlesDrop));
        Assert.That(decision.EffectIndex, Is.EqualTo(2), "The Drop-capable Performer found on the deck is the reported cast.");
        Assert.That(decision.EffectSource, Is.EqualTo(CueCastSource.DeckFind));
        Assert.That(decision.TransitionSource, Is.EqualTo(CueCastSource.NoPreferredAvailable), "No transition in this catalog handles Drops, so the staged one stood in.");
    }

    [Test]
    public void OrdinarySentCueReportsStagedCastInLastCueDecision()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Sent));
        Assert.That(decision.EventIntent, Is.EqualTo(CueEventIntent.Ordinary));
        Assert.That(decision.PreferredRepertoire, Is.EqualTo(RepertoireFlags.None));
        Assert.That(decision.EffectIndex, Is.EqualTo(switcher.LoadedCueStatus.TargetEffectIndex));
        Assert.That(decision.EffectSource, Is.EqualTo(CueCastSource.Staged));
        Assert.That(decision.TransitionSource, Is.EqualTo(CueCastSource.Staged));
    }

    [Test]
    public void DropProtectedDecisionReportsTheProtectedOnStagePerformer()
    {
        SetUpcomingDrop(beatsUntilStart: 8);
        SetTrackPhaseBeat(601, beatsUntilPhraseEnd: 8, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(2), "Setup: the first Drop cue casts the Drop-capable Performer.");
        RenderTransitionPastCompletion();

        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(621, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.DropProtected));
        Assert.That(decision.EventIntent, Is.EqualTo(CueEventIntent.Drop));
        Assert.That(decision.ImpactBeat, Is.EqualTo(625));
        Assert.That(decision.EffectIndex, Is.EqualTo(2), "The protected on-stage Performer is reported, not a cast target.");
        Assert.That(decision.EffectSource, Is.EqualTo(CueCastSource.None));
    }

    [Test]
    public void CadenceBlockedMarkReportsBlockedDecision()
    {
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609), "Setup: a cue commits the 609 mark.");

        // Beat 611 is still commit-eligible for the 617 boundary (lock 612), but 617 lands only
        // 8 beats after the committed 609 change — cadence blocks it.
        SetTrackPhaseBeat(611, beatsUntilPhraseEnd: 6, phraseLengthBeats: 8);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.BlockedByCadence));
        Assert.That(decision.Beat, Is.EqualTo(611));
        Assert.That(decision.ImpactBeat, Is.EqualTo(617));
        Assert.That(decision.EffectIndex, Is.EqualTo(-1), "A blocked mark casts nothing.");
    }

    [Test]
    public void HeldCueReportsHeldDecisionWithoutSendingAnything()
    {
        controller.heldEffect = 1;
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);

        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Held));
        Assert.That(decision.ImpactBeat, Is.EqualTo(609));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A held cue sends nothing to the Switcher.");
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
    }

    [Test]
    public void SwitcherRejectsCueArrivingAtItsLockPointAndKeepsCurrentEffectOnStage()
    {
        // Defense in depth behind the Director's own commit gate: a cue upserted at its Lock Point
        // (mark 20, Runway 4 -> lock beat 15) must not load — nothing fires late, nothing backdates.
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 20,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: controller.transitions[0].Repertoire);
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(currentBeat: 15, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A late cue is rejected outright; nothing loads.");
        RenderTransitionPastCompletion();
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "No transition may ever start for a rejected cue.");
    }

    // A present phrase_state always describes the *current* Phrase (OSC schema v2): its countBeats is the
    // countdown to the boundary, its lengthBeats the Phrase's own length. next_phrase_state is cleared so a
    // stale look-ahead from an earlier frame cannot leak across a tick.
    private void SetTrackPhaseBeat(int beat, int beatsUntilPhraseEnd, int phraseLengthBeats)
    {
        controller.beatManager.beatData.snapshot.bpm = 120f;
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = beat, total = -1 };
        controller.beatManager.beatData.snapshot.beatInBar = ((beat - 1) % 4) + 1;
        controller.beatManager.beatData.snapshot.phraseState = new PhraseState
        {
            label = "Phrase",
            countBeats = beatsUntilPhraseEnd,
            lengthBeats = phraseLengthBeats,
            irregular = 0,
        };
        controller.beatManager.beatData.snapshot.nextPhraseState = LabeledCountdown.Unavailable;
    }

    // No current Phrase, only the next one counting down to its start with its own announced length —
    // the v2 look-ahead the CuePlanner builds an upcoming sheet from.
    private void SetUpcomingPhraseBeat(int beat, int nextStartInBeats, int nextPhraseLengthBeats)
    {
        controller.beatManager.beatData.snapshot.bpm = 120f;
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = beat, total = -1 };
        controller.beatManager.beatData.snapshot.beatInBar = ((beat - 1) % 4) + 1;
        controller.beatManager.beatData.snapshot.phraseState = PhraseState.Unavailable;
        controller.beatManager.beatData.snapshot.nextPhraseState = new LabeledCountdown
        {
            label = "Next",
            countBeats = nextStartInBeats,
            lengthBeats = nextPhraseLengthBeats,
        };
    }

    private void SetUpcomingDrop(int beatsUntilStart)
    {
        controller.beatManager.beatData.snapshot.dropState = new CountdownState
        {
            active = 0,
            countBeats = beatsUntilStart,
            lengthBeats = 16,
            remaining = 1,
        };
    }

    private void SetUpcomingFill(int beatsUntilStart)
    {
        controller.beatManager.beatData.snapshot.fillState = new CountdownState
        {
            active = 0,
            countBeats = beatsUntilStart,
            lengthBeats = 8,
            remaining = 1,
        };
    }

    private void RebuildSwitcherAndDirectorWithTransitions(TransitionBase[] transitions, int[] transitionDeck)
    {
        controller.transitions = transitions;
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.transitionDeck = transitionDeck;
        controller.currentTransition = transitionDeck[0];
        switcher = new Switcher(controller, controller.effects, controller.transitions);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;
        director = new Director(
            controller,
            switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;
    }

    private void RenderTransitionPastCompletion()
    {
        switcher.RenderAtTime(Time.time + 10f, out _);
    }

    // Advances Switcher wall time by a fixed amount, so tests can observe the loaded cue between its
    // Lock Point and Start Beat, or the transition mid-flight (a loaded cue fires on time, not at commit).
    private void RenderTransitionAt(float secondsFromNow)
    {
        switcher.RenderAtTime(Time.time + secondsFromNow, out _);
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    private sealed class TestEffect : EffectBase
    {
        private readonly Repertoire repertoire;

        public TestEffect(Repertoire repertoire = Repertoire.None)
        {
            this.repertoire = repertoire;
            buffer = new Color[Penrose.Total];
        }

        public override Repertoire Repertoire => repertoire;

        public override string DebugText() => string.Empty;

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

    private sealed class HardCutTransition : TransitionBase
    {
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

    private sealed class TailedTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 4,
                tailBeats: 4,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
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

    private sealed class EventTransition : TransitionBase
    {
        private readonly RepertoireFlags tags;
        private readonly int runwayBeats;
        private readonly int tailBeats;

        public EventTransition(RepertoireFlags tags, int runwayBeats, int tailBeats)
        {
            this.tags = tags;
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                tags,
                runwayBeats,
                tailBeats,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
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

    private sealed class ZeroRunwayTailedTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 0,
                tailBeats: 12,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 12f));
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
}
