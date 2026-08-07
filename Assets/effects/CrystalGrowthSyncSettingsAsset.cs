// Stores Crystal Growth's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Crystal Growth.</summary>
[CreateAssetMenu(fileName = "CrystalGrowthSettings", menuName = "Penrose/Effect Sync Settings/Crystal Growth")]
public sealed class CrystalGrowthSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<CrystalGrowthSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private CrystalGrowthSyncSettings settings = CrystalGrowth.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(CrystalGrowth);

    /// <summary>The saved Crystal Growth Sync Settings.</summary>
    public CrystalGrowthSyncSettings Settings => settings ?? (settings = CrystalGrowth.SyncDefaults);

    /// <summary>Copies Crystal Growth's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(CrystalGrowth.SyncDefaults);
    }
}
