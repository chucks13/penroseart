// Stores Pulse's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Pulse.</summary>
[CreateAssetMenu(fileName = "PulseSettings", menuName = "Penrose/Effect Standalone Settings/Pulse")]
public sealed class PulseStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<PulseStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private PulseStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Pulse);

    /// <summary>The saved Pulse Standalone Settings.</summary>
    public PulseStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies Pulse's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Pulse.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Pulse's current in-file Standalone Defaults and Rails.</summary>
    private static PulseStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new PulseStandaloneSettings();
        defaultSettings.CopyFrom(Pulse.StandaloneDefaults);
        return defaultSettings;
    }
}
