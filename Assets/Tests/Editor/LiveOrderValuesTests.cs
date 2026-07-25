// Contract tests for the typed players_live Data Surface lane.

#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins live-order translation, focus selection, malformed-token tolerance, and no-beat clearing.
/// </summary>
public sealed class LiveOrderValuesTests
{
    /// <summary>The wire's newest-first order is preserved and its first player becomes focus.</summary>
    [Test]
    public void LiveOrderPreservesOrderAndFocusesFirstPlayer()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot => snapshot.playersLive = "6,3,1");

        beatManager.Update(0f);

        Assert.That(beatManager.LiveOrder.Players, Is.EqualTo(new[] { 6, 3, 1 }));
        Assert.That(beatManager.LiveOrder.Focus, Is.EqualTo(6));
    }

    /// <summary>Both an absent lane and an explicitly empty lane capture as empty with no focus.</summary>
    [Test]
    public void EmptyOrAbsentLiveOrderHasNoFocus()
    {
        var absentBeatManager = new BeatManager();
        BeatManagerWireFixture.Feed(absentBeatManager, _ => { });
        absentBeatManager.Update(0f);

        Assert.That(absentBeatManager.LiveOrder.Players, Is.Empty);
        Assert.That(absentBeatManager.LiveOrder.Focus, Is.Null);

        var emptyBeatManager = new BeatManager();
        BeatManagerWireFixture.Feed(emptyBeatManager, snapshot => snapshot.playersLive = "");
        emptyBeatManager.Update(0f);

        Assert.That(emptyBeatManager.LiveOrder.Players, Is.Empty);
        Assert.That(emptyBeatManager.LiveOrder.Focus, Is.Null);
    }

    /// <summary>Malformed and out-of-range tokens are ignored without disturbing valid order.</summary>
    [Test]
    public void MalformedLiveOrderTokensAreSkipped()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(
            beatManager,
            snapshot => snapshot.playersLive = "4,nope,,2,7,0,3x");

        Assert.DoesNotThrow(() => beatManager.Update(0f));
        Assert.That(beatManager.LiveOrder.Players, Is.EqualTo(new[] { 4, 2 }));
        Assert.That(beatManager.LiveOrder.Focus, Is.EqualTo(4));
    }

    /// <summary>Stopping the live source clears the order and focus through the no-beat path.</summary>
    [Test]
    public void BroadcastStopClearsLiveOrder()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot => snapshot.playersLive = "4,2");
        beatManager.Update(0f);
        Assert.That(beatManager.LiveOrder.Focus, Is.EqualTo(4));

        beatManager.SetLiveBeatSource(false);
        beatManager.Update(1f);

        Assert.That(beatManager.LiveOrder.Players, Is.Empty);
        Assert.That(beatManager.LiveOrder.Focus, Is.Null);
    }
}
