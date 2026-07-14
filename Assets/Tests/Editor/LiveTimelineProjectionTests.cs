// Specifies live sequencing timeline behavior through its pure projection seam.
#nullable enable

using NUnit.Framework;

/// <summary>Specifies the read-only live sequencing timeline through its pure projection seam.</summary>
public sealed class LiveTimelineProjectionTests
{
    /// <summary>A Phrase shorter than one Grid remains one partial block and keeps its final Cue Mark.</summary>
    [Test]
    public void ShortIrregularPhraseRendersOnePartialBlock()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 8, "Break", new[] { 8 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 103,
            currentPhraseBeat: 3,
            currentGridBeat: 3,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Current.IsAvailable, Is.True);
        Assert.That(model.Current.Blocks, Has.Count.EqualTo(1));
        Assert.That(model.Current.Blocks[0].Cells, Has.Count.EqualTo(8));
        Assert.That(model.Current.Blocks[0].Cells[7].PhraseBeat, Is.EqualTo(8));
        Assert.That(model.Current.Blocks[0].Cells[7].IsCueMark, Is.True);
    }

    /// <summary>A regular Phrase keeps exact 1-based Cue Mark positions across consecutive full Grids.</summary>
    [Test]
    public void RegularPhrasePlacesCueMarksAtBeatsSixteenAndFortyEight()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 48, "Chorus", new[] { 16, 48 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 301,
            currentPhraseBeat: 1,
            currentGridBeat: 1,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Current.Blocks, Has.Count.EqualTo(3));
        Assert.That(model.Current.Blocks[0].Cells[15].PhraseBeat, Is.EqualTo(16));
        Assert.That(model.Current.Blocks[0].Cells[15].IsCueMark, Is.True);
        Assert.That(model.Current.Blocks[2].Cells[15].PhraseBeat, Is.EqualTo(48));
        Assert.That(model.Current.Blocks[2].Cells[15].IsCueMark, Is.True);
    }

    /// <summary>Current position wins the fill while loaded Cue and lock identity remain independent marks.</summary>
    [Test]
    public void LoadedCueProjectsRunwayImpactTailAndCurrentBeatPrecedence()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 24, "Chorus", new[] { 8, 24 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 106,
            currentPhraseBeat: 6,
            currentGridBeat: 6,
            nextPhraseStartsInBeats: null,
            loadedCue: new SwitcherCueStatus(
                true,
                true,
                108,
                2,
                1,
                103,
                104,
                110,
                4,
                2),
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);
        var current = model.Current.Blocks[0].Cells[5];
        var impact = model.Current.Blocks[0].Cells[7];
        var tail = model.Current.Blocks[0].Cells[8];

        Assert.That(current.IsRunway, Is.True);
        Assert.That(current.IsCurrentBeat, Is.True);
        Assert.That(current.Fill, Is.EqualTo(LiveTimelineFill.CurrentBeat));
        Assert.That(impact.IsLoadedCue, Is.True);
        Assert.That(impact.IsImpactPoint, Is.True);
        Assert.That(impact.IsLocked, Is.True);
        Assert.That(tail.IsTail, Is.True);
    }

    /// <summary>Active progress remains visible without reverse-engineering an execution schedule from it.</summary>
    [Test]
    public void ActiveExecutionShowsProgressWithoutSynthesizingCellTiming()
    {
        var progress = 2.5f / 6f;
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 24, "Chorus", new[] { 8, 24 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 106,
            currentPhraseBeat: 6,
            currentGridBeat: 6,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: progress);

        var model = LiveTimelineProjection.Build(input);
        var current = model.Current.Blocks[0].Cells[5];
        var impact = model.Current.Blocks[0].Cells[7];
        var tail = model.Current.Blocks[0].Cells[8];

        Assert.That(model.ExecutionProgress, Is.EqualTo(progress));
        Assert.That(current.IsExecuting, Is.True);
        Assert.That(current.Fill, Is.EqualTo(LiveTimelineFill.CurrentBeat));
        Assert.That(current.IsRunway, Is.False);
        Assert.That(impact.IsImpactPoint, Is.False);
        Assert.That(impact.IsLoadedCue, Is.False);
        Assert.That(tail.IsTail, Is.False);
    }

    /// <summary>Long irregular Phrases keep every full Grid and their exact partial tail.</summary>
    [Test]
    public void FortyOneBeatPhraseRendersTwoFullBlocksAndNineBeatTail()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 41, "Build", new[] { 16, 41 }),
            nextSheet: new CueSheetView(true, 8, "Drop", new[] { 8 }),
            currentAbsoluteBeat: 116,
            currentPhraseBeat: 16,
            currentGridBeat: null,
            nextPhraseStartsInBeats: 1,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Current.Blocks, Has.Count.EqualTo(3));
        Assert.That(model.Current.Blocks[0].Cells, Has.Count.EqualTo(16));
        Assert.That(model.Current.Blocks[1].Cells, Has.Count.EqualTo(16));
        Assert.That(model.Current.Blocks[2].Cells, Has.Count.EqualTo(9));
        Assert.That(model.Current.Blocks[0].Cells[15].IsCueMark, Is.True);
        Assert.That(model.Current.Blocks[2].Cells[8].PhraseBeat, Is.EqualTo(41));
        Assert.That(model.Current.Blocks[2].Cells[8].IsCueMark, Is.True);
        Assert.That(model.Current.Blocks[0].Cells[15].IsCurrentBeat, Is.False);
        Assert.That(model.CurrentPositionAvailable, Is.False);
        Assert.That(model.Next.Label, Is.EqualTo("Drop"));
        Assert.That(model.Next.Blocks[0].Cells, Has.Count.EqualTo(8));
    }

    /// <summary>Missing sheets and clock facts remain explicit unavailable Standalone state.</summary>
    [Test]
    public void StandaloneWithoutSheetsProjectsUnavailablePlans()
    {
        var input = new LiveTimelineInput(
            isSynced: false,
            currentSheet: CueSheetView.Empty,
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: null,
            currentPhraseBeat: null,
            currentGridBeat: null,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.IsSynced, Is.False);
        Assert.That(model.Current.IsAvailable, Is.False);
        Assert.That(model.Next.IsAvailable, Is.False);
        Assert.That(model.CurrentPositionAvailable, Is.False);
        Assert.That(model.ExecutionProgress, Is.Null);
    }

    /// <summary>Standalone Mode never projects stale current or Transition timing facts as live placement.</summary>
    [Test]
    public void StandaloneSuppressesStaleClockAndExecutionPlacement()
    {
        var input = new LiveTimelineInput(
            isSynced: false,
            currentSheet: new CueSheetView(true, 16, "Chorus", new[] { 8, 16 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 105,
            currentPhraseBeat: 5,
            currentGridBeat: 5,
            nextPhraseStartsInBeats: null,
            loadedCue: new SwitcherCueStatus(true, true, 108, 2, 1, 103, 104, 110, 4, 2),
            executionProgress: 1.5f / 6f);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Current.Blocks[0].Cells[4].IsCurrentBeat, Is.False);
        Assert.That(model.Current.Blocks[0].Cells[4].IsRunway, Is.False);
        Assert.That(model.Current.Blocks[0].Cells[7].IsLoadedCue, Is.False);
        Assert.That(model.ExecutionProgress, Is.Null);
    }

    /// <summary>Loaded timing crosses the Phrase boundary without clipping to either Grid row.</summary>
    [Test]
    public void LoadedCueTimingCrossesFromCurrentPhraseIntoNextPhrase()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 8, "Build", new[] { 8 }),
            nextSheet: new CueSheetView(true, 8, "Drop", new[] { 2, 8 }),
            currentAbsoluteBeat: 107,
            currentPhraseBeat: 7,
            currentGridBeat: 7,
            nextPhraseStartsInBeats: 2,
            loadedCue: new SwitcherCueStatus(true, false, 110, 2, 1, 105, 106, 112, 4, 2),
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Current.Blocks[0].Cells[5].IsRunway, Is.True);
        Assert.That(model.Current.Blocks[0].Cells[7].IsRunway, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[0].IsRunway, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[0].PhraseBeat, Is.EqualTo(1));
        Assert.That(model.Next.Blocks[0].Cells[1].IsImpactPoint, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[1].IsLoadedCue, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[1].IsCueMark, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[2].IsTail, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[3].IsTail, Is.True);
    }

    /// <summary>A zero/zero loaded Transition marks only its Impact Point and invents no duration.</summary>
    [Test]
    public void ZeroRunwayAndTailProjectsHardCutOnly()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 16, "Drop", new[] { 1, 16 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 200,
            currentPhraseBeat: 1,
            currentGridBeat: 1,
            nextPhraseStartsInBeats: null,
            loadedCue: new SwitcherCueStatus(true, true, 200, 2, 1, 199, 200, 200, 0, 0),
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);
        var impact = model.Current.Blocks[0].Cells[0];

        Assert.That(impact.PhraseBeat, Is.EqualTo(1));
        Assert.That(impact.IsImpactPoint, Is.True);
        Assert.That(impact.IsRunway, Is.False);
        Assert.That(impact.IsTail, Is.False);
        Assert.That(model.Current.Blocks[0].Cells[15].PhraseBeat, Is.EqualTo(16));
    }

    /// <summary>The next Phrase anchor comes from its wire countdown, not extrapolation from the current sheet.</summary>
    [Test]
    public void NextPhraseUsesWireCountdownForItsOwnAnchor()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 16, "Build", new[] { 16 }),
            nextSheet: new CueSheetView(true, 8, "Drop", new[] { 2, 8 }),
            currentAbsoluteBeat: 110,
            currentPhraseBeat: 10,
            currentGridBeat: 10,
            nextPhraseStartsInBeats: 3,
            loadedCue: new SwitcherCueStatus(true, false, 114, 2, 1, 109, 110, 116, 4, 2),
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.Next.Blocks[0].Cells[1].IsLoadedCue, Is.True);
        Assert.That(model.Next.Blocks[0].Cells[1].PhraseBeat, Is.EqualTo(2));
    }

    /// <summary>Missing loaded-Cue timing remains explicit and contributes no invented Runway or Tail cells.</summary>
    [Test]
    public void MissingLoadedCueTimingIsUnavailable()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 16, "Build", new[] { 8, 16 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 105,
            currentPhraseBeat: 5,
            currentGridBeat: 5,
            nextPhraseStartsInBeats: null,
            loadedCue: new SwitcherCueStatus(true, false, 108, 2, 1, -1, -1, -1, 4, 2),
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.LoadedCueTimingAvailable, Is.False);
        Assert.That(model.Current.Blocks[0].Cells[7].IsLoadedCue, Is.False);
        Assert.That(model.Current.Blocks[0].Cells[6].IsRunway, Is.False);
    }

    /// <summary>Phrase and Grid positions must agree before the projection identifies a live current cell.</summary>
    [Test]
    public void ConflictingPhraseAndGridPositionIsUnavailable()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 32, "Build", new[] { 16, 32 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 117,
            currentPhraseBeat: 17,
            currentGridBeat: 2,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.CurrentPositionAvailable, Is.False);
        Assert.That(model.Current.Blocks[1].Cells[0].IsCurrentBeat, Is.False);
    }

    /// <summary>One-based positions at beat 1, beat 16, and the re-anchored beat 17 all select exact cells.</summary>
    [TestCase(1, 1, 0, 0)]
    [TestCase(16, 16, 0, 15)]
    [TestCase(17, 1, 1, 0)]
    public void OneBasedGridBoundariesSelectExactCells(
        int phraseBeat,
        int gridBeat,
        int expectedBlock,
        int expectedCell)
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentSheet: new CueSheetView(true, 32, "Build", new[] { 16, 32 }),
            nextSheet: CueSheetView.Empty,
            currentAbsoluteBeat: 100 + phraseBeat,
            currentPhraseBeat: phraseBeat,
            currentGridBeat: gridBeat,
            nextPhraseStartsInBeats: null,
            loadedCue: SwitcherCueStatus.Empty,
            executionProgress: null);

        var model = LiveTimelineProjection.Build(input);

        Assert.That(model.CurrentPositionAvailable, Is.True);
        Assert.That(model.Current.Blocks[expectedBlock].Cells[expectedCell].IsCurrentBeat, Is.True);
    }
}
