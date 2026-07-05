using System.IO;
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

        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("Phrase"), "The current sheet rides its original announcement.");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(builtOffsets), "Wobble on an unchanged announcement never re-rolls a sheet.");
    }

    [Test]
    public void AChangedNextAnnouncementRebuildsTheNextSheetWhileAnUnchangedOneDoesNot()
    {
        FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 32, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 32);
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("Next"));
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
        // Labels rotate as the wire would across a real boundary: the Phrase announced as next ("B") becomes
        // the current Phrase after turnover, and a fresh next ("C") is announced.
        FeedBeat(beat: 610, phraseStartBeat: 600, phraseLengthBeats: 16, nextPhraseStartBeat: 616, nextPhraseLengthBeats: 16, phraseLabel: "A", nextPhraseLabel: "B");
        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("A"), "Setup: current sheet is the A Phrase.");

        // Beat 616 is the current Phrase's end (its countdown wraps): the next sheet becomes current and the
        // emptied slot refills.
        FeedBeat(beat: 616, phraseStartBeat: 616, phraseLengthBeats: 16, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 16, phraseLabel: "B", nextPhraseLabel: "C");

        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("B"), "Turnover promotes next (B) to current.");
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("C"), "The emptied next slot refills (C) by the same check.");
    }

    [Test]
    public void AnUpwardCountdownRollMidPhraseIsATurnoverByDesign()
    {
        // The phrase lane fires on any upward roll of the countdown, with no monotonicity or near-boundary
        // guard: a DJ looping a section rolls the countdown back up mid-phrase, and that roll IS a turnover —
        // the instance increments and a PHRASE_TURNOVER logs with identical sides. This pins the accepted
        // phantom-instance cosmetic (live evidence: 2026-07-05 session logs) so a future "fix" cannot silently
        // add the wobble-suppression guard the design deliberately omits.
        var log = WireCueLogDirector();

        // Mid-phrase in a Chorus/96: countdown 86, far from any boundary.
        FeedBeat(beat: 610, phraseStartBeat: 600, phraseLengthBeats: 96, gridBeat: 11, phraseLabel: "Chorus");
        Assert.That(log.ToString(), Does.Not.Contain("PHRASE_TURNOVER"), "Setup: no turnover before the roll.");

        // The DJ jumps the loop back: two beats later the countdown reads higher (86 -> 100).
        FeedBeat(beat: 612, phraseStartBeat: 616, phraseLengthBeats: 96, gridBeat: 13, phraseLabel: "Chorus");

        Assert.That(
            log.ToString(),
            Does.Contain("PHRASE_TURNOVER").And.Contain("out=\"Chorus\"/96 in=\"Chorus\"/96 instance=1"),
            "An upward countdown roll mid-phrase fires a turnover with identical sides — the accepted phantom instance.");
    }

    [Test]
    public void AnInvalidPhraseLengthIsTreatedAsNoAnnouncementAndNeverThrows()
    {
        // A non-positive length is the only "no announcement" case left: the builder now accepts any positive
        // length, so the reducer treats 0 (an absent or zero-length lane) as nothing to build rather than a
        // length to roll — and the pure builder, which throws on <= 0, is never reached.
        Assert.That(() => FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 0), Throws.Nothing);
        Assert.That(director.Status.CurrentSheet.HasSheet, Is.False);
    }

    [Test]
    public void ASameIdentitySuccessorGetsItsOwnNextSheetInsteadOfBeingSuppressed()
    {
        // Two consecutive Chorus/64 phrases are two real phrases on the wire. Instances are wrap ordinals, not
        // names: the next slot is keyed to the coming instance, so the second Chorus gets its own next sheet
        // even though it shares the current announcement's (label, length) — the case a name comparison collapses.
        FeedBeat(beat: 660, phraseStartBeat: 600, phraseLengthBeats: 64, phraseLabel: "Chorus",
            nextPhraseStartBeat: 664, nextPhraseLengthBeats: 64, nextPhraseLabel: "Chorus");

        Assert.That(director.Status.NextSheet.HasSheet, Is.True, "The same-identity successor gets its own next sheet, not the empty slot a name guard leaves.");
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("Chorus"));
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(64));

        // The wrap promotes that real sheet to current; the emptied slot refills from the fresh next (Drop).
        FeedBeat(beat: 664, phraseStartBeat: 664, phraseLengthBeats: 64, phraseLabel: "Chorus",
            nextPhraseStartBeat: 728, nextPhraseLengthBeats: 16, nextPhraseLabel: "Drop");

        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("Chorus"), "The wrap promotes the real Chorus sheet built for the coming instance.");
        Assert.That(director.Status.CurrentSheet.PhraseLengthBeats, Is.EqualTo(64));
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("Drop"), "The emptied next slot refills for the next instance.");
    }

    [Test]
    public void AWireEchoOfCurrentAsNextBuildsOneSheetReRolledOnTheRealAnnouncement()
    {
        // Accepted trade-off: when the wire briefly echoes the current phrase as next, the ordinal guard builds
        // one sheet for the coming instance even though it duplicates the current (label, length). It is
        // deterministic and idempotent, and the recast-on-change path re-rolls it the moment the lane changes.
        FeedBeat(beat: 604, phraseStartBeat: 600, phraseLengthBeats: 16, phraseLabel: "A",
            nextPhraseStartBeat: 616, nextPhraseLengthBeats: 16, nextPhraseLabel: "A");

        Assert.That(director.Status.NextSheet.HasSheet, Is.True, "The echo builds one next sheet for the coming instance.");
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("A"), "The one extra build carries the echoed identity.");

        // The wire now announces the real next phrase; no wrap yet (the current countdown still descends), so the
        // sheet for the coming instance is re-rolled in place to the real announcement.
        FeedBeat(beat: 605, phraseStartBeat: 600, phraseLengthBeats: 16, phraseLabel: "A",
            nextPhraseStartBeat: 616, nextPhraseLengthBeats: 16, nextPhraseLabel: "B");

        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("B"), "The recast-on-change path re-rolls the coming instance's slot to the real announcement.");
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(16));
        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("A"), "The current sheet is untouched through the echo and its correction.");
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

    // ---- Irregular-phrase boundary Cue ------------------------------------------------------------

    [Test]
    public void AnIrregularPhraseStillCarriesItsBoundaryCueWithinTheGrid()
    {
        // 13:26 replay: Intro/40 is irregular, but it now builds the sheet [40] — a single mandatory end mark.
        // The cast outcome is unchanged, reached via the carried-mark path: on the Grid wake that brings the
        // Phrase end within one Grid (beatsUntilNext == 16), the carried mark 40 casts at beat 640, offset [40/40].
        var log = WireCueLogDirector();

        FeedBeat(beat: 623, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 40, phraseLabel: "Intro");
        Assert.That(director.Status.CurrentSheet.HasSheet, Is.True, "An irregular Phrase length now builds a sheet of just its end mark.");
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "The mid-Grid join casts nothing.");

        FeedBeat(beat: 624, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 40, phraseLabel: "Intro");

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(640), "The boundary Cue is minted at the Phrase-end beat.");
        StringAssert.Contains("cue=640[40/40]", log.ToString(), "The cast carries the boundary offset and announced length.");
        StringAssert.Contains("phrase=\"Intro\"", log.ToString(), "Display context is the sheet slot's label.");
    }

    [Test]
    public void AShortIrregularPhraseCarriesItsBoundaryCue()
    {
        // Up/24, the other 13:26 shape: boundary at beat 624, cast on the Grid wake at 608 (beatsUntilNext == 16).
        var log = WireCueLogDirector();

        FeedBeat(beat: 607, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 24, phraseLabel: "Up");
        FeedBeat(beat: 608, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 24, phraseLabel: "Up");

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(624), "The boundary Cue is minted at the Phrase-end beat.");
        StringAssert.Contains("cue=624[24/24]", log.ToString(), "The cast carries the boundary offset and announced length.");
        StringAssert.Contains("phrase=\"Up\"", log.ToString(), "Display context is the sheet slot's label.");
    }

    [Test]
    public void AnIrregularBoundaryFartherThanOneGridIsNotYetCast()
    {
        // Accepted edge: Intro/40 builds [40], but at this wake the carried mark (position 8 + one Grid == 24)
        // coincides with no sheet mark, and the lone end mark 40 is still two Grids out. Nothing casts until the
        // boundary lands within one Grid.
        WireCueLogDirector();

        FeedBeat(beat: 607, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 40, phraseLabel: "Intro");
        FeedBeat(beat: 608, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 40, phraseLabel: "Intro");

        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "A boundary more than one Grid away is not yet cast.");
    }

    [Test]
    public void ARegularPhraseStillCastsFromItsSheetNotTheBoundaryBranch()
    {
        // Regular phrases have sheets by cast time, so the no-sheet boundary branch is unreachable for them: the
        // 32-beat Phrase still casts its sheet's mandatory end mark exactly as before, with slot-sourced context.
        var log = WireCueLogDirector();

        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(director.Status.CurrentSheet.HasSheet, Is.True, "A regular Phrase length builds a sheet.");

        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "The sheet's phrase-end mark casts unchanged.");
        StringAssert.Contains("cue=632[32/32]", log.ToString(), "The regular cast carries the sheet offset and length.");
    }

    [Test]
    public void AnIrregularPhraseEndCastsOnTheReAnchoredFinalGrid()
    {
        // Chorus/56 is irregular: its sheet [16, 32, 56] ends on the Phrase end, and the wire re-anchors the grid
        // there. On the final partial Grid the phrase countdown (8) is shorter than a full-Grid extrapolation
        // (16), so the boundary read takes the countdown and the carried mark lands on the Phrase end rather than
        // a flat Grid out. Position 48, gridBeat 1: the cast targets beat phraseStart + 56 == 656, offset [56/56].
        var log = WireCueLogDirector();

        FeedBeat(beat: 647, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 56, phraseLabel: "Chorus");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(new[] { 16, 32, 56 }), "Setup: Chorus/56 carries marks [16, 32, 56].");

        FeedBeat(beat: 648, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 56, phraseLabel: "Chorus");

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(656), "The re-anchored final Grid casts the Phrase-end mark (phraseStart + 56).");
        StringAssert.Contains("cue=656[56/56]", log.ToString(), "The cast targets the Phrase end via the shorter phrase countdown, not a flat Grid out.");
        StringAssert.Contains("phrase=\"Chorus\"", log.ToString(), "Display context is the sheet slot's label.");
    }

    // ---- Starvation guard ------------------------------------------------------------------------

    [Test]
    public void AWallStarvedPastSixtyFourBeatsCastsFromTheGuard()
    {
        // 13:55 replay shape: a Break/96 sheet (marks [48, 80, 96]) is present, but the DJ loops the Phrase below
        // its first mark, so the live position holds at 0 and the carried mark (position + 16 == 16) never lands
        // on a mark. Four Grid wraps pass with no accepted cast; on the fourth the guard casts one Grid out —
        // beatsToMark 16, offset [16/96], lane-sourced Break — so the wall changes on the 64th beat.
        var log = WireCueLogDirector();

        // Build the current sheet, then loop: phraseStart tracks the beat so the position holds at 0 (Break/96).
        FeedBeat(beat: 601, gridBeat: 16, phraseStartBeat: 601, phraseLengthBeats: 96, phraseLabel: "Break");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(new[] { 48, 80, 96 }), "Setup: Break/96 carries marks [48, 80, 96].");

        FeedBeat(beat: 602, gridBeat: 1, phraseStartBeat: 602, phraseLengthBeats: 96, phraseLabel: "Break");   // wrap 1
        FeedBeat(beat: 603, gridBeat: 16, phraseStartBeat: 603, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 604, gridBeat: 1, phraseStartBeat: 604, phraseLengthBeats: 96, phraseLabel: "Break");   // wrap 2
        FeedBeat(beat: 605, gridBeat: 16, phraseStartBeat: 605, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 606, gridBeat: 1, phraseStartBeat: 606, phraseLengthBeats: 96, phraseLabel: "Break");   // wrap 3
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Three starved wraps do not yet reach the guard's ceiling.");

        FeedBeat(beat: 607, gridBeat: 16, phraseStartBeat: 607, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 608, gridBeat: 1, phraseStartBeat: 608, phraseLengthBeats: 96, phraseLabel: "Break");   // wrap 4

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(624), "The guard casts one Grid out, on the 64th beat after the last change.");
        StringAssert.Contains("cue=624[16/96]", log.ToString(), "The guard cast carries beatsToMark 16 and the live offset and length.");
        StringAssert.Contains("phrase=\"Break\"", log.ToString(), "Display context is lane-sourced.");
    }

    [Test]
    public void AStarvationGuardCastFromAMidGridWakeStillLandsOnTheGridBoundary()
    {
        // Same starved loop as above, but the fourth wake is a dropped One (the grid lane lands on 9, not 1).
        // The guard targets the same live beatsToBoundary the carried path does — 16 - (9 - 1) = 8 — so its
        // cast lands on the true Grid Boundary (608 + 8 = 616), never a flat Grid past a mid-Grid wake.
        var log = WireCueLogDirector();

        FeedBeat(beat: 601, gridBeat: 16, phraseStartBeat: 601, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 602, gridBeat: 1, phraseStartBeat: 602, phraseLengthBeats: 96, phraseLabel: "Break");   // wake 1
        FeedBeat(beat: 603, gridBeat: 16, phraseStartBeat: 603, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 604, gridBeat: 1, phraseStartBeat: 604, phraseLengthBeats: 96, phraseLabel: "Break");   // wake 2
        FeedBeat(beat: 605, gridBeat: 16, phraseStartBeat: 605, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 606, gridBeat: 1, phraseStartBeat: 606, phraseLengthBeats: 96, phraseLabel: "Break");   // wake 3
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Three starved wakes do not yet reach the guard's ceiling.");

        FeedBeat(beat: 607, gridBeat: 16, phraseStartBeat: 607, phraseLengthBeats: 96, phraseLabel: "Break");
        FeedBeat(beat: 608, gridBeat: 9, phraseStartBeat: 608, phraseLengthBeats: 96, phraseLabel: "Break");   // wake 4, dropped One

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(616), "The guard casts at the true next Boundary (608 + 8), not a flat Grid out.");
        StringAssert.Contains("cue=616[8/96]", log.ToString(), "The guard cast carries the live beatsToBoundary and offset.");
    }

    [Test]
    public void AStarvedLoopingPhraseStillReachesTheGuard()
    {
        // The guard is cause-agnostic: a looping Phrase whose sheet carries no mark where the held position lands
        // starves the wall exactly like a loop below a sheet's first mark. Outro/56 builds [32, 56]; the DJ loops
        // it so the position holds at 0, and the carried mark (position + one Grid == 16) coincides with no sheet
        // mark, so the loop never casts. Four Grid wraps pass with no accepted cast; on the fourth the guard casts
        // one Grid out — beatsToMark 16, offset [16/56], lane-sourced Outro — so the wall changes on the 64th beat.
        var log = WireCueLogDirector();

        FeedBeat(beat: 601, gridBeat: 16, phraseStartBeat: 601, phraseLengthBeats: 56, phraseLabel: "Outro");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(new[] { 32, 56 }), "Setup: Outro/56 carries marks [32, 56].");

        FeedBeat(beat: 602, gridBeat: 1, phraseStartBeat: 602, phraseLengthBeats: 56, phraseLabel: "Outro");    // wrap 1
        FeedBeat(beat: 603, gridBeat: 16, phraseStartBeat: 603, phraseLengthBeats: 56, phraseLabel: "Outro");
        FeedBeat(beat: 604, gridBeat: 1, phraseStartBeat: 604, phraseLengthBeats: 56, phraseLabel: "Outro");    // wrap 2
        FeedBeat(beat: 605, gridBeat: 16, phraseStartBeat: 605, phraseLengthBeats: 56, phraseLabel: "Outro");
        FeedBeat(beat: 606, gridBeat: 1, phraseStartBeat: 606, phraseLengthBeats: 56, phraseLabel: "Outro");    // wrap 3
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Three starved wraps do not yet reach the guard's ceiling.");

        FeedBeat(beat: 607, gridBeat: 16, phraseStartBeat: 607, phraseLengthBeats: 56, phraseLabel: "Outro");
        FeedBeat(beat: 608, gridBeat: 1, phraseStartBeat: 608, phraseLengthBeats: 56, phraseLabel: "Outro");    // wrap 4

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(624), "The guard casts one Grid out, on the 64th beat after the last change.");
        StringAssert.Contains("cue=624[16/56]", log.ToString(), "The guard cast carries beatsToMark 16 and the live offset and length.");
        StringAssert.Contains("phrase=\"Outro\"", log.ToString(), "Display context is lane-sourced.");
    }

    [Test]
    public void AHealthyPhraseCastsFromItsSheetAndNeverTripsTheGuard()
    {
        // The counter resets on every accepted cast, so a sheet that provides within four wraps keeps the guard
        // idle. Chorus/96's first mark (48) is reached on the second wrap and casts from the sheet; the guard,
        // whose ceiling is four wraps, is never in reach.
        var log = WireCueLogDirector();

        FeedBeat(beat: 584, gridBeat: 16, phraseStartBeat: 584, phraseLengthBeats: 96, phraseLabel: "Chorus");  // build
        FeedBeat(beat: 600, gridBeat: 1, phraseStartBeat: 584, phraseLengthBeats: 96, phraseLabel: "Chorus");   // wrap 1, position 16
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "The first wrap misses every mark; the guard does not jump in early.");

        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 584, phraseLengthBeats: 96, phraseLabel: "Chorus");
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 584, phraseLengthBeats: 96, phraseLabel: "Chorus");   // wrap 2, position 32 -> mark 48

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "The sheet's mark 48 casts on the second wrap, well inside the guard's ceiling.");
        StringAssert.Contains("cue=632[48/96]", log.ToString(), "The cast carries the sheet mark, not a guard offer.");
    }

    [Test]
    public void ASheetProvidingAtTheGuardCeilingCastsItsOwnMarkNotTheGuard()
    {
        // The carried mark is checked before the guard, so a legal 64-gap sheet is never overridden. Hook/96's
        // first mark sits a full four Grids (64 beats) past the Phrase start — the maximum legal gap — and lands
        // exactly on the wake the counter reaches four. That wake is mid-Grid (a dropped One, beatsToMark 8), so
        // the sheet's mark 64 differs from the guard's would-be offer (position 56 + 16 == 72): the cast must be
        // the sheet's.
        var log = WireCueLogDirector();

        FeedBeat(beat: 500, gridBeat: 16, phraseStartBeat: 500, phraseLengthBeats: 96, phraseLabel: "Hook");    // build Hook/96 [64, 96]
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(new[] { 64, 96 }), "Setup: Hook/96's first mark is a full 64 beats out.");

        FeedBeat(beat: 516, gridBeat: 1, phraseStartBeat: 516, phraseLengthBeats: 96, phraseLabel: "Hook");     // wrap 1, position 0
        FeedBeat(beat: 531, gridBeat: 16, phraseStartBeat: 516, phraseLengthBeats: 96, phraseLabel: "Hook");
        FeedBeat(beat: 532, gridBeat: 1, phraseStartBeat: 516, phraseLengthBeats: 96, phraseLabel: "Hook");     // wrap 2, position 16
        FeedBeat(beat: 547, gridBeat: 16, phraseStartBeat: 516, phraseLengthBeats: 96, phraseLabel: "Hook");
        FeedBeat(beat: 548, gridBeat: 1, phraseStartBeat: 516, phraseLengthBeats: 96, phraseLabel: "Hook");     // wrap 3, position 32
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.False, "Three starved wraps pass; none lands on mark 64.");

        FeedBeat(beat: 563, gridBeat: 16, phraseStartBeat: 508, phraseLengthBeats: 96, phraseLabel: "Hook");
        FeedBeat(beat: 564, gridBeat: 9, phraseStartBeat: 508, phraseLengthBeats: 96, phraseLabel: "Hook");     // wrap 4, position 56, mark 64 is 8 out

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(572), "The carried mark 64 casts 8 beats out (564 + 8), not the guard's 16.");
        StringAssert.Contains("cue=572[64/96]", log.ToString(), "The sheet's own mark 64 casts, not the guard's position + 16.");
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

    [Test]
    public void DirectorCuesOnlyTheLatestOfSeveralStagedEffectsAtTheMark()
    {
        // Sibling to DirectorCuesTheLatestStagedEffectAtTheNextCommittableMark, covering the repeated-staging
        // half of the contract: several manual stagings land before the mark, and only the LAST is cast.
        // Staging overwrites the pending choice each time; it never queues earlier stagings.
        director.SetNextEffect(1);
        director.SetNextEffect(2);
        director.SetNextEffect(3);

        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(3), "The latest staged Performer is the one cued at the mark; earlier stagings are overwritten, not queued.");
    }

    [Test]
    public void StagingADifferentEffectWhileACueIsLoadedLeavesThatCueUntouched()
    {
        // Manually staged choices never re-aim a loaded cue; they wait for the next Cue Mark. A cue is loaded,
        // the operator stages a different Effect, and the Grid loops back onto the SAME Cue Mark — a keep, so
        // the staged choice does not take and the loaded cue is untouched.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "Setup: the cue is loaded for the mark 632.");

        var markWhileLoaded = switcher.LoadedCueStatus.CueMarkBeat;
        var targetWhileLoaded = switcher.LoadedCueStatus.TargetEffectIndex;
        var transitionWhileLoaded = switcher.LoadedCueStatus.TransitionIndex;
        var effectDeckWhileLoaded = (int[])controller.effectDeck.Clone();
        var transitionDeckWhileLoaded = (int[])controller.transitionDeck.Clone();

        // Stage a different Effect, step forward off the Grid Boundary, then loop back onto it so OfferCue runs
        // at the SAME Cue Mark 632. The Switcher answers "kept" — the staged choice waits for the next mark.
        var stagedEffect = targetWhileLoaded == 3 ? 2 : 3;
        director.SetNextEffect(stagedEffect);
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(markWhileLoaded), "A same-mark keep never moves the loaded cue's mark.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(targetWhileLoaded), "Staging over a kept cue never re-aims its target.");
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(transitionWhileLoaded), "Staging over a kept cue never re-aims its transition.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckWhileLoaded), "A kept cue pulls no effect deck card.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckWhileLoaded), "A kept cue pulls no transition deck card.");
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

    // ---- Replay on change -------------------------------------------------------------------------

    [Test]
    public void ASameMarkReOfferKeepsTheLoadedCueUnchanged()
    {
        // A grid loop that re-presents the SAME Grid Boundary re-offers the SAME Cue Mark. Identity on the
        // Switcher seam is the Cue Mark alone, so the loaded cue rides unchanged and is never re-flavored — the
        // Director makes no keep decision of its own; the Switcher's answer is the decision.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "Setup: a cue is loaded for the mark 632.");
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.False, "Setup: the loaded cue is still unlocked.");

        var effectDeckWhileLoaded = (int[])controller.effectDeck.Clone();
        var transitionDeckWhileLoaded = (int[])controller.transitionDeck.Clone();
        var targetWhileLoaded = switcher.LoadedCueStatus.TargetEffectIndex;
        var transitionWhileLoaded = switcher.LoadedCueStatus.TransitionIndex;

        // Step forward off the Grid Boundary, then loop back onto it: the 16-count wraps again onto the SAME
        // Cue Mark 632, so the offer is a keep.
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "A same-mark re-offer keeps the loaded cue.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(targetWhileLoaded), "The kept cue's target is untouched.");
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(transitionWhileLoaded), "The kept cue's transition is untouched.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckWhileLoaded), "Keeping a cue rotates no effect deck card.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckWhileLoaded), "Keeping a cue rotates no transition deck card.");
    }

    [Test]
    public void AMovedDropWithoutANewGridNeverReOffersTheLoadedCue()
    {
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "Setup: a cue is loaded for the mark 632.");

        var effectDeckWhileLoaded = (int[])controller.effectDeck.Clone();
        var transitionDeckWhileLoaded = (int[])controller.transitionDeck.Clone();
        var targetWhileLoaded = switcher.LoadedCueStatus.TargetEffectIndex;
        var transitionWhileLoaded = switcher.LoadedCueStatus.TransitionIndex;

        // A Drop is now announced landing on the next Grid, but the grid moves forward (no new Grid begins), so
        // nothing is offered: casting reads the fresh Fill/Drop evidence only when a Grid begins. The loaded cue
        // never picks up the late Drop preference.
        FeedBeat(beat: 617, gridBeat: 2, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 15);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "The loaded cue's mark is unchanged by the moved Drop.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(targetWhileLoaded), "The cue is not re-flavored toward a Drop-capable Performer.");
        Assert.That(switcher.LoadedCueStatus.TransitionIndex, Is.EqualTo(transitionWhileLoaded), "The cue is not re-flavored toward a Drop-capable Transition.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckWhileLoaded), "A forward grid move rotates no deck.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckWhileLoaded));
    }

    [Test]
    public void ADifferentMarkOfferReplacesAnUnlockedLoadedCueWhenANewGridBegins()
    {
        // Load an unlocked cue aimed at a mark 700 that no new Grid will carry. It is loaded before its Lock
        // Point and so is not locked.
        var repertoire = controller.transitions[0].Repertoire;
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(700, targetEffectIndex: 1, transitionIndex: 0, repertoire),
            new SwitcherClockSnapshot(currentBeat: 660, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.True, "Setup: a cue is loaded for the mark 700.");
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.False, "Setup: the loaded cue is unlocked.");

        var effectDeckBefore = (int[])controller.effectDeck.Clone();

        // A new Grid carrying the Cue Mark 632 begins — a different-mark offer. The loaded cue is unlocked, so
        // the Switcher replaces it, and the accepted load rotates the deck.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(632), "A different-mark offer replaces the unlocked loaded cue.");
        Assert.That(controller.effectDeck, Is.Not.EqualTo(effectDeckBefore), "The accepted load rotates the deck like any cast.");
    }

    [Test]
    public void ALockedLoadedCueRidesThroughALateDrop()
    {
        // Lock a cue directly by re-offering it at its Lock Point (its start time stays in the future of the
        // frozen editor clock, so it never fires). A new Grid then offers a different-mark, Drop-flavored recast;
        // the locked cue must ride and no drop-protection path may fire.
        var repertoire = controller.transitions[0].Repertoire;
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(700, targetEffectIndex: 1, transitionIndex: 0, repertoire),
            new SwitcherClockSnapshot(currentBeat: 690, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(700, targetEffectIndex: 1, transitionIndex: 0, repertoire),
            new SwitcherClockSnapshot(currentBeat: 695, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 12f));
        Assert.That(switcher.LoadedCueStatus.IsLocked, Is.True, "Setup: the cue is locked at its Lock Point.");

        var effectDeckBefore = (int[])controller.effectDeck.Clone();
        var transitionDeckBefore = (int[])controller.transitionDeck.Clone();

        // A new Grid begins carrying mark 632 with a Drop announced onto the next Grid — a Drop too late to
        // commit cleanly. The Director offers a Drop-flavored recast fire-and-forget; the locked cue rejects it.
        FeedBeat(beat: 615, gridBeat: 16, phraseStartBeat: 600, phraseLengthBeats: 32);
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, dropBeatsUntilStart: 16);

        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(700), "The locked cue rides through the late Drop.");
        Assert.That(switcher.LoadedCueStatus.TargetEffectIndex, Is.EqualTo(1), "No drop-protection path re-flavors the locked cue.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBefore), "A rejected recast pulls no effect deck card.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckBefore), "A rejected recast pulls no transition deck card.");
    }

    // ---- Expectation lanes --------------------------------------------------------------------------

    [Test]
    public void ASkewingNextCountdownWithAStableAnnouncementNeverReRollsTheNextSheet()
    {
        // The 10:29 storm replay: the wire's beat and its next-phrase countdown skew by a snapshot, so the
        // derived next start flip-flops between adjacent beats (129<->130) while the announced label and length
        // never change. Identity is the announced (label, length), so the wobble re-rolls nothing.
        FeedBeat(beat: 100, phraseStartBeat: 96, phraseLengthBeats: 32, nextPhraseStartBeat: 129, nextPhraseLengthBeats: 32);
        var builtOffsets = (int[])director.Status.NextSheet.CueMarkOffsets.Clone();
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(32), "Setup: next built from the 32-beat announcement.");

        var flipFlop = new[] { 130, 129, 130, 129, 130, 129 };
        for (var i = 0; i < flipFlop.Length; i++)
        {
            FeedBeat(beat: 101 + i, phraseStartBeat: 96, phraseLengthBeats: 32, nextPhraseStartBeat: flipFlop[i], nextPhraseLengthBeats: 32);
            Assert.That(director.Status.NextSheet.CueMarkOffsets, Is.EqualTo(builtOffsets), "A skewing countdown on an unchanged announcement never re-rolls the next sheet.");
            Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("Next"), "The next sheet rides its announced identity through the wobble.");
        }
    }

    [Test]
    public void AFlappingNextAnnouncementRebuildsOnlyTheNextSlot()
    {
        // The 10:32:32 deck-switch flap: next_phrase_state alternates between two real announcements. Each real
        // (label, length) change rebuilds the next sheet only; the current sheet and any loaded cue are
        // untouched (no Grid begins during the flap, so nothing is offered).
        FeedBeat(beat: 604, gridBeat: 6, phraseStartBeat: 600, phraseLengthBeats: 64, nextPhraseStartBeat: 664, nextPhraseLengthBeats: 32, nextPhraseLabel: "Chorus");
        var currentOffsets = (int[])director.Status.CurrentSheet.CueMarkOffsets.Clone();
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(32), "Setup: next announced as the 32-beat Chorus.");

        // Park an unlocked loaded cue directly in the Switcher; the flap must not touch it.
        switcher.UpsertLoadedCue(
            new SwitcherCueDirection(900, targetEffectIndex: 1, transitionIndex: 0, controller.transitions[0].Repertoire),
            new SwitcherClockSnapshot(currentBeat: 604, beatFraction: 0f, secondsPerBeat: 0.5f, nowSeconds: 10f));
        var effectDeckBefore = (int[])controller.effectDeck.Clone();

        // Flap next to the 16-beat "Up", then back to the 32-beat "Chorus". Grid advances forward, so no cast.
        FeedBeat(beat: 605, gridBeat: 7, phraseStartBeat: 600, phraseLengthBeats: 64, nextPhraseStartBeat: 664, nextPhraseLengthBeats: 16, nextPhraseLabel: "Up");
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(16), "The first flap rebuilds next to the 16-beat Up.");
        FeedBeat(beat: 606, gridBeat: 8, phraseStartBeat: 600, phraseLengthBeats: 64, nextPhraseStartBeat: 664, nextPhraseLengthBeats: 32, nextPhraseLabel: "Chorus");
        Assert.That(director.Status.NextSheet.PhraseLengthBeats, Is.EqualTo(32), "The flap back rebuilds next to the 32-beat Chorus.");

        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(currentOffsets), "The flap never disturbs the current sheet.");
        Assert.That(switcher.LoadedCueStatus.CueMarkBeat, Is.EqualTo(900), "The flap never disturbs the loaded cue.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBefore), "The flap rotates no deck.");
    }

    [Test]
    public void TurnoverOnTheExpectedWrapCarriesThePhraseEndMarkAsTheNextPhrasesFirstBeat()
    {
        // Turnover is the expected phrase_state countdown wrap, not an end-beat comparison. The current
        // countdown reaches its boundary on beat 616 — the beat it "would hit 0" is beat 1 of the next Phrase —
        // so the next sheet shifts to current and the emptied slot refills.
        FeedBeat(beat: 610, phraseStartBeat: 600, phraseLengthBeats: 16, nextPhraseStartBeat: 616, nextPhraseLengthBeats: 16, phraseLabel: "A", nextPhraseLabel: "B");
        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("A"), "Setup: current is the A Phrase.");
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("B"), "Setup: next is the B Phrase.");

        FeedBeat(beat: 616, phraseStartBeat: 616, phraseLengthBeats: 16, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 16, phraseLabel: "B", nextPhraseLabel: "C");

        Assert.That(director.Status.CurrentSheet.PhraseLabel, Is.EqualTo("B"), "The wrap promotes next to current with no end-beat arithmetic.");
        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Does.Contain(16), "The mandatory final Cue Mark sits on the Phrase end (offset == length).");
        Assert.That(director.Status.NextSheet.PhraseLabel, Is.EqualTo("C"), "The emptied next slot refills by the same check.");
    }

    [Test]
    public void ATrackCountJumpWithStableLanesChangesNothing()
    {
        // The Director watches the five lanes and does no timing of its own: a jump in the track's own beat
        // count, with every watched lane holding (same Grid count, same phrase and next announcements, same
        // countdowns), is not an event. Nothing rebuilds, promotes, or casts.
        FeedBeat(beat: 616, gridBeat: 1, phraseStartBeat: 600, phraseLengthBeats: 32, nextPhraseStartBeat: 632, nextPhraseLengthBeats: 64);
        var currentOffsets = (int[])director.Status.CurrentSheet.CueMarkOffsets.Clone();
        var nextOffsets = (int[])director.Status.NextSheet.CueMarkOffsets.Clone();
        var effectDeckBefore = (int[])controller.effectDeck.Clone();
        var transitionDeckBefore = (int[])controller.transitionDeck.Clone();
        var hadCueBefore = switcher.LoadedCueStatus.HasCue;

        // snapshot.beat leaps 616 -> 700, but a compensating phraseStart keeps every derived lane identical:
        // Grid count 1, phrase countdown 16 (length 32), next countdown 16 (length 64), same labels. A second
        // clock reading the track count would project a spurious phrase wrap here and re-roll the next sheet.
        FeedBeat(beat: 700, gridBeat: 1, phraseStartBeat: 684, phraseLengthBeats: 32, nextPhraseStartBeat: 716, nextPhraseLengthBeats: 64);

        Assert.That(director.Status.CurrentSheet.CueMarkOffsets, Is.EqualTo(currentOffsets), "A track-count jump with stable lanes never re-rolls or promotes the current sheet.");
        Assert.That(director.Status.NextSheet.CueMarkOffsets, Is.EqualTo(nextOffsets), "A track-count jump with stable lanes never re-rolls the next sheet.");
        Assert.That(switcher.LoadedCueStatus.HasCue, Is.EqualTo(hadCueBefore), "A track-count jump casts nothing; only a new Grid casts.");
        Assert.That(controller.effectDeck, Is.EqualTo(effectDeckBefore), "A track-count jump rotates no deck.");
        Assert.That(controller.transitionDeck, Is.EqualTo(transitionDeckBefore));
    }

    // Builds a fresh Switcher+Director pipeline whose Director owns a Cue Log writing to the returned buffer, so
    // a test can read the frozen CUE_CAST grammar (offset/length and lane-sourced phrase) the Switcher seam does
    // not expose. gridProbe is null (grid=off), which the asserted cue= and phrase= tokens do not depend on.
    private StringWriter WireCueLogDirector()
    {
        var log = new StringWriter();
        var cueLog = new CueLog(() => log, () => null, ownsWriter: false);
        switcher = new Switcher(controller, controller.effects, controller.transitions);
        switcher.SetInitialEffect(0, controller.currentTransition);
        controller.switcher = switcher;
        director = new Director(
            controller,
            switcher,
            controller.timer,
            new[] { 0, 1, 2, 3 },
            new[] { 0, 1, 2 },
            controller.currentTransition,
            cueLog);
        controller.director = director;
        return log;
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
        int? energyBeatsUntilChange = null,
        string phraseLabel = "Phrase",
        string nextPhraseLabel = "Next")
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
            label = phraseLabel,
            countBeats = phraseStartBeat + phraseLengthBeats - beat,
            lengthBeats = phraseLengthBeats,
            irregular = 0,
        };
        snapshot.nextPhraseState = nextPhraseStartBeat is { } nextStart && nextPhraseLengthBeats is { } nextLength
            ? new LabeledCountdown { label = nextPhraseLabel, countBeats = nextStart - beat, lengthBeats = nextLength }
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
