// Verifies the agreed Effect Sync Settings seam: saved-or-default resolution and restoring Sync Defaults.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Resolution and restore tests for typed per-Effect Sync Settings.</summary>
public sealed class EffectSyncSettingsTests
{
    /// <summary>Temporary asset folder kept outside Resources for restore tests.</summary>
    private const string TempAssetFolder = "Assets/Tests/Editor/TempEffectSyncSettings";

    /// <summary>Temporary root containing an isolated Resources folder for resolution tests.</summary>
    private const string TempResourcesRoot = "Assets/Tests/Editor/TempEffectSyncSettingsResources";

    /// <summary>Temporary folder whose Resources-relative path matches the runtime provider.</summary>
    private const string TempResourcesFolder =
        TempResourcesRoot + "/Resources/" + EffectSyncSettingsProvider.ResourceFolder;

    /// <summary>Removes leftovers so each test starts with no saved test asset.</summary>
    [SetUp]
    public void SetUp()
    {
        CleanupTempAssets();
    }

    /// <summary>Removes temporary assets so resolution state cannot leak into another test.</summary>
    [TearDown]
    public void TearDown()
    {
        CleanupTempAssets();
    }

    /// <summary>Without a saved asset, resolution returns the exact supplied Sync Defaults object.</summary>
    [Test]
    public void ResolveWithoutAssetReturnsSyncDefaults()
    {
        var syncDefaults = TestSettingsEffect.SyncDefaults;

        var resolved = EffectSyncSettingsProvider.Resolve<TestEffectSyncSettings>(
            typeof(TestSettingsEffect),
            syncDefaults);

        Assert.That(resolved, Is.SameAs(syncDefaults));
    }

    /// <summary>With a saved Resources asset, resolution returns its live editable Sync Settings object.</summary>
    [Test]
    public void ResolveWithAssetReturnsSavedSyncSettings()
    {
        var asset = (TestEffectSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(TestSettingsEffect),
            TempResourcesFolder);
        asset.Settings.Amount = 9f;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var resolved = EffectSyncSettingsProvider.Resolve<TestEffectSyncSettings>(
            typeof(TestSettingsEffect),
            TestSettingsEffect.SyncDefaults);

        Assert.That(resolved, Is.SameAs(asset.Settings));
        Assert.That(resolved.Amount, Is.EqualTo(9f));
    }

