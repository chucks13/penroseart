// Seam-1 tests for the Levels doorway and the Color Bank (beat-data ticket 17): wire bytes in,
// three forms of one triple out.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;
using UnityEngine;

/// <summary>
/// Seam-1 tests for the Levels doorway and the Color Bank (beat-data ticket 17): real
/// <c>/rave/onair/levels</c> bytes in, the three forms of one triple out. Covers all-or-nothing
/// availability, shaping state dropping on lane loss so live samples snap in fresh, the Peak
/// drain against known tempos, the readings, and the Color Bank's classic wirings and knobs.
/// Expected values are hand-worked literals from the 0.2/0.4/0.6 worked example (Average 0.4,
/// Strongest 0.6 in the High band, Centroid 2/3, Dominance 2/3) — never re-run implementation math.
/// </summary>
public sealed class BeatManagerLevelsDoorwayTests
{
    // ---- Availability: all-or-nothing ---------------------------------------------------------

    /// <summary>Standalone Mode (no live OSC) serves no Levels at all.</summary>
    [Test]
    public void LevelsIsNullInStandaloneMode()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.Levels, Is.Null);
    }

    /// <summary>The wire's explicit -1/-1/-1 unavailable shape reads as a null doorway, never a negative triple.</summary>
    [Test]
    public void LevelsIsNullWhenTheWireSaysUnavailable()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, -1f, -1f, -1f));
        beatManager.Update(0f);

        Assert.That(beatManager.Levels, Is.Null);
    }

    /// <summary>A valid triple serves all three forms together; on the first live sample every form snaps to it.</summary>
    [Test]
    public void LevelsServesAllThreeFormsTogether()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 0.2f, 0.4f, 0.6f));
        beatManager.Update(0f);

        Assert.That(beatManager.Levels, Is.Not.Null);
        var levels = beatManager.Levels!.Value;
        Assert.That(levels.Normalized.Low, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(levels.Normalized.Mid, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(levels.Normalized.High, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(levels.Smoothed.Low, Is.EqualTo(0.2f).Within(0.0001f), "first sample snaps the follower in");
        Assert.That(levels.Smoothed.High, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(levels.Peak.Low, Is.EqualTo(0.2f).Within(0.0001f), "first sample snaps the peak in");
        Assert.That(levels.Peak.High, Is.EqualTo(0.6f).Within(0.0001f));
    }

    /// <summary>
    /// Lane loss drops the shaping state: the doorway reads null, and the next live samples snap
    /// in fresh — no release glide and no drain from the stale spike.
    /// </summary>
    [Test]
    public void LaneLossDropsShapingStateSoLiveSamplesSnapInFresh()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 1f, 1f, 1f));
        beatManager.Update(0f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, -1f, -1f, -1f));
        beatManager.Update(0.1f);
        Assert.That(beatManager.Levels, Is.Null);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 0.25f, 0.25f, 0.25f));
        beatManager.Update(0.2f);
        var levels = beatManager.Levels!.Value;
        Assert.That(levels.Smoothed.Low, Is.EqualTo(0.25f).Within(0.0001f), "no release from the stale 1.0");
        Assert.That(levels.Peak.Low, Is.EqualTo(0.25f).Within(0.0001f), "no drain from the stale 1.0");
    }

    // ---- Peak: instant rise, tempo-anchored linear drain --------------------------------------

    /// <summary>At 120 BPM (500 ms/beat) the peak drains the full scale in one beat: 0.25 s costs 0.5.</summary>
    [Test]
    public void PeakDrainsFullScalePerBeatAtLiveTempo()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 500);
            OnAirOscWriter.WriteLevels(ref bundle, 1f, 1f, 1f);
        });
        beatManager.Update(0f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 500);
            OnAirOscWriter.WriteLevels(ref bundle, 0f, 0f, 0f);
        });
        beatManager.Update(0.25f);

        Assert.That(beatManager.Levels!.Value.Peak.Low, Is.EqualTo(0.5f).Within(0.0001f));
    }

    /// <summary>The drain is anchored to the live tempo: at 60 BPM (1000 ms/beat) the same 0.25 s costs only 0.25.</summary>
    [Test]
    public void PeakDrainAnchorsToTheLiveTempo()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 1000);
            OnAirOscWriter.WriteLevels(ref bundle, 1f, 1f, 1f);
        });
        beatManager.Update(0f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 1000);
            OnAirOscWriter.WriteLevels(ref bundle, 0f, 0f, 0f);
        });
        beatManager.Update(0.25f);

        Assert.That(beatManager.Levels!.Value.Peak.Low, Is.EqualTo(0.75f).Within(0.0001f));
    }

    /// <summary>A sample above the draining value takes over instantly — rise is never smoothed.</summary>
    [Test]
    public void PeakRisesInstantlyAboveADrainingValue()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 500);
            OnAirOscWriter.WriteLevels(ref bundle, 1f, 1f, 1f);
        });
        beatManager.Update(0f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 500);
            OnAirOscWriter.WriteLevels(ref bundle, 0f, 0f, 0f);
        });
        beatManager.Update(0.25f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
        {
            WriteTempo(ref bundle, beatAverageMs: 500);
            OnAirOscWriter.WriteLevels(ref bundle, 0.9f, 0.9f, 0.9f);
        });
        beatManager.Update(0.3f);

        Assert.That(beatManager.Levels!.Value.Peak.Low, Is.EqualTo(0.9f).Within(0.0001f));
    }

    /// <summary>When levels flow with no usable tempo, the drain is the fixed 500 ms full scale, floored at 0.</summary>
    [Test]
    public void PeakDrainsFullScaleInFiveHundredMsWithoutTempo()
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 1f, 1f, 1f));
        beatManager.Update(0f);

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 0f, 0f, 0f));
        beatManager.Update(0.25f);
        Assert.That(beatManager.Levels!.Value.Peak.Low, Is.EqualTo(0.5f).Within(0.0001f));

        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, 0f, 0f, 0f));
        beatManager.Update(0.8f);
        Assert.That(beatManager.Levels!.Value.Peak.Low, Is.EqualTo(0f).Within(0.0001f), "the drain floors at 0");
    }

    // ---- The readings --------------------------------------------------------------------------

    /// <summary>The readings from the worked example, each an independently hand-worked literal.</summary>
    [Test]
    public void ReadingsComeFromTheWorkedExample()
    {
        var triple = WiredTriple(0.2f, 0.4f, 0.6f);

        Assert.That(triple.Average, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(triple.Strongest, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(triple.StrongestBand, Is.EqualTo(Band.High));
        // Spectral balance low→0, mid→0.5, high→1: (0.5·0.4 + 0.6) / 1.2 = 2/3.
        Assert.That(triple.Centroid, Is.EqualTo(2f / 3f).Within(0.0001f));
        // (0.6 − 0.2) / 0.6 = 2/3.
        Assert.That(triple.Dominance, Is.EqualTo(2f / 3f).Within(0.0001f));
    }

    /// <summary>StrongestBand tie-breaks deterministically: Low over Mid over High.</summary>
    [Test]
    public void StrongestBandTieBreaksLowOverMidOverHigh()
    {
        Assert.That(WiredTriple(0.5f, 0.5f, 0.5f).StrongestBand, Is.EqualTo(Band.Low));
        Assert.That(WiredTriple(0.2f, 0.5f, 0.5f).StrongestBand, Is.EqualTo(Band.Mid));
    }

    /// <summary>Silence reads as honest zeros: Centroid and Dominance are 0, never NaN.</summary>
    [Test]
    public void SilenceReadsZeroCentroidAndDominance()
    {
        var triple = WiredTriple(0f, 0f, 0f);

        Assert.That(triple.Centroid, Is.EqualTo(0f));
        Assert.That(triple.Dominance, Is.EqualTo(0f));
        Assert.That(triple.Strongest, Is.EqualTo(0f));
    }

    // ---- The Color Bank ------------------------------------------------------------------------

    /// <summary>No knobs turned = the classic RGB wiring: low→R, mid→G, high→B, alpha 1.</summary>
    [Test]
    public void RgbDefaultMapsBandsOntoChannels()
    {
        var color = WiredTriple(0.2f, 0.4f, 0.6f).Rgb();

        Assert.That(color.r, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(color.g, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(color.b, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(color.a, Is.EqualTo(1f));
    }

    /// <summary>No knobs turned = the classic HSV wiring: centroid→H, dominance→S, strongest→V.</summary>
    [Test]
    public void HsvDefaultMapsCentroidDominanceStrongest()
    {
        var color = WiredTriple(0.2f, 0.4f, 0.6f).Hsv();

        var expected = Color.HSVToRGB(2f / 3f, 2f / 3f, 0.6f);
        Assert.That(color.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(color.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(color.b, Is.EqualTo(expected.b).Within(0.0001f));
    }

    /// <summary>
    /// No knobs turned = the classic palette wiring: the caller's palette read at the centroid,
    /// RGB scaled by the strongest band, alpha preserved.
    /// </summary>
    [Test]
    public void FromPaletteReadsCentroidScaledByStrongest()
    {
        var palette = new AnimPalette();
        var expected = palette.read(2f / 3f);

        var color = WiredTriple(0.2f, 0.4f, 0.6f).FromPalette(palette);

        Assert.That(color.r, Is.EqualTo(expected.r * 0.6f).Within(0.0001f));
        Assert.That(color.g, Is.EqualTo(expected.g * 0.6f).Within(0.0001f));
        Assert.That(color.b, Is.EqualTo(expected.b * 0.6f).Within(0.0001f));
        Assert.That(color.a, Is.EqualTo(expected.a), "alpha is preserved, not scaled");
    }

    /// <summary>Knobs pick any band or reading for any component of any mapping.</summary>
    [Test]
    public void KnobsSelectBandsAndReadings()
    {
        var triple = WiredTriple(0.2f, 0.4f, 0.6f);

        var swapped = triple.Rgb(r: LevelSource.High, g: LevelSource.Low, b: LevelSource.Mid);
        Assert.That(swapped.r, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(swapped.g, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(swapped.b, Is.EqualTo(0.4f).Within(0.0001f));

        var readings = triple.Rgb(r: LevelSource.Average, b: LevelSource.Dominance);
        Assert.That(readings.r, Is.EqualTo(0.4f).Within(0.0001f), "Average drives red");
        Assert.That(readings.g, Is.EqualTo(0.4f).Within(0.0001f), "unturned knob keeps the classic mid wiring");
        Assert.That(readings.b, Is.EqualTo(2f / 3f).Within(0.0001f), "Dominance drives blue");
    }

    /// <summary>A float constant is a knob via implicit conversion: <c>Hsv(s: 0.7f)</c> pins saturation.</summary>
    [Test]
    public void KnobsAcceptFloatConstants()
    {
        var color = WiredTriple(0.2f, 0.4f, 0.6f).Hsv(s: 0.7f);

        var expected = Color.HSVToRGB(2f / 3f, 0.7f, 0.6f);
        Assert.That(color.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(color.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(color.b, Is.EqualTo(expected.b).Within(0.0001f));
    }

    // ---- Fixtures -----------------------------------------------------------------------------

    /// <summary>Writes lanes into one OSC bundle, so tests feed the wire contract's own bytes.</summary>
    private delegate void LaneWriter(ref OscBundleWriter bundle);

    /// <summary>
    /// Feeds real OSC bytes through the production parser into the manager, exactly as the live
    /// transport does — the seam-1 entry: wire in, doorways out.
    /// </summary>
    private static void FeedWire(BeatManager beatManager, LaneWriter writeLanes)
    {
        var buffer = new byte[1024];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
        writeLanes(ref bundle);
        var packet = System.MemoryExtensions.AsSpan(buffer, 0, bundle.Finish()).ToArray();

        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet);
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True, "the fed lanes must parse");
        beatManager.FeedWireSnapshot(snapshot);
    }

    /// <summary>Writes the tempo lanes a running clock carries: a live 4-count and the beat-interval yardstick.</summary>
    private static void WriteTempo(ref OscBundleWriter bundle, int beatAverageMs)
    {
        OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 1);
        OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_avg_ms", beatAverageMs);
    }

    /// <summary>Feeds one triple through the wire and returns its Normalized form — the seam-1 route to a triple.</summary>
    private static LevelsTriple WiredTriple(float low, float mid, float high)
    {
        var beatManager = new BeatManager();
        FeedWire(beatManager, (ref OscBundleWriter bundle) =>
            OnAirOscWriter.WriteLevels(ref bundle, low, mid, high));
        beatManager.Update(0f);
        return beatManager.Levels!.Value.Normalized;
    }
}
