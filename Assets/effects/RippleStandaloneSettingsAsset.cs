// Stores Ripple's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Ripple.</summary>
[CreateAssetMenu(fileName = "RippleSettings", menuName = "Penrose/Effect Standalone Settings/Ripple")]
public sealed class RippleStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<RippleStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private RippleStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Ripple);

    /// <summary>The saved Ripple Standalone Settings.</summary>
    public RippleStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Ripple's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Ripple.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Ripple's current in-file Standalone Defaults and Rails.</summary>
    private static RippleStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new RippleStandaloneSettings();
        defaultSettings.CopyFrom(Ripple.StandaloneDefaults);
        return defaultSettings;
    }
}
