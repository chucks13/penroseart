// Stores MetaBalls' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by MetaBalls.</summary>
[CreateAssetMenu(fileName = "MetaBallsSettings", menuName = "Penrose/Effect Sync Settings/MetaBalls")]
public sealed class MetaBallsSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<MetaBallsSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MetaBallsSyncSettings settings = MetaBalls.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(MetaBalls);

    /// <summary>The saved MetaBalls Sync Settings.</summary>
    public MetaBallsSyncSettings Settings => settings ?? (settings = MetaBalls.SyncDefaults);

    /// <summary>Copies MetaBalls' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(MetaBalls.SyncDefaults);
    }
}
