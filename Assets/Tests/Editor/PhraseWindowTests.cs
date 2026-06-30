using System.Linq;
using NUnit.Framework;

public sealed class PhraseWindowTests
{
    [Test]
    public void TrackPhaseCountdownDefinesPhraseWindowAndPhaseBoundaries()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);

        Assert.That(window.StartBeat, Is.EqualTo(577));
        Assert.That(window.EndBeat, Is.EqualTo(609));
        Assert.That(window.LengthBeats, Is.EqualTo(32));
        Assert.That(window.GridBoundariesAfter(588).ToArray(), Is.EqualTo(new[] { 593, 609 }));
    }

    [Test]
    public void TimingIdentityUsesExactStartEndAndLength()
    {
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 588,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var window), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 20,
            phraseLengthBeats: 32,
            out var sameTiming), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 21,
            phraseLengthBeats: 32,
            out var shiftedTiming), Is.True);
        Assert.That(PhraseWindow.TryFromTrackPhase(
            beat: 589,
            beatsToPhraseBoundary: 20,
            phraseLengthBeats: 48,
            out var differentLength), Is.True);

        Assert.That(window.HasSameTimingIdentity(sameTiming), Is.True);
        Assert.That(window.HasSameTimingIdentity(shiftedTiming), Is.False);
        Assert.That(window.HasSameTimingIdentity(differentLength), Is.False);
    }
}
