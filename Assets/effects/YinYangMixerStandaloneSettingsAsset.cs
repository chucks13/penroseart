// Stores YinYangMixer's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by YinYangMixer.</summary>
[CreateAssetMenu(
    fileName = "YinYangMixerSettings",
    menuName = "Penrose/Effect Standalone Settings/YinYangMixer")]
public sealed class YinYangMixerStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<YinYangMixerStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private YinYangMixerStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(YinYangMixer);

    /// <summary>The saved YinYangMixer Standalone Settings.</summary>
    public YinYangMixerStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies YinYangMixer's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(YinYangMixer.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of YinYangMixer's current in-file Standalone Defaults and Rails.
    /// </summary>
    private static YinYangMixerStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new YinYangMixerStandaloneSettings();
        defaultSettings.CopyFrom(YinYangMixer.StandaloneDefaults);
        return defaultSettings;
    }
}
