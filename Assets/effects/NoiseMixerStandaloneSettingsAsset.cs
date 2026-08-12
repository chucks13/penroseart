// Stores NoiseMixer's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by NoiseMixer.</summary>
[CreateAssetMenu(fileName = "NoiseMixerSettings", menuName = "Penrose/Effect Standalone Settings/NoiseMixer")]
public sealed class NoiseMixerStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<NoiseMixerStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseMixerStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(NoiseMixer);

    /// <summary>The saved NoiseMixer Standalone Settings.</summary>
    public NoiseMixerStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies NoiseMixer's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(NoiseMixer.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of NoiseMixer's current Standalone Defaults and Rails.</summary>
    private static NoiseMixerStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new NoiseMixerStandaloneSettings();
        defaultSettings.CopyFrom(NoiseMixer.StandaloneDefaults);
        return defaultSettings;
    }
}
