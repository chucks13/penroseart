// Contract tests for preserving every valid loop_state wire fact at the Loop doorway.

#nullable enable

using System.Reflection;
using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Pins the Loop doorway's region-versus-rolling contract from ADR-0013 using hand-worked wire
/// examples: valid region facts survive idle playback, unavailable facts remain null, and a
/// rolling span carries its measured length anchor.
/// </summary>
public sealed class LoopDoorwayContractTests
{
    /// <summary>An idle set region serves measured and nominal facts without claiming a rolling span.</summary>
    [Test]
    public void IdleSetLoopServesRegionFactsWithoutRollingSpan()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.loopState = new LoopState
        {
            active = 0,
            set = 1,
            lengthBeats = 0.5f,
            lengthMs = 234,
            sizeNumerator = 1,
            sizeDenominator = 2
        };

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.RegionSet, Is.True);
        Assert.That(loop.Region, Is.Not.Null);
        Assert.That(loop.Region!.Value.LengthBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(loop.Region.Value.LengthMs, Is.EqualTo(234));
        Assert.That(loop.Region.Value.NominalSizeBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(loop.Span.Current, Is.Null);
        Assert.That(loop.Span.Started, Is.False);
        Assert.That(loop.Span.Ended, Is.False);
    }

    /// <summary>The complete all-sentinel lane serves neither a region nor a region-set answer.</summary>
    [Test]
    public void UnavailableLoopServesNoRegionOrRegionSet()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.loopState = new LoopState
        {
            active = -1,
            set = -1,
            lengthBeats = -1f,
            lengthMs = -1,
            sizeNumerator = -1,
            sizeDenominator = -1
        };

        beatManager.Update(0f);

        Assert.That(beatManager.Loop.Region, Is.Null);
        Assert.That(beatManager.Loop.RegionSet, Is.Null);
        Assert.That(beatManager.Loop.Span.Current, Is.Null);
    }

    /// <summary>A rolling loop serves both region and span facts, with measured beats as the span anchor.</summary>
    [Test]
    public void ActiveLoopServesRegionAndSpanWithMeasuredLengthAnchor()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.loopState = new LoopState
        {
            active = 1,
            set = 1,
            lengthBeats = 4f,
            lengthMs = 1875,
            sizeNumerator = 4,
            sizeDenominator = 1
        };

        beatManager.Update(0f);

        var loop = beatManager.Loop;
        Assert.That(loop.Region, Is.Not.Null);
        Assert.That(loop.Region!.Value.LengthBeats, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(loop.Region.Value.LengthMs, Is.EqualTo(1875));
        Assert.That(loop.Region.Value.NominalSizeBeats, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(loop.Span.Current, Is.Not.Null);
        Assert.That(loop.Span.Current!.Value.LengthBeats, Is.EqualTo(4f).Within(0.0001f));

        var lengthAnchor = typeof(SpanView<LoopFacts>).GetField(
            "lengthBeats", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(lengthAnchor, Is.Not.Null);
        Assert.That(lengthAnchor!.GetValue(loop.Span), Is.EqualTo(4f).Within(0.0001f));
    }
}
