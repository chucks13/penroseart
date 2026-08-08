// Stores Flock's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Flock.</summary>
[CreateAssetMenu(fileName = "FlockSettings", menuName = "Penrose/Effect Sync Settings/Flock")]
public sealed class FlockSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<FlockSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private FlockSyncSettings settings = Flock.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Flock);

    /// <summary>The saved Flock Sync Settings.</summary>
    public FlockSyncSettings Settings => settings ?? (settings = Flock.SyncDefaults);

    /// <summary>Copies Flock's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Flock.SyncDefaults);
    }
}
