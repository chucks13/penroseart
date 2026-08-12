// Stores ShapeGlitch's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by ShapeGlitch.</summary>
[CreateAssetMenu(fileName = "ShapeGlitchSettings", menuName = "Penrose/Effect Standalone Settings/ShapeGlitch")]
public sealed class ShapeGlitchStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<ShapeGlitchStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private ShapeGlitchStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(ShapeGlitch);

    /// <summary>The saved ShapeGlitch Standalone Settings.</summary>
    public ShapeGlitchStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies ShapeGlitch's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(ShapeGlitch.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of ShapeGlitch's current in-file Standalone Defaults.</summary>
    private static ShapeGlitchStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new ShapeGlitchStandaloneSettings();
        defaultSettings.CopyFrom(ShapeGlitch.StandaloneDefaults);
        return defaultSettings;
    }
}
