// Stores ShapeGlitch's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>
/// The saved Sync Settings asset used by ShapeGlitch.
/// This typed asset is surfaced in the Effects tab.
/// </summary>
[CreateAssetMenu(fileName = "ShapeGlitchSettings", menuName = "Penrose/Effect Sync Settings/ShapeGlitch")]
public sealed class ShapeGlitchSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<ShapeGlitchSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private ShapeGlitchSyncSettings settings = ShapeGlitch.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(ShapeGlitch);

    /// <summary>The saved ShapeGlitch Sync Settings.</summary>
    public ShapeGlitchSyncSettings Settings => settings ?? (settings = ShapeGlitch.SyncDefaults);

    /// <summary>Copies ShapeGlitch's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(ShapeGlitch.SyncDefaults);
    }
}
