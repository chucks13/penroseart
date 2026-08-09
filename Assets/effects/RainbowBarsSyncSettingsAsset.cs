// Stores RainbowBars' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by RainbowBars.</summary>
[CreateAssetMenu(fileName = "RainbowBarsSettings", menuName = "Penrose/Effect Sync Settings/RainbowBars")]
public sealed class RainbowBarsSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<RainbowBarsSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RainbowBarsSyncSettings settings = RainbowBars.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(RainbowBars);

    /// <summary>The saved RainbowBars Sync Settings.</summary>
    public RainbowBarsSyncSettings Settings => settings ?? (settings = RainbowBars.SyncDefaults);

    /// <summary>Copies RainbowBars' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(RainbowBars.SyncDefaults);
    }
}
