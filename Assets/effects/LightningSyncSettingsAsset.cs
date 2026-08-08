// Stores Lightning's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Lightning.</summary>
[CreateAssetMenu(fileName = "LightningSettings", menuName = "Penrose/Effect Sync Settings/Lightning")]
public sealed class LightningSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<LightningSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private LightningSyncSettings settings = Lightning.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Lightning);

    /// <summary>The saved Lightning Sync Settings.</summary>
    public LightningSyncSettings Settings => settings ?? (settings = Lightning.SyncDefaults);

    /// <summary>Copies Lightning's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Lightning.SyncDefaults);
    }
}
