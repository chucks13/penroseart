// Stores AnimateShapes' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by AnimateShapes.</summary>
[CreateAssetMenu(fileName = "AnimateShapesSettings", menuName = "Penrose/Effect Standalone Settings/AnimateShapes")]
public sealed class AnimateShapesStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<AnimateShapesStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnimateShapesStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(AnimateShapes);

    /// <summary>The saved AnimateShapes Standalone Settings.</summary>
    public AnimateShapesStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies AnimateShapes' current file-local Standalone Defaults over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(AnimateShapes.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of AnimateShapes' current in-file Standalone Defaults.</summary>
    private static AnimateShapesStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new AnimateShapesStandaloneSettings();
        defaultSettings.CopyFrom(AnimateShapes.StandaloneDefaults);
        return defaultSettings;
    }
}
