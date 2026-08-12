// Stores Crystal Growth's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Crystal Growth.</summary>
[CreateAssetMenu(
    fileName = "CrystalGrowthSettings",
    menuName = "Penrose/Effect Standalone Settings/Crystal Growth")]
public sealed class CrystalGrowthStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<CrystalGrowthStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private CrystalGrowthStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(CrystalGrowth);

    /// <summary>The saved Crystal Growth Standalone Settings.</summary>
    public CrystalGrowthStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Crystal Growth's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(CrystalGrowth.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of Crystal Growth's current in-file Standalone Defaults and Rails.
    /// </summary>
    private static CrystalGrowthStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new CrystalGrowthStandaloneSettings();
        defaultSettings.CopyFrom(CrystalGrowth.StandaloneDefaults);
        return defaultSettings;
    }
}
