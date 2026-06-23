using NUnit.Framework;

public sealed class SyncedCueIntentTests
{
    [Test]
    public void EvaluateWaitsBeforeRunway()
    {
        var intent = Evaluate(Frame(currentBeat: 604, cueMarkBeat: 609));

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Wait));
        Assert.That(intent.BeatPlan.StartBeat, Is.EqualTo(605));
        Assert.That(intent.BeatPlan.ImpactBeat, Is.EqualTo(609));
    }

    [Test]
    public void EvaluateCuesInsideRunway()
    {
        var intent = Evaluate(Frame(currentBeat: 605, cueMarkBeat: 609));

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.ShouldCue, Is.True);
        Assert.That(intent.BeatsUntilImpact, Is.EqualTo(4));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
    }

    [Test]
    public void DropAlignedCueCastsPreferredPerformerFromDeck()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.DropAligned, Is.True);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.HandlesDrop));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(deck, Is.EqualTo(new[] { 1, 0, 2 }));
    }

    [Test]
    public void DropAlignedCueKeepsStagedPerformerWhenItAlreadyMatchesPreferredRepertoire()
    {
        var deck = new[] { 2, 0, 1 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 1 }));
    }

    [Test]
    public void DropAlignedCueKeepsStagedPerformerWhenNoPreferredPerformerIsAvailable()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            _ => Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.HandlesDrop));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCuePreservesStagedPerformerForHeldOrManualSelection()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: true,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void MisalignedDropCueDoesNotCastPreferredPerformer()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 3),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.DropAligned, Is.False);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.None));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void InProgressDropCueDoesNotCastPreferredPerformer()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            InProgressDrop(),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.DropAligned, Is.False);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.None));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCueBlockedByCadenceDoesNotRotatePreferredDeckCard()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(
                currentBeat: 605,
                cueMarkBeat: 609,
                previousCueMarkBeat: 600),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.BlockedByCadence));
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.None));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCuePreservesMatchingHeldOrManualStagedPerformer()
    {
        var deck = new[] { 2, 0, 1 };

        var intent = SyncedCueIntent.Evaluate(
            Frame(currentBeat: 605, cueMarkBeat: 609),
            FourBeatRunway(),
            UpcomingDrop(beatsUntilStart: 4),
            stagedEffectIndex: 1,
            preserveStagedEffect: true,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None,
            minimumChangeCadenceBeats: 16);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 1 }));
    }

    [Test]
    public void EvaluateWaitsAtImpactBeat()
    {
        var intent = Evaluate(Frame(currentBeat: 609, cueMarkBeat: 609));

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Wait));
        Assert.That(intent.ShouldCue, Is.False);
    }

    [Test]
    public void EvaluateCuesZeroRunwayAtImpactBeat()
    {
        var intent = Evaluate(
            Frame(currentBeat: 609, cueMarkBeat: 609),
            transitionRepertoire: HardCut());

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.ShouldCue, Is.True);
        Assert.That(intent.BeatsUntilImpact, Is.EqualTo(0));
    }

    [Test]
    public void EvaluateCuesZeroRunwayTailAfterImpactBeat()
    {
        var intent = Evaluate(
            Frame(currentBeat: 610, cueMarkBeat: 609),
            transitionRepertoire: ZeroRunwayTail());

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Cue));
        Assert.That(intent.ShouldCue, Is.True);
        Assert.That(intent.BeatsUntilImpact, Is.EqualTo(-1));
    }

    [Test]
    public void EvaluateBlocksCadenceInsideRunway()
    {
        var intent = Evaluate(Frame(
            currentBeat: 605,
            cueMarkBeat: 609,
            previousCueMarkBeat: 600));

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.BlockedByCadence));
        Assert.That(intent.BlockedByCadence, Is.True);
        Assert.That(intent.ShouldCue, Is.False);
    }

    [Test]
    public void EvaluateWaitsWhenCurrentBeatAlreadyIssuedCue()
    {
        var intent = Evaluate(Frame(
            currentBeat: 605,
            cueMarkBeat: 609,
            lastCueBeat: 605));

        Assert.That(intent.Kind, Is.EqualTo(SyncedCueIntentKind.Wait));
        Assert.That(intent.ShouldCue, Is.False);
    }

    private static SyncedCueIntent Evaluate(
        TimingFrame frame,
        TransitionRepertoire? transitionRepertoire = null)
    {
        var deck = new[] { 1, 2, 0 };
        return SyncedCueIntent.Evaluate(
            frame,
            transitionRepertoire ?? FourBeatRunway(),
            drop: null,
            stagedEffectIndex: 1,
            preserveStagedEffect: true,
            currentEffectIndex: 0,
            deck,
            _ => Repertoire.None,
            minimumChangeCadenceBeats: 16);
    }

    private static TimingFrame Frame(
        int currentBeat,
        int cueMarkBeat,
        int? lastCueBeat = null,
        int? previousCueMarkBeat = null)
    {
        return new TimingFrame(
            new OnAirTimingInput(
                currentBeat,
                totalBeats: -1,
                beatInBar: ((currentBeat - 1) % 4) + 1,
                trackPhaseActive: 1,
                beatsUntilPhraseBoundary: cueMarkBeat - currentBeat,
                phraseLengthBeats: 32),
            PhaseReading.Unavailable,
            hasPhaseAnchor: true,
            PhaseConfidence.Structural,
            cueMarkBeat,
            hasPhraseWindow: false,
            default,
            TimingFrameSource.TrackPhaseBoundary,
            beatRewoundToNewPass: false,
            new PassLocalTimingState(lastCueBeat, previousCueMarkBeat),
            clearedPassLocalCueState: false,
            clearedPassLocalCadenceState: false,
            reanchored: false);
    }

    private static PhraseEventInfo UpcomingDrop(int beatsUntilStart)
    {
        return new PhraseEventInfo(
            inProgress: false,
            beatsUntilStart,
            msUntilStart: null,
            beatsUntilEnd: null,
            progress: null,
            anticipation: null,
            lengthBeats: 16,
            remaining: 1);
    }

    private static PhraseEventInfo InProgressDrop()
    {
        return new PhraseEventInfo(
            inProgress: true,
            beatsUntilStart: null,
            msUntilStart: null,
            beatsUntilEnd: 4,
            progress: 0.5f,
            anticipation: null,
            lengthBeats: 16,
            remaining: 1);
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

    private static TransitionRepertoire HardCut()
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 0,
            tailBeats: 0,
            TransitionShape.Blend,
            TransitionIntensity.High,
            defaultDurationSeconds: 0f);
    }

    private static TransitionRepertoire ZeroRunwayTail()
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 0,
            tailBeats: 12,
            TransitionShape.Blend,
            TransitionIntensity.High,
            defaultDurationSeconds: 12f);
    }
}
