// Contract tests for Controller-owned rhythm stepping and live Performer access roots.

using System.Collections;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Tests Controller frame ownership and the live rhythm roots exposed to Performers.
/// </summary>
public sealed class PerformerAccessPathTests
{
    /// <summary>Returns the Editor to Edit Mode when a PlayMode seam test fails before its explicit cleanup.</summary>
    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterEachTest()
    {
        if (Application.isPlaying)
        {
            yield return new ExitPlayMode();
        }
    }

    /// <summary>
    /// Proves Unity component creation establishes both live roots before Performer Init and OnStart.
    /// </summary>
    [UnityTest]
    public IEnumerator ControllerCreationMakesLiveRhythmRootsAvailableToEffectAndTransitionLifecycle()
    {
        yield return new EnterPlayMode();

        var controllerObject = new GameObject("performer-access-controller");
        var controller = controllerObject.AddComponent<Controller>();
        var liveBeatManager = controller.beatManager;
        var liveWaveforms = controller.waveforms;
        var effect = new LifecycleEffect();
        effect.BindController(controller);
        effect.Init();
        effect.OnStart();

        var transition = new LifecycleTransition();
        transition.BindController(controller);
        transition.Init();
        transition.OnStart();

        Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(liveBeatManager, Is.Not.Null);
        Assert.That(liveWaveforms, Is.Not.Null);
        Assert.That(effect.InitBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(effect.StartBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(effect.InitWaveforms, Is.SameAs(liveWaveforms));
        Assert.That(effect.StartWaveforms, Is.SameAs(liveWaveforms));
        Assert.That(transition.InitBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(transition.StartBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(transition.InitWaveforms, Is.SameAs(liveWaveforms));
        Assert.That(transition.StartWaveforms, Is.SameAs(liveWaveforms));
    }

    /// <summary>Proves a Mixer-owned child receives ordinary Effect access without a Mixer rhythm seam.</summary>
    [UnityTest]
    public IEnumerator MixerOwnedChildUsesOrdinaryEffectRhythmRoots()
    {
        yield return new EnterPlayMode();

        var controllerObject = new GameObject("performer-access-mixer-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.penrose = controllerObject.AddComponent<Penrose>();
        var liveBeatManager = controller.beatManager;
        var liveWaveforms = controller.waveforms;
        var mixer = new OwningMixer();
        mixer.BindController(controller);
        mixer.Init();
        var child = mixer.GetRandomEffect();
        var childBeatManager = child.beatManager;
        var childWaveforms = child.waveforms;

        Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(liveBeatManager, Is.Not.Null);
        Assert.That(liveWaveforms, Is.Not.Null);
        Assert.That(childBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(childWaveforms, Is.SameAs(liveWaveforms));
    }

    /// <summary>Proves Effect owners can directly share, replace, or explicitly suppress Waveform configuration.</summary>
    [Test]
    public void EffectWaveformConfigurationIsPublicAndNonNullable()
    {
        EffectBase first = new BaseStartEffect();
        EffectBase second = new BaseStartEffect();
        var waveforms = new Waveforms(new BeatManager(), new[]
        {
            new WaveformPool.Entry("beat pulse", Waveform.Parse("QQQQ", "8888")),
        });
        var shared = waveforms.Random();

        first.waveform = shared;
        second.waveform = first.waveform;

        Assert.That(first.waveform, Is.EqualTo(shared));
        Assert.That(second.waveform, Is.EqualTo(shared));

        second.waveform = waveforms.None;

        Assert.That(second.waveform.Envelope, Is.Zero);
        Assert.That(second.waveform.Lerp(0.5f, 1f), Is.EqualTo(1f));
    }

    /// <summary>Proves base activation neither acquires nor replaces the Effect owner's Waveform.</summary>
    [Test]
    public void EffectBaseOnStartDoesNotAcquireWaveform()
    {
        var controllerObject = new GameObject("performer-access-policy-controller");
        var controller = controllerObject.AddComponent<Controller>();
        var effect = new BaseStartEffect();
        var configured = Waveform.Parse("QQQQ", "8080");
        effect.waveform = configured;
        effect.BindController(controller);
        try
        {
            Assert.That(
                () => effect.OnStart(),
                Throws.Nothing,
                "base activation has no acquisition policy");
            Assert.That(effect.waveform, Is.EqualTo(configured));
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>
    /// Proves EffectBase detects the Grid returning to one from its own prior observation.
    /// </summary>
    [Test]
    public void EffectGridHookUsesConsumerLocalGridHistory()
    {
        var controllerObject = new GameObject("performer-access-grid-controller");
        var controller = controllerObject.AddComponent<Controller>();
        var effect = new GridHookEffect();
        effect.BindController(controller);

        try
        {
            BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
            {
                snapshot.beatInBar = 1;
                snapshot.timingGrid = new TimingGrid { beat = 16, bar = 4, state = "locked" };
            });
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(effect.NewGridCount, Is.Zero);

            BeatManagerWireFixture.Feed(controller.beatManager, snapshot => snapshot.timingGrid = new TimingGrid { beat = 1, bar = 1, state = "disputed" });
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(effect.NewGridCount, Is.EqualTo(1), "the observed 16-count returned to One");

            BeatManagerWireFixture.Feed(controller.beatManager, snapshot => snapshot.timingGrid = new TimingGrid { beat = 2, bar = 1, state = "disputed" });
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(effect.NewGridCount, Is.EqualTo(1), "the hook does not repeat away from the boundary");
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>Effect probe that records both roots at the two lifecycle boundaries.</summary>
    private sealed class LifecycleEffect : EffectBase
    {
        /// <summary>This lifecycle probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>The BeatManager observed during Init.</summary>
        public BeatManager InitBeatManager { get; private set; }

        /// <summary>The Waveforms surface observed during Init.</summary>
        public Waveforms InitWaveforms { get; private set; }

        /// <summary>The BeatManager observed during OnStart.</summary>
        public BeatManager StartBeatManager { get; private set; }

        /// <summary>The Waveforms surface observed during OnStart.</summary>
        public Waveforms StartWaveforms { get; private set; }

        /// <summary>Records rhythm-root availability before EffectBase's Penrose-dependent setup.</summary>
        public override void Init()
        {
            InitBeatManager = beatManager;
            InitWaveforms = waveforms;
        }

        /// <summary>Records rhythm-root availability without acquiring artistic state.</summary>
        public override void OnStart()
        {
            StartBeatManager = beatManager;
            StartWaveforms = waveforms;
        }

        /// <summary>No cleanup is needed by this probe.</summary>
        public override void OnEnd() { }

        /// <summary>This lifecycle probe does not render.</summary>
        public override void Draw() { }
    }

    /// <summary>Transition probe that records both roots at the two lifecycle boundaries.</summary>
    private sealed class LifecycleTransition : TransitionBase
    {
        /// <summary>The BeatManager observed during Init.</summary>
        public BeatManager InitBeatManager { get; private set; }

        /// <summary>The Waveforms surface observed during Init.</summary>
        public Waveforms InitWaveforms { get; private set; }

        /// <summary>The BeatManager observed during OnStart.</summary>
        public BeatManager StartBeatManager { get; private set; }

        /// <summary>The Waveforms surface observed during OnStart.</summary>
        public Waveforms StartWaveforms { get; private set; }

        /// <summary>Records rhythm-root availability before normal transition setup.</summary>
        public override void Init()
        {
            InitBeatManager = beatManager;
            InitWaveforms = waveforms;
            base.Init();
        }

        /// <summary>Records rhythm-root availability without acquiring artistic state.</summary>
        public override void OnStart()
        {
            StartBeatManager = beatManager;
            StartWaveforms = waveforms;
        }

        /// <summary>No cleanup is needed by this probe.</summary>
        public override void OnEnd() { }

        /// <summary>This lifecycle probe does not render.</summary>
        public override void Draw() { }
    }

    /// <summary>Test Mixer whose child ownership operation uses ordinary Effect binding.</summary>
    private sealed class OwningMixer : MixerBase
    {
        /// <summary>This ownership probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>This ownership probe has no activation behavior.</summary>
        public override void OnStart() { }

        /// <summary>This ownership probe has no cleanup behavior.</summary>
        public override void OnEnd() { }

        /// <summary>This ownership probe does not render.</summary>
        public override void Draw() { }
    }

    /// <summary>Effect probe that inherits the retained base OnStart unchanged.</summary>
    private sealed class BaseStartEffect : EffectBase
    {
        /// <summary>This activation-policy probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>This activation-policy probe has no cleanup behavior.</summary>
        public override void OnEnd() { }

        /// <summary>This activation-policy probe does not render.</summary>
        public override void Draw() { }
    }

    /// <summary>Effect probe that counts the existing protected Grid hook.</summary>
    private sealed class GridHookEffect : EffectBase
    {
        /// <summary>Number of Grid returns detected from this Effect's local prior observation.</summary>
        public int NewGridCount { get; private set; }

        /// <summary>This Grid probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>This Grid probe performs no activation behavior.</summary>
        public override void OnStart() { }

        /// <summary>This Grid probe has no cleanup behavior.</summary>
        public override void OnEnd() { }

        /// <summary>This Grid probe does not render.</summary>
        public override void Draw() { }

        /// <summary>Records only the factual hook call; no artistic state is acquired or replaced.</summary>
        protected override void OnNewGrid()
        {
            NewGridCount++;
        }
    }
}
