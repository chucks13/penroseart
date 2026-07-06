using UnityEditor;
using UnityEngine;

/// <summary>
/// Default Controller inspector that repaints every editor frame during Play Mode.
/// </summary>
/// <remarks>
/// Controller.beatManager and Director state change every frame at runtime, while a default inspector only repaints
/// on the editor's idle tick. Requesting constant repaints while playing lets the BeatManager dashboard and the
/// read-only Director, Cue Sheet, Loaded Cue, and Switcher execution panel stay live without moving Director onto
/// a separate scene object.
/// </remarks>
/// <remarks>
/// Reduced to the Director reducer's real state (ADR-0011): the two Cue Sheets, the staged move, the Switcher's
/// loaded cue, and the wire-fed timing lanes. The decision-memory and candidate-window views are gone with the
/// planning machinery; the richer Observatory is rebuilt on this real state in a later step.
/// </remarks>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    private static readonly Color CurrentBeatColor = new Color(1f, 0.9f, 0.35f);
    private static readonly Color CueMarkColor = new Color(0.25f, 0.95f, 1f);

    private static bool showDirectorObservatory = false;
    private static bool showAdvancedTiming = false;

    /// <summary>Draws live HUD/debug state first, then the optional Director Observatory before serialized fields.</summary>
    public override void OnInspectorGUI()
    {
        var controller = (Controller)target;
        DrawRuntimeDebug(controller);
        EditorGUILayout.Space(6f);
        DrawDirectorObservatory(controller);
        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }

    /// <summary>Repaints continuously only while playing, where BeatData and Director status change every frame.</summary>
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private static void DrawRuntimeDebug(Controller controller)
    {
        EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawHudLines(controller);
        EditorGUILayout.EndVertical();
    }

    private static void DrawDirectorObservatory(Controller controller)
    {
        showDirectorObservatory = EditorGUILayout.Foldout(showDirectorObservatory, "Director Observatory", true);
        if (!showDirectorObservatory)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live Director, Cue Sheet, Loaded Cue, and Switcher state.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var directorStatus = controller.DirectorStatus;
        var switcherStatus = controller.SwitcherStatus;
        var cueStatus = controller.SwitcherLoadedCueStatus;

        DrawDirectorIntent(controller, directorStatus);
        EditorGUILayout.Space(6f);
        DrawCueSheets(directorStatus);
        EditorGUILayout.Space(6f);
        DrawLoadedCue(controller, directorStatus, cueStatus);
        EditorGUILayout.Space(6f);
        DrawSwitcherExecution(switcherStatus);
        EditorGUILayout.Space(6f);
        DrawAdvancedTiming(controller, directorStatus);

        EditorGUILayout.EndVertical();
    }

    private static void DrawDirectorIntent(Controller controller, DirectorStatus status)
    {
        var gridCount = controller.beatManager?.Grid is { } grid ? grid.Beat : -1;
        EditorGUILayout.LabelField("DIRECTOR", EditorStyles.boldLabel);
        DrawRow("Mode", status.Mode.ToString());
        DrawRow("Now", $"Beat {FormatBeat(status.CurrentBeat)} · Grid Count {FormatGridCount(gridCount)}");
        DrawRow("Staged Effect", FormatIndexedName(status.NextEffectIndex, status.NextEffectName));
        DrawRow("Staged Transition", FormatIndexedName(status.NextTransitionIndex, status.NextTransitionName));
        DrawRow("Hold Selected Effect", status.HoldSelectedEffect ? "On" : "Off");
        DrawRow("Hold Selected Transition", status.HoldSelectedTransition ? "On" : "Off");
    }

    private static void DrawCueSheets(DirectorStatus status)
    {
        EditorGUILayout.LabelField("CUE SHEETS", EditorStyles.boldLabel);
        DrawCueSheet("Current", status.CurrentSheet, status.CurrentBeat);
        DrawCueSheet("Next", status.NextSheet, status.CurrentBeat);
    }

    private static void DrawCueSheet(string label, CueSheetView cueSheet, int currentBeat)
    {
        if (!cueSheet.HasSheet)
        {
            DrawRow(label, "—");
            return;
        }

        DrawRow(label, $"{FormatBeat(cueSheet.PhraseStartBeat)} → {FormatBeat(cueSheet.PhraseEndBeat)} · {cueSheet.PhraseLengthBeats}b");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Cue Marks");
        var previousColor = GUI.backgroundColor;
        var offsets = cueSheet.CueMarkOffsets ?? System.Array.Empty<int>();
        foreach (var offset in offsets)
        {
            var absoluteBeat = cueSheet.PhraseStartBeat + offset;
            GUI.backgroundColor = absoluteBeat == currentBeat ? CurrentBeatColor : CueMarkColor;
            GUILayout.Label(FormatCueSheetOffset(offset, cueSheet.PhraseLengthBeats), EditorStyles.miniButton, GUILayout.MinWidth(44f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawLoadedCue(Controller controller, DirectorStatus directorStatus, SwitcherCueStatus cueStatus)
    {
        EditorGUILayout.LabelField("SWITCHER LOADED CUE", EditorStyles.boldLabel);
        if (!cueStatus.HasCue)
        {
            DrawRow("Loaded Cue", "none");
            return;
        }

        DrawRow("Loaded Cue", cueStatus.IsLocked ? "locked" : "mutable");
        DrawRow("Can Update", cueStatus.CanUpdate ? "yes" : "no");
        DrawRow("Cue Mark", $"{FormatBeat(cueStatus.CueMarkBeat)} ({FormatBeatDelta(directorStatus.CurrentBeat, cueStatus.CueMarkBeat)})");
        DrawRow("Lock Point", $"{FormatBeat(cueStatus.LockPointBeat)} ({FormatBeatDelta(directorStatus.CurrentBeat, cueStatus.LockPointBeat)})");
        DrawRow("Fire / Complete", $"{FormatBeat(cueStatus.StartBeat)} → {FormatBeat(cueStatus.CompleteBeat)}");
        DrawRow("Runway / Tail", $"{cueStatus.RunwayBeats}b / {cueStatus.TailBeats}b");
        DrawRow("Target Effect", FormatIndexedName(cueStatus.TargetEffectIndex, EffectName(controller, cueStatus.TargetEffectIndex)));
        DrawRow("Transition", FormatIndexedName(cueStatus.TransitionIndex, TransitionName(controller, cueStatus.TransitionIndex)));
    }

    private static void DrawSwitcherExecution(SwitcherStatus switcherStatus)
    {
        EditorGUILayout.LabelField("SWITCHER EXECUTION", EditorStyles.boldLabel);
        DrawRow("Stage", switcherStatus.StageName);
        if (switcherStatus.CurrentEffectIndex >= 0)
        {
            DrawRow("Current Effect", FormatIndexedName(switcherStatus.CurrentEffectIndex, switcherStatus.CurrentEffectName));
        }
        else
        {
            DrawRow("Source → Target", $"{FormatIndexedName(switcherStatus.SourceEffectIndex, switcherStatus.SourceEffectName)} → {FormatIndexedName(switcherStatus.TargetEffectIndex, switcherStatus.TargetEffectName)}");
            DrawRow("Active Transition", FormatIndexedName(switcherStatus.CurrentTransitionIndex, switcherStatus.CurrentTransitionName));
        }

        DrawProgress("Transition Progress", switcherStatus.TransitionProgress);
    }

    private static void DrawAdvancedTiming(Controller controller, DirectorStatus status)
    {
        showAdvancedTiming = EditorGUILayout.Foldout(showAdvancedTiming, "Advanced On-Air Timing", true);
        if (!showAdvancedTiming)
        {
            return;
        }

        var beatManager = controller.beatManager;
        var grid = beatManager?.Grid;
        DrawRow("Grid State", grid is { } g ? g.State.ToString() : "unlocked");
        DrawRow("Grid Beat", grid is { } gc ? $"{gc.Beat}/{CueSheet.GridBeats}" : "—");
        DrawRow("Grid Bar", grid is { } gb ? $"{gb.Bar}/4" : "—");
        DrawRow("Phrase", FormatPhraseRow(beatManager?.Phrase));
        DrawRow("Next Phrase", FormatNextPhraseRow(beatManager?.NextPhrase));
        DrawRow("Irregular Phrase", beatManager?.Phrase?.irregular == true ? "yes" : "no");
        DrawRow("Energy", FormatEnergyRow(beatManager?.Energy));
        DrawRow("Loop", FormatLoopRow(beatManager?.Loop));
    }

    /// <summary>Current phrase as <c>label · elapsed/length</c>, from the wire-fed <see cref="BeatManager.Phrase"/>.</summary>
    private static string FormatPhraseRow(PhraseInfo? info)
    {
        if (!(info is { } phrase))
        {
            return "—";
        }

        var span = phrase.lengthBeats is { } length && phrase.beatsUntilNext is { } untilNext
            ? $" · {Mathf.Max(0, length - untilNext)}/{length}"
            : string.Empty;
        return $"{phrase.label}{span}";
    }

    /// <summary>Upcoming phrase as <c>label · in Nb · Nb long</c>, from <see cref="BeatManager.NextPhrase"/>.</summary>
    private static string FormatNextPhraseRow(NextPhraseInfo? info)
    {
        if (!(info is { } next))
        {
            return "—";
        }

        var untilChange = next.beatsUntilChange is { } beats ? $" · in {beats}b" : string.Empty;
        var length = next.lengthBeats is { } len ? $" · {len}b long" : string.Empty;
        return $"{next.label}{untilChange}{length}";
    }

    /// <summary>Energy as <c>level (→ next in Nb)</c>, from <see cref="BeatManager.Energy"/>.</summary>
    private static string FormatEnergyRow(EnergyInfo? info)
    {
        if (!(info is { } energy))
        {
            return "—";
        }

        if (energy.next is { } next)
        {
            var untilChange = energy.beatsUntilChange is { } beats ? $" in {beats}b" : string.Empty;
            return $"{energy.level} (→ {next}{untilChange})";
        }

        return energy.level.ToString();
    }

    /// <summary>Loop as <c>rolling/set/off · Nb</c>, from the display-only <see cref="BeatManager.Loop"/>.</summary>
    private static string FormatLoopRow(LoopInfo? info)
    {
        if (!(info is { } loop))
        {
            return "—";
        }

        var state = loop.looping ? "rolling" : loop.regionSet ? "set" : "off";
        var length = loop.lengthBeats is { } beats ? $" · {beats:0.##}b" : string.Empty;
        return $"{state}{length}";
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
                : controller.LastRenderDebugText, GUILayout.MinHeight(72f));
        }
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
        var displayName = string.IsNullOrWhiteSpace(name) ? "—" : name;
        return index >= 0 ? $"{index}: {displayName}" : "—";
    }

    private static string EffectName(Controller controller, int index)
    {
        return controller.effects != null && index >= 0 && index < controller.effects.Length && controller.effects[index] != null
            ? controller.effects[index].Name
            : string.Empty;
    }

    private static string TransitionName(Controller controller, int index)
    {
        return controller.transitions != null && index >= 0 && index < controller.transitions.Length && controller.transitions[index] != null
            ? controller.transitions[index].Name
            : string.Empty;
    }

    private static string FormatGridCount(int gridPosition)
    {
        return gridPosition > 0 ? $"{gridPosition} / {CueSheet.GridBeats}" : "—";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "—";
    }

    private static string FormatBeatDelta(int currentBeat, int targetBeat)
    {
        if (currentBeat < 0 || targetBeat < 0 || currentBeat == int.MinValue || targetBeat == int.MinValue)
        {
            return "—";
        }

        var delta = targetBeat - currentBeat;
        return delta >= 0 ? $"+{delta}b" : $"{delta}b";
    }

    private static string FormatCueSheetOffset(int offset, int phraseLengthBeats)
    {
        if (offset == 0)
        {
            return "X";
        }

        return offset == phraseLengthBeats ? "End" : $"+{offset}";
    }
}
