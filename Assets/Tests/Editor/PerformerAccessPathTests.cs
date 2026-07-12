// Seam-3 contract tests for Controller-owned rhythm stepping and live Performer access roots.

using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Seam-3 tests for the live rhythm roots exposed to Effects and Transitions.
/// </summary>
public sealed class PerformerAccessPathTests
{
    /// <summary>Proves one Controller rhythm step observes the new hub frame exactly once in hub-then-synth order.</summary>
    [Test]
    public void ControllerRhythmStepAdvancesSynthExactlyOnceAfterBeatManager()
    {
        var controllerObject = new GameObject("ticket-20-frame-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.4f);
        controller.InitializeSynth();
        var quarterNotes = Waveform.Parse("QQQQ", "8888");

        try
        {
            controller.UpdateRhythm(0.4f);
            Assert.That(controller.synth.Hit(quarterNotes), Is.False, "first observation opens no hit window");

            // At 120 BPM, 0.4s -> 0.6s crosses the beat-2 onset at 0.5s.
            BeatClockFixture.SeedBeatClock(controller.beatManager, bpm: 120f, timeSeconds: 0.6f);
            controller.UpdateRhythm(0.6f);

            Assert.That(
                controller.synth.Hit(quarterNotes),
                Is.True,
                "one synth step after the new hub frame preserves the worked onset window");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>
    /// Proves both Performer bases can read the Controller's same live roots during Init and OnStart.
    /// </summary>
    [Test]
    public void EffectAndTransitionLifecycleReceiveTheSameLiveRhythmRoots()
    {
        var controllerObject = new GameObject("ticket-20-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.beatManager = new BeatManager();
        controller.InitializeSynth();

        try
        {
            var effect = new LifecycleEffect();
            effect.BindController(controller);
            effect.Init();
            effect.OnStart();

            var transition = new LifecycleTransition();
            transition.BindController(controller);
            transition.Init();
            transition.OnStart();

            Assert.That(effect.InitBeatManager, Is.SameAs(controller.beatManager));
            Assert.That(effect.StartBeatManager, Is.SameAs(controller.beatManager));
            Assert.That(effect.InitSynth, Is.SameAs(controller.synth));
            Assert.That(effect.StartSynth, Is.SameAs(controller.synth));
            Assert.That(transition.InitBeatManager, Is.SameAs(controller.beatManager));
            Assert.That(transition.StartBeatManager, Is.SameAs(controller.beatManager));
            Assert.That(transition.InitSynth, Is.SameAs(controller.synth));
            Assert.That(transition.StartSynth, Is.SameAs(controller.synth));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>Proves a Mixer-owned child receives ordinary Effect access without a Mixer rhythm seam.</summary>
    [Test]
    public void MixerOwnedChildUsesOrdinaryEffectRhythmRoots()
    {
        var controllerObject = new GameObject("ticket-20-mixer-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.beatManager = new BeatManager();
        controller.InitializeSynth();
        controller.penrose = controllerObject.AddComponent<Penrose>();

        try
        {
            var mixer = new OwningMixer();
            mixer.BindController(controller);
            mixer.Init();
            var child = mixer.GetRandomEffect();

            Assert.That(child.beatManager, Is.SameAs(controller.beatManager));
            Assert.That(child.synth, Is.SameAs(controller.synth));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>Proves the retained EffectBase activation lifecycle performs no synth acquisition or response.</summary>
    [Test]
    public void EffectBaseOnStartDoesNotAcquireFromSynth()
    {
        var controllerObject = new GameObject("ticket-20-policy-controller");
        var controller = controllerObject.AddComponent<Controller>();
        var effect = new BaseStartEffect();
        effect.BindController(controller);
        try
        {
            Assert.That(
                () => effect.OnStart(),
                Throws.Nothing,
                "the retained legacy OnStart does not require or acquire from the uninitialized synth root");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>
    /// Proves EffectBase reports the hub's one-frame Grid wrap Edge without rebuilding or gating it.
    /// </summary>
    [Test]
    public void EffectGridHookMatchesGridWrappedExactly()
    {
        var controllerObject = new GameObject("ticket-20-grid-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.InitializeSynth();
        var effect = new GridHookEffect();
        effect.BindController(controller);

        try
        {
            controller.beatManager.WireSnapshot.beatInBar = 1;
            controller.beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 16, bar = 4, state = "locked" };
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(controller.beatManager.Grid.Wrapped, Is.False);
            Assert.That(effect.NewGridCount, Is.Zero);

            controller.beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 1, bar = 1, state = "disputed" };
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(controller.beatManager.Grid.Wrapped, Is.True, "the wire's 16-count returned to One");
            Assert.That(effect.NewGridCount, Is.EqualTo(1), "Grid State is data, never a gate");

            controller.beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = 2, bar = 1, state = "disputed" };
            controller.beatManager.Update(0f);
            effect.UpdateTime();
            Assert.That(controller.beatManager.Grid.Wrapped, Is.False);
            Assert.That(effect.NewGridCount, Is.EqualTo(1), "the hook performs no latched response after the Edge closes");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    /// <summary>Effect probe that records both roots at the two lifecycle boundaries.</summary>
    private sealed class LifecycleEffect : EffectBase
    {
        /// <summary>This lifecycle probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>The BeatManager observed during Init.</summary>
        public BeatManager InitBeatManager { get; private set; }

        /// <summary>The synth observed during Init.</summary>
        public WaveformSynth InitSynth { get; private set; }

        /// <summary>The BeatManager observed during OnStart.</summary>
        public BeatManager StartBeatManager { get; private set; }

        /// <summary>The synth observed during OnStart.</summary>
        public WaveformSynth StartSynth { get; private set; }

        /// <summary>Records rhythm-root availability before EffectBase's Penrose-dependent setup.</summary>
        public override void Init()
        {
            InitBeatManager = beatManager;
            InitSynth = synth;
        }

        /// <summary>Records rhythm-root availability without acquiring artistic state.</summary>
        public override void OnStart()
        {
            StartBeatManager = beatManager;
            StartSynth = synth;
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

        /// <summary>The synth observed during Init.</summary>
        public WaveformSynth InitSynth { get; private set; }

        /// <summary>The BeatManager observed during OnStart.</summary>
        public BeatManager StartBeatManager { get; private set; }

        /// <summary>The synth observed during OnStart.</summary>
        public WaveformSynth StartSynth { get; private set; }

        /// <summary>Records rhythm-root availability before normal transition setup.</summary>
        public override void Init()
        {
            InitBeatManager = beatManager;
            InitSynth = synth;
            base.Init();
        }

        /// <summary>Records rhythm-root availability without acquiring artistic state.</summary>
        public override void OnStart()
        {
            StartBeatManager = beatManager;
            StartSynth = synth;
        }

        /// <summary>No cleanup is needed by this probe.</summary>
        public override void OnEnd() { }

        /// <summary>This lifecycle probe does not render.</summary>
        public override void Draw() { }
    }

    /// <summary>Test Mixer whose private ownership operation uses ordinary Effect binding.</summary>
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
        /// <summary>Number of hub Grid wrap Edges reported to this Effect.</summary>
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
