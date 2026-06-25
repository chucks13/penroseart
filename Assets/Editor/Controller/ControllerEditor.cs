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
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    private static readonly Color CurrentBeatColor = new Color(1f, 0.9f, 0.35f);
    private static readonly Color CueMarkColor = new Color(0.25f, 0.95f, 1f);
    private static readonly Color LockPointColor = new Color(1f, 0.55f, 0.2f);
    private static readonly Color RunwayColor = new Color(1f, 0.35f, 0.85f);
    private static readonly Color TailColor = new Color(0.25f, 0.8f, 0.45f);

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

    private readonly struct CueTimingOverlay
    {
        public static CueTimingOverlay None { get; } = new CueTimingOverlay(false, false, false, -1, -1, -1, -1, 0, 0);

        public readonly bool HasCue;
        public readonly bool IsLoaded;
        public readonly bool IsLocked;
        public readonly int CueMarkBeat;
        public readonly int LockPointBeat;
        public readonly int StartBeat;
        public readonly int CompleteBeat;
        public readonly int RunwayBeats;
        public readonly int TailBeats;

        private CueTimingOverlay(
            bool hasCue,
            bool isLoaded,
            bool isLocked,
            int cueMarkBeat,
            int lockPointBeat,
            int startBeat,
            int completeBeat,
            int runwayBeats,
            int tailBeats)
        {
            HasCue = hasCue;
            IsLoaded = isLoaded;
            IsLocked = isLocked;
            CueMarkBeat = cueMarkBeat;
            LockPointBeat = lockPointBeat;
            StartBeat = startBeat;
            CompleteBeat = completeBeat;
            RunwayBeats = runwayBeats;
            TailBeats = tailBeats;
        }

        public static CueTimingOverlay Loaded(SwitcherCueStatus status)
        {
            return new CueTimingOverlay(
                true,
                true,
                status.IsLocked,
                status.CueMarkBeat,
                status.LockPointBeat,
                status.StartBeat,
                status.CompleteBeat,
                status.RunwayBeats,
                status.TailBeats);
        }

        public static CueTimingOverlay Candidate(int cueMarkBeat, TransitionRepertoire repertoire)
        {
            var beatPlan = TransitionBeatPlan.FromCueMark(cueMarkBeat, repertoire);
            return new CueTimingOverlay(
                true,
                false,
                false,
                cueMarkBeat,
                beatPlan.StartBeat - 1,
                beatPlan.StartBeat,
                beatPlan.CompleteBeat,
                repertoire.RunwayBeats,
                repertoire.TailBeats);
        }
    }

    private enum PhaseSlotRole
    {
        None,
        Current,
        CueMark,
        LockPoint,
        Runway,
        Tail,
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
        var cueOverlay = ResolveCueTimingOverlay(controller, directorStatus, cueStatus);

        DrawTimingAndCueSheet(directorStatus, cueOverlay);
        EditorGUILayout.Space(6f);
        DrawDirectorIntent(directorStatus);
        EditorGUILayout.Space(6f);
        DrawLoadedCue(controller, directorStatus, cueStatus);
        EditorGUILayout.Space(6f);
        DrawSwitcherExecution(switcherStatus);
        EditorGUILayout.Space(6f);
        DrawAdvancedTiming(directorStatus);

        EditorGUILayout.EndVertical();
    }

    private static CueTimingOverlay ResolveCueTimingOverlay(Controller controller, DirectorStatus directorStatus, SwitcherCueStatus cueStatus)
    {
        if (cueStatus.HasCue)
        {
            return CueTimingOverlay.Loaded(cueStatus);
        }

        if (directorStatus.HasPhaseAnchor
            && TryGetTransitionRepertoire(controller, directorStatus.NextTransitionIndex, out var repertoire))
        {
            return CueTimingOverlay.Candidate(directorStatus.PhaseAnchorLandingBeat, repertoire);
        }

        return CueTimingOverlay.None;
    }

    private static void DrawTimingAndCueSheet(DirectorStatus status, CueTimingOverlay cueOverlay)
    {
        EditorGUILayout.LabelField("TIMING / CUE SHEET", EditorStyles.boldLabel);
        DrawRow("Now", $"Beat {FormatBeat(status.CurrentBeat)} · Phase Count {FormatPhaseCount(status.Phase.Position)}");
        DrawRow("Timing Source", FormatTimingSource(status));
        DrawCueTimingRows(status, cueOverlay);
        DrawPhaseStrip(status.Phase.Position, cueOverlay);
        DrawCueSheet(status.CueSheet, status.CurrentBeat);
    }

    private static void DrawCueTimingRows(DirectorStatus status, CueTimingOverlay cueOverlay)
    {
        if (!cueOverlay.HasCue)
        {
            DrawRow("Cue Timing", "—");
            return;
        }

        var source = cueOverlay.IsLoaded ? "Loaded Cue" : "Candidate Cue";
        DrawRow(source, $"Cue Mark {FormatBeat(cueOverlay.CueMarkBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.CueMarkBeat)})");
        DrawRow("Lock / Fire", $"Lock {FormatBeat(cueOverlay.LockPointBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.LockPointBeat)}) · Fire {FormatBeat(cueOverlay.StartBeat)} ({FormatBeatDelta(status.CurrentBeat, cueOverlay.StartBeat)})");
        DrawRow("Runway / Tail", $"{cueOverlay.RunwayBeats}b / {cueOverlay.TailBeats}b");
    }

    private static void DrawCueSheet(CueSheetStatus cueSheet, int currentBeat)
    {
        if (!cueSheet.HasSheet)
        {
            DrawRow("Cue Sheet", "—");
            return;
        }

        DrawRow("Phrase", $"{FormatBeat(cueSheet.PhraseStartBeat)} → {FormatBeat(cueSheet.PhraseEndBeat)} · {cueSheet.PhraseLengthBeats}b");
        DrawRow("Current Cue Mark", $"{FormatBeat(cueSheet.CurrentCueMarkBeat)} ({FormatBeatDelta(currentBeat, cueSheet.CurrentCueMarkBeat)})");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Cue Marks");
        var previousColor = GUI.backgroundColor;
        var offsets = cueSheet.CueMarkOffsets ?? System.Array.Empty<int>();
        for (var i = 0; i < offsets.Length; i++)
        {
            var offset = offsets[i];
            var absoluteBeat = cueSheet.PhraseStartBeat + offset;
            GUI.backgroundColor = i == cueSheet.CurrentCueMarkIndex
                ? CueMarkColor
                : absoluteBeat == currentBeat
                    ? CurrentBeatColor
                    : previousColor;
            GUILayout.Label(FormatCueSheetOffset(offset, cueSheet.PhraseLengthBeats), EditorStyles.miniButton, GUILayout.MinWidth(44f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawDirectorIntent(DirectorStatus status)
    {
        EditorGUILayout.LabelField("DIRECTOR INTENT", EditorStyles.boldLabel);
        DrawRow("Mode", status.Mode.ToString());
        DrawRow("Decision", status.Decision.ToString());
        DrawRow("Staged Effect", FormatIndexedName(status.NextEffectIndex, status.NextEffectName));
        DrawRow("Staged Transition", FormatIndexedName(status.NextTransitionIndex, status.NextTransitionName));
        DrawRow("Hold Selected Effect", status.HoldSelectedEffect ? "On" : "Off");
        DrawRow("Hold Selected Transition", status.HoldSelectedTransition ? "On" : "Off");
        DrawRow("Last Change", FormatBeat(status.LastChangeBeat));
        DrawRow("Last Sent Cue Mark", FormatBeat(status.TransitionLandingBeat));
        DrawRow("Cadence Ready In", FormatBeats(status.BeatsUntilCadenceReady));
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

    private static void DrawAdvancedTiming(DirectorStatus status)
    {
        showAdvancedTiming = EditorGUILayout.Foldout(showAdvancedTiming, "Advanced On-Air Timing", true);
        if (!showAdvancedTiming)
        {
            return;
        }

        DrawRow("Anchor", status.HasPhaseAnchor ? "locked" : "none");
        DrawRow("Lock State", status.HasPhaseAnchor ? status.Phase.State.ToString() : "unlocked");
        DrawRow("Offset", FormatBeat(status.Phase.Offset));
        DrawRow("Irregular Phrase", status.Phrase.IsIrregular ? "yes" : "no");
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

    private static void DrawPhaseStrip(int phasePosition, CueTimingOverlay cueOverlay)
    {
        EditorGUILayout.BeginHorizontal();
        var previousColor = GUI.backgroundColor;
        for (var i = 1; i <= PhaseGrid.PhraseBeats; i++)
        {
            var role = RoleForPhaseSlot(i, phasePosition, cueOverlay);
            GUI.backgroundColor = ColorForRole(role, i == phasePosition, previousColor);
            GUILayout.Label(FormatPhaseSlotLabel(i, phasePosition, role), EditorStyles.miniButton, GUILayout.MinWidth(28f));
        }

        GUI.backgroundColor = previousColor;
        EditorGUILayout.EndHorizontal();
    }

    private static PhaseSlotRole RoleForPhaseSlot(int slot, int phasePosition, CueTimingOverlay cueOverlay)
    {
        if (!cueOverlay.HasCue)
        {
            return slot == phasePosition ? PhaseSlotRole.Current : slot == 1 ? PhaseSlotRole.CueMark : PhaseSlotRole.None;
        }

        if (PhaseSlotForBeat(cueOverlay.CueMarkBeat, cueOverlay.CueMarkBeat) == slot)
        {
            return PhaseSlotRole.CueMark;
        }

        if (PhaseSlotForBeat(cueOverlay.LockPointBeat, cueOverlay.CueMarkBeat) == slot)
        {
            return PhaseSlotRole.LockPoint;
        }

        if (SlotContainsBeatRange(slot, cueOverlay.StartBeat, cueOverlay.CueMarkBeat - 1, cueOverlay.CueMarkBeat))
        {
            return PhaseSlotRole.Runway;
        }

        if (SlotContainsBeatRange(slot, cueOverlay.CueMarkBeat + 1, cueOverlay.CompleteBeat, cueOverlay.CueMarkBeat))
        {
            return PhaseSlotRole.Tail;
        }

        return slot == phasePosition ? PhaseSlotRole.Current : PhaseSlotRole.None;
    }

    private static bool SlotContainsBeatRange(int slot, int startBeat, int endBeat, int cueMarkBeat)
    {
        if (startBeat > endBeat)
        {
            return false;
        }

        for (var beat = startBeat; beat <= endBeat; beat++)
        {
            if (PhaseSlotForBeat(beat, cueMarkBeat) == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static int PhaseSlotForBeat(int beat, int cueMarkBeat)
    {
        var offset = (beat - cueMarkBeat) % PhaseGrid.PhraseBeats;
        if (offset < 0)
        {
            offset += PhaseGrid.PhraseBeats;
        }

        return offset + 1;
    }

    private static Color ColorForRole(PhaseSlotRole role, bool isCurrent, Color fallback)
    {
        // The current beat always wins: it must stay trackable on top of any cue role color
        // (runway / tail / lock point / cue mark), which would otherwise paint over it.
        if (isCurrent)
        {
            return CurrentBeatColor;
        }

        switch (role)
        {
            case PhaseSlotRole.CueMark:
                return CueMarkColor;
            case PhaseSlotRole.LockPoint:
                return LockPointColor;
            case PhaseSlotRole.Runway:
                return RunwayColor;
            case PhaseSlotRole.Tail:
                return TailColor;
            default:
                return fallback;
        }
    }

    private static string FormatPhaseSlotLabel(int slot, int phasePosition, PhaseSlotRole role)
    {
        return role == PhaseSlotRole.LockPoint ? "L" : slot == 1 ? "X" : slot.ToString();
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

    private static bool TryGetTransitionRepertoire(Controller controller, int index, out TransitionRepertoire repertoire)
    {
        if (controller.transitions != null && index >= 0 && index < controller.transitions.Length && controller.transitions[index] != null)
        {
            repertoire = controller.transitions[index].Repertoire;
            return true;
        }

        repertoire = default;
        return false;
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

    private static string FormatTimingSource(DirectorStatus status)
    {
        var source = status.TimingSource.ToString();
        return status.TimingReanchored ? $"Re-anchor {source}" : source;
    }

    private static string FormatPhaseCount(int phasePosition)
    {
        return phasePosition > 0 ? $"{phasePosition} / {PhaseGrid.PhraseBeats}" : "—";
    }

    private static string FormatBeat(int beat)
    {
        return beat >= 0 && beat != int.MinValue ? beat.ToString() : "—";
    }

    private static string FormatBeats(int beats)
    {
        return beats >= 0 ? $"{beats}b" : "—";
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
