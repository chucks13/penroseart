using NUnit.Framework;

public sealed class ChangeCadenceTests
{
    [Test]
    public void AllowsFirstChangeAndBlocksUntilMinimumBeatsPass()
    {
        Assert.That(ChangeCadence.CanChangeAt(593, previousSelectedPhaseBoundary: null, minimumBeats: 16), Is.True);
        Assert.That(ChangeCadence.CanChangeAt(608, previousSelectedPhaseBoundary: 593, minimumBeats: 16), Is.False);
        Assert.That(ChangeCadence.CanChangeAt(609, previousSelectedPhaseBoundary: 593, minimumBeats: 16), Is.True);
    }
}
