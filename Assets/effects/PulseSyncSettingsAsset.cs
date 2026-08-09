// Stores Pulse's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Pulse.</summary>
[CreateAssetMenu(fileName = "PulseSettings", menuName = "Penrose/Effect Sync Settings/Pulse")]
public sealed class PulseSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<PulseSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private PulseSyncSettings settings = Pulse.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Pulse);

    /// <summary>The saved Pulse Sync Settings.</summary>
    public PulseSyncSettings Settings => settings ?? (settings = Pulse.SyncDefaults);

    /// <summary>Copies Pulse's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Pulse.SyncDefaults);
    }
}
