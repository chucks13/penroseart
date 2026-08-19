// Contract tests for non-null normalized, smoothed, and peak Level values.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>Tests the always-present normalized, smoothed, and peak level groups.</summary>
public sealed class BeatManagerLevelsTests
{
    /// <summary>Every selectable Levels form serves its existing complete band-reading interface.</summary>
    [Test]
    public void EveryLevelFormExposesTheSameBandAndDerivedReadings()
    {
        var beatManager = ManagerWithLevels(0.2f, 0.5f, 0.8f, 0f);

        AssertBands(beatManager.Levels.Normalized);
        AssertBands(beatManager.Levels.Smoothed);
        AssertBands(beatManager.Levels.Peak);
        Assert.That(beatManager.Levels.Normalized.StrongestBand, Is.EqualTo(Band.High));
        Assert.That(beatManager.Levels.Normalized.Average, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(beatManager.Levels.Normalized.Centroid, Is.GreaterThan(0.5f));
        Assert.That(beatManager.Levels.Normalized.Dominance, Is.GreaterThan(0f));
    }

    /// <summary>The shared selector returns each existing Normalized, Smoothed, and Peak value.</summary>
    [Test]
    public void SelectReturnsTheRequestedExistingLevelForm()
    {
        var beatManager = ManagerWithLevels(1f, 0.5f, 0.25f, 0f);
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
            snapshot.levels = new Levels { low = 0f, mid = 0f, high = 0f });
        beatManager.Update(0.1f);

        Assert.That(
            beatManager.Levels.Select(LevelsForm.Normalized),
            Is.EqualTo(beatManager.Levels.Normalized));
        Assert.That(
            beatManager.Levels.Select(LevelsForm.Smoothed),
            Is.EqualTo(beatManager.Levels.Smoothed));
        Assert.That(
            beatManager.Levels.Select(LevelsForm.Peak),
            Is.EqualTo(beatManager.Levels.Peak));
    }

    /// <summary>Unavailable wire levels clear Normalized immediately without snapping shaped values.</summary>
    [Test]
    public void MissingWireLevelsBecomeNormalizedZeroWhileFollowersFall()
    {
        var beatManager = ManagerWithLevels(1f, 0.5f, 0.25f, 0f);
        BeatManagerWireFixture.Feed(beatManager, snapshot => snapshot.levels = Levels.Unavailable);
        beatManager.Update(0.05f);

        Assert.That(beatManager.Levels.Normalized.Average, Is.Zero);
        Assert.That(beatManager.Levels.Smoothed.Average, Is.GreaterThan(0f));
        Assert.That(beatManager.Levels.Peak.Average, Is.GreaterThan(0f));
    }

    /// <summary>Peak rises with its input and drains linearly over one synchronized beat.</summary>
    [Test]
    public void PeakRisesImmediatelyAndDrainsOverOneBeat()
    {
        var beatManager = ManagerWithLevels(1f, 0f, 0f, 0f);
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
            snapshot.levels = new Levels { low = 0f, mid = 0f, high = 0f });
        beatManager.Update(0.25f);
        Assert.That(beatManager.Levels.Peak.Low, Is.EqualTo(0.5f).Within(0.001f));

        beatManager.Update(0.5f);
        Assert.That(beatManager.Levels.Peak.Low, Is.Zero.Within(0.001f));
    }

    /// <summary>The musical Levels value does not acquire caller-owned color policy.</summary>
    [Test]
    public void LevelBandsDoNotContainColorPolicy()
    {
        Assert.That(typeof(LevelBands).GetMethod("Rgb"), Is.Null);
        Assert.That(typeof(LevelBands).GetMethod("Hsv"), Is.Null);
        Assert.That(typeof(LevelBands).GetMethod("FromPalette"), Is.Null);
    }

    /// <summary>Creates one synchronized BeatManager captured with the requested wire levels.</summary>
    private static BeatManager ManagerWithLevels(float low, float mid, float high, float time)
    {
        var beatManager = BeatClockFixture.CreateSeeded(120f, 0f);
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
            snapshot.levels = new Levels { low = low, mid = mid, high = high });
        beatManager.Update(time);
        return beatManager;
    }

    /// <summary>Asserts the complete caller-facing range contract for one band reading.</summary>
    private static void AssertBands(LevelBands bands)
    {
        Assert.That(bands.Low, Is.InRange(0f, 1f));
        Assert.That(bands.Mid, Is.InRange(0f, 1f));
        Assert.That(bands.High, Is.InRange(0f, 1f));
        Assert.That(bands.Average, Is.InRange(0f, 1f));
        Assert.That(bands.Strongest, Is.InRange(0f, 1f));
        Assert.That(bands.Centroid, Is.InRange(0f, 1f));
        Assert.That(bands.Dominance, Is.InRange(0f, 1f));
    }
}
