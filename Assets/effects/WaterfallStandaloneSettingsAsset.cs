// Stores Waterfall's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Waterfall.</summary>
[CreateAssetMenu(fileName = "WaterfallSettings", menuName = "Penrose/Effect Standalone Settings/Waterfall")]
public sealed class WaterfallStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<WaterfallStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private WaterfallStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Waterfall);

    /// <summary>The saved Waterfall Standalone Settings.</summary>
    public WaterfallStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies Waterfall's current file-local Standalone Defaults, including each range's editor rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Waterfall.StandaloneDefaults);
    }

    /// <summary>
    /// Creates an asset-owned copy of Waterfall's current in-file Standalone Defaults and range rails.
    /// </summary>
    private static WaterfallStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new WaterfallStandaloneSettings();
        defaultSettings.CopyFrom(Waterfall.StandaloneDefaults);
        return defaultSettings;
    }
}
