// Stores Nibbler's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Nibbler.</summary>
[CreateAssetMenu(fileName = "NibblerSettings", menuName = "Penrose/Effect Standalone Settings/Nibbler")]
public sealed class NibblerStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<NibblerStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NibblerStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Nibbler);

    /// <summary>The saved Nibbler Standalone Settings.</summary>
    public NibblerStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Nibbler's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Nibbler.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Nibbler's current in-file Standalone Defaults and Rails.</summary>
    private static NibblerStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new NibblerStandaloneSettings();
        defaultSettings.CopyFrom(Nibbler.StandaloneDefaults);
        return defaultSettings;
    }
}
