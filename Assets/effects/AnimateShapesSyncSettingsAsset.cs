// Stores AnimateShapes' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by AnimateShapes.</summary>
[CreateAssetMenu(fileName = "AnimateShapesSettings", menuName = "Penrose/Effect Sync Settings/AnimateShapes")]
public sealed class AnimateShapesSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<AnimateShapesSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnimateShapesSyncSettings settings = AnimateShapes.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(AnimateShapes);

    /// <summary>The saved AnimateShapes Sync Settings.</summary>
    public AnimateShapesSyncSettings Settings => settings ?? (settings = AnimateShapes.SyncDefaults);

    /// <summary>Copies AnimateShapes' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(AnimateShapes.SyncDefaults);
    }
}
