// Stores Noise's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Noise.</summary>
[CreateAssetMenu(fileName = "NoiseSettings", menuName = "Penrose/Effect Sync Settings/Noise")]
public sealed class NoiseSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<NoiseSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseSyncSettings settings = Noise.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Noise);

    /// <summary>The saved Noise Sync Settings.</summary>
    public NoiseSyncSettings Settings => settings ?? (settings = Noise.SyncDefaults);

    /// <summary>Copies Noise's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Noise.SyncDefaults);
    }
}
