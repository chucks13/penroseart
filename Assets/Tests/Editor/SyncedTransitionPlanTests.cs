using NUnit.Framework;

public sealed class SyncedTransitionPlanTests
{
    [Test]
    public void ProgressClampsBetweenStartAndDuration()
    {
        var plan = BuildPlan(startTime: 10f, secondsPerBeat: 0.5f);

        Assert.That(plan.Progress(9f), Is.EqualTo(0f));
        Assert.That(plan.Progress(12f), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(plan.Progress(15f), Is.EqualTo(1f));
    }

    [Test]
    public void EvaluateRecordsCurrentBeatAsImpactOnRewindWithoutCompletingEarly()
    {
        var plan = BuildPlan(startTime: 10f, secondsPerBeat: 0.5f);

        var update = plan.EvaluateUpdate(
            currentBeat: 588,
            beatRewoundToNewPass: true,
            recordedImpactBeat: plan.ImpactBeat,
            now: 11f);

        Assert.That(update.Progress, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(update.RecordImpactOnRewind, Is.True);
        Assert.That(update.ImpactBeat, Is.EqualTo(588));
        Assert.That(update.ShouldComplete, Is.False);
    }

    [Test]
    public void EvaluateCompletesOnlyAfterDuration()
    {
        var plan = BuildPlan(startTime: 10f, secondsPerBeat: 0.5f);

        var beforeComplete = plan.EvaluateUpdate(
            currentBeat: 612,
            beatRewoundToNewPass: false,
            recordedImpactBeat: plan.ImpactBeat,
            now: 13.99f);
        var complete = plan.EvaluateUpdate(
            currentBeat: 613,
            beatRewoundToNewPass: false,
            recordedImpactBeat: plan.ImpactBeat,
            now: 14f);

        Assert.That(beforeComplete.ShouldComplete, Is.False);
        Assert.That(complete.Progress, Is.EqualTo(1f));
        Assert.That(complete.ShouldComplete, Is.True);
        Assert.That(complete.ImpactBeat, Is.EqualTo(plan.ImpactBeat));
    }

    private static SyncedTransitionPlan BuildPlan(float startTime, float secondsPerBeat)
    {
        var repertoire = TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Dissolve,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);
        var beatPlan = TransitionBeatPlan.FromImpactBeat(609, repertoire);
        return new SyncedTransitionPlan(
            transitionIndex: 3,
            targetEffectIndex: 7,
            beatPlan,
            repertoire,
            startTime,
            secondsPerBeat);
    }
}
