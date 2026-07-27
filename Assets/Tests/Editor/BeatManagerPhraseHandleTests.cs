// Contract tests for the seven typed Phrase handles fed by the Focus player's Song Structure.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Tests the structure-fed Phrase handles through the Data Surface seam: a hand-built wire snapshot
/// carrying a per-player Song Structure, its structure cursor, and the live order goes in, one frame
/// is captured, and the handles' spans are read. Envelope internals are never touched directly.
/// </summary>
/// <remarks>
/// Expected values come from the linear contract — Build is the normalized position and Decay is one
/// minus that position — worked out by hand rather than recomputed the way the runtime does.
/// </remarks>
public sealed class BeatManagerPhraseHandleTests
{
    /// <summary>Verifies In reads position through the phrase the cursor sits inside.</summary>
    [Test]
    public void InReadsPositionThroughTheCoveringPhrase()
    {
        // Beat 17 of the 32-beat chorus at ordinal 3: half way through it.
        var beatManager = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Chorus.In.Decay(), Is.EqualTo(0.5f));
    }

    /// <summary>Verifies In with no argument spans the covering phrase's own length.</summary>
    [Test]
    public void InDefaultsToTheCoveringPhrasesOwnLength()
    {
        var beatManager = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);

        // Sixteen beats elapsed: a sixteen-beat window has completed, the phrase's own 32 is half way,
        // and the linear position across a 64-beat window is 16 / 64 = 0.25.
        Assert.That(beatManager.Chorus.In.Build(16), Is.EqualTo(1f));
        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Chorus.In.Build(64), Is.EqualTo(0.25f));
    }

    /// <summary>
    /// Verifies Before targets the next ordinal occurrence of the type rather than the one being
    /// played, so both spans of one handle are live in the same frame.
    /// </summary>
    [Test]
    public void BeforeTargetsTheFollowingOccurrenceWhileInIsLive()
    {
        // Beat 81 of the track, inside the chorus at ordinal 3; the chorus at ordinal 4 starts at 97.
        var beatManager = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);

        // Sixteen beats out on a 32-beat runway is half way along it.
        Assert.That(beatManager.Chorus.Before.Build(32), Is.EqualTo(0.5f));
        Assert.That(beatManager.Chorus.Before.Decay(32), Is.EqualTo(0.5f));
        // A 16-beat runway is only now opening.
        Assert.That(beatManager.Chorus.Before.Build(16), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
    }

    /// <summary>Verifies Before reads a type the cursor is not inside, and rests beyond its window.</summary>
    [Test]
    public void BeforeReadsATypeTheCursorIsNotInside()
    {
        // From beat 81: the down section starts at 129 (48 beats out), the outro at 161 (80 out).
        var beatManager = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);

        Assert.That(beatManager.Down.Before.Build(96), Is.EqualTo(0.5f));
        Assert.That(beatManager.Outro.Before.Build(160), Is.EqualTo(0.5f));
        // Eighty beats out is beyond a 64-beat runway, so the outro still reads as infinitely far.
        Assert.That(beatManager.Outro.Before.Build(64), Is.EqualTo(0f));
        Assert.That(beatManager.Outro.Before.Decay(64), Is.EqualTo(1f));
        Assert.That(beatManager.Down.In.Build(), Is.EqualTo(0f));
    }

    /// <summary>Verifies In and Before use the same continuous intra-beat position.</summary>
    [Test]
    public void InAndBeforeMoveContinuouslyWithinTheBeat()
    {
        var onTheBeat = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);
        var halfwayThroughTheBeat = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17, timeSeconds: 0.25f);
        var nextWholeBeat = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 18, timeSeconds: 0.25f);

        // Half a beat further on, both linear spans read 16.5 / 32 = 0.515625.
        Assert.That(halfwayThroughTheBeat.Chorus.In.Build(), Is.EqualTo(16.5f / 32f));
        Assert.That(halfwayThroughTheBeat.Chorus.Before.Build(32), Is.EqualTo(16.5f / 32f));
        Assert.That(halfwayThroughTheBeat.Chorus.In.Build(), Is.GreaterThan(onTheBeat.Chorus.In.Build()));
        Assert.That(halfwayThroughTheBeat.Chorus.Before.Build(32),
            Is.GreaterThan(onTheBeat.Chorus.Before.Build(32)));
        Assert.That(nextWholeBeat.Chorus.Before.Build(32), Is.EqualTo(17.5f / 32f));
    }

    /// <summary>
    /// Verifies adjacent identical types stay distinct phrases: entering the second chorus starts its
    /// In span and leaves no chorus for Before to approach.
    /// </summary>
    [Test]
    public void AdjacentIdenticalPhrasesAreDistinctOccurrences()
    {
        var beatManager = FocusDeck(Track(), currentPhrase: 4, beatInPhrase: 1);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.In.Decay(), Is.EqualTo(1f));
        Assert.That(beatManager.Chorus.Before.Build(32), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.Before.Decay(32), Is.EqualTo(1f));
    }

    /// <summary>Verifies handles with no current or upcoming occurrence rest at nothing-happening values.</summary>
    [Test]
    public void HandlesWithNoCurrentOrUpcomingOccurrenceRestAtNothingHappeningValues()
    {
        // From beat 81 the intro is behind and the track carries no verse at all.
        var beatManager = FocusDeck(Track(), currentPhrase: 3, beatInPhrase: 17);

        Assert.That(beatManager.Intro.Before.Build(32), Is.EqualTo(0f));
        Assert.That(beatManager.Intro.Before.Decay(32), Is.EqualTo(1f));
        Assert.That(beatManager.Intro.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Verse.Before.Build(32), Is.EqualTo(0f));
        Assert.That(beatManager.Verse.Before.Decay(32), Is.EqualTo(1f));
        Assert.That(beatManager.Verse.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Bridge.Before.Decay(32), Is.EqualTo(1f));
    }

    /// <summary>Verifies the handles read the Focus deck and re-read a new one the frame focus moves.</summary>
    [Test]
    public void AFocusChangeReReadsTheNewDecksStructureImmediately()
    {
        var beatManager = new BeatManager();
        FeedTwoDecks(beatManager, liveOrder: "1");

        // Player 1 sits half way through a 32-beat chorus; player 2 half way through a 64-beat outro.
        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Outro.In.Build(), Is.EqualTo(0f));

        FeedTwoDecks(beatManager, liveOrder: "2,1");

        Assert.That(beatManager.Outro.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0f));
    }

    /// <summary>Verifies a cursor bound to another structure generation is ignored until one matches.</summary>
    [Test]
    public void AGenerationMismatchedCursorIsIgnoredUntilAMatchingOneArrives()
    {
        var beatManager = new BeatManager();
        Feed(beatManager, Track(), currentPhrase: 3, beatInPhrase: 17,
            structureGeneration: 7, cursorGeneration: 6);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.Before.Build(32), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.Before.Decay(32), Is.EqualTo(1f));

        Feed(beatManager, Track(), currentPhrase: 3, beatInPhrase: 17,
            structureGeneration: 7, cursorGeneration: 7);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Chorus.Before.Build(32), Is.EqualTo(0.5f));
    }

    /// <summary>
    /// Verifies a Loop rewinding into the phrase re-enters its In span, because the reading is
    /// positional rather than accumulated over frames.
    /// </summary>
    [Test]
    public void LoopRewindReEntersTheInSpan()
    {
        var beatManager = new BeatManager();
        Feed(beatManager, Track(), currentPhrase: 3, beatInPhrase: 32);

        // Thirty-one of 32 beats elapsed: the linear position is 31 / 32 = 0.96875.
        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(31f / 32f));

        Feed(beatManager, Track(), currentPhrase: 3, beatInPhrase: 1);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Chorus.In.Decay(), Is.EqualTo(1f));
    }

    /// <summary>Verifies every handle rests while the Focus deck holds no structure.</summary>
    [Test]
    public void HandlesRestWhenNoStructureIsHeld()
    {
        var beatManager = new BeatManager();
        LiveFrame(beatManager, snapshot =>
        {
            snapshot.playersLive = "1";
            snapshot.players[0] = PlayerState.Unavailable;
        });

        AssertEveryHandleRests(beatManager);
    }

    /// <summary>Verifies a cursor covering no phrase leaves every handle at rest.</summary>
    [Test]
    public void HandlesRestWhenTheCursorCoversNoPhrase()
    {
        var beatManager = FocusDeck(Track(), currentPhrase: -1, beatInPhrase: -1);

        AssertEveryHandleRests(beatManager);
    }

    /// <summary>Verifies a held structure with no live player leaves every handle at rest.</summary>
    [Test]
    public void HandlesRestWhenNoPlayerIsLive()
    {
        var beatManager = new BeatManager();
        LiveFrame(beatManager, snapshot =>
        {
            snapshot.playersLive = "";
            snapshot.players[0] = Deck(Track(), currentPhrase: 3, beatInPhrase: 17,
                structureGeneration: 1, cursorGeneration: 1);
        });

        AssertEveryHandleRests(beatManager);
    }

    /// <summary>
    /// Verifies a live structure and cursor still rest while the wire reports no running beat count:
    /// without a clock the envelopes would step once per beat rather than move musically.
    /// </summary>
    [Test]
    public void HandlesRestWhileTheWireCarriesNoBeatCount()
    {
        var beatManager = new BeatManager();
        UnsyncedFrame(beatManager, snapshot =>
        {
            snapshot.playersLive = "1";
            snapshot.players[0] = Deck(Track(), currentPhrase: 3, beatInPhrase: 17,
                structureGeneration: 1, cursorGeneration: 1);
        });

        Assert.That(beatManager.IsSynced, Is.False);
        // The structure itself still arrived — only the envelopes rest.
        Assert.That(beatManager.Players[0].Cursor.CurrentPhrase, Is.EqualTo(3));
        AssertEveryHandleRests(beatManager);
    }

    /// <summary>
    /// Verifies handles rest while structure chunks are still converging: until the visible phrase list
    /// is complete a cursor ordinal can name a different tuple than the one at that position.
    /// </summary>
    [Test]
    public void HandlesRestWhileThePhraseListIsStillAssembling()
    {
        var beatManager = new BeatManager();
        var firstChunk = new[] { Phrase(1, 32, "intro"), Phrase(33, 64, "up"), Phrase(65, 96, "chorus") };
        LiveFrame(beatManager, snapshot =>
        {
            snapshot.playersLive = "1";
            // Three of the announced six phrases have landed so far.
            snapshot.players[0] = Deck(firstChunk, currentPhrase: 3, beatInPhrase: 17,
                structureGeneration: 1, cursorGeneration: 1, phraseCount: 6);
        });

        AssertEveryHandleRests(beatManager);

        // The remaining chunk lands under the same generation and the same position now reads.
        Feed(beatManager, Track(), currentPhrase: 3, beatInPhrase: 17);

        Assert.That(beatManager.Chorus.In.Build(), Is.EqualTo(0.5f));
    }

    /// <summary>
    /// Verifies Standalone Mode leaves every envelope at rest, so a speed multiplier written against a
    /// Before decay reads "no response" rather than freezing the effect at zero.
    /// </summary>
    [Test]
    public void HandlesRestInStandaloneMode()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        AssertEveryHandleRests(beatManager);
    }

    /// <summary>
    /// Verifies a structure phrase of the drop type does not feed the event-fed Drop handle, which
    /// keeps its single source in the on-air drop lane.
    /// </summary>
    [Test]
    public void AStructureDropPhraseDoesNotFeedTheEventFedDropHandle()
    {
        var beatManager = FocusDeck(new[] { Phrase(1, 32, "drop") }, currentPhrase: 1, beatInPhrase: 17);

        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(1f));
    }

    /// <summary>Asserts all seven handles read their nothing-happening values.</summary>
    private static void AssertEveryHandleRests(BeatManager beatManager)
    {
        var handles = new[]
        {
            beatManager.Intro, beatManager.Up, beatManager.Down, beatManager.Verse,
            beatManager.Bridge, beatManager.Chorus, beatManager.Outro,
        };

        foreach (var handle in handles)
        {
            Assert.That(handle.Before.Build(32), Is.EqualTo(0f));
            Assert.That(handle.Before.Decay(32), Is.EqualTo(1f));
            Assert.That(handle.In.Build(), Is.EqualTo(0f));
            Assert.That(handle.In.Decay(), Is.EqualTo(0f));
            Assert.That(handle.In.Decay(16), Is.EqualTo(0f));
        }
    }

    /// <summary>A six-phrase track with two adjacent choruses and no verse or bridge.</summary>
    private static StructurePhrase[] Track() => new[]
    {
        Phrase(1, 32, "intro"),
        Phrase(33, 64, "up"),
        Phrase(65, 96, "chorus"),
        Phrase(97, 128, "chorus"),
        Phrase(129, 160, "down"),
        Phrase(161, 192, "outro"),
    };

    /// <summary>Captures one live frame whose focus deck holds the given structure and cursor.</summary>
    private static BeatManager FocusDeck(StructurePhrase[] phrases, int currentPhrase, int beatInPhrase,
        float timeSeconds = 0f)
    {
        var beatManager = new BeatManager();
        Feed(beatManager, phrases, currentPhrase, beatInPhrase, timeSeconds: timeSeconds);
        return beatManager;
    }

    /// <summary>Feeds one live frame in which player 1 is the focus deck and holds the structure.</summary>
    private static void Feed(BeatManager beatManager, StructurePhrase[] phrases, int currentPhrase,
        int beatInPhrase, int structureGeneration = 1, int? cursorGeneration = null, float timeSeconds = 0f)
    {
        LiveFrame(
            beatManager,
            snapshot =>
            {
                snapshot.playersLive = "1";
                snapshot.players[0] = Deck(phrases, currentPhrase, beatInPhrase, structureGeneration,
                    cursorGeneration ?? structureGeneration);
            },
            timeSeconds);
    }

    /// <summary>Feeds one live frame carrying two decks whose structures cannot be confused.</summary>
    private static void FeedTwoDecks(BeatManager beatManager, string liveOrder)
    {
        LiveFrame(beatManager, snapshot =>
        {
            snapshot.playersLive = liveOrder;
            snapshot.players[0] = Deck(new[] { Phrase(1, 32, "chorus") },
                currentPhrase: 1, beatInPhrase: 17, structureGeneration: 1, cursorGeneration: 1);
            snapshot.players[1] = Deck(new[] { Phrase(1, 64, "outro") },
                currentPhrase: 1, beatInPhrase: 33, structureGeneration: 4, cursorGeneration: 4);
        });
    }

    /// <summary>Feeds one live frame after applying a focused mutation to a deterministic snapshot.</summary>
    private static void LiveFrame(BeatManager beatManager, System.Action<RaveWireSnapshot> mutate,
        float timeSeconds = 0f)
    {
        var snapshot = BeatClockFixture.CreateSnapshot(120f, timeSeconds);
        mutate(snapshot);
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
    }

    /// <summary>
    /// Feeds one live frame carrying no beat count at all, the lossy-UDP case in which per-player lanes
    /// arrive while the beat lane is unavailable.
    /// </summary>
    private static void UnsyncedFrame(BeatManager beatManager, System.Action<RaveWireSnapshot> mutate)
    {
        var snapshot = new RaveWireSnapshot();
        mutate(snapshot);
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
    }

    /// <summary>Builds one physical player holding a song structure and its live cursor.</summary>
    /// <param name="phraseCount">
    /// Full-track phrase count the sender announces; leave at <c>-1</c> for a fully assembled structure,
    /// or pass a larger number to model a phrase list whose remaining chunks have not landed yet.
    /// </param>
    private static PlayerState Deck(StructurePhrase[] phrases, int currentPhrase, int beatInPhrase,
        int structureGeneration, int cursorGeneration, int phraseCount = -1)
    {
        var covered = currentPhrase >= 1 && currentPhrase <= phrases.Length && beatInPhrase >= 1;
        var deck = PlayerState.Unavailable;
        deck.structure = new PlayerStructure
        {
            generation = structureGeneration,
            trackId = "phrase-handle-test",
            source = "analyzed",
            totalBeats = phrases[phrases.Length - 1].endBeat,
            phraseCount = phraseCount >= 0 ? phraseCount : phrases.Length,
            phrases = phrases,
        };
        deck.cursor = new StructureCursor
        {
            generation = cursorGeneration,
            currentPhrase = covered ? currentPhrase : -1,
            beatInPhrase = covered ? beatInPhrase : -1,
            beatsToNextPhrase = covered
                ? phrases[currentPhrase - 1].endBeat - phrases[currentPhrase - 1].startBeat - beatInPhrase + 2
                : -1,
        };
        return deck;
    }

    /// <summary>Creates one wire phrase tuple of the given type.</summary>
    private static StructurePhrase Phrase(int startBeat, int endBeat, string type) => new StructurePhrase
    {
        startBeat = startBeat,
        endBeat = endBeat,
        type = type,
        variant = 0,
        fillStartBeat = 0,
        dropLandingBeat = 0,
    };
}
