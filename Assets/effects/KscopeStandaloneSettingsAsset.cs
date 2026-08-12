// Stores Kscope's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Kscope.</summary>
[CreateAssetMenu(fileName = "KscopeSettings", menuName = "Penrose/Effect Standalone Settings/Kscope")]
public sealed class KscopeStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<KscopeStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private KscopeStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Kscope);

    /// <summary>The saved Kscope Standalone Settings.</summary>
    public KscopeStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Kscope's current file-local Standalone Defaults, including every range's Rails, over
    /// the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Kscope.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Kscope's current in-file Standalone Defaults and Rails.</summary>
    private static KscopeStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new KscopeStandaloneSettings();
        defaultSettings.CopyFrom(Kscope.StandaloneDefaults);
        return defaultSettings;
    }
}
