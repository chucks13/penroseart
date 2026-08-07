// Stores Ripple's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Ripple.</summary>
[CreateAssetMenu(fileName = "RippleSettings", menuName = "Penrose/Effect Sync Settings/Ripple")]
public sealed class RippleSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<RippleSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RippleSyncSettings settings = Ripple.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Ripple);

    /// <summary>The saved Ripple Sync Settings.</summary>
    public RippleSyncSettings Settings => settings ?? (settings = Ripple.SyncDefaults);

    /// <summary>Copies Ripple's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Ripple.SyncDefaults);
    }
}
