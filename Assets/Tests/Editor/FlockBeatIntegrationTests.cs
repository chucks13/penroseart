
#nullable enable

using NUnit.Framework;
using UnityEngine;

public sealed class FlockBeatIntegrationTests
{
    [TestCase(0f, 1f)]
    [TestCase(0.25f, 1.25f)]
    [TestCase(0.5f, 1.7071068f)]
    [TestCase(1f, 3f)]
    public void BeatSpeedMultiplierMapsWaveformEnvelopeToAdditiveSpeedLift(float envelope, float expected)
    {
        Assert.That(Flock.GetBeatSpeedMultiplier(envelope, true), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void BeatSpeedMultiplierIsNormalSpeedWhenBeatIsDisabled()
    {
        Assert.That(Flock.GetBeatSpeedMultiplier(1f, false), Is.EqualTo(1f));
    }


    [Test]
    public void LowEnergyHueShiftLeavesColorAloneAtZero()
    {
        var color = Color.HSVToRGB(0.25f, 0.5f, 0.75f);

        Assert.That(Flock.ShiftHueByLowEnergy(color, 0f), Is.EqualTo(color));
    }

    [Test]
    public void LowEnergyHueShiftSlightlyOffsetsHueAtFullLowEnergy()
    {
        var color = Color.HSVToRGB(0.25f, 0.5f, 0.75f);
        var shifted = Flock.ShiftHueByLowEnergy(color, 1f);

        Color.RGBToHSV(shifted, out var hue, out var saturation, out var value);
        Assert.That(hue, Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(saturation, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(value, Is.EqualTo(0.75f).Within(0.0001f));
    }
}
