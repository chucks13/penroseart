// Resolves effective per-Effect Sync Settings from Resources assets with file-local defaults as fallback.
using System;
using System.Reflection;
using UnityEngine;

/// <summary>Declares the concrete saved Sync Settings asset type fitted to an Effect.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EffectSyncSettingsAttribute : Attribute
{
    /// <summary>Creates an Effect-to-asset declaration after validating the asset contract.</summary>
    public EffectSyncSettingsAttribute(Type assetType)
    {
        if (assetType == null)
        {
            throw new ArgumentNullException(nameof(assetType));
        }

        if (!typeof(EffectSyncSettingsAsset).IsAssignableFrom(assetType))
        {
            throw new ArgumentException(
                $"{assetType.FullName} is not an Effect Sync Settings asset type.",
                nameof(assetType));
        }

        AssetType = assetType;
    }

    /// <summary>The concrete typed ScriptableObject used for this Effect's saved Sync Settings.</summary>
    public Type AssetType { get; }
}

/// <summary>Resolves the saved Sync Settings for a typed Effect without imposing a shared settings shape.</summary>
public static class EffectSyncSettingsProvider
{
    /// <summary>Resources folder containing per-Effect Sync Settings assets.</summary>
    public const string ResourceFolder = "EffectSyncSettings";

    /// <summary>Returns the Resources path used for an Effect's Sync Settings asset.</summary>
    public static string ResourcePathFor(Type effectType)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        return $"{ResourceFolder}/{effectType.Name}Settings";
    }

    /// <summary>Returns the Sync Settings asset type declared by a fitted Effect, or null when not fitted.</summary>
    public static Type SettingsAssetTypeFor(Type effectType)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        return effectType.GetCustomAttribute<EffectSyncSettingsAttribute>()?.AssetType;
    }

    /// <summary>
    /// Returns saved Sync Settings when a correctly typed asset exists; otherwise returns Sync Defaults.
    /// </summary>
    public static TSettings Resolve<TSettings>(
        Type effectType,
        TSettings syncDefaults)
        where TSettings : class
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        if (syncDefaults == null)
        {
            throw new ArgumentNullException(nameof(syncDefaults));
        }

        var settingsAssetType = SettingsAssetTypeFor(effectType);
        if (settingsAssetType == null)
        {
            throw new InvalidOperationException($"{effectType.FullName} has no Sync Settings asset type.");
        }

        var asset = Resources.Load(ResourcePathFor(effectType), settingsAssetType) as EffectSyncSettingsAsset;
        if (asset == null)
        {
            return syncDefaults;
        }

        if (asset.EffectType != effectType)
        {
            throw new InvalidOperationException(
                $"{asset.name} configures {asset.EffectType.FullName}, not {effectType.FullName}.");
        }

        if (!(asset is ISyncSettingsAsset<TSettings> typedAsset))
        {
            throw new InvalidOperationException(
                $"{asset.GetType().FullName} does not expose {typeof(TSettings).FullName} Sync Settings.");
        }

        return typedAsset.Settings;
    }
}
