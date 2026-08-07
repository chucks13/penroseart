// Test-only saved Sync Settings asset, in its own matching-name file so Unity keeps the script association.
using System;
using UnityEngine;

/// <summary>Test-only saved Sync Settings asset for <see cref="TestSettingsEffect"/>.</summary>
public sealed class TestEffectSyncSettingsAsset :
    EffectSyncSettingsAsset,
    ISyncSettingsAsset<TestEffectSyncSettings>
{
    /// <summary>Serialized saved test settings.</summary>
    [SerializeField]
    private TestEffectSyncSettings settings = TestSettingsEffect.SyncDefaults;

    /// <summary>The test Effect configured by this asset.</summary>
    public override Type EffectType => typeof(TestSettingsEffect);

    /// <summary>The saved test Sync Settings.</summary>
    public TestEffectSyncSettings Settings => settings ?? (settings = TestSettingsEffect.SyncDefaults);

    /// <summary>Restores the test Effect's file-local Sync Defaults.</summary>
    public override void RestoreSyncDefaults()
    {
        Settings.CopyFrom(TestSettingsEffect.SyncDefaults);
    }
}
