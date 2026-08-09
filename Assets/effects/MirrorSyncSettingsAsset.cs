// Stores Mirror's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Mirror.</summary>
[CreateAssetMenu(fileName = "MirrorSettings", menuName = "Penrose/Effect Sync Settings/Mirror")]
public sealed class MirrorSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<MirrorSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MirrorSyncSettings settings = Mirror.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Mirror);

    /// <summary>The saved Mirror Sync Settings.</summary>
    public MirrorSyncSettings Settings => settings ?? (settings = Mirror.SyncDefaults);

    /// <summary>Copies Mirror's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Mirror.SyncDefaults);
    }
}
