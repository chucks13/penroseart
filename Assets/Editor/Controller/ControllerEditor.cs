using UnityEditor;
using UnityEngine;

/// <summary>
/// Compact Controller inspector for scene wiring, essential configuration, runtime health, and Tuning Window access.
/// </summary>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    /// <summary>Serialized properties owned by the canonical Tuning Window instead of the compact Inspector.</summary>
    private static readonly string[] TuningWindowProperties = { "m_Script", "beatManager" };

    /// <summary>Draws the workspace entry, concise runtime health, and remaining scene/configuration properties.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawTuningWindowEntry();
        EditorGUILayout.Space(6f);
        DrawRuntimeHealth((Controller)target);
        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Scene & Configuration", EditorStyles.boldLabel);
        DrawPropertiesExcluding(serializedObject, TuningWindowProperties);
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>Repaints only while Play Mode can change the compact runtime health summary.</summary>
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    /// <summary>Provides one reliable action from the compact Inspector to the canonical workspace.</summary>
    private static void DrawTuningWindowEntry()
    {
        EditorGUILayout.LabelField("Tuning Workspace", EditorStyles.boldLabel);
        using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Live sequencing, rhythm detail, and Transition tuning live in the Tuning Window.",
            EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Open Tuning Window"))
        {
            PenroseTuningWindow.Open();
        }
    }

    /// <summary>Draws availability-honest runtime state without duplicating the detailed live Observatory.</summary>
    private static void DrawRuntimeHealth(Controller controller)
    {
        EditorGUILayout.LabelField("Runtime Health", EditorStyles.boldLabel);
        using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Mode", "Edit Mode");
            EditorGUILayout.LabelField("Controller", "Ready for Play Mode");
            return;
        }

        if (!LiveControllerAccess.TryGet(out var liveController) || liveController != controller)
        {
            EditorGUILayout.LabelField("Mode", "Play Mode");
            EditorGUILayout.LabelField("Controller", "Not ready");
            return;
        }

        var director = liveController.DirectorStatus;
        var switcher = liveController.SwitcherStatus;
        if (director.Mode == DirectorMode.NotReady)
        {
            EditorGUILayout.LabelField("Mode", "Play Mode");
            EditorGUILayout.LabelField("Controller", "Initializing");
            return;
        }

        EditorGUILayout.LabelField("Mode", director.IsSyncedMode ? "Synced Mode" : "Standalone Mode");
        EditorGUILayout.LabelField("Director Next", FormatDirectorNext(director));
        EditorGUILayout.LabelField(
            "Hold Selected",
            $"Effect {(director.HoldSelectedEffect ? "On" : "Off")} · Transition {(director.HoldSelectedTransition ? "On" : "Off")}");
        EditorGUILayout.LabelField("Held Effect", FormatHeldEffect(liveController));
        EditorGUILayout.LabelField("Switcher Active", FormatSwitcherActive(switcher));
    }

    /// <summary>Formats the Director's staged Effect and Transition as one concise future-intent row.</summary>
    private static string FormatDirectorNext(DirectorStatus status)
    {
        return $"{FormatCatalogChoice(status.NextEffectIndex, status.NextEffectName)} · " +
            FormatCatalogChoice(status.NextTransitionIndex, status.NextTransitionName);
    }

    /// <summary>Formats the current Effect hold independently from the editor's Hold Selected behavior.</summary>
    private static string FormatHeldEffect(Controller controller)
    {
        if (!controller.TryGetHeldEffectIndex(out var effectIndex))
        {
            return "Random";
        }

        return controller.effects != null && effectIndex < controller.effects.Length && controller.effects[effectIndex] != null
            ? controller.effects[effectIndex].GetType().Name
            : $"Effect {effectIndex}";
    }

    /// <summary>Formats the Switcher's current Effect or active Transition without implying future intent.</summary>
    private static string FormatSwitcherActive(SwitcherStatus status)
    {
        if (!status.Ready)
        {
            return "Unavailable";
        }

        return status.CurrentTransitionIndex >= 0
            ? $"{FormatCatalogChoice(status.CurrentTransitionIndex, status.CurrentTransitionName)} · {status.TransitionProgress:P0}"
            : FormatCatalogChoice(status.CurrentEffectIndex, status.CurrentEffectName);
    }

    /// <summary>Formats a catalog identity while retaining its stable runtime index.</summary>
    private static string FormatCatalogChoice(int index, string name)
    {
        if (index < 0)
        {
            return "Unavailable";
        }

        return string.IsNullOrWhiteSpace(name) ? $"#{index}" : $"{name} (#{index})";
    }
}
