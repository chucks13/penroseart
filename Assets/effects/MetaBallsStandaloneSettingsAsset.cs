// Stores MetaBalls' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by MetaBalls.</summary>
[CreateAssetMenu(fileName = "MetaBallsSettings", menuName = "Penrose/Effect Standalone Settings/MetaBalls")]
public sealed class MetaBallsStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<MetaBallsStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MetaBallsStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(MetaBalls);

    /// <summary>The saved MetaBalls Standalone Settings.</summary>
    public MetaBallsStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>
    /// Copies MetaBalls' current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(MetaBalls.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of MetaBalls' current in-file Standalone Defaults and Rails.</summary>
    private static MetaBallsStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new MetaBallsStandaloneSettings();
        defaultSettings.CopyFrom(MetaBalls.StandaloneDefaults);
        return defaultSettings;
    }
}
