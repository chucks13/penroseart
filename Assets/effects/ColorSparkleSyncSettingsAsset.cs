// Stores ColorSparkle's typed Sync Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Sync Settings asset used by ColorSparkle.</summary>
[CreateAssetMenu(fileName = "ColorSparkleSettings", menuName = "Penrose/Effect Sync Settings/ColorSparkle")]
public sealed class ColorSparkleSyncSettingsAsset : EffectSyncSettingsAsset, ISyncSettingsAsset<ColorSparkleSyncSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private ColorSparkleSyncSettings settings = ColorSparkle.SyncDefaults;

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(ColorSparkle);

    /// <summary>The saved ColorSparkle Sync Settings.</summary>
    public ColorSparkleSyncSettings Settings => settings ?? (settings = ColorSparkle.SyncDefaults);

    /// <summary>Copies ColorSparkle's current file-local Sync Defaults over the saved Sync Settings.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(ColorSparkle.SyncDefaults);
    }
}
