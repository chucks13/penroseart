using System.Reflection;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

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
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.effects = new EffectBase[] { new TestEffect(), new TestEffect(), new TestEffect() };
        controller.transitions = new TransitionBase[] { new TailedTransition(), new TailedTransition() };
        foreach (var transition in controller.transitions)
        {
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
    public void TailedTransitionCompletionKeepsNextAnchorOnUpcomingTrackPhaseBoundary()
    {
        SetTrackPhaseBeat(605, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The setup should cue a tailed transition toward beat 609.");

        RenderTransitionPastCompletion();
        SetTrackPhaseBeat(613, phaseActive: 0, beatsToPhraseBoundary: 10, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.GreaterThanOrEqualTo(0), "The tailed transition should have completed.");
        Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(623));
    }

    [Test]
    public void StartingTailedTransitionMarksCadenceAtSelectedPhaseBoundary()
    {
        SetTrackPhaseBeat(605, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The setup should cue a tailed transition toward beat 609.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
        Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
    }

    [Test]
    public void NextTransitionCanCueAfterTailedTransitionCompletes()
    {
        SetTrackPhaseBeat(605, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);
        RenderTransitionPastCompletion();

        SetTrackPhaseBeat(621, phaseActive: 0, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Director should cue the next transition as soon as cadence allows after Tail completion.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(625));
    }

    [Test]
    public void DecisionMatrixFollowsSelectedBoundaryWhileTailedTransitionIsStillRendering()
    {
        SetTrackPhaseBeat(605, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        SetTrackPhaseBeat(610, phaseActive: 0, beatsToPhraseBoundary: 15, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The previous transition Tail is still mechanically rendering in this test.");
        Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(625));
        Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForRunway));
    }

    [Test]
    public void StartingTailedTransitionImmediatelyStagesFollowingMove()
    {
        director.SetNextEffect(1);

        SetTrackPhaseBeat(605, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(director.Status.NextEffectIndex, Is.Not.EqualTo(1), "The consumed target should not remain staged until Tail completion.");
    }

    [Test]
    public void TrackPhaseDisappearanceAfterAnchorCoastsAndStillCuesOnCoastedBoundary()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);

            SetTrackPhaseBeat(588, phaseActive: 1, beatsToPhraseBoundary: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            SetTrackPhaseUnavailableBeat(594);
            director.Tick(0f);
            Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Synced));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.Coast));
            Assert.That(director.Status.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(609));
            Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForRunway));

            SetTrackPhaseUnavailableBeat(605);
            director.Tick(0f);

            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.Coast));
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "The coasted anchor should still be a valid synced cue target.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(609));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(609));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void FreshTrackPhaseAfterCoastReportsReanchorInDirectorStatus()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);

            SetTrackPhaseBeat(588, phaseActive: 1, beatsToPhraseBoundary: 53, phraseLengthBeats: 64);
            director.Tick(0f);
            SetTrackPhaseUnavailableBeat(594);
            director.Tick(0f);
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.Coast));

            SetTrackPhaseBeat(600, phaseActive: 1, beatsToPhraseBoundary: 41, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.TimingReanchored, Is.True);
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
            Assert.That(director.Status.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(641));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void TrackPhaseDisappearanceWithoutPriorAnchorWaitsInSyncedMode()
    {
        director.SetNextEffect(1);

        SetTrackPhaseUnavailableBeat(605);
        director.Tick(0f);

        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Synced));
        Assert.That(director.Status.IsSyncedMode, Is.True);
        Assert.That(director.Status.HasPhaseAnchor, Is.False);
        Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(director.Status.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(director.Status.Decision, Is.EqualTo(DirectorDecision.WaitingForPhase));
        Assert.That(switcher.Status.TargetEffectIndex, Is.Not.EqualTo(1), "Unlocked timing should not cue the staged target.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(-1));

        SetTrackPhaseBeat(589, phaseActive: 1, beatsToPhraseBoundary: 4, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "Fresh structural timing should let the Director resume cueing from a valid boundary.");
        Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
    }

    [Test]
    public void SameWindowBeatRewindKeepsSelectedPhaseBoundaryPlanAndMovesCursorBack()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            SetTrackPhaseBeat(588, phaseActive: 1, beatsToPhraseBoundary: 53, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));

            SetTrackPhaseBeat(620, phaseActive: 1, beatsToPhraseBoundary: 21, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(641));

            SetTrackPhaseBeat(588, phaseActive: 1, beatsToPhraseBoundary: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(int.MinValue));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    [Test]
    public void SameWindowBeatRewindLetsDirectorCueSameSelectedBoundaryAgain()
    {
        var randomState = Random.state;
        try
        {
            Random.InitState(20);
            director.SetNextEffect(1);
            SetTrackPhaseBeat(588, phaseActive: 1, beatsToPhraseBoundary: 53, phraseLengthBeats: 64);
            director.Tick(0f);

            SetTrackPhaseBeat(589, phaseActive: 1, beatsToPhraseBoundary: 52, phraseLengthBeats: 64);
            director.Tick(0f);
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "The setup should cue the selected boundary on the first pass.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));

            SetTrackPhaseBeat(620, phaseActive: 1, beatsToPhraseBoundary: 21, phraseLengthBeats: 64);
            director.Tick(0f);
            director.SetNextEffect(2);

            SetTrackPhaseBeat(589, phaseActive: 1, beatsToPhraseBoundary: 52, phraseLengthBeats: 64);
            director.Tick(0f);

            Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.TimingSource, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));
            Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2), "The rewound loop pass should be allowed to cue the same selected boundary again.");
            Assert.That(director.Status.TransitionLandingBeat, Is.EqualTo(593));
            Assert.That(director.Status.LastChangeBeat, Is.EqualTo(593));
        }
        finally
        {
            Random.state = randomState;
        }
    }

    private void SetTrackPhaseBeat(int beat, int phaseActive, int beatsToPhraseBoundary, int phraseLengthBeats)
    {
        controller.beatManager.beatData.bpm = 120f;
        controller.beatManager.beatData.beat = new BeatPosition { current = beat, total = -1 };
        controller.beatManager.beatData.beatInBar = ((beat - 1) % 4) + 1;
        controller.beatManager.beatData.phaseState = new PhaseState
        {
            current = "Phrase",
            next = "Next",
            active = phaseActive,
            countBeats = beatsToPhraseBoundary,
            lengthBeats = phraseLengthBeats,
            remaining = 1,
        };
    }

    private void SetTrackPhaseUnavailableBeat(int beat)
    {
        controller.beatManager.beatData.bpm = 120f;
        controller.beatManager.beatData.beat = new BeatPosition { current = beat, total = -1 };
        controller.beatManager.beatData.beatInBar = ((beat - 1) % 4) + 1;
        controller.beatManager.beatData.phaseState = new PhaseState
        {
            current = string.Empty,
            next = string.Empty,
            active = -1,
            countBeats = -1,
            lengthBeats = -1,
            remaining = -1,
        };
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
        public TestEffect()
        {
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
        }
    }

    private sealed class TailedTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                global::Repertoire.None,
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
}
