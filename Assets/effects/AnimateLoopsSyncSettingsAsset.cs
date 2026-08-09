// Stores AnimateLoops' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by AnimateLoops.</summary>
[CreateAssetMenu(fileName = "AnimateLoopsSettings", menuName = "Penrose/Effect Sync Settings/AnimateLoops")]
public sealed class AnimateLoopsSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<AnimateLoopsSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnimateLoopsSyncSettings settings = AnimateLoops.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(AnimateLoops);

    /// <summary>The saved AnimateLoops Sync Settings.</summary>
    public AnimateLoopsSyncSettings Settings => settings ?? (settings = AnimateLoops.SyncDefaults);

    /// <summary>Copies AnimateLoops' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(AnimateLoops.SyncDefaults);
    }
}
