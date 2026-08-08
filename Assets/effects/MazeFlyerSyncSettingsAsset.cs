// Stores MazeFlyer's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by MazeFlyer.</summary>
[CreateAssetMenu(fileName = "MazeFlyerSettings", menuName = "Penrose/Effect Sync Settings/MazeFlyer")]
public sealed class MazeFlyerSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<MazeFlyerSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MazeFlyerSyncSettings settings = MazeFlyer.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(MazeFlyer);

    /// <summary>The saved MazeFlyer Sync Settings.</summary>
    public MazeFlyerSyncSettings Settings => settings ?? (settings = MazeFlyer.SyncDefaults);

    /// <summary>Copies MazeFlyer's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(MazeFlyer.SyncDefaults);
    }
}
