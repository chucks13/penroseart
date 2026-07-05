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
    public void CommitWindowClosesAtTheLockPoint(int runwayBeats, int tailBeats)
    {
        var plan = TransitionBeatPlan.FromCueMark(609, TransitionRepertoireFor(runwayBeats, tailBeats));
        var lockPointBeat = 609 - runwayBeats - 1;

        Assert.That(plan.LockPointBeat, Is.EqualTo(lockPointBeat));
        Assert.That(plan.CanCommitAt(lockPointBeat - 1), Is.True);
        Assert.That(plan.CanCommitAt(lockPointBeat), Is.False, "The Lock Point itself is too late to commit.");
        Assert.That(plan.CanCommitAt(plan.StartBeat), Is.False);
        Assert.That(plan.CanCommitAt(609), Is.False, "The Impact Point is far too late to commit.");
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
