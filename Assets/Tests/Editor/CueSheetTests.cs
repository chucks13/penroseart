using NUnit.Framework;

public sealed class CueSheetTests
{
    [Test]
    public void BuildSelectsRandomInteriorCueMarksAndMandatoryFinalCueMark()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);
        var randomCalls = 0;

        var sheet = CueSheet.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, maxExclusive) =>
            {
                randomCalls++;
                return randomCalls == 1 ? maxExclusive - 1 : minInclusive;
            });

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 16, 32 }));
        Assert.That(sheet.Matches(window), Is.True);
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[0]), Is.EqualTo(593));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[1]), Is.EqualTo(609));
    }

    [Test]
    public void BuildKeepsMandatoryFinalCueMarkWhenNoInteriorCueMarkIsSelected()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var sheet = CueSheet.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 32 }));
        Assert.That(sheet.Matches(window), Is.True);
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[0]), Is.EqualTo(609));
    }

    [Test]
    public void BuildBeforeThePhraseStartsIncludesTheBoundaryAsACueMark()
    {
        Assert.That(PhraseWindow.TryFromUpcomingTrackPhase(
            beat: 613,
            beatsToPhraseStart: 12,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var sheet = CueSheet.Build(
            window,
            currentBeat: 613,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 0, 32 }));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[0]), Is.EqualTo(625));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[1]), Is.EqualTo(657));
    }

    [Test]
    public void BuildSkipsFuturePhraseStartWhenCadenceBlocksIt()
    {
        Assert.That(PhraseWindow.TryFromUpcomingTrackPhase(
            beat: 613,
            beatsToPhraseStart: 12,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var sheet = CueSheet.Build(
            window,
            currentBeat: 613,
            canChangeAtBeat: cueMarkBeat => cueMarkBeat != window.StartBeat,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 32 }));
    }

    [Test]
    public void BuildFillsGeneratedCueSheetGapsLongerThanSixtyFourBeats()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 181,
            phraseLengthBeats: 192,
            out var window), Is.True);

        var sheet = CueSheet.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 64, 128, 192 }));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[0]), Is.EqualTo(641));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[1]), Is.EqualTo(705));
        Assert.That(sheet.ToAbsoluteBeat(window.StartBeat, sheet.CueMarkOffsets[2]), Is.EqualTo(769));
    }

    [Test]
    public void BuildRespectsCadenceWhenFillingGeneratedCueSheetGaps()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 181,
            phraseLengthBeats: 192,
            out var window), Is.True);

        var blockedCueMarkBeat = window.StartBeat + 64;
        var sheet = CueSheet.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: cueMarkBeat => cueMarkBeat != blockedCueMarkBeat,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.CueMarkOffsets, Is.EqualTo(new[] { 128, 192 }));
    }

    [Test]
    public void MatchesSamePhraseLengthInsteadOfExactAbsoluteTiming()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 20,
            phraseLengthBeats: 32,
            out var sameTiming), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var shiftedTiming), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 20,
            phraseLengthBeats: 48,
            out var differentLength), Is.True);

        var sheet = CueSheet.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(sheet.Matches(sameTiming), Is.True);
        Assert.That(sheet.Matches(shiftedTiming), Is.True);
        Assert.That(sheet.Matches(differentLength), Is.False);
    }
}
