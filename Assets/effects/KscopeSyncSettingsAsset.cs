// Stores Kscope's saved Sync Settings for live Effect tuning and default restoration.
using System;
using UnityEngine;

/// <summary>Typed ScriptableObject carrying Kscope's saved Sync Settings.</summary>
[CreateAssetMenu(fileName = "KscopeSettings", menuName = "Penrose/Effect Sync Settings/Kscope")]
public sealed class KscopeSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<KscopeSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private KscopeSyncSettings settings = Kscope.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Kscope);

    /// <summary>The saved Kscope Sync Settings.</summary>
    public KscopeSyncSettings Settings => settings ?? (settings = Kscope.SyncDefaults);

    /// <summary>Copies Kscope's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Kscope.SyncDefaults);
    }
}
