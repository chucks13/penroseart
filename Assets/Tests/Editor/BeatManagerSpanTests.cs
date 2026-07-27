// Contract tests for the Before/In spans served by the Drop and Fill handles.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Tests the span envelopes through the Data Surface seam: a hand-built wire snapshot goes in, one
/// frame is captured, and the handle's spans are read. Envelope internals are never touched directly.
/// </summary>
/// <remarks>
/// Expected values come from the linear contract — Build is the normalized position and Decay is one
/// minus that position — worked out by hand rather than recomputed the way the runtime does.
/// </remarks>
public sealed class BeatManagerSpanTests
{
    /// <summary>Verifies the approach build rises from zero to one as the upcoming drop lands.</summary>
    [Test]
    public void BeforeBuildRisesToOneAsTheDropLands()
    {
        // Eight beats out fills the whole eight-beat runway, so the ramp has not started.
        Assert.That(UpcomingDrop(beatsUntil: 8).Drop.Before.Build(8), Is.EqualTo(0f));
        Assert.That(UpcomingDrop(beatsUntil: 4).Drop.Before.Build(8), Is.EqualTo(0.5f));
        Assert.That(UpcomingDrop(beatsUntil: 0).Drop.Before.Build(8), Is.EqualTo(1f));
    }

    /// <summary>Verifies the approach decay falls from one to zero as the upcoming drop lands.</summary>
    [Test]
    public void BeforeDecayFallsToZeroAsTheDropLands()
    {
        Assert.That(UpcomingDrop(beatsUntil: 8).Drop.Before.Decay(8), Is.EqualTo(1f));
        Assert.That(UpcomingDrop(beatsUntil: 4).Drop.Before.Decay(8), Is.EqualTo(0.5f));
        Assert.That(UpcomingDrop(beatsUntil: 0).Drop.Before.Decay(8), Is.EqualTo(0f));
    }

    /// <summary>Verifies approach decay exactly matches the linear distance formula across its window.</summary>
    [Test]
    public void BeforeDecayMatchesTheLinearDistanceFormulaAcrossTheWholeWindow()
    {
        for (var until = 0; until <= 8; until++)
        {
            Assert.That(UpcomingDrop(until).Drop.Before.Decay(8), Is.EqualTo(until / 8f));
        }
    }

    /// <summary>Verifies Fluid's fill gate opens exactly inside the final six whole beats.</summary>
    [Test]
    public void FillBeforeBuildOpensOnlyInsideTheFinalSixBeats()
    {
        for (var until = 0; until <= 8; until++)
        {
            var gateIsOpen = UpcomingFill(until).Fill.Before.Build(6) > 0f;
            Assert.That(gateIsOpen, Is.EqualTo(until <= 5));
        }
    }

