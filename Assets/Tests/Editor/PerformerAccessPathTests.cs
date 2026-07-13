// Seam-3 contract tests for Controller-owned rhythm stepping and live Performer access roots.

using System.Collections;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Seam-3 tests for Controller frame ownership and the live rhythm roots exposed to Performers.
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

    /// <summary>Proves one Controller timing step publishes the fresh hub and synth frame before Director and Effect work.</summary>
    [UnityTest]
    public IEnumerator ControllerFramePublishesFreshRhythmBeforeDirectorAndRendering()
    {
        yield return new EnterPlayMode();

        var controllerObject = new GameObject("performer-access-frame-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.logDirectorSwitching = false;
        var quarterNotes = Waveform.Parse("QQQQ", "8888");
        var currentEffect = new FrameObservationEffect(quarterNotes);
        var nextEffect = new FrameObservationEffect(quarterNotes);
        currentEffect.BindController(controller);
        currentEffect.Init();
        nextEffect.BindController(controller);
        nextEffect.Init();
        var transition = new FrameTransition(quarterNotes);
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
        var directorSawFreshSynth = transition.ObservedHit;
        controller.switcher.RenderAtTime(0.6f, out _);
        var effectSawFreshSynth = currentEffect.ObservedHit;

        UnityEngine.Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(joinedMidGridWithoutCue, Is.True);
        Assert.That(directorSawFreshGrid, Is.True);
        Assert.That(directorSawFreshSynth, Is.True);
        Assert.That(effectSawFreshSynth, Is.True);
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
        var liveSynth = controller.synth;
        var effect = new LifecycleEffect();
        effect.BindController(controller);
        effect.Init();
        effect.OnStart();

        var transition = new LifecycleTransition();
        transition.BindController(controller);
        transition.Init();
        transition.OnStart();

        UnityEngine.Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(liveBeatManager, Is.Not.Null);
        Assert.That(liveSynth, Is.Not.Null);
        Assert.That(effect.InitBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(effect.StartBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(effect.InitSynth, Is.SameAs(liveSynth));
        Assert.That(effect.StartSynth, Is.SameAs(liveSynth));
        Assert.That(transition.InitBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(transition.StartBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(transition.InitSynth, Is.SameAs(liveSynth));
        Assert.That(transition.StartSynth, Is.SameAs(liveSynth));
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
        var liveSynth = controller.synth;
        var mixer = new OwningMixer();
        mixer.BindController(controller);
        mixer.Init();
        var child = mixer.GetRandomEffect();
        var childBeatManager = child.beatManager;
        var childSynth = child.synth;

        UnityEngine.Object.DestroyImmediate(controllerObject);
        yield return new ExitPlayMode();

        Assert.That(liveBeatManager, Is.Not.Null);
        Assert.That(liveSynth, Is.Not.Null);
        Assert.That(childBeatManager, Is.SameAs(liveBeatManager));
        Assert.That(childSynth, Is.SameAs(liveSynth));
    }

    /// <summary>Proves the retained EffectBase activation lifecycle does not require a synth root.</summary>
    [Test]
    public void EffectBaseOnStartDoesNotRequireSynth()
    {
        var controllerObject = new GameObject("performer-access-policy-controller");
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
        var controllerObject = new GameObject("performer-access-grid-controller");
        var controller = controllerObject.AddComponent<Controller>();
        controller.beatManager.SetLiveBeatSource(true);
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

    /// <summary>Seeds one independent worked clock and wire frame for Controller timing-order observations.</summary>
    private static void SeedFrame(BeatManager beatManager, float timeSeconds, int beat, int gridBeat)
    {
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: timeSeconds);
        var snapshot = beatManager.WireSnapshot;
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
    }

    /// <summary>Effect probe that observes the synth through the ordinary render seam.</summary>
    private sealed class FrameObservationEffect : EffectBase
    {
        /// <summary>The worked quarter-note Waveform observed during Draw.</summary>
        private readonly Waveform waveform;

        /// <summary>Creates a render probe for the worked Waveform.</summary>
        public FrameObservationEffect(Waveform waveform)
        {
            this.waveform = waveform;
        }

        /// <summary>Whether the last Draw observed the worked onset window.</summary>
        public bool ObservedHit { get; private set; }

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

        /// <summary>Observes the live synth at the same seam where an ordinary Effect renders.</summary>
        public override void Draw()
        {
            ObservedHit = synth.Hit(waveform);
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

        /// <summary>Whether Director's latest Repertoire read observed the fresh synth onset window.</summary>
        public bool ObservedHit { get; private set; }

        /// <summary>Observes the synth through the Transition interface Director reads while casting.</summary>
        public override TransitionRepertoire Repertoire
        {
            get
            {
                ObservedHit = synth.Hit(waveform);
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
