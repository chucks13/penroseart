#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins <see cref="PhraseEventView"/>'s display model of a <see cref="PhraseEventInfo"/> (Fill/Drop):
/// the status chip label, the meter fill, the one-line readout, and the Now/Soon/Idle state. This is
/// the interface the BeatManager inspector — and any future telnet/OSC readout — actually calls, so
/// the tests target it directly rather than reaching into a drawer's private helpers.
/// </summary>
public sealed class PhraseEventViewTests
{
    private static PhraseEventInfo InProgressEvent => new PhraseEventInfo(
        inProgress: true, beatsUntilStart: null, msUntilStart: null, beatsUntilEnd: 9,
        progress: 0.4375f, anticipation: null, lengthBeats: 16, remaining: 1);

    private static PhraseEventInfo UpcomingEvent => new PhraseEventInfo(
        inProgress: false, beatsUntilStart: 7, msUntilStart: 3500, beatsUntilEnd: null,
        progress: null, anticipation: 0.796875f, lengthBeats: 16, remaining: 2);

    /// <summary>The "no more left" wire shape: nothing upcoming, zero remaining — values, not a state.</summary>
    private static PhraseEventInfo NoUpcomingEvent => new PhraseEventInfo(
        inProgress: false, beatsUntilStart: null, msUntilStart: null, beatsUntilEnd: null,
        progress: null, anticipation: null, lengthBeats: null, remaining: 0);

    [Test]
    public void ChipSaysNowInProgressCountdownWhileUpcomingAndNullOtherwise()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).Chip, Is.EqualTo("NOW"));
        Assert.That(PhraseEventView.Of(UpcomingEvent).Chip, Is.EqualTo("IN 7"));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).Chip, Is.EqualTo("—"));
    }

    [Test]
    public void StateClassifiesNowWhilePlayingSoonWhileCountingDownIdleOtherwise()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).State, Is.EqualTo(PhraseEventState.Now));
        Assert.That(PhraseEventView.Of(UpcomingEvent).State, Is.EqualTo(PhraseEventState.Soon));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).State, Is.EqualTo(PhraseEventState.Idle));
    }

    [Test]
    public void MeterSweepsProgressInProgressAndFillsWithAnticipationWhileCountingDown()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).Meter, Is.EqualTo(0.4375f));
        Assert.That(PhraseEventView.Of(UpcomingEvent).Meter, Is.EqualTo(0.796875f));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).Meter, Is.EqualTo(0f));
    }

    [Test]
    public void ReadoutShowsNullAsNullAndZeroAsZero()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).Readout, Is.EqualTo("ends in 9b · len 16 · ×1"));
        Assert.That(PhraseEventView.Of(UpcomingEvent).Readout, Is.EqualTo("in 3.5s · len 16 · ×2"));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).Readout, Is.EqualTo("next — · ×0"));
    }

    [Test]
    public void ReadoutFallsBackToBeatsWhenNoBeatIntervalIsAvailable()
    {
        var noMs = new PhraseEventInfo(
            inProgress: false, beatsUntilStart: 7, msUntilStart: null, beatsUntilEnd: null,
            progress: null, anticipation: 0.796875f, lengthBeats: 16, remaining: 2);

        Assert.That(PhraseEventView.Of(noMs).Readout, Is.EqualTo("in 7b · len 16 · ×2"));
    }
}