    /// <summary>Verifies a drop beyond the named window reads as infinitely far in both envelopes.</summary>
    [Test]
    public void BeforeEnvelopesRestWhileTheDropIsBeyondTheWindow()
    {
        var beatManager = UpcomingDrop(beatsUntil: 16);

        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(1f));
    }

    /// <summary>Verifies the approach advances continuously with the captured intra-beat fraction.</summary>
    [Test]
    public void BeforeEnvelopesMoveContinuouslyWithinTheBeat()
    {
        var onTheBeat = UpcomingDrop(beatsUntil: 4).Drop.Before.Build(8);
        var halfwayThroughTheBeat = UpcomingDrop(beatsUntil: 4, timeSeconds: 0.25f).Drop.Before.Build(8);
        var nextWholeBeat = UpcomingDrop(beatsUntil: 3, timeSeconds: 0.25f).Drop.Before.Build(8);

        Assert.That(onTheBeat, Is.EqualTo(0.5f));
        Assert.That(halfwayThroughTheBeat, Is.EqualTo(0.5625f));
        Assert.That(nextWholeBeat, Is.EqualTo(0.6875f));
    }

    /// <summary>Verifies the window is read as whole beats, so a shorter runway ramps later.</summary>
    [Test]
    public void BeforeWindowsAreCountedInWholeBeats()
    {
        var beatManager = UpcomingDrop(beatsUntil: 4);

        Assert.That(beatManager.Drop.Before.Build(4), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0.5f));
        // A longer runway started earlier, so four beats out is already three quarters along it.
        Assert.That(beatManager.Drop.Before.Build(16), Is.EqualTo(0.75f));
    }

    /// <summary>Verifies an unusable window rests rather than throwing or reading as landed.</summary>
    [Test]
    public void BeforeEnvelopesRestForNonPositiveWindows()
    {
        var beatManager = UpcomingDrop(beatsUntil: 4);

        Assert.That(beatManager.Drop.Before.Build(0), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(0), Is.EqualTo(1f));
        Assert.That(beatManager.Drop.Before.Build(-1), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(-1), Is.EqualTo(1f));
    }

    /// <summary>Verifies the approach rests once the drop is under way, since nothing is coming.</summary>
    [Test]
    public void BeforeEnvelopesRestWhileTheDropIsActive()
    {
        var beatManager = ActiveDrop(beatsRemaining: 20, lengthBeats: 32);

        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(1f));
    }

    /// <summary>Verifies In with no argument spans the event's own length, not a hidden default.</summary>
    [Test]
    public void InDefaultsToTheEventsOwnLength()
    {
        // Eight of 32 beats elapsed is 8 / 32 = 0.25 across the drop's own length, while an
        // eight-beat window has completed and a sixteen-beat window is half way.
        var beatManager = ActiveDrop(beatsRemaining: 24, lengthBeats: 32);

        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0.25f));
        Assert.That(beatManager.Drop.In.Build(8), Is.EqualTo(1f));
        Assert.That(beatManager.Drop.In.Build(16), Is.EqualTo(0.5f));
        Assert.That(beatManager.Drop.In.Decay(), Is.EqualTo(0.75f));
    }

    /// <summary>
    /// Verifies independently valued live countdown lanes still feed their whole-beat approach envelopes
    /// while the beat clock is absent.
    /// </summary>
    [Test]
    public void BeforeEnvelopesReadIndependentLiveCountdownsWithoutABeatClock()
    {
        var beatManager = new BeatManager();
        var snapshot = new RaveWireSnapshot
        {
            dropState = new CountdownState { active = 0, countBeats = 4, lengthBeats = 16, remaining = 1 },
            fillState = new CountdownState { active = 0, countBeats = 2, lengthBeats = 8, remaining = 1 },
        };
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Drop.BeatsUntil, Is.EqualTo(4));
        Assert.That(beatManager.Fill.BeatsUntil, Is.EqualTo(2));
        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0.5f));
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(0.5f));
        Assert.That(beatManager.Fill.Before.Build(8), Is.EqualTo(0.75f));
        Assert.That(beatManager.Fill.Before.Decay(8), Is.EqualTo(0.25f));
    }

    /// <summary>
    /// Verifies live through-event envelopes step with whole-beat countdown changes while the intra-beat
    /// clock lane is absent.
    /// </summary>
    [Test]
    public void InEnvelopesStepFromLiveCountdownsWithoutABeatClock()
    {
        var beatManager = new BeatManager();
        var snapshot = new RaveWireSnapshot
        {
            dropState = new CountdownState { active = 1, countBeats = 16, lengthBeats = 32, remaining = 1 },
            fillState = new CountdownState { active = 1, countBeats = 24, lengthBeats = 32, remaining = 1 },
        };
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Drop.In.Decay(), Is.EqualTo(0.5f));
        Assert.That(beatManager.Drop.In.Build(8), Is.EqualTo(1f));
        Assert.That(beatManager.Fill.In.Build(), Is.EqualTo(0.25f));
        Assert.That(beatManager.Fill.In.Decay(), Is.EqualTo(0.75f));
        Assert.That(beatManager.Drop.Active, Is.True);
        Assert.That(beatManager.Drop.BeatsRemaining, Is.EqualTo(16));
        Assert.That(beatManager.Drop.LengthBeats, Is.EqualTo(32));
        Assert.That(beatManager.Drop.Progress, Is.EqualTo(0.5f));

        snapshot.dropState.countBeats = 15;
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(17f / 32f));
    }

    /// <summary>Verifies the through-the-drop envelopes rest while the drop is only upcoming.</summary>
    [Test]
    public void InEnvelopesRestWhileTheDropIsOnlyUpcoming()
    {
        var beatManager = UpcomingDrop(beatsUntil: 4);

        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.In.Decay(), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.In.Decay(8), Is.EqualTo(0f));
    }

    /// <summary>Verifies both spans rest at their nothing-happening values when the lane is unavailable.</summary>
    [Test]
    public void SpansRestWhenTheWireCarriesNoDropOrFill()
    {
        var beatManager = LiveManager(_ => { });

        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(1f));
        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.In.Decay(), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.Before.Decay(8), Is.EqualTo(1f));
        Assert.That(beatManager.Fill.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.In.Decay(), Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies Standalone Mode leaves every envelope at rest, so a speed multiplier written against a
    /// Before decay reads "no response" rather than freezing the effect at zero.
    /// </summary>
    [Test]
    public void SpansRestInStandaloneMode()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Drop.Before.Decay(8), Is.EqualTo(1f));
        Assert.That(beatManager.Fill.Before.Decay(8), Is.EqualTo(1f));
        Assert.That(beatManager.Drop.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.Before.Build(8), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Drop.In.Decay(), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.In.Build(), Is.EqualTo(0f));
        Assert.That(beatManager.Fill.In.Decay(), Is.EqualTo(0f));
    }

    /// <summary>Verifies the Fill handle reads identically to Drop from identical wire state.</summary>
    [Test]
    public void FillSpansMatchDropSpansFromIdenticalWireState()
    {
        var upcoming = LiveManager(snapshot =>
        {
            snapshot.dropState = new CountdownState { active = 0, countBeats = 4, lengthBeats = 8, remaining = 1 };
            snapshot.fillState = new CountdownState { active = 0, countBeats = 4, lengthBeats = 8, remaining = 1 };
        });
        var running = LiveManager(snapshot =>
        {
            snapshot.dropState = new CountdownState { active = 1, countBeats = 4, lengthBeats = 8, remaining = 1 };
            snapshot.fillState = new CountdownState { active = 1, countBeats = 4, lengthBeats = 8, remaining = 1 };
        });

        Assert.That(upcoming.Fill.Before.Build(8), Is.EqualTo(upcoming.Drop.Before.Build(8)));
        Assert.That(upcoming.Fill.Before.Decay(8), Is.EqualTo(upcoming.Drop.Before.Decay(8)));
        Assert.That(running.Fill.In.Build(), Is.EqualTo(running.Drop.In.Build()));
        Assert.That(running.Fill.In.Decay(), Is.EqualTo(running.Drop.In.Decay()));
        Assert.That(running.Fill.In.Build(), Is.EqualTo(0.5f));
    }

    /// <summary>Captures one live frame carrying an upcoming drop the given number of beats away.</summary>
    private static BeatManager UpcomingDrop(int beatsUntil, float timeSeconds = 0f) =>
        LiveManager(
            snapshot => snapshot.dropState =
                new CountdownState { active = 0, countBeats = beatsUntil, lengthBeats = 16, remaining = 1 },
            timeSeconds);

    /// <summary>Captures one live frame carrying an upcoming fill the given number of beats away.</summary>
    private static BeatManager UpcomingFill(int beatsUntil) =>
        LiveManager(snapshot => snapshot.fillState =
            new CountdownState { active = 0, countBeats = beatsUntil, lengthBeats = 8, remaining = 1 });

    /// <summary>Captures one live frame carrying a drop already under way.</summary>
    private static BeatManager ActiveDrop(int beatsRemaining, int lengthBeats) =>
        LiveManager(snapshot => snapshot.dropState = new CountdownState
        {
            active = 1, countBeats = beatsRemaining, lengthBeats = lengthBeats, remaining = 1,
        });

    /// <summary>Captures one live frame after applying a focused mutation to a deterministic snapshot.</summary>
    private static BeatManager LiveManager(System.Action<RaveWireSnapshot> mutate, float timeSeconds = 0f)
    {
        var snapshot = BeatClockFixture.CreateSnapshot(120f, timeSeconds);
        mutate(snapshot);
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }
}
