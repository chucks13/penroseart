// Stores TileShapes' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by TileShapes.</summary>
[CreateAssetMenu(fileName = "TileShapesSettings", menuName = "Penrose/Effect Sync Settings/TileShapes")]
public sealed class TileShapesSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<TileShapesSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private TileShapesSyncSettings settings = TileShapes.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(TileShapes);

    /// <summary>The saved TileShapes Sync Settings.</summary>
    public TileShapesSyncSettings Settings => settings ?? (settings = TileShapes.SyncDefaults);

    /// <summary>Copies TileShapes' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(TileShapes.SyncDefaults);
    }
}
