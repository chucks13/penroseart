// Stores Petals' typed Standalone Settings as a Unity asset that can be edited live and restored.
using System;
using UnityEngine;

/// <summary>The saved Standalone Settings asset used by Petals.</summary>
[CreateAssetMenu(fileName = "PetalsSettings", menuName = "Penrose/Effect Standalone Settings/Petals")]
public sealed class PetalsStandaloneSettingsAsset :
    EffectStandaloneSettingsAsset,
    IStandaloneSettingsAsset<PetalsStandaloneSettings>
{
    /// <summary>The serialized saved copy edited by the Tuning Window.</summary>
    [SerializeField]
    private PetalsStandaloneSettings settings = CreateDefaultSettings();

    /// <summary>The Effect type configured by this asset.</summary>
    public override Type EffectType => typeof(Petals);

    /// <summary>The saved Petals Standalone Settings.</summary>
    public PetalsStandaloneSettings Settings => settings ?? (settings = CreateDefaultSettings());

    /// <summary>Copies Petals' current file-local Standalone Defaults over the saved settings.</summary>
    public override void RestoreStandaloneDefaults()
    {
        Settings.CopyFrom(Petals.StandaloneDefaults);
    }

    /// <summary>Creates an asset-owned copy of Petals' current in-file Standalone Defaults and Rails.</summary>
    private static PetalsStandaloneSettings CreateDefaultSettings()
    {
        var defaultSettings = new PetalsStandaloneSettings();
        defaultSettings.CopyFrom(Petals.StandaloneDefaults);
        return defaultSettings;
    }
}
