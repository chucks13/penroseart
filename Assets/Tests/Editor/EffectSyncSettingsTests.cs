// Verifies the Effect Settings seam: independent defaults, saved resolution, and restore behavior.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Resolution, independence, and restore tests for typed per-Effect settings.</summary>
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

        var resolved = EffectSyncSettingsProvider.Resolve(
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

        var resolved = EffectSyncSettingsProvider.Resolve(
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

    /// <summary>
    /// Noise Standalone Defaults resolve as fresh, mutually independent settings and ranges without
    /// pinning the authored look that ADR-0013 leaves to judgment on the wall.
    /// </summary>
    [Test]
    public void NoiseStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Noise.StandaloneDefaults;
        var second = Noise.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.TileCenterScale, Is.Not.SameAs(second.TileCenterScale));
        AssertFloatRangeEqual(first.TileCenterScale, second.TileCenterScale);
        Assert.That(first.NoiseFieldDriftSpeed, Is.Not.SameAs(second.NoiseFieldDriftSpeed));
        AssertFloatRangeEqual(first.NoiseFieldDriftSpeed, second.NoiseFieldDriftSpeed);
        Assert.That(first.PerlinAmplitude, Is.Not.SameAs(second.PerlinAmplitude));
        AssertFloatRangeEqual(first.PerlinAmplitude, second.PerlinAmplitude);
        Assert.That(first.WaveformResponseMode, Is.Not.SameAs(second.WaveformResponseMode));
        AssertIntRangeEqual(first.WaveformResponseMode, second.WaveformResponseMode);
        Assert.That(first.Brightness, Is.Not.SameAs(second.Brightness));
        AssertFloatRangeEqual(first.Brightness, second.Brightness);
    }

    /// <summary>
    /// Restore replaces every edited Noise Standalone Setting and Rail with the current file-local
    /// Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryNoiseValue()
    {
        var asset = (NoiseStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Noise),
            TempAssetFolder);
        asset.Settings.TileCenterScale = new FloatRange(10f, 11f, 9f, 12f);
        asset.Settings.NoiseFieldDriftSpeed = new FloatRange(13f, 14f, 12f, 15f);
        asset.Settings.PerlinAmplitude = new FloatRange(16f, 17f, 15f, 18f);
        asset.Settings.WaveformResponseMode = new IntRange(19, 20, 18, 21);
        asset.Settings.Brightness = new FloatRange(0.22f, 0.23f, 0.21f, 0.24f);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(Noise),
            TempAssetFolder);

        var defaults = Noise.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.TileCenterScale, defaults.TileCenterScale);
        AssertFloatRangeEqual(asset.Settings.NoiseFieldDriftSpeed, defaults.NoiseFieldDriftSpeed);
        AssertFloatRangeEqual(asset.Settings.PerlinAmplitude, defaults.PerlinAmplitude);
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
    }

    /// <summary>
    /// Fluid Standalone Defaults resolve as fresh independent values without pinning the authored
    /// look that ADR-0013 leaves to judgment on the wall.
    /// </summary>
    [Test]
    public void FluidStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Fluid.StandaloneDefaults;
        var second = Fluid.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Damping, Is.EqualTo(second.Damping));
        Assert.That(first.Impulse, Is.EqualTo(second.Impulse));
        Assert.That(
            first.InjectionChanceDenominator,
            Is.EqualTo(second.InjectionChanceDenominator));
        Assert.That(first.PaletteScale, Is.EqualTo(second.PaletteScale));
        Assert.That(first.NeighborWeight, Is.EqualTo(second.NeighborWeight));
        Assert.That(first.PaletteOffset, Is.EqualTo(second.PaletteOffset));
    }

    /// <summary>
    /// Restore replaces every edited Fluid Standalone Setting with the current file-local
    /// Standalone Defaults without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryFluidValue()
    {
        var asset = (FluidStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Fluid),
            TempAssetFolder);
        asset.Settings.Damping = 0.11f;
        asset.Settings.Impulse = 12f;
        asset.Settings.InjectionChanceDenominator = 13;
        asset.Settings.PaletteScale = 14f;
        asset.Settings.NeighborWeight = 15f;
        asset.Settings.PaletteOffset = 0.16f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Fluid), TempAssetFolder);

        var defaults = Fluid.StandaloneDefaults;
        Assert.That(asset.Settings.Damping, Is.EqualTo(defaults.Damping));
        Assert.That(asset.Settings.Impulse, Is.EqualTo(defaults.Impulse));
        Assert.That(
            asset.Settings.InjectionChanceDenominator,
            Is.EqualTo(defaults.InjectionChanceDenominator));
        Assert.That(asset.Settings.PaletteScale, Is.EqualTo(defaults.PaletteScale));
        Assert.That(asset.Settings.NeighborWeight, Is.EqualTo(defaults.NeighborWeight));
        Assert.That(asset.Settings.PaletteOffset, Is.EqualTo(defaults.PaletteOffset));
    }

    /// <summary>
    /// Mirror Standalone Defaults resolve as fresh copies whose layout ranges do not share mutable
    /// state, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void MirrorStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Mirror.StandaloneDefaults;
        var second = Mirror.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.MirrorLayout, Is.Not.SameAs(second.MirrorLayout));
        AssertIntRangeEqual(first.MirrorLayout, second.MirrorLayout);
    }

    /// <summary>
    /// Restore replaces Mirror's edited Standalone layout endpoints and Rails with the current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryMirrorValue()
    {
        var asset = (MirrorStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Mirror),
            TempAssetFolder);
        asset.Settings.MirrorLayout = new IntRange(17, 18, 16, 19);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Mirror), TempAssetFolder);

        AssertIntRangeEqual(asset.Settings.MirrorLayout, Mirror.StandaloneDefaults.MirrorLayout);
    }

    /// <summary>
    /// Kscope Standalone Defaults resolve as fresh, mutually independent copies without pinning the
    /// authored look values that ADR-0013 reserves for watching on the wall.
    /// </summary>
    [Test]
    public void KscopeStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Kscope.StandaloneDefaults;
        var second = Kscope.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.TextureMinimumAdvance, Is.EqualTo(second.TextureMinimumAdvance));
        Assert.That(first.TextureAdvanceRangeDivisor, Is.EqualTo(second.TextureAdvanceRangeDivisor));
        Assert.That(first.ColorSwapRollMaxExclusive, Is.EqualTo(second.ColorSwapRollMaxExclusive));
        Assert.That(first.ChannelSwapSelectorMaxExclusive, Is.EqualTo(second.ChannelSwapSelectorMaxExclusive));
        Assert.That(first.MotionStep, Is.Not.SameAs(second.MotionStep));
        AssertIntRangeEqual(first.MotionStep, second.MotionStep);
        Assert.That(first.MotionStepDivisor, Is.EqualTo(second.MotionStepDivisor));
        Assert.That(first.AngularSpeedStep, Is.Not.SameAs(second.AngularSpeedStep));
        AssertIntRangeEqual(first.AngularSpeedStep, second.AngularSpeedStep);
        Assert.That(first.AngularSpeedStepDivisor, Is.EqualTo(second.AngularSpeedStepDivisor));
    }

    /// <summary>
    /// Restore replaces every edited Kscope Standalone Setting and Rail with the current file-local
    /// Standalone Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryKscopeValue()
    {
        var asset = (KscopeStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Kscope),
            TempAssetFolder);
        asset.Settings.TextureMinimumAdvance = 17;
        asset.Settings.TextureAdvanceRangeDivisor = 18;
        asset.Settings.ColorSwapRollMaxExclusive = 19;
        asset.Settings.ChannelSwapSelectorMaxExclusive = 20;
        asset.Settings.MotionStep = new IntRange(21, 22, 20, 23);
        asset.Settings.MotionStepDivisor = 24f;
        asset.Settings.AngularSpeedStep = new IntRange(25, 26, 24, 27);
        asset.Settings.AngularSpeedStepDivisor = 28f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Kscope), TempAssetFolder);

        var defaults = Kscope.StandaloneDefaults;
        Assert.That(asset.Settings.TextureMinimumAdvance, Is.EqualTo(defaults.TextureMinimumAdvance));
        Assert.That(asset.Settings.TextureAdvanceRangeDivisor, Is.EqualTo(defaults.TextureAdvanceRangeDivisor));
        Assert.That(asset.Settings.ColorSwapRollMaxExclusive, Is.EqualTo(defaults.ColorSwapRollMaxExclusive));
        Assert.That(asset.Settings.ChannelSwapSelectorMaxExclusive, Is.EqualTo(defaults.ChannelSwapSelectorMaxExclusive));
        AssertIntRangeEqual(asset.Settings.MotionStep, defaults.MotionStep);
        Assert.That(asset.Settings.MotionStepDivisor, Is.EqualTo(defaults.MotionStepDivisor));
        AssertIntRangeEqual(asset.Settings.AngularSpeedStep, defaults.AngularSpeedStep);
        Assert.That(asset.Settings.AngularSpeedStepDivisor, Is.EqualTo(defaults.AngularSpeedStepDivisor));
    }

    /// <summary>
    /// Restore replaces every edited Kscope Sync Setting and Rail with the current file-local Sync
    /// Defaults, including wall-unit pan and both live mirror-layout motion calibrations.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryKscopeValue()
    {
        var asset = (KscopeSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Kscope),
            TempAssetFolder);
        asset.Settings.TextureMinimumAdvance = 17;
        asset.Settings.TextureAdvanceRangeDivisor = 18;
        asset.Settings.ColorSwapRollMaxExclusive = 19;
        asset.Settings.ChannelSwapSelectorMaxExclusive = 20;
        asset.Settings.PanWallUnitsPerBeat = 21f;
        asset.Settings.Mirror2MotionScale = 22f;
        asset.Settings.Mirror10MotionScale = 23f;
        asset.Settings.RotationRadiansPerBeat = 24f;
        asset.Settings.EnergyPace = new FloatRange(25f, 26f, 24f, 27f);
        asset.Settings.LowPresenceThreshold = 0.28f;
        asset.Settings.OnBeatPushStrength = 29f;
        asset.Settings.PaletteSaturationFloor = 0.29f;
        asset.Settings.BeatHueOffset = 0.3f;
        asset.Settings.FillContrast = 30f;
        asset.Settings.DropSlowdownBeats = 31;
        asset.Settings.DropBurstPace = 32f;
        asset.Settings.DropBurstBeats = 33;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Kscope), TempAssetFolder);

        var defaults = Kscope.SyncDefaults;
        Assert.That(asset.Settings.TextureMinimumAdvance, Is.EqualTo(defaults.TextureMinimumAdvance));
        Assert.That(asset.Settings.TextureAdvanceRangeDivisor, Is.EqualTo(defaults.TextureAdvanceRangeDivisor));
        Assert.That(asset.Settings.ColorSwapRollMaxExclusive, Is.EqualTo(defaults.ColorSwapRollMaxExclusive));
        Assert.That(asset.Settings.ChannelSwapSelectorMaxExclusive, Is.EqualTo(defaults.ChannelSwapSelectorMaxExclusive));
        Assert.That(asset.Settings.PanWallUnitsPerBeat, Is.EqualTo(defaults.PanWallUnitsPerBeat));
        Assert.That(asset.Settings.Mirror2MotionScale, Is.EqualTo(defaults.Mirror2MotionScale));
        Assert.That(asset.Settings.Mirror10MotionScale, Is.EqualTo(defaults.Mirror10MotionScale));
        Assert.That(asset.Settings.RotationRadiansPerBeat, Is.EqualTo(defaults.RotationRadiansPerBeat));
        AssertFloatRangeEqual(asset.Settings.EnergyPace, defaults.EnergyPace);
        Assert.That(asset.Settings.LowPresenceThreshold, Is.EqualTo(defaults.LowPresenceThreshold));
        Assert.That(asset.Settings.OnBeatPushStrength, Is.EqualTo(defaults.OnBeatPushStrength));
        Assert.That(asset.Settings.PaletteSaturationFloor, Is.EqualTo(defaults.PaletteSaturationFloor));
        Assert.That(asset.Settings.BeatHueOffset, Is.EqualTo(defaults.BeatHueOffset));
        Assert.That(asset.Settings.FillContrast, Is.EqualTo(defaults.FillContrast));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
        Assert.That(asset.Settings.DropBurstPace, Is.EqualTo(defaults.DropBurstPace));
        Assert.That(asset.Settings.DropBurstBeats, Is.EqualTo(defaults.DropBurstBeats));
    }

    /// <summary>
    /// ColorSparkle Standalone Defaults resolve as fresh, mutually independent copies without
    /// pinning the authored look in the test.
    /// </summary>
    [Test]
    public void ColorSparkleStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = ColorSparkle.StandaloneDefaults;
        var second = ColorSparkle.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.RandomColorThreshold, Is.EqualTo(second.RandomColorThreshold));
        Assert.That(first.ActivationHue, Is.Not.SameAs(second.ActivationHue));
        AssertFloatRangeEqual(first.ActivationHue, second.ActivationHue);
        Assert.That(first.PerSparkleHue, Is.Not.SameAs(second.PerSparkleHue));
        AssertFloatRangeEqual(first.PerSparkleHue, second.PerSparkleHue);
        Assert.That(first.WaveformHueOffset, Is.Not.SameAs(second.WaveformHueOffset));
        AssertFloatRangeEqual(first.WaveformHueOffset, second.WaveformHueOffset);
        Assert.That(first.HueWrapPeriod, Is.EqualTo(second.HueWrapPeriod));
    }

    /// <summary>
    /// Restore replaces every edited ColorSparkle Standalone Setting and Rail with the current
    /// file-local Standalone Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryColorSparkleValue()
    {
        var asset = (ColorSparkleStandaloneSettingsAsset)
            EffectStandaloneSettingsAssetUtility.EnsureAsset(
                typeof(ColorSparkle),
                TempAssetFolder);
        asset.Settings.RandomColorThreshold = 0.17f;
        asset.Settings.ActivationHue = new FloatRange(0.18f, 0.19f, 0.17f, 0.2f);
        asset.Settings.PerSparkleHue = new FloatRange(0.21f, 0.22f, 0.2f, 0.23f);
        asset.Settings.WaveformHueOffset = new FloatRange(0.24f, 0.25f, 0.23f, 0.26f);
        asset.Settings.HueWrapPeriod = 0.27f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(ColorSparkle),
            TempAssetFolder);

        var defaults = ColorSparkle.StandaloneDefaults;
        Assert.That(asset.Settings.RandomColorThreshold, Is.EqualTo(defaults.RandomColorThreshold));
        AssertFloatRangeEqual(asset.Settings.ActivationHue, defaults.ActivationHue);
        AssertFloatRangeEqual(asset.Settings.PerSparkleHue, defaults.PerSparkleHue);
        AssertFloatRangeEqual(asset.Settings.WaveformHueOffset, defaults.WaveformHueOffset);
        Assert.That(asset.Settings.HueWrapPeriod, Is.EqualTo(defaults.HueWrapPeriod));
    }

    /// <summary>
    /// NoiseTunnel Standalone Defaults resolve as fresh, mutually independent copies without
    /// pinning the authored look in the test.
    /// </summary>
    [Test]
    public void NoiseTunnelStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = NoiseTunnel.StandaloneDefaults;
        var second = NoiseTunnel.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.TileCenterScale, Is.Not.SameAs(second.TileCenterScale));
        AssertFloatRangeEqual(first.TileCenterScale, second.TileCenterScale);
        Assert.That(first.TunnelFlowSpeed, Is.Not.SameAs(second.TunnelFlowSpeed));
        AssertFloatRangeEqual(first.TunnelFlowSpeed, second.TunnelFlowSpeed);
        Assert.That(first.PerlinAmplitude, Is.Not.SameAs(second.PerlinAmplitude));
        AssertFloatRangeEqual(first.PerlinAmplitude, second.PerlinAmplitude);
        Assert.That(first.DistanceStyle, Is.Not.SameAs(second.DistanceStyle));
        AssertIntRangeEqual(first.DistanceStyle, second.DistanceStyle);
        Assert.That(first.DistanceDirection, Is.Not.SameAs(second.DistanceDirection));
        AssertIntRangeEqual(first.DistanceDirection, second.DistanceDirection);
        Assert.That(first.WaveformResponseMode, Is.Not.SameAs(second.WaveformResponseMode));
        AssertIntRangeEqual(first.WaveformResponseMode, second.WaveformResponseMode);
        Assert.That(first.Brightness, Is.Not.SameAs(second.Brightness));
        AssertFloatRangeEqual(first.Brightness, second.Brightness);
    }

    /// <summary>
    /// Restore replaces every edited NoiseTunnel Standalone Setting and Rail with its current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryNoiseTunnelValue()
    {
        var asset = (NoiseTunnelStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(NoiseTunnel),
            TempAssetFolder);
        asset.Settings.TileCenterScale = new FloatRange(10f, 11f, 9f, 12f);
        asset.Settings.TunnelFlowSpeed = new FloatRange(13f, 14f, 12f, 15f);
        asset.Settings.PerlinAmplitude = new FloatRange(16f, 17f, 15f, 18f);
        asset.Settings.DistanceStyle = new IntRange(19, 20, 18, 21);
        asset.Settings.DistanceDirection = new IntRange(22, 23, 21, 24);
        asset.Settings.WaveformResponseMode = new IntRange(25, 26, 24, 27);
        asset.Settings.Brightness = new FloatRange(28f, 29f, 27f, 30f);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(NoiseTunnel),
            TempAssetFolder);

        var defaults = NoiseTunnel.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.TileCenterScale, defaults.TileCenterScale);
        AssertFloatRangeEqual(asset.Settings.TunnelFlowSpeed, defaults.TunnelFlowSpeed);
        AssertFloatRangeEqual(asset.Settings.PerlinAmplitude, defaults.PerlinAmplitude);
        AssertIntRangeEqual(asset.Settings.DistanceStyle, defaults.DistanceStyle);
        AssertIntRangeEqual(asset.Settings.DistanceDirection, defaults.DistanceDirection);
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
    }

    /// <summary>
    /// Pulse Standalone Defaults resolve as fresh, mutually independent copies without pinning the
    /// authored look values that ADR-0013 reserves for watching on the wall.
    /// </summary>
    [Test]
    public void PulseStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Pulse.StandaloneDefaults;
        var second = Pulse.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BaseHue, Is.Not.SameAs(second.BaseHue));
        Assert.That(first.BaseHue.Min, Is.EqualTo(second.BaseHue.Min));
        Assert.That(first.BaseHue.Max, Is.EqualTo(second.BaseHue.Max));
        Assert.That(first.ColorPingPongSeconds, Is.Not.SameAs(second.ColorPingPongSeconds));
        Assert.That(first.ColorPingPongSeconds.Min, Is.EqualTo(second.ColorPingPongSeconds.Min));
        Assert.That(first.ColorPingPongSeconds.Max, Is.EqualTo(second.ColorPingPongSeconds.Max));
        Assert.That(first.ColorHueDelta, Is.Not.SameAs(second.ColorHueDelta));
        Assert.That(first.ColorHueDelta.Min, Is.EqualTo(second.ColorHueDelta.Min));
        Assert.That(first.ColorHueDelta.Max, Is.EqualTo(second.ColorHueDelta.Max));
        Assert.That(first.BeatMode, Is.Not.SameAs(second.BeatMode));
        Assert.That(first.BeatMode.MinInclusive, Is.EqualTo(second.BeatMode.MinInclusive));
        Assert.That(first.BeatMode.MaxExclusive, Is.EqualTo(second.BeatMode.MaxExclusive));
        Assert.That(first.PulseMultiplier, Is.Not.SameAs(second.PulseMultiplier));
        Assert.That(first.PulseMultiplier.Min, Is.EqualTo(second.PulseMultiplier.Min));
        Assert.That(first.PulseMultiplier.Max, Is.EqualTo(second.PulseMultiplier.Max));
        Assert.That(first.PulseScaleDivisorMilliseconds, Is.EqualTo(second.PulseScaleDivisorMilliseconds));
        Assert.That(first.PulseHeightAtWaveformPeak, Is.EqualTo(second.PulseHeightAtWaveformPeak));
        Assert.That(first.SaturationPulseMultiplier, Is.EqualTo(second.SaturationPulseMultiplier));
        Assert.That(first.ValuePulseBase, Is.EqualTo(second.ValuePulseBase));
    }

    /// <summary>
    /// Restore replaces every edited Pulse Standalone Setting and Rail with the current file-local
    /// Standalone Defaults, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryPulseValue()
    {
        var asset = (PulseStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Pulse),
            TempAssetFolder);
        asset.Settings.BaseHue = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.ColorPingPongSeconds = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.ColorHueDelta = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.BeatMode = new IntRange(17, 18, 16, 19);
        asset.Settings.PulseMultiplier = new FloatRange(26f, 27f, 25f, 28f);
        asset.Settings.PulseScaleDivisorMilliseconds = 29f;
        asset.Settings.PulseHeightAtWaveformPeak = 30f;
        asset.Settings.SaturationPulseMultiplier = 31f;
        asset.Settings.ValuePulseBase = 32f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Pulse), TempAssetFolder);

        var defaults = Pulse.StandaloneDefaults;
        Assert.That(asset.Settings.BaseHue.Min, Is.EqualTo(defaults.BaseHue.Min));
        Assert.That(asset.Settings.BaseHue.Max, Is.EqualTo(defaults.BaseHue.Max));
        Assert.That(asset.Settings.BaseHue.LowRail, Is.EqualTo(defaults.BaseHue.LowRail));
        Assert.That(asset.Settings.BaseHue.HighRail, Is.EqualTo(defaults.BaseHue.HighRail));
        Assert.That(asset.Settings.ColorPingPongSeconds.Min, Is.EqualTo(defaults.ColorPingPongSeconds.Min));
        Assert.That(asset.Settings.ColorPingPongSeconds.Max, Is.EqualTo(defaults.ColorPingPongSeconds.Max));
        Assert.That(asset.Settings.ColorPingPongSeconds.LowRail, Is.EqualTo(defaults.ColorPingPongSeconds.LowRail));
        Assert.That(asset.Settings.ColorPingPongSeconds.HighRail, Is.EqualTo(defaults.ColorPingPongSeconds.HighRail));
        Assert.That(asset.Settings.ColorHueDelta.Min, Is.EqualTo(defaults.ColorHueDelta.Min));
        Assert.That(asset.Settings.ColorHueDelta.Max, Is.EqualTo(defaults.ColorHueDelta.Max));
        Assert.That(asset.Settings.ColorHueDelta.LowRail, Is.EqualTo(defaults.ColorHueDelta.LowRail));
        Assert.That(asset.Settings.ColorHueDelta.HighRail, Is.EqualTo(defaults.ColorHueDelta.HighRail));
        Assert.That(asset.Settings.BeatMode.MinInclusive, Is.EqualTo(defaults.BeatMode.MinInclusive));
        Assert.That(asset.Settings.BeatMode.MaxExclusive, Is.EqualTo(defaults.BeatMode.MaxExclusive));
        Assert.That(asset.Settings.BeatMode.LowRail, Is.EqualTo(defaults.BeatMode.LowRail));
        Assert.That(asset.Settings.BeatMode.HighRail, Is.EqualTo(defaults.BeatMode.HighRail));
        Assert.That(asset.Settings.PulseMultiplier.Min, Is.EqualTo(defaults.PulseMultiplier.Min));
        Assert.That(asset.Settings.PulseMultiplier.Max, Is.EqualTo(defaults.PulseMultiplier.Max));
        Assert.That(asset.Settings.PulseMultiplier.LowRail, Is.EqualTo(defaults.PulseMultiplier.LowRail));
        Assert.That(asset.Settings.PulseMultiplier.HighRail, Is.EqualTo(defaults.PulseMultiplier.HighRail));
        Assert.That(asset.Settings.PulseScaleDivisorMilliseconds, Is.EqualTo(defaults.PulseScaleDivisorMilliseconds));
        Assert.That(asset.Settings.PulseHeightAtWaveformPeak, Is.EqualTo(defaults.PulseHeightAtWaveformPeak));
        Assert.That(asset.Settings.SaturationPulseMultiplier, Is.EqualTo(defaults.SaturationPulseMultiplier));
        Assert.That(asset.Settings.ValuePulseBase, Is.EqualTo(defaults.ValuePulseBase));
    }

    /// <summary>
    /// Julia Standalone Defaults resolve as independent values, including their range and preset
    /// objects, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void JuliaStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Julia.StandaloneDefaults;
        var second = Julia.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BreathingZoomSpeed, Is.Not.SameAs(second.BreathingZoomSpeed));
        AssertFloatRangeEqual(first.BreathingZoomSpeed, second.BreathingZoomSpeed);
        Assert.That(first.WindowWidth, Is.Not.SameAs(second.WindowWidth));
        AssertFloatRangeEqual(first.WindowWidth, second.WindowWidth);
        Assert.That(first.PaletteChance, Is.EqualTo(second.PaletteChance));
        Assert.That(first.HueBaseRate, Is.EqualTo(second.HueBaseRate));
        Assert.That(first.HueBeatRate, Is.EqualTo(second.HueBeatRate));
        Assert.That(first.HueCycleDrive, Is.EqualTo(second.HueCycleDrive));
        Assert.That(first.JuliaConstants, Is.Not.SameAs(second.JuliaConstants));
        Assert.That(first.JuliaConstants, Is.EqualTo(second.JuliaConstants));
        Assert.That(first.PresetViewCenters, Is.Not.SameAs(second.PresetViewCenters));
        Assert.That(first.PresetViewCenters, Is.EqualTo(second.PresetViewCenters));
    }

    /// <summary>
    /// Restore replaces every edited Julia Standalone Setting, range Rail, and preset-table value
    /// with the current file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryJuliaValue()
    {
        var asset = (JuliaStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Julia),
            TempAssetFolder);
        asset.Settings.BreathingZoomSpeed = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.WindowWidth = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.PaletteChance = 0.11f;
        asset.Settings.HueBaseRate = 23f;
        asset.Settings.HueBeatRate = 24f;
        asset.Settings.HueCycleDrive = 0.12f;
        asset.Settings.JuliaConstants = new[] { new Vector2(25f, 26f) };
        asset.Settings.PresetViewCenters = new[] { new Vector2(27f, 28f) };

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Julia), TempAssetFolder);

        var defaults = Julia.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.BreathingZoomSpeed, defaults.BreathingZoomSpeed);
        AssertFloatRangeEqual(asset.Settings.WindowWidth, defaults.WindowWidth);
        Assert.That(asset.Settings.PaletteChance, Is.EqualTo(defaults.PaletteChance));
        Assert.That(asset.Settings.HueBaseRate, Is.EqualTo(defaults.HueBaseRate));
        Assert.That(asset.Settings.HueBeatRate, Is.EqualTo(defaults.HueBeatRate));
        Assert.That(asset.Settings.HueCycleDrive, Is.EqualTo(defaults.HueCycleDrive));
        Assert.That(asset.Settings.JuliaConstants, Is.Not.SameAs(defaults.JuliaConstants));
        Assert.That(asset.Settings.JuliaConstants, Is.EqualTo(defaults.JuliaConstants));
        Assert.That(asset.Settings.PresetViewCenters, Is.Not.SameAs(defaults.PresetViewCenters));
        Assert.That(asset.Settings.PresetViewCenters, Is.EqualTo(defaults.PresetViewCenters));
    }

    /// <summary>
    /// RainbowBars Standalone Defaults resolve as fresh, deeply independent copies without pinning
    /// the authored look that ADR-0013 leaves to judgment on the wall.
    /// </summary>
    [Test]
    public void RainbowBarsStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = RainbowBars.StandaloneDefaults;
        var second = RainbowBars.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Direction, Is.Not.SameAs(second.Direction));
        AssertIntRangeEqual(first.Direction, second.Direction);
        Assert.That(first.Brightness, Is.Not.SameAs(second.Brightness));
        AssertFloatRangeEqual(first.Brightness, second.Brightness);
        Assert.That(first.DirectionSkew, Is.EqualTo(second.DirectionSkew));
    }

    /// <summary>
    /// Restore replaces every edited RainbowBars Standalone Setting and Rail with the current
    /// file-local Standalone Defaults, without pinning their authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryRainbowBarsValue()
    {
        var asset = (RainbowBarsStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(RainbowBars),
            TempAssetFolder);
        asset.Settings.Direction = new IntRange(10, 11, 9, 12);
        asset.Settings.Brightness = new FloatRange(0.1f, 0.2f, 0f, 0.3f);
        asset.Settings.DirectionSkew = 0.4f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(RainbowBars),
            TempAssetFolder);

        var defaults = RainbowBars.StandaloneDefaults;
        AssertIntRangeEqual(asset.Settings.Direction, defaults.Direction);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
        Assert.That(asset.Settings.DirectionSkew, Is.EqualTo(defaults.DirectionSkew));
    }

    /// <summary>
    /// Ripple Standalone Defaults resolve as fresh, independent copies without pinning authored
    /// tuning values that are judged on the wall.
    /// </summary>
    [Test]
    public void RippleStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Ripple.StandaloneDefaults;
        var second = Ripple.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.DropSpawnChance, Is.Not.SameAs(second.DropSpawnChance));
        AssertFloatRangeEqual(first.DropSpawnChance, second.DropSpawnChance);
        Assert.That(first.Velocity, Is.Not.SameAs(second.Velocity));
        AssertFloatRangeEqual(first.Velocity, second.Velocity);
        Assert.That(first.VelocityDivisor, Is.EqualTo(second.VelocityDivisor));
        Assert.That(first.DistanceDivisor, Is.EqualTo(second.DistanceDivisor));
        Assert.That(first.PaletteOffset, Is.EqualTo(second.PaletteOffset));
        Assert.That(first.HueShift, Is.EqualTo(second.HueShift));
    }

    /// <summary>
    /// Restore replaces every edited Ripple Standalone Setting and Rail with the current file-local
    /// Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryRippleValue()
    {
        var asset = (RippleStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Ripple),
            TempAssetFolder);
        asset.Settings.DropSpawnChance = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.Velocity = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.VelocityDivisor = 23f;
        asset.Settings.DistanceDivisor = 24f;
        asset.Settings.PaletteOffset = 0.11f;
        asset.Settings.HueShift = 0.12f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Ripple), TempAssetFolder);

        var defaults = Ripple.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.DropSpawnChance, defaults.DropSpawnChance);
        AssertFloatRangeEqual(asset.Settings.Velocity, defaults.Velocity);
        Assert.That(asset.Settings.VelocityDivisor, Is.EqualTo(defaults.VelocityDivisor));
        Assert.That(asset.Settings.DistanceDivisor, Is.EqualTo(defaults.DistanceDivisor));
        Assert.That(asset.Settings.PaletteOffset, Is.EqualTo(defaults.PaletteOffset));
        Assert.That(asset.Settings.HueShift, Is.EqualTo(defaults.HueShift));
    }

    /// <summary>
    /// Nibbler Standalone Defaults resolve as fresh, deeply independent copies without pinning the
    /// authored look that ADR-0013 leaves to judgment on the wall.
    /// </summary>
    [Test]
    public void NibblerStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Nibbler.StandaloneDefaults;
        var second = Nibbler.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.FixedColorChance, Is.EqualTo(second.FixedColorChance));
        Assert.That(first.TrailRetention, Is.Not.SameAs(second.TrailRetention));
        AssertFloatRangeEqual(first.TrailRetention, second.TrailRetention);
        Assert.That(first.FixedColorHue, Is.Not.SameAs(second.FixedColorHue));
        AssertFloatRangeEqual(first.FixedColorHue, second.FixedColorHue);
        Assert.That(first.StepHue, Is.Not.SameAs(second.StepHue));
        AssertFloatRangeEqual(first.StepHue, second.StepHue);
        Assert.That(first.WaveformHueMode, Is.Not.SameAs(second.WaveformHueMode));
        AssertIntRangeEqual(first.WaveformHueMode, second.WaveformHueMode);
        Assert.That(first.BeatBrightnessAtPeak, Is.EqualTo(second.BeatBrightnessAtPeak));
        Assert.That(first.WalkerStepsPerSecond, Is.EqualTo(second.WalkerStepsPerSecond));
    }

    /// <summary>
    /// Restore replaces every edited Nibbler Standalone Setting and range Rail with the current
    /// file-local Standalone Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryNibblerValue()
    {
        var asset = (NibblerStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Nibbler),
            TempAssetFolder);
        asset.Settings.FixedColorChance = 0.11f;
        asset.Settings.TrailRetention = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.FixedColorHue = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.StepHue = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.WaveformHueMode = new IntRange(26, 27, 25, 28);
        asset.Settings.BeatBrightnessAtPeak = 0.12f;
        asset.Settings.WalkerStepsPerSecond = 29f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Nibbler), TempAssetFolder);

        var defaults = Nibbler.StandaloneDefaults;
        Assert.That(asset.Settings.FixedColorChance, Is.EqualTo(defaults.FixedColorChance));
        AssertFloatRangeEqual(asset.Settings.TrailRetention, defaults.TrailRetention);
        AssertFloatRangeEqual(asset.Settings.FixedColorHue, defaults.FixedColorHue);
        AssertFloatRangeEqual(asset.Settings.StepHue, defaults.StepHue);
        AssertIntRangeEqual(asset.Settings.WaveformHueMode, defaults.WaveformHueMode);
        Assert.That(asset.Settings.BeatBrightnessAtPeak, Is.EqualTo(defaults.BeatBrightnessAtPeak));
        Assert.That(asset.Settings.WalkerStepsPerSecond, Is.EqualTo(defaults.WalkerStepsPerSecond));
    }

    /// <summary>
    /// MetaBalls Standalone Defaults resolve as fresh, deeply independent copies without pinning
    /// authored tuning values that are judged on the wall.
    /// </summary>
    [Test]
    public void MetaBallsStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = MetaBalls.StandaloneDefaults;
        var second = MetaBalls.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Radius, Is.EqualTo(second.Radius));
        Assert.That(first.BallVelocity, Is.Not.SameAs(second.BallVelocity));
        AssertFloatRangeEqual(first.BallVelocity, second.BallVelocity);
        Assert.That(first.BallVelocityDivisor, Is.EqualTo(second.BallVelocityDivisor));
        Assert.That(first.WaveformResponseMode, Is.Not.SameAs(second.WaveformResponseMode));
        AssertIntRangeEqual(first.WaveformResponseMode, second.WaveformResponseMode);
        Assert.That(first.HorizontalBounceMargin, Is.EqualTo(second.HorizontalBounceMargin));
        Assert.That(first.VerticalBounceMargin, Is.EqualTo(second.VerticalBounceMargin));
        Assert.That(first.WaveformBrightnessAtPeak, Is.EqualTo(second.WaveformBrightnessAtPeak));
    }

    /// <summary>
    /// Restore replaces every edited MetaBalls Standalone Setting and Rail with the current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryMetaBallsValue()
    {
        var asset = (MetaBallsStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(MetaBalls),
            TempAssetFolder);
        asset.Settings.Radius = 17f;
        asset.Settings.BallVelocity = new FloatRange(18f, 19f, 17f, 20f);
        asset.Settings.BallVelocityDivisor = 21f;
        asset.Settings.WaveformResponseMode = new IntRange(22, 23, 21, 24);
        asset.Settings.HorizontalBounceMargin = 25f;
        asset.Settings.VerticalBounceMargin = 26f;
        asset.Settings.WaveformBrightnessAtPeak = 0.27f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(MetaBalls),
            TempAssetFolder);

        var defaults = MetaBalls.StandaloneDefaults;
        Assert.That(asset.Settings.Radius, Is.EqualTo(defaults.Radius));
        AssertFloatRangeEqual(asset.Settings.BallVelocity, defaults.BallVelocity);
        Assert.That(asset.Settings.BallVelocityDivisor, Is.EqualTo(defaults.BallVelocityDivisor));
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        Assert.That(asset.Settings.HorizontalBounceMargin, Is.EqualTo(defaults.HorizontalBounceMargin));
        Assert.That(asset.Settings.VerticalBounceMargin, Is.EqualTo(defaults.VerticalBounceMargin));
        Assert.That(asset.Settings.WaveformBrightnessAtPeak, Is.EqualTo(defaults.WaveformBrightnessAtPeak));
    }

    /// <summary>
    /// Restore replaces every edited MetaBalls Sync Setting and Rail with the current file-local
    /// Sync Defaults, including picture-shaping values dual-homed on both mode surfaces.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryMetaBallsValue()
    {
        var asset = (MetaBallsSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(MetaBalls),
            TempAssetFolder);
        asset.Settings.Radius = 17f;
        asset.Settings.BallVelocity = new FloatRange(18f, 19f, 17f, 20f);
        asset.Settings.BallVelocityDivisor = 21f;
        asset.Settings.WaveformResponseMode = new IntRange(22, 23, 21, 24);
        asset.Settings.HorizontalBounceMargin = 25f;
        asset.Settings.VerticalBounceMargin = 26f;
        asset.Settings.WaveformBrightness = new FloatRange(0.27f, 0.28f, 0.26f, 0.29f);
        asset.Settings.WaveformHueShift = 0.3f;
        asset.Settings.WaveformDeltaBoost = 31f;
        asset.Settings.FillSaturation = 0.32f;
        asset.Settings.DropSlowdownBeats = 33;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(MetaBalls), TempAssetFolder);

        var defaults = MetaBalls.SyncDefaults;
        Assert.That(asset.Settings.Radius, Is.EqualTo(defaults.Radius));
        AssertFloatRangeEqual(asset.Settings.BallVelocity, defaults.BallVelocity);
        Assert.That(asset.Settings.BallVelocityDivisor, Is.EqualTo(defaults.BallVelocityDivisor));
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        Assert.That(asset.Settings.HorizontalBounceMargin, Is.EqualTo(defaults.HorizontalBounceMargin));
        Assert.That(asset.Settings.VerticalBounceMargin, Is.EqualTo(defaults.VerticalBounceMargin));
        AssertFloatRangeEqual(asset.Settings.WaveformBrightness, defaults.WaveformBrightness);
        Assert.That(asset.Settings.WaveformHueShift, Is.EqualTo(defaults.WaveformHueShift));
        Assert.That(asset.Settings.WaveformDeltaBoost, Is.EqualTo(defaults.WaveformDeltaBoost));
        Assert.That(asset.Settings.FillSaturation, Is.EqualTo(defaults.FillSaturation));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>
    /// Crystal Growth Standalone Defaults resolve as fresh, deeply independent copies without
    /// pinning the authored look's numeric values.
    /// </summary>
    [Test]
    public void CrystalGrowthStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = CrystalGrowth.StandaloneDefaults;
        var second = CrystalGrowth.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.HeatEpsilon, Is.EqualTo(second.HeatEpsilon));
        Assert.That(first.FrontPush, Is.EqualTo(second.FrontPush));
        Assert.That(first.CoverageToAdvance, Is.EqualTo(second.CoverageToAdvance));
        Assert.That(first.MaxFrontPassesPerFrame, Is.EqualTo(second.MaxFrontPassesPerFrame));
        Assert.That(first.CrystalFloor, Is.EqualTo(second.CrystalFloor));
        Assert.That(first.HueRelaxPerSec, Is.EqualTo(second.HueRelaxPerSec));
        Assert.That(first.HueRelaxMaxPerFrame, Is.EqualTo(second.HueRelaxMaxPerFrame));
        Assert.That(first.ActivityLevel, Is.Not.SameAs(second.ActivityLevel));
        AssertFloatRangeEqual(first.ActivityLevel, second.ActivityLevel);
        Assert.That(first.GoldenStep, Is.EqualTo(second.GoldenStep));
        Assert.That(first.SpreadPerSec, Is.Not.SameAs(second.SpreadPerSec));
        AssertFloatRangeEqual(first.SpreadPerSec, second.SpreadPerSec);
        Assert.That(first.LeakPerSec, Is.Not.SameAs(second.LeakPerSec));
        AssertFloatRangeEqual(first.LeakPerSec, second.LeakPerSec);
        Assert.That(first.BeatSurge, Is.Not.SameAs(second.BeatSurge));
        AssertFloatRangeEqual(first.BeatSurge, second.BeatSurge);
        Assert.That(first.SeedInterval, Is.Not.SameAs(second.SeedInterval));
        AssertFloatRangeEqual(first.SeedInterval, second.SeedInterval);
        Assert.That(first.SelfBeatPeriod, Is.Not.SameAs(second.SelfBeatPeriod));
        AssertFloatRangeEqual(first.SelfBeatPeriod, second.SelfBeatPeriod);
        Assert.That(first.SelfPulsePeak, Is.Not.SameAs(second.SelfPulsePeak));
        AssertFloatRangeEqual(first.SelfPulsePeak, second.SelfPulsePeak);
        Assert.That(first.SelfPulseNoiseSpeed, Is.EqualTo(second.SelfPulseNoiseSpeed));
        Assert.That(first.SelfPulseDecayPerSec, Is.EqualTo(second.SelfPulseDecayPerSec));
        Assert.That(first.TipThreshold, Is.EqualTo(second.TipThreshold));
        Assert.That(first.TipWhitenAmount, Is.EqualTo(second.TipWhitenAmount));
        Assert.That(first.BloomCountBase, Is.EqualTo(second.BloomCountBase));
        Assert.That(first.BloomCountOffset, Is.Not.SameAs(second.BloomCountOffset));
        AssertIntRangeEqual(first.BloomCountOffset, second.BloomCountOffset);
    }

    /// <summary>
    /// Restore replaces every edited Crystal Growth Standalone Setting and Rail with the current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryCrystalGrowthValue()
    {
        var asset = (CrystalGrowthStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(CrystalGrowth),
            TempAssetFolder);
        asset.Settings.HeatEpsilon = 0.11f;
        asset.Settings.FrontPush = 0.12f;
        asset.Settings.CoverageToAdvance = 0.13f;
        asset.Settings.MaxFrontPassesPerFrame = 14;
        asset.Settings.CrystalFloor = 0.15f;
        asset.Settings.HueRelaxPerSec = 0.16f;
        asset.Settings.HueRelaxMaxPerFrame = 0.17f;
        asset.Settings.ActivityLevel = new FloatRange(0.18f, 0.19f, 0.17f, 0.2f);
        asset.Settings.GoldenStep = 0.21f;
        asset.Settings.SpreadPerSec = new FloatRange(22f, 23f, 21f, 24f);
        asset.Settings.LeakPerSec = new FloatRange(0.25f, 0.26f, 0.24f, 0.27f);
        asset.Settings.BeatSurge = new FloatRange(28f, 29f, 27f, 30f);
        asset.Settings.SeedInterval = new FloatRange(0.31f, 0.32f, 0.3f, 0.33f);
        asset.Settings.SelfBeatPeriod = new FloatRange(34f, 35f, 33f, 36f);
        asset.Settings.SelfPulsePeak = new FloatRange(0.81f, 0.82f, 0.8f, 0.83f);
        asset.Settings.SelfPulseNoiseSpeed = 84f;
        asset.Settings.SelfPulseDecayPerSec = 37f;
        asset.Settings.TipThreshold = 0.38f;
        asset.Settings.TipWhitenAmount = 0.39f;
        asset.Settings.BloomCountBase = 40;
        asset.Settings.BloomCountOffset = new IntRange(41, 42, 40, 43);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(CrystalGrowth),
            TempAssetFolder);

        var defaults = CrystalGrowth.StandaloneDefaults;
        Assert.That(asset.Settings.HeatEpsilon, Is.EqualTo(defaults.HeatEpsilon));
        Assert.That(asset.Settings.FrontPush, Is.EqualTo(defaults.FrontPush));
        Assert.That(asset.Settings.CoverageToAdvance, Is.EqualTo(defaults.CoverageToAdvance));
        Assert.That(asset.Settings.MaxFrontPassesPerFrame, Is.EqualTo(defaults.MaxFrontPassesPerFrame));
        Assert.That(asset.Settings.CrystalFloor, Is.EqualTo(defaults.CrystalFloor));
        Assert.That(asset.Settings.HueRelaxPerSec, Is.EqualTo(defaults.HueRelaxPerSec));
        Assert.That(asset.Settings.HueRelaxMaxPerFrame, Is.EqualTo(defaults.HueRelaxMaxPerFrame));
        AssertFloatRangeEqual(asset.Settings.ActivityLevel, defaults.ActivityLevel);
        Assert.That(asset.Settings.GoldenStep, Is.EqualTo(defaults.GoldenStep));
        AssertFloatRangeEqual(asset.Settings.SpreadPerSec, defaults.SpreadPerSec);
        AssertFloatRangeEqual(asset.Settings.LeakPerSec, defaults.LeakPerSec);
        AssertFloatRangeEqual(asset.Settings.BeatSurge, defaults.BeatSurge);
        AssertFloatRangeEqual(asset.Settings.SeedInterval, defaults.SeedInterval);
        AssertFloatRangeEqual(asset.Settings.SelfBeatPeriod, defaults.SelfBeatPeriod);
        AssertFloatRangeEqual(asset.Settings.SelfPulsePeak, defaults.SelfPulsePeak);
        Assert.That(asset.Settings.SelfPulseNoiseSpeed, Is.EqualTo(defaults.SelfPulseNoiseSpeed));
        Assert.That(asset.Settings.SelfPulseDecayPerSec, Is.EqualTo(defaults.SelfPulseDecayPerSec));
        Assert.That(asset.Settings.TipThreshold, Is.EqualTo(defaults.TipThreshold));
        Assert.That(asset.Settings.TipWhitenAmount, Is.EqualTo(defaults.TipWhitenAmount));
        Assert.That(asset.Settings.BloomCountBase, Is.EqualTo(defaults.BloomCountBase));
        AssertIntRangeEqual(asset.Settings.BloomCountOffset, defaults.BloomCountOffset);
    }

    /// <summary>
    /// Verifies AnimateShapes Standalone Defaults resolve as fresh, mutually independent copies
    /// while leaving their numeric contents unconstrained.
    /// </summary>
    [Test]
    public void AnimateShapesStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = AnimateShapes.StandaloneDefaults;
        var second = AnimateShapes.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BackgroundHueRate, Is.EqualTo(second.BackgroundHueRate));
        AssertPaletteConditioningEqual(
            first.ForegroundPaletteConditioning,
            second.ForegroundPaletteConditioning);
        Assert.That(first.ForegroundTilePositionStep, Is.EqualTo(second.ForegroundTilePositionStep));
        Assert.That(
            first.ForegroundPositionAdvancePerSecond,
            Is.EqualTo(second.ForegroundPositionAdvancePerSecond));
    }

    /// <summary>
    /// Restore replaces every edited AnimateShapes Standalone Setting with the current file-local
    /// Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryAnimateShapesValue()
    {
        var asset = (AnimateShapesStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(AnimateShapes),
            TempAssetFolder);
        asset.Settings.BackgroundHueRate = 17f;
        asset.Settings.ForegroundPaletteConditioning = new PaletteConditioning
        {
            TargetLuminance = 0.11f,
            MinimumLuminance = 0.12f,
            LuminanceEqualization = 0.13f,
            HueSpreadReference = 0.14f,
            MaximumLuminanceScale = 1.15f,
            DarkLuminanceThreshold = 0.016f,
            DuplicateThreshold = 0.017f,
            HueRedistribution = 0.18f,
        };
        asset.Settings.ForegroundTilePositionStep = 18f;
        asset.Settings.ForegroundPositionAdvancePerSecond = 19f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(AnimateShapes),
            TempAssetFolder);

        var defaults = AnimateShapes.StandaloneDefaults;
        Assert.That(asset.Settings.BackgroundHueRate, Is.EqualTo(defaults.BackgroundHueRate));
        AssertPaletteConditioningEqual(
            asset.Settings.ForegroundPaletteConditioning,
            defaults.ForegroundPaletteConditioning);
        Assert.That(
            asset.Settings.ForegroundTilePositionStep,
            Is.EqualTo(defaults.ForegroundTilePositionStep));
        Assert.That(
            asset.Settings.ForegroundPositionAdvancePerSecond,
            Is.EqualTo(defaults.ForegroundPositionAdvancePerSecond));
    }

    /// <summary>Restore replaces every edited Tunnel Sync Setting with the current file-local Sync Defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryTunnelValue()
    {
        var asset = (TunnelSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Tunnel),
            TempAssetFolder);
        asset.Settings.TileIndexPhaseStep = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.LowCycleDuration = Duration.Sixteenth;
        asset.Settings.MidCycleDuration = Duration.Eighth;
        asset.Settings.HighCycleDuration = Duration.Whole;
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
        asset.Settings.FillScrollRateMultiplier = 17f;
        asset.Settings.FillRingCompression = 18f;
        asset.Settings.DropBars = 7;
        asset.Settings.DropReverseScrollRateMultiplier = 19f;
        asset.Settings.DropRingCompression = 20f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Tunnel), TempAssetFolder);

        var defaults = Tunnel.SyncDefaults;
        Assert.That(asset.Settings.TileIndexPhaseStep.Min, Is.EqualTo(defaults.TileIndexPhaseStep.Min));
        Assert.That(asset.Settings.TileIndexPhaseStep.Max, Is.EqualTo(defaults.TileIndexPhaseStep.Max));
        Assert.That(asset.Settings.TileIndexPhaseStep.LowRail, Is.EqualTo(defaults.TileIndexPhaseStep.LowRail));
        Assert.That(asset.Settings.TileIndexPhaseStep.HighRail, Is.EqualTo(defaults.TileIndexPhaseStep.HighRail));
        Assert.That(asset.Settings.LowCycleDuration, Is.EqualTo(defaults.LowCycleDuration));
        Assert.That(asset.Settings.MidCycleDuration, Is.EqualTo(defaults.MidCycleDuration));
        Assert.That(asset.Settings.HighCycleDuration, Is.EqualTo(defaults.HighCycleDuration));
        Assert.That(asset.Settings.RadialMix.Min, Is.EqualTo(defaults.RadialMix.Min));
        Assert.That(asset.Settings.RadialMix.Max, Is.EqualTo(defaults.RadialMix.Max));
        Assert.That(asset.Settings.RadialMix.LowRail, Is.EqualTo(defaults.RadialMix.LowRail));
        Assert.That(asset.Settings.RadialMix.HighRail, Is.EqualTo(defaults.RadialMix.HighRail));
        Assert.That(asset.Settings.CenterScale, Is.EqualTo(defaults.CenterScale));
        AssertPaletteConditioningEqual(
            asset.Settings.PaletteConditioning,
            defaults.PaletteConditioning);
        Assert.That(asset.Settings.FillScrollRateMultiplier, Is.EqualTo(defaults.FillScrollRateMultiplier));
        Assert.That(asset.Settings.FillRingCompression, Is.EqualTo(defaults.FillRingCompression));
        Assert.That(asset.Settings.DropBars, Is.EqualTo(defaults.DropBars));
        Assert.That(asset.Settings.DropReverseScrollRateMultiplier, Is.EqualTo(defaults.DropReverseScrollRateMultiplier));
        Assert.That(asset.Settings.DropRingCompression, Is.EqualTo(defaults.DropRingCompression));
    }

    /// <summary>
    /// Restore replaces every edited Nibbler Sync Setting and range Rail with the current file-local
    /// Sync Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryNibblerValue()
    {
        var asset = (NibblerSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Nibbler),
            TempAssetFolder);
        asset.Settings.FixedColorChance = 0.11f;
        asset.Settings.TrailRetention = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.FixedColorHue = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.StepHue = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.WaveformHueMode = new IntRange(26, 27, 25, 28);
        asset.Settings.BeatBrightnessAtPeak = 0.12f;
        asset.Settings.BeatBrightnessAtTrough = 0.13f;
        asset.Settings.BeatHueShift = 0.14f;
        asset.Settings.WalkerStepsPerSecond = 29f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Nibbler), TempAssetFolder);

        var defaults = Nibbler.SyncDefaults;
        Assert.That(asset.Settings.FixedColorChance, Is.EqualTo(defaults.FixedColorChance));
        AssertFloatRangeEqual(asset.Settings.TrailRetention, defaults.TrailRetention);
        AssertFloatRangeEqual(asset.Settings.FixedColorHue, defaults.FixedColorHue);
        AssertFloatRangeEqual(asset.Settings.StepHue, defaults.StepHue);
        AssertIntRangeEqual(asset.Settings.WaveformHueMode, defaults.WaveformHueMode);
        Assert.That(asset.Settings.BeatBrightnessAtPeak, Is.EqualTo(defaults.BeatBrightnessAtPeak));
        Assert.That(asset.Settings.BeatBrightnessAtTrough, Is.EqualTo(defaults.BeatBrightnessAtTrough));
        Assert.That(asset.Settings.BeatHueShift, Is.EqualTo(defaults.BeatHueShift));
        Assert.That(asset.Settings.WalkerStepsPerSecond, Is.EqualTo(defaults.WalkerStepsPerSecond));
    }

    /// <summary>
    /// Restore replaces every edited Noise Sync Setting and Rail with the current file-local Sync
    /// Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryNoiseValue()
    {
        var asset = (NoiseSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Noise),
            TempAssetFolder);
        asset.Settings.TileCenterScale = new FloatRange(10f, 11f, 9f, 12f);
        asset.Settings.NoiseFieldDriftSpeed = new FloatRange(13f, 14f, 12f, 15f);
        asset.Settings.PerlinAmplitude = new FloatRange(16f, 17f, 15f, 18f);
        asset.Settings.WaveformResponseMode = new IntRange(19, 20, 18, 21);
        asset.Settings.Brightness = new FloatRange(0.22f, 0.23f, 0.21f, 0.24f);
        asset.Settings.HueShiftAtWaveformPeak = 25f;
        asset.Settings.TimeOffsetAtWaveformPeak = 26f;
        asset.Settings.FillSaturation = 0.27f;
        asset.Settings.DropSlowdownBeats = 28;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Noise), TempAssetFolder);

        var defaults = Noise.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.TileCenterScale, defaults.TileCenterScale);
        AssertFloatRangeEqual(asset.Settings.NoiseFieldDriftSpeed, defaults.NoiseFieldDriftSpeed);
        AssertFloatRangeEqual(asset.Settings.PerlinAmplitude, defaults.PerlinAmplitude);
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
        Assert.That(asset.Settings.HueShiftAtWaveformPeak, Is.EqualTo(defaults.HueShiftAtWaveformPeak));
        Assert.That(asset.Settings.TimeOffsetAtWaveformPeak, Is.EqualTo(defaults.TimeOffsetAtWaveformPeak));
        Assert.That(asset.Settings.FillSaturation, Is.EqualTo(defaults.FillSaturation));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>
    /// Restore replaces every edited Fluid Sync Setting and range Rail with the current file-local
    /// Sync Defaults without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryFluidValue()
    {
        var asset = (FluidSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Fluid),
            TempAssetFolder);
        asset.Settings.Damping = 0.21f;
        asset.Settings.Impulse = 22f;
        asset.Settings.InjectionChanceDenominator = 23;
        asset.Settings.PaletteScale = 24f;
        asset.Settings.NeighborWeight = 25f;
        asset.Settings.PaletteOffset = 0.26f;
        asset.Settings.DropApproachBeats = 27;
        asset.Settings.DropDamping = 0.28f;
        asset.Settings.DropForcedInjectionChanceDenominator = 29;
        asset.Settings.DropOnsetSimulationAdvances = 30;
        asset.Settings.DropPaletteOffset = new FloatRange(0.31f, 0.32f, 0.3f, 0.33f);

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Fluid), TempAssetFolder);

        var defaults = Fluid.SyncDefaults;
        Assert.That(asset.Settings.Damping, Is.EqualTo(defaults.Damping));
        Assert.That(asset.Settings.Impulse, Is.EqualTo(defaults.Impulse));
        Assert.That(
            asset.Settings.InjectionChanceDenominator,
            Is.EqualTo(defaults.InjectionChanceDenominator));
        Assert.That(asset.Settings.PaletteScale, Is.EqualTo(defaults.PaletteScale));
        Assert.That(asset.Settings.NeighborWeight, Is.EqualTo(defaults.NeighborWeight));
        Assert.That(asset.Settings.PaletteOffset, Is.EqualTo(defaults.PaletteOffset));
        Assert.That(asset.Settings.DropApproachBeats, Is.EqualTo(defaults.DropApproachBeats));
        Assert.That(asset.Settings.DropDamping, Is.EqualTo(defaults.DropDamping));
        Assert.That(
            asset.Settings.DropForcedInjectionChanceDenominator,
            Is.EqualTo(defaults.DropForcedInjectionChanceDenominator));
        Assert.That(
            asset.Settings.DropOnsetSimulationAdvances,
            Is.EqualTo(defaults.DropOnsetSimulationAdvances));
        AssertFloatRangeEqual(asset.Settings.DropPaletteOffset, defaults.DropPaletteOffset);
    }

    /// <summary>
    /// Restore replaces Mirror's edited Sync layout endpoints and Rails with the current file-local
    /// Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryMirrorValue()
    {
        var asset = (MirrorSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Mirror),
            TempAssetFolder);
        asset.Settings.MirrorLayout = new IntRange(17, 18, 16, 19);

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Mirror), TempAssetFolder);

        AssertIntRangeEqual(asset.Settings.MirrorLayout, Mirror.SyncDefaults.MirrorLayout);
    }

    /// <summary>
    /// Restore replaces every edited ColorSparkle Sync Setting and Rail with its current file-local
    /// Sync Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryColorSparkleValue()
    {
        var asset = (ColorSparkleSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(ColorSparkle),
            TempAssetFolder);
        asset.Settings.ActivationHue = new FloatRange(0.31f, 0.32f, 0.3f, 0.33f);
        asset.Settings.DropHue = new FloatRange(0.34f, 0.35f, 0.33f, 0.36f);
        asset.Settings.WaveformHueOffset = new FloatRange(0.37f, 0.38f, 0.36f, 0.39f);
        asset.Settings.HueWrapPeriod = 0.4f;
        asset.Settings.DropSparkleDivisor = 7;
        asset.Settings.FillWhiteChance = 0.41f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(ColorSparkle), TempAssetFolder);

        var defaults = ColorSparkle.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.ActivationHue, defaults.ActivationHue);
        AssertFloatRangeEqual(asset.Settings.DropHue, defaults.DropHue);
        AssertFloatRangeEqual(asset.Settings.WaveformHueOffset, defaults.WaveformHueOffset);
        Assert.That(asset.Settings.HueWrapPeriod, Is.EqualTo(defaults.HueWrapPeriod));
        Assert.That(asset.Settings.DropSparkleDivisor, Is.EqualTo(defaults.DropSparkleDivisor));
        Assert.That(asset.Settings.FillWhiteChance, Is.EqualTo(defaults.FillWhiteChance));
    }

    /// <summary>
    /// Restore replaces every edited NoiseTunnel Sync Setting and Rail with its current file-local
    /// Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryNoiseTunnelValue()
    {
        var asset = (NoiseTunnelSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(NoiseTunnel),
            TempAssetFolder);
        asset.Settings.WaveformEnergyOne = Energy.High;
        asset.Settings.WaveformEnergyTwo = Energy.High;
        asset.Settings.TileCenterScale = new FloatRange(10f, 11f, 9f, 12f);
        asset.Settings.TunnelFlowSpeed = new FloatRange(13f, 14f, 12f, 15f);
        asset.Settings.PerlinAmplitude = new FloatRange(16f, 17f, 15f, 18f);
        asset.Settings.DistanceStyle = new IntRange(19, 20, 18, 21);
        asset.Settings.DistanceDirection = new IntRange(22, 23, 21, 24);
        asset.Settings.WaveformResponseMode = new IntRange(25, 26, 24, 27);
        asset.Settings.Brightness = new FloatRange(28f, 29f, 27f, 30f);
        asset.Settings.HueShiftAtWaveformPeak = 31f;
        asset.Settings.TimeOffsetAtWaveformPeak = 32f;
        asset.Settings.FillSaturation = 0.33f;
        asset.Settings.DropSlowdownBeats = 34;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(NoiseTunnel), TempAssetFolder);

        var defaults = NoiseTunnel.SyncDefaults;
        Assert.That(asset.Settings.WaveformEnergyOne, Is.EqualTo(defaults.WaveformEnergyOne));
        Assert.That(asset.Settings.WaveformEnergyTwo, Is.EqualTo(defaults.WaveformEnergyTwo));
        AssertFloatRangeEqual(asset.Settings.TileCenterScale, defaults.TileCenterScale);
        AssertFloatRangeEqual(asset.Settings.TunnelFlowSpeed, defaults.TunnelFlowSpeed);
        AssertFloatRangeEqual(asset.Settings.PerlinAmplitude, defaults.PerlinAmplitude);
        AssertIntRangeEqual(asset.Settings.DistanceStyle, defaults.DistanceStyle);
        AssertIntRangeEqual(asset.Settings.DistanceDirection, defaults.DistanceDirection);
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
        Assert.That(asset.Settings.HueShiftAtWaveformPeak, Is.EqualTo(defaults.HueShiftAtWaveformPeak));
        Assert.That(asset.Settings.TimeOffsetAtWaveformPeak, Is.EqualTo(defaults.TimeOffsetAtWaveformPeak));
        Assert.That(asset.Settings.FillSaturation, Is.EqualTo(defaults.FillSaturation));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>Restore replaces every edited Pulse Sync Setting and Rail with the file-local Sync Defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryPulseValue()
    {
        var asset = (PulseSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Pulse),
            TempAssetFolder);
        asset.Settings.BaseHue = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.ColorPingPongSeconds = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.ColorHueDelta = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.BeatMode = new IntRange(17, 18, 16, 19);
        asset.Settings.PulseMultiplier = new FloatRange(26f, 27f, 25f, 28f);
        asset.Settings.PulseScaleDivisorMilliseconds = 29f;
        asset.Settings.PulseHeightAtWaveformTrough = 30f;
        asset.Settings.PulseHeightAtWaveformPeak = 31f;
        asset.Settings.FillColorTimeMultiplier = 32f;
        asset.Settings.SaturationPulseMultiplier = 33f;
        asset.Settings.ValuePulseBase = 34f;
        asset.Settings.DropSlowdownBeats = 35;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Pulse), TempAssetFolder);

        var defaults = Pulse.SyncDefaults;
        Assert.That(asset.Settings.BaseHue.Min, Is.EqualTo(defaults.BaseHue.Min));
        Assert.That(asset.Settings.BaseHue.Max, Is.EqualTo(defaults.BaseHue.Max));
        Assert.That(asset.Settings.BaseHue.LowRail, Is.EqualTo(defaults.BaseHue.LowRail));
        Assert.That(asset.Settings.BaseHue.HighRail, Is.EqualTo(defaults.BaseHue.HighRail));
        Assert.That(asset.Settings.ColorPingPongSeconds.Min, Is.EqualTo(defaults.ColorPingPongSeconds.Min));
        Assert.That(asset.Settings.ColorPingPongSeconds.Max, Is.EqualTo(defaults.ColorPingPongSeconds.Max));
        Assert.That(asset.Settings.ColorPingPongSeconds.LowRail, Is.EqualTo(defaults.ColorPingPongSeconds.LowRail));
        Assert.That(asset.Settings.ColorPingPongSeconds.HighRail, Is.EqualTo(defaults.ColorPingPongSeconds.HighRail));
        Assert.That(asset.Settings.ColorHueDelta.Min, Is.EqualTo(defaults.ColorHueDelta.Min));
        Assert.That(asset.Settings.ColorHueDelta.Max, Is.EqualTo(defaults.ColorHueDelta.Max));
        Assert.That(asset.Settings.ColorHueDelta.LowRail, Is.EqualTo(defaults.ColorHueDelta.LowRail));
        Assert.That(asset.Settings.ColorHueDelta.HighRail, Is.EqualTo(defaults.ColorHueDelta.HighRail));
        Assert.That(asset.Settings.BeatMode.MinInclusive, Is.EqualTo(defaults.BeatMode.MinInclusive));
        Assert.That(asset.Settings.BeatMode.MaxExclusive, Is.EqualTo(defaults.BeatMode.MaxExclusive));
        Assert.That(asset.Settings.BeatMode.LowRail, Is.EqualTo(defaults.BeatMode.LowRail));
        Assert.That(asset.Settings.BeatMode.HighRail, Is.EqualTo(defaults.BeatMode.HighRail));
        Assert.That(asset.Settings.PulseMultiplier.Min, Is.EqualTo(defaults.PulseMultiplier.Min));
        Assert.That(asset.Settings.PulseMultiplier.Max, Is.EqualTo(defaults.PulseMultiplier.Max));
        Assert.That(asset.Settings.PulseMultiplier.LowRail, Is.EqualTo(defaults.PulseMultiplier.LowRail));
        Assert.That(asset.Settings.PulseMultiplier.HighRail, Is.EqualTo(defaults.PulseMultiplier.HighRail));
        Assert.That(asset.Settings.PulseScaleDivisorMilliseconds, Is.EqualTo(defaults.PulseScaleDivisorMilliseconds));
        Assert.That(asset.Settings.PulseHeightAtWaveformTrough, Is.EqualTo(defaults.PulseHeightAtWaveformTrough));
        Assert.That(asset.Settings.PulseHeightAtWaveformPeak, Is.EqualTo(defaults.PulseHeightAtWaveformPeak));
        Assert.That(asset.Settings.FillColorTimeMultiplier, Is.EqualTo(defaults.FillColorTimeMultiplier));
        Assert.That(asset.Settings.SaturationPulseMultiplier, Is.EqualTo(defaults.SaturationPulseMultiplier));
        Assert.That(asset.Settings.ValuePulseBase, Is.EqualTo(defaults.ValuePulseBase));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>
    /// Restore replaces every edited Julia Sync Setting, range Rail, and preset-table value with
    /// the current file-local Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryJuliaValue()
    {
        var asset = (JuliaSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Julia),
            TempAssetFolder);
        asset.Settings.BreathingZoomSpeed = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.WindowWidth = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.PaletteChance = 0.11f;
        asset.Settings.HueBaseRate = 23f;
        asset.Settings.FillDiveDepth = 24f;
        asset.Settings.DropDecayBeats = 25;
        asset.Settings.DropSpinRate = 26f;
        asset.Settings.DropBlowout = 27f;
        asset.Settings.DropHueKick = 0.12f;
        asset.Settings.NegativeDropSpinChance = 0.13f;
        asset.Settings.HueCycleDrive = new FloatRange(0.14f, 0.15f, 0.13f, 0.16f);
        asset.Settings.HueBeatRate = 28f;
        asset.Settings.JuliaConstants = new[] { new Vector2(29f, 30f) };
        asset.Settings.PresetViewCenters = new[] { new Vector2(31f, 32f) };

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Julia), TempAssetFolder);

        var defaults = Julia.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.BreathingZoomSpeed, defaults.BreathingZoomSpeed);
        AssertFloatRangeEqual(asset.Settings.WindowWidth, defaults.WindowWidth);
        Assert.That(asset.Settings.PaletteChance, Is.EqualTo(defaults.PaletteChance));
        Assert.That(asset.Settings.HueBaseRate, Is.EqualTo(defaults.HueBaseRate));
        Assert.That(asset.Settings.FillDiveDepth, Is.EqualTo(defaults.FillDiveDepth));
        Assert.That(asset.Settings.DropDecayBeats, Is.EqualTo(defaults.DropDecayBeats));
        Assert.That(asset.Settings.DropSpinRate, Is.EqualTo(defaults.DropSpinRate));
        Assert.That(asset.Settings.DropBlowout, Is.EqualTo(defaults.DropBlowout));
        Assert.That(asset.Settings.DropHueKick, Is.EqualTo(defaults.DropHueKick));
        Assert.That(asset.Settings.NegativeDropSpinChance, Is.EqualTo(defaults.NegativeDropSpinChance));
        AssertFloatRangeEqual(asset.Settings.HueCycleDrive, defaults.HueCycleDrive);
        Assert.That(asset.Settings.HueBeatRate, Is.EqualTo(defaults.HueBeatRate));
        Assert.That(asset.Settings.JuliaConstants, Is.Not.SameAs(defaults.JuliaConstants));
        Assert.That(asset.Settings.JuliaConstants, Is.EqualTo(defaults.JuliaConstants));
        Assert.That(asset.Settings.PresetViewCenters, Is.Not.SameAs(defaults.PresetViewCenters));
        Assert.That(asset.Settings.PresetViewCenters, Is.EqualTo(defaults.PresetViewCenters));
    }

    /// <summary>
    /// Restore replaces every edited RainbowBars Sync Setting and Rail with the current file-local
    /// Sync Defaults, proving the saved surface covers every production-consumed value.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryRainbowBarsValue()
    {
        var asset = (RainbowBarsSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(RainbowBars),
            TempAssetFolder);
        asset.Settings.Direction = new IntRange(10, 11, 9, 12);
        asset.Settings.WaveformResponseMode = new IntRange(13, 14, 12, 15);
        asset.Settings.Brightness = new FloatRange(0.1f, 0.2f, 0f, 0.3f);
        asset.Settings.HueShiftAtWaveformPeak = 0.4f;
        asset.Settings.TimeOffsetAtWaveformPeak = 5f;
        asset.Settings.DirectionSkew = 6f;
        asset.Settings.FillSaturation = 0.7f;
        asset.Settings.DropSlowdownBeats = 8;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(RainbowBars), TempAssetFolder);

        var defaults = RainbowBars.SyncDefaults;
        AssertIntRangeEqual(asset.Settings.Direction, defaults.Direction);
        AssertIntRangeEqual(asset.Settings.WaveformResponseMode, defaults.WaveformResponseMode);
        AssertFloatRangeEqual(asset.Settings.Brightness, defaults.Brightness);
        Assert.That(asset.Settings.HueShiftAtWaveformPeak, Is.EqualTo(defaults.HueShiftAtWaveformPeak));
        Assert.That(asset.Settings.TimeOffsetAtWaveformPeak, Is.EqualTo(defaults.TimeOffsetAtWaveformPeak));
        Assert.That(asset.Settings.DirectionSkew, Is.EqualTo(defaults.DirectionSkew));
        Assert.That(asset.Settings.FillSaturation, Is.EqualTo(defaults.FillSaturation));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>
    /// Restore replaces every edited Ripple Sync Setting and Rail, including the Levels form and
    /// Low presence threshold, with the current file-local Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryRippleValue()
    {
        var asset = (RippleSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Ripple),
            TempAssetFolder);
        asset.Settings.DropSpawnChance = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.Velocity = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.VelocityDivisor = 23f;
        asset.Settings.DistanceDivisor = 24f;
        asset.Settings.PaletteOffset = 0.11f;
        asset.Settings.LowLevelsForm = LevelsForm.Peak;
        asset.Settings.LowPresenceThreshold = 0.8f;
        asset.Settings.HueShiftMax = 0.9f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Ripple), TempAssetFolder);

        var defaults = Ripple.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.DropSpawnChance, defaults.DropSpawnChance);
        AssertFloatRangeEqual(asset.Settings.Velocity, defaults.Velocity);
        Assert.That(asset.Settings.VelocityDivisor, Is.EqualTo(defaults.VelocityDivisor));
        Assert.That(asset.Settings.DistanceDivisor, Is.EqualTo(defaults.DistanceDivisor));
        Assert.That(asset.Settings.PaletteOffset, Is.EqualTo(defaults.PaletteOffset));
        Assert.That(asset.Settings.LowLevelsForm, Is.EqualTo(defaults.LowLevelsForm));
        Assert.That(asset.Settings.LowPresenceThreshold, Is.EqualTo(defaults.LowPresenceThreshold));
        Assert.That(asset.Settings.HueShiftMax, Is.EqualTo(defaults.HueShiftMax));
    }

    /// <summary>Restore replaces every edited Crystal Growth Sync Setting with its file-local Sync Default.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryCrystalGrowthValue()
    {
        var asset = (CrystalGrowthSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(CrystalGrowth),
            TempAssetFolder);
        asset.Settings.LowLevelsForm = LevelsForm.Peak;
        asset.Settings.ActivityLevelsForm = LevelsForm.Smoothed;
        asset.Settings.LowPresenceThreshold = 0.9f;
        asset.Settings.ActivityLevel = new FloatRange(0.1f, 0.8f, 0.05f, 0.9f);
        asset.Settings.QuietGrowthMultiplier = 0.6f;
        asset.Settings.EnergyPace = new FloatRange(0.2f, 1.8f, 0.1f, 1.9f);
        asset.Settings.DropFlashBrightness = 4f;
        asset.Settings.DropFlashSpread = 5f;
        asset.Settings.DropFlashSeeds = 6;
        asset.Settings.IdleSeedInterval = new FloatRange(1.1f, 1.8f, 1f, 1.9f);
        asset.Settings.DropRatchetSpread = 8f;
        asset.Settings.DropStrobeDepth = 0.2f;
        asset.Settings.DropSeedBurst = 9;
        asset.Settings.FillSwell = 1.4f;
        asset.Settings.DrivingBrightnessFloor = 0.4f;
        asset.Settings.LowSeedBurst = new FloatRange(4f, 10f, 3f, 11f);
        asset.Settings.DownbeatSeedBonus = 5;
        asset.Settings.HeatEpsilon = 0.21f;
        asset.Settings.FrontPush = 0.22f;
        asset.Settings.CoverageToAdvance = 0.23f;
        asset.Settings.MaxFrontPassesPerFrame = 24;
        asset.Settings.CrystalFloor = 0.25f;
        asset.Settings.HueRelaxPerSec = 0.26f;
        asset.Settings.HueRelaxMaxPerFrame = 0.27f;
        asset.Settings.GoldenStep = 0.28f;
        asset.Settings.SpreadPerSec = new FloatRange(29f, 30f, 28f, 31f);
        asset.Settings.LeakPerSec = new FloatRange(0.32f, 0.33f, 0.31f, 0.34f);
        asset.Settings.BeatSurge = new FloatRange(35f, 36f, 34f, 37f);
        asset.Settings.TipThreshold = 0.41f;
        asset.Settings.TipWhitenAmount = 0.42f;
        asset.Settings.BloomCountBase = 43;
        asset.Settings.BloomCountOffset = new IntRange(44, 45, 43, 46);

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(CrystalGrowth), TempAssetFolder);

        var defaults = CrystalGrowth.SyncDefaults;
        Assert.That(asset.Settings.LowLevelsForm, Is.EqualTo(defaults.LowLevelsForm));
        Assert.That(asset.Settings.ActivityLevelsForm, Is.EqualTo(defaults.ActivityLevelsForm));
        Assert.That(asset.Settings.LowPresenceThreshold, Is.EqualTo(defaults.LowPresenceThreshold));
        AssertFloatRangeEqual(asset.Settings.ActivityLevel, defaults.ActivityLevel);
        Assert.That(asset.Settings.QuietGrowthMultiplier, Is.EqualTo(defaults.QuietGrowthMultiplier));
        AssertFloatRangeEqual(asset.Settings.EnergyPace, defaults.EnergyPace);
        Assert.That(asset.Settings.DropFlashBrightness, Is.EqualTo(defaults.DropFlashBrightness));
        Assert.That(asset.Settings.DropFlashSpread, Is.EqualTo(defaults.DropFlashSpread));
        Assert.That(asset.Settings.DropFlashSeeds, Is.EqualTo(defaults.DropFlashSeeds));
        AssertFloatRangeEqual(asset.Settings.IdleSeedInterval, defaults.IdleSeedInterval);
        Assert.That(asset.Settings.DropRatchetSpread, Is.EqualTo(defaults.DropRatchetSpread));
        Assert.That(asset.Settings.DropStrobeDepth, Is.EqualTo(defaults.DropStrobeDepth));
        Assert.That(asset.Settings.DropSeedBurst, Is.EqualTo(defaults.DropSeedBurst));
        Assert.That(asset.Settings.FillSwell, Is.EqualTo(defaults.FillSwell));
        Assert.That(asset.Settings.DrivingBrightnessFloor, Is.EqualTo(defaults.DrivingBrightnessFloor));
        AssertFloatRangeEqual(asset.Settings.LowSeedBurst, defaults.LowSeedBurst);
        Assert.That(asset.Settings.DownbeatSeedBonus, Is.EqualTo(defaults.DownbeatSeedBonus));
        Assert.That(asset.Settings.HeatEpsilon, Is.EqualTo(defaults.HeatEpsilon));
        Assert.That(asset.Settings.FrontPush, Is.EqualTo(defaults.FrontPush));
        Assert.That(asset.Settings.CoverageToAdvance, Is.EqualTo(defaults.CoverageToAdvance));
        Assert.That(asset.Settings.MaxFrontPassesPerFrame, Is.EqualTo(defaults.MaxFrontPassesPerFrame));
        Assert.That(asset.Settings.CrystalFloor, Is.EqualTo(defaults.CrystalFloor));
        Assert.That(asset.Settings.HueRelaxPerSec, Is.EqualTo(defaults.HueRelaxPerSec));
        Assert.That(asset.Settings.HueRelaxMaxPerFrame, Is.EqualTo(defaults.HueRelaxMaxPerFrame));
        Assert.That(asset.Settings.GoldenStep, Is.EqualTo(defaults.GoldenStep));
        AssertFloatRangeEqual(asset.Settings.SpreadPerSec, defaults.SpreadPerSec);
        AssertFloatRangeEqual(asset.Settings.LeakPerSec, defaults.LeakPerSec);
        AssertFloatRangeEqual(asset.Settings.BeatSurge, defaults.BeatSurge);
        Assert.That(asset.Settings.TipThreshold, Is.EqualTo(defaults.TipThreshold));
        Assert.That(asset.Settings.TipWhitenAmount, Is.EqualTo(defaults.TipWhitenAmount));
        Assert.That(asset.Settings.BloomCountBase, Is.EqualTo(defaults.BloomCountBase));
        AssertIntRangeEqual(asset.Settings.BloomCountOffset, defaults.BloomCountOffset);
    }

    /// <summary>
    /// After every saved AnimateShapes Sync Setting receives a sentinel value, verifies that Restore
    /// replaces it—including foreground Energy-crawl, Drop-ribbon, and Fill controls plus the regular
    /// background's Waveform and Drop controls—with the current file-local Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryAnimateShapesValue()
    {
        var asset = (AnimateShapesSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(AnimateShapes),
            TempAssetFolder);
        asset.Settings.BackgroundHueRate = 17f;
        asset.Settings.ForegroundPaletteConditioning = new PaletteConditioning
        {
            TargetLuminance = 0.21f,
            MinimumLuminance = 0.22f,
            LuminanceEqualization = 0.23f,
            HueSpreadReference = 0.24f,
            MaximumLuminanceScale = 1.25f,
            DarkLuminanceThreshold = 0.026f,
            DuplicateThreshold = 0.027f,
            HueRedistribution = 0.28f,
        };
        asset.Settings.ForegroundTilePositionStep = 18f;
        asset.Settings.ForegroundPositionAdvancePerSecond = 19f;
        asset.Settings.ForegroundEnergyCrawlSpeedMultiplier = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.ForegroundDropRibbonWindowBeats = 221;
        asset.Settings.ForegroundDropRibbonFlowCyclesPerBeatAtLanding = 222f;
        asset.Settings.ForegroundDropRibbonBrightness = 223f;
        asset.Settings.BackgroundWaveformName = "sentinel entry";
        asset.Settings.BackgroundWaveformBrightnessFloor = 22f;
        asset.Settings.BackgroundWaveformPeakBrightnessTarget = 24f;
        asset.Settings.BackgroundDropTileHueStep = 25f;
        asset.Settings.BackgroundDropHueRate = 26f;
        asset.Settings.BackgroundDropValue = 27f;
        asset.Settings.ForegroundFillBlackAndWhiteProbability = 0.29f;
        asset.Settings.ForegroundFillBrightnessLift = 0.31f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(AnimateShapes), TempAssetFolder);

        var defaults = AnimateShapes.SyncDefaults;
        Assert.That(asset.Settings.BackgroundHueRate, Is.EqualTo(defaults.BackgroundHueRate));
        AssertPaletteConditioningEqual(
            asset.Settings.ForegroundPaletteConditioning,
            defaults.ForegroundPaletteConditioning);
        Assert.That(
            asset.Settings.ForegroundTilePositionStep,
            Is.EqualTo(defaults.ForegroundTilePositionStep));
        Assert.That(
            asset.Settings.ForegroundPositionAdvancePerSecond,
            Is.EqualTo(defaults.ForegroundPositionAdvancePerSecond));
        AssertFloatRangeEqual(
            asset.Settings.ForegroundEnergyCrawlSpeedMultiplier,
            defaults.ForegroundEnergyCrawlSpeedMultiplier);
        Assert.That(
            asset.Settings.ForegroundDropRibbonWindowBeats,
            Is.EqualTo(defaults.ForegroundDropRibbonWindowBeats));
        Assert.That(
            asset.Settings.ForegroundDropRibbonFlowCyclesPerBeatAtLanding,
            Is.EqualTo(defaults.ForegroundDropRibbonFlowCyclesPerBeatAtLanding));
        Assert.That(
            asset.Settings.ForegroundDropRibbonBrightness,
            Is.EqualTo(defaults.ForegroundDropRibbonBrightness));
        Assert.That(
            asset.Settings.BackgroundWaveformName,
            Is.EqualTo(defaults.BackgroundWaveformName));
        Assert.That(
            asset.Settings.BackgroundWaveformBrightnessFloor,
            Is.EqualTo(defaults.BackgroundWaveformBrightnessFloor));
        Assert.That(
            asset.Settings.BackgroundWaveformPeakBrightnessTarget,
            Is.EqualTo(defaults.BackgroundWaveformPeakBrightnessTarget));
        Assert.That(
            asset.Settings.BackgroundDropTileHueStep,
            Is.EqualTo(defaults.BackgroundDropTileHueStep));
        Assert.That(
            asset.Settings.BackgroundDropHueRate,
            Is.EqualTo(defaults.BackgroundDropHueRate));
        Assert.That(
            asset.Settings.BackgroundDropValue,
            Is.EqualTo(defaults.BackgroundDropValue));
        Assert.That(
            asset.Settings.ForegroundFillBlackAndWhiteProbability,
            Is.EqualTo(defaults.ForegroundFillBlackAndWhiteProbability));
        Assert.That(
            asset.Settings.ForegroundFillBrightnessLift,
            Is.EqualTo(defaults.ForegroundFillBrightnessLift));
    }

    /// <summary>
    /// Vortex Standalone Defaults resolve as fresh, mutually independent copies without pinning
    /// authored tuning values that are judged on the wall.
    /// </summary>
    [Test]
    public void VortexStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Vortex.StandaloneDefaults;
        var second = Vortex.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.SpinnerCount, Is.Not.SameAs(second.SpinnerCount));
        AssertIntRangeEqual(first.SpinnerCount, second.SpinnerCount);
        Assert.That(first.AngularSpeedDegreesPerSecond, Is.Not.SameAs(second.AngularSpeedDegreesPerSecond));
        AssertIntRangeEqual(first.AngularSpeedDegreesPerSecond, second.AngularSpeedDegreesPerSecond);
        Assert.That(first.ReverseDirectionRoll, Is.Not.SameAs(second.ReverseDirectionRoll));
        AssertIntRangeEqual(first.ReverseDirectionRoll, second.ReverseDirectionRoll);
        Assert.That(first.SpinnerTwist, Is.Not.SameAs(second.SpinnerTwist));
        AssertFloatRangeEqual(first.SpinnerTwist, second.SpinnerTwist);
        Assert.That(first.DistortionModeRoll, Is.Not.SameAs(second.DistortionModeRoll));
        AssertIntRangeEqual(first.DistortionModeRoll, second.DistortionModeRoll);
        Assert.That(first.RingScaleAtRest, Is.EqualTo(second.RingScaleAtRest));
        Assert.That(first.HorizontalRadius, Is.EqualTo(second.HorizontalRadius));
        Assert.That(first.VerticalRadius, Is.EqualTo(second.VerticalRadius));
        Assert.That(first.SpinnerArms, Is.EqualTo(second.SpinnerArms));
        Assert.That(first.BrightnessAtRest, Is.EqualTo(second.BrightnessAtRest));
        Assert.That(first.TimeScale, Is.EqualTo(second.TimeScale));
    }

    /// <summary>
    /// Restore replaces every edited Vortex Standalone Setting and Rail with the current file-local
    /// Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryVortexValue()
    {
        var asset = (VortexStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Vortex),
            TempAssetFolder);
        asset.Settings.SpinnerCount = new IntRange(17, 18, 16, 19);
        asset.Settings.AngularSpeedDegreesPerSecond = new IntRange(20, 21, 19, 22);
        asset.Settings.ReverseDirectionRoll = new IntRange(23, 24, 22, 25);
        asset.Settings.SpinnerTwist = new FloatRange(26f, 27f, 25f, 28f);
        asset.Settings.DistortionModeRoll = new IntRange(29, 30, 28, 31);
        asset.Settings.RingScaleAtRest = 32f;
        asset.Settings.HorizontalRadius = 33f;
        asset.Settings.VerticalRadius = 34f;
        asset.Settings.SpinnerArms = 35;
        asset.Settings.BrightnessAtRest = 36f;
        asset.Settings.TimeScale = 37f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Vortex), TempAssetFolder);

        var defaults = Vortex.StandaloneDefaults;
        AssertIntRangeEqual(asset.Settings.SpinnerCount, defaults.SpinnerCount);
        AssertIntRangeEqual(
            asset.Settings.AngularSpeedDegreesPerSecond,
            defaults.AngularSpeedDegreesPerSecond);
        AssertIntRangeEqual(asset.Settings.ReverseDirectionRoll, defaults.ReverseDirectionRoll);
        AssertFloatRangeEqual(asset.Settings.SpinnerTwist, defaults.SpinnerTwist);
        AssertIntRangeEqual(asset.Settings.DistortionModeRoll, defaults.DistortionModeRoll);
        Assert.That(asset.Settings.RingScaleAtRest, Is.EqualTo(defaults.RingScaleAtRest));
        Assert.That(asset.Settings.HorizontalRadius, Is.EqualTo(defaults.HorizontalRadius));
        Assert.That(asset.Settings.VerticalRadius, Is.EqualTo(defaults.VerticalRadius));
        Assert.That(asset.Settings.SpinnerArms, Is.EqualTo(defaults.SpinnerArms));
        Assert.That(asset.Settings.BrightnessAtRest, Is.EqualTo(defaults.BrightnessAtRest));
        Assert.That(asset.Settings.TimeScale, Is.EqualTo(defaults.TimeScale));
    }

    /// <summary>
    /// Restore replaces every edited Vortex Sync Setting and Rail with the current file-local Sync
    /// Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryVortexValue()
    {
        var asset = (VortexSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Vortex),
            TempAssetFolder);
        asset.Settings.SpinnerCount = new IntRange(17, 18, 16, 19);
        asset.Settings.AngularSpeedDegreesPerSecond = new IntRange(20, 21, 19, 22);
        asset.Settings.ReverseDirectionRoll = new IntRange(23, 24, 22, 25);
        asset.Settings.SpinnerTwist = new FloatRange(26f, 27f, 25f, 28f);
        asset.Settings.DistortionModeRoll = new IntRange(29, 30, 28, 31);
        asset.Settings.RingScaleAtRest = 32f;
        asset.Settings.HorizontalRadius = 33f;
        asset.Settings.VerticalRadius = 34f;
        asset.Settings.SpinnerArms = 35;
        asset.Settings.BrightnessAtRest = 36f;
        asset.Settings.TimeScale = 37f;
        asset.Settings.BrightnessAtWaveformTrough = 38f;
        asset.Settings.HueShiftAtWaveformPeak = 39f;
        asset.Settings.TimeStepAtWaveformPeak = 40f;
        asset.Settings.DropSpinDecayBeats = 41;
        asset.Settings.DropSpinSpeedAtStart = 42f;
        asset.Settings.DropRingCloseCountdownBeats = new IntRange(43, 44, 42, 45);
        asset.Settings.DropRingClosedScale = 46f;
        asset.Settings.FillSaturation = 47f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Vortex), TempAssetFolder);

        var defaults = Vortex.SyncDefaults;
        AssertIntRangeEqual(asset.Settings.SpinnerCount, defaults.SpinnerCount);
        AssertIntRangeEqual(
            asset.Settings.AngularSpeedDegreesPerSecond,
            defaults.AngularSpeedDegreesPerSecond);
        AssertIntRangeEqual(asset.Settings.ReverseDirectionRoll, defaults.ReverseDirectionRoll);
        AssertFloatRangeEqual(asset.Settings.SpinnerTwist, defaults.SpinnerTwist);
        AssertIntRangeEqual(asset.Settings.DistortionModeRoll, defaults.DistortionModeRoll);
        Assert.That(asset.Settings.RingScaleAtRest, Is.EqualTo(defaults.RingScaleAtRest));
        Assert.That(asset.Settings.HorizontalRadius, Is.EqualTo(defaults.HorizontalRadius));
        Assert.That(asset.Settings.VerticalRadius, Is.EqualTo(defaults.VerticalRadius));
        Assert.That(asset.Settings.SpinnerArms, Is.EqualTo(defaults.SpinnerArms));
        Assert.That(asset.Settings.BrightnessAtRest, Is.EqualTo(defaults.BrightnessAtRest));
        Assert.That(asset.Settings.TimeScale, Is.EqualTo(defaults.TimeScale));
        Assert.That(
            asset.Settings.BrightnessAtWaveformTrough,
            Is.EqualTo(defaults.BrightnessAtWaveformTrough));
        Assert.That(asset.Settings.HueShiftAtWaveformPeak, Is.EqualTo(defaults.HueShiftAtWaveformPeak));
        Assert.That(asset.Settings.TimeStepAtWaveformPeak, Is.EqualTo(defaults.TimeStepAtWaveformPeak));
        Assert.That(asset.Settings.DropSpinDecayBeats, Is.EqualTo(defaults.DropSpinDecayBeats));
        Assert.That(asset.Settings.DropSpinSpeedAtStart, Is.EqualTo(defaults.DropSpinSpeedAtStart));
        AssertIntRangeEqual(
            asset.Settings.DropRingCloseCountdownBeats,
            defaults.DropRingCloseCountdownBeats);
        Assert.That(asset.Settings.DropRingClosedScale, Is.EqualTo(defaults.DropRingClosedScale));
        Assert.That(asset.Settings.FillSaturation, Is.EqualTo(defaults.FillSaturation));
    }

    /// <summary>
    /// ShapeGlitch Standalone Defaults resolve as fresh, mutually independent copies without
    /// pinning authored tuning values that are judged on the wall.
    /// </summary>
    [Test]
    public void ShapeGlitchStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = ShapeGlitch.StandaloneDefaults;
        var second = ShapeGlitch.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.ModeRoll, Is.Not.SameAs(second.ModeRoll));
        AssertIntRangeEqual(first.ModeRoll, second.ModeRoll);
        Assert.That(first.HighlightCount, Is.Not.SameAs(second.HighlightCount));
        AssertIntRangeEqual(first.HighlightCount, second.HighlightCount);
        Assert.That(first.ShapeRoll, Is.Not.SameAs(second.ShapeRoll));
        AssertIntRangeEqual(first.ShapeRoll, second.ShapeRoll);
        Assert.That(first.HighlightColorValue, Is.EqualTo(second.HighlightColorValue));
        Assert.That(first.SpawnRoll, Is.Not.SameAs(second.SpawnRoll));
        AssertIntRangeEqual(first.SpawnRoll, second.SpawnRoll);
        Assert.That(first.HighlightInitialIntensity, Is.EqualTo(second.HighlightInitialIntensity));
        Assert.That(first.FadeIntensityPerFrame, Is.EqualTo(second.FadeIntensityPerFrame));
        Assert.That(first.BlinkIntensityPerFrame, Is.EqualTo(second.BlinkIntensityPerFrame));
        Assert.That(first.BlinkIntensityLimit, Is.EqualTo(second.BlinkIntensityLimit));
        Assert.That(first.HueDriftPerShape, Is.EqualTo(second.HueDriftPerShape));
    }

    /// <summary>
    /// Restore replaces every edited ShapeGlitch Standalone Setting and Rail with the current
    /// file-local Standalone Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryShapeGlitchValue()
    {
        var asset = (ShapeGlitchStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(ShapeGlitch),
            TempAssetFolder);
        asset.Settings.ModeRoll = new IntRange(17, 18, 16, 19);
        asset.Settings.HighlightCount = new IntRange(20, 21, 19, 22);
        asset.Settings.ShapeRoll = new IntRange(23, 24, 22, 25);
        asset.Settings.HighlightColorValue = 26f;
        asset.Settings.SpawnRoll = new IntRange(27, 28, 26, 29);
        asset.Settings.HighlightInitialIntensity = 30f;
        asset.Settings.FadeIntensityPerFrame = 31f;
        asset.Settings.BlinkIntensityPerFrame = 32f;
        asset.Settings.BlinkIntensityLimit = 33f;
        asset.Settings.HueDriftPerShape = 34f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(ShapeGlitch),
            TempAssetFolder);

        var defaults = ShapeGlitch.StandaloneDefaults;
        AssertIntRangeEqual(asset.Settings.ModeRoll, defaults.ModeRoll);
        AssertIntRangeEqual(asset.Settings.HighlightCount, defaults.HighlightCount);
        AssertIntRangeEqual(asset.Settings.ShapeRoll, defaults.ShapeRoll);
        Assert.That(asset.Settings.HighlightColorValue, Is.EqualTo(defaults.HighlightColorValue));
        AssertIntRangeEqual(asset.Settings.SpawnRoll, defaults.SpawnRoll);
        Assert.That(
            asset.Settings.HighlightInitialIntensity,
            Is.EqualTo(defaults.HighlightInitialIntensity));
        Assert.That(asset.Settings.FadeIntensityPerFrame, Is.EqualTo(defaults.FadeIntensityPerFrame));
        Assert.That(asset.Settings.BlinkIntensityPerFrame, Is.EqualTo(defaults.BlinkIntensityPerFrame));
        Assert.That(asset.Settings.BlinkIntensityLimit, Is.EqualTo(defaults.BlinkIntensityLimit));
        Assert.That(asset.Settings.HueDriftPerShape, Is.EqualTo(defaults.HueDriftPerShape));
    }

    /// <summary>
    /// Restore replaces every edited ShapeGlitch Sync Setting and Rail with the current file-local
    /// Sync Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryShapeGlitchValue()
    {
        var asset = (ShapeGlitchSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(ShapeGlitch),
            TempAssetFolder);
        asset.Settings.ModeRoll = new IntRange(17, 18, 16, 19);
        asset.Settings.HighlightCount = new IntRange(20, 21, 19, 22);
        asset.Settings.ShapeRoll = new IntRange(23, 24, 22, 25);
        asset.Settings.HighlightColorValue = 26f;
        asset.Settings.SpawnRoll = new IntRange(27, 28, 26, 29);
        asset.Settings.HighlightInitialIntensity = 30f;
        asset.Settings.FadeIntensityPerFrame = 31f;
        asset.Settings.BlinkIntensityPerFrame = 32f;
        asset.Settings.BlinkIntensityLimit = 33f;
        asset.Settings.HueDriftPerShape = 34f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(ShapeGlitch), TempAssetFolder);

        var defaults = ShapeGlitch.SyncDefaults;
        AssertIntRangeEqual(asset.Settings.ModeRoll, defaults.ModeRoll);
        AssertIntRangeEqual(asset.Settings.HighlightCount, defaults.HighlightCount);
        AssertIntRangeEqual(asset.Settings.ShapeRoll, defaults.ShapeRoll);
        Assert.That(asset.Settings.HighlightColorValue, Is.EqualTo(defaults.HighlightColorValue));
        AssertIntRangeEqual(asset.Settings.SpawnRoll, defaults.SpawnRoll);
        Assert.That(
            asset.Settings.HighlightInitialIntensity,
            Is.EqualTo(defaults.HighlightInitialIntensity));
        Assert.That(asset.Settings.FadeIntensityPerFrame, Is.EqualTo(defaults.FadeIntensityPerFrame));
        Assert.That(asset.Settings.BlinkIntensityPerFrame, Is.EqualTo(defaults.BlinkIntensityPerFrame));
        Assert.That(asset.Settings.BlinkIntensityLimit, Is.EqualTo(defaults.BlinkIntensityLimit));
        Assert.That(asset.Settings.HueDriftPerShape, Is.EqualTo(defaults.HueDriftPerShape));
    }

    /// <summary>
    /// RandomEffectsMixer Standalone Defaults resolve as fresh, mutually independent copies without
    /// pinning the authored look in the test.
    /// </summary>
    [Test]
    public void RandomEffectsMixerStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = RandomEffectsMixer.StandaloneDefaults;
        var second = RandomEffectsMixer.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.ChildCount, Is.Not.SameAs(second.ChildCount));
        AssertIntRangeEqual(first.ChildCount, second.ChildCount);
        Assert.That(first.MixGain, Is.EqualTo(second.MixGain));
    }

    /// <summary>
    /// Restore replaces every edited RandomEffectsMixer Standalone Setting and Rail with the current
    /// file-local Standalone Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryRandomEffectsMixerValue()
    {
        var asset = (RandomEffectsMixerStandaloneSettingsAsset)
            EffectStandaloneSettingsAssetUtility.EnsureAsset(
                typeof(RandomEffectsMixer),
                TempAssetFolder);
        asset.Settings.ChildCount = new IntRange(17, 18, 16, 19);
        asset.Settings.MixGain = 20f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(RandomEffectsMixer),
            TempAssetFolder);

        var defaults = RandomEffectsMixer.StandaloneDefaults;
        AssertIntRangeEqual(asset.Settings.ChildCount, defaults.ChildCount);
        Assert.That(asset.Settings.MixGain, Is.EqualTo(defaults.MixGain));
    }

    /// <summary>
    /// Restore replaces every edited RandomEffectsMixer Sync Setting and Rail with the current
    /// file-local Sync Defaults, without pinning authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryRandomEffectsMixerValue()
    {
        var asset = (RandomEffectsMixerSyncSettingsAsset)
            EffectSyncSettingsAssetUtility.EnsureAsset(
                typeof(RandomEffectsMixer),
                TempAssetFolder);
        asset.Settings.ChildCount = new IntRange(17, 18, 16, 19);
        asset.Settings.MixGain = 20f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(
            typeof(RandomEffectsMixer),
            TempAssetFolder);

        var defaults = RandomEffectsMixer.SyncDefaults;
        AssertIntRangeEqual(asset.Settings.ChildCount, defaults.ChildCount);
        Assert.That(asset.Settings.MixGain, Is.EqualTo(defaults.MixGain));
    }

    /// <summary>
    /// Verifies TileShapes resolves independent Standalone Settings copies whose values and Rails
    /// match the authored Standalone Defaults.
    /// </summary>
    [Test]
    public void TileShapesStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = TileShapes.StandaloneDefaults;
        var second = TileShapes.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.RandomColorThreshold, Is.EqualTo(second.RandomColorThreshold));
        Assert.That(first.FixedHue, Is.Not.SameAs(second.FixedHue));
        AssertFloatRangeEqual(first.FixedHue, second.FixedHue);
        Assert.That(first.ShapeSelector, Is.Not.SameAs(second.ShapeSelector));
        AssertIntRangeEqual(first.ShapeSelector, second.ShapeSelector);
        Assert.That(
            first.RandomColorBrightnessAtPeak,
            Is.EqualTo(second.RandomColorBrightnessAtPeak));
        Assert.That(first.FixedHueShiftAtPeak, Is.EqualTo(second.FixedHueShiftAtPeak));
        Assert.That(first.FlashCountDivisor, Is.EqualTo(second.FlashCountDivisor));
        Assert.That(first.RandomHue, Is.Not.SameAs(second.RandomHue));
        AssertFloatRangeEqual(first.RandomHue, second.RandomHue);
    }

    /// <summary>
    /// Verifies restoring TileShapes Standalone Settings replaces every saved value and range Rail
    /// with its in-file Standalone Default.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryTileShapesValue()
    {
        var asset = (TileShapesStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(TileShapes),
            TempAssetFolder);
        asset.Settings.RandomColorThreshold = 0.17f;
        asset.Settings.FixedHue = new FloatRange(18f, 19f, 17f, 20f);
        asset.Settings.ShapeSelector = new IntRange(21, 22, 20, 23);
        asset.Settings.RandomColorBrightnessAtPeak = 24f;
        asset.Settings.FixedHueShiftAtPeak = 25f;
        asset.Settings.FlashCountDivisor = 26;
        asset.Settings.RandomHue = new FloatRange(27f, 28f, 26f, 29f);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(TileShapes),
            TempAssetFolder);

        var defaults = TileShapes.StandaloneDefaults;
        Assert.That(asset.Settings.RandomColorThreshold, Is.EqualTo(defaults.RandomColorThreshold));
        AssertFloatRangeEqual(asset.Settings.FixedHue, defaults.FixedHue);
        AssertIntRangeEqual(asset.Settings.ShapeSelector, defaults.ShapeSelector);
        Assert.That(
            asset.Settings.RandomColorBrightnessAtPeak,
            Is.EqualTo(defaults.RandomColorBrightnessAtPeak));
        Assert.That(asset.Settings.FixedHueShiftAtPeak, Is.EqualTo(defaults.FixedHueShiftAtPeak));
        Assert.That(asset.Settings.FlashCountDivisor, Is.EqualTo(defaults.FlashCountDivisor));
        AssertFloatRangeEqual(asset.Settings.RandomHue, defaults.RandomHue);
    }

    /// <summary>
    /// Verifies restoring TileShapes Sync Settings replaces every saved scalar and range Rail with
    /// its in-file Sync Default.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryTileShapesValue()
    {
        var asset = (TileShapesSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(TileShapes),
            TempAssetFolder);
        asset.Settings.RandomColorThreshold = 0.17f;
        asset.Settings.FixedHue = new FloatRange(18f, 19f, 17f, 20f);
        asset.Settings.ShapeSelector = new IntRange(21, 22, 20, 23);
        asset.Settings.RandomColorBrightness = new FloatRange(24f, 25f, 23f, 26f);
        asset.Settings.FixedHueShift = new FloatRange(27f, 28f, 26f, 29f);
        asset.Settings.FlashCountDivisor = 30;
        asset.Settings.RandomHue = new FloatRange(31f, 32f, 30f, 33f);

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(TileShapes), TempAssetFolder);

        var defaults = TileShapes.SyncDefaults;
        Assert.That(asset.Settings.RandomColorThreshold, Is.EqualTo(defaults.RandomColorThreshold));
        AssertFloatRangeEqual(asset.Settings.FixedHue, defaults.FixedHue);
        AssertIntRangeEqual(asset.Settings.ShapeSelector, defaults.ShapeSelector);
        AssertFloatRangeEqual(
            asset.Settings.RandomColorBrightness,
            defaults.RandomColorBrightness);
        AssertFloatRangeEqual(asset.Settings.FixedHueShift, defaults.FixedHueShift);
        Assert.That(asset.Settings.FlashCountDivisor, Is.EqualTo(defaults.FlashCountDivisor));
        AssertFloatRangeEqual(asset.Settings.RandomHue, defaults.RandomHue);
    }

    /// <summary>
    /// NoiseMixer Standalone Defaults resolve as fresh, mutually independent copies without pinning
    /// the authored look in the test.
    /// </summary>
    [Test]
    public void NoiseMixerStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = NoiseMixer.StandaloneDefaults;
        var second = NoiseMixer.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BorderSaturation, Is.EqualTo(second.BorderSaturation));
        Assert.That(first.BorderValue, Is.EqualTo(second.BorderValue));
        Assert.That(first.DistortionMode, Is.Not.SameAs(second.DistortionMode));
        AssertIntRangeEqual(first.DistortionMode, second.DistortionMode);
        Assert.That(first.NoiseScale, Is.EqualTo(second.NoiseScale));
        Assert.That(first.MaskWidth, Is.Not.SameAs(second.MaskWidth));
        AssertFloatRangeEqual(first.MaskWidth, second.MaskWidth);
    }

    /// <summary>
    /// Restore replaces every edited NoiseMixer Standalone Setting and Rail with the current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryNoiseMixerValue()
    {
        var asset = (NoiseMixerStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(NoiseMixer),
            TempAssetFolder);
        asset.Settings.BorderSaturation = 0.17f;
        asset.Settings.BorderValue = 0.18f;
        asset.Settings.DistortionMode = new IntRange(19, 20, 18, 21);
        asset.Settings.NoiseScale = 22f;
        asset.Settings.MaskWidth = new FloatRange(23f, 24f, 22f, 25f);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(NoiseMixer),
            TempAssetFolder);

        var defaults = NoiseMixer.StandaloneDefaults;
        Assert.That(asset.Settings.BorderSaturation, Is.EqualTo(defaults.BorderSaturation));
        Assert.That(asset.Settings.BorderValue, Is.EqualTo(defaults.BorderValue));
        AssertIntRangeEqual(asset.Settings.DistortionMode, defaults.DistortionMode);
        Assert.That(asset.Settings.NoiseScale, Is.EqualTo(defaults.NoiseScale));
        AssertFloatRangeEqual(asset.Settings.MaskWidth, defaults.MaskWidth);
    }

    /// <summary>
    /// Restore replaces every edited NoiseMixer Sync Setting and Rail with the current file-local
    /// Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryNoiseMixerValue()
    {
        var asset = (NoiseMixerSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(NoiseMixer),
            TempAssetFolder);
        asset.Settings.BorderSaturation = 0.17f;
        asset.Settings.BorderValue = 0.18f;
        asset.Settings.DistortionMode = new IntRange(19, 20, 18, 21);
        asset.Settings.NoiseScale = 22f;
        asset.Settings.MaskWidth = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.RhythmTimeOffset = 26f;
        asset.Settings.DropSlowdownBeats = 27;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(NoiseMixer), TempAssetFolder);

        var defaults = NoiseMixer.SyncDefaults;
        Assert.That(asset.Settings.BorderSaturation, Is.EqualTo(defaults.BorderSaturation));
        Assert.That(asset.Settings.BorderValue, Is.EqualTo(defaults.BorderValue));
        AssertIntRangeEqual(asset.Settings.DistortionMode, defaults.DistortionMode);
        Assert.That(asset.Settings.NoiseScale, Is.EqualTo(defaults.NoiseScale));
        AssertFloatRangeEqual(asset.Settings.MaskWidth, defaults.MaskWidth);
        Assert.That(asset.Settings.RhythmTimeOffset, Is.EqualTo(defaults.RhythmTimeOffset));
        Assert.That(asset.Settings.DropSlowdownBeats, Is.EqualTo(defaults.DropSlowdownBeats));
    }

    /// <summary>
    /// YinYangMixer Standalone Defaults resolve as fresh, deeply independent copies without
    /// pinning the authored look that ADR-0013 leaves to judgment on the wall.
    /// </summary>
    [Test]
    public void YinYangMixerStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = YinYangMixer.StandaloneDefaults;
        var second = YinYangMixer.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.SpinRate, Is.Not.SameAs(second.SpinRate));
        AssertFloatRangeEqual(first.SpinRate, second.SpinRate);
        Assert.That(first.RadialAngleScale, Is.Not.SameAs(second.RadialAngleScale));
        AssertFloatRangeEqual(first.RadialAngleScale, second.RadialAngleScale);
        Assert.That(first.RibbonHalfWidth, Is.EqualTo(second.RibbonHalfWidth));
        Assert.That(first.RibbonPalettePosition, Is.EqualTo(second.RibbonPalettePosition));
        Assert.That(first.WaveformBrightnessPeak, Is.EqualTo(second.WaveformBrightnessPeak));
    }

    /// <summary>
    /// Restore replaces every edited YinYangMixer Standalone Setting and Rail with the current
    /// file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryYinYangMixerValue()
    {
        var asset = (YinYangMixerStandaloneSettingsAsset)
            EffectStandaloneSettingsAssetUtility.EnsureAsset(
                typeof(YinYangMixer),
                TempAssetFolder);
        asset.Settings.SpinRate = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.RadialAngleScale = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.RibbonHalfWidth = 23f;
        asset.Settings.RibbonPalettePosition = 0.24f;
        asset.Settings.WaveformBrightnessPeak = 0.25f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(YinYangMixer),
            TempAssetFolder);

        var defaults = YinYangMixer.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.SpinRate, defaults.SpinRate);
        AssertFloatRangeEqual(asset.Settings.RadialAngleScale, defaults.RadialAngleScale);
        Assert.That(asset.Settings.RibbonHalfWidth, Is.EqualTo(defaults.RibbonHalfWidth));
        Assert.That(asset.Settings.RibbonPalettePosition, Is.EqualTo(defaults.RibbonPalettePosition));
        Assert.That(asset.Settings.WaveformBrightnessPeak, Is.EqualTo(defaults.WaveformBrightnessPeak));
    }

    /// <summary>
    /// Restore replaces every edited YinYangMixer Sync Setting and Rail with the current file-local
    /// Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryYinYangMixerValue()
    {
        var asset = (YinYangMixerSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(YinYangMixer),
            TempAssetFolder);
        asset.Settings.SpinRate = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.RadialAngleScale = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.RibbonHalfWidth = 23f;
        asset.Settings.RibbonPalettePosition = 0.24f;
        asset.Settings.WaveformBrightnessFloor = 0.25f;
        asset.Settings.WaveformBrightnessPeak = 0.26f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(YinYangMixer), TempAssetFolder);

        var defaults = YinYangMixer.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.SpinRate, defaults.SpinRate);
        AssertFloatRangeEqual(asset.Settings.RadialAngleScale, defaults.RadialAngleScale);
        Assert.That(asset.Settings.RibbonHalfWidth, Is.EqualTo(defaults.RibbonHalfWidth));
        Assert.That(asset.Settings.RibbonPalettePosition, Is.EqualTo(defaults.RibbonPalettePosition));
        Assert.That(asset.Settings.WaveformBrightnessFloor, Is.EqualTo(defaults.WaveformBrightnessFloor));
        Assert.That(asset.Settings.WaveformBrightnessPeak, Is.EqualTo(defaults.WaveformBrightnessPeak));
    }

    /// <summary>Lightning Standalone Defaults resolve as fresh copies without pinning authored values.</summary>
    [Test]
    public void LightningStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Lightning.StandaloneDefaults;
        var second = Lightning.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.StartHueDelta, Is.EqualTo(second.StartHueDelta));
        Assert.That(first.RayHueDelta, Is.EqualTo(second.RayHueDelta));
        Assert.That(first.TileHueDelta, Is.EqualTo(second.TileHueDelta));
        Assert.That(first.BoltBrightness, Is.EqualTo(second.BoltBrightness));
    }

    /// <summary>Restore copies every Lightning Standalone Setting from the current file-local defaults.</summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryLightningValue()
    {
        var asset = (LightningStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Lightning), TempAssetFolder);
        asset.Settings.StartHueDelta = 17f;
        asset.Settings.RayHueDelta = 18f;
        asset.Settings.TileHueDelta = 19f;
        asset.Settings.BoltBrightness = 20f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Lightning), TempAssetFolder);

        var defaults = Lightning.StandaloneDefaults;
        Assert.That(asset.Settings.StartHueDelta, Is.EqualTo(defaults.StartHueDelta));
        Assert.That(asset.Settings.RayHueDelta, Is.EqualTo(defaults.RayHueDelta));
        Assert.That(asset.Settings.TileHueDelta, Is.EqualTo(defaults.TileHueDelta));
        Assert.That(asset.Settings.BoltBrightness, Is.EqualTo(defaults.BoltBrightness));
    }

    /// <summary>Restore copies every Lightning Sync Setting and brightness Rail from the file-local defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryLightningValue()
    {
        var asset = (LightningSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Lightning), TempAssetFolder);
        asset.Settings.StartHueDelta = 17f;
        asset.Settings.RayHueDelta = 18f;
        asset.Settings.TileHueDelta = 19f;
        asset.Settings.WaveformBrightness = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.WaveformHueOffset = 23f;
        asset.Settings.DropBars = 24;
        asset.Settings.DropValueLift = 25f;
        asset.Settings.DropFlickerDepth = 26f;
        asset.Settings.DropFlickerHz = 27f;
        asset.Settings.DropFieldFlood = 28f;
        asset.Settings.DropFieldWhiteFlash = 29f;
        asset.Settings.DropTrailFade = 30f;
        asset.Settings.FillRewalkDuration = Duration.Whole;
        asset.Settings.FillStrobeDuration = Duration.Half;
        asset.Settings.FillStrobeFloor = 31f;
        asset.Settings.FillStrobeDuty = 32f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Lightning), TempAssetFolder);

        var defaults = Lightning.SyncDefaults;
        Assert.That(asset.Settings.StartHueDelta, Is.EqualTo(defaults.StartHueDelta));
        Assert.That(asset.Settings.RayHueDelta, Is.EqualTo(defaults.RayHueDelta));
        Assert.That(asset.Settings.TileHueDelta, Is.EqualTo(defaults.TileHueDelta));
        AssertFloatRangeEqual(asset.Settings.WaveformBrightness, defaults.WaveformBrightness);
        Assert.That(asset.Settings.WaveformHueOffset, Is.EqualTo(defaults.WaveformHueOffset));
        Assert.That(asset.Settings.DropBars, Is.EqualTo(defaults.DropBars));
        Assert.That(asset.Settings.DropValueLift, Is.EqualTo(defaults.DropValueLift));
        Assert.That(asset.Settings.DropFlickerDepth, Is.EqualTo(defaults.DropFlickerDepth));
        Assert.That(asset.Settings.DropFlickerHz, Is.EqualTo(defaults.DropFlickerHz));
        Assert.That(asset.Settings.DropFieldFlood, Is.EqualTo(defaults.DropFieldFlood));
        Assert.That(asset.Settings.DropFieldWhiteFlash, Is.EqualTo(defaults.DropFieldWhiteFlash));
        Assert.That(asset.Settings.DropTrailFade, Is.EqualTo(defaults.DropTrailFade));
        Assert.That(asset.Settings.FillRewalkDuration, Is.EqualTo(defaults.FillRewalkDuration));
        Assert.That(asset.Settings.FillStrobeDuration, Is.EqualTo(defaults.FillStrobeDuration));
        Assert.That(asset.Settings.FillStrobeFloor, Is.EqualTo(defaults.FillStrobeFloor));
        Assert.That(asset.Settings.FillStrobeDuty, Is.EqualTo(defaults.FillStrobeDuty));
    }

    /// <summary>
    /// Petals Standalone Defaults resolve as fresh, independently editable copies without pinning
    /// the authored tuning values judged on the wall.
    /// </summary>
    [Test]
    public void PetalsStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Petals.StandaloneDefaults;
        var second = Petals.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.LayerHue, Is.Not.SameAs(second.LayerHue));
        AssertFloatRangeEqual(first.LayerHue, second.LayerHue);
        Assert.That(first.LayerSaturation, Is.Not.SameAs(second.LayerSaturation));
        AssertFloatRangeEqual(first.LayerSaturation, second.LayerSaturation);
        Assert.That(first.BackgroundHue, Is.Not.SameAs(second.BackgroundHue));
        AssertFloatRangeEqual(first.BackgroundHue, second.BackgroundHue);
        Assert.That(first.BackgroundHueAdvance, Is.EqualTo(second.BackgroundHueAdvance));
        Assert.That(first.WaveformBrightnessAtPeak, Is.EqualTo(second.WaveformBrightnessAtPeak));
        Assert.That(first.TileHueSpread, Is.EqualTo(second.TileHueSpread));
        Assert.That(first.LayerHueAdvance, Is.EqualTo(second.LayerHueAdvance));
        Assert.That(first.LayerMask, Is.Not.SameAs(second.LayerMask));
        AssertIntRangeEqual(first.LayerMask, second.LayerMask);
    }

    /// <summary>
    /// Restore replaces every edited Petals Standalone Setting and Rail with the current in-file
    /// Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryPetalsValue()
    {
        var asset = (PetalsStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Petals),
            TempAssetFolder);
        asset.Settings.LayerHue = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.LayerSaturation = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.BackgroundHue = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.BackgroundHueAdvance = 26f;
        asset.Settings.WaveformBrightnessAtPeak = 27f;
        asset.Settings.TileHueSpread = 28f;
        asset.Settings.LayerHueAdvance = 29f;
        asset.Settings.LayerMask = new IntRange(30, 31, 29, 32);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Petals), TempAssetFolder);

        var defaults = Petals.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.LayerHue, defaults.LayerHue);
        AssertFloatRangeEqual(asset.Settings.LayerSaturation, defaults.LayerSaturation);
        AssertFloatRangeEqual(asset.Settings.BackgroundHue, defaults.BackgroundHue);
        Assert.That(asset.Settings.BackgroundHueAdvance, Is.EqualTo(defaults.BackgroundHueAdvance));
        Assert.That(asset.Settings.WaveformBrightnessAtPeak, Is.EqualTo(defaults.WaveformBrightnessAtPeak));
        Assert.That(asset.Settings.TileHueSpread, Is.EqualTo(defaults.TileHueSpread));
        Assert.That(asset.Settings.LayerHueAdvance, Is.EqualTo(defaults.LayerHueAdvance));
        AssertIntRangeEqual(asset.Settings.LayerMask, defaults.LayerMask);
    }

    /// <summary>Restore replaces every edited Petals Sync Setting and Rail with the in-file Sync Defaults.</summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryPetalsValue()
    {
        var asset = (PetalsSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Petals),
            TempAssetFolder);
        asset.Settings.LayerHue = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.LayerSaturation = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.BackgroundHue = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.BackgroundHueAdvance = 26f;
        asset.Settings.WaveformBrightnessAtTrough = 27f;
        asset.Settings.WaveformBrightnessAtPeak = 28f;
        asset.Settings.WaveformHueShiftAtPeak = 29f;
        asset.Settings.LayerMask = new IntRange(30, 31, 29, 32);
        asset.Settings.SparkleChance = 0.33f;
        asset.Settings.SparklePhaseTileIndexStep = 34f;
        asset.Settings.SparklePhaseSpeed = 35f;
        asset.Settings.SparkleValue = 36f;
        asset.Settings.TileHueSpread = 37f;
        asset.Settings.LayerHueAdvance = 38f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Petals), TempAssetFolder);

        var defaults = Petals.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.LayerHue, defaults.LayerHue);
        AssertFloatRangeEqual(asset.Settings.LayerSaturation, defaults.LayerSaturation);
        AssertFloatRangeEqual(asset.Settings.BackgroundHue, defaults.BackgroundHue);
        Assert.That(asset.Settings.BackgroundHueAdvance, Is.EqualTo(defaults.BackgroundHueAdvance));
        Assert.That(asset.Settings.WaveformBrightnessAtTrough, Is.EqualTo(defaults.WaveformBrightnessAtTrough));
        Assert.That(asset.Settings.WaveformBrightnessAtPeak, Is.EqualTo(defaults.WaveformBrightnessAtPeak));
        Assert.That(asset.Settings.WaveformHueShiftAtPeak, Is.EqualTo(defaults.WaveformHueShiftAtPeak));
        AssertIntRangeEqual(asset.Settings.LayerMask, defaults.LayerMask);
        Assert.That(asset.Settings.SparkleChance, Is.EqualTo(defaults.SparkleChance));
        Assert.That(asset.Settings.SparklePhaseTileIndexStep, Is.EqualTo(defaults.SparklePhaseTileIndexStep));
        Assert.That(asset.Settings.SparklePhaseSpeed, Is.EqualTo(defaults.SparklePhaseSpeed));
        Assert.That(asset.Settings.SparkleValue, Is.EqualTo(defaults.SparkleValue));
        Assert.That(asset.Settings.TileHueSpread, Is.EqualTo(defaults.TileHueSpread));
        Assert.That(asset.Settings.LayerHueAdvance, Is.EqualTo(defaults.LayerHueAdvance));
    }

    /// <summary>
    /// Flock Standalone Defaults resolve as fresh copies, including independent ranges, without
    /// pinning the authored look's numeric values in the test.
    /// </summary>
    [Test]
    public void FlockStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = Flock.StandaloneDefaults;
        var second = Flock.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BaseAlignmentWeight, Is.EqualTo(second.BaseAlignmentWeight));
        Assert.That(first.BaseCohesionWeight, Is.EqualTo(second.BaseCohesionWeight));
        Assert.That(first.BaseSeparationWeight, Is.EqualTo(second.BaseSeparationWeight));
        Assert.That(first.MovementActivity, Is.EqualTo(second.MovementActivity));
        Assert.That(first.SpeedMultiplier, Is.Not.SameAs(second.SpeedMultiplier));
        AssertFloatRangeEqual(first.SpeedMultiplier, second.SpeedMultiplier);
        Assert.That(first.RoutineEnvelope, Is.EqualTo(second.RoutineEnvelope));
        Assert.That(first.TrailHalfLife, Is.Not.SameAs(second.TrailHalfLife));
        AssertFloatRangeEqual(first.TrailHalfLife, second.TrailHalfLife);
        Assert.That(first.RoutineHueShift, Is.EqualTo(second.RoutineHueShift));
        Assert.That(first.RoutineSaturationFloor, Is.EqualTo(second.RoutineSaturationFloor));
        Assert.That(first.RoutineValueFloor, Is.EqualTo(second.RoutineValueFloor));
        Assert.That(first.WanderStrength, Is.Not.SameAs(second.WanderStrength));
        AssertFloatRangeEqual(first.WanderStrength, second.WanderStrength);
        Assert.That(first.WanderTurnRate, Is.Not.SameAs(second.WanderTurnRate));
        AssertFloatRangeEqual(first.WanderTurnRate, second.WanderTurnRate);
        Assert.That(first.WanderHeadingRadians, Is.EqualTo(second.WanderHeadingRadians));
        Assert.That(first.WanderFrequency, Is.Not.SameAs(second.WanderFrequency));
        AssertFloatRangeEqual(first.WanderFrequency, second.WanderFrequency);
        Assert.That(first.QuietWanderMultiplier, Is.EqualTo(second.QuietWanderMultiplier));
        Assert.That(first.QuietWanderTurnMultiplier, Is.EqualTo(second.QuietWanderTurnMultiplier));
    }

    /// <summary>
    /// Restore replaces every edited Flock Standalone Setting and Rail with the current file-local
    /// Standalone Defaults, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryFlockValue()
    {
        var asset = (FlockStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(Flock),
            TempAssetFolder);
        asset.Settings.BaseAlignmentWeight = 11f;
        asset.Settings.BaseCohesionWeight = 12f;
        asset.Settings.BaseSeparationWeight = 13f;
        asset.Settings.MovementActivity = 14f;
        asset.Settings.SpeedMultiplier = new FloatRange(15f, 16f, 14f, 17f);
        asset.Settings.RoutineEnvelope = 18f;
        asset.Settings.TrailHalfLife = new FloatRange(19f, 20f, 18f, 21f);
        asset.Settings.RoutineHueShift = 20f;
        asset.Settings.RoutineSaturationFloor = 21f;
        asset.Settings.RoutineValueFloor = 22f;
        asset.Settings.WanderStrength = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.WanderTurnRate = new FloatRange(26f, 27f, 25f, 28f);
        asset.Settings.WanderHeadingRadians = 29f;
        asset.Settings.WanderFrequency = new FloatRange(30f, 31f, 29f, 32f);
        asset.Settings.QuietWanderMultiplier = 33f;
        asset.Settings.QuietWanderTurnMultiplier = 34f;

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(typeof(Flock), TempAssetFolder);

        var defaults = Flock.StandaloneDefaults;
        Assert.That(asset.Settings.BaseAlignmentWeight, Is.EqualTo(defaults.BaseAlignmentWeight));
        Assert.That(asset.Settings.BaseCohesionWeight, Is.EqualTo(defaults.BaseCohesionWeight));
        Assert.That(asset.Settings.BaseSeparationWeight, Is.EqualTo(defaults.BaseSeparationWeight));
        Assert.That(asset.Settings.MovementActivity, Is.EqualTo(defaults.MovementActivity));
        AssertFloatRangeEqual(asset.Settings.SpeedMultiplier, defaults.SpeedMultiplier);
        Assert.That(asset.Settings.RoutineEnvelope, Is.EqualTo(defaults.RoutineEnvelope));
        AssertFloatRangeEqual(asset.Settings.TrailHalfLife, defaults.TrailHalfLife);
        Assert.That(asset.Settings.RoutineHueShift, Is.EqualTo(defaults.RoutineHueShift));
        Assert.That(asset.Settings.RoutineSaturationFloor, Is.EqualTo(defaults.RoutineSaturationFloor));
        Assert.That(asset.Settings.RoutineValueFloor, Is.EqualTo(defaults.RoutineValueFloor));
        AssertFloatRangeEqual(asset.Settings.WanderStrength, defaults.WanderStrength);
        AssertFloatRangeEqual(asset.Settings.WanderTurnRate, defaults.WanderTurnRate);
        Assert.That(asset.Settings.WanderHeadingRadians, Is.EqualTo(defaults.WanderHeadingRadians));
        AssertFloatRangeEqual(asset.Settings.WanderFrequency, defaults.WanderFrequency);
        Assert.That(asset.Settings.QuietWanderMultiplier, Is.EqualTo(defaults.QuietWanderMultiplier));
        Assert.That(asset.Settings.QuietWanderTurnMultiplier, Is.EqualTo(defaults.QuietWanderTurnMultiplier));
    }

    /// <summary>
    /// Restore replaces every edited Flock Sync Setting and Rail with the current file-local Sync
    /// Defaults, without pinning the authored tuning values in the test.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryFlockValue()
    {
        var asset = (FlockSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(Flock),
            TempAssetFolder);
        asset.Settings.BaseAlignmentWeight = 11f;
        asset.Settings.BaseCohesionWeight = 12f;
        asset.Settings.BaseSeparationWeight = 13f;
        asset.Settings.MovementLowWeight = 14f;
        asset.Settings.MovementMidWeight = 15f;
        asset.Settings.MovementHighWeight = 16f;
        asset.Settings.MovementLevel = new FloatRange(17f, 18f, 16f, 19f);
        asset.Settings.SpeedMultiplier = new FloatRange(20f, 21f, 19f, 22f);
        asset.Settings.EnergyPaceMultiplier = new FloatRange(71f, 72f, 70f, 73f);
        asset.Settings.MidManeuverLevel = new FloatRange(23f, 24f, 22f, 25f);
        asset.Settings.MidAlignmentLift = 26f;
        asset.Settings.MidCohesionLift = 27f;
        asset.Settings.CollectiveTurnStrength = 28f;
        asset.Settings.SpectralCentroid = new FloatRange(29f, 30f, 28f, 31f);
        asset.Settings.SpectralSeparationLift = 32f;
        asset.Settings.TrailHalfLife = new FloatRange(33f, 34f, 32f, 35f);
        asset.Settings.RoutineHueShift = 36f;
        asset.Settings.RoutineSaturationFloor = 37f;
        asset.Settings.RoutineValueFloor = 38f;
        asset.Settings.TypicalFillBeats = 39f;
        asset.Settings.MinimumFillBeats = 40f;
        asset.Settings.FillOnsetImpulse = 42f;
        asset.Settings.FillOrbitSteering = 43f;
        asset.Settings.FillOrbitAtFullGather = 44f;
        asset.Settings.FillAlignmentLift = 45f;
        asset.Settings.FillSeparationLift = 46f;
        asset.Settings.DropSpiralSteering = 74f;
        asset.Settings.DropGatherBeats = 47;
        asset.Settings.DropGatherSteering = 48f;
        asset.Settings.DropGatherSeparationSuppression = 49f;
        asset.Settings.DropAftermathBeats = 75;
        asset.Settings.DropTrailHalfLife = 76f;
        asset.Settings.DropBurstSpeedMultiplier = 51f;
        asset.Settings.DropVelocityCarry = 52f;
        asset.Settings.DropOutwardSteering = 53f;
        asset.Settings.DropSpeedLift = 54f;
        asset.Settings.DropCohesionSuppression = 55f;
        asset.Settings.DropSeparationLift = 56f;
        asset.Settings.QuietWanderMultiplier = 57f;
        asset.Settings.QuietWanderTurnMultiplier = 58f;
        asset.Settings.SpectralWanderStrengthLift = 59f;
        asset.Settings.SpectralWanderTurnLift = 60f;
        asset.Settings.WanderStrength = new FloatRange(61f, 62f, 60f, 63f);
        asset.Settings.WanderTurnRate = new FloatRange(64f, 65f, 63f, 66f);
        asset.Settings.WanderHeadingRadians = 67f;
        asset.Settings.WanderFrequency = new FloatRange(68f, 69f, 67f, 70f);

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Flock), TempAssetFolder);

        var defaults = Flock.SyncDefaults;
        Assert.That(asset.Settings.BaseAlignmentWeight, Is.EqualTo(defaults.BaseAlignmentWeight));
        Assert.That(asset.Settings.BaseCohesionWeight, Is.EqualTo(defaults.BaseCohesionWeight));
        Assert.That(asset.Settings.BaseSeparationWeight, Is.EqualTo(defaults.BaseSeparationWeight));
        Assert.That(asset.Settings.MovementLowWeight, Is.EqualTo(defaults.MovementLowWeight));
        Assert.That(asset.Settings.MovementMidWeight, Is.EqualTo(defaults.MovementMidWeight));
        Assert.That(asset.Settings.MovementHighWeight, Is.EqualTo(defaults.MovementHighWeight));
        AssertFloatRangeEqual(asset.Settings.MovementLevel, defaults.MovementLevel);
        AssertFloatRangeEqual(asset.Settings.SpeedMultiplier, defaults.SpeedMultiplier);
        AssertFloatRangeEqual(asset.Settings.EnergyPaceMultiplier, defaults.EnergyPaceMultiplier);
        AssertFloatRangeEqual(asset.Settings.MidManeuverLevel, defaults.MidManeuverLevel);
        Assert.That(asset.Settings.MidAlignmentLift, Is.EqualTo(defaults.MidAlignmentLift));
        Assert.That(asset.Settings.MidCohesionLift, Is.EqualTo(defaults.MidCohesionLift));
        Assert.That(asset.Settings.CollectiveTurnStrength, Is.EqualTo(defaults.CollectiveTurnStrength));
        AssertFloatRangeEqual(asset.Settings.SpectralCentroid, defaults.SpectralCentroid);
        Assert.That(asset.Settings.SpectralSeparationLift, Is.EqualTo(defaults.SpectralSeparationLift));
        AssertFloatRangeEqual(asset.Settings.TrailHalfLife, defaults.TrailHalfLife);
        Assert.That(asset.Settings.RoutineHueShift, Is.EqualTo(defaults.RoutineHueShift));
        Assert.That(asset.Settings.RoutineSaturationFloor, Is.EqualTo(defaults.RoutineSaturationFloor));
        Assert.That(asset.Settings.RoutineValueFloor, Is.EqualTo(defaults.RoutineValueFloor));
        Assert.That(asset.Settings.TypicalFillBeats, Is.EqualTo(defaults.TypicalFillBeats));
        Assert.That(asset.Settings.MinimumFillBeats, Is.EqualTo(defaults.MinimumFillBeats));
        Assert.That(asset.Settings.FillOnsetImpulse, Is.EqualTo(defaults.FillOnsetImpulse));
        Assert.That(asset.Settings.FillOrbitSteering, Is.EqualTo(defaults.FillOrbitSteering));
        Assert.That(asset.Settings.FillOrbitAtFullGather, Is.EqualTo(defaults.FillOrbitAtFullGather));
        Assert.That(asset.Settings.FillAlignmentLift, Is.EqualTo(defaults.FillAlignmentLift));
        Assert.That(asset.Settings.FillSeparationLift, Is.EqualTo(defaults.FillSeparationLift));
        Assert.That(asset.Settings.DropSpiralSteering, Is.EqualTo(defaults.DropSpiralSteering));
        Assert.That(asset.Settings.DropGatherBeats, Is.EqualTo(defaults.DropGatherBeats));
        Assert.That(asset.Settings.DropGatherSteering, Is.EqualTo(defaults.DropGatherSteering));
        Assert.That(asset.Settings.DropGatherSeparationSuppression, Is.EqualTo(defaults.DropGatherSeparationSuppression));
        Assert.That(asset.Settings.DropAftermathBeats, Is.EqualTo(defaults.DropAftermathBeats));
        Assert.That(asset.Settings.DropTrailHalfLife, Is.EqualTo(defaults.DropTrailHalfLife));
        Assert.That(asset.Settings.DropBurstSpeedMultiplier, Is.EqualTo(defaults.DropBurstSpeedMultiplier));
        Assert.That(asset.Settings.DropVelocityCarry, Is.EqualTo(defaults.DropVelocityCarry));
        Assert.That(asset.Settings.DropOutwardSteering, Is.EqualTo(defaults.DropOutwardSteering));
        Assert.That(asset.Settings.DropSpeedLift, Is.EqualTo(defaults.DropSpeedLift));
        Assert.That(asset.Settings.DropCohesionSuppression, Is.EqualTo(defaults.DropCohesionSuppression));
        Assert.That(asset.Settings.DropSeparationLift, Is.EqualTo(defaults.DropSeparationLift));
        Assert.That(asset.Settings.QuietWanderMultiplier, Is.EqualTo(defaults.QuietWanderMultiplier));
        Assert.That(asset.Settings.QuietWanderTurnMultiplier, Is.EqualTo(defaults.QuietWanderTurnMultiplier));
        Assert.That(asset.Settings.SpectralWanderStrengthLift, Is.EqualTo(defaults.SpectralWanderStrengthLift));
        Assert.That(asset.Settings.SpectralWanderTurnLift, Is.EqualTo(defaults.SpectralWanderTurnLift));
        AssertFloatRangeEqual(asset.Settings.WanderStrength, defaults.WanderStrength);
        AssertFloatRangeEqual(asset.Settings.WanderTurnRate, defaults.WanderTurnRate);
        Assert.That(asset.Settings.WanderHeadingRadians, Is.EqualTo(defaults.WanderHeadingRadians));
        AssertFloatRangeEqual(asset.Settings.WanderFrequency, defaults.WanderFrequency);
    }

    /// <summary>
    /// MazeFlyer Standalone Defaults resolve as fresh, mutually independent copies without pinning
    /// the authored look in the test.
    /// </summary>
    [Test]
    public void MazeFlyerStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = MazeFlyer.StandaloneDefaults;
        var second = MazeFlyer.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.FlightSpeed, Is.Not.SameAs(second.FlightSpeed));
        Assert.That(first.BlockRegionsHueJitter, Is.Not.SameAs(second.BlockRegionsHueJitter));
        Assert.That(first.CuratedPalette, Is.Not.SameAs(second.CuratedPalette));
        AssertFloatRangeEqual(first.FlightSpeed, second.FlightSpeed);
        Assert.That(first.TurnSpeedMultiplier, Is.EqualTo(second.TurnSpeedMultiplier));
        Assert.That(first.FlightSpeedSmoothTime, Is.EqualTo(second.FlightSpeedSmoothTime));
        Assert.That(first.RandomCellOccupancyProbability, Is.EqualTo(second.RandomCellOccupancyProbability));
        Assert.That(first.SpatialWavesHueScale, Is.EqualTo(second.SpatialWavesHueScale));
        Assert.That(first.BlockRegionsSize, Is.EqualTo(second.BlockRegionsSize));
        Assert.That(first.PureRandomSaturation, Is.EqualTo(second.PureRandomSaturation));
        Assert.That(first.PureRandomValue, Is.EqualTo(second.PureRandomValue));
        Assert.That(first.SpatialWavesSaturation, Is.EqualTo(second.SpatialWavesSaturation));
        Assert.That(first.SpatialWavesValue, Is.EqualTo(second.SpatialWavesValue));
        AssertFloatRangeEqual(first.BlockRegionsHueJitter, second.BlockRegionsHueJitter);
        Assert.That(first.BlockRegionsSaturation, Is.EqualTo(second.BlockRegionsSaturation));
        Assert.That(first.BlockRegionsValue, Is.EqualTo(second.BlockRegionsValue));
        Assert.That(first.CuratedPalette.Length, Is.EqualTo(second.CuratedPalette.Length));
        for (int i = 0; i < second.CuratedPalette.Length; i++)
        {
            Assert.That(first.CuratedPalette[i], Is.EqualTo(second.CuratedPalette[i]));
        }

        Assert.That(first.CameraFocalLength, Is.EqualTo(second.CameraFocalLength));
        Assert.That(first.TurnBlendStart, Is.EqualTo(second.TurnBlendStart));
        Assert.That(first.ForwardContinuationThreshold, Is.EqualTo(second.ForwardContinuationThreshold));
        Assert.That(first.MaxRayDistance, Is.EqualTo(second.MaxRayDistance));
        Assert.That(first.XAxisFaceShade, Is.EqualTo(second.XAxisFaceShade));
        Assert.That(first.YAxisFaceShade, Is.EqualTo(second.YAxisFaceShade));
        Assert.That(first.ZAxisFaceShade, Is.EqualTo(second.ZAxisFaceShade));
        Assert.That(first.HeadlightMinShade, Is.EqualTo(second.HeadlightMinShade));
        Assert.That(first.FogDensity, Is.EqualTo(second.FogDensity));
        Assert.That(first.EdgeLineThicknessTiles, Is.EqualTo(second.EdgeLineThicknessTiles));
        Assert.That(first.EdgeLineShade, Is.EqualTo(second.EdgeLineShade));
        Assert.That(first.RaySampleSpread, Is.EqualTo(second.RaySampleSpread));
        Assert.That(first.SharedPaletteMinValue, Is.EqualTo(second.SharedPaletteMinValue));
        Assert.That(first.MinBrightness, Is.EqualTo(second.MinBrightness));
    }

    /// <summary>
    /// Restore replaces every edited MazeFlyer Standalone Setting, Rail, and palette entry with the
    /// current file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryMazeFlyerValue()
    {
        var asset = (MazeFlyerStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(MazeFlyer),
            TempAssetFolder);
        MutateEveryMazeFlyerPictureSetting(asset.Settings);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(MazeFlyer),
            TempAssetFolder);

        var defaults = MazeFlyer.StandaloneDefaults;
        AssertFloatRangeEqual(asset.Settings.FlightSpeed, defaults.FlightSpeed);
        Assert.That(asset.Settings.TurnSpeedMultiplier, Is.EqualTo(defaults.TurnSpeedMultiplier));
        Assert.That(asset.Settings.FlightSpeedSmoothTime, Is.EqualTo(defaults.FlightSpeedSmoothTime));
        Assert.That(asset.Settings.RandomCellOccupancyProbability, Is.EqualTo(defaults.RandomCellOccupancyProbability));
        Assert.That(asset.Settings.SpatialWavesHueScale, Is.EqualTo(defaults.SpatialWavesHueScale));
        Assert.That(asset.Settings.BlockRegionsSize, Is.EqualTo(defaults.BlockRegionsSize));
        Assert.That(asset.Settings.PureRandomSaturation, Is.EqualTo(defaults.PureRandomSaturation));
        Assert.That(asset.Settings.PureRandomValue, Is.EqualTo(defaults.PureRandomValue));
        Assert.That(asset.Settings.SpatialWavesSaturation, Is.EqualTo(defaults.SpatialWavesSaturation));
        Assert.That(asset.Settings.SpatialWavesValue, Is.EqualTo(defaults.SpatialWavesValue));
        AssertFloatRangeEqual(asset.Settings.BlockRegionsHueJitter, defaults.BlockRegionsHueJitter);
        Assert.That(asset.Settings.BlockRegionsSaturation, Is.EqualTo(defaults.BlockRegionsSaturation));
        Assert.That(asset.Settings.BlockRegionsValue, Is.EqualTo(defaults.BlockRegionsValue));
        Assert.That(asset.Settings.CuratedPalette.Length, Is.EqualTo(defaults.CuratedPalette.Length));
        for (int i = 0; i < defaults.CuratedPalette.Length; i++)
        {
            Assert.That(asset.Settings.CuratedPalette[i], Is.EqualTo(defaults.CuratedPalette[i]));
        }

        Assert.That(asset.Settings.CameraFocalLength, Is.EqualTo(defaults.CameraFocalLength));
        Assert.That(asset.Settings.TurnBlendStart, Is.EqualTo(defaults.TurnBlendStart));
        Assert.That(asset.Settings.ForwardContinuationThreshold, Is.EqualTo(defaults.ForwardContinuationThreshold));
        Assert.That(asset.Settings.MaxRayDistance, Is.EqualTo(defaults.MaxRayDistance));
        Assert.That(asset.Settings.XAxisFaceShade, Is.EqualTo(defaults.XAxisFaceShade));
        Assert.That(asset.Settings.YAxisFaceShade, Is.EqualTo(defaults.YAxisFaceShade));
        Assert.That(asset.Settings.ZAxisFaceShade, Is.EqualTo(defaults.ZAxisFaceShade));
        Assert.That(asset.Settings.HeadlightMinShade, Is.EqualTo(defaults.HeadlightMinShade));
        Assert.That(asset.Settings.FogDensity, Is.EqualTo(defaults.FogDensity));
        Assert.That(asset.Settings.EdgeLineThicknessTiles, Is.EqualTo(defaults.EdgeLineThicknessTiles));
        Assert.That(asset.Settings.EdgeLineShade, Is.EqualTo(defaults.EdgeLineShade));
        Assert.That(asset.Settings.RaySampleSpread, Is.EqualTo(defaults.RaySampleSpread));
        Assert.That(asset.Settings.SharedPaletteMinValue, Is.EqualTo(defaults.SharedPaletteMinValue));
        Assert.That(asset.Settings.MinBrightness, Is.EqualTo(defaults.MinBrightness));
    }

    /// <summary>
    /// Restore replaces every edited MazeFlyer Sync Setting, including dual-homed picture values,
    /// Rails, and palette entries, with the current file-local Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryMazeFlyerValue()
    {
        var asset = (MazeFlyerSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(MazeFlyer),
            TempAssetFolder);
        MutateEveryMazeFlyerPictureSetting(asset.Settings);
        asset.Settings.OnBeatLowThreshold = 49f;
        asset.Settings.OnBeatBrightnessPulse = 50f;
        asset.Settings.LowEnergyFlightSpeed = 51f;
        asset.Settings.MidEnergyFlightSpeed = 52f;
        asset.Settings.HighEnergyFlightSpeed = 53f;
        asset.Settings.FlightSpeedSmoothTime = 54f;
        asset.Settings.EnergyFlightSpeedRampBeats = 55;
        asset.Settings.DropStopBeats = 56;
        asset.Settings.DropSitBeats = 57;
        asset.Settings.DropLaunchMultiplier = 58f;
        asset.Settings.FillEdgeInversion = 59f;
        asset.Settings.FillLineGlow = 60f;
        asset.Settings.DropCameraSpinSpeed = 61f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(MazeFlyer), TempAssetFolder);

        var defaults = MazeFlyer.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.FlightSpeed, defaults.FlightSpeed);
        Assert.That(asset.Settings.TurnSpeedMultiplier, Is.EqualTo(defaults.TurnSpeedMultiplier));
        Assert.That(asset.Settings.RandomCellOccupancyProbability, Is.EqualTo(defaults.RandomCellOccupancyProbability));
        Assert.That(asset.Settings.SpatialWavesHueScale, Is.EqualTo(defaults.SpatialWavesHueScale));
        Assert.That(asset.Settings.BlockRegionsSize, Is.EqualTo(defaults.BlockRegionsSize));
        Assert.That(asset.Settings.PureRandomSaturation, Is.EqualTo(defaults.PureRandomSaturation));
        Assert.That(asset.Settings.PureRandomValue, Is.EqualTo(defaults.PureRandomValue));
        Assert.That(asset.Settings.SpatialWavesSaturation, Is.EqualTo(defaults.SpatialWavesSaturation));
        Assert.That(asset.Settings.SpatialWavesValue, Is.EqualTo(defaults.SpatialWavesValue));
        AssertFloatRangeEqual(asset.Settings.BlockRegionsHueJitter, defaults.BlockRegionsHueJitter);
        Assert.That(asset.Settings.BlockRegionsSaturation, Is.EqualTo(defaults.BlockRegionsSaturation));
        Assert.That(asset.Settings.BlockRegionsValue, Is.EqualTo(defaults.BlockRegionsValue));
        Assert.That(asset.Settings.CuratedPalette.Length, Is.EqualTo(defaults.CuratedPalette.Length));
        for (int i = 0; i < defaults.CuratedPalette.Length; i++)
        {
            Assert.That(asset.Settings.CuratedPalette[i], Is.EqualTo(defaults.CuratedPalette[i]));
        }

        Assert.That(asset.Settings.CameraFocalLength, Is.EqualTo(defaults.CameraFocalLength));
        Assert.That(asset.Settings.TurnBlendStart, Is.EqualTo(defaults.TurnBlendStart));
        Assert.That(asset.Settings.ForwardContinuationThreshold, Is.EqualTo(defaults.ForwardContinuationThreshold));
        Assert.That(asset.Settings.MaxRayDistance, Is.EqualTo(defaults.MaxRayDistance));
        Assert.That(asset.Settings.XAxisFaceShade, Is.EqualTo(defaults.XAxisFaceShade));
        Assert.That(asset.Settings.YAxisFaceShade, Is.EqualTo(defaults.YAxisFaceShade));
        Assert.That(asset.Settings.ZAxisFaceShade, Is.EqualTo(defaults.ZAxisFaceShade));
        Assert.That(asset.Settings.HeadlightMinShade, Is.EqualTo(defaults.HeadlightMinShade));
        Assert.That(asset.Settings.FogDensity, Is.EqualTo(defaults.FogDensity));
        Assert.That(asset.Settings.EdgeLineThicknessTiles, Is.EqualTo(defaults.EdgeLineThicknessTiles));
        Assert.That(asset.Settings.EdgeLineShade, Is.EqualTo(defaults.EdgeLineShade));
        Assert.That(asset.Settings.RaySampleSpread, Is.EqualTo(defaults.RaySampleSpread));
        Assert.That(asset.Settings.SharedPaletteMinValue, Is.EqualTo(defaults.SharedPaletteMinValue));
        Assert.That(asset.Settings.MinBrightness, Is.EqualTo(defaults.MinBrightness));
        Assert.That(asset.Settings.OnBeatLowThreshold, Is.EqualTo(defaults.OnBeatLowThreshold));
        Assert.That(asset.Settings.OnBeatBrightnessPulse, Is.EqualTo(defaults.OnBeatBrightnessPulse));
        Assert.That(asset.Settings.LowEnergyFlightSpeed, Is.EqualTo(defaults.LowEnergyFlightSpeed));
        Assert.That(asset.Settings.MidEnergyFlightSpeed, Is.EqualTo(defaults.MidEnergyFlightSpeed));
        Assert.That(asset.Settings.HighEnergyFlightSpeed, Is.EqualTo(defaults.HighEnergyFlightSpeed));
        Assert.That(asset.Settings.FlightSpeedSmoothTime, Is.EqualTo(defaults.FlightSpeedSmoothTime));
        Assert.That(asset.Settings.EnergyFlightSpeedRampBeats, Is.EqualTo(defaults.EnergyFlightSpeedRampBeats));
        Assert.That(asset.Settings.DropStopBeats, Is.EqualTo(defaults.DropStopBeats));
        Assert.That(asset.Settings.DropSitBeats, Is.EqualTo(defaults.DropSitBeats));
        Assert.That(asset.Settings.DropLaunchMultiplier, Is.EqualTo(defaults.DropLaunchMultiplier));
        Assert.That(asset.Settings.FillEdgeInversion, Is.EqualTo(defaults.FillEdgeInversion));
        Assert.That(asset.Settings.FillLineGlow, Is.EqualTo(defaults.FillLineGlow));
        Assert.That(asset.Settings.DropCameraSpinSpeed, Is.EqualTo(defaults.DropCameraSpinSpeed));
    }

    /// <summary>Changes every picture-setting field shared by MazeFlyer's two settings surfaces.</summary>
    private static void MutateEveryMazeFlyerPictureSetting(MazeFlyerStandaloneSettings settings)
    {
        settings.FlightSpeed = new FloatRange(17f, 18f, 16f, 19f);
        settings.TurnSpeedMultiplier = 20f;
        settings.FlightSpeedSmoothTime = 20.5f;
        settings.RandomCellOccupancyProbability = 21f;
        settings.SpatialWavesHueScale = 22f;
        settings.BlockRegionsSize = 23;
        settings.PureRandomSaturation = 24f;
        settings.PureRandomValue = 25f;
        settings.SpatialWavesSaturation = 26f;
        settings.SpatialWavesValue = 27f;
        settings.BlockRegionsHueJitter = new FloatRange(28f, 29f, 27f, 30f);
        settings.BlockRegionsSaturation = 31f;
        settings.BlockRegionsValue = 32f;
        settings.CuratedPalette = new[] { Color.magenta };
        settings.CameraFocalLength = 33f;
        settings.TurnBlendStart = 34f;
        settings.ForwardContinuationThreshold = 35f;
        settings.MaxRayDistance = 36f;
        settings.XAxisFaceShade = 37f;
        settings.YAxisFaceShade = 38f;
        settings.ZAxisFaceShade = 39f;
        settings.HeadlightMinShade = 40f;
        settings.FogDensity = 41f;
        settings.EdgeLineThicknessTiles = 42f;
        settings.EdgeLineShade = 43f;
        settings.RaySampleSpread = 44f;
        settings.SharedPaletteMinValue = 45f;
        settings.MinBrightness = 46f;
    }

    /// <summary>Changes every picture-setting field shared by MazeFlyer's two settings surfaces.</summary>
    private static void MutateEveryMazeFlyerPictureSetting(MazeFlyerSyncSettings settings)
    {
        settings.FlightSpeed = new FloatRange(17f, 18f, 16f, 19f);
        settings.TurnSpeedMultiplier = 20f;
        settings.RandomCellOccupancyProbability = 21f;
        settings.SpatialWavesHueScale = 22f;
        settings.BlockRegionsSize = 23;
        settings.PureRandomSaturation = 24f;
        settings.PureRandomValue = 25f;
        settings.SpatialWavesSaturation = 26f;
        settings.SpatialWavesValue = 27f;
        settings.BlockRegionsHueJitter = new FloatRange(28f, 29f, 27f, 30f);
        settings.BlockRegionsSaturation = 31f;
        settings.BlockRegionsValue = 32f;
        settings.CuratedPalette = new[] { Color.cyan };
        settings.CameraFocalLength = 33f;
        settings.TurnBlendStart = 34f;
        settings.ForwardContinuationThreshold = 35f;
        settings.MaxRayDistance = 36f;
        settings.XAxisFaceShade = 37f;
        settings.YAxisFaceShade = 38f;
        settings.ZAxisFaceShade = 39f;
        settings.HeadlightMinShade = 40f;
        settings.FogDensity = 41f;
        settings.EdgeLineThicknessTiles = 42f;
        settings.EdgeLineShade = 43f;
        settings.RaySampleSpread = 44f;
        settings.SharedPaletteMinValue = 45f;
        settings.MinBrightness = 46f;
    }

    /// <summary>Asserts that a float range's endpoints and editor Rails match.</summary>
    /// <param name="actual">The restored or independently resolved settings range.</param>
    /// <param name="expected">The current file-local defaults range.</param>
    private static void AssertFloatRangeEqual(FloatRange actual, FloatRange expected)
    {
        Assert.That(actual.Min, Is.EqualTo(expected.Min));
        Assert.That(actual.Max, Is.EqualTo(expected.Max));
        Assert.That(actual.LowRail, Is.EqualTo(expected.LowRail));
        Assert.That(actual.HighRail, Is.EqualTo(expected.HighRail));
    }

    /// <summary>Asserts that an integer range's endpoints and editor Rails match.</summary>
    /// <param name="actual">The restored or independently resolved settings range.</param>
    /// <param name="expected">The current file-local defaults range.</param>
    private static void AssertIntRangeEqual(IntRange actual, IntRange expected)
    {
        Assert.That(actual.MinInclusive, Is.EqualTo(expected.MinInclusive));
        Assert.That(actual.MaxExclusive, Is.EqualTo(expected.MaxExclusive));
        Assert.That(actual.LowRail, Is.EqualTo(expected.LowRail));
        Assert.That(actual.HighRail, Is.EqualTo(expected.HighRail));
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
    public static TestEffectSyncSettings SyncDefaults => new() { Amount = 2f };

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
