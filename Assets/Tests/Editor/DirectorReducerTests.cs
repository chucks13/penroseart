using System.Collections.Generic;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Reducer-seam tests for the plan-driven Director (ADR-0019). Per-player wire snapshots go in through the
// BeatManager; casts are observed at the Switcher seam (Status.TargetEffectIndex and the mirrored
// controller.currentTransition). The Director's own sheet is the deterministic TrackCueSheet, so each test
// builds the same sheet as ground truth and asserts the casts follow it — never asserting a random layout.
public sealed class DirectorReducerTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, instance);
    }

    /// <summary>Builds the real Director/Switcher cycle used to observe handovers and query decisions.</summary>
    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("DirectorReducerTestsController");
        controller = controllerObject.AddComponent<Controller>();
        SetControllerSingleton(controller);
        controller.paletteSource = string.Empty;
        EffectBase.LoadPalette(controller.paletteSource);
        controller.logDirectorSwitching = false;
        controller.effectTime = 10f;
        controller.beatManager = new BeatManager();
        controller.beatManager.SetLiveBeatSource(true);
        controller.effects = new EffectBase[]
        {
            new TestEffect(),
            new TestEffect(),
            new TestEffect(Repertoire.HandlesDrop),
            new TestEffect(Repertoire.HandlesFill),
        };
        controller.transitions = new TransitionBase[]
        {
            new TestTransition(RepertoireFlags.None, runwayBeats: 4, tailBeats: 4),
            new TestTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 4),
            new TestTransition(RepertoireFlags.HandlesFill, runwayBeats: 4, tailBeats: 4),
        };
        foreach (var transition in controller.transitions)
        {
            transition.BindController(controller);
            transition.Init();
        }

        controller.effectDeck = new[] { 0, 1, 2, 3 };
        controller.transitionDeck = new[] { 0, 1, 2 };
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
        switcher.BindDirector(director);
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    // ---- Sheet construction and focus handover --------------------------------------------------

    /// <summary>Pins generation identity: a changed structure generation rebuilds the player slot's sheet.</summary>
    [Test]
    public void AGenerationChangeRebuildsThePlayerSheet()
    {
        var phrasesA = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrasesA, generation: 1);
        var sheetA = ExpectedSheet(playerSlot: 0, generation: 1);
        FeedFrame(RunwayStart(sheetA.Marks[0]), phrasesA, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(sheetA.Marks[0].EffectIndex), "Generation 1 drives execution.");

        var phrasesB = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrasesB, generation: 2);
        var sheetB = ExpectedSheet(playerSlot: 0, generation: 2);
        FeedFrame(RunwayStart(sheetB.Marks[0]), phrasesB, generation: 2);

        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(sheetB.Marks[0].EffectIndex), "The rebuilt generation-2 sheet is handed over.");
        Assert.That(controller.currentTransition, Is.EqualTo(sheetB.Marks[0].TransitionIndex));
    }

    /// <summary>Pins focus resolution: the new on-air player's sheet is handed to the Switcher on the focus-change frame.</summary>
    [Test]
    public void TheOnAirPlayersSheetIsHandedToTheSwitcherOnAFocusChange()
    {
        var phrases1 = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrases1, generation: 1, focusPlayer: 1);
        var sheet1 = ExpectedSheet(playerSlot: 0, generation: 1, playerNumber: 1);
        FeedFrame(RunwayStart(sheet1.Marks[0]), phrases1, generation: 1, focusPlayer: 1);
        switcher.RenderAtTime(1_000_000f, out _);

        var phrases2 = new[] { Phrase(1, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases2, generation: 2, focusPlayer: 2);
        var sheet2 = ExpectedSheet(playerSlot: 1, generation: 2, playerNumber: 2);
        var player2Mark = sheet2.Marks[0];

        FeedFrame(RunwayStart(player2Mark), phrases2, generation: 2, focusPlayer: 2);

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The new player's due mark executes in the handover frame.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(player2Mark.EffectIndex));
        Assert.That(controller.currentTransition, Is.EqualTo(player2Mark.TransitionIndex));
    }

    // ---- Decision answers (ADR-0017 / ADR-0020) ------------------------------------------------

    /// <summary>Pins the basic question seam: DecideCue returns the baked plan assignment unchanged.</summary>
    [Test]
    public void DecideCueAnswersWithTheMarksBakedAssignment()
    {
        var mark = new CuePlanMark(beat: 32, effectIndex: 2, transitionIndex: 1);

        var decision = director.DecideCue(mark);

        Assert.That(decision.Perform, Is.True);
        Assert.That(decision.EffectIndex, Is.EqualTo(mark.EffectIndex));
        Assert.That(decision.TransitionIndex, Is.EqualTo(mark.TransitionIndex));
    }

    /// <summary>Pins ADR-0017 one-shots: staged Effect and Transition mask one answer, then the plan resumes.</summary>
    [Test]
    public void OneShotOverridesMaskExactlyOneDecision()
    {
        var mark = new CuePlanMark(beat: 32, effectIndex: 0, transitionIndex: 0);
        director.SetNextEffect(2);
        director.SetNextTransition(1);

        var overridden = director.DecideCue(mark);
        var resumed = director.DecideCue(mark);

        Assert.That(overridden.Perform, Is.True);
        Assert.That(overridden.EffectIndex, Is.EqualTo(2));
        Assert.That(overridden.TransitionIndex, Is.EqualTo(1));
        Assert.That(resumed.EffectIndex, Is.EqualTo(mark.EffectIndex));
        Assert.That(resumed.TransitionIndex, Is.EqualTo(mark.TransitionIndex));
    }

    /// <summary>Pins Hold Selected masking: selected cards answer repeatedly while held and release returns to the plan.</summary>
    [Test]
    public void HoldSelectedMasksEveryDecisionUntilReleased()
    {
        var mark = new CuePlanMark(beat: 32, effectIndex: 0, transitionIndex: 0);
        director.SetNextEffect(2);
        director.SetNextTransition(1);
        director.SetHoldSelectedEffect(true);
        director.SetHoldSelectedTransition(true);

        var first = director.DecideCue(mark);
        var second = director.DecideCue(mark);
        director.SetHoldSelectedEffect(false);
        director.SetHoldSelectedTransition(false);
        var released = director.DecideCue(mark);

        Assert.That(first.EffectIndex, Is.EqualTo(2));
        Assert.That(first.TransitionIndex, Is.EqualTo(1));
        Assert.That(second.EffectIndex, Is.EqualTo(2));
        Assert.That(second.TransitionIndex, Is.EqualTo(1));
        Assert.That(released.EffectIndex, Is.EqualTo(mark.EffectIndex));
        Assert.That(released.TransitionIndex, Is.EqualTo(mark.TransitionIndex));
    }

    /// <summary>Pins Hold: a held wall makes both cue and one-off questions answer no-perform.</summary>
    [Test]
    public void HoldAnswersNoPerform()
    {
        var phrases = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        controller.heldEffect = 1;

        var cueDecision = director.DecideCue(new CuePlanMark(beat: 32, effectIndex: 2, transitionIndex: 1));
        var oneOffDecision = director.DecideOneOff(beat: 4);

        Assert.That(cueDecision.Perform, Is.False);
        Assert.That(oneOffDecision.Perform, Is.False);
    }

    /// <summary>Pins the one-off answer: Director deals from the focus sheet at the requested beat.</summary>
    [Test]
    public void DecideOneOffAnswersFromTheFocusPlayersSheet()
    {
        var phrases = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        var expected = ExpectedSheet(playerSlot: 0, generation: 1).DealAt(boundaryBeat: 4);

        var decision = director.DecideOneOff(beat: 4);

        Assert.That(decision.Perform, Is.True);
        Assert.That(decision.EffectIndex, Is.EqualTo(expected.EffectIndex));
        Assert.That(decision.TransitionIndex, Is.EqualTo(expected.TransitionIndex));
    }

    // ---- Standalone ----------------------------------------------------------------------------

    /// <summary>Pins Standalone behavior: its timer decision still starts the selected seconds-based transition.</summary>
    [Test]
    public void StandaloneTimerStillStartsTheSelectedSecondsBasedTransition()
    {
        controller.beatManager.SetLiveBeatSource(false);
        director.SetNextEffect(2);
        director.SetNextTransition(1);

        director.OnTimerFinished();

        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0));
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(2));
        // The running Transition is the seam here: Standalone re-stages the next pick from the deck
        // immediately after starting, so controller.currentTransition already names the *next* card.
        Assert.That(switcher.Status.CurrentTransitionIndex, Is.EqualTo(1));
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>Returns a mark's Runway start for the downstream Switcher execution used to observe a handover.</summary>
    private int RunwayStart(CuePlanMark mark)
    {
        return mark.Beat - controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;
    }

    /// <summary>Builds the sheet the Director builds for a player slot, using the same catalogs and seed.</summary>
    private TrackCueSheet ExpectedSheet(int playerSlot, int generation, int playerNumber = 1)
    {
        var structure = controller.beatManager.Players[playerSlot].Structure;
        return TrackCueSheet.Build(structure, EffectDescriptors(), TransitionDescriptors(), generation, playerNumber);
    }

    private IReadOnlyList<EffectDescriptor> EffectDescriptors()
    {
        var descriptors = new EffectDescriptor[controller.effects.Length];
        for (var i = 0; i < descriptors.Length; i++)
        {
            descriptors[i] = new EffectDescriptor(i, controller.EffectiveRepertoire(i));
        }

        return descriptors;
    }

    private IReadOnlyList<TransitionDescriptor> TransitionDescriptors()
    {
        var descriptors = new TransitionDescriptor[controller.transitions.Length];
        for (var i = 0; i < descriptors.Length; i++)
        {
            descriptors[i] = new TransitionDescriptor(i, controller.transitions[i].Repertoire);
        }

        return descriptors;
    }

    /// <summary>
    /// Feeds one wire frame: on-air clock plus one focus player with a complete structure, then ticks both seams. The
    /// on-air Grid beat defaults to the focus beat's position within its Grid; tests pass <paramref name="gridBeat"/>
    /// explicitly to drive on-air Grid starts (wraps to 1) independently of the focus beat.
    /// </summary>
    private void FeedFrame(int focusBeat, StructurePhrase[] phrases, int generation, int focusPlayer = 1, int? gridBeat = null)
    {
        var onAirGridBeat = gridBeat ?? (((focusBeat - 1) % 16) + 1);
        BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
        {
            snapshot.beatInBar = ((focusBeat - 1) % 4) + 1;
            snapshot.beat = new BeatPosition { current = focusBeat, total = -1 };
            snapshot.bpm = 120f;
            snapshot.timingGrid = new TimingGrid { beat = onAirGridBeat, bar = ((onAirGridBeat - 1) / 4) + 1, state = "locked" };
            snapshot.playersLive = focusPlayer.ToString();
            snapshot.players ??= new PlayerState[RaveWireSnapshot.PlayerCount];
            var player = PlayerState.Unavailable;
            player.clock = new PlayerClock
            {
                bpm = 120f,
                beat = focusBeat,
                bar = ((focusBeat - 1) / 4) + 1,
                beatInBar = ((focusBeat - 1) % 4) + 1,
                beatPulse = 1f,
            };
            player.transport = new PlayerTransport { playing = 1, cued = 0, onAir = 1, master = 1, synced = 1 };
            player.structure = new PlayerStructure
            {
                generation = generation,
                trackId = "track" + generation,
                source = "analyzed",
                totalBeats = 512,
                phraseCount = phrases.Length,
                phrases = phrases,
            };
            snapshot.players[focusPlayer - 1] = player;
        });
        controller.beatManager.Update(0f);
        director.Tick(0f);
        switcher.Tick();
    }

    private static StructurePhrase Phrase(int startBeat, int endBeat, string type, int fillStartBeat = 0, int dropLandingBeat = 0)
    {
        return new StructurePhrase
        {
            startBeat = startBeat,
            endBeat = endBeat,
            type = type,
            variant = 0,
            fillStartBeat = fillStartBeat,
            dropLandingBeat = dropLandingBeat,
        };
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

    private sealed class TestTransition : TransitionBase
    {
        private readonly Repertoire tags;
        private readonly int runwayBeats;
        private readonly int tailBeats;

        public TestTransition(Repertoire tags, int runwayBeats, int tailBeats)
        {
            this.tags = tags;
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
            buffer = new Color[Penrose.Total];
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                tags,
                runwayBeats,
                tailBeats,
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
        }
    }
}
