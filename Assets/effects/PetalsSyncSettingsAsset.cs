// Stores Petals' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Petals.</summary>
[CreateAssetMenu(fileName = "PetalsSettings", menuName = "Penrose/Effect Sync Settings/Petals")]
public sealed class PetalsSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<PetalsSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private PetalsSyncSettings settings = Petals.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Petals);

    /// <summary>The saved Petals Sync Settings.</summary>
    public PetalsSyncSettings Settings => settings ?? (settings = Petals.SyncDefaults);

    /// <summary>Copies Petals' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Petals.SyncDefaults);
    }
}
