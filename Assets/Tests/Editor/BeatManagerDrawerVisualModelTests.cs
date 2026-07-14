#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Pins the pure BeatManager dashboard seam: authoritative mode, independent availability, required-Pool
/// health, responsive grouping, one-based placement, and timing glyph/readout helpers. Fill and Drop keep
/// their canonical presentation in <see cref="PhraseEventView"/> and its focused tests.
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

    /// <summary>An unresolved BeatManager renders Standalone labels and unavailable value rows.</summary>
    [Test]
    public void FromUsesStandaloneHeaderWhenBeatManagerIsUnavailable()
    {
        var model = BeatManagerDashboardModel.From(null, default, "");

        Assert.That(model.Synced, Is.False);
        Assert.That(model.BadgeText, Is.EqualTo("STANDALONE MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.HeaderRightText, Is.EqualTo("Unavailable"));
        Assert.That(model.Fill.HasValue, Is.False);
        Assert.That(model.Drop.HasValue, Is.False);
    }

    /// <summary>NEXT BEAT follows the next musical label instead of reusing the current label's zero-ms gate.</summary>
    [Test]
    public void FromUsesTheFollowingMusicalLabelForNextBeat()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            beatAverageMs = 400,
            beatsCountMs = new[] { 1200, 0, 400, 800 },
            onBeats = new[] { false, true, false, false },
        });
        beatManager.Update(0f);

        var model = BeatManagerDashboardModel.From(beatManager, Waveform.Parse("QQQQ", "8888"), "");

        Assert.That(model.NextBeat.Value, Is.EqualTo("400ms"));
    }

    /// <summary>A live beat count remains Synced even when every optional musical lane is unavailable.</summary>
    [Test]
    public void FromUsesIsSyncedAsTheOnlyModeAuthority()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot { beatInBar = 1 });

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.Synced, Is.True);
        Assert.That(model.BadgeText, Is.EqualTo("SYNCED MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.NextBeat.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeat, Is.Null);
        Assert.That(model.BeatPulse, Is.Zero);
        Assert.That(model.OffBeatPulse, Is.Zero);
        Assert.That(model.EighthPulse, Is.Zero);
        Assert.That(model.OffBeatGateAt(0), Is.Null);
        Assert.That(model.BarPhase, Is.Zero);
        Assert.That(model.CurrentPhrase.HasValue, Is.False);
        Assert.That(model.NextPhrase.HasValue, Is.False);
        Assert.That(model.Grid.HasValue, Is.False);
    }

    /// <summary>Standalone mode suppresses stale or default facts instead of presenting fabricated music.</summary>
    [Test]
    public void FromMakesEveryLiveFactUnavailableInStandaloneMode()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            bpm = 128f,
            track = "Stale Track",
            phraseState = new PhraseState { label = "Verse", countBeats = 8, lengthBeats = 16, irregular = 0 },
            nextPhraseState = new LabeledCountdown { label = "Drop", countBeats = 8, lengthBeats = 16 },
            timingGrid = new TimingGrid { state = "locked", beat = 6, bar = 2 },
        });
        beatManager.SetLiveBeatSource(false);
        beatManager.Update(1f);

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.BadgeText, Is.EqualTo("STANDALONE MODE"));
        Assert.That(model.TrackText, Is.EqualTo("Unavailable"));
        Assert.That(model.HeaderRightText, Is.EqualTo("Unavailable"));
        Assert.That(model.NextBeat.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeatGate.Value, Is.EqualTo("Unavailable"));
        Assert.That(model.OnBeat, Is.Null);
        Assert.That(model.BeatPulse, Is.Null);
        Assert.That(model.OffBeatPulse, Is.Null);
        Assert.That(model.EighthPulse, Is.Null);
        Assert.That(model.OffBeatGateAt(0), Is.Null);
        Assert.That(model.BarPhase, Is.Null);
        Assert.That(model.CurrentPhrase.HasValue, Is.False);
        Assert.That(model.NextPhrase.HasValue, Is.False);
        Assert.That(model.Grid.HasValue, Is.False);
        Assert.That(model.Levels.HasValue, Is.False);
    }

    /// <summary>Required Waveform Pool failures remain part of the dashboard's immutable health facts.</summary>
    [Test]
    public void FromPreservesRequiredWaveformPoolFailure()
    {
        const string error = "Required Waveform Pool 'waveforms.txt' contains no Waveforms.";

        var model = BeatManagerDashboardModel.From(null, default, error);
        var healthy = BeatManagerDashboardModel.From(null, Waveform.Parse("QQQQ", "8888"), "");

        Assert.That(model.PoolHealthy, Is.False);
        Assert.That(model.PoolError, Is.EqualTo(error));
        Assert.That(healthy.PoolHealthy, Is.True);
        Assert.That(healthy.PoolError, Is.Empty);
    }

    /// <summary>Dashboard groups expose the scan order and responsive flow used by the renderer.</summary>
    [Test]
    public void DashboardGroupsAndFlowDescribeNarrowAndWidePresentation()
    {
        var model = BeatManagerDashboardModel.From(null, default, "");

        Assert.That(model.Groups, Is.EqualTo(new[]
        {
            RhythmDashboardGroup.Timing,
            RhythmDashboardGroup.Waveform,
            RhythmDashboardGroup.Current,
            RhythmDashboardGroup.Next,
            RhythmDashboardGroup.Levels,
        }));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(560f), Is.EqualTo(RhythmDashboardFlow.Stacked));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(819f), Is.EqualTo(RhythmDashboardFlow.Stacked));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(820f), Is.EqualTo(RhythmDashboardFlow.Split));
        Assert.That(BeatManagerDashboardModel.FlowForWidth(900f), Is.EqualTo(RhythmDashboardFlow.Split));
    }

    /// <summary>Current Phrase, next Phrase, and Grid display their runtime-provided one-based musical counts.</summary>
    [Test]
    public void FromSeparatesCurrentAndNextPhraseAndKeepsGridCountsOneBased()
    {
        var beatManager = LiveManager(new RaveOnAirSnapshot
        {
            beatInBar = 2,
            bpm = 128f,
            phraseState = new PhraseState { label = "Intro", countBeats = 24, lengthBeats = 32, irregular = 0 },
            nextPhraseState = new LabeledCountdown { label = "Drop", countBeats = 24, lengthBeats = 32 },
            energyState = new LabeledCountdown { label = "Low", countBeats = 24, lengthBeats = 32 },
            nextEnergyState = new LabeledCountdown { label = "High", countBeats = 24, lengthBeats = 32 },
            timingGrid = new TimingGrid { state = "locked", beat = 5, bar = 2 },
        });

        var model = BeatManagerDashboardModel.From(beatManager, default, "");

        Assert.That(model.CurrentPhrase.Label, Is.EqualTo("Intro"));
        Assert.That(model.CurrentPhrase.Readout, Does.Contain("24b"));
        Assert.That(model.NextPhrase.Label, Is.EqualTo("Drop"));
        Assert.That(model.NextPhrase.Readout, Does.Contain("24b"));
        Assert.That(model.CurrentEnergy.Chip, Is.EqualTo("LOW"));
        Assert.That(model.NextEnergy.Chip, Is.EqualTo("HIGH"));
        Assert.That(model.Grid.Readout, Is.EqualTo("Bar 2 · Beat 5"));
    }

    /// <summary>Captures one live BeatManager frame from an exact OSC-shaped snapshot.</summary>
    private static BeatManager LiveManager(RaveOnAirSnapshot snapshot)
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }
}
