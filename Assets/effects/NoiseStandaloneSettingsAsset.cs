// Stores Noise's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Noise.</summary>
[CreateAssetMenu(fileName = "NoiseSettings", menuName = "Penrose/Effect Standalone Settings/Noise")]
public sealed class NoiseStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<NoiseStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Noise);

    /// <summary>The saved Noise Standalone Settings.</summary>
    public NoiseStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>Copies Noise's current file-local Standalone Defaults over the saved Standalone Settings.</summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Noise.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Noise's current in-file Standalone Defaults and Rails.</summary>
    private static NoiseStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new NoiseStandaloneSettings();
        defaultSettings.CopyFrom(Noise.StandaloneDefaults);
        return defaultSettings;
    }
}
