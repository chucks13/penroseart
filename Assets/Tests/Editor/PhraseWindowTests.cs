using System.Linq;
using NUnit.Framework;

public sealed class PhraseWindowTests
{
    [Test]
    public void TrackPhaseCountdownDefinesPhraseWindowAndImpactSlots()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);

        Assert.That(window.StartBeat, Is.EqualTo(577));
        Assert.That(window.EndBeat, Is.EqualTo(609));
        Assert.That(window.ImpactSlotsAfter(588).ToArray(), Is.EqualTo(new[] { 593, 609 }));
    }
}