    /// <summary>Standalone Settings resolve as fresh, mutually independent copies of the Standalone
    /// Defaults. The authored values themselves are deliberately not pinned here: they are the
    /// Standalone look, which ADR-0012 says is judged on the wall, not asserted in tests.</summary>
    [Test]
    public void TunnelStandaloneSettingsResolveToStandaloneDefaults()
    {
        var first = Tunnel.StandaloneSettings;
        var second = Tunnel.StandaloneSettings;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Density.Min, Is.EqualTo(second.Density.Min));
        Assert.That(first.Density.Max, Is.EqualTo(second.Density.Max));
        Assert.That(first.Speed.Min, Is.EqualTo(second.Speed.Min));
        Assert.That(first.Speed.Max, Is.EqualTo(second.Speed.Max));
        Assert.That(first.Mix.Min, Is.EqualTo(second.Mix.Min));
        Assert.That(first.Mix.Max, Is.EqualTo(second.Mix.Max));
        Assert.That(first.CenterScale, Is.EqualTo(second.CenterScale));
    }

    /// <summary>Ripple Standalone Settings resolve as fresh copies without pinning authored values.</summary>
    [Test]
    public void RippleStandaloneSettingsResolveToStandaloneDefaults()
    {
        var first = Ripple.StandaloneSettings;
        var second = Ripple.StandaloneSettings;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Intensity.Min, Is.EqualTo(second.Intensity.Min));
        Assert.That(first.Intensity.Max, Is.EqualTo(second.Intensity.Max));
        Assert.That(first.Velocity.Min, Is.EqualTo(second.Velocity.Min));
        Assert.That(first.Velocity.Max, Is.EqualTo(second.Velocity.Max));
        Assert.That(first.VelocityDivisor, Is.EqualTo(second.VelocityDivisor));
        Assert.That(first.DistanceDivisor, Is.EqualTo(second.DistanceDivisor));
        Assert.That(first.PaletteOffset, Is.EqualTo(second.PaletteOffset));
        Assert.That(first.HueShift, Is.EqualTo(second.HueShift));
    }

    /// <summary>Crystal Growth Standalone Settings resolve as fresh copies of its scalar and machinery defaults.</summary>
    [Test]
    public void CrystalGrowthStandaloneSettingsResolveToStandaloneDefaults()
    {
        var first = CrystalGrowth.StandaloneSettings;
        var second = CrystalGrowth.StandaloneSettings;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.HeatEpsilon, Is.EqualTo(second.HeatEpsilon));
        Assert.That(first.FrontPush, Is.EqualTo(second.FrontPush));
        Assert.That(first.CoverageToAdvance, Is.EqualTo(second.CoverageToAdvance));
        Assert.That(first.MaxFrontPassesPerFrame, Is.EqualTo(second.MaxFrontPassesPerFrame));
        Assert.That(first.CrystalFloor, Is.EqualTo(second.CrystalFloor));
        Assert.That(first.HueRelaxPerSec, Is.EqualTo(second.HueRelaxPerSec));
        Assert.That(first.HueRelaxMaxPerFrame, Is.EqualTo(second.HueRelaxMaxPerFrame));
        Assert.That(first.QuietEnergy, Is.EqualTo(second.QuietEnergy));
        Assert.That(first.ActiveEnergy, Is.EqualTo(second.ActiveEnergy));
        Assert.That(first.GoldenStep, Is.EqualTo(second.GoldenStep));
        Assert.That(first.SpreadPerSec.Min, Is.EqualTo(second.SpreadPerSec.Min));
        Assert.That(first.SpreadPerSec.Max, Is.EqualTo(second.SpreadPerSec.Max));
        Assert.That(first.LeakPerSec.Min, Is.EqualTo(second.LeakPerSec.Min));
        Assert.That(first.LeakPerSec.Max, Is.EqualTo(second.LeakPerSec.Max));
        Assert.That(first.BeatSurge.Min, Is.EqualTo(second.BeatSurge.Min));
        Assert.That(first.BeatSurge.Max, Is.EqualTo(second.BeatSurge.Max));
        Assert.That(first.SeedInterval.Min, Is.EqualTo(second.SeedInterval.Min));
        Assert.That(first.SeedInterval.Max, Is.EqualTo(second.SeedInterval.Max));
        Assert.That(first.SelfBeatPeriod.Min, Is.EqualTo(second.SelfBeatPeriod.Min));
        Assert.That(first.SelfBeatPeriod.Max, Is.EqualTo(second.SelfBeatPeriod.Max));
        Assert.That(first.SelfPulseDecayPerSec, Is.EqualTo(second.SelfPulseDecayPerSec));
        Assert.That(first.TipThreshold, Is.EqualTo(second.TipThreshold));
        Assert.That(first.TipWhitenAmount, Is.EqualTo(second.TipWhitenAmount));
        Assert.That(first.BloomCountBase, Is.EqualTo(second.BloomCountBase));
        Assert.That(first.BloomCountOffsetMinInclusive, Is.EqualTo(second.BloomCountOffsetMinInclusive));
        Assert.That(first.BloomCountOffsetMaxExclusive, Is.EqualTo(second.BloomCountOffsetMaxExclusive));
    }

    /// <summary>Restore replaces every edited Tunnel Sync Setting with the current file-local Sync Defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryTunnelValue()
    {
        var asset = (TunnelSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Tunnel),
            TempAssetFolder);
        asset.Settings.WaveformEnergyOne = Energy.High;
        asset.Settings.WaveformEnergyTwo = Energy.High;
        asset.Settings.FillRush = 17f;
        asset.Settings.FillZoom = 18f;
        asset.Settings.BeatBrightnessFloor = 0.1f;
        asset.Settings.DropBars = 7;
        asset.Settings.DropRush = 19f;
        asset.Settings.DropZoom = 20f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Tunnel), TempAssetFolder);

        var defaults = Tunnel.SyncDefaults;
        Assert.That(asset.Settings.WaveformEnergyOne, Is.EqualTo(defaults.WaveformEnergyOne));
        Assert.That(asset.Settings.WaveformEnergyTwo, Is.EqualTo(defaults.WaveformEnergyTwo));
        Assert.That(asset.Settings.FillRush, Is.EqualTo(defaults.FillRush));
        Assert.That(asset.Settings.FillZoom, Is.EqualTo(defaults.FillZoom));
        Assert.That(asset.Settings.BeatBrightnessFloor, Is.EqualTo(defaults.BeatBrightnessFloor));
        Assert.That(asset.Settings.DropBars, Is.EqualTo(defaults.DropBars));
        Assert.That(asset.Settings.DropRush, Is.EqualTo(defaults.DropRush));
        Assert.That(asset.Settings.DropZoom, Is.EqualTo(defaults.DropZoom));
    }

    /// <summary>Restore replaces every edited Ripple Sync Setting with the current file-local Sync Defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryRippleValue()
    {
        var asset = (RippleSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Ripple),
            TempAssetFolder);
        asset.Settings.HueShiftMax = 0.9f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Ripple), TempAssetFolder);

        var defaults = Ripple.SyncDefaults;
        Assert.That(asset.Settings.HueShiftMax, Is.EqualTo(defaults.HueShiftMax));
    }

    /// <summary>Restore replaces every edited Crystal Growth Sync Setting with its file-local Sync Default.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryCrystalGrowthValue()
    {
        var asset = (CrystalGrowthSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(CrystalGrowth),
            TempAssetFolder);
        asset.Settings.KickThreshold = 0.9f;
        asset.Settings.QuietEnergy = 0.1f;
        asset.Settings.ActiveEnergy = 0.8f;
        asset.Settings.DropFadeBars = 7;
        asset.Settings.DropFlashBrightness = 4f;
        asset.Settings.DropFlashSpread = 5f;
        asset.Settings.DropFlashSeeds = 6;
        asset.Settings.IdleSeedIntervalMin = 1.1f;
        asset.Settings.IdleSeedIntervalMax = 1.8f;
        asset.Settings.DropRatchetSpread = 8f;
        asset.Settings.DropStrobeDepth = 0.2f;
        asset.Settings.DropSeedBurst = 9;
        asset.Settings.DropSeedBurstThreshold = 0.7f;
        asset.Settings.FillHoldback = 0.3f;
        asset.Settings.FillSwell = 1.4f;
        asset.Settings.DrivingBrightnessFloor = 0.4f;
        asset.Settings.KickBurstMin = 4f;
        asset.Settings.KickBurstMax = 10f;
        asset.Settings.DownbeatSeedBonus = 5;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(CrystalGrowth), TempAssetFolder);

        var defaults = CrystalGrowth.SyncDefaults;
        Assert.That(asset.Settings.KickThreshold, Is.EqualTo(defaults.KickThreshold));
        Assert.That(asset.Settings.QuietEnergy, Is.EqualTo(defaults.QuietEnergy));
        Assert.That(asset.Settings.ActiveEnergy, Is.EqualTo(defaults.ActiveEnergy));
        Assert.That(asset.Settings.DropFadeBars, Is.EqualTo(defaults.DropFadeBars));
        Assert.That(asset.Settings.DropFlashBrightness, Is.EqualTo(defaults.DropFlashBrightness));
        Assert.That(asset.Settings.DropFlashSpread, Is.EqualTo(defaults.DropFlashSpread));
        Assert.That(asset.Settings.DropFlashSeeds, Is.EqualTo(defaults.DropFlashSeeds));
        Assert.That(asset.Settings.IdleSeedIntervalMin, Is.EqualTo(defaults.IdleSeedIntervalMin));
        Assert.That(asset.Settings.IdleSeedIntervalMax, Is.EqualTo(defaults.IdleSeedIntervalMax));
        Assert.That(asset.Settings.DropRatchetSpread, Is.EqualTo(defaults.DropRatchetSpread));
        Assert.That(asset.Settings.DropStrobeDepth, Is.EqualTo(defaults.DropStrobeDepth));
        Assert.That(asset.Settings.DropSeedBurst, Is.EqualTo(defaults.DropSeedBurst));
        Assert.That(asset.Settings.DropSeedBurstThreshold, Is.EqualTo(defaults.DropSeedBurstThreshold));
        Assert.That(asset.Settings.FillHoldback, Is.EqualTo(defaults.FillHoldback));
        Assert.That(asset.Settings.FillSwell, Is.EqualTo(defaults.FillSwell));
        Assert.That(asset.Settings.DrivingBrightnessFloor, Is.EqualTo(defaults.DrivingBrightnessFloor));
        Assert.That(asset.Settings.KickBurstMin, Is.EqualTo(defaults.KickBurstMin));
        Assert.That(asset.Settings.KickBurstMax, Is.EqualTo(defaults.KickBurstMax));
        Assert.That(asset.Settings.DownbeatSeedBonus, Is.EqualTo(defaults.DownbeatSeedBonus));
    }

    /// <summary>Deletes the temporary test roots through Unity so generated assets and metadata stay paired.</summary>
    private static void CleanupTempAssets()
    {
        AssetDatabase.DeleteAsset(TempAssetFolder);
        AssetDatabase.DeleteAsset(TempResourcesRoot);
        AssetDatabase.Refresh();
    }
}

/// <summary>Minimal typed Sync Settings used to isolate provider resolution from real Tunnel assets.</summary>
[Serializable]
public sealed class TestEffectSyncSettings
{
    /// <summary>Single editable value proving which settings object resolution returned.</summary>
    public float Amount;

    /// <summary>Copies the complete test settings value.</summary>
    public void CopyFrom(TestEffectSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Amount = source.Amount;
    }
}

/// <summary>Test-only Effect declaring a typed Sync Settings asset without joining the runtime catalog.</summary>
[RuntimeCatalogIgnore]
[EffectSyncSettings(typeof(TestEffectSyncSettingsAsset))]
public sealed class TestSettingsEffect : EffectBase
{
    /// <summary>Returns a fresh test Sync Defaults object.</summary>
    public static TestEffectSyncSettings SyncDefaults => new TestEffectSyncSettings { Amount = 2f };

    /// <summary>Returns no runtime debug text because this test Effect never renders.</summary>
    public override string DebugText() => string.Empty;

    /// <summary>Reserved test deactivation hook.</summary>
    public override void OnEnd()
    {
    }

    /// <summary>Does not render; the test exercises only settings resolution.</summary>
    public override void Draw()
    {
    }
}
