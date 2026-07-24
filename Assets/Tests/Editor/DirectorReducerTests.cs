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

    [Test]
    public void ALoopStraddlingARunwayStartDoesNotReCastEachPass()
    {
        var phrases = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: at least two marks.");
        var mark1 = sheet.Marks[0];
        var mark2 = sheet.Marks[1];
        var runwayStart = mark2.Beat - controller.transitions[mark2.TransitionIndex].Repertoire.RunwayBeats;
        Assert.That(runwayStart, Is.GreaterThan(mark1.Beat + 1), "Setup: room in the prior segment below the Runway start.");

        // Forward past the Runway start: the next mark casts once. Settle the transition so any later re-cast
        // flips the stage back to a transition and is unambiguously observable.
        FeedFrame(focusBeat: runwayStart, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark2.EffectIndex), "The next mark casts on its Runway start.");
        switcher.RenderAtTime(1_000_000f, out _);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark2.EffectIndex), "The cast transition settles onto the next mark's Effect.");

        // Backward into the prior segment (below the Runway start): the loop-straddle pass is suppressed — a
        // re-cast would put a transition back on the stage (CurrentEffectIndex < 0); it stays settled.
        FeedFrame(focusBeat: mark1.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark2.EffectIndex), "A backward loop pass casts nothing (no flicker).");

        // Forward across the Runway start again: the same mark is already the last Cast, so still no new cast.
        FeedFrame(focusBeat: runwayStart, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark2.EffectIndex), "Re-crossing the Runway start does not re-cast the same mark.");

        // One more backward pass: still suppressed.
        FeedFrame(focusBeat: mark1.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(mark2.EffectIndex), "The loop keeps riding the cast Effect without flicker.");
    }

    // ---- Starvation guard -----------------------------------------------------------------------

    [Test]
    public void StarvationInjectsAFreshDealtCastAtTheGridStartCeiling()
    {
        var phrases = new[] { Phrase(1, 128, "intro") };
        // Focus beat 4 sits in the run-in before the first mark's Runway, so no plan cast ever fires here.
        FeedFrame(focusBeat: 4, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Run-in: no cast yet.");

        // Three on-air Grid starts stay under the ceiling: still no cast.
        FeedGridStarts(3, focusBeat: 4, phrases, generation: 1);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.EqualTo(0), "Three Grid starts stay under the ceiling.");

        // The fourth Grid start injects exactly one cast dealt fresh from the sheet's bags at this boundary.
        FeedGridStarts(1, focusBeat: 4, phrases, generation: 1);
        var injected = sheet.DealAt(4);
        Assert.That(switcher.Status.CurrentEffectIndex, Is.LessThan(0), "The ceiling injects a cast.");
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(injected.EffectIndex), "The injection is dealt fresh from the sheet's bags, not the plan.");
        Assert.That(controller.currentTransition, Is.EqualTo(injected.TransitionIndex));
    }

    [Test]
    public void ANormalPlanCastResetsTheStarvationCount()
    {
        var phrases = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 4, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: at least two marks.");
        var mark0 = sheet.Marks[0];

        // Three Grid starts accumulate in the run-in (under the ceiling).
        FeedGridStarts(3, focusBeat: 4, phrases, generation: 1);

        // A normal plan cast crossing the first mark resets the count.
        FeedFrame(focusBeat: mark0.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark0.EffectIndex), "Crossing the first mark casts its plan card.");

        // Three more Grid starts within the first segment: had the count not reset, 3 + 3 would exceed the
        // ceiling and inject. Because it reset, the wall still shows the first mark's plan card.
        FeedGridStarts(3, focusBeat: mark0.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark0.EffectIndex), "A normal cast reset the count; no injection within three more Grid starts.");
        Assert.That(controller.currentTransition, Is.EqualTo(mark0.TransitionIndex), "No injection changed the Transition either.");
    }

    [Test]
    public void StarvationSurvivesAFocusHandoverAndAHandoverCastResetsIt()
    {
        var phrases1 = new[] { Phrase(1, 128, "intro") };
        FeedFrame(focusBeat: 4, phrases1, generation: 1, focusPlayer: 1);

        // Accumulate three Grid starts on player 1 (under the ceiling).
        FeedGridStarts(3, focusBeat: 4, phrases1, generation: 1, focusPlayer: 1);

        // Player 2 comes on air as focus at one of its marks: a normal handover cast fires and resets the count.
        var phrases2 = new[] { Phrase(1, 96, "chorus") };
        FeedFrame(focusBeat: 4, phrases2, generation: 2, focusPlayer: 2);
        var sheet2 = ExpectedSheet(playerSlot: 1, generation: 2, playerNumber: 2);
        var mark2 = sheet2.Marks[0];
        FeedFrame(focusBeat: mark2.Beat, phrases2, generation: 2, focusPlayer: 2);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark2.EffectIndex), "The handover casts player 2's plan card.");

        // Only three Grid starts on player 2's held position: the reset means no injection yet.
        FeedGridStarts(3, focusBeat: mark2.Beat + 1, phrases2, generation: 2, focusPlayer: 2);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark2.EffectIndex), "The handover cast reset the count across the focus change.");
    }

    // ---- Override masks (ADR-0017) --------------------------------------------------------------

    [Test]
    public void AStagedEffectPickWinsExactlyOneCastThenThePlanResumes()
    {
        var phrases = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: at least two marks.");
        var mark0 = sheet.Marks[0];
        var mark1 = sheet.Marks[1];
        var stagedEffect = EffectIndexOtherThan(mark0.EffectIndex, mark1.EffectIndex);

        director.SetNextEffect(stagedEffect);

        // The next cast plays the staged pick, not the mark's dealt Effect; the plan's Transition is untouched.
        FeedFrame(focusBeat: mark0.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(stagedEffect), "The staged pick wins exactly the next cast.");
        Assert.That(controller.currentTransition, Is.EqualTo(mark0.TransitionIndex), "A staged Effect override leaves the plan's Transition intact.");

        // The following mark resumes the plan verbatim.
        FeedFrame(focusBeat: mark1.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark1.EffectIndex), "The plan resumes at the following mark.");
    }

    [Test]
    public void AHoldTrumpsEveryDealUntilReleasedThenThePlanResumes()
    {
        var phrases = new[] { Phrase(1, 48, "verse"), Phrase(49, 96, "chorus") };
        FeedFrame(focusBeat: 1, phrases, generation: 1);
        var sheet = ExpectedSheet(playerSlot: 0, generation: 1);
        Assert.That(sheet.Marks.Count, Is.GreaterThanOrEqualTo(2), "Setup: at least two marks.");
        var mark0 = sheet.Marks[0];
        var mark1 = sheet.Marks[1];
        var heldEffect = EffectIndexOtherThan(mark0.EffectIndex, mark1.EffectIndex);

        director.SetNextEffect(heldEffect);
        director.SetHoldSelectedEffect(true);

        // Marks keep firing on cadence, each with the held pick.
        FeedFrame(focusBeat: mark0.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(heldEffect), "The held pick trumps the first mark's deal.");
        FeedFrame(focusBeat: mark1.Beat, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(heldEffect), "The held pick trumps every deal while held.");

        // Release: the very next mark plays exactly what the sheet says.
        director.SetHoldSelectedEffect(false);
        FeedFrame(focusBeat: mark0.Beat + 1, phrases, generation: 1);
        Assert.That(switcher.Status.TargetEffectIndex, Is.EqualTo(mark0.EffectIndex), "Release lands on the plan deterministically.");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>Feeds <paramref name="count"/> on-air Grid starts (a 16→1 wrap each) while the focus beat holds still.</summary>
    private void FeedGridStarts(int count, int focusBeat, StructurePhrase[] phrases, int generation, int focusPlayer = 1)
    {
        for (var i = 0; i < count; i++)
        {
            FeedFrame(focusBeat, phrases, generation, focusPlayer, gridBeat: 16);
            FeedFrame(focusBeat, phrases, generation, focusPlayer, gridBeat: 1);
        }
    }

    /// <summary>The first Effect catalog index not in <paramref name="avoid"/>; used to pick a distinct override pick.</summary>
    private int EffectIndexOtherThan(params int[] avoid)
    {
        for (var i = 0; i < controller.effects.Length; i++)
        {
            if (System.Array.IndexOf(avoid, i) < 0)
            {
                return i;
            }
        }

        return 0;
    }

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

    /// <summary>
    /// Feeds one wire frame: on-air clock plus one focus player with a complete structure, then ticks. The
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
