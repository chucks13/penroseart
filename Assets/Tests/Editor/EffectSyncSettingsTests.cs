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

