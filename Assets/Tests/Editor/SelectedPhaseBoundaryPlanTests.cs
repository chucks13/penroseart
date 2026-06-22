using NUnit.Framework;

public sealed class SelectedPhaseBoundaryPlanTests
{
    [Test]
    public void BuildSelectsRandomInteriorPhaseBoundariesAndMandatoryPhraseBoundary()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);
        var randomCalls = 0;

        var plan = SelectedPhaseBoundaryPlan.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, maxExclusive) =>
            {
                randomCalls++;
                return randomCalls == 1 ? maxExclusive - 1 : minInclusive;
            });

        Assert.That(plan.SelectedPhaseBoundaries, Is.EqualTo(new[] { 593, 609 }));
        Assert.That(plan.Matches(window), Is.True);
    }

    [Test]
    public void BuildKeepsMandatoryPhraseBoundaryWhenNoInteriorPhaseBoundaryIsSelected()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var plan = SelectedPhaseBoundaryPlan.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(plan.SelectedPhaseBoundaries, Is.EqualTo(new[] { 609 }));
        Assert.That(plan.Matches(window), Is.True);
    }

    [Test]
    public void BuildCanIncludeFuturePhraseStartForUpcomingPlan()
    {
        Assert.That(PhraseWindow.TryFromUpcomingTrackPhase(
            beat: 613,
            beatsToPhraseStart: 12,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var plan = SelectedPhaseBoundaryPlan.Build(
            window,
            currentBeat: 613,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive,
            includePhraseStart: true);

        Assert.That(plan.SelectedPhaseBoundaries, Is.EqualTo(new[] { 625, 657 }));
    }

    [Test]
    public void MatchesOnlyExactPhraseWindowTimingIdentity()
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

        var plan = SelectedPhaseBoundaryPlan.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(plan.Matches(sameTiming), Is.True);
        Assert.That(plan.Matches(shiftedTiming), Is.False);
        Assert.That(plan.Matches(differentLength), Is.False);
    }
}
