// Stores Waterfall's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Waterfall.</summary>
[CreateAssetMenu(fileName = "WaterfallSettings", menuName = "Penrose/Effect Sync Settings/Waterfall")]
public sealed class WaterfallSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<WaterfallSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private WaterfallSyncSettings settings = Waterfall.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Waterfall);

    /// <summary>The saved Waterfall Sync Settings.</summary>
    public WaterfallSyncSettings Settings => settings ?? (settings = Waterfall.SyncDefaults);

    /// <summary>
    /// Copies Waterfall's current file-local Sync Defaults, including each range's editor rails, over
    /// the saved Sync Settings.
    /// </summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Waterfall.SyncDefaults);
    }
}
