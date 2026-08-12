// Stores MazeFlyer's typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by MazeFlyer.</summary>
[CreateAssetMenu(fileName = "MazeFlyerSettings", menuName = "Penrose/Effect Standalone Settings/MazeFlyer")]
public sealed class MazeFlyerStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<MazeFlyerStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private MazeFlyerStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(MazeFlyer);

    /// <summary>The saved MazeFlyer Standalone Settings.</summary>
    public MazeFlyerStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies MazeFlyer's current file-local Standalone Defaults, including range Rails and the
    /// curated palette, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(MazeFlyer.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of MazeFlyer's current in-file Standalone Defaults.</summary>
    private static MazeFlyerStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new MazeFlyerStandaloneSettings();
        defaultSettings.CopyFrom(MazeFlyer.StandaloneDefaults);
        return defaultSettings;
    }
}
