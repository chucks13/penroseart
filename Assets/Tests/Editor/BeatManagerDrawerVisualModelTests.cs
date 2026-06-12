#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins <see cref="BeatManagerDrawer"/>'s pure visual-model helpers: the beat-dot glyph row, the
/// combined eighth-note pulse, and the Fill/Drop phrase-event chip/meter/readout model. These are the
/// only drawer pieces with rendering-independent logic; everything else reads the live BeatManager and
/// is validated visually in the Inspector.
/// </summary>
public sealed class BeatManagerDrawerVisualModelTests
{
    [Test]
    public void BuildBeatDotGlyphsUsesRaveSystemFilledDotsForMusicalBeatPosition()
    {
        var glyphs = BeatManagerDrawer.BuildBeatDotGlyphs(active: true, beatInBar: 3);

        Assert.That(glyphs, Is.EqualTo("●●●○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatClockIsUnavailable()
    {
        var glyphs = BeatManagerDrawer.BuildBeatDotGlyphs(active: false, beatInBar: -1);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatLabelIsOutOfRange()
    {
        var glyphs = BeatManagerDrawer.BuildBeatDotGlyphs(active: true, beatInBar: 7);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void GetClampedEighthPulseValueUsesStrongerOnBeatOrOffBeatPulse()
    {
        var pulse = BeatManagerDrawer.GetClampedEighthPulseValue(0.25f, 1.25f);

        Assert.That(pulse, Is.EqualTo(1f));
    }

    // --- Fill / Drop phrase-event row model ---

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
    public void PhraseEventChipSaysNowInProgressCountdownWhileUpcomingAndNullOtherwise()
    {
        Assert.That(BeatManagerDrawer.BuildPhraseEventChipLabel(InProgressEvent), Is.EqualTo("NOW"));
        Assert.That(BeatManagerDrawer.BuildPhraseEventChipLabel(UpcomingEvent), Is.EqualTo("IN 7"));
        Assert.That(BeatManagerDrawer.BuildPhraseEventChipLabel(NoUpcomingEvent), Is.EqualTo("—"));
    }

    [Test]
    public void PhraseEventMeterSweepsProgressInProgressAndFillsWithAnticipationWhileCountingDown()
    {
        Assert.That(BeatManagerDrawer.GetPhraseEventMeterValue(InProgressEvent), Is.EqualTo(0.4375f));
        Assert.That(BeatManagerDrawer.GetPhraseEventMeterValue(UpcomingEvent), Is.EqualTo(0.796875f));
        Assert.That(BeatManagerDrawer.GetPhraseEventMeterValue(NoUpcomingEvent), Is.EqualTo(0f));
    }

    [Test]
    public void PhraseEventReadoutShowsNullAsNullAndZeroAsZero()
    {
        Assert.That(BeatManagerDrawer.BuildPhraseEventReadout(InProgressEvent), Is.EqualTo("ends in 9b · len 16 · ×1"));
        Assert.That(BeatManagerDrawer.BuildPhraseEventReadout(UpcomingEvent), Is.EqualTo("in 3.5s · len 16 · ×2"));
        Assert.That(BeatManagerDrawer.BuildPhraseEventReadout(NoUpcomingEvent), Is.EqualTo("next — · ×0"));
    }

    [Test]
    public void PhraseEventReadoutFallsBackToBeatsWhenNoBeatIntervalIsAvailable()
    {
        var noMs = new PhraseEventInfo(
            inProgress: false, beatsUntilStart: 7, msUntilStart: null, beatsUntilEnd: null,
            progress: null, anticipation: 0.796875f, lengthBeats: 16, remaining: 2);

        Assert.That(BeatManagerDrawer.BuildPhraseEventReadout(noMs), Is.EqualTo("in 7b · len 16 · ×2"));
    }
}
