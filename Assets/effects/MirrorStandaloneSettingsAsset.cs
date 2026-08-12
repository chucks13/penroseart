// Stores Mirror's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Mirror.</summary>
[CreateAssetMenu(fileName = "MirrorSettings", menuName = "Penrose/Effect Standalone Settings/Mirror")]
public sealed class MirrorStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<MirrorStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MirrorStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Mirror);

    /// <summary>The saved Mirror Standalone Settings.</summary>
    public MirrorStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies Mirror's current file-local Standalone Defaults, including the layout range's Rails,
    /// over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Mirror.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Mirror's current in-file Standalone Defaults and Rails.</summary>
    private static MirrorStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new MirrorStandaloneSettings();
        defaultSettings.CopyFrom(Mirror.StandaloneDefaults);
        return defaultSettings;
    }
}
