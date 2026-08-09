// Stores RandomEffectsMixer's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by RandomEffectsMixer.</summary>
[CreateAssetMenu(fileName = "RandomEffectsMixerSettings", menuName = "Penrose/Effect Sync Settings/RandomEffectsMixer")]
public sealed class RandomEffectsMixerSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<RandomEffectsMixerSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RandomEffectsMixerSyncSettings settings = RandomEffectsMixer.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(RandomEffectsMixer);

    /// <summary>The saved RandomEffectsMixer Sync Settings.</summary>
    public RandomEffectsMixerSyncSettings Settings => settings ?? (settings = RandomEffectsMixer.SyncDefaults);

    /// <summary>Copies RandomEffectsMixer's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(RandomEffectsMixer.SyncDefaults);
    }
}
