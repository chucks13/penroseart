// Stores Circles' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Circles.</summary>
[CreateAssetMenu(fileName = "CirclesSettings", menuName = "Penrose/Effect Standalone Settings/Circles")]
public sealed class CirclesStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<CirclesStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private CirclesStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Circles);

    /// <summary>The saved Circles Standalone Settings.</summary>
    public CirclesStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Circles' current file-local Standalone Defaults, including the distortion-mode
    /// Rails, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Circles.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Circles' current in-file Standalone Defaults.</summary>
    private static CirclesStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new CirclesStandaloneSettings();
        defaultSettings.CopyFrom(Circles.StandaloneDefaults);
        return defaultSettings;
    }
}
