using UnityEditor;
using UnityEngine;

/// <summary>
/// Default Controller inspector that repaints every editor frame during Play Mode.
/// </summary>
/// <remarks>
/// Controller.beatManager and Director state change every frame at runtime, while a default inspector only repaints
/// on the editor's idle tick. Requesting constant repaints while playing lets the BeatManager dashboard and the
/// read-only Director/Phase/Switcher panel stay live without moving Director onto a separate scene object.
/// </remarks>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    /// <summary>Draws live Director/Switcher state before the normal serialized Controller fields.</summary>
    public override void OnInspectorGUI()
    {
        var controller = (Controller)target;
        DrawRuntimeStatus(controller);
        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }

    /// <summary>Repaints continuously only while playing, where BeatData and Director status change every frame.</summary>
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private static void DrawRuntimeStatus(Controller controller)
    {
        EditorGUILayout.LabelField("Runtime Observability", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live Director, Phase Lock, Switcher, and HUD state.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawDirectorStage(controller.DirectorStatus, controller.SwitcherStatus);
        EditorGUILayout.Space(6f);
        DrawPhaseLock(controller.DirectorStatus);
        EditorGUILayout.Space(6f);
        DrawHudLines(controller);

        EditorGUILayout.EndVertical();
    }

    private static void DrawDirectorStage(DirectorStatus directorStatus, SwitcherStatus switcherStatus)
    {
        EditorGUILayout.LabelField("DIRECTOR / STAGE", EditorStyles.boldLabel);
        DrawRow("Mode", directorStatus.Mode.ToString());
        DrawRow("Decision", directorStatus.Decision.ToString());
        DrawRow("Stage", switcherStatus.StageName);
        DrawRow("Current Effect", FormatIndexedName(switcherStatus.CurrentEffectIndex, switcherStatus.CurrentEffectName));
        DrawRow("Next Effect", FormatIndexedName(directorStatus.NextEffectIndex, directorStatus.NextEffectName));
        DrawRow("Source Effect", FormatIndexedName(switcherStatus.SourceEffectIndex, switcherStatus.SourceEffectName));
        DrawRow("Target Effect", FormatIndexedName(switcherStatus.TargetEffectIndex, switcherStatus.TargetEffectName));
        DrawRow("Active Transition", FormatIndexedName(switcherStatus.CurrentTransitionIndex, switcherStatus.CurrentTransitionName));
        DrawRow("Next Transition", FormatIndexedName(directorStatus.NextTransitionIndex, directorStatus.NextTransitionName));
        DrawRow("Hold Selected Effect", directorStatus.HoldSelectedEffect ? "On" : "Off");
        DrawRow("Hold Selected Transition", directorStatus.HoldSelectedTransition ? "On" : "Off");
        DrawRow("Current Beat", FormatBeat(directorStatus.CurrentBeat));
        DrawRow("Last Change", FormatBeat(directorStatus.LastChangeBeat));
        DrawRow("Landing", FormatBeat(directorStatus.TransitionLandingBeat));
        DrawRow("Landing In", FormatBeats(directorStatus.BeatsUntilLanding));
        DrawRow("Cadence Ready In", FormatBeats(directorStatus.BeatsUntilCadenceReady));
        DrawProgress("Transition Progress", switcherStatus.TransitionProgress);
    }

    private static void DrawPhaseLock(DirectorStatus status)
    {
        EditorGUILayout.LabelField("PHASE LOCK DETAILS", EditorStyles.boldLabel);
        DrawRow("Anchor", status.HasPhaseAnchor ? "locked" : "none");
        DrawRow("Confidence", status.PhaseAnchorConfidence.ToString());
        DrawRow("Phase", status.Phase.PhasePosition > 0 ? $"{status.Phase.PhasePosition} / 16" : "—");
        DrawRow("Bar In Phrase", FormatBeat(status.Phase.BarInPhrase));
        DrawRow("Beat In Bar", FormatBeat(status.Phase.BeatInBar));
        DrawRow("One Beat", FormatBeat(status.Phase.OneOfCurrentPhrase));
        DrawRow("Offset", FormatBeat(status.Phase.Offset));
        DrawRow("Clean Grid", status.Phase.CleanGrid ? "yes" : "no");
        DrawRow("Beat-In-Bar Agrees", status.Phase.BeatInBarAgrees ? "yes" : "no");
        DrawRow("Anchor Landing", FormatBeat(status.PhaseAnchorLandingBeat));
        DrawPhaseStrip(status.Phase.PhasePosition);
    }

    private static void DrawHudLines(Controller controller)
    {
        EditorGUILayout.LabelField("SCREEN HUD", EditorStyles.boldLabel);
        DrawRow("Top Line", controller.LastRuntimeHudLine);
        DrawRow("Detail Line", controller.LastRuntimeDetailLine);
        EditorGUILayout.LabelField("Render Debug", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(string.IsNullOrWhiteSpace(controller.LastRenderDebugText)
                ? "—"
                : controller.LastRenderDebugText, GUILayout.MinHeight(42f));
        }
    }

    private static void DrawPhaseStrip(int phasePosition)
    {
        EditorGUILayout.BeginHorizontal();
        var previousColor = GUI.backgroundColor;
        for (var i = 1; i <= PhaseClock.PhraseBeats; i++)
        {
            GUI.backgroundColor = i == phasePosition
                ? new Color(1f, 0.9f, 0.35f)
                : i == 1
                    ? new Color(0.25f, 0.95f, 1f)
                    : i >= 13
                        ? new Color(1f, 0.35f, 0.85f)
                        : previousColor;
            GUILayout.Label(i == 1 ? "X" : i.ToString(), EditorStyles.miniButton, GUILayout.MinWidth(22f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawProgress(string label, float value)
    {
        var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        var labelRect = rect;
        labelRect.width = EditorGUIUtility.labelWidth;
        EditorGUI.LabelField(labelRect, label);

        var barRect = rect;
        barRect.xMin += EditorGUIUtility.labelWidth;
        EditorGUI.ProgressBar(barRect, Mathf.Clamp01(value), $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%");
    }

    private static void DrawRow(string label, string value)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(label, string.IsNullOrWhiteSpace(value) ? "—" : value);
        }
    }

    private static string FormatIndexedName(int index, string name)
    {
        return index >= 0 ? $"{index}: {name}" : "—";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "—";
    }

    private static string FormatBeats(int beats)
    {
        return beats >= 0 ? $"{beats}b" : "—";
    }
}
