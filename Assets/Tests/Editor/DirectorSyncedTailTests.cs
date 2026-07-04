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
    public void DirectorWaitsBeforeTransitionRunway()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForRunway));
    }

    [Test]
    public void DirectorUsesLatestStagedEffectWhenCueWindowArrives()
    {
        director.SetNextEffect(1);
        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);

        director.SetNextEffect(2);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void SentCueStartsAtRunwayStartBeforeCueMark()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));

        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.TransitionProgress, Is.LessThan(1f));
    }

    [Test]
    public void TailedTransitionCompletionKeepsNextAnchorOnUpcomingTrackPhaseBoundary()
    {
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The setup should cue a tailed transition toward beat 609.");

        RenderTransitionPastCompletion();
        SetTrackPhaseBeat(613, beatsUntilPhraseEnd: 12, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0), "The tailed transition should have completed.");
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(625));
    }

    [Test]
    public void StartingTailedTransitionMarksCadenceAtCueMark()
    {
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The setup should cue a tailed transition toward beat 609.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void ZeroRunwayTailedTransitionCanCueWhenExactImpactBeatIsMissed()
    {
        controller.transitions[0] = new ZeroRunwayTailedTransition();
        controller.transitions[0].BindController(controller);
        controller.transitions[0].Init();
        director.SetNextTransition(0);

        SetTrackPhaseBeat(608, beatsUntilPhraseEnd: 1, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(609));
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 31, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void NextTransitionCanCueAfterTailedTransitionCompletes()
    {
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        RenderTransitionPastCompletion();

        SetTrackPhaseBeat(621, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Director should cue the next transition as soon as cadence allows after Tail completion.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
    }

    [Test]
    public void DecisionMatrixFollowsCueMarkWhileTailedTransitionIsStillRendering()
    {
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The previous transition Tail is still mechanically rendering in this test.");
        Assert.That(director.Status.CueMarkBeat, Is.EqualTo(625));
        Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForRunway));
    }

    [Test]
    public void StartingTailedTransitionImmediatelyStagesFollowingMove()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(director.Status.NextEffectIndex, Is.Not.EqualTo(1), "The consumed target should not remain staged until Tail completion.");
    }

    [Test]
    public void SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        SetTrackPhaseBeat(606, beatsUntilPhraseEnd: 3, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.SourceEffectIndex, Is.EqualTo(0), "Restarting the same cue would replace the source with the previous target.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void FiredCueLoadsTheUpcomingPhraseCueMarkOnceTheReadingCountsDown()
    {
        director.SetNextEffect(1);

        // Cue toward the current Phrase's 609 boundary (runway 4) and let it fire.
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "Setup: the mandatory boundary cues on 609.");
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));

        director.SetNextEffect(2);

        // The 609 boundary fired; the feed now counts down to the next Phrase (its own length 64) starting
        // in 2 beats via next_phrase_state. The upcoming-phrase sheet loads its first cue mark past 609.
        SetUpcomingPhraseBeat(607, nextStartInBeats: 2, nextPhraseLengthBeats: 64);
        director.Tick(0f);

        Assert.That(director.Status.HasCueMark, Is.True);
        Assert.That(director.Status.CueMarkBeat, Is.GreaterThan(609), "The upcoming Phrase's cue mark loads past the fired boundary.");
        Assert.That((director.Status.CueMarkBeat - 1) % 16, Is.EqualTo(0), "Upcoming cue marks land on a 16-grid boundary.");
    }

    [Test]
    public void MandatoryPhraseBoundaryRemainsCueableWhenNextPhraseFrameArrivesOnTheBoundary()
    {
        controller.transitions[0] = new HardCutTransition();
        controller.transitions[0].BindController(controller);
        controller.transitions[0].Init();
        director.SetNextTransition(0);

        SetTrackPhaseBeat(594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32);
        director.Tick(0f);
        SetTrackPhaseBeat(609, beatsUntilPhraseEnd: 64, phraseLengthBeats: 64);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "The mandatory phrase boundary should still cue even after Track Phase advances to the next Phrase Window.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void DropAlignedCueCastsDropCapablePerformerWhenAvailable()
    {
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Director should start a synced transition on the Drop runway.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The Drop-aligned cue should cast the available Drop-capable Performer.");
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
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "A held cue must not start a transition.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBeforeTick), "Deck candidates rotate only when a cue is actually sent; a held cue sends none.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckBeforeTick), "Deck candidates rotate only when a cue is actually sent; a held cue sends none.");
    }

    [Test]
    public void DropAlignedCuePreservesManualStagedPerformerWhenDropCapablePerformerIsAvailable()
    {
        director.SetNextEffect(1);
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Director should start a synced transition on the Drop runway.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "Manual staging should preserve the chosen Performer instead of recasting from the deck.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void DropAlignedCueCastsDropCapableTransitionWhenAvailable()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(1));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void FillAlignedCueCastsFillCapableTransitionWhenAvailable()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesFill, runwayBeats: 4, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingFill(beatsUntilStart: 3);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(1));
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
        SetUpcomingDrop(beatsUntilStart: 1);
        SetTrackPhaseBeat(608, beatsUntilPhraseEnd: 1, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0));
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
    }

    [Test]
    public void EventAlignedCuePreservesManualTransitionWithoutHold()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new TailedTransition(), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 2, tailBeats: 0) },
            new[] { 0, 1 });
        director.SetNextTransition(0);
        SetUpcomingDrop(beatsUntilStart: 2);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0));
        Assert.That(director.Status.NextTransitionIndex, Is.Not.EqualTo(1), "Manual staging should not be recast by event-aware transition selection.");
    }

    [Test]
    public void EventAlignedCueKeepsStagedTransitionWhenPreferredTransitionCannotCueNow()
    {
        RebuildSwitcherAndDirectorWithTransitions(
            new TransitionBase[] { new EventTransition(RepertoireFlags.None, runwayBeats: 4, tailBeats: 0), new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 1, tailBeats: 0) },
            new[] { 0, 1 });
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "Transition preference must not delay a valid scheduled cue.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0));
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
            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.CueMark));

            SetTrackPhaseBeat(620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(641));

            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
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
            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            SetTrackPhaseBeat(589, beatsUntilPhraseEnd: 52, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "The setup should cue the Cue Mark on the first pass.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));

            SetTrackPhaseBeat(620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64);
            director.Tick(0f);
            director.SetNextEffect(2);

            SetTrackPhaseBeat(589, beatsUntilPhraseEnd: 52, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.CueMark));
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The rewound loop pass should be allowed to cue the same Cue Mark again.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void LoopReplayIntoAFiredCueMarksWindowDoesNotRefireIt()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);
            SetTrackPhaseBeat(588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            SetTrackPhaseBeat(589, beatsUntilPhraseEnd: 52, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "Setup: the Cue Mark cues on the first pass.");
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));

            SetTrackPhaseBeat(620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64);
            director.Tick(0f);
            director.SetNextEffect(2);

            // The loop replays from 597 — after the fired mark, inside its late-cue window (593 +
            // Tail 4). The pass-local commit memory (593 < 597) survives this rewind, so the fired
            // mark must not be re-presented or re-fired; only a replay from before the mark re-arms it.
            SetTrackPhaseBeat(597, beatsUntilPhraseEnd: 44, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(641), "The fired mark must not be re-presented inside its window.");
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "No new cue may fire for the already-committed mark.");
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void DifferentLengthPhraseUpdateBeforeCueWindowUsesCurrentCueMark()
    {
        const int staleCueMarkBeat = 593;
        const int currentCueMarkBeat = 609;
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);

            SetTrackPhaseBeat(584, beatsUntilPhraseEnd: 57, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(staleCueMarkBeat));
            Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));

            SetTrackPhaseBeat(603, beatsUntilPhraseEnd: 6, phraseLengthBeats: 32);
            director.Tick(0f);
            Assert.That(director.Status.CueMarkBeat, Is.EqualTo(currentCueMarkBeat));
            Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForRunway));

            SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
            director.Tick(0f);

            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(currentCueMarkBeat));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void PhraseAndStagedChangesAfterSentCueDoNotMutateSwitcherCommand()
    {
        const int sentCueMarkBeat = 609;
        director.SetNextEffect(1);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "The setup should send the cue-window command.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(sentCueMarkBeat));

        controller.transitions[1] = new ZeroRunwayTailedTransition();
        controller.transitions[1].BindController(controller);
        controller.transitions[1].Init();
        director.SetNextTransition(1);
        director.SetNextEffect(2);
        SetTrackPhaseBeat(606, beatsUntilPhraseEnd: 35, phraseLengthBeats: 64);
        director.Tick(0f);

        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "Later Phrase evidence should only affect future cue commands.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(0), "A newly staged Transition cannot rewrite the already-sent cue.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(sentCueMarkBeat));
    }

    [Test]
    public void HeldEffectSuppressesSyncedCueCommand()
    {
        const int heldEffectIndex = 1;
        const int stagedEffectIndex = 2;
        controller.heldEffect = heldEffectIndex;
        director.SetNextEffect(stagedEffectIndex);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Hold));
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
        // A cue was loaded while Synced; the clock then drops. The Director must reach Standalone (not
        // freeze on a dead return) and abort the Switcher-held cue so it cannot fire into a dead clock.
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

        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Sent));
        Assert.That(decision.Beat, Is.EqualTo(605));
        Assert.That(decision.ImpactBeat, Is.EqualTo(609));
        Assert.That(decision.BeatsBeforeImpact, Is.EqualTo(4));
        Assert.That(decision.EventIntent, Is.EqualTo(CueEventIntent.Drop));
        Assert.That(decision.PreferredRepertoire, Is.EqualTo(RepertoireFlags.HandlesDrop));
        Assert.That(decision.EffectIndex, Is.EqualTo(2), "The Drop-capable Performer found on the deck is the reported cast.");
        Assert.That(decision.EffectSource, Is.EqualTo(CueCastSource.DeckFind));
        Assert.That(decision.TransitionSource, Is.EqualTo(CueCastSource.NoPreferredAvailable), "No transition in this catalog handles Drops, so the staged one stood in.");
    }

    [Test]
    public void OrdinarySentCueReportsStagedCastInLastCueDecision()
    {
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Sent));
        Assert.That(decision.EventIntent, Is.EqualTo(CueEventIntent.Ordinary));
        Assert.That(decision.PreferredRepertoire, Is.EqualTo(RepertoireFlags.None));
        Assert.That(decision.EffectIndex, Is.EqualTo(switcher.Status.TargetEffectIndex));
        Assert.That(decision.EffectSource, Is.EqualTo(CueCastSource.Staged));
        Assert.That(decision.TransitionSource, Is.EqualTo(CueCastSource.Staged));
    }

    [Test]
    public void DropProtectedDecisionReportsTheProtectedOnStagePerformer()
    {
        SetUpcomingDrop(beatsUntilStart: 4);
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "Setup: the first Drop cue casts the Drop-capable Performer.");
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
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609), "Setup: a cue commits the 609 mark.");

        SetTrackPhaseBeat(613, beatsUntilPhraseEnd: 4, phraseLengthBeats: 8);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.BlockedByCadence));
        Assert.That(decision.Beat, Is.EqualTo(613));
        Assert.That(decision.ImpactBeat, Is.EqualTo(617));
        Assert.That(decision.EffectIndex, Is.EqualTo(-1), "A blocked mark casts nothing.");
    }

    [Test]
    public void HeldCueReportsHeldDecisionWithoutSendingAnything()
    {
        controller.heldEffect = 1;
        SetTrackPhaseBeat(605, beatsUntilPhraseEnd: 4, phraseLengthBeats: 32);

        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Held));
        Assert.That(decision.ImpactBeat, Is.EqualTo(609));
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "A held cue sends nothing to the Switcher.");
    }

    [Test]
    public void LateSentCueReportsHardCutImpactDistance()
    {
        controller.transitions[0] = new ZeroRunwayTailedTransition();
        controller.transitions[0].BindController(controller);
        controller.transitions[0].Init();
        director.SetNextTransition(0);
        SetTrackPhaseBeat(608, beatsUntilPhraseEnd: 1, phraseLengthBeats: 32);
        director.Tick(0f);
        Assert.That(director.Status.LastCue.Outcome, Is.EqualTo(CueDecisionOutcome.None), "Setup: nothing may fire before the impact beat for a zero-Runway transition.");

        SetTrackPhaseBeat(610, beatsUntilPhraseEnd: 31, phraseLengthBeats: 32);
        director.Tick(0f);

        var decision = director.Status.LastCue;
        Assert.That(decision.Outcome, Is.EqualTo(CueDecisionOutcome.Sent));
        Assert.That(decision.Beat, Is.EqualTo(610));
        Assert.That(decision.ImpactBeat, Is.EqualTo(609));
        Assert.That(decision.BeatsBeforeImpact, Is.EqualTo(-1), "A cue sent after its Impact Point lands as a hard cut.");
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
