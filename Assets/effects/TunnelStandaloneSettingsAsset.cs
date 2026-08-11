// Stores Tunnel's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Tunnel.</summary>
[CreateAssetMenu(fileName = "TunnelSettings", menuName = "Penrose/Effect Standalone Settings/Tunnel")]
public sealed class TunnelStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<TunnelStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private TunnelStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Tunnel);

    /// <summary>The saved Tunnel Standalone Settings.</summary>
    public TunnelStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies Tunnel's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Tunnel.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Tunnel's current in-file Standalone Defaults and Rails.</summary>
    private static TunnelStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new TunnelStandaloneSettings();
        defaultSettings.CopyFrom(Tunnel.StandaloneDefaults);
        return defaultSettings;
    }
}
