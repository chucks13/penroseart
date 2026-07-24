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
    }

    [TearDown]
    public void TearDown()
    {
        SetControllerSingleton(null);
        Object.DestroyImmediate(controllerObject);
    }

    // ---- The focus sheet drives casts at the Runway start ---------------------------------------

    [Test]
    public void TheFocusSheetDrivesTheCastAtItsFirstMarkRunwayStart()
    {
        var phrases = new[] { Phrase(1, 64, "intro") };

        // Run-in before the first mark: the sheet is built but nothing is cast yet.
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Run-in before the first mark: no cast.");

        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThan(0), "Setup: the phrase produces at least one mark.");
        var mark = sheet.Marks[0];
        var runway = controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;

        // One beat before the Runway start: still no cast — the Director waits for the last responsible moment.
        FeedFrame(focusBeat: mark.Beat - runway - 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Before the Runway start the mark is not cast.");

        // The Runway start beat casts the mark from the focus sheet.
        FeedFrame(focusBeat: mark.Beat - runway, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The Runway start casts the mark.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark.EffectIndex), "The cast plays the mark's baked Effect.");
        Assert.That(controller.currentTransition, Is.EqualTo(mark.TransitionIndex), "The cast uses the mark's baked Transition.");
    }

    // ---- Slot rebuild on generation change ------------------------------------------------------

    [Test]
    public void AGenerationChangeRebuildsTheSlotAndTheNewSheetDrivesCasts()
    {
        var phrasesA = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrasesA, generation: 1);
        var sheetA = ExpectedSheet(playerSlot: 0, generation: 1);
        CastMark(sheetA.Marks[0], phrasesA, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(sheetA.Marks[0].EffectIndex), "Setup: generation 1's sheet drives the cast.");

        // A new track loads on the same player: a different generation and a different structure rebuild the slot.
        var phrasesB = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrasesB, generation: 2);
        var sheetB = ExpectedSheet(playerSlot: 0, generation: 2);
        var markB = sheetB.Marks[0];

        CastMark(markB, phrasesB, generation: 2);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(markB.EffectIndex), "The rebuilt generation-2 sheet drives the cast.");
        Assert.That(controller.currentTransition, Is.EqualTo(markB.TransitionIndex), "The rebuilt sheet supplies the Transition too.");
    }

    // ---- Focus handover -------------------------------------------------------------------------

    [Test]
    public void FocusHandoverCastsFromTheNewFocusPlayersSheet()
    {
        var phrases1 = new[] { Phrase(1, 64, "intro") };
        FeedFrame(focusBeat: 1, phrases1, generation: 1, focusPlayer: 1);
        var sheet1 = ExpectedSheet(playerSlot: 0, generation: 1, playerNumber: 1);
        CastMark(sheet1.Marks[0], phrases1, generation: 1, focusPlayer: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(sheet1.Marks[0].EffectIndex), "Setup: player 1's sheet is on air.");

        // Player 2 comes on air as the new focus with its own loaded track.
        var phrases2 = new[] { Phrase(1, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases2, generation: 2, focusPlayer: 2);
        var sheet2 = ExpectedSheet(playerSlot: 1, generation: 2, playerNumber: 2);
        var mark2 = sheet2.Marks[0];

        // At player 2's first mark the handover takes over with a normal cast from player 2's sheet.
        FeedFrame(focusBeat: mark2.Beat, phrases2, generation: 2, focusPlayer: 2);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark2.EffectIndex), "Handover casts from the new focus player's sheet.");
        Assert.That(controller.currentTransition, Is.EqualTo(mark2.TransitionIndex));
    }

    // ---- Segment crossing under loop and needle-drop --------------------------------------------

    [Test]
    public void SegmentCrossingCastsOnEntryAndStaysQuietWithinASegment()
    {
        var phrases = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: the structure produces at least two marks.");
        var first = sheet.Marks[0];
        var second = sheet.Marks[1];

        // Enter the first segment: cast the first mark.
        FeedFrame(focusBeat: first.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(first.EffectIndex), "Entering the first segment casts its mark.");

        // Move forward within the first segment (no new segment): no new cast.
        FeedFrame(focusBeat: first.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(first.EffectIndex), "Staying within a segment casts nothing new.");

        // Cross into the second segment: cast the second mark.
        FeedFrame(focusBeat: second.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(second.EffectIndex), "Crossing into the second segment casts its mark.");

        // Loop / back-cue: the wire beat jumps backward into the first segment — the owning mark changes, so a
        // normal cast re-asserts the first segment.
        FeedFrame(focusBeat: first.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(first.EffectIndex), "A backward jump into a prior segment re-asserts its mark.");

        // Needle-drop: the wire beat jumps forward into the second segment again — re-asserts the second mark.
        FeedFrame(focusBeat: second.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(second.EffectIndex), "A forward jump into a later segment re-asserts its mark.");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>Drives the focus beat to a mark's Runway start so the Director casts exactly that mark.</summary>
    private void CastMark(CuePlanMark mark, StructurePhrase[] phrases, int generation, int focusPlayer = 1)
    {
        var runway = controller.transitions[mark.TransitionIndex].Repertoire.RunwayBeats;
        FeedFrame(focusBeat: mark.Beat - runway, phrases, generation, focusPlayer);
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

    /// <summary>Feeds one wire frame: on-air clock plus one focus player with a complete structure, then ticks.</summary>
    private void FeedFrame(int focusBeat, StructurePhrase[] phrases, int generation, int focusPlayer = 1)
    {
        BeatManagerWireFixture.Feed(controller.beatManager, snapshot =>
        {
            snapshot.beatInBar = ((focusBeat - 1) % 4) + 1;
            snapshot.beat = new BeatPosition { current = focusBeat, total = -1 };
            snapshot.bpm = 120f;
            snapshot.timingGrid = new TimingGrid { beat = ((focusBeat - 1) % 16) + 1, bar = 1, state = "locked" };
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
