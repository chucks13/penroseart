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
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot { loopState = new LoopState
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
        Assert.That(loop.NominalSizeBeats, Is.EqualTo(4f).Within(0.0001f));
    }

    /// <summary>An idle, set lane preserves its measured and nominal region facts per ADR-0013.</summary>
    [Test]
    public void IdleSetLoopPreservesLengthsAndNominalSize()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot { loopState = new LoopState
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
        Assert.That(loop.NominalSizeBeats, Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>The complete all-sentinel lane translates every flat Loop fact to null.</summary>
    [Test]
    public void AllSentinelLoopServesAllNull()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot { loopState = new LoopState
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
        Assert.That(loop.Rolling, Is.Null);
        Assert.That(loop.RegionSet, Is.Null);
        Assert.That(loop.LengthBeats, Is.Null);
        Assert.That(loop.LengthMilliseconds, Is.Null);
        Assert.That(loop.SizeNumerator, Is.Null);
        Assert.That(loop.SizeDenominator, Is.Null);
        Assert.That(loop.NominalSizeBeats, Is.Null);
    }

    /// <summary>Real zero wire values remain zero while a zero denominator produces no nominal size.</summary>
    [Test]
    public void RealZerosRemainZeroWithoutInventingANominalSize()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot { loopState = new LoopState
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
        Assert.That(loop.NominalSizeBeats, Is.Null);
    }
}
