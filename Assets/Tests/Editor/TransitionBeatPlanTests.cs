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
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);

        var plan = TransitionBeatPlan.FromImpactBeat(609, repertoire);

        Assert.That(plan.StartBeat, Is.EqualTo(605));
        Assert.That(plan.ImpactBeat, Is.EqualTo(609));
        Assert.That(plan.CompleteBeat, Is.EqualTo(613));
    }

    [Test]
    public void CueWindowIncludesRunwayAndExcludesImpact()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 0,
            TransitionShape.Blend,
            TransitionIntensity.Subtle,
            defaultDurationSeconds: 4f);

        var plan = TransitionBeatPlan.FromImpactBeat(609, repertoire);

        Assert.That(plan.IsCueBeat(604), Is.False);
        Assert.That(plan.IsCueBeat(605), Is.True);
        Assert.That(plan.IsCueBeat(608), Is.True);
        Assert.That(plan.IsCueBeat(609), Is.False);
    }
}
