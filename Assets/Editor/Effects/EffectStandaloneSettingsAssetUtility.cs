// Creates, loads, edits, and restores typed per-Effect Standalone Settings assets through Unity's asset APIs.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only asset operations for Effect-specific Standalone Settings.</summary>
public static class EffectStandaloneSettingsAssetUtility
{
    /// <summary>Default project folder whose Resources-relative path matches the runtime provider.</summary>
    public const string DefaultAssetFolder = "Assets/effects/Resources/EffectStandaloneSettings";

    /// <summary>Returns the project asset path for an Effect's Standalone Settings asset.</summary>
    public static string AssetPathFor(Type effectType, string assetFolder = DefaultAssetFolder)
    {
        if (effectType == null)
        {
            throw new ArgumentNullException(nameof(effectType));
        }

        return $"{assetFolder}/{effectType.Name}Settings.asset";
    }

    /// <summary>Loads an existing Effect Standalone Settings asset without creating folders or dirty state.</summary>
    public static EffectStandaloneSettingsAsset LoadAsset(
        Type effectType,
        string assetFolder = DefaultAssetFolder)
    {
        return AssetDatabase.LoadAssetAtPath<EffectStandaloneSettingsAsset>(AssetPathFor(effectType, assetFolder));
    }

    /// <summary>Applies pending serialized Standalone Settings edits and marks the asset for persistence.</summary>
    public static bool ApplySettings(SerializedObject serializedAsset)
    {
        if (serializedAsset == null)
        {
            throw new ArgumentNullException(nameof(serializedAsset));
        }

        var changed = serializedAsset.ApplyModifiedProperties();
        if (changed)
        {
            EditorUtility.SetDirty(serializedAsset.targetObject);
        }

        return changed;
    }

    /// <summary>Creates a missing typed Standalone Settings asset; an existing asset is returned unchanged.</summary>
    public static EffectStandaloneSettingsAsset EnsureAsset(
        Type effectType,
        string assetFolder = DefaultAssetFolder)
    {
        var assetType = EffectStandaloneSettingsProvider.SettingsAssetTypeFor(effectType);
        if (assetType == null)
        {
            throw new InvalidOperationException($"{effectType.FullName} has no Standalone Settings asset type.");
        }

        EnsureFolder(assetFolder);
        var existing = LoadAsset(effectType, assetFolder);
        if (existing != null)
        {
            return existing;
        }

        var asset = ScriptableObject.CreateInstance(assetType) as EffectStandaloneSettingsAsset;
        if (asset == null)
        {
            throw new InvalidOperationException($"Could not create Standalone Settings asset type {assetType.FullName}.");
        }

        asset.Initialize(effectType);
        AssetDatabase.CreateAsset(asset, AssetPathFor(effectType, assetFolder));
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Restores an existing or newly created asset from its Effect's file-local Standalone Defaults.</summary>
    public static EffectStandaloneSettingsAsset RestoreStandaloneDefaults(
        Type effectType,
        string assetFolder = DefaultAssetFolder)
    {
        var asset = EnsureAsset(effectType, assetFolder);
        Undo.RecordObject(asset, $"Restore {effectType.Name} Standalone Defaults");
        asset.RestoreStandaloneDefaults();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Creates missing Standalone Settings assets for every fitted Effect in the runtime catalog.</summary>
    public static IReadOnlyList<EffectStandaloneSettingsAsset> EnsureCatalogAssets(
        string assetFolder = DefaultAssetFolder)
    {
        var assets = new List<EffectStandaloneSettingsAsset>();
        var factory = new Factory<EffectBase>();
        foreach (var effectType in factory.Types)
        {
            if (EffectStandaloneSettingsProvider.SettingsAssetTypeFor(effectType) != null)
            {
                assets.Add(EnsureAsset(effectType, assetFolder));
            }
        }

        return assets;
    }

    /// <summary>Creates every missing fitted Effect Standalone Settings asset from the Unity menu.</summary>
    [MenuItem("Window/Penrose/Create Missing Effect Standalone Settings")]
    public static void EnsureCatalogAssetsMenu()
    {
        var assets = EnsureCatalogAssets();
        Debug.Log($"Ensured {assets.Count} Effect Standalone Settings assets in {DefaultAssetFolder}.");
    }

    /// <summary>Creates the requested asset folder through Unity's refreshable project filesystem.</summary>
    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        Directory.CreateDirectory(assetFolder);
        AssetDatabase.Refresh();
    }
}
