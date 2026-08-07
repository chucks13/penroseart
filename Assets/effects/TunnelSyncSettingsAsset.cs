// Stores Tunnel's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Tunnel.</summary>
[CreateAssetMenu(fileName = "TunnelSettings", menuName = "Penrose/Effect Sync Settings/Tunnel")]
public sealed class TunnelSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<TunnelSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private TunnelSyncSettings settings = Tunnel.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Tunnel);

    /// <summary>The saved Tunnel Sync Settings.</summary>
    public TunnelSyncSettings Settings => settings ?? (settings = Tunnel.SyncDefaults);

    /// <summary>Copies Tunnel's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Tunnel.SyncDefaults);
    }
}
