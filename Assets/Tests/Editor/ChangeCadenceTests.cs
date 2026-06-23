using NUnit.Framework;

public sealed class ChangeCadenceTests
{
    [Test]
    public void AllowsFirstChangeAndBlocksUntilMinimumBeatsPass()
    {
        Assert.That(ChangeCadence.CanChangeAt(593, previousCueMarkBeat: null, minimumBeats: 16), Is.True);
        Assert.That(ChangeCadence.CanChangeAt(608, previousCueMarkBeat: 593, minimumBeats: 16), Is.False);
        Assert.That(ChangeCadence.CanChangeAt(609, previousCueMarkBeat: 593, minimumBeats: 16), Is.True);
    }
}
