using NUnit.Framework;

// Casting behavior of SyncedCueIntent (which Performer a cleared cue targets, and how live phrase
// events classify a cue for casting). The per-beat timing verdict — wait/cue/blocked-on-cadence —
// lives on CuePlanner.EvaluateCueTiming and is covered by CuePlannerTests.
public sealed class SyncedCueIntentTests
{
    [Test]
    public void OrdinaryCastKeepsTheStagedPerformer()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Ordinary,
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None);

        Assert.That(intent.EventIntent, Is.EqualTo(CueEventIntent.Ordinary));
        Assert.That(intent.DropAligned, Is.False);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.None));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCastFindsPreferredPerformerOnTheDeckWithoutRotatingIt()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Drop,
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None);

        Assert.That(intent.EventIntent, Is.EqualTo(CueEventIntent.Drop));
        Assert.That(intent.DropAligned, Is.True);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.HandlesDrop));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }), "Casting must not rotate deck candidates; the card is pulled only when the cue is sent.");
    }

    [Test]
    public void DropAlignedCastKeepsStagedPerformerWhenItAlreadyMatchesPreferredRepertoire()
    {
        var deck = new[] { 2, 0, 1 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Drop,
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 1 }));
    }

    [Test]
    public void DropAlignedCastKeepsStagedPerformerWhenNoPreferredPerformerIsAvailable()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Drop,
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            _ => Repertoire.None);

        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.HandlesDrop));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCastPreservesStagedPerformerForHeldOrManualSelection()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Drop,
            stagedEffectIndex: 1,
            preserveStagedEffect: true,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesDrop : Repertoire.None);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.False);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void DropAlignedCastPreservesMatchingHeldOrManualStagedPerformer()
    {
        var deck = new[] { 2, 0, 1 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Drop,
            stagedEffectIndex: 1,
            preserveStagedEffect: true,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex is 1 or 2 ? Repertoire.HandlesDrop : Repertoire.None);

        Assert.That(intent.TargetEffectIndex, Is.EqualTo(1));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(-1));
        Assert.That(deck, Is.EqualTo(new[] { 2, 0, 1 }));
    }

    [Test]
    public void FillAlignedCastFindsPreferredPerformerOnTheDeckWithoutRotatingIt()
    {
        var deck = new[] { 1, 2, 0 };

        var intent = SyncedCueIntent.Cast(
            CueEventIntent.Fill,
            stagedEffectIndex: 1,
            preserveStagedEffect: false,
            currentEffectIndex: 0,
            deck,
            effectIndex => effectIndex == 2 ? Repertoire.HandlesFill : Repertoire.None);

        Assert.That(intent.EventIntent, Is.EqualTo(CueEventIntent.Fill));
        Assert.That(intent.FillAligned, Is.True);
        Assert.That(intent.DropAligned, Is.False);
        Assert.That(intent.PreferredRepertoire, Is.EqualTo(Repertoire.HandlesFill));
        Assert.That(intent.TargetEffectIndex, Is.EqualTo(2));
        Assert.That(intent.CastPreferredPerformer, Is.True);
        Assert.That(intent.EffectDeckIndex, Is.EqualTo(1));
        Assert.That(deck, Is.EqualTo(new[] { 1, 2, 0 }), "Casting must not rotate deck candidates; the card is pulled only when the cue is sent.");
    }

    [Test]
    public void ResolveEventIntentPrefersDropWhenFillAndDropBothAlign()
    {
        var frame = Frame(currentBeat: 605, cueMarkBeat: 609);

        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            UpcomingFill(beatsUntilStart: 4),
            UpcomingDrop(beatsUntilStart: 4));

        Assert.That(eventIntent, Is.EqualTo(CueEventIntent.Drop));
    }

    [Test]
    public void ResolveEventIntentUsesFillThatOverlapsNextGrid()
    {
        var frame = Frame(currentBeat: 605, cueMarkBeat: 609);

        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            UpcomingFill(beatsUntilStart: 3),
            drop: null);

        Assert.That(eventIntent, Is.EqualTo(CueEventIntent.Fill));
    }

    [Test]
    public void ResolveEventIntentIgnoresFillThatEndsBeforeNextGrid()
    {
        var frame = Frame(currentBeat: 605, cueMarkBeat: 609);

        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            UpcomingFill(beatsUntilStart: 1, lengthBeats: 2),
            drop: null);

        Assert.That(eventIntent, Is.EqualTo(CueEventIntent.Ordinary));
    }

    [Test]
    public void ResolveEventIntentIgnoresFillThatStartsAfterNextGrid()
    {
        var frame = Frame(currentBeat: 605, cueMarkBeat: 609);

        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            UpcomingFill(beatsUntilStart: 20),
            drop: null);

        Assert.That(eventIntent, Is.EqualTo(CueEventIntent.Ordinary));
    }

    [Test]
    public void ResolveEventIntentUsesInProgressFillButIgnoresInProgressDrop()
    {
        var frame = Frame(currentBeat: 605, cueMarkBeat: 609);

        var eventIntent = SyncedCueIntent.ResolveEventIntent(
            frame,
            InProgressFill(),
            InProgressDrop());

        Assert.That(eventIntent, Is.EqualTo(CueEventIntent.Fill));
    }

    private static TimingFrame Frame(int currentBeat, int cueMarkBeat)
    {
        return new TimingFrame(
            new OnAirTimingInput(
                currentBeat,
                beatInBar: ((currentBeat - 1) % 4) + 1,
                trackPhaseActive: 1,
                beatsUntilPhraseBoundary: cueMarkBeat - currentBeat,
                phraseLengthBeats: 32),
            new GridReading(0, 1, GridSyncState.Locked, false),
            hasGridAnchor: true,
            cueMarkBeat,
            hasPhraseWindow: false,
            default,
            TimingFrameSource.TrackPhaseBoundary,
            beatRewoundToNewPass: false,
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

    private static PhraseEventInfo UpcomingFill(int beatsUntilStart, int lengthBeats = 8)
    {
        return new PhraseEventInfo(
            inProgress: false,
            beatsUntilStart,
            msUntilStart: null,
            beatsUntilEnd: null,
            progress: null,
            anticipation: null,
            lengthBeats,
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

    private static PhraseEventInfo InProgressFill()
    {
        return new PhraseEventInfo(
            inProgress: true,
            beatsUntilStart: null,
            msUntilStart: null,
            beatsUntilEnd: 4,
            progress: 0.5f,
            anticipation: null,
            lengthBeats: 8,
            remaining: 1);
    }
}
