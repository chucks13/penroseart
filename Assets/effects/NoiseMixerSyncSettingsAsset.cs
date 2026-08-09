// Stores NoiseMixer's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by NoiseMixer.</summary>
[CreateAssetMenu(fileName = "NoiseMixerSettings", menuName = "Penrose/Effect Sync Settings/NoiseMixer")]
public sealed class NoiseMixerSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<NoiseMixerSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseMixerSyncSettings settings = NoiseMixer.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(NoiseMixer);

    /// <summary>The saved NoiseMixer Sync Settings.</summary>
    public NoiseMixerSyncSettings Settings => settings ?? (settings = NoiseMixer.SyncDefaults);

    /// <summary>Copies NoiseMixer's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(NoiseMixer.SyncDefaults);
    }
}
