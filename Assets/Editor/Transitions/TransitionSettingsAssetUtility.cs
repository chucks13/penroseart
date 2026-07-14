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

    /// <summary>Loads an existing Transition Settings asset without creating folders, assets, or dirty state.</summary>
    public static TransitionSettingsAsset LoadAsset(Type transitionType, string assetFolder = DefaultAssetFolder)
    {
        return AssetDatabase.LoadAssetAtPath<TransitionSettingsAsset>(AssetPathFor(transitionType, assetFolder));
    }

    /// <summary>
    /// Constrains pending serialized Runway and Tail edits and applies the complete asset change as one Undo transaction.
    /// </summary>
    public static bool ApplyConstrainedSettings(SerializedObject serializedAsset)
    {
        if (serializedAsset == null)
        {
            throw new ArgumentNullException(nameof(serializedAsset));
        }

        var settings = serializedAsset.FindProperty("settings")
            ?? throw new InvalidOperationException("Transition Settings asset is missing its serialized settings field.");
        var runway = settings.FindPropertyRelative(nameof(TransitionSettings.RunwayBeats));
        var tail = settings.FindPropertyRelative(nameof(TransitionSettings.TailBeats));
        var constrainedRunway = Mathf.Clamp(runway.intValue, 0, TransitionRepertoire.MaxDurationBeats);
        runway.intValue = constrainedRunway;
        tail.intValue = Mathf.Clamp(tail.intValue, 0, TransitionRepertoire.MaxDurationBeats - constrainedRunway);
        return serializedAsset.ApplyModifiedProperties();
    }

    /// <summary>Creates the settings asset if missing, initialized from Code Defaults; existing assets are returned unchanged.</summary>
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
        var asset = LoadAsset(transitionType, assetFolder);
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
        var defaultsAsset = ScriptableObject.CreateInstance<TransitionSettingsAsset>();
        try
        {
            defaultsAsset.Initialize(transitionType.FullName ?? transitionType.Name, codeDefaults);
            var serializedAsset = new SerializedObject(asset);
            var serializedDefaults = new SerializedObject(defaultsAsset);
            serializedAsset.Update();
            serializedDefaults.Update();
            serializedAsset.CopyFromSerializedProperty(serializedDefaults.FindProperty("settings"));
            serializedAsset.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return asset;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(defaultsAsset);
        }
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
