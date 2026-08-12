// Stores NoiseTunnel's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by NoiseTunnel.</summary>
[CreateAssetMenu(fileName = "NoiseTunnelSettings", menuName = "Penrose/Effect Standalone Settings/NoiseTunnel")]
public sealed class NoiseTunnelStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<NoiseTunnelStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private NoiseTunnelStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(NoiseTunnel);

    /// <summary>The saved NoiseTunnel Standalone Settings.</summary>
    public NoiseTunnelStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies NoiseTunnel's current file-local Standalone Defaults, including every range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(NoiseTunnel.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of NoiseTunnel's current Standalone Defaults and Rails.</summary>
    private static NoiseTunnelStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new NoiseTunnelStandaloneSettings();
        defaultSettings.CopyFrom(NoiseTunnel.StandaloneDefaults);
        return defaultSettings;
    }
}
