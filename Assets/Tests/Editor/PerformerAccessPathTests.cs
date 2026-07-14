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

    /// <summary>Proves one Controller timing step publishes fresh BeatManager and Waveforms state before Director and Effect work.</summary>
    [UnityTest]
    public IEnumerator ControllerFramePublishesFreshRhythmBeforeDirectorAndRendering()
    {
        yield return new EnterPlayMode();

        var controllerObject = new GameObject("performer-access-frame-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.logDirectorSwitching = false;
        var observedWaveform = controller.waveforms.Random();
        var currentEffect = new FrameObservationEffect(observedWaveform);
        var nextEffect = new FrameObservationEffect(observedWaveform);
        currentEffect.BindController(controller);
        currentEffect.Init();
        nextEffect.BindController(controller);
        nextEffect.Init();
        var transition = new FrameTransition(observedWaveform);
        transition.BindController(controller);
        transition.Init();

        controller.effects = new EffectBase[] { currentEffect, nextEffect };
        controller.transitions = new TransitionBase[] { transition };
        controller.effectDeck = new[] { 1, 0 };
        controller.transitionDeck = new[] { 0 };
        controller.currentTransition = 0;
        controller.timer = new Timer(10f, false);
        controller.switcher = new Switcher(controller, controller.effects, controller.transitions);
        controller.switcher.SetInitialEffect(0, controller.currentTransition);
        controller.director = new Director(
            controller,
            controller.switcher,
            controller.timer,
            controller.effectDeck,
            controller.transitionDeck,
            controller.currentTransition);

        SeedFrame(controller.beatManager, timeSeconds: 0.4f, beat: 615, gridBeat: 16);
        controller.AdvanceFrameTiming(0.4f, deltaTime: 0f);
        var joinedMidGridWithoutCue = !controller.switcher.LoadedCueStatus.HasCue;

        SeedFrame(controller.beatManager, timeSeconds: 0.6f, beat: 616, gridBeat: 1);
        controller.AdvanceFrameTiming(0.6f, deltaTime: 0f);
        var directorSawFreshGrid = controller.switcher.LoadedCueStatus.HasCue;
        var expectedEnvelope = observedWaveform.Envelope;
        var directorSawFreshWaveforms = transition.ObservedEnvelope;
        controller.switcher.RenderAtTime(0.6f, out _);
        var effectSawFreshWaveforms = currentEffect.ObservedEnvelope;
        var directorObservedWaveforms = transition.WasObserved;
        var effectObservedWaveforms = currentEffect.WasObserved;

        Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(joinedMidGridWithoutCue, Is.True);
        Assert.That(directorSawFreshGrid, Is.True);
        Assert.That(directorObservedWaveforms, Is.True);
        Assert.That(effectObservedWaveforms, Is.True);
        Assert.That(directorSawFreshWaveforms, Is.EqualTo(expectedEnvelope).Within(0.0001f));
        Assert.That(effectSawFreshWaveforms, Is.EqualTo(expectedEnvelope).Within(0.0001f));
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

    /// <summary>Seeds one independent worked clock and wire frame for Controller timing-order observations.</summary>
    private static void SeedFrame(BeatManager beatManager, float timeSeconds, int beat, int gridBeat)
    {
        var snapshot = BeatClockFixture.CreateSnapshot(bpm: 120f, timeSeconds: timeSeconds);
        snapshot.beat = new BeatPosition { current = beat, total = -1 };
        snapshot.timingGrid = new TimingGrid
        {
            beat = gridBeat,
            bar = ((gridBeat - 1) / 4) + 1,
            state = "locked",
        };
        snapshot.phraseState = new PhraseState
        {
            label = "Phrase",
            countBeats = 632 - beat,
            lengthBeats = 32,
            irregular = 0,
        };
        snapshot.nextPhraseState = LabeledCountdown.Unavailable;
        snapshot.dropState = CountdownState.Unavailable;
        snapshot.fillState = CountdownState.Unavailable;
        beatManager.FeedWireSnapshot(snapshot);
    }

    /// <summary>Effect probe that observes Waveforms through the ordinary render seam.</summary>
    private sealed class FrameObservationEffect : EffectBase
    {
        /// <summary>The worked quarter-note Waveform observed during Draw.</summary>
        private readonly Waveform observedWaveform;

        /// <summary>Creates a render probe for the worked Waveform.</summary>
        public FrameObservationEffect(Waveform waveform)
        {
            observedWaveform = waveform;
        }

        /// <summary>The live Waveform envelope observed during the last Draw.</summary>
        public float ObservedEnvelope { get; private set; }

        /// <summary>Whether the real render seam invoked this probe.</summary>
        public bool WasObserved { get; private set; }

        /// <summary>Initializes only the buffer needed by the Switcher's render seam.</summary>
        public override void Init()
        {
            buffer = new Color[Penrose.Total];
        }

        /// <summary>This frame-order probe has no runtime debug detail.</summary>
        public override string DebugText() => string.Empty;

        /// <summary>This frame-order probe has no activation behavior.</summary>
        public override void OnStart() { }

        /// <summary>This frame-order probe has no cleanup behavior.</summary>
        public override void OnEnd() { }

        /// <summary>Observes live Waveforms state at the same seam where an ordinary Effect renders.</summary>
        public override void Draw()
        {
            WasObserved = true;
            ObservedEnvelope = observedWaveform.Envelope;
        }
    }

    /// <summary>Minimal Transition needed for the real Switcher and Director timing seam.</summary>
    private sealed class FrameTransition : TransitionBase
    {
        /// <summary>The worked quarter-note Waveform observed while Director reads Repertoire.</summary>
        private readonly Waveform waveform;

        /// <summary>Creates a Director-order probe for the worked Waveform.</summary>
        public FrameTransition(Waveform waveform)
        {
            this.waveform = waveform;
        }

        /// <summary>The live Waveform envelope observed by Director's latest Repertoire read.</summary>
        public float ObservedEnvelope { get; private set; }

        /// <summary>Whether Director read this transition's Repertoire.</summary>
        public bool WasObserved { get; private set; }

        /// <summary>Observes Waveforms through the Transition interface Director reads while casting.</summary>
        public override TransitionRepertoire Repertoire
        {
            get
            {
                WasObserved = true;
                ObservedEnvelope = waveform.Envelope;
                return base.Repertoire;
            }
        }

        /// <summary>This frame-order probe has no activation behavior.</summary>
        public override void OnStart() { }

        /// <summary>This frame-order probe has no cleanup behavior.</summary>
        public override void OnEnd() { }

        /// <summary>This frame-order probe does not render.</summary>
        public override void Draw() { }
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
