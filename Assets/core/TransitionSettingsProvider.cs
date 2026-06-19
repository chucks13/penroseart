using System;
using UnityEngine;

/// <summary>
/// Resolves the effective Transition Settings for runtime code without depending on UnityEditor APIs.
/// </summary>
public static class TransitionSettingsProvider
{
    public const string ResourceFolder = "TransitionSettings";

    /// <summary>Returns the Resources path used for a Transition's settings asset.</summary>
    public static string ResourcePathFor(Type transitionType)
    {
        if (transitionType == null)
        {
            throw new ArgumentNullException(nameof(transitionType));
        }

        return $"{ResourceFolder}/{transitionType.Name}Settings";
    }

    /// <summary>
    /// Reads the saved settings asset for the Transition when present; otherwise returns the supplied Code Defaults.
    /// </summary>
    public static TransitionSettings Resolve(Type transitionType, TransitionSettings codeDefaults)
    {
        if (codeDefaults == null)
        {
            throw new ArgumentNullException(nameof(codeDefaults));
        }

        var asset = Resources.Load<TransitionSettingsAsset>(ResourcePathFor(transitionType));
        return asset != null ? asset.Settings : codeDefaults;
    }
}
