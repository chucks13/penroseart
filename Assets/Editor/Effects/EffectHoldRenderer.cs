// Shared IMGUI drawing for the operator's one Effect / Hold control.
// DirectorStatus supplies live facts; the selected row writes Controller.heldEffect directly.
#nullable enable

using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws one catalog selector that chooses between normal rotation and pinning an Effect to the wall.
/// </summary>
internal static class EffectHoldRenderer
{
    /// <summary>Popup label for the <c>-1</c> sentinel that permits normal rotation.</summary>
    private const string RandomLabel = "Random (deck rotation)";

    /// <summary>Explains that selecting an Effect engages the real wall freeze rather than staging a cue.</summary>
    private static readonly GUIContent ControlLabel = new(
        "Effect / Hold",
        "Random permits normal rotation. Choosing an Effect pins it to the wall until Random is chosen again.");

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
                ? "[Controller] Effect Hold released — back to Random (deck rotation)."
                : $"[Controller] Effect held: {options[selectedRow]} (index {next}).");
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
            return "Random · normal rotation";
        }

        var heldEffect = ControllerStatusText.FormatHeldEffect(status);
        return status.IsStandaloneCadenceFrozen
            ? $"{heldEffect} · wall pinned · Standalone cadence frozen"
            : $"{heldEffect} · wall pinned";
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
