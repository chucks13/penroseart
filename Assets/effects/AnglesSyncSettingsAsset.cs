// Stores Angles' typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Angles.</summary>
[CreateAssetMenu(fileName = "AnglesSettings", menuName = "Penrose/Effect Sync Settings/Angles")]
public sealed class AnglesSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<AnglesSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnglesSyncSettings settings = Angles.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Angles);

    /// <summary>The saved Angles Sync Settings.</summary>
    public AnglesSyncSettings Settings => settings ?? (settings = Angles.SyncDefaults);

    /// <summary>Copies Angles' current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Angles.SyncDefaults);
    }
}
