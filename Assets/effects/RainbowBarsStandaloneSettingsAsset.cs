// Stores RainbowBars' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by RainbowBars.</summary>
[CreateAssetMenu(fileName = "RainbowBarsSettings", menuName = "Penrose/Effect Standalone Settings/RainbowBars")]
public sealed class RainbowBarsStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<RainbowBarsStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RainbowBarsStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(RainbowBars);

    /// <summary>The saved RainbowBars Standalone Settings.</summary>
    public RainbowBarsStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies RainbowBars' current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(RainbowBars.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of RainbowBars' current in-file Standalone Defaults and Rails.</summary>
    private static RainbowBarsStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new RainbowBarsStandaloneSettings();
        defaultSettings.CopyFrom(RainbowBars.StandaloneDefaults);
        return defaultSettings;
    }
}
