// Stores NoiseTunnel's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by NoiseTunnel.</summary>
[CreateAssetMenu(fileName = "NoiseTunnelSettings", menuName = "Penrose/Effect Sync Settings/NoiseTunnel")]
public sealed class NoiseTunnelSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<NoiseTunnelSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseTunnelSyncSettings settings = NoiseTunnel.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(NoiseTunnel);

    /// <summary>The saved NoiseTunnel Sync Settings.</summary>
    public NoiseTunnelSyncSettings Settings => settings ?? (settings = NoiseTunnel.SyncDefaults);

    /// <summary>Copies NoiseTunnel's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(NoiseTunnel.SyncDefaults);
    }
}
