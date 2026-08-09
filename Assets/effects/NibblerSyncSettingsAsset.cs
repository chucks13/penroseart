// Stores Nibbler's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Nibbler.</summary>
[CreateAssetMenu(fileName = "NibblerSettings", menuName = "Penrose/Effect Sync Settings/Nibbler")]
public sealed class NibblerSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<NibblerSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NibblerSyncSettings settings = Nibbler.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Nibbler);

    /// <summary>The saved Nibbler Sync Settings.</summary>
    public NibblerSyncSettings Settings => settings ?? (settings = Nibbler.SyncDefaults);

    /// <summary>Copies Nibbler's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Nibbler.SyncDefaults);
    }
}
