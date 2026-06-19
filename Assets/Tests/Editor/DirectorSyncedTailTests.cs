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
        controller.transitions = new TransitionBase[] { new TailedTransition() };
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

        Assert.That(switcher.IsTransitioning, Is.True, "The setup should cue a tailed transition toward beat 609.");

        ForceTransitionCompleteAtImpact(impactBeat: 609);
        SetTrackPhaseBeat(613, phaseActive: 0, beatsToPhraseBoundary: 10, phraseLengthBeats: 32);
        director.Tick(0f);

        Assert.That(switcher.IsTransitioning, Is.False, "The tailed transition should have completed.");
        Assert.That(director.Status.PhaseAnchorLandingBeat, Is.EqualTo(623));
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

    private void ForceTransitionCompleteAtImpact(int impactBeat)
    {
        var repertoire = controller.transitions[0].Repertoire;
        var beatPlan = TransitionBeatPlan.FromImpactBeat(impactBeat, repertoire);
        var completePlan = new SyncedTransitionPlan(
            transitionIndex: 0,
            targetEffectIndex: switcher.TransitionTargetEffectIndex,
            beatPlan,
            repertoire,
            startTime: Time.time - repertoire.DurationBeats,
            secondsPerBeat: 1f);
        typeof(Director)
            .GetField("transitionPlan", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(director, completePlan);
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    private sealed class TestEffect : EffectBase
    {
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
