// Stores Fluid's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Fluid.</summary>
[CreateAssetMenu(fileName = "FluidSettings", menuName = "Penrose/Effect Sync Settings/Fluid")]
public sealed class FluidSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<FluidSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private FluidSyncSettings settings = Fluid.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Fluid);

    /// <summary>The saved Fluid Sync Settings.</summary>
    public FluidSyncSettings Settings => settings ?? (settings = Fluid.SyncDefaults);

    /// <summary>Copies Fluid's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Fluid.SyncDefaults);
    }
}
