using NUnit.Framework;

public sealed class TransitionBeatPlanTests
{
    [TestCase(12, 0)]
    [TestCase(0, 12)]
    [TestCase(6, 6)]
    [TestCase(1, 11)]
    [TestCase(11, 1)]
    [TestCase(0, 0)]
    public void FromCueMarkAppliesRunwayAndTail(int runwayBeats, int tailBeats)
    {
        var plan = TransitionBeatPlan.FromCueMark(609, TransitionRepertoireFor(runwayBeats, tailBeats));

        Assert.That(plan.StartBeat, Is.EqualTo(609 - runwayBeats));
        Assert.That(plan.ImpactBeat, Is.EqualTo(609));
        Assert.That(plan.CompleteBeat, Is.EqualTo(609 + tailBeats));
    }

    [TestCase(12, 0)]
    [TestCase(0, 12)]
    [TestCase(6, 6)]
    [TestCase(1, 11)]
    [TestCase(11, 1)]
    [TestCase(0, 0)]
    public void CueWindowSpansStartThroughComplete(int runwayBeats, int tailBeats)
    {
        var plan = TransitionBeatPlan.FromCueMark(609, TransitionRepertoireFor(runwayBeats, tailBeats));
        var firstCueBeat = 609 - runwayBeats;
        var lastCueBeat = 609 + tailBeats;

        Assert.That(plan.IsCueBeat(firstCueBeat - 1), Is.False);
        Assert.That(plan.IsCueBeat(firstCueBeat), Is.True);
        Assert.That(plan.IsCueBeat(609), Is.True, "The Impact Point is always inside the cue window.");
        Assert.That(plan.IsCueBeat(lastCueBeat), Is.True);
        Assert.That(plan.IsCueBeat(lastCueBeat + 1), Is.False);
    }

    private static TransitionRepertoire TransitionRepertoireFor(int runwayBeats, int tailBeats)
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats,
            tailBeats,
            TransitionShape.Blend,
            TransitionIntensity.Medium,
            defaultDurationSeconds: 4f);
    }
}
