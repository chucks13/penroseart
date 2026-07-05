using NUnit.Framework;
using UnityEngine;
using RepertoireFlags = Repertoire;

public sealed class SwitcherExecutionTests
{
    private GameObject controllerObject;
    private Switcher switcher;
    private TimedTransition transition;
    private HardCutTransition hardCutTransition;

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("SwitcherExecutionTestsController");
        var controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;

        transition = new TimedTransition();
        hardCutTransition = new HardCutTransition();
        var effects = new EffectBase[] { new SolidEffect(Color.red), new SolidEffect(Color.blue) };
        var transitions = new TransitionBase[] { transition, hardCutTransition };
        controller.effects = effects;
        controller.transitions = transitions;
        transition.BindController(controller);
        transition.Init();
        hardCutTransition.BindController(controller);
        hardCutTransition.Init();
        switcher = new Switcher(controller, effects, transitions);
        switcher.SetInitialEffect(0, 0);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void LoadedCueStartsFromScheduledBeatDomainClock()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(
            currentBeat: 7,
            beatFraction: 0f,
            secondsPerBeat: 0.5f,
            nowSeconds: 10f));

        var buffer = switcher.RenderAtTime(11.25f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(transition.V, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(buffer[0], Is.EqualTo(Color.Lerp(Color.red, Color.blue, 0.5f)));
    }

    [Test]
    public void LoadedCueCanBeReplacedBeforeLock()
    {
        var originalCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var replacementCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        switcher.UpsertLoadedCue(originalCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));
        switcher.UpsertLoadedCue(replacementCue, new SwitcherClockSnapshot(7, 0.5f, 0.5f, 10.25f));

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.CanUpdate, Is.True);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(0));
    }

    [Test]
    public void LoadedCueIgnoresReplacementAtLockPoint()
    {
        var originalCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);
        var replacementCue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 0,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        switcher.UpsertLoadedCue(originalCue, new SwitcherClockSnapshot(7, 0f, 0.5f, 10f));
        switcher.UpsertLoadedCue(replacementCue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10.5f));

        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1));
    }

    [Test]
    public void ZeroRunwayHardCutLoadedCuePromotesDestinationOnCueMark()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 1,
            transitionRepertoire: hardCutTransition.Repertoire);

        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10f));
        var buffer = switcher.RenderAtTime(11f, out _);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    [Test]
    public void CueArrivingAtOrPastItsLockPointIsRejected()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        // Runway 1 puts the Lock Point on beat 8: a cue arriving on it is already too late.
        switcher.UpsertLoadedCue(cue, new SwitcherClockSnapshot(8, 0f, 0.5f, 10f));
        var buffer = switcher.RenderAtTime(12f, out _);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.red));
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
        public TimedTransition()
        {
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 1,
                tailBeats: 0,
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
