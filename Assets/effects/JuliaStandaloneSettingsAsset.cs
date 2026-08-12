// Stores Julia's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Julia.</summary>
[CreateAssetMenu(fileName = "JuliaSettings", menuName = "Penrose/Effect Standalone Settings/Julia")]
public sealed class JuliaStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<JuliaStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private JuliaStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Julia);

    /// <summary>The saved Julia Standalone Settings.</summary>
    public JuliaStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Julia's current file-local Standalone Defaults, including range Rails and preset
    /// tables, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Julia.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Julia's current in-file Standalone Defaults.</summary>
    private static JuliaStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new JuliaStandaloneSettings();
        defaultSettings.CopyFrom(Julia.StandaloneDefaults);
        return defaultSettings;
    }
}
