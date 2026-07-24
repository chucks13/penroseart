using NUnit.Framework;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Contract tests for the contracted Switcher (ADR-0019): fire-and-forget Cast plus the Standalone
// seconds path. The loaded-cue lifecycle (UpsertLoadedCue, Lock Point, kept/loaded/rejected verdict,
// ActiveCueStatus/LoadedCueStatus) is deleted, so its tests are gone with it.
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

    private static TransitionRepertoire RepertoireFor(int runwayBeats, int tailBeats)
    {
        return TransitionRepertoire.FromRunwayAndTail(
            RepertoireFlags.None,
            runwayBeats,
            tailBeats,
            TransitionShape.Blend,
            TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
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

    #region Fire-and-forget cast (decide-at-cast)

    /// <summary>
    /// On-time cast: the Runway begins now, the Impact Point lands on the Cue Mark beat, and the Tail
    /// completes after — the whole fire-and-forget contract in one flight.
    /// </summary>
    [Test]
    public void CastRunsFullRunwayImpactOnMarkThenCompletesAfterTail()
    {
        // Runway 2, Tail 2 (Duration 4 beats): Impact Point sits at progress 0.5. Cue Mark 10 means the
        // Runway beat is 8; the Director casts exactly there.
        var repertoire = RepertoireFor(runwayBeats: 2, tailBeats: 2);
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: repertoire);

        switcher.Cast(cue, new SwitcherClockSnapshot(currentBeat: 8, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));

        // Runway begins now: the transition is live and starts from the top of its Runway.
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Runway is under way the instant the cast lands.");
        Assert.That(switcher.Status.TransitionProgress, Is.EqualTo(0f).Within(0.001f), "An on-time cast runs the full Runway from zero.");

        // Cue Mark beat 10 is two beats after the Runway beat: 10f + 2 * 0.5f = 11f.
        switcher.RenderAtTime(11f, out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Impact Point is mid-flight, not completion.");
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(repertoire.ImpactPoint).Within(0.001f),
            "The Impact Point lands exactly on the Cue Mark beat.");

        // Completion beat is Cue Mark + Tail = 12: 10f + 4 * 0.5f = 12f.
        var buffer = switcher.RenderAtTime(12f, out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "The Tail completes after the Impact.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>
    /// A late cast still fires: it begins already under way (a compressed Runway) rather than being
    /// refused, and its Impact still lands on the Cue Mark beat.
    /// </summary>
    [Test]
    public void LateCastFiresAsCompressedRunwayWithImpactStillOnMark()
    {
        var repertoire = RepertoireFor(runwayBeats: 2, tailBeats: 2);
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: repertoire);

        // Runway beat is 8, but the Director is a beat late and casts on beat 9 (wall time 10.5f). The
        // transition starts from the Runway beat's time (10f), already 0.25 of the way in at cast.
        switcher.Cast(cue, new SwitcherClockSnapshot(currentBeat: 9, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10.5f));

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "A late cast fires rather than being refused.");
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(0.25f).Within(0.001f),
            "The Runway is compressed: the transition is already under way at cast.");

        // Cue Mark beat 10 is still wall time 11f; the Impact still lands there despite the late cast.
        switcher.RenderAtTime(11f, out _);
        Assert.That(
            switcher.Status.TransitionProgress,
            Is.EqualTo(repertoire.ImpactPoint).Within(0.001f),
            "Compression preserves the Impact on the Cue Mark beat.");

        var buffer = switcher.RenderAtTime(12f, out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>A hard cut (zero Runway, zero Tail) promotes its destination immediately on the Cue Mark.</summary>
    [Test]
    public void HardCutCastPromotesDestinationImmediatelyOnTheMark()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 1,
            transitionRepertoire: hardCutTransition.Repertoire);

        // Zero Runway means the Runway beat is the Cue Mark itself; the Director casts on beat 10.
        switcher.Cast(cue, new SwitcherClockSnapshot(currentBeat: 10, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(1), "A zero-duration cast promotes without a render tick.");
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));

        var buffer = switcher.RenderAtTime(10f, out _);
        Assert.That(buffer[0], Is.EqualTo(Color.blue));
    }

    /// <summary>The fire-and-forget path executes unconditionally and parks no cue: a transition simply owns the stage.</summary>
    [Test]
    public void CastFiresUnconditionallyWithNoParkedCue()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 0,
            transitionRepertoire: transition.Repertoire);

        switcher.Cast(cue, new SwitcherClockSnapshot(currentBeat: 9, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The cast fired: a transition owns the stage.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(1), "The cast aimed the transition at its target effect.");
    }

    /// <summary>The Standalone seconds-based transition path still runs on its default duration after a cast.</summary>
    [Test]
    public void StandaloneSecondsPathIsUnaffectedByCast()
    {
        var cue = new SwitcherCueDirection(
            cueMarkBeat: 10,
            targetEffectIndex: 1,
            transitionIndex: 1,
            transitionRepertoire: hardCutTransition.Repertoire);
        switcher.Cast(cue, new SwitcherClockSnapshot(currentBeat: 10, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));

        // A Standalone-mode transition uses the repertoire's default seconds, untouched by the prior
        // beat-domain cast: TimedTransition's one-second duration promotes at 21.1f.
        switcher.StartTransition(0, 0, TransitionStartTiming.FromDefaultDuration(startTime: 20f));
        var buffer = switcher.RenderAtTime(21.1f, out _);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0));
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(-1));
        Assert.That(buffer[0], Is.EqualTo(Color.red));
    }

    #endregion

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
