// Defines the typed runtime contract shared by per-Effect Sync Settings assets.
using System;
using UnityEngine;

/// <summary>Exposes the concrete Sync Settings object held by a typed Effect asset.</summary>
public interface ISyncSettingsAsset<out TSettings>
{
    /// <summary>The saved Sync Settings read by the Effect at runtime.</summary>
    TSettings Settings { get; }
}

/// <summary>
/// Base identity and restore contract for an Effect's saved, Effect-specific Sync Settings asset.
/// </summary>
public abstract class EffectSyncSettingsAsset : ScriptableObject
{
    /// <summary>Serialized field name each concrete asset uses for its Effect-specific settings object.</summary>
    public const string SerializedSettingsFieldName = "settings";

    /// <summary>Saved full type name used to detect an asset assigned to the wrong Effect.</summary>
    [SerializeField]
    private string effectTypeName = string.Empty;

    /// <summary>The Effect type this concrete asset supports.</summary>
    public abstract Type EffectType { get; }

    /// <summary>Full C# type name of the Effect this asset was initialized for.</summary>
    public string EffectTypeName => effectTypeName;

    /// <summary>Initializes a new asset for its declared Effect and restores its Sync Defaults.</summary>
    public void Initialize(Type effectType)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        if (effectType != EffectType)
        {
            throw new ArgumentException(
                $"{GetType().Name} supports {EffectType.FullName}, not {effectType.FullName}.",
                nameof(effectType));
        }

        effectTypeName = effectType.FullName ?? effectType.Name;
        RestoreSyncDefaults();
    }

    /// <summary>Copies the Effect's file-local Sync Defaults over this asset's saved Sync Settings.</summary>
    public abstract void RestoreSyncDefaults();
}
