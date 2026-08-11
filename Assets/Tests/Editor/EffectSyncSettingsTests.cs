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

    /// <summary>Standalone Defaults resolve as fresh, mutually independent copies. The authored
    /// values themselves are deliberately not pinned here: they are the Standalone look, which
    /// ADR-0013 says is judged on the wall, not asserted in tests.</summary>
    [Test]
    public void TunnelStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Tunnel.StandaloneDefaults;
        var second = Tunnel.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.TileIndexPhaseStep, Is.Not.SameAs(second.TileIndexPhaseStep));
        Assert.That(first.TileIndexPhaseStep.Min, Is.EqualTo(second.TileIndexPhaseStep.Min));
        Assert.That(first.TileIndexPhaseStep.Max, Is.EqualTo(second.TileIndexPhaseStep.Max));
        Assert.That(first.ScrollSpeed, Is.Not.SameAs(second.ScrollSpeed));
        Assert.That(first.ScrollSpeed.Min, Is.EqualTo(second.ScrollSpeed.Min));
        Assert.That(first.ScrollSpeed.Max, Is.EqualTo(second.ScrollSpeed.Max));
        Assert.That(first.RadialMix, Is.Not.SameAs(second.RadialMix));
        Assert.That(first.RadialMix.Min, Is.EqualTo(second.RadialMix.Min));
        Assert.That(first.RadialMix.Max, Is.EqualTo(second.RadialMix.Max));
        Assert.That(first.CenterScale, Is.EqualTo(second.CenterScale));
        AssertPaletteConditioningEqual(first.PaletteConditioning, second.PaletteConditioning);
    }

    /// <summary>
    /// Restore replaces every edited Tunnel Standalone Setting and Rail with the current file-local
    /// Standalone Defaults, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryTunnelValue()
    {
        var asset = (TunnelStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Tunnel),
            TempAssetFolder);
        asset.Settings.TileIndexPhaseStep = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.ScrollSpeed = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.RadialMix = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.CenterScale = 26f;
        asset.Settings.PaletteConditioning = new PaletteConditioning
        {
            TargetLuminance = 0.11f,
            MinimumLuminance = 0.12f,
            LuminanceEqualization = 0.13f,
            HueSpreadReference = 0.14f,
            MaximumLuminanceScale = 2f,
            DarkLuminanceThreshold = 0.15f,
            DuplicateThreshold = 0.16f,
            HueRedistribution = 0.17f,
        };

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Tunnel), TempAssetFolder);

        var defaults = Tunnel.StandaloneDefaults;
        Assert.That(asset.Settings.TileIndexPhaseStep.Min, Is.EqualTo(defaults.TileIndexPhaseStep.Min));
        Assert.That(asset.Settings.TileIndexPhaseStep.Max, Is.EqualTo(defaults.TileIndexPhaseStep.Max));
        Assert.That(asset.Settings.TileIndexPhaseStep.LowRail, Is.EqualTo(defaults.TileIndexPhaseStep.LowRail));
        Assert.That(asset.Settings.TileIndexPhaseStep.HighRail, Is.EqualTo(defaults.TileIndexPhaseStep.HighRail));
        Assert.That(asset.Settings.ScrollSpeed.Min, Is.EqualTo(defaults.ScrollSpeed.Min));
        Assert.That(asset.Settings.ScrollSpeed.Max, Is.EqualTo(defaults.ScrollSpeed.Max));
        Assert.That(asset.Settings.ScrollSpeed.LowRail, Is.EqualTo(defaults.ScrollSpeed.LowRail));
        Assert.That(asset.Settings.ScrollSpeed.HighRail, Is.EqualTo(defaults.ScrollSpeed.HighRail));
        Assert.That(asset.Settings.RadialMix.Min, Is.EqualTo(defaults.RadialMix.Min));
        Assert.That(asset.Settings.RadialMix.Max, Is.EqualTo(defaults.RadialMix.Max));
        Assert.That(asset.Settings.RadialMix.LowRail, Is.EqualTo(defaults.RadialMix.LowRail));
        Assert.That(asset.Settings.RadialMix.HighRail, Is.EqualTo(defaults.RadialMix.HighRail));
        Assert.That(asset.Settings.CenterScale, Is.EqualTo(defaults.CenterScale));
        AssertPaletteConditioningEqual(
            asset.Settings.PaletteConditioning,
            defaults.PaletteConditioning);
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
        asset.Settings.TileIndexPhaseStep = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.ScrollSpeed = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.RadialMix = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.CenterScale = 26f;
        asset.Settings.PaletteConditioning = new PaletteConditioning
        {
            TargetLuminance = 0.21f,
            MinimumLuminance = 0.22f,
            LuminanceEqualization = 0.23f,
            HueSpreadReference = 0.24f,
            MaximumLuminanceScale = 3f,
            DarkLuminanceThreshold = 0.2f,
            DuplicateThreshold = 0.18f,
            HueRedistribution = 0.19f,
        };
        asset.Settings.WaveformEnergyOne = Energy.High;
        asset.Settings.WaveformEnergyTwo = Energy.High;
        asset.Settings.FillScrollRateMultiplier = 17f;
        asset.Settings.FillRingCompression = 18f;
        asset.Settings.BeatBrightnessFloor = 0.1f;
        asset.Settings.DropBars = 7;
        asset.Settings.DropReverseScrollRateMultiplier = 19f;
        asset.Settings.DropRingCompression = 20f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Tunnel), TempAssetFolder);

        var defaults = Tunnel.SyncDefaults;
        Assert.That(asset.Settings.TileIndexPhaseStep.Min, Is.EqualTo(defaults.TileIndexPhaseStep.Min));
        Assert.That(asset.Settings.TileIndexPhaseStep.Max, Is.EqualTo(defaults.TileIndexPhaseStep.Max));
        Assert.That(asset.Settings.TileIndexPhaseStep.LowRail, Is.EqualTo(defaults.TileIndexPhaseStep.LowRail));
        Assert.That(asset.Settings.TileIndexPhaseStep.HighRail, Is.EqualTo(defaults.TileIndexPhaseStep.HighRail));
        Assert.That(asset.Settings.ScrollSpeed.Min, Is.EqualTo(defaults.ScrollSpeed.Min));
        Assert.That(asset.Settings.ScrollSpeed.Max, Is.EqualTo(defaults.ScrollSpeed.Max));
        Assert.That(asset.Settings.ScrollSpeed.LowRail, Is.EqualTo(defaults.ScrollSpeed.LowRail));
        Assert.That(asset.Settings.ScrollSpeed.HighRail, Is.EqualTo(defaults.ScrollSpeed.HighRail));
        Assert.That(asset.Settings.RadialMix.Min, Is.EqualTo(defaults.RadialMix.Min));
        Assert.That(asset.Settings.RadialMix.Max, Is.EqualTo(defaults.RadialMix.Max));
        Assert.That(asset.Settings.RadialMix.LowRail, Is.EqualTo(defaults.RadialMix.LowRail));
        Assert.That(asset.Settings.RadialMix.HighRail, Is.EqualTo(defaults.RadialMix.HighRail));
        Assert.That(asset.Settings.CenterScale, Is.EqualTo(defaults.CenterScale));
        AssertPaletteConditioningEqual(
            asset.Settings.PaletteConditioning,
            defaults.PaletteConditioning);
        Assert.That(asset.Settings.WaveformEnergyOne, Is.EqualTo(defaults.WaveformEnergyOne));
        Assert.That(asset.Settings.WaveformEnergyTwo, Is.EqualTo(defaults.WaveformEnergyTwo));
        Assert.That(asset.Settings.FillScrollRateMultiplier, Is.EqualTo(defaults.FillScrollRateMultiplier));
        Assert.That(asset.Settings.FillRingCompression, Is.EqualTo(defaults.FillRingCompression));
        Assert.That(asset.Settings.BeatBrightnessFloor, Is.EqualTo(defaults.BeatBrightnessFloor));
        Assert.That(asset.Settings.DropBars, Is.EqualTo(defaults.DropBars));
        Assert.That(asset.Settings.DropReverseScrollRateMultiplier, Is.EqualTo(defaults.DropReverseScrollRateMultiplier));
        Assert.That(asset.Settings.DropRingCompression, Is.EqualTo(defaults.DropRingCompression));
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

    /// <summary>
    /// Asserts that every Palette Conditioning control was copied without pinning any Effect's
    /// authored tuning value.
    /// </summary>
    /// <param name="actual">The restored or independently resolved live settings value.</param>
    /// <param name="expected">The current file-local defaults value.</param>
    private static void AssertPaletteConditioningEqual(
        PaletteConditioning actual,
        PaletteConditioning expected)
    {
        Assert.That(actual.TargetLuminance, Is.EqualTo(expected.TargetLuminance));
        Assert.That(actual.MinimumLuminance, Is.EqualTo(expected.MinimumLuminance));
        Assert.That(actual.LuminanceEqualization, Is.EqualTo(expected.LuminanceEqualization));
        Assert.That(actual.HueSpreadReference, Is.EqualTo(expected.HueSpreadReference));
        Assert.That(actual.MaximumLuminanceScale, Is.EqualTo(expected.MaximumLuminanceScale));
        Assert.That(actual.DarkLuminanceThreshold, Is.EqualTo(expected.DarkLuminanceThreshold));
        Assert.That(actual.DuplicateThreshold, Is.EqualTo(expected.DuplicateThreshold));
        Assert.That(actual.HueRedistribution, Is.EqualTo(expected.HueRedistribution));
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
