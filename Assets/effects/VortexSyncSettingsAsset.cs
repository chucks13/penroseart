// Stores Vortex's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Vortex.</summary>
[CreateAssetMenu(fileName = "VortexSettings", menuName = "Penrose/Effect Sync Settings/Vortex")]
public sealed class VortexSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<VortexSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private VortexSyncSettings settings = Vortex.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Vortex);

    /// <summary>The saved Vortex Sync Settings.</summary>
    public VortexSyncSettings Settings => settings ?? (settings = Vortex.SyncDefaults);

    /// <summary>Copies Vortex's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Vortex.SyncDefaults);
    }
}
