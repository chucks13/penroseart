#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins <see cref="BeatManagerDrawer"/>'s pure visual-model helpers: the beat-dot glyph row and the
/// combined eighth-note pulse. These are the only drawer-owned pieces with rendering-independent
/// logic; everything else reads the live BeatManager and is validated visually in the Inspector. (The
/// Fill/Drop phrase-event display model moved to <see cref="PhraseEventView"/>; see PhraseEventViewTests.)
/// </summary>
public sealed class BeatManagerDrawerVisualModelTests
{
    [Test]
    public void BuildBeatDotGlyphsUsesRaveSystemFilledDotsForMusicalBeatPosition()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: true, beatInBar: 3);

        Assert.That(glyphs, Is.EqualTo("●●●○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatClockIsUnavailable()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: false, beatInBar: -1);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatLabelIsOutOfRange()
    {
        var glyphs = BeatManagerDashboardModel.BuildBeatDotGlyphs(active: true, beatInBar: 7);

        Assert.That(glyphs, Is.EqualTo("○○○○"));
    }

    [Test]
    public void GetClampedEighthPulseValueUsesStrongerOnBeatOrOffBeatPulse()
    {
        var pulse = BeatManagerDashboardModel.GetClampedEighthPulseValue(0.25f, 1.25f);

        Assert.That(pulse, Is.EqualTo(1f));
    }

    [Test]
    public void FromUsesOfflineHeaderWhenBeatManagerIsUnavailable()
    {
        var model = BeatManagerDashboardModel.From(null, waveformIndex: 0);

        Assert.That(model.Active, Is.False);
        Assert.That(model.BadgeText, Is.EqualTo("OFFLINE"));
        Assert.That(model.TrackText, Is.EqualTo("—"));
        Assert.That(model.HeaderRightText, Is.EqualTo("-- BPM"));
    }
}
