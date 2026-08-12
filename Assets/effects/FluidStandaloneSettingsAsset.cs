// Stores Fluid's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Fluid.</summary>
[CreateAssetMenu(fileName = "FluidSettings", menuName = "Penrose/Effect Standalone Settings/Fluid")]
public sealed class FluidStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<FluidStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private FluidStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Fluid);

    /// <summary>The saved Fluid Standalone Settings.</summary>
    public FluidStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>Copies Fluid's current file-local Standalone Defaults over the saved Settings.</summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Fluid.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Fluid's current in-file Standalone Defaults.</summary>
    private static FluidStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new FluidStandaloneSettings();
        defaultSettings.CopyFrom(Fluid.StandaloneDefaults);
        return defaultSettings;
    }
}
