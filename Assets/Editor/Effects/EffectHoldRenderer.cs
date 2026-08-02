// Shared IMGUI drawing for the Held Effect control.
// DirectorStatus supplies live facts; the selected row writes Controller.heldEffect directly.
#nullable enable

using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws one catalog selector that chooses between normal switching and holding an Effect.
/// </summary>
internal static class EffectHoldRenderer
{
    /// <summary>Popup label for the <c>-1</c> sentinel that permits normal switching.</summary>
    private const string RandomLabel = "Random (normal switching)";

    /// <summary>Explains that selecting an Effect activates Hold instead of staging the next Cue.</summary>
    private static readonly GUIContent ControlLabel = new(
        "Effect / Hold",
        "Select Random for normal switching. Select an Effect to hold it until you select Random again.");

    /// <summary>Cached popup rows in runtime catalog order, prefixed by the Random sentinel.</summary>
    private static string[]? cachedOptions;

    /// <summary>Draws the shared Effect / Hold selector and its runtime-backed state.</summary>
    /// <param name="controller">The Controller whose <see cref="Controller.heldEffect"/> selection the control edits.</param>
    /// <param name="status">The Director snapshot supplying the held identity and Standalone cadence state.</param>
    public static void Draw(Controller controller, DirectorStatus status)
    {
        var options = GetOptions();
        var current = status.Mode == DirectorMode.NotReady
            ? controller.heldEffect
            : status.HeldEffectIndex;
        var shown = current >= 0 && current < options.Length - 1 ? current + 1 : 0;

        using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        var selectedRow = EditorGUILayout.Popup(ControlLabel, shown, options);
        if (EditorGUI.EndChangeCheck())
        {
            ApplySelection(controller, selectedRow, options);
        }

        EditorGUILayout.LabelField("State", FormatStatus(status));
    }

    /// <summary>Maps a popup row to <see cref="Controller.heldEffect"/> and records the operator change for Undo.</summary>
    /// <param name="controller">The Controller receiving the held catalog index or Random sentinel.</param>
    /// <param name="selectedRow">Popup row zero for Random, or an Effect row one past its catalog index.</param>
    /// <param name="options">Popup labels used to identify the selected Effect in the Play Mode log.</param>
    private static void ApplySelection(Controller controller, int selectedRow, string[] options)
    {
        var next = selectedRow - 1;
        if (next == controller.heldEffect)
        {
            return;
        }

        Undo.RecordObject(controller, "Change Effect Hold");
        controller.heldEffect = next;

        if (Application.isPlaying)
        {
            Debug.Log(next < 0
                ? "[Controller] Hold released. Random selected. Normal switching resumed."
                : $"[Controller] Hold active. Effect {options[selectedRow]} (index {next}).");
        }
    }

    /// <summary>Formats the held identity and cadence state from the Director snapshot.</summary>
    /// <param name="status">The current Director snapshot.</param>
    /// <returns>A concise statement of normal rotation, wall pinning, or unavailable runtime state.</returns>
    private static string FormatStatus(DirectorStatus status)
    {
        if (status.Mode == DirectorMode.NotReady)
        {
            return "Selection applies when the Controller runs.";
        }

        if (status.HeldEffectIndex < 0)
        {
            return "Random · normal switching";
        }

        var heldEffect = ControllerStatusText.FormatHeldEffect(status);
        return status.IsStandaloneCadenceFrozen
            ? $"{heldEffect} · Hold active · Standalone cadence frozen"
            : $"{heldEffect} · Hold active";
    }

    /// <summary>Builds the Random row and reflection-discovered Effect catalog once per domain load.</summary>
    /// <returns>Popup labels whose non-Random rows map directly to runtime Effect catalog indices.</returns>
    private static string[] GetOptions()
    {
        if (cachedOptions != null)
        {
            return cachedOptions;
        }

        var names = new Factory<EffectBase>().Names;
        cachedOptions = new string[names.Length + 1];
        cachedOptions[0] = RandomLabel;
        for (var i = 0; i < names.Length; i++)
        {
            cachedOptions[i + 1] = names[i];
        }

        return cachedOptions;
    }
}
