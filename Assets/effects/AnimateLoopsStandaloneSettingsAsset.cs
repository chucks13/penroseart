// Stores AnimateLoops' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by AnimateLoops.</summary>
[CreateAssetMenu(fileName = "AnimateLoopsSettings", menuName = "Penrose/Effect Standalone Settings/AnimateLoops")]
public sealed class AnimateLoopsStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<AnimateLoopsStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private AnimateLoopsStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(AnimateLoops);

    /// <summary>The saved AnimateLoops Standalone Settings.</summary>
    public AnimateLoopsStandaloneSettings Settings => settings ??= CreateDefaultSettings();

    /// <summary>
    /// Copies AnimateLoops' current file-local Standalone Defaults, including the distortion-mode
    /// Rails, over the saved Standalone Settings.
    /// </summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(AnimateLoops.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of AnimateLoops' current in-file Standalone Defaults.</summary>
    private static AnimateLoopsStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new AnimateLoopsStandaloneSettings();
        defaultSettings.CopyFrom(AnimateLoops.StandaloneDefaults);
        return defaultSettings;
    }
}
