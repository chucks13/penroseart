using NUnit.Framework;

public sealed class TransitionBeatPlanTests
{
    [Test]
    public void FromImpactBeatAppliesRunwayAndTail()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Dissolve,
            TransitionIntensity.High);

        var plan = TransitionBeatPlan.FromImpactBeat(609, repertoire);

        Assert.That(plan.StartBeat, Is.EqualTo(605));
        Assert.That(plan.ImpactBeat, Is.EqualTo(609));
        Assert.That(plan.CompleteBeat, Is.EqualTo(613));
    }
}
