
#nullable enable

using NUnit.Framework;

public sealed class FlockBeatIntegrationTests
{
    [TestCase(0f, 0.25f)]
    [TestCase(0.25f, 0.59375f)]
    [TestCase(0.5f, 1.2222718f)]
    [TestCase(1f, 3f)]
    public void BeatSpeedMultiplierMapsBeatPulseToDramaticCurvedSpeed(float beatPulse, float expected)
    {
        Assert.That(Flock.GetBeatSpeedMultiplier(beatPulse, true), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void BeatSpeedMultiplierIsNormalSpeedWhenBeatIsDisabled()
    {
        Assert.That(Flock.GetBeatSpeedMultiplier(1f, false), Is.EqualTo(1f));
    }
}
