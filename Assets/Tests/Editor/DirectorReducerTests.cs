using System.Reflection;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using RepertoireFlags = Repertoire;

// Seam-2 tests for the wire-change reducer (ADR-0011). BeatManager snapshot sequences go in; observations
// are taken only at the Switcher handoff (switcher.LoadedCueStatus and the decks) and at the Director's real
// Cue Sheet state — never at decision memory, which the reducer does not keep. A Phrase's mandatory final
// Cue Mark is always its length offset, so phrase-end marks give deterministic cast targets without asserting
// a random interior layout.
public sealed class DirectorReducerTests
{
    private GameObject controllerObject;
    private Controller controller;
    private Switcher switcher;
    private Director director;

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
            new TailedTransition(),
            new EventTransition(RepertoireFlags.HandlesDrop, runwayBeats: 4, tailBeats: 0),
            new EventTransition(RepertoireFlags.HandlesFill, runwayBeats: 4, tailBeats: 0),
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

    // ---- Repair invariant ------------------------------------------------------------------------

    [Test]
    public void StartupBuildsTheCurrentSheetFromThePhraseAnnouncement()
    {
        // No cold-join case: the first synced wake with a Phrase announcement builds the current sheet.
        FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 32);

        var sheet = director.Status.CurrentSheet;
        Assert.That(sheet.HasSheet, Is.True);
        Assert.That(sheet.PhraseStartBeat, Is.EqualTo(600));
        Assert.That(sheet.PhraseLengthBeats, Is.EqualTo(32));
        Assert.That(sheet.CueMarkOffsets, Does.Contain(32), "The mandatory final Cue Mark always sits on the Phrase end.");
    }

    [Test]
    public void TimingWobbleOnTheCurrentPhraseNeverReRollsTheSheet()
    {
        FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 32);
        var builtOffsets = (int[])director.Status.CurrentSheet.CueMarkOffsets.Clone();

        // A later wake whose announcement wobbles the derived start by a beat must not rebuild the current
        // sheet: it is keyed to the announcement it was built from and only rebuilds when absent.
        FeedBeat(beat: 605, phraseStartBeat: 599, phraseLengthBeats: 32);

        Assert.That(director.Status.CurrentSheet.PhraseStartBeat, Is.EqualTo(600), "The current sheet rides its original announcement.");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(builtOffsets), "Wobble on an unchanged announcement never re-rolls a sheet.");
    }

    [Test]
    public void AChangedNextAnnouncementRebuildsTheNextSheetWhileAnUnchangedOneDoesNot()
    {
        FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 32, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 32);
        Assert.That(director.Status.NextSheet.PhraseStartBeat, Is.EqualTo(632));
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(32));
        var firstNextOffsets = (int[])director.Status.NextSheet.CueMarkOffsets.Clone();

        // Same announcement -> no rebuild.
        FeedBeat(beat: 605, phraseStartBeat: 600, phraseLengthBeats: 32, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 32);
        Assert.That(director.Status.NextSheet.CueMarkOffsets, Is.EqualTo(firstNextOffsets), "An unchanged next announcement never re-rolls the next sheet.");

        // Changed announcement (different length) -> rebuild.
        FeedBeat(beat: 606, phraseStartBeat: 600, phraseLengthBeats: 32, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 48);
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(48), "A changed next announcement rebuilds the next sheet.");
    }

    [Test]
    public void PhraseTurnoverPromotesTheNextSheetToCurrent()
    {
        FeedBeat(beat: 610, phraseStartBeat: 600, phraseLengthBeats: 16, nextPhraseStartBeat: 616, nextPhraseLengthBeats: 16);
        Assert.That(director.Status.CurrentSheet.PhraseStartBeat, Is.EqualTo(600), "Setup: current sheet is the 600 Phrase.");

        // Beat 616 is the current Phrase's end: the next sheet becomes current and the emptied slot refills.
        FeedBeat(beat: 616, phraseStartBeat: 616, phraseLengthBeats: 16, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 16);

        Assert.That(director.Status.CurrentSheet.PhraseStartBeat, Is.EqualTo(616), "Turnover promotes next to current.");
        Assert.That(director.Status.NextSheet.PhraseStartBeat, Is.EqualTo(632), "The emptied next slot refills by the same check.");
    }

    [Test]
    public void AnInvalidPhraseLengthIsTreatedAsNoAnnouncementAndNeverThrows()
    {
        // 20 is not a multiple of one Grid; the pure builder throws on such lengths, so the reducer must
        // guard it as no usable announcement rather than catch the exception as flow control.
        Assert.That(() => FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 20), Throws.Nothing);
        Assert.That(director.Status.CurrentSheet.HasSheet, Is.False);
    }

    // ---- Once-per-beat ---------------------------------------------------------------------------

    [Test]
    public void RepeatedFramesWithinABeatChangeNothing()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: the new Grid casts a cue at beat 616.");

        var effectDeckAfterCast = (int[])controller.effectDeck.Clone();
        var transitionDeckAfterCast = (int[])controller.transitionDeck.Clone();
        var loadedCueMark = switcher.LoadedCueStatus.CueMarkBeat;

        // Another frame on the same beat: the decision path must not run again.
        director.Tick(0f);
        director.Tick(0f);

        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckAfterCast), "A repeated frame within a beat rotates no deck.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckAfterCast));
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(loadedCueMark));
    }

    // ---- New Grid detection ----------------------------------------------------------------------

    [Test]
    public void ANewGridCarryingACueMarkCastsExactlyOneCueOnTheRightBeat()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "The first Grid reading joins mid-Grid and casts nothing.");

        // The 16-count wraps 16 -> 1: the Grid [616, 632) begins, and it carries the Phrase-end Cue Mark 632.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "The cast aims at the Cue Mark the new Grid carries.");
    }

    [Test]
    public void ADroppedPacketThatSkipsTheOneStillCastsOnTheWrap()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);

        // The One (Grid beat 1) packet dropped: the next reading is Grid beat 2 at absolute beat 617. That is
        // still a backward wrap, so the new Grid [616, 632) is not skipped and its Cue Mark 632 still casts.
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "A dropped One must not skip a Grid.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632));
    }

    [Test]
    public void GridBeatEqualToOneIsNotByItselfANewGrid()
    {
        // Joining exactly on the One (no prior count to wrap from) is the first reading — it casts nothing —
        // and the following forward move is not a new Grid either. Equality with 1 never triggers a cast.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False);
    }

    // ---- Lazy, preference-based casting -----------------------------------------------------------

    [Test]
    public void ADropOnTheNextGridCastsADropCapablePerformer()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);

        // The Cue Mark is 632; a Drop starting on beat 632 lands on the front of the next Grid [632, 648).
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 16);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(2), "The Drop makes the Drop-capable Performer preferred.");
    }

    [Test]
    public void AFillOnThisGridCastsAFillCapableTransition()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);

        // A Fill starting on beat 623 lands on this Grid [616, 632), so a Fill-capable Transition is preferred.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, fillBeatsUntilStart: 7);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(2), "The Fill makes the Fill-capable Transition preferred.");
    }

    [Test]
    public void APreferenceWithNoCapablePerformerStillCastsANonCapableOne()
    {
        // The preference is a lean, never a filter: with no Drop-capable Performer reachable in the deck, a Drop
        // still casts — with a non-capable Performer — rather than casting nothing or collapsing variety onto the
        // few capable cards. effects[2] is the only Drop-capable Performer, so a deck without it has none.
        var effectDeckWithoutDropCapable = new[] { 0, 1, 3 };
        director = new Director(
            controller,
            switcher,
            controller.timer,
            effectDeckWithoutDropCapable,
            controller.transitionDeck,
            controller.currentTransition);
        controller.director = director;

        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        // Cue Mark 632; a Drop starting on beat 632 lands on the front of the next Grid [632, 648).
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 16);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "The cast still happens with no capable Performer available.");
        Assert.That(
            controller.EffectiveRepertoire(switcher.LoadedCueStatus.TargetEffectIndex) & Repertoire.HandlesDrop,
            Is.EqualTo(Repertoire.None),
            "A non-capable Performer is cast; the preference never filters casting down to capable cards only.");
    }

    [Test]
    public void EnergyOnTheWireNeverDivertsTheCast()
    {
        // Energy is a Performer/Transition input read from BeatManager, never a Director casting input (ADR-0011).
        // The same Drop casts the same Drop-capable Performer whether the energy lane reads far-off or imminent,
        // observed at the Switcher handoff — the reducer consults it for nothing.
        var underLowEnergy = CastDropTargetWithEnergy(energyBeatsUntilChange: 64);
        var underHighEnergy = CastDropTargetWithEnergy(energyBeatsUntilChange: 1);

        Assert.That(underLowEnergy, Is.EqualTo(2), "The Drop preference casts the Drop-capable Performer.");
        Assert.That(underHighEnergy, Is.EqualTo(underLowEnergy), "Changing only the energy lane never changes the cast.");
    }

    [Test]
    public void AFillLandingExactlyOnTheCueMarkIsOffThisGridAndAddsNoPreference()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);

        // Fill start = 616 + 16 = 632 = the Cue Mark. The Fill window is [beat, cueMark), exclusive at the mark,
        // so a Fill on the mark belongs to the next Grid, not this one: no Fill preference flavors the cast, and
        // the staged (non-Fill) Transition 0 is cast unflavored rather than the Fill-capable Transition 2.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, fillBeatsUntilStart: 16);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0), "A Fill on the mark is off this Grid; the cast is unflavored.");
    }

    [Test]
    public void ADropLandingPastTheNextGridWindowIsExcludedAndAddsNoPreference()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);

        // Drop start = 616 + 32 = 648 = cueMark + 16 = the exclusive far edge of the next-Grid window [632, 648).
        // A Drop there lands on the Grid after next, so it does not flavor this Cue: the staged (non-Drop)
        // Transition 0 is cast, not the Drop-capable Transition 1 a real next-Grid Drop would prefer.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 32);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True);
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(0), "A Drop past the next-Grid window adds no preference; the cast is unflavored.");
    }

    [Test]
    public void DirectorCuesTheLatestStagedEffectAtTheNextCommittableMark()
    {
        // Contract preserved by name: a manually staged Effect is cast at the next Cue Mark and never re-aims
        // a cue already loaded.
        director.SetNextEffect(1);
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "The staged Performer is cast at the mark.");

        // Staging a different Effect now must not re-aim the loaded cue.
        director.SetNextEffect(3);
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "A later staged Effect never re-aims a loaded cue; it waits for the next mark.");
    }

    // ---- Deck discipline --------------------------------------------------------------------------

    [Test]
    public void ARejectedOfferBurnsNothing()
    {
        // Lock a cue far in the future directly in the Switcher: load it before its Lock Point, then latch the
        // lock with a second offer at its Lock Point. A locked cue rejects any differing offer.
        var lockedRepertoire = controller.transitions[0].Repertoire;
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(700, targetEffectIndex: 1, transitionIndex: 0, lockedRepertoire),
            new SwitcherClockSnapshot(currentBeat: 690, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(700, targetEffectIndex: 1, transitionIndex: 0, lockedRepertoire),
            new SwitcherClockSnapshot(currentBeat: 696, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 12f));
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "Setup: the loaded cue is locked at its Lock Point.");

        var effectDeckBefore = (int[])controller.effectDeck.Clone();
        var transitionDeckBefore = (int[])controller.transitionDeck.Clone();

        // The Director now casts toward the new Grid's Cue Mark 632 — a differing offer the locked cue rejects.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(700), "The locked cue rides; the rejected offer changed nothing.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBefore), "A rejected offer pulls no effect deck card.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckBefore), "A rejected offer pulls no transition deck card.");
    }

    [Test]
    public void AnAcceptedCastRotatesTheDecks()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        var effectDeckBefore = (int[])controller.effectDeck.Clone();

        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: the cast is accepted.");
        Assert.That(controller.effectDeck, Is.Not.EqualTo(effectDeckBefore), "An accepted cast pulls and re-stages, rotating the deck.");
    }

    // ---- Standalone boundary ----------------------------------------------------------------------

    [Test]
    public void LeavingSyncedModeAbortsTheLoadedCueAndClearsSheetMemory()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: a cue is loaded while Synced.");

        // The clock drops: the 4-count sentinel clears IsSynced, so the next Tick runs the Standalone path.
        controller.beatManager.beatData.snapshot.bpm = -1f;
        controller.beatManager.beatData.snapshot.beatInBar = -1;
        controller.beatManager.beatData.snapshot.beat = new BeatPosition { current = -1, total = -1 };
        director.Tick(1f);

        Assert.That(director.Status.Mode, Is.EqualTo(DirectorMode.Standalone));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Entering Standalone aborts the loaded cue.");
        Assert.That(director.Status.CurrentSheet.HasSheet, Is.False, "Sheet memory does not cross a Standalone gap.");
    }

    // Builds a fresh Switcher+Director pipeline, feeds a next-Grid Drop scenario carrying the given energy-lane
    // value, and returns the Performer index cast at the Switcher handoff. Fresh decks each call keep the two
    // runs independent so the only difference between them is the energy value.
    private int CastDropTargetWithEnergy(int energyBeatsUntilChange)
    {
        switcher = new Switcher(controller, controller.effects, controller.transitions);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;
        director = new Director(
            controller,
            switcher,
            controller.timer,
            new[] { 0, 1, 2, 3 },
            new[] { 0, 1, 2 },
            controller.currentTransition);
        controller.director = director;

        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32, energyBeatsUntilChange: energyBeatsUntilChange);
        // Cue Mark 632; a Drop starting on beat 632 lands on the front of the next Grid [632, 648).
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 16, energyBeatsUntilChange: energyBeatsUntilChange);
        return switcher.LoadedCueStatus.TargetEffectIndex;
    }

    // ---- Snapshot helpers -------------------------------------------------------------------------

    // Feeds one synced BeatManager frame and ticks the Director once. gridBeat < 1 leaves the wall off the
    // Grid (no cast); a present Phrase describes the current Phrase [phraseStartBeat, +phraseLengthBeats).
    private void FeedBeat(
        int beat,
        int phraseStartBeat,
        int phraseLengthBeats,
        int gridBeat = -1,
        int? nextPhraseStartBeat = null,
        int? nextPhraseLengthBeats = null,
        int? dropBeatsUntilStart = null,
        int? fillBeatsUntilStart = null,
        int? energyBeatsUntilChange = null)
    {
        var snapshot = controller.beatManager.beatData.snapshot;
        snapshot.bpm = 120f;
        snapshot.beat = new BeatPosition { current = beat, total = -1 };
        snapshot.beatInBar = ((beat - 1) % 4) + 1;
        snapshot.timingGrid = gridBeat >= 1
            ? new TimingGrid { beat = gridBeat, bar = ((gridBeat - 1) / 4) + 1, state = "locked" }
            : TimingGrid.Unavailable;
        snapshot.phraseState = new PhraseState
        {
            label = "Phrase",
            countBeats = phraseStartBeat + phraseLengthBeats - beat,
            lengthBeats = phraseLengthBeats,
            irregular = 0,
        };
        snapshot.nextPhraseState = nextPhraseStartBeat is { } nextStart && nextPhraseLengthBeats is { } nextLength
            ? new LabeledCountdown { label = "Next", countBeats = nextStart - beat, lengthBeats = nextLength }
            : LabeledCountdown.Unavailable;
        snapshot.dropState = dropBeatsUntilStart is { } dropStart
            ? new CountdownState { active = 0, countBeats = dropStart, lengthBeats = 16, remaining = 1 }
            : CountdownState.Unavailable;
        snapshot.fillState = fillBeatsUntilStart is { } fillStart
            ? new CountdownState { active = 0, countBeats = fillStart, lengthBeats = 8, remaining = 1 }
            : CountdownState.Unavailable;
        // An energy lane the reducer must never consult for casting (ADR-0011): present so tests can prove the
        // cast outcome is invariant to it, not because the Director reads it.
        snapshot.energyState = energyBeatsUntilChange is { } energyChange
            ? new LabeledCountdown { label = "Energy", countBeats = energyChange, lengthBeats = 16 }
            : LabeledCountdown.Unavailable;
        controller.beatManager.beatData.snapshot = snapshot;
        director.Tick(0f);
    }

    private static void SetControllerSingleton(Controller instance)
    {
        typeof(Singleton<Controller>)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, instance);
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

    private sealed class TailedTransition : TransitionBase
    {
        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                RepertoireFlags.None,
                runwayBeats: 4,
                tailBeats: 4,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
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

    private sealed class EventTransition : TransitionBase
    {
        private readonly RepertoireFlags tags;
        private readonly int runwayBeats;
        private readonly int tailBeats;

        public EventTransition(RepertoireFlags tags, int runwayBeats, int tailBeats)
        {
            this.tags = tags;
            this.runwayBeats = runwayBeats;
            this.tailBeats = tailBeats;
        }

        protected override TransitionSettings BuildCodeDefaults()
        {
            return TransitionSettings.FromRepertoire(TransitionRepertoire.FromRunwayAndTail(
                tags,
                runwayBeats,
                tailBeats,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
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
