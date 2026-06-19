using NUnit.Framework;

public sealed class SyncedCueDecisionTests
{
    [Test]
    public void EvaluateWaitsBeforeRunway()
    {
        var decision = SyncedCueDecision.Evaluate(
            currentBeat: 604,
            selectedPhaseBoundary: 609,
            transitionRepertoire: FourBeatRunway(),
            lastCueBeat: null,
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(decision.Kind, Is.EqualTo(SyncedCueDecisionKind.Wait));
        Assert.That(decision.BeatPlan.StartBeat, Is.EqualTo(605));
        Assert.That(decision.BeatPlan.ImpactBeat, Is.EqualTo(609));
    }

    [Test]
    public void EvaluateCuesInsideRunway()
    {
        var decision = SyncedCueDecision.Evaluate(
            currentBeat: 605,
            selectedPhaseBoundary: 609,
            transitionRepertoire: FourBeatRunway(),
            lastCueBeat: null,
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(decision.Kind, Is.EqualTo(SyncedCueDecisionKind.Cue));
        Assert.That(decision.ShouldCue, Is.True);
        Assert.That(decision.BeatsUntilImpact, Is.EqualTo(4));
    }

    [Test]
    public void EvaluateWaitsAtImpactBeat()
    {
        var decision = SyncedCueDecision.Evaluate(
            currentBeat: 609,
            selectedPhaseBoundary: 609,
            transitionRepertoire: FourBeatRunway(),
            lastCueBeat: null,
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(decision.Kind, Is.EqualTo(SyncedCueDecisionKind.Wait));
        Assert.That(decision.ShouldCue, Is.False);
    }

    [Test]
    public void EvaluateBlocksCadenceInsideRunway()
    {
        var decision = SyncedCueDecision.Evaluate(
            currentBeat: 605,
            selectedPhaseBoundary: 609,
            transitionRepertoire: FourBeatRunway(),
            lastCueBeat: null,
            previousSelectedPhaseBoundary: 600,
            minimumChangeCadenceBeats: 16);

        Assert.That(decision.Kind, Is.EqualTo(SyncedCueDecisionKind.BlockedByCadence));
        Assert.That(decision.BlockedByCadence, Is.True);
        Assert.That(decision.ShouldCue, Is.False);
    }

    [Test]
    public void EvaluateWaitsWhenCurrentBeatAlreadyIssuedCue()
    {
        var decision = SyncedCueDecision.Evaluate(
            currentBeat: 605,
            selectedPhaseBoundary: 609,
            transitionRepertoire: FourBeatRunway(),
            lastCueBeat: 605,
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(decision.Kind, Is.EqualTo(SyncedCueDecisionKind.Wait));
        Assert.That(decision.ShouldCue, Is.False);
    }

    private static TransitionRepertoire FourBeatRunway()
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Dissolve,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);
    }
}
