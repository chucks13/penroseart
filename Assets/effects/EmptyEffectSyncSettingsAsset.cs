// Stores EmptyEffect's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by EmptyEffect.</summary>
[CreateAssetMenu(fileName = "EmptyEffectSettings", menuName = "Penrose/Effect Sync Settings/EmptyEffect")]
public sealed class EmptyEffectSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<EmptyEffectSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private EmptyEffectSyncSettings settings = EmptyEffect.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(EmptyEffect);

    /// <summary>The saved EmptyEffect Sync Settings.</summary>
    public EmptyEffectSyncSettings Settings => settings ?? (settings = EmptyEffect.SyncDefaults);

    /// <summary>Copies EmptyEffect's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(EmptyEffect.SyncDefaults);
    }
}
