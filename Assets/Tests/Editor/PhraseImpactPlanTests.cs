using NUnit.Framework;

public sealed class PhraseImpactPlanTests
{
    [Test]
    public void BuildSelectsRandomInteriorSlotsAndMandatoryBoundary()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);
        var randomCalls = 0;

        var plan = PhraseImpactPlan.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, maxExclusive) =>
            {
                randomCalls++;
                return randomCalls == 1 ? maxExclusive - 1 : minInclusive;
            });

        Assert.That(plan.ImpactBeats, Is.EqualTo(new[] { 593, 609 }));
    }

    [Test]
    public void BuildKeepsMandatoryBoundaryWhenNoInteriorSlotIsSelected()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);

        var plan = PhraseImpactPlan.Build(
            window,
            currentBeat: 588,
            canChangeAtBeat: _ => true,
            randomRange: (minInclusive, _) => minInclusive);

        Assert.That(plan.ImpactBeats, Is.EqualTo(new[] { 609 }));
    }
}
