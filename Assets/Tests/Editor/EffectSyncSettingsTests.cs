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
    /// Restore replaces every edited Ripple Sync Setting and Rail with the current file-local Sync
    /// Defaults.
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
        asset.Settings.HueShiftMax = 0.9f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(Ripple), TempAssetFolder);

        var defaults = Ripple.SyncDefaults;
        AssertFloatRangeEqual(asset.Settings.DropSpawnChance, defaults.DropSpawnChance);
        AssertFloatRangeEqual(asset.Settings.Velocity, defaults.Velocity);
        Assert.That(asset.Settings.VelocityDivisor, Is.EqualTo(defaults.VelocityDivisor));
        Assert.That(asset.Settings.DistanceDivisor, Is.EqualTo(defaults.DistanceDivisor));
        Assert.That(asset.Settings.PaletteOffset, Is.EqualTo(defaults.PaletteOffset));
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
    /// AnimateLoops Standalone Defaults resolve as fresh, mutually independent copies without
    /// pinning authored tuning values that are judged on the wall.
    /// </summary>
    [Test]
    public void AnimateLoopsStandaloneDefaultsResolveAsIndependentCopies()
    {
        var first = AnimateLoops.StandaloneDefaults;
        var second = AnimateLoops.StandaloneDefaults;

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.BackgroundHueRate, Is.EqualTo(second.BackgroundHueRate));
        Assert.That(first.LoopTileHueStep, Is.EqualTo(second.LoopTileHueStep));
        Assert.That(first.LoopHueAdvance, Is.EqualTo(second.LoopHueAdvance));
        Assert.That(first.DistortionMode, Is.Not.SameAs(second.DistortionMode));
        AssertIntRangeEqual(first.DistortionMode, second.DistortionMode);
    }

    /// <summary>
    /// Restore replaces every edited AnimateLoops Standalone Setting and distortion-mode Rail with
    /// the current file-local Standalone Defaults.
    /// </summary>
    [Test]
    public void RestoreStandaloneDefaultsCopiesEveryAnimateLoopsValue()
    {
        var asset = (AnimateLoopsStandaloneSettingsAsset)EffectStandaloneSettingsAssetUtility.EnsureAsset(
            typeof(AnimateLoops),
            TempAssetFolder);
        asset.Settings.BackgroundHueRate = 17f;
        asset.Settings.LoopTileHueStep = 18f;
        asset.Settings.LoopHueAdvance = 19f;
        asset.Settings.DistortionMode = new IntRange(20, 21, 19, 22);

        EffectStandaloneSettingsAssetUtility.RestoreStandaloneDefaults(
            typeof(AnimateLoops),
            TempAssetFolder);

        var defaults = AnimateLoops.StandaloneDefaults;
        Assert.That(asset.Settings.BackgroundHueRate, Is.EqualTo(defaults.BackgroundHueRate));
        Assert.That(asset.Settings.LoopTileHueStep, Is.EqualTo(defaults.LoopTileHueStep));
        Assert.That(asset.Settings.LoopHueAdvance, Is.EqualTo(defaults.LoopHueAdvance));
        AssertIntRangeEqual(asset.Settings.DistortionMode, defaults.DistortionMode);
    }

    /// <summary>
    /// Restore replaces every edited AnimateLoops Sync Setting and distortion-mode Rail with the
    /// current file-local Sync Defaults.
    /// </summary>
    [Test]
    public void RestoreSyncDefaultsCopiesEveryAnimateLoopsValue()
    {
        var asset = (AnimateLoopsSyncSettingsAsset)EffectSyncSettingsAssetUtility.EnsureAsset(
            typeof(AnimateLoops),
            TempAssetFolder);
        asset.Settings.BackgroundHueRate = 17f;
        asset.Settings.LoopTileHueStep = 18f;
        asset.Settings.LoopHueAdvance = 19f;
        asset.Settings.DistortionMode = new IntRange(20, 21, 19, 22);
        asset.Settings.HueResponseMagnitude = 23f;
        asset.Settings.TimeWarpSeconds = 24f;
        asset.Settings.TimeWarpHueScale = 25f;
        asset.Settings.DropTileHueStep = 26f;
        asset.Settings.DropHueRate = 27f;
        asset.Settings.DropBrightness = 28f;
        asset.Settings.FillBlackAndWhiteProbability = 0.29f;

        EffectSyncSettingsAssetUtility.RestoreSyncDefaults(typeof(AnimateLoops), TempAssetFolder);

        var defaults = AnimateLoops.SyncDefaults;
        Assert.That(asset.Settings.BackgroundHueRate, Is.EqualTo(defaults.BackgroundHueRate));
        Assert.That(asset.Settings.LoopTileHueStep, Is.EqualTo(defaults.LoopTileHueStep));
        Assert.That(asset.Settings.LoopHueAdvance, Is.EqualTo(defaults.LoopHueAdvance));
        AssertIntRangeEqual(asset.Settings.DistortionMode, defaults.DistortionMode);
        Assert.That(asset.Settings.HueResponseMagnitude, Is.EqualTo(defaults.HueResponseMagnitude));
        Assert.That(asset.Settings.TimeWarpSeconds, Is.EqualTo(defaults.TimeWarpSeconds));
        Assert.That(asset.Settings.TimeWarpHueScale, Is.EqualTo(defaults.TimeWarpHueScale));
        Assert.That(asset.Settings.DropTileHueStep, Is.EqualTo(defaults.DropTileHueStep));
        Assert.That(asset.Settings.DropHueRate, Is.EqualTo(defaults.DropHueRate));
        Assert.That(asset.Settings.DropBrightness, Is.EqualTo(defaults.DropBrightness));
        Assert.That(
            asset.Settings.FillBlackAndWhiteProbability,
            Is.EqualTo(defaults.FillBlackAndWhiteProbability));
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
