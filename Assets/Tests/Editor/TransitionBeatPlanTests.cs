using NUnit.Framework;

public sealed class TransitionBeatPlanTests
{
    [Test]
    public void FromSelectedPhaseBoundaryAppliesRunwayAndTail()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Dissolve,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);

        var plan = TransitionBeatPlan.FromSelectedPhaseBoundary(609, repertoire);

        Assert.That(plan.StartBeat, Is.EqualTo(605));
        Assert.That(plan.ImpactBeat, Is.EqualTo(609));
        Assert.That(plan.CompleteBeat, Is.EqualTo(613));
    }

    [Test]
    public void ZeroRunwayCuesOnImpactBeat()
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 0,
            tailBeats: 0,
            TransitionShape.Blend,
            TransitionIntensity.Subtle,
            defaultDurationSeconds: 0f);

        var plan = TransitionBeatPlan.FromSelectedPhaseBoundary(609, repertoire);

        Assert.That(plan.StartBeat, Is.EqualTo(609));
        Assert.That(plan.ImpactBeat, Is.EqualTo(609));
        Assert.That(plan.CompleteBeat, Is.EqualTo(609));
        Assert.That(plan.IsCueBeat(608), Is.False);
        Assert.That(plan.IsCueBeat(609), Is.True);
        Assert.That(plan.IsCueBeat(610), Is.False);
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

        var plan = TransitionBeatPlan.FromSelectedPhaseBoundary(609, repertoire);

        Assert.That(plan.IsCueBeat(604), Is.False);
        Assert.That(plan.IsCueBeat(605), Is.True);
        Assert.That(plan.IsCueBeat(608), Is.True);
        Assert.That(plan.IsCueBeat(609), Is.False);
    }
}
