// Stores YinYangMixer's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by YinYangMixer.</summary>
[CreateAssetMenu(fileName = "YinYangMixerSettings", menuName = "Penrose/Effect Sync Settings/YinYangMixer")]
public sealed class YinYangMixerSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<YinYangMixerSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private YinYangMixerSyncSettings settings = YinYangMixer.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(YinYangMixer);

    /// <summary>The saved YinYangMixer Sync Settings.</summary>
    public YinYangMixerSyncSettings Settings => settings ?? (settings = YinYangMixer.SyncDefaults);

    /// <summary>Copies YinYangMixer's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(YinYangMixer.SyncDefaults);
    }
}
