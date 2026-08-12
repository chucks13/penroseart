// Stores ColorSparkle's typed Standalone Settings as a live-editable, restorable Unity asset.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by ColorSparkle.</summary>
[CreateAssetMenu(
    fileName = "ColorSparkleSettings",
    menuName = "Penrose/Effect Standalone Settings/ColorSparkle")]
public sealed class ColorSparkleStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<ColorSparkleStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private ColorSparkleStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(ColorSparkle);

    /// <summary>The saved ColorSparkle Standalone Settings.</summary>
    public ColorSparkleStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies ColorSparkle's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(ColorSparkle.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of ColorSparkle's current Standalone Defaults and Rails.
    /// </summary>
    private static ColorSparkleStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new ColorSparkleStandaloneSettings();
        defaultSettings.CopyFrom(ColorSparkle.StandaloneDefaults);
        return defaultSettings;
    }
}
