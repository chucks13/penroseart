using NUnit.Framework;
using UnityEngine;

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
        controller.logDirectorSwitching = false;

        transition = new TimedTransition();
        hardCutTransition = new HardCutTransition();
        var effects = new EffectBase[] { new SolidEffect(Color.red), new SolidEffect(Color.blue) };
        var transitions = new TransitionBase[] { transition, hardCutTransition };
        controller.effects = effects;
        controller.transitions = transitions;
        transition.Init();
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
                global::Repertoire.None,
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
                global::Repertoire.None,
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
