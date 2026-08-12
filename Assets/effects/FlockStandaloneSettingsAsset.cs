// Stores Flock's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Flock.</summary>
[CreateAssetMenu(fileName = "FlockSettings", menuName = "Penrose/Effect Standalone Settings/Flock")]
public sealed class FlockStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<FlockStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private FlockStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Flock);

    /// <summary>The saved Flock Standalone Settings.</summary>
    public FlockStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Flock's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Flock.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Flock's current in-file Standalone Defaults and Rails.</summary>
    private static FlockStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new FlockStandaloneSettings();
        defaultSettings.CopyFrom(Flock.StandaloneDefaults);
        return defaultSettings;
    }
}
