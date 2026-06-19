using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only creation and restore helpers for per-transition settings assets.
/// </summary>
public static class TransitionSettingsAssetUtility
{
    public const string DefaultAssetFolder = "Assets/transitions/Resources/TransitionSettings";

    /// <summary>Returns the project asset path for a Transition's settings asset.</summary>
    public static string AssetPathFor(Type transitionType, string assetFolder = DefaultAssetFolder)
    {
        if (transitionType == null)
        {
            throw new ArgumentNullException(nameof(transitionType));
        }

        return $"{assetFolder}/{transitionType.Name}Settings.asset";
    }

    /// <summary>Creates the settings asset if missing, initialized from Code Defaults.</summary>
    public static TransitionSettingsAsset EnsureAsset(
        Type transitionType,
        TransitionSettings codeDefaults,
        string assetFolder = DefaultAssetFolder)
    {
        if (transitionType == null)
        {
            throw new ArgumentNullException(nameof(transitionType));
        }

        if (codeDefaults == null)
        {
            throw new ArgumentNullException(nameof(codeDefaults));
        }

        EnsureFolder(assetFolder);
        var assetPath = AssetPathFor(transitionType, assetFolder);
        var asset = AssetDatabase.LoadAssetAtPath<TransitionSettingsAsset>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<TransitionSettingsAsset>();
        asset.Initialize(transitionType.FullName ?? transitionType.Name, codeDefaults);
        AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Restores an existing or newly created settings asset from Code Defaults.</summary>
    public static TransitionSettingsAsset RestoreDefaults(
        Type transitionType,
        TransitionSettings codeDefaults,
        string assetFolder = DefaultAssetFolder)
    {
        var asset = EnsureAsset(transitionType, codeDefaults, assetFolder);
        Undo.RecordObject(asset, $"Restore {transitionType.Name} Transition Settings Defaults");
        asset.RestoreDefaults(codeDefaults);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Creates any missing settings assets for the reflected Transition catalog.</summary>
    public static IReadOnlyList<TransitionSettingsAsset> EnsureCatalogAssets(string assetFolder = DefaultAssetFolder)
    {
        var createdOrExisting = new List<TransitionSettingsAsset>();
        var factory = new Factory<TransitionBase>();
        foreach (var transitionType in factory.Types)
        {
            var transition = factory.Create(transitionType);
            if (transition == null)
            {
                throw new InvalidOperationException($"Could not instantiate Transition type {transitionType.FullName}.");
            }

            createdOrExisting.Add(EnsureAsset(transitionType, transition.CodeDefaults, assetFolder));
        }

        return createdOrExisting;
    }

    [MenuItem("Window/Penrose/Create Missing Transition Settings")]
    public static void EnsureCatalogAssetsMenu()
    {
        var assets = EnsureCatalogAssets();
        Debug.Log($"Ensured {assets.Count} Transition Settings assets in {DefaultAssetFolder}.");
    }

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
