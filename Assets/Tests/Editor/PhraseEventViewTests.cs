#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins <see cref="PhraseEventView"/>'s display model of the canonical Fill doorway:
/// the status chip label, the meter fill, the one-line readout, and the Now/Soon/Idle state. This is
/// the interface the BeatManager inspector actually calls, so the tests target it directly rather
/// than reaching into a drawer's private helpers.
/// </summary>
public sealed class PhraseEventViewTests
{
    /// <summary>A running Fill with served progress, length, countdown, and remaining count.</summary>
    private static FillView InProgressEvent => new FillView(
        new SpanView<FillFacts>(new FillFacts(9, 16), 0.4375f, false, false, 7f, 16f),
        null, null, 1);

    /// <summary>An upcoming Fill seven beats away with two occurrences remaining.</summary>
    private static FillView UpcomingEvent => new FillView(default, 7, 16, 2);

    /// <summary>The "no more left" wire shape: nothing upcoming, zero remaining — values, not a state.</summary>
    private static FillView NoUpcomingEvent => new FillView(default, null, null, 0);

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

    /// <summary>Running progress and upcoming beat anticipation map onto the expected meter values.</summary>
    [Test]
    public void MeterSweepsProgressInProgressAndFillsWithAnticipationWhileCountingDown()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).Meter, Is.EqualTo(0.4375f));
        Assert.That(PhraseEventView.Of(UpcomingEvent).Meter, Is.EqualTo(0.78125f));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).Meter, Is.EqualTo(0f));
    }

    /// <summary>Readouts preserve running, upcoming, and real zero-remaining facts.</summary>
    [Test]
    public void ReadoutShowsNullAsNullAndZeroAsZero()
    {
        Assert.That(PhraseEventView.Of(InProgressEvent).Readout, Is.EqualTo("ends in 9b · len 16 · ×1"));
        Assert.That(PhraseEventView.Of(UpcomingEvent).Readout, Is.EqualTo("in 7b · len 16 · ×2"));
        Assert.That(PhraseEventView.Of(NoUpcomingEvent).Readout, Is.EqualTo("next — · ×0"));
    }

    /// <summary>The Drop doorway formats through the same countdown display shape as Fill.</summary>
    [Test]
    public void DropUsesTheSameCanonicalCountdownShape()
    {
        var drop = new DropView(default, 7, 16, 2);

        Assert.That(PhraseEventView.Of(drop).Readout, Is.EqualTo("in 7b · len 16 · ×2"));
    }
}
