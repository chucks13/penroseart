using System;
using UnityEngine;

/// <summary>
/// Persistent Unity asset containing the editable settings copy for one Transition type.
/// </summary>
[CreateAssetMenu(fileName = "TransitionSettings", menuName = "Penrose/Transition Settings")]
public sealed class TransitionSettingsAsset : ScriptableObject
{
    [SerializeField] private string transitionTypeName = string.Empty;
    [SerializeField] private TransitionSettings settings = new TransitionSettings();

    /// <summary>Full C# type name of the Transition this asset configures.</summary>
    public string TransitionTypeName => transitionTypeName;

    /// <summary>Editable saved authoring values for the Transition.</summary>
    public TransitionSettings Settings => settings ?? (settings = new TransitionSettings());

    /// <summary>Initializes a newly created asset from a Transition's Code Defaults.</summary>
    public void Initialize(string transitionTypeName, TransitionSettings codeDefaults)
    {
        if (string.IsNullOrWhiteSpace(transitionTypeName))
        {
            throw new ArgumentException("Transition type name is required.", nameof(transitionTypeName));
        }

        this.transitionTypeName = transitionTypeName;
        RestoreDefaults(codeDefaults);
    }

    /// <summary>Copies a Transition's current Code Defaults over the saved settings.</summary>
    public void RestoreDefaults(TransitionSettings codeDefaults)
    {
        Settings.CopyFrom(codeDefaults ?? throw new ArgumentNullException(nameof(codeDefaults)));
    }
}
