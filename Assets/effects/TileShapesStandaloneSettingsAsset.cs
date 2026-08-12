// Stores TileShapes' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by TileShapes.</summary>
[CreateAssetMenu(fileName = "TileShapesSettings", menuName = "Penrose/Effect Standalone Settings/TileShapes")]
public sealed class TileShapesStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<TileShapesStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private TileShapesStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(TileShapes);

    /// <summary>The saved TileShapes Standalone Settings.</summary>
    public TileShapesStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>Copies TileShapes' current file-local Standalone Defaults over the saved Settings.</summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(TileShapes.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of TileShapes' current in-file Standalone Defaults.</summary>
    private static TileShapesStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new TileShapesStandaloneSettings();
        defaultSettings.CopyFrom(TileShapes.StandaloneDefaults);
        return defaultSettings;
    }
}
