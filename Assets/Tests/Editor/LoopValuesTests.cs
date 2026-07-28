// Contract tests for the Loop values' flat, nullable mirror of the loop_state wire lane.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Pins the Loop group's flat wire-mirror contract with hand-worked lane values: rolling and set
/// are independent facts, idle regions retain their measurements, and sentinels translate to null
/// without derived span semantics.
/// </summary>
public sealed class LoopValuesTests
{
    /// <summary>A rolling, set lane serves every translated wire fact directly on the flat group.</summary>
    [Test]
    public void RollingSetLoopServesAllFacts()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveWireSnapshot { loopState = new LoopState
        {
            active = 1,
            set = 1,
            lengthBeats = 4f,
            lengthMs = 1875,
            sizeNumerator = 4,
            sizeDenominator = 1
        }});

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.Rolling, Is.True);
        Assert.That(loop.RegionSet, Is.True);
        Assert.That(loop.LengthBeats, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(loop.LengthMilliseconds, Is.EqualTo(1875));
        Assert.That(loop.SizeNumerator, Is.EqualTo(4));
        Assert.That(loop.SizeDenominator, Is.EqualTo(1));
    }

    /// <summary>An idle, set lane preserves its measured region facts.</summary>
    [Test]
    public void IdleSetLoopPreservesItsMeasuredLengths()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveWireSnapshot { loopState = new LoopState
        {
            active = 0,
            set = 1,
            lengthBeats = 0.5f,
            lengthMs = 234,
            sizeNumerator = 1,
            sizeDenominator = 2
        }});

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.Rolling, Is.False);
        Assert.That(loop.RegionSet, Is.True);
        Assert.That(loop.LengthBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(loop.LengthMilliseconds, Is.EqualTo(234));
        Assert.That(loop.SizeNumerator, Is.EqualTo(1));
        Assert.That(loop.SizeDenominator, Is.EqualTo(2));
    }

    /// <summary>The complete all-sentinel lane rests the flags at false and translates every numeric fact to null.</summary>
    [Test]
    public void AllSentinelLoopServesRestingValues()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveWireSnapshot { loopState = new LoopState
        {
            active = -1,
            set = -1,
            lengthBeats = -1f,
            lengthMs = -1,
            sizeNumerator = -1,
            sizeDenominator = -1
        }});

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.Rolling, Is.False);
        Assert.That(loop.RegionSet, Is.False);
        Assert.That(loop.LengthBeats, Is.Null);
        Assert.That(loop.LengthMilliseconds, Is.Null);
        Assert.That(loop.SizeNumerator, Is.Null);
        Assert.That(loop.SizeDenominator, Is.Null);
    }

    /// <summary>Real zero wire values remain zero rather than translating to the null sentinel.</summary>
    [Test]
    public void RealZerosRemainZero()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveWireSnapshot { loopState = new LoopState
        {
            active = 0,
            set = 0,
            lengthBeats = 0f,
            lengthMs = 0,
            sizeNumerator = 0,
            sizeDenominator = 0
        }});

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.LengthBeats, Is.EqualTo(0f));
        Assert.That(loop.LengthMilliseconds, Is.Zero);
        Assert.That(loop.SizeNumerator, Is.Zero);
        Assert.That(loop.SizeDenominator, Is.Zero);
    }
}
