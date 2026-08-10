// Stores Angles' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Angles.</summary>
[CreateAssetMenu(fileName = "AnglesSettings", menuName = "Penrose/Effect Standalone Settings/Angles")]
public sealed class AnglesStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<AnglesStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnglesStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Angles);

    /// <summary>The saved Angles Standalone Settings.</summary>
    public AnglesStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies Angles' current file-local Standalone Defaults, including the speed and directional-
    /// shading depth ranges' editor rails, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Angles.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of Angles' current in-file Standalone Defaults and range rails.
    /// </summary>
    private static AnglesStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new AnglesStandaloneSettings();
        defaultSettings.CopyFrom(Angles.StandaloneDefaults);
        return defaultSettings;
    }
}
