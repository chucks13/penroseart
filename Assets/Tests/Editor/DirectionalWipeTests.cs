using NUnit.Framework;
using UnityEngine;

public sealed class DirectionalWipeTests
{
    [Test]
    public void LowBandBrightnessLeavesColorUnchangedAwayFromEdge()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 1f);

        AssertSameColor(color, DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 0f, lowBandLevel: 1f));
    }

    [Test]
    public void EdgeBrightnessHasBaseLiftWithoutLowBandPulse()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 0.75f);

        var baseEdge = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 0f);

        Assert.That(baseEdge.r, Is.GreaterThan(color.r));
        Assert.That(baseEdge.g, Is.GreaterThan(color.g));
        Assert.That(baseEdge.b, Is.GreaterThan(color.b));
        Assert.That(baseEdge.r, Is.LessThan(0.55f));
        Assert.That(baseEdge.a, Is.EqualTo(color.a));
    }

    [Test]
    public void LowBandBrightnessAddsReactiveLiftAtWipeEdge()
    {
        var color = new Color(0.4f, 0.2f, 0.1f, 0.75f);

        var baseEdge = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 0f);
        var brightened = DirectionalWipe.ApplyLowBandEdgeBrightness(color, edgePresence: 1f, lowBandLevel: 1f);

        Assert.That(brightened.r, Is.GreaterThan(baseEdge.r + 0.25f));
        Assert.That(brightened.g, Is.GreaterThan(baseEdge.g + 0.15f));
        Assert.That(brightened.b, Is.GreaterThan(baseEdge.b + 0.1f));
        Assert.That(brightened.r, Is.LessThanOrEqualTo(1f));
        Assert.That(brightened.a, Is.EqualTo(color.a));
    }

    [Test]
    public void EdgePresencePeaksAtWipeBoundaryAndFallsAway()
    {
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.5f, transitionProgress: 0.5f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.51f, transitionProgress: 0.5f), Is.GreaterThan(0f));
        Assert.That(DirectionalWipe.EdgePresence(projectedProgress: 0.8f, transitionProgress: 0.5f), Is.EqualTo(0f).Within(0.001f));
    }

    private static void AssertSameColor(Color expected, Color actual)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}
