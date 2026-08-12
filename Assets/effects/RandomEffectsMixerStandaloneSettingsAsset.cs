// Stores RandomEffectsMixer's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by RandomEffectsMixer.</summary>
[CreateAssetMenu(
    fileName = "RandomEffectsMixerSettings",
    menuName = "Penrose/Effect Standalone Settings/RandomEffectsMixer")]
public sealed class RandomEffectsMixerStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<RandomEffectsMixerStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RandomEffectsMixerStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(RandomEffectsMixer);

    /// <summary>The saved RandomEffectsMixer Standalone Settings.</summary>
    public RandomEffectsMixerStandaloneSettings Settings =>
        settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies RandomEffectsMixer's current file-local Standalone Defaults, including every range's
    /// Rails, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(RandomEffectsMixer.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of RandomEffectsMixer's current in-file Standalone Defaults and Rails.
    /// </summary>
    private static RandomEffectsMixerStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new RandomEffectsMixerStandaloneSettings();
        defaultSettings.CopyFrom(RandomEffectsMixer.StandaloneDefaults);
        return defaultSettings;
    }
}
