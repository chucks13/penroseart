// Stores Julia's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by Julia.</summary>
[CreateAssetMenu(fileName = "JuliaSettings", menuName = "Penrose/Effect Sync Settings/Julia")]
public sealed class JuliaSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<JuliaSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private JuliaSyncSettings settings = Julia.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Julia);

    /// <summary>The saved Julia Sync Settings.</summary>
    public JuliaSyncSettings Settings => settings ?? (settings = Julia.SyncDefaults);

    /// <summary>Copies Julia's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(Julia.SyncDefaults);
    }
}
