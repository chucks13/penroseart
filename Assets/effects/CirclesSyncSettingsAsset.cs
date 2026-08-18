// Stores Circles' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Circles.</summary>
[CreateAssetMenu(fileName = "CirclesSettings", menuName = "Penrose/Effect Sync Settings/Circles")]
public sealed class CirclesSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<CirclesSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private CirclesSyncSettings settings = Circles.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Circles);

    /// <summary>The saved Circles Sync Settings.</summary>
    public CirclesSyncSettings Settings => settings ?? (settings = Circles.SyncDefaults);

    /// <summary>Copies Circles' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Circles.SyncDefaults);
    }
}
