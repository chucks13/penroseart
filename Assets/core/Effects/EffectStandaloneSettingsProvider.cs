// Resolves effective per-Effect Standalone Settings from Resources assets with file-local defaults as fallback.
using System;
using System.Reflection;
using UnityEngine;

/// <summary>Declares the concrete saved Standalone Settings asset type fitted to an Effect.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EffectStandaloneSettingsAttribute : Attribute
{
    /// <summary>Creates an Effect-to-asset declaration after validating the asset contract.</summary>
    public EffectStandaloneSettingsAttribute(Type assetType)
    {
        if (assetType == null)
        {
            throw new ArgumentNullException(nameof(assetType));
        }

        if (!typeof(EffectStandaloneSettingsAsset).IsAssignableFrom(assetType))
        {
            throw new ArgumentException(
                $"{assetType.FullName} is not an Effect Standalone Settings asset type.",
                nameof(assetType));
        }

        AssetType = assetType;
    }

    /// <summary>The concrete typed ScriptableObject used for this Effect's saved Standalone Settings.</summary>
    public Type AssetType { get; }
}

/// <summary>Resolves the saved Standalone Settings for a typed Effect without imposing a shared settings shape.</summary>
public static class EffectStandaloneSettingsProvider
{
    /// <summary>Resources folder containing per-Effect Standalone Settings assets.</summary>
    public const string ResourceFolder = "EffectStandaloneSettings";

    /// <summary>Returns the Resources path used for an Effect's Standalone Settings asset.</summary>
    public static string ResourcePathFor(Type effectType)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        return $"{ResourceFolder}/{effectType.Name}Settings";
    }

    /// <summary>Returns the Standalone Settings asset type declared by a fitted Effect, or null when not fitted.</summary>
    public static Type SettingsAssetTypeFor(Type effectType)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        return effectType.GetCustomAttribute<EffectStandaloneSettingsAttribute>()?.AssetType;
    }

    /// <summary>
    /// Returns saved Standalone Settings when a correctly typed asset exists; otherwise returns Standalone Defaults.
    /// </summary>
    /// <remarks>
    /// Resolution consumes no <see cref="UnityEngine.Random"/> values, so it cannot disturb an Effect's Roll order.
    /// </remarks>
    public static TSettings Resolve<TSettings>(
        Type effectType,
        TSettings standaloneDefaults)
        where TSettings : class
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        if (standaloneDefaults == null)
        {
            throw new ArgumentNullException(nameof(standaloneDefaults));
        }

        var settingsAssetType = SettingsAssetTypeFor(effectType);
        if (settingsAssetType == null)
        {
            throw new InvalidOperationException($"{effectType.FullName} has no Standalone Settings asset type.");
        }

        var asset = Resources.Load(ResourcePathFor(effectType), settingsAssetType) as EffectStandaloneSettingsAsset;
        if (asset == null)
        {
            return standaloneDefaults;
        }

        if (asset.EffectType != effectType)
        {
            throw new InvalidOperationException(
                $"{asset.name} configures {asset.EffectType.FullName}, not {effectType.FullName}.");
        }

        if (!(asset is IStandaloneSettingsAsset<TSettings> typedAsset))
        {
            throw new InvalidOperationException(
                $"{asset.GetType().FullName} does not expose {typeof(TSettings).FullName} Standalone Settings.");
        }

        return typedAsset.Settings;
    }
}
