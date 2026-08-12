// Stores Lightning's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Lightning.</summary>
[CreateAssetMenu(fileName = "LightningSettings", menuName = "Penrose/Effect Standalone Settings/Lightning")]
public sealed class LightningStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<LightningStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private LightningStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Lightning);

    /// <summary>The saved Lightning Standalone Settings.</summary>
    public LightningStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>Copies Lightning's current file-local Standalone Defaults over the saved Standalone Settings.</summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Lightning.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Lightning's current file-local Standalone Defaults.</summary>
    private static LightningStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new LightningStandaloneSettings();
        defaultSettings.CopyFrom(Lightning.StandaloneDefaults);
        return defaultSettings;
    }
}
