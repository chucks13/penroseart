// Stores Vortex's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Vortex.</summary>
[CreateAssetMenu(fileName = "VortexSettings", menuName = "Penrose/Effect Standalone Settings/Vortex")]
public sealed class VortexStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<VortexStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private VortexStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Vortex);

    /// <summary>The saved Vortex Standalone Settings.</summary>
    public VortexStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Vortex's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Vortex.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Vortex's current in-file Standalone Defaults and Rails.</summary>
    private static VortexStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new VortexStandaloneSettings();
        defaultSettings.CopyFrom(Vortex.StandaloneDefaults);
        return defaultSettings;
    }
}
